using ChurrascariaPDV.Domain.Entities;
using ChurrascariaPDV.Domain.Enums;
using ChurrascariaPDV.Infrastructure.Data;
using ChurrascariaPDV.Services.Notificacoes;
using Microsoft.EntityFrameworkCore;

namespace ChurrascariaPDV.Services;

public record ProdutoEstoque(int Id, string Nome, bool ControlaEstoque, decimal QuantidadeEstoque);

public record MovimentoEstoqueResumo(
    DateTime DataHora,
    string Produto,
    TipoMovimentacaoEstoque Tipo,
    decimal Quantidade,
    decimal SaldoApos,
    string? Observacao,
    string? Usuario);

public class EstoqueService(IDbContextFactory<AppDbContext> dbFactory, NotificadorMesas notificador)
{
    public async Task<List<ProdutoEstoque>> ListarProdutosAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Produtos.AsNoTracking()
            .Where(produto => produto.ControlaEstoque)
            .OrderBy(produto => produto.Nome)
            .Select(produto => new ProdutoEstoque(produto.Id, produto.Nome, produto.ControlaEstoque, produto.QuantidadeEstoque))
            .ToListAsync();
    }

    public async Task<List<MovimentoEstoqueResumo>> ListarMovimentacoesAsync(int limite = 100)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.MovimentacoesEstoque.AsNoTracking()
            .Include(movimento => movimento.Produto)
            .Include(movimento => movimento.Usuario)
            .OrderByDescending(movimento => movimento.DataHora)
            .ThenByDescending(movimento => movimento.Id)
            .Take(Math.Clamp(limite, 1, 500))
            .Select(movimento => new MovimentoEstoqueResumo(
                movimento.DataHora,
                movimento.Produto.Nome,
                movimento.Tipo,
                movimento.Quantidade,
                movimento.SaldoApos,
                movimento.Observacao,
                movimento.Usuario == null ? null : movimento.Usuario.Nome))
            .ToListAsync();
    }

    public async Task AjustarAsync(int produtoId, decimal quantidade, bool entrada, string? observacao, int usuarioId)
    {
        quantidade = Math.Round(quantidade, 3, MidpointRounding.AwayFromZero);
        if (quantidade <= 0)
        {
            throw new RegraNegocioException("Informe uma quantidade maior que zero.");
        }

        if (string.IsNullOrWhiteSpace(observacao) || observacao.Trim().Length < 3)
        {
            throw new RegraNegocioException("Informe um motivo com pelo menos 3 caracteres.");
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        await using var transacao = await db.Database.BeginTransactionAsync();
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u =>
            u.Id == usuarioId && u.Ativo && !u.TrocaPinObrigatoria && u.Perfil == PerfilUsuario.Gerente);
        if (usuario is null)
        {
            throw new RegraNegocioException("Somente um gerente ativo pode ajustar o estoque.");
        }

        var produto = await db.Produtos.FirstOrDefaultAsync(p => p.Id == produtoId && p.ControlaEstoque)
            ?? throw new RegraNegocioException("Produto não encontrado ou sem controle de estoque.");
        var variacao = entrada ? quantidade : -quantidade;
        produto.QuantidadeEstoque += variacao;
        db.MovimentacoesEstoque.Add(new MovimentacaoEstoque
        {
            Produto = produto,
            UsuarioId = usuarioId,
            Tipo = entrada ? TipoMovimentacaoEstoque.Entrada : TipoMovimentacaoEstoque.Perda,
            Quantidade = variacao,
            SaldoApos = produto.QuantidadeEstoque,
            Observacao = observacao.Trim()[..Math.Min(250, observacao.Trim().Length)]
        });
        await db.SaveChangesAsync();
        await transacao.CommitAsync();
        await notificador.NotificarAsync();
    }

    internal static void BaixarComanda(AppDbContext db, Comanda comanda, int? usuarioId)
    {
        var itens = comanda.Pedidos.SelectMany(pedido => pedido.Itens)
            .Where(item => !item.Cancelado && item.Produto.ControlaEstoque)
            .ToList();
        if (itens.Count == 0)
        {
            return;
        }

        var itemIds = itens.Select(item => item.Id).ToList();
        var jaBaixados = db.MovimentacoesEstoque
            .Where(movimento => movimento.ItemPedidoId != null && itemIds.Contains(movimento.ItemPedidoId.Value))
            .Select(movimento => movimento.ItemPedidoId!.Value)
            .ToHashSet();

        foreach (var item in itens.Where(item => !jaBaixados.Contains(item.Id)))
        {
            item.Produto.QuantidadeEstoque -= item.Quantidade;
            db.MovimentacoesEstoque.Add(new MovimentacaoEstoque
            {
                Produto = item.Produto,
                ItemPedido = item,
                UsuarioId = usuarioId,
                Tipo = TipoMovimentacaoEstoque.Venda,
                Quantidade = -item.Quantidade,
                SaldoApos = item.Produto.QuantidadeEstoque,
                Observacao = $"Venda da {comanda.Descricao}"
            });
        }
    }

    internal static void RegistrarPerdaPorCancelamento(
        AppDbContext db,
        ItemPedido item,
        int gerenteId,
        string? motivo)
    {
        if (!item.Produto.ControlaEstoque)
        {
            return;
        }

        item.Produto.QuantidadeEstoque -= item.Quantidade;
        db.MovimentacoesEstoque.Add(new MovimentacaoEstoque
        {
            Produto = item.Produto,
            ItemPedido = item,
            UsuarioId = gerenteId,
            Tipo = TipoMovimentacaoEstoque.Perda,
            Quantidade = -item.Quantidade,
            SaldoApos = item.Produto.QuantidadeEstoque,
            Observacao = $"Cancelamento: {motivo}"
        });
    }
}
