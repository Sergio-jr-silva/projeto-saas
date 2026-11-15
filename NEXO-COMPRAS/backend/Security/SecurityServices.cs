using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nexo.Api.Data;
using Nexo.Api.MultiTenancy;

namespace Nexo.Api.Security;

public static class Secrets
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // sem 0/O, 1/I

    public static string Sha256Hex(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    public static bool FixedEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    public static string RandomToken(int bytes = 24) => Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Código de acesso legível (12 caracteres, ~60 bits): XXXX-XXXX-XXXX.</summary>
    public static string AccessCode()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < 12; i++)
        {
            sb.Append(Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]);
            if (i % 4 == 3 && i < 11) sb.Append('-');
        }
        return sb.ToString();
    }

    public static string NormalizeCode(string? code) => new((code ?? string.Empty).ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static readonly HashSet<string> SenhasComuns = new(StringComparer.OrdinalIgnoreCase)
    { "senha12345", "password123", "12345678910", "qwerty12345", "admin12345", "mudar12345", "senha@12345", "password@123" };

    /// <summary>Política: 10–128 caracteres, com maiúscula, minúscula e número; rejeita senhas triviais conhecidas.</summary>
    public static bool PasswordIsStrong(string? pwd) =>
        !string.IsNullOrEmpty(pwd) && pwd.Length is >= 10 and <= 128 && pwd.Any(char.IsUpper) && pwd.Any(char.IsLower) && pwd.Any(char.IsDigit)
        && !SenhasComuns.Contains(pwd);

    /// <summary>Senha temporária criptograficamente aleatória (16 caracteres) que atende à política.</summary>
    public static string GeneratePassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ", lower = "abcdefghijkmnopqrstuvwxyz", digits = "23456789", all = upper + lower + digits + "#@!$%";
        var chars = new List<char> { upper[RandomNumberGenerator.GetInt32(upper.Length)], lower[RandomNumberGenerator.GetInt32(lower.Length)], digits[RandomNumberGenerator.GetInt32(digits.Length)] };
        while (chars.Count < 16) chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);
        for (var i = chars.Count - 1; i > 0; i--) { var j = RandomNumberGenerator.GetInt32(i + 1); (chars[i], chars[j]) = (chars[j], chars[i]); }
        return new string(chars.ToArray());
    }

    public const string PasswordPolicyMessage = "A senha deve ter de 10 a 128 caracteres, com letra maiúscula, minúscula e número.";

    /// <summary>BCrypt com fator de custo 12 (hashes antigos continuam válidos e são verificados normalmente).</summary>
    public static string HashPassword(string pwd) => BCrypt.Net.BCrypt.HashPassword(pwd, 12);
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("nexo-dummy-password", 12);
    /// <summary>Verifica a senha gastando o mesmo tempo mesmo quando o usuário não existe (evita enumeração por tempo de resposta).</summary>
    public static bool VerifyPassword(string pwd, string? hash)
    {
        try { return BCrypt.Net.BCrypt.Verify(pwd, string.IsNullOrEmpty(hash) ? DummyHash : hash) && !string.IsNullOrEmpty(hash); }
        catch { return false; }
    }
}

/// <summary>Credencial do agente: nxk_{agentId}_{segredo}. O tenant é deduzido do agente cadastrado, nunca informado pelo cliente.</summary>
public static class AgentKey
{
    public static (string key, string hash, string prefix) Generate(Guid agentId)
    {
        var secret = Secrets.RandomToken(32);
        var key = $"nxk_{agentId:N}_{secret}";
        return (key, Secrets.Sha256Hex(secret), key[..12]);
    }

    public static bool TryParse(string? key, out Guid agentId, out string secret)
    {
        agentId = Guid.Empty; secret = string.Empty;
        if (string.IsNullOrWhiteSpace(key)) return false;
        var parts = key.Split('_', 3);
        if (parts.Length != 3 || parts[0] != "nxk" || !Guid.TryParseExact(parts[1], "N", out agentId)) return false;
        secret = parts[2];
        return secret.Length >= 20;
    }
}

public class AgentAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, IServiceProvider sp)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Agent-Key", out var header)) return AuthenticateResult.NoResult();
        var keyStr = header.ToString().Trim();
        if (string.IsNullOrWhiteSpace(keyStr)) return AuthenticateResult.NoResult();

        using var scope = sp.CreateScope();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        var db = scope.ServiceProvider.GetRequiredService<NexoDbContext>();

        // 1. Chave exclusiva no formato nxk_{agentId}_{segredo}
        if (AgentKey.TryParse(keyStr, out var agentId, out var secret))
        {
            Models.Agente? agente;
            using (tenant.SystemScope())
                agente = await db.Agentes.AsNoTracking().FirstOrDefaultAsync(a => a.Id == agentId);

            if (agente == null || agente.Revogado || !Secrets.FixedEquals(agente.ChaveHash, Secrets.Sha256Hex(secret)))
                return AuthenticateResult.Fail("Credencial inválida ou revogada.");

            var claims = new List<Claim>
            {
                new("uid", agente.Id.ToString()),
                new("agent_id", agente.Id.ToString()),
                new("tenant_id", agente.TenantId.ToString()),
                new(ClaimTypes.Name, agente.Nome),
                new(ClaimTypes.Role, "Agent"),
            };
            if (agente.FilialId.HasValue)
            {
                claims.Add(new("branch_id", agente.FilialId.Value.ToString()));
            }
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
        }

        // 2. SUPORTE A TOKEN DA FILIAL (Web ID Filial: Token FLK-... ou UUID da Filial)
        using (tenant.SystemScope())
        {
            Models.Filial? filial = null;
            if (Guid.TryParse(keyStr, out var branchGuid))
            {
                filial = await db.Filiais.Include(f => f.Tenant).AsNoTracking().FirstOrDefaultAsync(f => f.Id == branchGuid && f.Ativa && !f.TokenRevogado);
            }
            if (filial == null)
            {
                filial = await db.Filiais.Include(f => f.Tenant).AsNoTracking().FirstOrDefaultAsync(f => f.Token == keyStr && f.Ativa && !f.TokenRevogado);
            }

            if (filial != null)
            {
                var empresa = filial.Tenant ?? await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == filial.TenantId);
                if (empresa == null || empresa.Status == "Bloqueado")
                    return AuthenticateResult.Fail("Empresa da filial bloqueada ou inativa.");

                var agente = await db.Agentes.AsNoTracking().FirstOrDefaultAsync(a => a.FilialId == filial.Id && !a.Revogado)
                             ?? await db.Agentes.AsNoTracking().FirstOrDefaultAsync(a => a.TenantId == filial.TenantId && !a.Revogado);

                var effAgentId = agente?.Id ?? filial.Id;
                var effAgentName = agente?.Nome ?? $"Agente Local - {filial.Nome}";

                var claims = new[]
                {
                    new Claim("uid", effAgentId.ToString()),
                    new Claim("agent_id", effAgentId.ToString()),
                    new Claim("tenant_id", filial.TenantId.ToString()),
                    new Claim("branch_id", filial.Id.ToString()),
                    new Claim("branch_code", filial.Codigo),
                    new Claim("branch_name", filial.Nome),
                    new Claim(ClaimTypes.Name, effAgentName),
                    new Claim(ClaimTypes.Role, "Agent"),
                };
                var identity = new ClaimsIdentity(claims, Scheme.Name);
                return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
            }
        }

        // 3. SUPORTE DIRETO AO ID DA LOJA (Tenant ID / UUID) OU ID DO USUÁRIO
        if (Guid.TryParse(keyStr, out var storeGuid))
        {
            Models.Tenant? empresa;
            Models.Agente? agente;
            using (tenant.SystemScope())
            {
                empresa = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == storeGuid);
                if (empresa == null)
                {
                    var usuario = await db.Usuarios.Include(u => u.Tenant).AsNoTracking().FirstOrDefaultAsync(u => u.Id == storeGuid);
                    if (usuario != null)
                    {
                        empresa = usuario.Tenant ?? await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Status == "Ativo");
                        if (empresa != null) storeGuid = empresa.Id;
                    }
                }

                if (empresa == null || empresa.Status == "Bloqueado")
                    return AuthenticateResult.Fail("Loja/Tenant não encontrado ou bloqueado.");

                agente = await db.Agentes.AsNoTracking().FirstOrDefaultAsync(a => a.TenantId == storeGuid && !a.Revogado);
            }

            var effAgentId = agente?.Id ?? storeGuid;
            var effAgentName = agente?.Nome ?? $"Agente Local - {empresa.NomeFantasia ?? empresa.RazaoSocial}";

            var claims = new[]
            {
                new Claim("uid", effAgentId.ToString()),
                new Claim("agent_id", effAgentId.ToString()),
                new Claim("tenant_id", storeGuid.ToString()),
                new Claim(ClaimTypes.Name, effAgentName),
                new Claim(ClaimTypes.Role, "Agent"),
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
        }

        return AuthenticateResult.Fail("Formato de credencial ou ID da Loja inválido.");
    }
}

