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
    NotificadorMesas notificador,
    ProtecaoPin? protecao = null)
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
        var perfil = await PerfilEstabelecimentoAtual.ObterAsync(db);
        await using var transacao = await db.Database.BeginTransactionAsync();

        var garcom = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == novo.UsuarioId && u.Ativo && !u.TrocaPinObrigatoria)
                     ?? throw new RegraNegocioException("Usuário inválido. Faça login novamente.");
        var mesa = await ContaMesa.ObterMesaAsync(db, novo.MesaId);

        var produtoIds = novo.Itens.Select(i => i.ProdutoId).Distinct().ToList();
        var produtos = await db.Produtos
            .Include(p => p.Categoria)
            .Include(p => p.SetorImpressao)
            .Include(p => p.GruposModificadores).ThenInclude(g => g.GrupoModificador).ThenInclude(g => g.Opcoes)
            .Include(p => p.GruposModificadores).ThenInclude(g => g.GrupoModificador).ThenInclude(g => g.SetorImpressao)
            .Where(p => produtoIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var pedido = new Pedido { MesaId = mesa.Id, UsuarioGarcomId = garcom.Id, DataHoraLancamento = DateTime.Now };
        var pessoasSugeridas = 0m;
        foreach (var item in novo.Itens)
        {
            if (!produtos.TryGetValue(item.ProdutoId, out var produto) || !produto.Ativo)
            {
                throw new RegraNegocioException("Um dos produtos do pedido não está mais disponível.");
            }

            var regras = AplicarModificadores(produto, item);
            if (regras.SugerePessoas)
            {
                pessoasSugeridas += item.Quantidade;
            }

            pedido.Itens.Add(new ItemPedido
            {
                ProdutoId = produto.Id,
                Produto = produto,
                Quantidade = item.Quantidade,
                PrecoUnitarioHistorico = CalculoPrecoProduto.Calcular(CardapioService.Mapear(produto), item.OpcoesIds),
                ObservacoesModificadores = regras.Observacoes,
                SetorImpressao = regras.Setor,
                SetorImpressaoId = regras.Setor?.Id,
                DestacarObservacoes = regras.DestacarObservacoes
            });
        }

        ContaMesa.Ocupar(mesa, (int)pessoasSugeridas);
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
        await ContaMesa.AtualizarValorAsync(db, mesa, perfil);
        await transacao.CommitAsync();

        var (impressos, falhas, pendentes) = await ImprimirProducaoAsync(
            mesa.Numero, comanda.Descricao, garcom.Nome, pedido, perfil, reimpressao: false);
        pedido.ImpressoNosSetores = falhas.Count == 0;
        await db.SaveChangesAsync();

        await notificador.NotificarAsync(mesa.Id);
        if (pendentes.Count > 0)
        {
            _ = TentarNovamenteAsync(pedido.Id, mesa.Id, pendentes);
        }
        return new ResultadoPedido(pedido.Id, impressos, falhas);
    }

    private static readonly TimeSpan[] EsperasNovaTentativa =
        [TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(45), TimeSpan.FromMinutes(2)];

    /// <summary>
    /// Reenvia em segundo plano somente os tickets dos setores que falharam, para não duplicar
    /// o preparo nos setores que já imprimiram. Se todos saírem, o pedido deixa de aparecer como "não impresso".
    /// </summary>
    private async Task TentarNovamenteAsync(int pedidoId, int mesaId, List<TicketPendente> pendentes)
    {
        try
        {
            foreach (var espera in EsperasNovaTentativa)
            {
                await Task.Delay(espera);
                await using (var consulta = await dbFactory.CreateDbContextAsync())
                {
                    // O caixa pode ter reimpresso manualmente durante a espera.
                    if (await consulta.Pedidos.AnyAsync(p => p.Id == pedidoId && p.ImpressoNosSetores)) return;
                }

                var resultados = new List<TicketPendente>();
                foreach (var ticket in pendentes)
                {
                    if (!await impressao.ImprimirAsync(ticket.Setor, ticket.Titulo, ticket.Documento))
                        resultados.Add(ticket);
                }
                pendentes = resultados;
                if (pendentes.Count > 0) continue;

                await using var db = await dbFactory.CreateDbContextAsync();
                await db.Pedidos.Where(p => p.Id == pedidoId)
                    .ExecuteUpdateAsync(p => p.SetProperty(x => x.ImpressoNosSetores, true));
                await notificador.NotificarAsync(mesaId);
                return;
            }
        }
        catch (Exception)
        {
            // Sem nova tentativa: o pedido continua "não impresso" e pode ser reimpresso pelo caixa.
        }
    }

    public async Task<ResultadoPedido> ReimprimirPedidoAsync(int pedidoId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var perfil = await PerfilEstabelecimentoAtual.ObterAsync(db);
        var pedido = await db.Pedidos
            .Include(p => p.Itens).ThenInclude(i => i.Produto)
            .Include(p => p.Itens).ThenInclude(i => i.SetorImpressao)
            .Include(p => p.Mesa)
            .Include(p => p.Comanda)
            .Include(p => p.UsuarioGarcom)
            .FirstOrDefaultAsync(p => p.Id == pedidoId)
            ?? throw new RegraNegocioException("Pedido não encontrado.");

        var (impressos, falhas, _) = await ImprimirProducaoAsync(pedido.Mesa.Numero, pedido.Comanda.Descricao,
            pedido.UsuarioGarcom.Nome, pedido, perfil, reimpressao: true);
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
        var perfil = await PerfilEstabelecimentoAtual.ObterAsync(db);
        await using var transacao = await db.Database.BeginTransactionAsync();
        var gerente = await UsuarioService.ValidarGerenteAsync(db, pinGerente, protecao);

        var item = await db.ItensPedido.Include(i => i.Produto).Include(i => i.SetorImpressao)
            .FirstOrDefaultAsync(i => i.Id == itemId)
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
                ObservacoesModificadores = item.ObservacoesModificadores,
                SetorImpressaoId = item.SetorImpressaoId,
                DestacarObservacoes = item.DestacarObservacoes
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

        ContaMesa.GarantirSaldoNaoNegativo(comanda, perfil);
        EstoqueService.RegistrarPerdaPorCancelamento(db, cancelado, gerente.Id, cancelado.MotivoCancelamento);
        await ContaMesa.AtualizarValorAsync(db, pedido.Mesa, perfil);
        await transacao.CommitAsync();

        if (item.SetorImpressao is { } setor && pedido.ImpressoNosSetores)
        {
            var documento = TicketsImpressao.Cancelamento(impressao.NovoDocumento(perfil), setor,
                pedido.Mesa.Numero, comanda.Descricao, gerente.Nome, cancelado.MotivoCancelamento,
                new LinhaTicketProducao(quantidade, item.Produto.Nome, item.ObservacoesModificadores));
            await impressao.ImprimirAsync(setor, $"Cancelamento mesa {pedido.Mesa.Numero}", documento);
        }

        await notificador.NotificarAsync(pedido.MesaId);
    }

    /// <summary>Transfere um item (ou parte dele) para outra comanda, da mesma mesa ou de outra.</summary>
    public async Task TransferirItemAsync(int itemId, decimal quantidade, int mesaDestinoId, int? comandaDestinoId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var perfil = await PerfilEstabelecimentoAtual.ObterAsync(db);
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
                ObservacoesModificadores = item.ObservacoesModificadores,
                SetorImpressaoId = item.SetorImpressaoId,
                DestacarObservacoes = item.DestacarObservacoes
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
        ContaMesa.GarantirSaldoNaoNegativo(comandaOrigemAtualizada, perfil);

        await ContaMesa.AtualizarValorAsync(db, pedidoOrigem.Mesa, perfil);
        if (mesaDestino.Id != pedidoOrigem.MesaId)
        {
            await ContaMesa.AtualizarValorAsync(db, mesaDestino, perfil);
        }

        await transacao.CommitAsync();
        await notificador.NotificarAsync(pedidoOrigem.MesaId, mesaDestino.Id);
    }

    private record RegrasItem(string? Observacoes, SetorImpressao? Setor, bool DestacarObservacoes, bool SugerePessoas);

    private static RegrasItem AplicarModificadores(Produto produto, ItemNovoPedido item)
    {
        var opcoesSelecionadas = item.OpcoesIds.Distinct().ToHashSet();
        var partes = new List<string>();
        var setores = new Dictionary<string, SetorImpressao>();
        var destacarObservacoes = false;
        var sugerePessoas = false;

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

            partes.AddRange(escolhidas.Select(o => grupo.Sabores ? $"1/{escolhidas.Count} {o.Nome}" : o.Nome));
            if (escolhidas.Count > 0 || grupo.Opcoes.Count == 0)
            {
                destacarObservacoes |= grupo.DestacarObservacoes;
                sugerePessoas |= grupo.SugerePessoas;
                if (grupo.SetorImpressao is { } setor)
                {
                    setores.TryAdd(setor.Id, setor);
                }
            }
        }

        if (opcoesSelecionadas.Count > 0)
        {
            throw new RegraNegocioException($"Há opções inválidas selecionadas para {produto.Nome}.");
        }

        if (setores.Count > 1)
        {
            throw new RegraNegocioException($"As opções de {produto.Nome} direcionam o item para setores diferentes. Revise os grupos.");
        }

        if (!string.IsNullOrWhiteSpace(item.Observacao))
        {
            partes.Add(item.Observacao.Trim());
        }

        var texto = string.Join(", ", partes);
        var setorDestino = setores.Values.SingleOrDefault() ?? produto.SetorImpressao;
        return new RegrasItem(
            texto.Length == 0 ? null : texto[..Math.Min(500, texto.Length)],
            setorDestino, destacarObservacoes || setorDestino?.ObservacoesEmDestaque == true, sugerePessoas);
    }

    private sealed record TicketPendente(SetorImpressao Setor, string Titulo, DocumentoEscPos Documento);

    private async Task<(List<SetorImpressao> Impressos, List<SetorImpressao> Falhas, List<TicketPendente> Pendentes)> ImprimirProducaoAsync(
        int numeroMesa,
        string comanda,
        string garcom,
        Pedido pedido,
        PerfilEstabelecimento perfil,
        bool reimpressao)
    {
        var impressos = new List<SetorImpressao>();
        var falhas = new List<SetorImpressao>();
        var pendentes = new List<TicketPendente>();

        var porSetor = pedido.Itens
            .Where(i => !i.Cancelado && i.SetorImpressao is not null)
            .GroupBy(i => i.SetorImpressao!)
            .OrderBy(g => g.Key.Ordem).ThenBy(g => g.Key.Nome);

        foreach (var grupo in porSetor)
        {
            var documento = TicketsImpressao.Producao(impressao.NovoDocumento(perfil), grupo.Key, numeroMesa, comanda, garcom,
                pedido.DataHoraLancamento, pedido.Id,
                grupo.Select(i => new LinhaTicketProducao(
                    i.Quantidade, i.Produto.Nome, i.ObservacoesModificadores, i.DestacarObservacoes)),
                reimpressao);

            var titulo = $"Pedido #{pedido.Id} mesa {numeroMesa}";
            var sucesso = await impressao.ImprimirAsync(grupo.Key, titulo, documento);
            (sucesso ? impressos : falhas).Add(grupo.Key);
            if (!sucesso) pendentes.Add(new TicketPendente(grupo.Key, titulo, documento));
        }

        return (impressos, falhas, pendentes);
    }
}
