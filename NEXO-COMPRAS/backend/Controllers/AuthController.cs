using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.DTOs;
using Nexo.Api.Models;
using Nexo.Api.MultiTenancy;
using Nexo.Api.Security;
using Nexo.Api.Services;

namespace Nexo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(NexoDbContext db, IJwtService jwt, ITenantContext tenant, IAuditService audit, ISessionService sessionService) : ControllerBase
{
    private static TenantSummaryDto? ToDto(Tenant? t) => t == null
        ? null
        : new(t.Id, t.RazaoSocial, t.NomeFantasia, t.Cnpj, t.Status, t.Plano?.Nome, t.LogoUrl, t.Slug, t.MensalidadeFinal, t.ModuloContratado, t.SituacaoFinanceira);

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Senha))
            return BadRequest(new { erro = "E-mail e senha são obrigatórios." });

        using var _ = tenant.SystemScope();
        var email = req.Email.Trim().ToLowerInvariant();
        var u = await db.Usuarios.Include(x => x.Tenant).ThenInclude(t => t!.Plano).FirstOrDefaultAsync(x => x.Email.ToLower() == email);

        if (u?.BloqueadoAte > DateTime.UtcNow)
            return StatusCode(429, new { erro = "Conta temporariamente bloqueada por excesso de tentativas. Tente novamente em alguns minutos." });

        if (u == null || !BCrypt.Net.BCrypt.Verify(req.Senha, u.SenhaHash))
        {
            if (u != null)
            {
                u.FalhasLogin++;
                if (u.FalhasLogin >= 5) { u.BloqueadoAte = DateTime.UtcNow.AddMinutes(15); u.FalhasLogin = 0; }
                audit.Add("LoginFalhou", "Usuario", u.Id.ToString(), null, u.TenantId, u.Id);
                await db.SaveChangesAsync();
            }
            return Unauthorized(new { erro = "Credenciais inválidas." });
        }
        if (!u.Ativo) return Unauthorized(new { erro = "Usuário inativo. Contate o administrador." });
        if (u.Tenant is { Status: "Bloqueado" or "Suspenso" or "Inativo" or "Encerrado" })
            return Unauthorized(new { codigo = "EMPRESA_SUSPENSA", erro = $"Acesso da empresa {u.Tenant.Status.ToLower()}. Contate o suporte ou Administrador Geral." });

        // Se o login partiu de uma URL individual da empresa (/:slug/login), valida que o usuário pertence àquela empresa
        if (!string.IsNullOrWhiteSpace(req.Slug) && u.Tenant != null)
        {
            var reqSlugNorm = SlugHelper.NormalizeSlug(req.Slug);
            if (!string.IsNullOrWhiteSpace(reqSlugNorm) &&
                !string.IsNullOrWhiteSpace(u.Tenant.Slug) &&
                !string.Equals(u.Tenant.Slug, reqSlugNorm, StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(403, new { codigo = "ACESSO_CRUZADO_NEGADO", erro = "Esta conta de usuário pertence a outra empresa e não pode acessar este endereço." });
            }
        }

        u.UltimoLogin = DateTime.UtcNow; u.FalhasLogin = 0; u.BloqueadoAte = null;
        audit.Add("Login", "Usuario", u.Id.ToString(), null, u.TenantId, u.Id);
        await db.SaveChangesAsync();

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var tokens = await sessionService.StartAsync(u, u.Tenant, ip);
        return Ok(new LoginResponse(tokens.AccessToken, new UserDto(u.Id, u.Nome, u.Email, u.Perfil, u.TenantId), ToDto(u.Tenant), tokens.RefreshToken, (int)(tokens.AccessExpiresAt - DateTime.UtcNow).TotalSeconds));
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.RefreshToken))
            return BadRequest(new { erro = "Refresh token obrigatório." });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (tokens, usuario, tenantInfo) = await sessionService.RefreshAsync(req.RefreshToken, ip);
        if (tokens == null || usuario == null)
            return Unauthorized(new { erro = "Sessão inválida, expirada ou revogada. Faça login novamente." });

        return Ok(new LoginResponse(tokens.AccessToken, new UserDto(usuario.Id, usuario.Nome, usuario.Email, usuario.Perfil, usuario.TenantId), ToDto(tenantInfo), tokens.RefreshToken, (int)(tokens.AccessExpiresAt - DateTime.UtcNow).TotalSeconds));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest? req)
    {
        if (!string.IsNullOrWhiteSpace(req?.RefreshToken))
            await sessionService.RevokeAsync(req.RefreshToken);
        else if (tenant.CurrentUserId.HasValue)
            await sessionService.RevokeAllForUserAsync(tenant.CurrentUserId.Value, "logout-manual");

        return Ok(new { mensagem = "Sessão encerrada com sucesso." });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        if (tenant.CurrentUserId == null) return Unauthorized();
        using var _ = tenant.SystemScope();
        var uid = tenant.CurrentUserId.Value;
        var u = await db.Usuarios.AsNoTracking().Include(x => x.Tenant).ThenInclude(t => t!.Plano).FirstOrDefaultAsync(x => x.Id == uid);
        if (u == null || !u.Ativo) return Unauthorized();
        return Ok(new { usuario = new UserDto(u.Id, u.Nome, u.Email, u.Perfil, u.TenantId), tenant = ToDto(u.Tenant) });
    }

    [HttpPost("register-tenant")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> RegisterTenant([FromBody] RegisterTenantRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.RazaoSocial) || string.IsNullOrWhiteSpace(r.Cnpj) || string.IsNullOrWhiteSpace(r.AdminEmail) || string.IsNullOrWhiteSpace(r.AdminNome))
            return BadRequest(new { erro = "Razão social, CNPJ, nome e e-mail do administrador são obrigatórios." });
        if (!Secrets.PasswordIsStrong(r.AdminSenha))
            return BadRequest(new { erro = "A senha deve ter ao menos 8 caracteres, com letras e números." });
        if (!r.AdminEmail.Contains('@')) return BadRequest(new { erro = "E-mail do administrador inválido." });
        var cnpj = new string(r.Cnpj.Where(char.IsDigit).ToArray());
        if (cnpj.Length != 14) return BadRequest(new { erro = "CNPJ deve conter 14 dígitos." });

        using var _ = tenant.SystemScope();
        if (await db.Tenants.AnyAsync(t => t.Cnpj == cnpj)) return BadRequest(new { erro = "Já existe uma empresa cadastrada com este CNPJ." });
        var email = r.AdminEmail.Trim().ToLowerInvariant();
        if (await db.Usuarios.AnyAsync(u => u.Email.ToLower() == email)) return BadRequest(new { erro = "Este e-mail já está cadastrado na plataforma." });

        var plano = r.PlanoId.HasValue ? await db.Planos.FirstOrDefaultAsync(p => p.Id == r.PlanoId && p.Ativo) : null;
        plano ??= await db.Planos.Where(p => p.Ativo).OrderBy(p => p.Mensalidade).FirstOrDefaultAsync();

        string slugFinal;
        if (!string.IsNullOrWhiteSpace(r.Slug))
        {
            var (valido, slugNorm, erroSlug) = SlugHelper.ValidateSlug(r.Slug);
            if (!valido) return BadRequest(new { erro = erroSlug });
            if (await db.Tenants.IgnoreQueryFilters().AnyAsync(x => x.Slug.ToLower() == slugNorm))
                return BadRequest(new { erro = $"O endereço '/{slugNorm}' já está em uso por outra empresa." });
            slugFinal = slugNorm;
        }
        else
        {
            var baseSlug = SlugHelper.NormalizeSlug(!string.IsNullOrWhiteSpace(r.NomeFantasia) ? r.NomeFantasia : r.RazaoSocial);
            if (string.IsNullOrWhiteSpace(baseSlug) || SlugHelper.IsReservedSlug(baseSlug) || baseSlug.Length < 2)
                baseSlug = $"empresa-{cnpj[..8]}";
            slugFinal = baseSlug;
            int suffix = 2;
            while (await db.Tenants.IgnoreQueryFilters().AnyAsync(x => x.Slug.ToLower() == slugFinal))
            {
                slugFinal = $"{baseSlug}-{suffix++}";
            }
        }

        var t = new Tenant
        {
            Slug = slugFinal,
            RazaoSocial = r.RazaoSocial.Trim(),
            NomeFantasia = string.IsNullOrWhiteSpace(r.NomeFantasia) ? r.RazaoSocial.Trim() : r.NomeFantasia.Trim(),
            Cnpj = cnpj,
            Email = r.Email?.Trim() ?? string.Empty,
            Telefone = r.Telefone?.Trim() ?? string.Empty,
            Status = "Ativo",
            PlanoId = plano?.Id,
            DataExpiracao = DateTime.UtcNow.AddDays(15),
            ModuloContratado = "Basico",
            VolumeUtilizacao = "Baixo",
            MensalidadeCalculada = 50.00m,
            MensalidadeFinal = 50.00m,
            DataInicioCobranca = DateTime.UtcNow,
            ProximoVencimento = DateTime.UtcNow.AddDays(15),
            SituacaoFinanceira = "Regular"
        };
        var admin = new Usuario
        {
            Nome = r.AdminNome.Trim(), Email = email, SenhaHash = BCrypt.Net.BCrypt.HashPassword(r.AdminSenha), Perfil = "TenantAdmin", TenantId = t.Id,
        };
        db.Tenants.Add(t);
        db.Usuarios.Add(admin);
        db.ConfiguracoesTenant.Add(new ConfiguracaoTenant { TenantId = t.Id, Uf = (r.Uf ?? string.Empty).Trim().ToUpperInvariant() });
        if (plano != null)
            db.Assinaturas.Add(new Assinatura { TenantId = t.Id, PlanoId = plano.Id, Status = "Teste", DataFim = DateTime.UtcNow.AddDays(15), Valor = t.MensalidadeFinal });
        audit.Add("EmpresaCriada", "Tenant", t.Id.ToString(), new { t.Cnpj, t.Slug, plano = plano?.Nome }, t.Id, admin.Id);
        await db.SaveChangesAsync();

        t.Plano = plano;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var tokens = await sessionService.StartAsync(admin, t, ip);
        return Ok(new LoginResponse(tokens.AccessToken, new UserDto(admin.Id, admin.Nome, admin.Email, admin.Perfil, t.Id), ToDto(t), tokens.RefreshToken, (int)(tokens.AccessExpiresAt - DateTime.UtcNow).TotalSeconds));
    }
}

