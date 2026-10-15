using ChurrascariaPDV.Domain.Entities;
using ChurrascariaPDV.Domain.Enums;
using ChurrascariaPDV.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ChurrascariaPDV.Services;

public record OpcaoCardapio(int Id, string Nome, decimal Preco = 0);

public record GrupoCardapio(int Id, string Nome, bool Obrigatorio, bool SelecaoUnica, IReadOnlyList<OpcaoCardapio> Opcoes, bool Sabores = false);

public record ProdutoCardapio(
    int Id,
    string Nome,
    decimal Preco,
    int CategoriaId,
    string Categoria,
    TipoItem TipoItem,
    SetorImpressao? SetorImpressao,
    IReadOnlyList<GrupoCardapio> Grupos,
    RegraPrecoSabores RegraPrecoSabores = RegraPrecoSabores.MaiorValor,
    int MaximoSabores = 2,
    bool QuantidadeFracionada = false)
{
    public bool TemModificadores => QuantidadeFracionada || Grupos.Any(grupo => grupo.Opcoes.Count > 0);
}

public record CategoriaCardapio(int Id, string Nome, IReadOnlyList<ProdutoCardapio> Produtos);

public class CardapioService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<List<CategoriaCardapio>> ObterCardapioAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var produtos = await db.Produtos.AsNoTracking()
            .Where(p => p.Ativo)
            .Include(p => p.Categoria)
            .Include(p => p.SetorImpressao)
            .Include(p => p.GruposModificadores).ThenInclude(g => g.GrupoModificador).ThenInclude(g => g.Opcoes)
            .ToListAsync();

        return produtos
            .GroupBy(p => p.CategoriaId)
            .OrderBy(g => g.Key)
            .Select(g => new CategoriaCardapio(g.Key, g.First().Categoria.Nome, g.OrderBy(p => p.Nome).Select(Mapear).ToList()))
            .ToList();
    }

    internal static ProdutoCardapio Mapear(Produto produto) => new(
        produto.Id,
        produto.Nome,
        produto.Preco,
        produto.CategoriaId,
        produto.Categoria.Nome,
        produto.TipoItem,
        produto.SetorImpressao,
        produto.GruposModificadores
            .Select(v => v.GrupoModificador)
            .OrderByDescending(g => g.Obrigatorio).ThenBy(g => g.Nome)
            .Select(g => new GrupoCardapio(g.Id, g.Nome, g.Obrigatorio, g.SelecaoUnica,
                g.Opcoes.OrderBy(o => o.Id).Select(o => new OpcaoCardapio(o.Id, o.Nome, o.Preco)).ToList(), g.Sabores))
            .ToList(), produto.RegraPrecoSabores, produto.MaximoSabores, produto.QuantidadeFracionada);

    // ---------- Administração ----------

    public async Task<List<CategoriaProduto>> ListarCategoriasAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CategoriasProdutos.AsNoTracking().OrderBy(c => c.Id).ToListAsync();
    }

    public async Task<List<Produto>> ListarProdutosAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Produtos.AsNoTracking()
            .Include(p => p.Categoria)
            .Include(p => p.SetorImpressao)
            .Include(p => p.GruposModificadores)
            .OrderBy(p => p.CategoriaId).ThenBy(p => p.Nome)
            .ToListAsync();
    }

    public async Task<List<GrupoModificador>> ListarGruposAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.GruposModificadores.AsNoTracking().Include(g => g.Opcoes)
            .Include(g => g.SetorImpressao).OrderBy(g => g.Nome).ToListAsync();
    }

    public async Task SalvarCategoriaAsync(int id, string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new RegraNegocioException("Informe o nome da categoria.");
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        nome = nome.Trim();
        if (await db.CategoriasProdutos.AnyAsync(c => c.Nome == nome && c.Id != id))
        {
            throw new RegraNegocioException("Já existe uma categoria com esse nome.");
        }

        var categoria = id == 0 ? new CategoriaProduto() : await db.CategoriasProdutos.FindAsync(id)
            ?? throw new RegraNegocioException("Categoria não encontrada.");
        categoria.Nome = nome;
        if (id == 0)
        {
            db.CategoriasProdutos.Add(categoria);
        }

        await db.SaveChangesAsync();
    }

    public async Task SalvarProdutoAsync(Produto dados, IReadOnlyCollection<int> gruposModificadores)
    {
        if (string.IsNullOrWhiteSpace(dados.Nome))
        {
            throw new RegraNegocioException("Informe o nome do produto.");
        }

        if (dados.Preco < 0)
        {
            throw new RegraNegocioException("O preço não pode ser negativo.");
        }

        if (!Enum.IsDefined(dados.TipoItem))
        {
            throw new RegraNegocioException("Selecione um tipo de item válido.");
        }

        if (!Enum.IsDefined(dados.RegraPrecoSabores) || dados.MaximoSabores is < 1 or > 8)
        {
            throw new RegraNegocioException("Selecione a regra de preço e um limite de 1 a 8 sabores.");
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        if (!await db.CategoriasProdutos.AnyAsync(c => c.Id == dados.CategoriaId))
        {
            throw new RegraNegocioException("Selecione uma categoria válida.");
        }

        var setorId = string.IsNullOrWhiteSpace(dados.SetorImpressaoId) ? null : dados.SetorImpressaoId;
        if (setorId is not null && !await db.SetoresImpressao.AnyAsync(s => s.Id == setorId))
        {
            throw new RegraNegocioException("Selecione um setor de impressão válido.");
        }

        var gruposIds = gruposModificadores.Distinct().ToList();
        if (await db.GruposModificadores.CountAsync(g => gruposIds.Contains(g.Id)) != gruposIds.Count)
        {
            throw new RegraNegocioException("Selecione grupos de opções válidos.");
        }
        if (await db.GruposModificadores.CountAsync(g => gruposIds.Contains(g.Id) && g.Sabores) > 1)
        {
            throw new RegraNegocioException("Vincule apenas um grupo de sabores ao produto.");
        }

        var produto = dados.Id == 0
            ? new Produto()
            : await db.Produtos.Include(p => p.GruposModificadores).FirstOrDefaultAsync(p => p.Id == dados.Id)
              ?? throw new RegraNegocioException("Produto não encontrado.");

        produto.Nome = dados.Nome.Trim();
        produto.Preco = CalculoConta.Arredondar(dados.Preco);
        produto.RegraPrecoSabores = dados.RegraPrecoSabores;
        produto.MaximoSabores = dados.MaximoSabores;
        produto.CategoriaId = dados.CategoriaId;
        produto.TipoItem = dados.TipoItem;
        produto.SetorImpressaoId = setorId;
        produto.Ativo = dados.Ativo;
        produto.ControlaEstoque = dados.ControlaEstoque;
        produto.QuantidadeFracionada = dados.QuantidadeFracionada;

        produto.GruposModificadores.Clear();
        foreach (var grupoId in gruposIds)
        {
            produto.GruposModificadores.Add(new GrupoModificadorProduto { GrupoModificadorId = grupoId });
        }

        if (produto.Id == 0)
        {
            db.Produtos.Add(produto);
        }

        await db.SaveChangesAsync();
    }

    public async Task SalvarGrupoAsync(
        int id, string nome, bool obrigatorio, bool selecaoUnica, IReadOnlyList<string> opcoes,
        string? setorImpressaoId = null, bool destacarObservacoes = false, bool sugerePessoas = false,
        bool sabores = false, IReadOnlyDictionary<string, decimal>? precosOpcoes = null)
    {
        var nomesOpcoes = opcoes.Select(o => o.Trim()).Where(o => o.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new RegraNegocioException("Informe o nome do grupo.");
        }
        if (nomesOpcoes.Any(n => n.Length > 80) || precosOpcoes?.Any(p => p.Value < 0 || !nomesOpcoes.Contains(p.Key, StringComparer.OrdinalIgnoreCase)) == true)
        {
            throw new RegraNegocioException("Informe opções com até 80 caracteres e preços não negativos.");
        }
        if (sabores && (nomesOpcoes.Count == 0 || selecaoUnica || !obrigatorio))
        {
            throw new RegraNegocioException("O grupo de sabores deve ser obrigatório, permitir múltiplas escolhas e ter opções.");
        }

        if (nomesOpcoes.Count == 0 && (obrigatorio || (!sugerePessoas && !destacarObservacoes && string.IsNullOrWhiteSpace(setorImpressaoId))))
        {
            throw new RegraNegocioException("Informe ao menos uma opção.");
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        setorImpressaoId = string.IsNullOrWhiteSpace(setorImpressaoId) ? null : setorImpressaoId;
        if (setorImpressaoId is not null && !await db.SetoresImpressao.AnyAsync(s => s.Id == setorImpressaoId))
        {
            throw new RegraNegocioException("Selecione um setor de impressão válido para o grupo.");
        }

        nome = nome.Trim();
        if (await db.GruposModificadores.AnyAsync(g => g.Nome == nome && g.Id != id))
        {
            throw new RegraNegocioException("Já existe um grupo com esse nome.");
        }

        var grupo = id == 0
            ? new GrupoModificador()
            : await db.GruposModificadores.Include(g => g.Opcoes).FirstOrDefaultAsync(g => g.Id == id)
              ?? throw new RegraNegocioException("Grupo não encontrado.");
        if (sabores && await db.GruposModificadoresProdutos.AnyAsync(v =>
                v.GrupoModificadorId == id && v.Produto.GruposModificadores.Any(outro =>
                    outro.GrupoModificadorId != id && outro.GrupoModificador.Sabores)))
        {
            throw new RegraNegocioException("Um produto vinculado já possui outro grupo de sabores.");
        }
        grupo.Nome = nome;
        grupo.Obrigatorio = obrigatorio;
        grupo.SelecaoUnica = selecaoUnica;
        grupo.SetorImpressaoId = setorImpressaoId;
        grupo.DestacarObservacoes = destacarObservacoes;
        grupo.SugerePessoas = sugerePessoas;
        grupo.Sabores = sabores;

        foreach (var removida in grupo.Opcoes.Where(o => !nomesOpcoes.Contains(o.Nome, StringComparer.OrdinalIgnoreCase)).ToList())
        {
            grupo.Opcoes.Remove(removida);
        }

        foreach (var nova in nomesOpcoes.Where(n => !grupo.Opcoes.Any(o => string.Equals(o.Nome, n, StringComparison.OrdinalIgnoreCase))))
        {
            grupo.Opcoes.Add(new OpcaoModificador { Nome = nova });
        }
        if (precosOpcoes is not null)
        {
            foreach (var opcao in grupo.Opcoes)
            {
                var entrada = precosOpcoes.FirstOrDefault(p => string.Equals(p.Key, opcao.Nome, StringComparison.OrdinalIgnoreCase));
                opcao.Preco = CalculoConta.Arredondar(entrada.Value);
            }
        }

        if (id == 0)
        {
            db.GruposModificadores.Add(grupo);
        }

        await db.SaveChangesAsync();
    }
}
