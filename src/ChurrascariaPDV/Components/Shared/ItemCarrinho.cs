using ChurrascariaPDV.Services;

namespace ChurrascariaPDV.Components.Shared;

/// <summary>Item escolhido na tela, ainda não enviado para a produção.</summary>
public class ItemCarrinho
{
    public required ProdutoCardapio Produto { get; init; }
    public decimal Quantidade { get; set; } = 1;
    public IReadOnlyList<int> OpcoesIds { get; init; } = [];
    public IReadOnlyList<string> OpcoesNomes { get; init; } = [];
    public string? Observacao { get; init; }

    public decimal Total => CalculoConta.Arredondar(Quantidade * Produto.Preco);

    public string Detalhes => string.Join(", ", OpcoesNomes.Concat(string.IsNullOrWhiteSpace(Observacao) ? [] : [Observacao!]));

    public bool MesmoItem(ItemCarrinho outro) =>
        outro.Produto.Id == Produto.Id && outro.OpcoesIds.SequenceEqual(OpcoesIds) && outro.Observacao == Observacao;

    public ItemNovoPedido ParaPedido() => new(Produto.Id, Quantidade, OpcoesIds, Observacao);

    public static void Adicionar(List<ItemCarrinho> carrinho, ItemCarrinho novo)
    {
        var existente = carrinho.FirstOrDefault(i => i.MesmoItem(novo));
        if (existente is null)
        {
            carrinho.Add(novo);
        }
        else
        {
            existente.Quantidade += novo.Quantidade;
        }
    }
}
