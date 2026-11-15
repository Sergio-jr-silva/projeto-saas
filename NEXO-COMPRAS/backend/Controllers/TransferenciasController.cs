using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexo.Api.Data;
using Nexo.Api.DTOs;
using Nexo.Api.MultiTenancy;
using Nexo.Api.Services;

namespace Nexo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "TenantAdmin,Buyer,PlatformAdmin")]
public class TransferenciasController(IFilialService filialService, NexoDbContext db, ITenantContext tenant) : ControllerBase
{
    private Guid ObterTenantId()
    {
        if (tenant.CurrentTenantId.HasValue) return tenant.CurrentTenantId.Value;
        var t = db.Tenants.FirstOrDefault(x => x.Status == "Ativo");
        return t?.Id ?? throw new UnauthorizedAccessException("Nenhuma loja ativa identificada.");
    }

    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var tenantId = ObterTenantId();
        var lista = await filialService.ListarTransferenciasAsync(tenantId);
        return Ok(lista);
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarTransferenciaRequest req)
    {
        var tenantId = ObterTenantId();
        var uid = tenant.CurrentUserId ?? Guid.Empty;
        var nome = User.FindFirstValue(ClaimTypes.Name) ?? "Operador Compras";

        var (transf, erro) = await filialService.CriarTransferenciaAsync(tenantId, uid, nome, req);
        if (erro != null) return BadRequest(new { erro });
        return Ok(transf);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> AtualizarStatus(Guid id, [FromBody] AtualizarStatusTransferenciaRequest req)
    {
        var tenantId = ObterTenantId();
        var uid = tenant.CurrentUserId ?? Guid.Empty;
        var nome = User.FindFirstValue(ClaimTypes.Name) ?? "Aprovador Compras";

        var atualizada = await filialService.AtualizarStatusTransferenciaAsync(tenantId, id, uid, nome, req);
        if (atualizada == null) return NotFound(new { erro = "Transferência não encontrada." });
        return Ok(atualizada);
    }
}

