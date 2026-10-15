using ChurrascariaPDV.Domain.Entities;
using ChurrascariaPDV.Domain.Enums;
using ChurrascariaPDV.Infrastructure.Data;
using ChurrascariaPDV.Services.Impressao;
using ChurrascariaPDV.Services.Notificacoes;
using Microsoft.EntityFrameworkCore;

namespace ChurrascariaPDV.Services;

public record ItemNovoPedido(int ProdutoId, decimal Quantidade, IReadOnlyList<int> OpcoesIds, string? Observacao = null);

public record NovoPedido(int MesaId, int? ComandaId, int UsuarioId, IReadOnlyList<ItemNovoPedido> Itens);

public record ResultadoPedido(int PedidoId, IReadOnlyList<SetorImpressao> SetoresImpressos, IReadOnlyList<SetorImpressao> SetoresComFalha)
{
    public bool ImpressoComSucesso => SetoresComFalha.Count == 0;
}

public class PedidoService(
    IDbContextFactory<AppDbContext> dbFactory,
    ImpressaoService impressao,
    NotificadorMesas notificador)
{
    private const decimal QuantidadeMaxima = 999;

    /// <summary>
    /// Lança um pedido na mesa/comanda. Abre a mesa automaticamente quando estiver livre
    /// e envia um ticket para cada setor de produção envolvido.
    /// </summary>
    public async Task<ResultadoPedido> LancarPedidoAsync(NovoPedido novo)
    {
        if (novo.Itens.Count == 0)
        {
            throw new RegraNegocioException("Adicione ao menos um item ao pedido.");
        }

        if (novo.Itens.Any(i => i.Quantidade <= 0 || i.Quantidade > QuantidadeMaxima))
        {
            throw new RegraNegocioException($"As quantidades devem estar entre 1 e {QuantidadeMaxima}.");
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        await using var transacao = await db.Database.BeginTransactionAsync();

        var garcom = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == novo.UsuarioId && u.Ativo)
                     ?? throw new RegraNegocioException("Usuário inválido. Faça login novamente.");
        var mesa = await ContaMesa.ObterMesaAsync(db, novo.MesaId);

        var produtoIds = novo.Itens.Select(i => i.ProdutoId).Distinct().ToList();
        var produtos = await db.Produtos
            .Include(p => p.GruposModificadores).ThenInclude(g => g.GrupoModificador).ThenInclude(g => g.Opcoes)
            .Where(p => produtoIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var pedido = new Pedido { MesaId = mesa.Id, UsuarioGarcomId = garcom.Id, DataHoraLancamento = DateTime.Now };
        foreach (var item in novo.Itens)
        {
            if (!produtos.TryGetValue(item.ProdutoId, out var produto) || !produto.Ativo)
            {
                throw new RegraNegocioException("Um dos produtos do pedido não está mais disponível.");
            }

            pedido.Itens.Add(new ItemPedido
            {
                ProdutoId = produto.Id,
                Produto = produto,
                Quantidade = item.Quantidade,
                PrecoUnitarioHistorico = produto.Preco,
                ObservacoesModificadores = MontarObservacoes(produto, item)
            });
        }

        var pessoasRodizio = (int)pedido.Itens.Where(i => i.Produto.TipoItem == TipoItem.Rodizio).Sum(i => i.Quantidade);
        ContaMesa.Ocupar(mesa, pessoasRodizio);
        if (mesa.Status == StatusMesa.ContaSolicitada)
        {
            // Consumo novo depois da pré-conta: a conferência impressa deixou de valer.
            mesa.Status = StatusMesa.Ocupada;
        }

        Comanda comanda;
        if (novo.ComandaId is { } comandaId)
        {
            comanda = await db.Comandas.FirstOrDefaultAsync(c => c.Id == comandaId && c.MesaId == mesa.Id && c.Status == StatusComanda.Aberta)
                      ?? throw new RegraNegocioException("A comanda selecionada não está aberta nesta mesa.");
        }
        else
        {
            comanda = await ContaMesa.ObterOuCriarContaGeralAsync(db, mesa);
        }

        pedido.Comanda = comanda;
        db.Pedidos.Add(pedido);
        await ContaMesa.AtualizarValorAsync(db, mesa);
        await transacao.CommitAsync();

        var (impressos, falhas) = await ImprimirProducaoAsync(mesa.Numero, comanda.Descricao, garcom.Nome, pedido, reimpressao: false);
        pedido.ImpressoNosSetores = falhas.Count == 0;
        await db.SaveChangesAsync();

        await notificador.NotificarAsync(mesa.Id);
        return new ResultadoPedido(pedido.Id, impressos, falhas);
    }

    public async Task<ResultadoPedido> ReimprimirPedidoAsync(int pedidoId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var pedido = await db.Pedidos
            .Include(p => p.Itens).ThenInclude(i => i.Produto)
            .Include(p => p.Mesa)
            .Include(p => p.Comanda)
            .Include(p => p.UsuarioGarcom)
            .FirstOrDefaultAsync(p => p.Id == pedidoId)
            ?? throw new RegraNegocioException("Pedido não encontrado.");

        var (impressos, falhas) = await ImprimirProducaoAsync(pedido.Mesa.Numero, pedido.Comanda.Descricao,
            pedido.UsuarioGarcom.Nome, pedido, reimpressao: true);
        if (falhas.Count == 0 && !pedido.ImpressoNosSetores)
        {
            pedido.ImpressoNosSetores = true;
            await db.SaveChangesAsync();
            await notificador.NotificarAsync(pedido.MesaId);
        }

        return new ResultadoPedido(pedido.Id, impressos, falhas);
    }

    /// <summary>Cancela (total ou parcialmente) um item já lançado. Exige PIN de gerente e motivo.</summary>
    public async Task CancelarItemAsync(int itemId, decimal quantidade, string? pinGerente, string? motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length < 3)
        {
            throw new RegraNegocioException("Informe o motivo do cancelamento.");
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        await using var transacao = await db.Database.BeginTransactionAsync();
        var gerente = await UsuarioService.ValidarGerenteAsync(db, pinGerente);

        var item = await db.ItensPedido.Include(i => i.Produto).FirstOrDefaultAsync(i => i.Id == itemId)
                   ?? throw new RegraNegocioException("Item não encontrado.");
        if (item.Cancelado)
        {
            throw new RegraNegocioException("Este item já foi cancelado.");
        }

        if (quantidade <= 0 || quantidade > item.Quantidade)
        {
            throw new RegraNegocioException($"Informe uma quantidade entre 1 e {TicketsImpressao.Quantidade(item.Quantidade)}.");
        }

        var pedido = await db.Pedidos.Include(p => p.Mesa).Include(p => p.Itens).FirstAsync(p => p.Id == item.PedidoId);
        var comanda = await ContaMesa.ComandasCompletas(db).FirstAsync(c => c.Id == pedido.ComandaId);
        if (comanda.Status != StatusComanda.Aberta)
        {
            throw new RegraNegocioException("A conta deste item já foi fechada.");
        }

        var cancelado = item;
        if (quantidade < item.Quantidade)
        {
            item.Quantidade -= quantidade;
            cancelado = new ItemPedido
            {
                PedidoId = item.PedidoId,
                ProdutoId = item.ProdutoId,
                Produto = item.Produto,
                Quantidade = quantidade,
                PrecoUnitarioHistorico = item.PrecoUnitarioHistorico,
                ObservacoesModificadores = item.ObservacoesModificadores
            };
            pedido.Itens.Add(cancelado);
        }

        cancelado.Cancelado = true;
        cancelado.MotivoCancelamento = motivo.Trim()[..Math.Min(250, motivo.Trim().Length)];
        cancelado.AutorizadoPorGerente = true;
        cancelado.UsuarioGerenteAutorizacaoId = gerente.Id;
        cancelado.DataHoraCancelamento = DateTime.Now;

        if (pedido.Itens.All(i => i.Cancelado))
        {
            pedido.Status = StatusPedido.Cancelado;
        }

        ContaMesa.GarantirSaldoNaoNegativo(comanda);
        EstoqueService.RegistrarPerdaPorCancelamento(db, cancelado, gerente.Id, cancelado.MotivoCancelamento);
        await ContaMesa.AtualizarValorAsync(db, pedido.Mesa);
        await transacao.CommitAsync();

        if (item.Produto.SetorImpressao != SetorImpressao.Nenhum && pedido.ImpressoNosSetores)
        {
            var documento = TicketsImpressao.Cancelamento(impressao.NovoDocumento(), item.Produto.SetorImpressao,
                pedido.Mesa.Numero, comanda.Descricao, gerente.Nome, cancelado.MotivoCancelamento,
                new LinhaTicketProducao(quantidade, item.Produto.Nome, item.ObservacoesModificadores));
            await impressao.ImprimirAsync(item.Produto.SetorImpressao, $"Cancelamento mesa {pedido.Mesa.Numero}", documento);
        }

        await notificador.NotificarAsync(pedido.MesaId);
    }

    /// <summary>Transfere um item (ou parte dele) para outra comanda, da mesma mesa ou de outra.</summary>
    public async Task TransferirItemAsync(int itemId, decimal quantidade, int mesaDestinoId, int? comandaDestinoId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await using var transacao = await db.Database.BeginTransactionAsync();

        var item = await db.ItensPedido.FirstOrDefaultAsync(i => i.Id == itemId)
                   ?? throw new RegraNegocioException("Item não encontrado.");
        if (item.Cancelado)
        {
            throw new RegraNegocioException("Não é possível transferir um item cancelado.");
        }

        if (quantidade <= 0 || quantidade > item.Quantidade)
        {
            throw new RegraNegocioException($"Informe uma quantidade entre 1 e {TicketsImpressao.Quantidade(item.Quantidade)}.");
        }

        var pedidoOrigem = await db.Pedidos.Include(p => p.Itens).Include(p => p.Mesa).FirstAsync(p => p.Id == item.PedidoId);
        var comandaOrigem = await ContaMesa.ComandasCompletas(db).FirstAsync(c => c.Id == pedidoOrigem.ComandaId);
        if (comandaOrigem.Status != StatusComanda.Aberta)
        {
            throw new RegraNegocioException("A conta de origem já foi fechada.");
        }

        var mesaDestino = await ContaMesa.ObterMesaAsync(db, mesaDestinoId);
        Comanda comandaDestino;
        if (comandaDestinoId is { } id)
        {
            comandaDestino = await db.Comandas.FirstOrDefaultAsync(c => c.Id == id && c.MesaId == mesaDestino.Id && c.Status == StatusComanda.Aberta)
                             ?? throw new RegraNegocioException("A comanda de destino não está aberta.");
        }
        else
        {
            ContaMesa.Ocupar(mesaDestino, 1);
            comandaDestino = await ContaMesa.ObterOuCriarContaGeralAsync(db, mesaDestino);
        }

        if (comandaDestino.Id != 0 && comandaDestino.Id == comandaOrigem.Id)
        {
            throw new RegraNegocioException("O item já está nessa comanda.");
        }

        var pedidoDestino = new Pedido
        {
            MesaId = mesaDestino.Id,
            Comanda = comandaDestino,
            UsuarioGarcomId = pedidoOrigem.UsuarioGarcomId,
            DataHoraLancamento = pedidoOrigem.DataHoraLancamento,
            ImpressoNosSetores = true
        };
        db.Pedidos.Add(pedidoDestino);

        if (quantidade < item.Quantidade)
        {
            item.Quantidade -= quantidade;
            pedidoDestino.Itens.Add(new ItemPedido
            {
                ProdutoId = item.ProdutoId,
                Quantidade = quantidade,
                PrecoUnitarioHistorico = item.PrecoUnitarioHistorico,
                ObservacoesModificadores = item.ObservacoesModificadores
            });
        }
        else
        {
            item.Pedido = pedidoDestino;
            db.ChangeTracker.DetectChanges();
            if (pedidoOrigem.Itens.Count == 0)
            {
                db.Pedidos.Remove(pedidoOrigem);
            }
        }

        await db.SaveChangesAsync();
        var comandaOrigemAtualizada = await ContaMesa.ComandasCompletas(db).FirstAsync(c => c.Id == comandaOrigem.Id);
        ContaMesa.GarantirSaldoNaoNegativo(comandaOrigemAtualizada);

        await ContaMesa.AtualizarValorAsync(db, pedidoOrigem.Mesa);
        if (mesaDestino.Id != pedidoOrigem.MesaId)
        {
            await ContaMesa.AtualizarValorAsync(db, mesaDestino);
        }

        await transacao.CommitAsync();
        await notificador.NotificarAsync(pedidoOrigem.MesaId, mesaDestino.Id);
    }

    private static string? MontarObservacoes(Produto produto, ItemNovoPedido item)
    {
        var opcoesSelecionadas = item.OpcoesIds.Distinct().ToHashSet();
        var partes = new List<string>();

        foreach (var grupo in produto.GruposModificadores.Select(g => g.GrupoModificador).OrderByDescending(g => g.Obrigatorio).ThenBy(g => g.Nome))
        {
            var escolhidas = grupo.Opcoes.Where(o => opcoesSelecionadas.Remove(o.Id)).OrderBy(o => o.Id).ToList();
            if (grupo.Obrigatorio && escolhidas.Count == 0)
            {
                throw new RegraNegocioException($"Escolha \"{grupo.Nome}\" para {produto.Nome}.");
            }

            if (grupo.SelecaoUnica && escolhidas.Count > 1)
            {
                throw new RegraNegocioException($"Escolha apenas uma opção de \"{grupo.Nome}\" para {produto.Nome}.");
            }

            partes.AddRange(escolhidas.Select(o => o.Nome));
        }

        if (opcoesSelecionadas.Count > 0)
        {
            throw new RegraNegocioException($"Há opções inválidas selecionadas para {produto.Nome}.");
        }

        if (!string.IsNullOrWhiteSpace(item.Observacao))
        {
            partes.Add(item.Observacao.Trim());
        }

        var texto = string.Join(", ", partes);
        return texto.Length == 0 ? null : texto[..Math.Min(500, texto.Length)];
    }

    private async Task<(List<SetorImpressao> Impressos, List<SetorImpressao> Falhas)> ImprimirProducaoAsync(
        int numeroMesa, string comanda, string garcom, Pedido pedido, bool reimpressao)
    {
        var impressos = new List<SetorImpressao>();
        var falhas = new List<SetorImpressao>();

        // Rodízio (setor Nenhum) só entra no saldo da mesa: não gasta papel na produção.
        var porSetor = pedido.Itens
            .Where(i => !i.Cancelado && i.Produto.SetorImpressao != SetorImpressao.Nenhum)
            .GroupBy(i => i.Produto.SetorImpressao)
            .OrderBy(g => g.Key);

        foreach (var grupo in porSetor)
        {
            var documento = TicketsImpressao.Producao(impressao.NovoDocumento(), grupo.Key, numeroMesa, comanda, garcom,
                pedido.DataHoraLancamento, pedido.Id,
                grupo.Select(i => new LinhaTicketProducao(i.Quantidade, i.Produto.Nome, i.ObservacoesModificadores)),
                reimpressao);

            var sucesso = await impressao.ImprimirAsync(grupo.Key, $"Pedido #{pedido.Id} mesa {numeroMesa}", documento);
            (sucesso ? impressos : falhas).Add(grupo.Key);
        }

        return (impressos, falhas);
    }
}
