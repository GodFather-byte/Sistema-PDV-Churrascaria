using System.Collections.Concurrent;
using System.Security.Cryptography;
using LicencasServidor.Data;
using Licenciamento.Comum;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LicencasServidor.Services;

public class RegraException(string mensagem) : Exception(mensagem);

/// <summary>Cadastro de licenças e resposta às conferências online dos caixas.</summary>
public class LicencasService(IDbContextFactory<LicencasDbContext> dbFactory, Emissor emissor)
{
    public async Task<List<LicencaCliente>> ListarAsync(string? busca)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var consulta = db.Licencas.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim().ToUpper();
            consulta = consulta.Where(l => l.Cliente.ToUpper().Contains(termo) || l.Maquina.Contains(termo) || l.Id.Contains(termo));
        }
        return await consulta.OrderBy(l => l.Cliente).ToListAsync();
    }

    public async Task<LicencaCliente?> ObterAsync(string id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Licencas.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);
    }

    public async Task<LicencaCliente> SalvarAsync(LicencaCliente dados)
    {
        dados.Cliente = dados.Cliente?.Trim() ?? string.Empty;
        dados.Maquina = IdentificacaoMaquina.Normalizar(dados.Maquina ?? string.Empty);
        if (dados.Cliente.Length is 0 or > 120) throw new RegraException("Informe o cliente (até 120 caracteres).");
        if (!System.Text.RegularExpressions.Regex.IsMatch(dados.Maquina, "^[A-Z0-9]{4}(-[A-Z0-9]{4}){3}$"))
            throw new RegraException("Código do computador inválido. Use o código mostrado em Gerência > Licença no PDV (ex.: ABCD-EFGH-JKLM-NPQR).");
        if (dados.Bloqueada && string.IsNullOrWhiteSpace(dados.MotivoBloqueio))
            throw new RegraException("Informe o motivo do bloqueio; ele aparece para o cliente.");

        await using var db = await dbFactory.CreateDbContextAsync();
        var registro = string.IsNullOrEmpty(dados.Id) ? null : await db.Licencas.FindAsync(dados.Id);
        if (registro is null)
        {
            registro = new LicencaCliente { Id = NovoId() };
            db.Licencas.Add(registro);
        }
        registro.Cliente = dados.Cliente;
        registro.Contato = dados.Contato?.Trim();
        registro.Observacoes = dados.Observacoes?.Trim();
        registro.Maquina = dados.Maquina;
        registro.ValidaAte = dados.ValidaAte;
        registro.Bloqueada = dados.Bloqueada;
        registro.MotivoBloqueio = dados.Bloqueada ? dados.MotivoBloqueio!.Trim() : null;
        await db.SaveChangesAsync();
        return registro;
    }

    public string GerarArquivo(LicencaCliente registro) => emissor.GerarArquivo(registro);

    /// <summary>
    /// Responde à conferência de um caixa. Procura pela licença informada (na mesma máquina) ou, se o caixa
    /// ainda não tem licença, pela licença cadastrada para o código da máquina. Registra o último contato.
    /// </summary>
    public async Task<string> VerificarAsync(PedidoVerificacao pedido, string? ip)
    {
        var maquina = IdentificacaoMaquina.Normalizar(pedido.Maquina ?? string.Empty);
        if (maquina.Length != 19) throw new RegraException("Código de máquina inválido.");

        await using var db = await dbFactory.CreateDbContextAsync();
        var registro = string.IsNullOrWhiteSpace(pedido.LicencaId)
            ? null
            : await db.Licencas.FirstOrDefaultAsync(l => l.Id == pedido.LicencaId && l.Maquina == maquina);
        registro ??= await db.Licencas.Where(l => l.Maquina == maquina)
            .OrderBy(l => l.Bloqueada).ThenByDescending(l => l.ValidaAte).FirstOrDefaultAsync();

        RespostaVerificacao resposta;
        if (registro is null)
        {
            resposta = new RespostaVerificacao(StatusVerificacao.NaoEncontrada, maquina, null, null,
                "Nenhuma licença cadastrada para este computador.", emissor.Agora);
        }
        else
        {
            registro.UltimaVerificacao = emissor.Agora.UtcDateTime;
            registro.UltimaVersao = pedido.Versao is { Length: > 40 } v ? v[..40] : pedido.Versao;
            registro.UltimoIp = ip;
            registro.TotalVerificacoes++;
            await db.SaveChangesAsync();
            resposta = registro.Bloqueada
                ? new RespostaVerificacao(StatusVerificacao.Bloqueada, maquina, registro.Id, null, registro.MotivoBloqueio, emissor.Agora)
                : new RespostaVerificacao(StatusVerificacao.Ativa, maquina, registro.Id, emissor.GerarArquivo(registro), null, emissor.Agora);
        }
        return emissor.Assinar(resposta);
    }

    private static string NovoId() => Convert.ToHexString(RandomNumberGenerator.GetBytes(6));
}

/// <summary>Contas da equipe do fornecedor que acessam o painel.</summary>
public class AdministradoresService(IDbContextFactory<LicencasDbContext> dbFactory, LimiteTentativas limite)
{
    public const int TamanhoMinimoSenha = 10;
    private static readonly PasswordHasher<Administrador> Hasher = new();

    public async Task<Administrador?> AutenticarAsync(string usuario, string senha, string origem)
    {
        limite.GarantirLiberado(origem);
        await using var db = await dbFactory.CreateDbContextAsync();
        var admin = await db.Administradores.AsNoTracking().FirstOrDefaultAsync(a => a.Usuario == usuario.Trim().ToLower());
        var ok = admin is not null && Hasher.VerifyHashedPassword(admin, admin.SenhaHash, senha) != PasswordVerificationResult.Failed;
        if (ok) limite.RegistrarSucesso(origem); else limite.RegistrarFalha(origem);
        return ok ? admin : null;
    }

    public async Task<List<Administrador>> ListarAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Administradores.AsNoTracking().OrderBy(a => a.Usuario).ToListAsync();
    }

    public async Task<bool> ExisteAlgumAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Administradores.AnyAsync();
    }

    public async Task CriarAsync(string usuario, string senha)
    {
        usuario = usuario?.Trim().ToLower() ?? string.Empty;
        if (!System.Text.RegularExpressions.Regex.IsMatch(usuario, "^[a-z0-9._-]{3,60}$"))
            throw new RegraException("Usuário deve ter de 3 a 60 caracteres: letras, números, ponto, hífen ou sublinhado.");
        ValidarSenha(senha);
        await using var db = await dbFactory.CreateDbContextAsync();
        if (await db.Administradores.AnyAsync(a => a.Usuario == usuario)) throw new RegraException("Usuário já existe.");
        var admin = new Administrador { Usuario = usuario };
        admin.SenhaHash = Hasher.HashPassword(admin, senha);
        db.Administradores.Add(admin);
        await db.SaveChangesAsync();
    }

    public async Task TrocarSenhaAsync(string usuario, string senhaAtual, string novaSenha)
    {
        ValidarSenha(novaSenha);
        await using var db = await dbFactory.CreateDbContextAsync();
        var admin = await db.Administradores.FirstOrDefaultAsync(a => a.Usuario == usuario)
            ?? throw new RegraException("Usuário não encontrado.");
        if (Hasher.VerifyHashedPassword(admin, admin.SenhaHash, senhaAtual) == PasswordVerificationResult.Failed)
            throw new RegraException("Senha atual incorreta.");
        admin.SenhaHash = Hasher.HashPassword(admin, novaSenha);
        await db.SaveChangesAsync();
    }

    public async Task RemoverAsync(int id, string usuarioAtual)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var admin = await db.Administradores.FindAsync(id) ?? throw new RegraException("Usuário não encontrado.");
        if (admin.Usuario == usuarioAtual) throw new RegraException("Você não pode remover o próprio acesso.");
        db.Administradores.Remove(admin);
        await db.SaveChangesAsync();
    }

    private static void ValidarSenha(string senha)
    {
        if (string.IsNullOrEmpty(senha) || senha.Length < TamanhoMinimoSenha)
            throw new RegraException($"A senha deve ter pelo menos {TamanhoMinimoSenha} caracteres.");
    }
}

/// <summary>Bloqueia por tempo crescente o IP que erra a senha do painel várias vezes seguidas.</summary>
public class LimiteTentativas(TimeProvider relogio)
{
    private const int TentativasLivres = 5;
    private sealed record Estado(int Falhas, DateTimeOffset BloqueadoAte);
    private readonly ConcurrentDictionary<string, Estado> _origens = new();

    public void GarantirLiberado(string origem)
    {
        if (_origens.TryGetValue(origem, out var estado) && estado.BloqueadoAte > relogio.GetUtcNow())
            throw new RegraException("Muitas tentativas incorretas. Aguarde alguns minutos e tente novamente.");
    }

    public void RegistrarFalha(string origem) => _origens.AddOrUpdate(origem, _ => new Estado(1, DateTimeOffset.MinValue),
        (_, atual) => atual.Falhas + 1 < TentativasLivres
            ? atual with { Falhas = atual.Falhas + 1 }
            : new Estado(atual.Falhas + 1, relogio.GetUtcNow().AddMinutes(Math.Min(60, Math.Pow(2, atual.Falhas + 1 - TentativasLivres)))));

    public void RegistrarSucesso(string origem) => _origens.TryRemove(origem, out _);
}

public static class SituacaoPainel
{
    /// <summary>Texto e cor (Bootstrap) da situação de uma licença no painel.</summary>
    public static (string Texto, string Cor) De(LicencaCliente l)
    {
        if (l.Bloqueada) return ("Bloqueada", "danger");
        var dias = l.ValidaAte.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber;
        return dias switch
        {
            < 0 => ("Vencida", "danger"),
            <= 7 => ($"Vence em {dias} dia(s)", "warning"),
            _ => ("Ativa", "success")
        };
    }
}
