using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Nexo.Api.Data;
using Nexo.Api.Models;
using Nexo.Api.MultiTenancy;

namespace Nexo.Api.Security;

public record SessionTokens(string AccessToken, string RefreshToken, DateTime AccessExpiresAt);

public interface ISessionService
{
    /// <summary>Abre uma nova sessão (família de refresh tokens) e emite access token curto + refresh token opaco.</summary>
    Task<SessionTokens> StartAsync(Usuario u, Tenant? tenant, string ip);
    /// <summary>Rotaciona o refresh token. Reuso de token já rotacionado revoga TODA a sessão (detecção de roubo).</summary>
    Task<(SessionTokens? Tokens, Usuario? Usuario, Tenant? Tenant)> RefreshAsync(string refreshToken, string ip);
    Task RevokeAsync(string refreshToken);
    Task RevokeAllForUserAsync(Guid usuarioId, string motivo);
    /// <summary>Verifica (com cache curto) se a sessão do access token continua válida e o usuário/empresa seguem ativos.</summary>
    Task<bool> IsSessionActiveAsync(Guid sessionId, Guid usuarioId);
}

public class SessionService(NexoDbContext db, ITenantContext tenant, IJwtService jwt, JwtOptions opt, IMemoryCache cache) : ISessionService
{
    private static string Hash(string token) => Secrets.Sha256Hex(token);

    public async Task<SessionTokens> StartAsync(Usuario u, Tenant? t, string ip)
    {
        var family = Guid.NewGuid();
        var (rt, _) = await CreateRefreshAsync(u.Id, family, ip);
        var (access, exp) = jwt.GenerateToken(u, t, family);
        return new SessionTokens(access, rt, exp);
    }

    private async Task<(string Raw, RefreshToken Entity)> CreateRefreshAsync(Guid usuarioId, Guid family, string ip)
    {
        var raw = Secrets.RandomToken(48);
        var e = new RefreshToken
        {
            UsuarioId = usuarioId, FamilyId = family, TokenHash = Hash(raw),
            ExpiraEm = DateTime.UtcNow.AddDays(opt.RefreshTokenDays), Ip = ip.Length > 60 ? ip[..60] : ip,
        };
        db.RefreshTokens.Add(e);
        await db.SaveChangesAsync();
        return (raw, e);
    }

    public async Task<(SessionTokens?, Usuario?, Tenant?)> RefreshAsync(string refreshToken, string ip)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || refreshToken.Length > 200) return (null, null, null);
        using var _ = tenant.SystemScope();
        var h = Hash(refreshToken);
        var rt = await db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == h);
        if (rt == null) return (null, null, null);

        if (rt.RevogadoEm != null)
        {
            // Token já usado/revogado sendo reapresentado: assume vazamento e derruba a sessão inteira.
            await RevokeFamilyAsync(rt.FamilyId, "reuso-detectado");
            return (null, null, null);
        }
        if (rt.ExpiraEm < DateTime.UtcNow) return (null, null, null);

        var u = await db.Usuarios.Include(x => x.Tenant).ThenInclude(x => x!.Plano).FirstOrDefaultAsync(x => x.Id == rt.UsuarioId);
        if (u == null || !u.Ativo || u.Tenant is { Status: "Bloqueado" })
        {
            await RevokeFamilyAsync(rt.FamilyId, "usuario-inativo");
            return (null, null, null);
        }

        var (raw, novo) = await CreateRefreshAsync(u.Id, rt.FamilyId, ip);
        rt.RevogadoEm = DateTime.UtcNow; rt.SubstituidoPorId = novo.Id; rt.Motivo = "rotacionado";
        await db.SaveChangesAsync();
        var (access, exp) = jwt.GenerateToken(u, u.Tenant, rt.FamilyId);
        return (new SessionTokens(access, raw, exp), u, u.Tenant);
    }

    public async Task RevokeAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        using var _ = tenant.SystemScope();
        var h = Hash(refreshToken);
        var rt = await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(x => x.TokenHash == h);
        if (rt != null) await RevokeFamilyAsync(rt.FamilyId, "logout");
    }

    public async Task RevokeAllForUserAsync(Guid usuarioId, string motivo)
    {
        using var _ = tenant.SystemScope();
        var fams = await db.RefreshTokens.Where(x => x.UsuarioId == usuarioId && x.RevogadoEm == null).Select(x => x.FamilyId).Distinct().ToListAsync();
        foreach (var f in fams) await RevokeFamilyAsync(f, motivo);
    }

    private async Task RevokeFamilyAsync(Guid family, string motivo)
    {
        var agora = DateTime.UtcNow;
        await db.RefreshTokens.Where(x => x.FamilyId == family && x.RevogadoEm == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevogadoEm, agora).SetProperty(x => x.Motivo, motivo));
        cache.Remove($"sess:{family}");
    }

    public async Task<bool> IsSessionActiveAsync(Guid sessionId, Guid usuarioId)
    {
        if (cache.TryGetValue($"sess:{sessionId}", out bool ativo)) return ativo;
        using var _ = tenant.SystemScope();
        var sessaoAtiva = await db.RefreshTokens.AsNoTracking().AnyAsync(x => x.FamilyId == sessionId && x.UsuarioId == usuarioId && x.RevogadoEm == null);
        var usuarioOk = sessaoAtiva && await db.Usuarios.AsNoTracking().AnyAsync(u => u.Id == usuarioId && u.Ativo && (u.Tenant == null || u.Tenant.Status != "Bloqueado"));
        ativo = sessaoAtiva && usuarioOk;
        cache.Set($"sess:{sessionId}", ativo, TimeSpan.FromSeconds(15)); // janela máxima de propagação de uma revogação
        return ativo;
    }
}
