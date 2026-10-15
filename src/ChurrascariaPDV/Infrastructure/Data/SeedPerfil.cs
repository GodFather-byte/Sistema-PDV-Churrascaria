using ChurrascariaPDV.Domain.Entities;
using ChurrascariaPDV.Domain.Enums;
using ChurrascariaPDV.Services;
using Microsoft.EntityFrameworkCore;

namespace ChurrascariaPDV.Infrastructure.Data;

public sealed class InstalacaoOptions
{
    public TipoEstabelecimento Tipo { get; set; } = TipoEstabelecimento.Churrascaria;
    public string Nome { get; set; } = "Meu estabelecimento";
}

public static class SeedPerfil
{
    // Executado somente na criação do banco, nunca para trocar o perfil de um banco em uso.
    public static async Task AplicarAsync(AppDbContext db, InstalacaoOptions configuracao)
    {
        if (!Enum.IsDefined(configuracao.Tipo) || string.IsNullOrWhiteSpace(configuracao.Nome) || configuracao.Nome.Trim().Length > 120)
            throw new RegraNegocioException("Informe um tipo válido e um nome de até 120 caracteres para a instalação.");
        if (await db.Pedidos.AnyAsync() || await db.Comandas.AnyAsync() || await db.TurnosCaixa.AnyAsync()
            || await db.MovimentacoesEstoque.AnyAsync() || await db.Usuarios.AnyAsync(u => u.PinAcesso.Length > 4))
            throw new RegraNegocioException("Não é permitido reaplicar exemplos em um banco com operação registrada.");

        await using var transacao = await db.Database.BeginTransactionAsync();
        await db.GruposModificadoresProdutos.ExecuteDeleteAsync();
        await db.OpcoesModificadores.ExecuteDeleteAsync();
        await db.GruposModificadores.ExecuteDeleteAsync();
        await db.Produtos.ExecuteDeleteAsync();
        await db.CategoriasProdutos.ExecuteDeleteAsync();
        await db.SetoresImpressao.ExecuteDeleteAsync();
        await db.Usuarios.ExecuteDeleteAsync();
        await db.PerfisEstabelecimento.ExecuteDeleteAsync();
        await db.Mesas.ExecuteDeleteAsync();

        var tipo = configuracao.Tipo;
        db.PerfisEstabelecimento.Add(new PerfilEstabelecimento
        {
            Tipo = tipo, Nome = configuracao.Nome.Trim(),
            PercentualTaxaServico = tipo is TipoEstabelecimento.RestaurantePorQuilo ? 0 : 10
        });
        db.Mesas.AddRange(Enumerable.Range(1, 50).Select(n => new Mesa
        {
            Id = n, Numero = n, CapacidadeLugares = n <= 10 ? 2 : n <= 35 ? 4 : 6
        }));
        var caixa = new SetorImpressao { Id = SetorImpressao.Caixa, Nome = "Caixa", RecebeContas = true, Ordem = 4 };
        var bebidas = new SetorImpressao { Id = SetorImpressao.BarCopa, Nome = "Bar / Copa", ItensEmDestaque = false, Ordem = 1 };
        var cozinha = new SetorImpressao
        {
            Id = SetorImpressao.Cozinha,
            Nome = tipo == TipoEstabelecimento.Pizzaria ? "Forno" : tipo == TipoEstabelecimento.Hamburgueria ? "Chapa" : "Cozinha",
            Ordem = 2
        };
        db.SetoresImpressao.AddRange(caixa, bebidas, cozinha);
        if (tipo == TipoEstabelecimento.Churrascaria)
        {
            db.SetoresImpressao.Add(new SetorImpressao
            {
                Id = SetorImpressao.Churrasqueira, Nome = "Churrasqueira", Ordem = 3, ObservacoesEmDestaque = true
            });
            CriarChurrascaria(db);
        }
        else
        {
            var categoria = new CategoriaProduto
            {
                Nome = tipo switch
                {
                    TipoEstabelecimento.Pizzaria => "Pizzas",
                    TipoEstabelecimento.Hamburgueria => "Hambúrgueres",
                    TipoEstabelecimento.Bar => "Petiscos",
                    TipoEstabelecimento.RestaurantePorQuilo => "Buffet por quilo",
                    _ => "Pratos"
                }
            };
            var produto = new Produto
            {
                Categoria = categoria, SetorImpressao = cozinha,
                Nome = tipo switch
                {
                    TipoEstabelecimento.Pizzaria => "Pizza grande",
                    TipoEstabelecimento.Hamburgueria => "Hambúrguer da casa",
                    TipoEstabelecimento.Bar => "Batata frita",
                    TipoEstabelecimento.RestaurantePorQuilo => "Buffet (kg)",
                    _ => "Prato do dia"
                },
                Preco = tipo == TipoEstabelecimento.RestaurantePorQuilo ? 69.90m : 30m,
                QuantidadeFracionada = tipo == TipoEstabelecimento.RestaurantePorQuilo
            };
            db.Produtos.Add(produto);
            if (tipo == TipoEstabelecimento.Pizzaria)
            {
                var sabores = new GrupoModificador
                {
                    Nome = "Sabores", Sabores = true, Obrigatorio = true,
                    Opcoes = [new() { Nome = "Calabresa", Preco = 50 }, new() { Nome = "Muçarela", Preco = 45 }, new() { Nome = "Especial", Preco = 65 }]
                };
                produto.Preco = 45;
                produto.GruposModificadores.Add(new() { GrupoModificador = sabores });
            }
            if (tipo == TipoEstabelecimento.Hamburgueria)
            {
                produto.GruposModificadores.Add(new()
                {
                    GrupoModificador = new GrupoModificador
                    {
                        Nome = "Adicionais",
                        Opcoes = [new() { Nome = "Queijo", Preco = 4 }, new() { Nome = "Bacon", Preco = 6 }]
                    }
                });
            }
            db.Produtos.Add(new Produto
            {
                Nome = "Refrigerante lata", Preco = 8, Categoria = new CategoriaProduto { Nome = "Bebidas" }, SetorImpressao = bebidas
            });
        }
        await db.SaveChangesAsync();
        await transacao.CommitAsync();
    }

    private static void CriarChurrascaria(AppDbContext db)
    {
        db.CategoriasProdutos.AddRange(new CategoriaProduto { Id = 1, Nome = "Rodízio" },
            new() { Id = 2, Nome = "Carnes" }, new() { Id = 3, Nome = "Porções" },
            new() { Id = 4, Nome = "Bebidas" }, new() { Id = 5, Nome = "Sobremesas" });
        var ponto = new GrupoModificador
        {
            Id = 1, Nome = "Ponto da Carne", Obrigatorio = true, SelecaoUnica = true,
            Opcoes = [new() { Id = 1, Nome = "Malpassada" }, new() { Id = 2, Nome = "Ao Ponto" }, new() { Id = 3, Nome = "Bem Passada" }]
        };
        var bebida = new GrupoModificador
        {
            Id = 2, Nome = "Preferência Bebida",
            Opcoes = [new() { Id = 4, Nome = "Com Gelo e Limão" }, new() { Id = 5, Nome = "Sem Gelo" }, new() { Id = 6, Nome = "2 Copos" }]
        };
        var pessoas = new GrupoModificador { Id = 3, Nome = "Consumo por pessoa", SugerePessoas = true };
        db.GruposModificadores.AddRange(ponto, bebida, pessoas);
        Produto Criar(int id, string nome, decimal preco, int categoria, TipoItem tipo, string? setor, GrupoModificador? grupo = null)
        {
            var p = new Produto { Id = id, Nome = nome, Preco = preco, CategoriaId = categoria, TipoItem = tipo, SetorImpressaoId = setor };
            if (grupo is not null) p.GruposModificadores.Add(new() { GrupoModificador = grupo });
            return p;
        }
        db.Produtos.AddRange(
            Criar(1, "Rodízio Completo Adulto", 89.90m, 1, TipoItem.Rodizio, null, pessoas),
            Criar(2, "Rodízio Infantil", 44.90m, 1, TipoItem.Rodizio, null, pessoas),
            Criar(3, "Picanha na Tábua", 119.90m, 2, TipoItem.CarneAlaCarte, SetorImpressao.Churrasqueira, ponto),
            Criar(4, "Costela no Bafo", 99.90m, 2, TipoItem.CarneAlaCarte, SetorImpressao.Churrasqueira, ponto),
            Criar(5, "Polenta Frita", 32.90m, 3, TipoItem.PorcaoCozinha, SetorImpressao.Cozinha),
            Criar(6, "Mandioca Frita", 34.90m, 3, TipoItem.PorcaoCozinha, SetorImpressao.Cozinha),
            Criar(7, "Chopp 500ml", 14.90m, 4, TipoItem.Bebida, SetorImpressao.BarCopa, bebida),
            Criar(8, "Refrigerante Lata", 8.90m, 4, TipoItem.Bebida, SetorImpressao.BarCopa, bebida),
            Criar(9, "Suco Jarra", 22.90m, 4, TipoItem.Bebida, SetorImpressao.BarCopa, bebida),
            Criar(10, "Petit Gateau", 24.90m, 5, TipoItem.Sobremesa, SetorImpressao.Cozinha));
    }
}
