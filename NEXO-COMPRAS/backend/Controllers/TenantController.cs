using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.DTOs;
using Nexo.Api.Models;
using Nexo.Api.MultiTenancy;
using Nexo.Api.Security;
using Nexo.Api.Services;
using Nexo.Api.Services.Fiscal;

namespace Nexo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "TenantAdmin,Buyer,Viewer,PlatformAdmin")]
public class TenantController(NexoDbContext db, ITenantContext tenant, ITenantConfigService cfgService, IAuditService audit, IFileStorage fileStorage) : ControllerBase
{
    [HttpGet("info")]
    public async Task<IActionResult> Info()
    {
        if (!tenant.CurrentTenantId.HasValue) return BadRequest(new { erro = "Nenhum tenant associado ao contexto." });
        var t = await db.Tenants.Include(x => x.Plano).FirstOrDefaultAsync();
        if (t == null) return NotFound(new { erro = "Empresa não encontrada." });
        return Ok(new
        {
            t.Id, t.RazaoSocial, t.NomeFantasia, t.Cnpj, t.Email, t.Telefone, t.Status, t.DataExpiracao,
            t.InscricaoEstadual, t.InscricaoMunicipal,
            t.Endereco, t.Numero, t.Complemento, t.Bairro, t.Cidade, t.Uf, t.Cep,
            t.LogoUrl, t.LogoMimeType, t.LogoAtualizadaEm,
            Plano = t.Plano == null ? null : new { t.Plano.Id, t.Plano.Nome, t.Plano.MaxUsuarios, t.Plano.MaxFiliais, t.Plano.MaxFornecedores, t.Plano.MaxCotacoesMes },
            TotalUsuarios = await db.Usuarios.CountAsync(), TotalFiliais = await db.Filiais.CountAsync(), TotalAgentes = await db.Agentes.CountAsync(a => !a.Revogado),
        });
    }

    public record UpdateTenantInfoRequest(
        string? RazaoSocial,
        string? NomeFantasia,
        string? Cnpj,
        string? InscricaoEstadual,
        string? InscricaoMunicipal,
        string? Endereco,
        string? Numero,
        string? Complemento,
        string? Bairro,
        string? Cidade,
        string? Uf,
        string? Cep,
        string? Telefone,
        string? Email
    );

    [HttpPut("info")]
    [Authorize(Roles = "TenantAdmin")]
    public async Task<IActionResult> UpdateInfo([FromBody] UpdateTenantInfoRequest r)
    {
        if (!tenant.CurrentTenantId.HasValue) return BadRequest(new { erro = "Nenhum tenant associado ao contexto." });
        var t = await db.Tenants.FirstOrDefaultAsync();
        if (t == null) return NotFound(new { erro = "Empresa não encontrada." });

        if (!string.IsNullOrWhiteSpace(r.RazaoSocial)) t.RazaoSocial = r.RazaoSocial.Trim();
        if (r.NomeFantasia != null) t.NomeFantasia = r.NomeFantasia.Trim();
        if (!string.IsNullOrWhiteSpace(r.Cnpj)) t.Cnpj = new string(r.Cnpj.Where(char.IsDigit).ToArray());
        if (r.InscricaoEstadual != null) t.InscricaoEstadual = r.InscricaoEstadual.Trim();
        if (r.InscricaoMunicipal != null) t.InscricaoMunicipal = r.InscricaoMunicipal.Trim();
        if (r.Endereco != null) t.Endereco = r.Endereco.Trim();
        if (r.Numero != null) t.Numero = r.Numero.Trim();
        if (r.Complemento != null) t.Complemento = r.Complemento.Trim();
        if (r.Bairro != null) t.Bairro = r.Bairro.Trim();
        if (r.Cidade != null) t.Cidade = r.Cidade.Trim();
        if (r.Uf != null) t.Uf = r.Uf.Trim().ToUpperInvariant();
        if (r.Cep != null) t.Cep = r.Cep.Trim();
        if (r.Telefone != null) t.Telefone = r.Telefone.Trim();
        if (r.Email != null) t.Email = r.Email.Trim().ToLowerInvariant();

        t.DataAtualizacao = DateTime.UtcNow;
        await db.SaveChangesAsync();
        audit.Add("TenantInfoAtualizada", "Tenant", t.Id.ToString(), new { t.RazaoSocial, t.Cnpj });

        return Ok(new
        {
            t.Id, t.RazaoSocial, t.NomeFantasia, t.Cnpj, t.Email, t.Telefone, t.Status,
            t.InscricaoEstadual, t.InscricaoMunicipal,
            t.Endereco, t.Numero, t.Complemento, t.Bairro, t.Cidade, t.Uf, t.Cep,
            t.LogoUrl, t.LogoMimeType, t.LogoAtualizadaEm
        });
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users() =>
        Ok(await db.Usuarios.OrderBy(u => u.Nome).Select(u => new UserDto(u.Id, u.Nome, u.Email, u.Perfil, u.TenantId)).ToListAsync());

    [HttpPost("users")]
    [Authorize(Roles = "TenantAdmin")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest r)
    {
        var tid = tenant.CurrentTenantId!.Value;
        if (string.IsNullOrWhiteSpace(r.Nome) || string.IsNullOrWhiteSpace(r.Email) || !r.Email.Contains('@')) return BadRequest(new { erro = "Nome e e-mail válidos são obrigatórios." });
        if (!Secrets.PasswordIsStrong(r.Senha)) return BadRequest(new { erro = Secrets.PasswordPolicyMessage });
        var perfil = string.IsNullOrWhiteSpace(r.Perfil) ? "Buyer" : r.Perfil;
        if (perfil is not ("TenantAdmin" or "Buyer" or "Viewer")) return BadRequest(new { erro = "Perfil inválido." });

        var email = r.Email.Trim().ToLowerInvariant();
        using (tenant.SystemScope())
            if (await db.Usuarios.AnyAsync(u => u.Email.ToLower() == email)) return BadRequest(new { erro = "Já existe um usuário com este e-mail." });

        var plano = (await db.Tenants.Include(t => t.Plano).FirstAsync()).Plano;
        if (plano != null && await db.Usuarios.CountAsync() >= plano.MaxUsuarios)
            return BadRequest(new { erro = $"Limite de {plano.MaxUsuarios} usuários do plano '{plano.Nome}' atingido. Faça upgrade para adicionar mais." });

        var u = new Usuario { Nome = r.Nome.Trim(), Email = email, SenhaHash = Secrets.HashPassword(r.Senha), Perfil = perfil, TenantId = tid };
        db.Usuarios.Add(u);
        audit.Add("UsuarioCriado", "Usuario", u.Id.ToString(), new { u.Email, u.Perfil });
        await db.SaveChangesAsync();
        return Ok(new UserDto(u.Id, u.Nome, u.Email, u.Perfil, u.TenantId));
    }

    [HttpGet("branches")]
    public async Task<IActionResult> Branches() => Ok(await db.Filiais.OrderBy(f => f.Nome).ToListAsync());

    [HttpPost("branches")]
    [Authorize(Roles = "TenantAdmin")]
    public async Task<IActionResult> CreateBranch([FromBody] CreateBranchRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Nome)) return BadRequest(new { erro = "Nome da filial é obrigatório." });
        var plano = (await db.Tenants.Include(t => t.Plano).FirstAsync()).Plano;
        if (plano != null && await db.Filiais.CountAsync() >= plano.MaxFiliais)
            return BadRequest(new { erro = $"Limite de {plano.MaxFiliais} filiais do plano '{plano.Nome}' atingido." });
        var f = new Filial
        {
            Codigo = r.Codigo?.Trim() ?? "",
            Nome = r.Nome.Trim(),
            Cnpj = r.Cnpj?.Trim() ?? "",
            Cidade = r.Cidade?.Trim() ?? "",
            Uf = r.Uf?.Trim().ToUpperInvariant() ?? "",
            Token = $"FLK-{Guid.NewGuid():N}"[..24].ToUpperInvariant(),
            TokenGeradoEm = DateTime.UtcNow,
            TokenRevogado = false,
            Ativa = true
        };
        db.Filiais.Add(f);
        await db.SaveChangesAsync();
        return Ok(f);
    }

    private static ConfiguracaoDto ToDto(ConfiguracaoTenant c) => new(c.Uf, c.RegimeTributario, c.CreditaIcms, c.CreditaIpi, c.PercCreditoPisCofins, c.LeadTimePadraoDias, c.DiasSeguranca, c.DiasCobertura, c.ValidadeCotacaoDias);

    [HttpGet("config")]
    public async Task<IActionResult> GetConfig() => Ok(ToDto(await cfgService.GetAsync()));

    [HttpPut("config")]
    [Authorize(Roles = "TenantAdmin")]
    public async Task<IActionResult> PutConfig([FromBody] ConfiguracaoDto r)
    {
        var erros = new TributacaoConfig(r.RegimeTributario, r.CreditaIcms, r.CreditaIpi, r.PercCreditoPisCofins).Validate().ToList();
        if (r.LeadTimePadraoDias is < 0 or > 180) erros.Add("Prazo de entrega padrão deve estar entre 0 e 180 dias.");
        if (r.DiasSeguranca is < 0 or > 180 || r.DiasCobertura is < 1 or > 365) erros.Add("Dias de segurança/cobertura inválidos.");
        if (r.ValidadeCotacaoDias is < 1 or > 90) erros.Add("Validade da cotação deve estar entre 1 e 90 dias.");
        if (erros.Count > 0) return BadRequest(new { erro = string.Join(" ", erros), erros });

        var c = await cfgService.GetAsync();
        c.Uf = (r.Uf ?? "").Trim().ToUpperInvariant(); c.RegimeTributario = r.RegimeTributario; c.CreditaIcms = r.CreditaIcms; c.CreditaIpi = r.CreditaIpi;
        c.PercCreditoPisCofins = r.PercCreditoPisCofins; c.LeadTimePadraoDias = r.LeadTimePadraoDias; c.DiasSeguranca = r.DiasSeguranca;
        c.DiasCobertura = r.DiasCobertura; c.ValidadeCotacaoDias = r.ValidadeCotacaoDias;
        audit.Add("ConfiguracaoAlterada", "ConfiguracaoTenant", c.Id.ToString(), r);
        await db.SaveChangesAsync();
        return Ok(ToDto(c));
    }

    [HttpPost("logo")]
    [Authorize(Roles = "TenantAdmin")]
    public async Task<IActionResult> UploadLogo(IFormFile? arquivo)
    {
        if (!tenant.CurrentTenantId.HasValue)
            return BadRequest(new { erro = "Nenhum tenant associado ao contexto." });

        if (arquivo == null || arquivo.Length == 0)
            return BadRequest(new { erro = "Nenhum arquivo enviado." });

        // Validação de tamanho: máximo 3MB
        if (arquivo.Length > 3 * 1024 * 1024)
            return BadRequest(new { erro = "O arquivo excede o limite máximo permitido de 3MB." });

        var ext = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp" or ".svg"))
            return BadRequest(new { erro = "Formato de arquivo não suportado. Utilize PNG, JPG, WEBP ou SVG." });

        // Validação profunda de assinatura e formato real (Magic Bytes / Sanitização)
        using var stream = arquivo.OpenReadStream();
        var buffer = new byte[Math.Min(1024, arquivo.Length)];
        var bytesRead = await stream.ReadAsync(buffer);
        stream.Position = 0;

        string mimeType;
        if (ext == ".png")
        {
            if (bytesRead < 8 || buffer[0] != 0x89 || buffer[1] != 0x50 || buffer[2] != 0x4E || buffer[3] != 0x47)
                return BadRequest(new { erro = "O conteúdo do arquivo não corresponde a uma imagem PNG válida." });
            mimeType = "image/png";
        }
        else if (ext is ".jpg" or ".jpeg")
        {
            if (bytesRead < 3 || buffer[0] != 0xFF || buffer[1] != 0xD8 || buffer[2] != 0xFF)
                return BadRequest(new { erro = "O conteúdo do arquivo não corresponde a uma imagem JPEG válida." });
            mimeType = "image/jpeg";
        }
        else if (ext == ".webp")
        {
            if (bytesRead < 12 || buffer[0] != 'R' || buffer[1] != 'I' || buffer[2] != 'F' || buffer[3] != 'F' ||
                buffer[8] != 'W' || buffer[9] != 'E' || buffer[10] != 'B' || buffer[11] != 'P')
                return BadRequest(new { erro = "O conteúdo do arquivo não corresponde a uma imagem WEBP válida." });
            mimeType = "image/webp";
        }
        else if (ext == ".svg")
        {
            using var reader = new StreamReader(arquivo.OpenReadStream());
            var svgContent = await reader.ReadToEndAsync();
            var lower = svgContent.ToLowerInvariant();

            if (!lower.Contains("<svg") || !lower.Contains("</svg>"))
                return BadRequest(new { erro = "O arquivo SVG enviado não possui estrutura XML válida." });

            // Sanitização rigorosa contra vetores de ataque XSS
            var forbidden = new[] { "<script", "javascript:", "onload=", "onerror=", "onclick=", "expression(", "<iframe", "<foreignobject" };
            if (forbidden.Any(f => lower.Contains(f)))
                return BadRequest(new { erro = "Arquivo SVG rejeitado por conter scripts ou conteúdo não permitido." });

            mimeType = "image/svg+xml";
        }
        else
        {
            return BadRequest(new { erro = "Tipo de arquivo não suportado." });
        }

        var t = await db.Tenants.FirstOrDefaultAsync();
        if (t == null) return NotFound(new { erro = "Empresa não encontrada." });

        // Remove logo anterior do storage se houver
        if (!string.IsNullOrWhiteSpace(t.LogoStoragePath))
        {
            try { fileStorage.Delete(t.LogoStoragePath); } catch { }
        }

        // Salva novo arquivo
        var storagePath = await fileStorage.SaveLogoAsync(t.Id, ext, stream);

        t.LogoStoragePath = storagePath;
        t.LogoMimeType = mimeType;
        t.LogoAtualizadaEm = DateTime.UtcNow;
        t.LogoUrl = $"/api/tenant/logo?v={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        audit.Add("LogoAtualizada", "Tenant", t.Id.ToString(), new { t.LogoUrl, t.LogoMimeType, Arquivo = arquivo.FileName });
        await db.SaveChangesAsync();

        return Ok(new
        {
            logoUrl = t.LogoUrl,
            mimeType = t.LogoMimeType,
            atualizadaEm = t.LogoAtualizadaEm
        });
    }

    [HttpGet("logo")]
    [AllowAnonymous]
    public async Task<IActionResult> GetLogo([FromQuery] Guid? tenantId = null)
    {
        Tenant? t;
        if (tenantId.HasValue)
        {
            t = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == tenantId.Value);
        }
        else if (tenant.CurrentTenantId.HasValue)
        {
            t = await db.Tenants.FirstOrDefaultAsync(x => x.Id == tenant.CurrentTenantId.Value);
        }
        else
        {
            return NotFound(new { erro = "Empresa não informada ou não autenticada." });
        }

        if (t == null || string.IsNullOrWhiteSpace(t.LogoStoragePath) || !fileStorage.Exists(t.LogoStoragePath))
            return NotFound(new { erro = "Logomarca não cadastrada para esta empresa." });

        Response.Headers.CacheControl = "public, max-age=3600";
        var stream = fileStorage.Open(t.LogoStoragePath);
        return File(stream, !string.IsNullOrWhiteSpace(t.LogoMimeType) ? t.LogoMimeType : "image/png");
    }

    [HttpDelete("logo")]
    [Authorize(Roles = "TenantAdmin")]
    public async Task<IActionResult> RemoverLogo()
    {
        if (!tenant.CurrentTenantId.HasValue)
            return BadRequest(new { erro = "Nenhum tenant associado ao contexto." });

        var t = await db.Tenants.FirstOrDefaultAsync();
        if (t == null) return NotFound(new { erro = "Empresa não encontrada." });

        if (!string.IsNullOrWhiteSpace(t.LogoStoragePath))
        {
            try { fileStorage.Delete(t.LogoStoragePath); } catch { }
        }

        t.LogoStoragePath = "";
        t.LogoUrl = "";
        t.LogoMimeType = "";
        t.LogoAtualizadaEm = null;

        audit.Add("LogoRemovida", "Tenant", t.Id.ToString(), new { });
        await db.SaveChangesAsync();

        return Ok(new { mensagem = "Logomarca removida com sucesso." });
    }
}
