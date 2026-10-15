using ChurrascariaPDV.Domain.Entities;
using ChurrascariaPDV.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ChurrascariaPDV.Services;

internal static class PerfilEstabelecimentoAtual
{
    public static Task<PerfilEstabelecimento> ObterAsync(AppDbContext db) =>
        db.PerfisEstabelecimento.SingleAsync(perfil => perfil.Id == PerfilEstabelecimento.IdPadrao);
}
