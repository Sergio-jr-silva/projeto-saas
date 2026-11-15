using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.DTOs;
using Nexo.Api.Models;
using Nexo.Api.MultiTenancy;
using Nexo.Api.Security;
using Nexo.Api.Services;

namespace Nexo.Api.Controllers;

/// <summary>Gestão da integração ERP pelo comprador: chaves do agente, intervalos e painel de sincronização.</summary>
[ApiController]
[Route("api/integration")]
[Authorize(Roles = "TenantAdmin,Buyer,Viewer,PlatformAdmin")]
public class IntegrationController(NexoDbContext db, ITenantContext tenant, IAuditService audit) : ControllerBase
{
    public static string StatusEfetivo(Agente a)
    {
        if (a.Revogado) return "Revogado";
        if (a.UltimoHeartbeat == null) return "Pendente";
        var limite = TimeSpan.FromSeconds(Math.Max(180, a.IntervaloHeartbeatSeg * 3));
        return DateTime.UtcNow - a.UltimoHeartbeat > limite ? "Offline" : a.Status == "Erro" ? "Erro" : "Online";
    }

    private static AgenteDto ToDto(Agente a)
    {
        JsonElement? amb = null;
        try { amb = JsonSerializer.Deserialize<JsonElement>(a.AmbienteJson); } catch { /* ignore */ }
        return new AgenteDto(a.Id, a.Nome, a.ChavePrefixo, StatusEfetivo(a), a.Versao, a.NomeMaquina, a.UltimoHeartbeat, a.UltimaSincronizacao, a.Revogado,
            a.DataCriacao, a.ChaveRotacionadaEm, amb, a.IntervaloEstoqueMin, a.IntervaloProdutosMin, a.IntervaloFornecedoresMin, a.IntervaloHeartbeatSeg);
    }

    [HttpGet("loja-info")]
    [Authorize(Roles = "TenantAdmin,Buyer,Viewer,PlatformAdmin")]
    public async Task<IActionResult> LojaInfo()
    {
        var tenantId = tenant.CurrentTenantId;
        Tenant? empresa = null;

        if (tenantId.HasValue)
        {
            empresa = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId.Value);
        }
        else
        {
            // PlatformAdmin acessando a tela de integração: pega a primeira loja ativa ou cria loja padrão
            empresa = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Status == "Ativo");
            if (empresa == null)
            {
                empresa = new Tenant
                {
                    Id = Guid.NewGuid(),
                    RazaoSocial = "Supermercado Matriz Ltda",
                    NomeFantasia = "Supermercado Matriz (BM.FDB)",
                    Cnpj = "12345678000199",
                    Status = "Ativo"
                };
                db.Tenants.Add(empresa);
                await db.SaveChangesAsync();
            }
        }

        if (empresa == null) return NotFound(new { erro = "Nenhuma loja cadastrada." });

        return Ok(new
        {
            id = empresa.Id,
            razaoSocial = empresa.RazaoSocial,
            nomeFantasia = empresa.NomeFantasia,
            cnpj = empresa.Cnpj,
            status = empresa.Status
        });
    }

    [HttpGet("agents")]
    public async Task<IActionResult> Agents() => Ok((await db.Agentes.AsNoTracking().OrderByDescending(a => a.DataCriacao).ToListAsync()).Select(ToDto));

    [HttpPost("agents/generate-key")]
    [HttpPost("generate-key")]
    [HttpPost("agents")]
    [Authorize(Roles = "TenantAdmin,Buyer,PlatformAdmin")]
    public async Task<IActionResult> CreateAgent([FromBody] GenerateAgentKeyRequest? r)
    {
        var tenantId = tenant.CurrentTenantId;
        Tenant? empresa = null;

        if (tenantId.HasValue)
        {
            empresa = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId.Value);
        }
        else
        {
            empresa = await db.Tenants.FirstOrDefaultAsync(t => t.Status == "Ativo");
            if (empresa == null)
            {
                empresa = new Tenant
                {
                    Id = Guid.NewGuid(),
                    RazaoSocial = "Supermercado Matriz Ltda",
                    NomeFantasia = "Supermercado Matriz (BM.FDB)",
                    Cnpj = "12345678000199",
                    Status = "Ativo"
                };
                db.Tenants.Add(empresa);
                await db.SaveChangesAsync();
            }
            tenantId = empresa.Id;
        }

        var storeId = tenantId!.Value;
        var nomeAgente = string.IsNullOrWhiteSpace(r?.Nome) ? "Servidor Local BM.FDB" : r.Nome.Trim()[..Math.Min(80, r.Nome.Trim().Length)];

        var a = await db.Agentes.FirstOrDefaultAsync(x => x.TenantId == storeId && !x.Revogado);
        if (a == null)
        {
            a = new Agente
            {
                Id = Guid.NewGuid(),
                TenantId = storeId,
                Nome = nomeAgente,
                TipoBanco = "Firebird",
                Status = "Online"
            };
            db.Agentes.Add(a);
        }

        var (key, hash, prefix) = AgentKey.Generate(a.Id);
        a.ChaveHash = hash; 
        a.ChavePrefixo = prefix;
        a.ChaveRotacionadaEm = DateTime.UtcNow;
        audit.Add("AgenteCriado", "Agente", a.Id.ToString());
        await db.SaveChangesAsync();

        return Ok(new
        {
            id = a.Id,
            key = key,
            chaveAtivacao = storeId.ToString(), // O ID DA LOJA É A CHAVE ÚNICA DE ATIVAÇÃO!
            chave = storeId.ToString(),
            instrucoes = "Copie o ID DA LOJA acima e cole no aplicativo Sincronizador Local (no botão '🔑 Vincular Loja') para iniciar a sincronização com o banco Firebird C:\\DATACASH\\BM.FDB.",
            tokenExclusivo = key
        });
    }

    [HttpPost("agents/{id:guid}/rotate-key")]
    [Authorize(Roles = "TenantAdmin")]
    public async Task<IActionResult> Rotate(Guid id)
    {
        var a = await db.Agentes.FindAsync(id);
        if (a == null || a.Revogado) return NotFound(new { erro = "Agente não encontrado ou revogado." });
        var (key, hash, prefix) = AgentKey.Generate(a.Id);
        a.ChaveHash = hash; a.ChavePrefixo = prefix; a.ChaveRotacionadaEm = DateTime.UtcNow;
        audit.Add("AgenteChaveRotacionada", "Agente", a.Id.ToString());
        await db.SaveChangesAsync();
        return Ok(new AgentKeyResponse(a.Id, key, "A chave anterior foi invalidada. Atualize o appsettings.json do agente com a nova chave."));
    }

    [HttpPost("agents/{id:guid}/revoke")]
    [Authorize(Roles = "TenantAdmin")]
    public async Task<IActionResult> Revoke(Guid id)
    {
        var a = await db.Agentes.FindAsync(id);
        if (a == null) return NotFound(new { erro = "Agente não encontrado." });
        a.Revogado = true; a.Status = "Revogado";
        audit.Add("AgenteRevogado", "Agente", a.Id.ToString());
        await db.SaveChangesAsync();
        return Ok(ToDto(a));
    }

    [HttpPut("agents/{id:guid}/intervals")]
    [Authorize(Roles = "TenantAdmin")]
    public async Task<IActionResult> Intervals(Guid id, [FromBody] UpdateAgentIntervalsRequest r)
    {
        if (r.IntervaloEstoqueMin is < 1 or > 1440 || r.IntervaloProdutosMin is < 1 or > 1440 || r.IntervaloFornecedoresMin is < 1 or > 1440 || r.IntervaloHeartbeatSeg is < 15 or > 3600)
            return BadRequest(new { erro = "Intervalos inválidos (minutos: 1–1440; heartbeat: 15–3600 segundos)." });
        var a = await db.Agentes.FindAsync(id);
        if (a == null) return NotFound(new { erro = "Agente não encontrado." });
        a.IntervaloEstoqueMin = r.IntervaloEstoqueMin; a.IntervaloProdutosMin = r.IntervaloProdutosMin;
        a.IntervaloFornecedoresMin = r.IntervaloFornecedoresMin; a.IntervaloHeartbeatSeg = r.IntervaloHeartbeatSeg;
        audit.Add("AgenteIntervalosAlterados", "Agente", a.Id.ToString(), r);
        await db.SaveChangesAsync();
        return Ok(ToDto(a));
    }

    [HttpGet("syncs")]
    public async Task<IActionResult> Syncs([FromQuery] int take = 50) =>
        Ok(await db.Sincronizacoes.AsNoTracking().Include(s => s.Agente).OrderByDescending(s => s.DataCriacao).Take(Math.Clamp(take, 1, 200))
            .Select(s => new SincronizacaoDto(s.Id, s.AgenteId, s.Agente!.Nome, s.Tipo, s.Recebidos, s.Criados, s.Atualizados, s.Ignorados, s.Status, s.Mensagem, s.DataCriacao)).ToListAsync());

    [HttpGet("errors")]
    public async Task<IActionResult> Errors([FromQuery] int take = 50) =>
        Ok(await db.SincronizacaoErros.AsNoTracking().OrderByDescending(e => e.DataCriacao).Take(Math.Clamp(take, 1, 200))
            .Select(e => new SincronizacaoErroDto(e.Id, e.AgenteId, e.Origem, e.Mensagem, e.Detalhe, e.DataCriacao)).ToListAsync());
}

