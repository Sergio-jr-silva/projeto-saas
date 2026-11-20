using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.Services;

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

            var isMasterAdmin = string.Equals(role, "PlatformAdmin", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(role, "AdministradorGeral", StringComparison.OrdinalIgnoreCase);

            if (isMasterAdmin)
            {
                if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var h) && Guid.TryParse(h, out var imp))
                    tenantId = imp;                       // visão restrita a um tenant
                else
                    tenant.EnablePlatformBypass();        // visão global (rotas /api/admin)
            }

            tenant.SetIdentity(userId == Guid.Empty ? null : userId, p.FindFirstValue(ClaimTypes.Name), role, tenantId, ip);

            // Validação de isolamento de slug: nunca permitir que usuário da Empresa A opere na rota /:slug da Empresa B
            if (!isMasterAdmin && tenantId.HasValue &&
                !path.StartsWithSegments("/api/tenants/resolve") &&
                !path.StartsWithSegments("/api/tenant/resolve") &&
                !path.StartsWithSegments("/api/auth/logout"))
            {
                if (context.Request.Headers.TryGetValue("X-Tenant-Slug", out var headerSlug) && !string.IsNullOrWhiteSpace(headerSlug))
                {
                    var requestedSlug = SlugHelper.NormalizeSlug(headerSlug);
                    if (!string.IsNullOrWhiteSpace(requestedSlug) && !SlugHelper.IsReservedSlug(requestedSlug))
                    {
                        var db = context.RequestServices.GetRequiredService<NexoDbContext>();
                        var tenantRecord = await db.Tenants.IgnoreQueryFilters().AsNoTracking()
                            .FirstOrDefaultAsync(x => x.Id == tenantId.Value);

                        if (tenantRecord != null)
                        {
                            if (tenantRecord.Status is "Suspenso" or "Bloqueado" or "Inativo" or "Encerrado")
                            {
                                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                                await context.Response.WriteAsJsonAsync(new { codigo = "EMPRESA_SUSPENSA", erro = $"O acesso da empresa encontra-se {tenantRecord.Status.ToLower()}." });
                                return;
                            }

                            if (!string.IsNullOrWhiteSpace(tenantRecord.Slug) &&
                                !string.Equals(tenantRecord.Slug, requestedSlug, StringComparison.OrdinalIgnoreCase))
                            {
                                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                                await context.Response.WriteAsJsonAsync(new { codigo = "ISOLAMENTO_VIOLADO", erro = "Acesso negado: sua sessão pertence a outra empresa." });
                                return;
                            }
                        }
                    }
                }
            }
        }
        else
        {
            tenant.SetIdentity(null, null, null, null, context.Connection.RemoteIpAddress?.ToString() ?? string.Empty);
        }

        await next(context);
    }
}
