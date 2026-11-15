using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Nexo.Api.MultiTenancy;

/// <summary>
/// Resolve a identidade da requisição e popula o ITenantContext.
/// - /api/agent/*  → credencial do agente (esquema "Agent")
/// - /api/portal/* → token de sessão do portal do fornecedor (esquema "Portal")
/// - demais        → JWT de usuário (esquema "Bearer")
/// O tenant_id vem sempre das claims assinadas. PlatformAdmin só enxerga um tenant específico via
/// X-Tenant-Id (impersonação consciente); sem o header, opera no modo plataforma (sem escopo de tenant).
/// </summary>
public class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenant)
    {
        var path = context.Request.Path;
        string scheme = path.StartsWithSegments("/api/agent") ? "Agent"
                      : path.StartsWithSegments("/api/portal") ? "Portal"
                      : "Bearer";

        var result = await context.AuthenticateAsync(scheme);
        if (result.Succeeded && result.Principal != null)
        {
            context.User = result.Principal;
            var p = result.Principal;
            Guid.TryParse(p.FindFirstValue("uid") ?? p.FindFirstValue(ClaimTypes.NameIdentifier), out var userId);
            var role = p.FindFirstValue(ClaimTypes.Role);
            Guid? tenantId = Guid.TryParse(p.FindFirstValue("tenant_id"), out var t) ? t : null;
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

            if (string.Equals(role, "PlatformAdmin", StringComparison.OrdinalIgnoreCase))
            {
                if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var h) && Guid.TryParse(h, out var imp))
                    tenantId = imp;                       // visão restrita a um tenant
                else
                    tenant.EnablePlatformBypass();        // visão global (rotas /api/admin)
            }
            tenant.SetIdentity(userId == Guid.Empty ? null : userId, p.FindFirstValue(ClaimTypes.Name), role, tenantId, ip);
        }
        else
        {
            tenant.SetIdentity(null, null, null, null, context.Connection.RemoteIpAddress?.ToString() ?? string.Empty);
        }

        await next(context);
    }
}
