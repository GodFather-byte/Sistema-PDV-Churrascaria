using System.Net;
using ChurrascariaPDV.Domain.Entities;
using ChurrascariaPDV.Infrastructure.Data;
using ChurrascariaPDV.Services.Impressao;
using Microsoft.EntityFrameworkCore;

namespace ChurrascariaPDV.Services;

public class SetorImpressaoService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<List<SetorImpressao>> ListarAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.SetoresImpressao.AsNoTracking().OrderBy(s => s.Ordem).ThenBy(s => s.Nome).ToListAsync();
    }

    public static Task<SetorImpressao> ObterSetorContasAsync(AppDbContext db) =>
        db.SetoresImpressao.SingleAsync(s => s.RecebeContas);

    public async Task SalvarAsync(SetorImpressao dados)
    {
        if (string.IsNullOrWhiteSpace(dados.Nome) || dados.Nome.Trim().Length > 80)
        {
            throw new RegraNegocioException("Informe um nome de até 80 caracteres.");
        }

        if (!string.IsNullOrWhiteSpace(dados.Ip) && !IPAddress.TryParse(dados.Ip.Trim(), out _))
        {
            throw new RegraNegocioException("Informe um endereço IP válido.");
        }

        if (dados.Porta is < 1 or > 65535)
        {
            throw new RegraNegocioException("A porta deve estar entre 1 e 65535.");
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        await using var transacao = await db.Database.BeginTransactionAsync();
        var setor = await db.SetoresImpressao.FindAsync(dados.Id);
        var novo = setor is null;
        setor ??= new SetorImpressao { Id = dados.Id };
        if (await db.SetoresImpressao.AnyAsync(s => s.Id != dados.Id && s.Nome == dados.Nome.Trim()))
        {
            throw new RegraNegocioException("Já existe um setor com esse nome.");
        }

        if (setor.RecebeContas && !dados.RecebeContas)
        {
            throw new RegraNegocioException("Selecione outro setor para receber contas antes de remover esta função.");
        }

        if (dados.RecebeContas)
        {
            var anterior = await db.SetoresImpressao.FirstOrDefaultAsync(s => s.RecebeContas && s.Id != dados.Id);
            if (anterior is not null)
            {
                anterior.RecebeContas = false;
                await db.SaveChangesAsync();
            }
        }

        setor.Nome = dados.Nome.Trim();
        setor.Ip = dados.Ip.Trim();
        setor.Porta = dados.Porta;
        setor.Habilitada = dados.Habilitada;
        setor.RecebeContas = dados.RecebeContas;
        setor.ItensEmDestaque = dados.ItensEmDestaque;
        setor.ObservacoesEmDestaque = dados.ObservacoesEmDestaque;
        setor.Ordem = dados.Ordem;
        setor.ConfiguracaoLegadaImportada = true;
        if (novo)
        {
            db.SetoresImpressao.Add(setor);
        }

        await db.SaveChangesAsync();
        await transacao.CommitAsync();
    }

    public async Task ImportarConfiguracaoLegadaAsync(ImpressaoOptions config)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var setores = await db.SetoresImpressao.Where(s => !s.ConfiguracaoLegadaImportada).ToListAsync();
        foreach (var setor in setores)
        {
            if (config.Impressoras.TryGetValue(setor.Id, out var impressora))
            {
                setor.Ip = impressora.Ip;
                setor.Porta = impressora.Porta;
                setor.Habilitada = impressora.Habilitada;
            }

            setor.ConfiguracaoLegadaImportada = true;
        }

        await db.SaveChangesAsync();
    }
}
