using ChurrascariaPDV.Domain.Entities;
using ChurrascariaPDV.Domain.Enums;
using ChurrascariaPDV.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ChurrascariaPDV.Services;

public class PerfilEstabelecimentoService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<PerfilEstabelecimento> ObterAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.PerfisEstabelecimento.AsNoTracking()
            .SingleAsync(perfil => perfil.Id == PerfilEstabelecimento.IdPadrao);
    }

    public async Task SalvarAsync(PerfilEstabelecimento dados)
    {
        if (!Enum.IsDefined(dados.Tipo))
        {
            throw new RegraNegocioException("Selecione um tipo de estabelecimento válido.");
        }

        if (string.IsNullOrWhiteSpace(dados.Nome) || dados.Nome.Trim().Length > 120)
        {
            throw new RegraNegocioException("Informe um nome de até 120 caracteres.");
        }

        if ((dados.Endereco?.Length ?? 0) > 250)
        {
            throw new RegraNegocioException("O endereço pode ter no máximo 250 caracteres.");
        }

        if (dados.PercentualTaxaServico is < 0 or > 100)
        {
            throw new RegraNegocioException("A taxa de serviço deve estar entre 0% e 100%.");
        }

        if (dados.LarguraTicket is < 16 or > 80)
        {
            throw new RegraNegocioException("A largura do ticket deve estar entre 16 e 80 colunas.");
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var perfil = await PerfilEstabelecimentoAtual.ObterAsync(db);
        var mudouTaxa = perfil.PercentualTaxaServico != dados.PercentualTaxaServico
            || perfil.TaxaServicoOpcionalPorComanda != dados.TaxaServicoOpcionalPorComanda;
        if (mudouTaxa && await db.Comandas.AnyAsync(comanda => comanda.Status == Domain.Enums.StatusComanda.Aberta))
        {
            throw new RegraNegocioException("Feche as comandas abertas antes de alterar a taxa de serviço.");
        }

        perfil.Tipo = dados.Tipo;
        perfil.Nome = dados.Nome.Trim();
        perfil.Endereco = dados.Endereco?.Trim() ?? string.Empty;
        perfil.PercentualTaxaServico = dados.PercentualTaxaServico;
        perfil.TaxaServicoOpcionalPorComanda = dados.TaxaServicoOpcionalPorComanda;
        perfil.LarguraTicket = dados.LarguraTicket;
        await db.SaveChangesAsync();
    }
}
