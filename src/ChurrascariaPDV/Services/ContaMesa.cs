using ChurrascariaPDV.Domain.Entities;
using ChurrascariaPDV.Domain.Enums;
using ChurrascariaPDV.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ChurrascariaPDV.Services;

/// <summary>Operações de apoio compartilhadas pelos serviços que mexem na conta das mesas.</summary>
internal static class ContaMesa
{
    public static IQueryable<Comanda> ComandasCompletas(AppDbContext db) => db.Comandas
        .Include(c => c.Pedidos).ThenInclude(p => p.Itens).ThenInclude(i => i.Produto)
        .Include(c => c.Pedidos).ThenInclude(p => p.UsuarioGarcom)
        .Include(c => c.Pagamentos)
        .AsSplitQuery();

    public static Task<List<Comanda>> ComandasAbertasAsync(AppDbContext db, int mesaId) =>
        ComandasCompletas(db)
            .Where(c => c.MesaId == mesaId && c.Status == StatusComanda.Aberta)
            .OrderBy(c => c.CodigoNumero)
            .ToListAsync();

    public static async Task<Mesa> ObterMesaAsync(AppDbContext db, int mesaId) =>
        await db.Mesas.FirstOrDefaultAsync(m => m.Id == mesaId)
        ?? throw new RegraNegocioException("Mesa não encontrada.");

    public static async Task<Comanda> ObterOuCriarContaGeralAsync(AppDbContext db, Mesa mesa)
    {
        var contaGeral = await ComandasCompletas(db).FirstOrDefaultAsync(c =>
            c.MesaId == mesa.Id && c.Status == StatusComanda.Aberta && c.CodigoNumero == Comanda.CodigoContaGeral);
        if (contaGeral is not null)
        {
            return contaGeral;
        }

        contaGeral = new Comanda { Mesa = mesa, MesaId = mesa.Id, CodigoNumero = Comanda.CodigoContaGeral };
        db.Comandas.Add(contaGeral);
        return contaGeral;
    }

    public static void Ocupar(Mesa mesa, int pessoasSugeridas = 0)
    {
        if (mesa.Status is StatusMesa.Livre or StatusMesa.AguardandoLimpeza)
        {
            mesa.Status = StatusMesa.Ocupada;
            mesa.DataHoraAbertura = DateTime.Now;
            mesa.QuantidadePessoas = 0;
            mesa.ValorTotalAtual = 0;
        }

        if (mesa.QuantidadePessoas <= 0 && pessoasSugeridas > 0)
        {
            mesa.QuantidadePessoas = pessoasSugeridas;
        }
    }

    public static void Desocupar(Mesa mesa, StatusMesa novoStatus)
    {
        mesa.Status = novoStatus;
        mesa.DataHoraAbertura = null;
        mesa.QuantidadePessoas = 0;
        mesa.ValorTotalAtual = 0;
    }

    /// <summary>Recalcula o saldo devedor exibido no mapa de mesas.</summary>
    public static async Task AtualizarValorAsync(AppDbContext db, Mesa mesa)
    {
        await db.SaveChangesAsync();
        var comandas = await ComandasAbertasAsync(db, mesa.Id);
        mesa.ValorTotalAtual = Math.Max(0, CalculoConta.Calcular(comandas).Saldo);
        await db.SaveChangesAsync();
    }

    /// <summary>Impede que um cancelamento/transferência deixe a comanda com mais pagamentos que consumo.</summary>
    public static void GarantirSaldoNaoNegativo(Comanda comanda)
    {
        if (CalculoConta.Calcular(comanda).Saldo < 0)
        {
            throw new RegraNegocioException(
                $"A {comanda.Descricao} ficaria com pagamentos maiores que o consumo. Estorne um pagamento antes.");
        }
    }

    public static void FecharComanda(AppDbContext db, Comanda comanda, Mesa mesa, int? usuarioId)
    {
        var resumo = CalculoConta.Calcular(comanda);
        var temMovimento = resumo.Total > 0 || comanda.Pagamentos.Any(p => !p.Estornado);
        comanda.Status = temMovimento ? StatusComanda.Fechada : StatusComanda.Cancelada;
        comanda.DataHoraFechamento = DateTime.Now;

        foreach (var pedido in comanda.Pedidos.Where(p => p.Status == StatusPedido.Aberto))
        {
            pedido.Status = StatusPedido.Finalizado;
        }

        if (temMovimento)
        {
            db.FechamentosConta.Add(new FechamentoConta
            {
                MesaId = mesa.Id,
                ComandaId = comanda.Id,
                UsuarioId = usuarioId,
                Subtotal = resumo.Subtotal,
                ValorTaxaServico = resumo.TaxaServico,
                Total = resumo.Total,
                QuantidadePessoas = mesa.QuantidadePessoas
            });
        }
    }
}
