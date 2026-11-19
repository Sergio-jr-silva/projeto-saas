namespace Nexo.Api.MultiTenancy;

/// <summary>
/// Contexto da requisição. O tenant vem SEMPRE da identidade autenticada (JWT / credencial do agente / token do portal),
/// nunca de parâmetros livres enviados pelo cliente.
/// </summary>
public interface ITenantContext
{
    Guid? CurrentTenantId { get; }
    Guid? CurrentUserId { get; }
    string? UserName { get; }
    string? Role { get; }
    bool IsPlatformAdmin { get; }
    /// <summary>Quando verdadeiro, o RLS e os filtros globais são ignorados (uso restrito a fluxos de sistema).</summary>
    bool BypassRls { get; }
    string Ip { get; }

    void SetIdentity(Guid? userId, string? userName, string? role, Guid? tenantId, string ip);
    void SetTenant(Guid? tenantId);
    void EnablePlatformBypass();
    /// <summary>Escopo explícito de sistema (login, cadastro de empresa, lookup de credenciais). Deve ser curto.</summary>
    IDisposable SystemScope();
}

public class TenantContext : ITenantContext
{
    private int _bypassDepth;
    private bool _platformBypass;

    public Guid? CurrentTenantId { get; private set; }
    public Guid? CurrentUserId { get; private set; }
    public string? UserName { get; private set; }
    public string? Role { get; private set; }
    public string Ip { get; private set; } = string.Empty;
    public bool IsPlatformAdmin => string.Equals(Role, "PlatformAdmin", StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(Role, "AdministradorGeral", StringComparison.OrdinalIgnoreCase);
    public bool BypassRls => _bypassDepth > 0 || _platformBypass;

    public void SetIdentity(Guid? userId, string? userName, string? role, Guid? tenantId, string ip)
    {
        CurrentUserId = userId; UserName = userName; Role = role; CurrentTenantId = tenantId; Ip = ip;
    }

    public void SetTenant(Guid? tenantId) => CurrentTenantId = tenantId;
    public void EnablePlatformBypass() => _platformBypass = true;

    public IDisposable SystemScope()
    {
        _bypassDepth++;
        return new Scope(this);
    }

    private sealed class Scope(TenantContext ctx) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ctx._bypassDepth--;
        }
    }
}
