using System.Text.Json;
using Nexo.Api.Data;
using Nexo.Api.Models;
using Nexo.Api.MultiTenancy;

namespace Nexo.Api.Services;

public interface IAuditService
{
    /// <summary>Registra o evento no contexto atual (não chama SaveChanges; use junto de outra gravação ou LogNowAsync).</summary>
    void Add(string acao, string entidade, string entidadeId, object? dados = null, Guid? tenantId = null, Guid? usuarioId = null);
    Task LogNowAsync(string acao, string entidade, string entidadeId, object? dados = null, Guid? tenantId = null, Guid? usuarioId = null);
}

public class AuditService(NexoDbContext db, ITenantContext tenant, ILogger<AuditService> log) : IAuditService
{
    public void Add(string acao, string entidade, string entidadeId, object? dados = null, Guid? tenantId = null, Guid? usuarioId = null)
    {
        db.LogsAuditoria.Add(new LogAuditoria
        {
            TenantId = tenantId ?? tenant.CurrentTenantId,
            UsuarioId = usuarioId ?? tenant.CurrentUserId,
            Acao = acao, Entidade = entidade, EntidadeId = entidadeId,
            DadosJson = dados == null ? "{}" : JsonSerializer.Serialize(dados),
            Ip = tenant.Ip,
        });
        log.LogInformation("AUDIT {Acao} {Entidade} {Id} tenant={Tenant}", acao, entidade, entidadeId, tenantId ?? tenant.CurrentTenantId);
    }

    public async Task LogNowAsync(string acao, string entidade, string entidadeId, object? dados = null, Guid? tenantId = null, Guid? usuarioId = null)
    {
        Add(acao, entidade, entidadeId, dados, tenantId, usuarioId);
        await db.SaveChangesAsync();
    }
}

