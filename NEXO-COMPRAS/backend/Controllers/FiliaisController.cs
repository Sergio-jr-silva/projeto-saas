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
public class FiliaisController(IFilialService filialService, NexoDbContext db, ITenantContext tenant) : ControllerBase
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
        var lista = await filialService.ListarFiliaisAsync(tenantId);
        return Ok(lista);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id)
    {
        var tenantId = ObterTenantId();
        var filial = await filialService.ObterFilialPorIdAsync(tenantId, id);
        if (filial == null) return NotFound(new { erro = "Filial não encontrada." });
        return Ok(filial);
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] SalvarFilialRequest req)
    {
        var tenantId = ObterTenantId();
        var (filial, erro) = await filialService.SalvarFilialAsync(tenantId, null, req);
        if (erro != null) return BadRequest(new { erro });
        return CreatedAtAction(nameof(ObterPorId), new { id = filial!.Id }, filial);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] SalvarFilialRequest req)
    {
        var tenantId = ObterTenantId();
        var (filial, erro) = await filialService.SalvarFilialAsync(tenantId, id, req);
        if (erro != null) return BadRequest(new { erro });
        return Ok(filial);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Desativar(Guid id)
    {
        var tenantId = ObterTenantId();
        var sucesso = await filialService.DesativarFilialAsync(tenantId, id);
        if (!sucesso) return NotFound(new { erro = "Filial não encontrada." });
        return Ok(new { ok = true, mensagem = "Filial desativada com sucesso." });
    }

    [HttpGet("{id:guid}/estoque")]
    public async Task<IActionResult> ObterEstoqueFilial(Guid id)
    {
        var tenantId = ObterTenantId();
        var estoque = await filialService.ObterEstoqueFilialAsync(tenantId, id);
        return Ok(estoque);
    }

    [HttpPost("{id:guid}/estoque")]
    public async Task<IActionResult> AtualizarEstoqueFilial(Guid id, [FromBody] AtualizarEstoqueFilialRequest req)
    {
        var tenantId = ObterTenantId();
        var ok = await filialService.AtualizarEstoqueFilialAsync(tenantId, id, req);
        return Ok(new { ok = true });
    }

    [HttpGet("relatorio-consolidado")]
    public async Task<IActionResult> RelatorioConsolidado()
    {
        var tenantId = ObterTenantId();
        var rel = await filialService.ObterRelatorioConsolidadoAsync(tenantId);
        return Ok(rel);
    }

    [HttpPost("{id:guid}/renovar-token")]
    public async Task<IActionResult> RenovarToken(Guid id)
    {
        var tenantId = ObterTenantId();
        var (novoToken, erro) = await filialService.RenovarTokenAsync(tenantId, id);
        if (erro != null) return BadRequest(new { erro });
        return Ok(new { token = novoToken, mensagem = "Token da filial renovado com sucesso." });
    }

    [HttpPost("{id:guid}/revogar-token")]
    public async Task<IActionResult> RevogarToken(Guid id)
    {
        var tenantId = ObterTenantId();
        var sucesso = await filialService.RevogarTokenAsync(tenantId, id);
        if (!sucesso) return NotFound(new { erro = "Filial não encontrada." });
        return Ok(new { ok = true, mensagem = "Token da filial revogado com sucesso." });
    }
}

