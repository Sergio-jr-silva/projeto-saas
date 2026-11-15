using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Nexo.Api.Models;

namespace Nexo.Api.Security;

public class JwtOptions
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "https://nexocompras.com.br";
    public string Audience { get; set; } = "https://app.nexocompras.com.br";
    /// <summary>Vida do access token. Curta de propósito: a renovação é feita pelo refresh token (com rotação e revogação).</summary>
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 14;
    public int PortalSessionMinutes { get; set; } = 120;
    public const string PortalAudience = "nexo-portal";

    public SymmetricSecurityKey UserKey => new(Encoding.UTF8.GetBytes(Secret));
    /// <summary>Chave derivada, distinta da chave dos usuários: um token do portal nunca vale como token de usuário.</summary>
    public SymmetricSecurityKey PortalKey => new(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes("nexo-portal-v1")));
}

public interface IJwtService
{
    (string Token, DateTime ExpiresAt) GenerateToken(Usuario usuario, Tenant? tenant, Guid sessionId);
    string GenerateToken(Usuario usuario, Tenant? tenant);
    string GeneratePortalToken(Guid acessoId, Guid cotacaoFornecedorId, Guid tenantId);
}

public class JwtService(JwtOptions o) : IJwtService
{
    public string GenerateToken(Usuario u, Tenant? tenant) => GenerateToken(u, tenant, Guid.NewGuid()).Token;

    public (string Token, DateTime ExpiresAt) GenerateToken(Usuario u, Tenant? tenant, Guid sessionId)
    {
        var claims = new List<Claim>
        {
            new("uid", u.Id.ToString()),
            new(ClaimTypes.NameIdentifier, u.Id.ToString()),
            new(ClaimTypes.Name, u.Nome),
            new(ClaimTypes.Email, u.Email),
            new(ClaimTypes.Role, u.Perfil),
            new("tenant_id", u.TenantId?.ToString() ?? string.Empty),
            new("sid", sessionId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        var exp = DateTime.UtcNow.AddMinutes(o.AccessTokenMinutes);
        var token = new JwtSecurityToken(o.Issuer, o.Audience, claims, notBefore: DateTime.UtcNow.AddSeconds(-5), expires: exp,
            signingCredentials: new SigningCredentials(o.UserKey, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), exp);
    }

    public string GeneratePortalToken(Guid acessoId, Guid cotacaoFornecedorId, Guid tenantId)
    {
        var claims = new List<Claim>
        {
            new("acesso_id", acessoId.ToString()),
            new("cf_id", cotacaoFornecedorId.ToString()),
            new("tenant_id", tenantId.ToString()),
            new(ClaimTypes.Role, "Supplier"),
        };
        var token = new JwtSecurityToken(o.Issuer, JwtOptions.PortalAudience, claims, expires: DateTime.UtcNow.AddMinutes(o.PortalSessionMinutes),
            signingCredentials: new SigningCredentials(o.PortalKey, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
