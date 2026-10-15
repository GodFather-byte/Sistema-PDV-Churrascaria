using ChurrascariaPDV.Domain.Entities;
using ChurrascariaPDV.Infrastructure.Data;
using ChurrascariaPDV.Services;
using ChurrascariaPDV.Services.Impressao;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using static ChurrascariaPDV.Tests.AmbienteTeste;

namespace ChurrascariaPDV.Tests;

public class SetorImpressaoTests
{
    [Fact]
    public async Task MigrationPreservaProdutosPersonalizadosEConverteNenhum()
    {
        await using var conexao = new SqliteConnection("Data Source=:memory:");
        await conexao.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conexao).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261015150815_PerfilEstabelecimento");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO Produtos (Id, Nome, Preco, CategoriaId, TipoItem, SetorImpressao, Ativo, ControlaEstoque, QuantidadeEstoque)
            VALUES (101, 'Produto sem producao', '30', 1, 'Rodizio', 'Nenhum', 1, 0, '0'),
                   (102, 'Produto personalizado', '42', 2, 'CarneAlaCarte', 'Cozinha', 1, 0, '0');
            UPDATE Produtos SET Nome = 'Nome personalizado', Preco = '150', SetorImpressao = 'BarCopa' WHERE Id = 3;
            """);

        await migrator.MigrateAsync();

        Assert.Equal(4, await db.SetoresImpressao.CountAsync());
        Assert.Null((await db.Produtos.SingleAsync(p => p.Id == 101)).SetorImpressaoId);
        Assert.Equal(SetorImpressao.Cozinha, (await db.Produtos.SingleAsync(p => p.Id == 102)).SetorImpressaoId);
        var personalizado = await db.Produtos.SingleAsync(p => p.Id == 3);
        Assert.Equal("Nome personalizado", personalizado.Nome);
        Assert.Equal(150m, personalizado.Preco);
        Assert.Equal(SetorImpressao.BarCopa, personalizado.SetorImpressaoId);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "PRAGMA foreign_key_check";
        Assert.Null(await comando.ExecuteScalarAsync());
    }

    [Fact]
    public async Task NovoSetorPodeReceberProducaoEContas()
    {
        await using var ambiente = await CriarAsync();
        var service = ambiente.Servico<SetorImpressaoService>();
        var forno = new SetorImpressao { Nome = "Forno", Ip = "127.0.0.1", Porta = 9200, RecebeContas = true };
        await service.SalvarAsync(forno);
        await using (var db = ambiente.NovoContexto())
        {
            var produto = await db.Produtos.SingleAsync(p => p.Id == PolentaFrita);
            produto.SetorImpressaoId = forno.Id;
            await db.SaveChangesAsync();
        }

        var resultado = await ambiente.LancarAsync(1, Item(PolentaFrita));
        await ambiente.Mesas.SolicitarContaAsync(1);

        Assert.Equal(forno.Id, Assert.Single(resultado.SetoresImpressos).Id);
        Assert.Contains(ambiente.Impressoes.Listar(), t => t.Setor.Id == forno.Id && t.Conteudo.Contains("FORNO"));
        Assert.Contains(ambiente.Impressoes.Listar(), t => t.Setor.Id == forno.Id && t.Conteudo.Contains("CONFERÊNCIA"));
        Assert.Single((await service.ListarAsync()).Where(s => s.RecebeContas));
    }

    [Fact]
    public async Task ConfiguracaoLegadaEImportadaUmaUnicaVez()
    {
        await using var ambiente = await CriarAsync();
        var service = ambiente.Servico<SetorImpressaoService>();
        var legado = new ImpressaoOptions
        {
            Impressoras = new()
            {
                [SetorImpressao.BarCopa] = new ImpressoraConfig { Ip = "10.0.0.50", Porta = 9200, Habilitada = false }
            }
        };
        await service.ImportarConfiguracaoLegadaAsync(legado);
        var bar = (await service.ListarAsync()).Single(s => s.Id == SetorImpressao.BarCopa);
        Assert.Equal("10.0.0.50", bar.Ip);
        Assert.Equal(9200, bar.Porta);
        Assert.False(bar.Habilitada);

        bar.Ip = "10.0.0.60";
        await service.SalvarAsync(bar);
        await service.ImportarConfiguracaoLegadaAsync(legado);
        Assert.Equal("10.0.0.60", (await service.ListarAsync()).Single(s => s.Id == bar.Id).Ip);
    }

    [Fact]
    public async Task ImpressaoRealSemIpFalhaSemSimularSucesso()
    {
        await using var ambiente = await CriarAsync(o => o.ModoSimulacao = false);
        await using (var db = ambiente.NovoContexto())
        {
            var bar = await db.SetoresImpressao.SingleAsync(s => s.Id == SetorImpressao.BarCopa);
            bar.Ip = "";
            await db.SaveChangesAsync();
        }
        var resultado = await ambiente.LancarAsync(1, Item(Chopp));

        Assert.False(resultado.ImpressoComSucesso);
        var registro = Assert.Single(ambiente.Impressoes.Listar());
        Assert.False(registro.Simulado);
        Assert.False(registro.Sucesso);
        Assert.Contains("não configurado", registro.Erro);
    }
}
