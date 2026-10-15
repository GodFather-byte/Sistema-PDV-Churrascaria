using ChurrascariaPDV.Domain.Enums;

namespace ChurrascariaPDV.Services;

public static class CalculoPrecoProduto
{
    public static decimal Calcular(ProdutoCardapio produto, IEnumerable<int> opcoesIds)
    {
        var ids = opcoesIds.ToHashSet();
        var sabores = new List<decimal>();
        var adicionais = 0m;
        if (produto.Grupos.Count(g => g.Sabores) > 1)
            throw new RegraNegocioException("O produto possui mais de um grupo de sabores.");

        foreach (var grupo in produto.Grupos)
        {
            var escolhidas = grupo.Opcoes.Where(o => ids.Remove(o.Id)).ToList();
            if ((grupo.Obrigatorio || grupo.Sabores) && escolhidas.Count == 0)
                throw new RegraNegocioException($"Escolha \"{grupo.Nome}\" para {produto.Nome}.");
            if (grupo.SelecaoUnica && escolhidas.Count > 1)
                throw new RegraNegocioException($"Escolha apenas uma opção de \"{grupo.Nome}\".");
            if (grupo.Sabores)
            {
                if (escolhidas.Count > produto.MaximoSabores)
                    throw new RegraNegocioException($"Escolha até {produto.MaximoSabores} sabores para {produto.Nome}.");
                sabores.AddRange(escolhidas.Select(o => o.Preco));
            }
            else
            {
                adicionais += escolhidas.Sum(o => o.Preco);
            }
        }

        if (ids.Count > 0)
            throw new RegraNegocioException($"Há opções inválidas selecionadas para {produto.Nome}.");

        var preco = sabores.Count == 0 ? produto.Preco : produto.RegraPrecoSabores switch
        {
            RegraPrecoSabores.MaiorValor => sabores.Max(),
            RegraPrecoSabores.Media => sabores.Average(),
            RegraPrecoSabores.Soma => sabores.Sum(),
            _ => throw new RegraNegocioException("Regra de preço de sabores inválida.")
        };
        return CalculoConta.Arredondar(preco + adicionais);
    }
}
