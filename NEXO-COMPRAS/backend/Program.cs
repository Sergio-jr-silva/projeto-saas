using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Nexo.Api.Data;
using Nexo.Api.MultiTenancy;
using Nexo.Api.Security;
using Nexo.Api.Services;
using Nexo.Api.Services.Fiscal;

var builder = WebApplication.CreateBuilder(args);

// Configuração de porta dinâmica (compatível com Railway, Docker e execução local)
var portEnv = Environment.GetEnvironmentVariable("PORT");
var urls = !string.IsNullOrWhiteSpace(portEnv)
    ? $"http://0.0.0.0:{portEnv}"
    : (Environment.GetEnvironmentVariable("ASPNETCORE_URLS") 
        ?? builder.Configuration["Urls"] 
        ?? "http://0.0.0.0:5000");
builder.WebHost.UseUrls(urls);

// 1. Configuração do Banco de Dados PostgreSQL (suporta connection string padrão e DATABASE_URL do Railway)
var rawConn = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=127.0.0.1;Port=5433;Database=nexo;Username=postgres;Password=postgres";
var connectionString = ParseConnectionString(rawConn);

builder.Services.AddScoped<TenantConnectionInterceptor>();
builder.Services.AddDbContext<NexoDbContext>((sp, options) =>
{
    options.UseNpgsql(connectionString);
    options.AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>());
});

// 2. Injeção de Dependências de Segurança e Multi-Tenancy
builder.Services.AddScoped<ITenantContext, TenantContext>();

var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "NexoComprasSaaSSecretKey2026SuperSecureKeyWith32CharsMin!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "https://nexocompras.com.br";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "https://app.nexocompras.com.br";
var jwtOptions = new JwtOptions
{
    Secret = jwtSecret,
    Issuer = jwtIssuer,
    Audience = jwtAudience
};
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<IJwtService, JwtService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ISessionService, SessionService>();

// 3. Serviços de Domínio e Infraestrutura
builder.Services.AddScoped<ITenantConfigService, TenantConfigService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IAssinaturaService, AssinaturaService>();
builder.Services.AddScoped<IFilialService, FilialService>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();

// 4. Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy("portal-acesso", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy("portal", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// 5. Autenticação Multi-Scheme (Bearer, Agent, Portal)
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = jwtOptions.UserKey,
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
})
.AddJwtBearer("Portal", options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = jwtOptions.PortalKey,
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = JwtOptions.PortalAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
})
.AddScheme<AuthenticationSchemeOptions, AgentAuthHandler>("Agent", _ => { });

// 6. CORS para Frontend Vite e Ambientes SaaS
var allowedOriginsEnv = Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS");
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (!string.IsNullOrWhiteSpace(allowedOriginsEnv))
        {
            var origins = allowedOriginsEnv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            policy.WithOrigins(origins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            policy.SetIsOriginAllowed(origin => true)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
    });
});

builder.Services.AddControllers();

var app = builder.Build();

// 7. Inicialização e Seed do Banco
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<NexoDbContext>();
    await DbInitializer.InitializeAsync(dbContext);
}

// 8. Pipeline HTTP
app.UseCors();
app.UseRateLimiter();

app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>();
app.UseAuthorization();

// Endpoints de Verificação de Saúde (Health Check)
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    service = "nexo-compras-api",
    version = "1.0.0"
}));
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    service = "nexo-compras-api",
    version = "1.0.0"
}));

app.MapControllers();

app.Run();

// Necessário para testes de integração no xUnit com WebApplicationFactory
public partial class Program
{
    public static string ParseConnectionString(string rawConn)
    {
        if (string.IsNullOrWhiteSpace(rawConn)) return rawConn;

        if (rawConn.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            rawConn.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(rawConn);
                var userInfo = uri.UserInfo.Split(':');
                var user = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "postgres";
                var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
                var db = uri.AbsolutePath.TrimStart('/');
                var port = uri.Port > 0 ? uri.Port : 5432;
                return $"Host={uri.Host};Port={port};Database={db};Username={user};Password={pass};SSL Mode=Prefer;Trust Server Certificate=true;";
            }
            catch
            {
                return rawConn;
            }
        }
        return rawConn;
    }
}
