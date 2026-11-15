using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.DTOs;
using Nexo.Api.MultiTenancy;
using Nexo.Api.Services;

namespace Nexo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "TenantAdmin,PlatformAdmin")]
public class AssinaturasController(IAssinaturaService assinaturaService, NexoDbContext db, ITenantContext tenant) : ControllerBase
{
    private Guid ObterTenantId()
    {
        if (tenant.CurrentTenantId.HasValue) return tenant.CurrentTenantId.Value;
        // Se for PlatformAdmin, obtém a primeira loja ativa
        var t = db.Tenants.FirstOrDefault(x => x.Status == "Ativo");
        return t?.Id ?? throw new UnauthorizedAccessException("Nenhuma loja ativa identificada.");
    }

    [HttpGet("minha")]
    public async Task<IActionResult> MinhaAssinatura()
    {
        var tenantId = ObterTenantId();
        var detalhe = await assinaturaService.ObterAssinaturaAtualAsync(tenantId);
        return Ok(detalhe);
    }

    [HttpGet("planos")]
    [AllowAnonymous]
    public async Task<IActionResult> ListarPlanos()
    {
        var planos = await db.Planos.Where(p => p.Ativo).OrderBy(p => p.Mensalidade).ToListAsync();
        return Ok(planos);
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout([FromBody] CheckoutAssinaturaRequest req)
    {
        var tenantId = ObterTenantId();
        var fatura = await assinaturaService.RealizarCheckoutAsync(tenantId, req);
        return Ok(fatura);
    }

    [HttpPost("cancelar")]
    public async Task<IActionResult> Cancelar([FromBody] CancelarAssinaturaRequest req)
    {
        var tenantId = ObterTenantId();
        var sucesso = await assinaturaService.CancelarAssinaturaAsync(tenantId, req.Motivo);
        if (!sucesso) return BadRequest(new { erro = "Não foi possível cancelar a assinatura." });
        return Ok(new { ok = true, mensagem = "Assinatura cancelada com sucesso." });
    }

    [HttpPost("reativar")]
    public async Task<IActionResult> Reativar()
    {
        var tenantId = ObterTenantId();
        var sucesso = await assinaturaService.ReativarAssinaturaAsync(tenantId);
        if (!sucesso) return BadRequest(new { erro = "Não foi possível reativar a assinatura." });
        return Ok(new { ok = true, mensagem = "Assinatura reativada com sucesso." });
    }

    [HttpGet("faturas")]
    public async Task<IActionResult> Faturas()
    {
        var tenantId = ObterTenantId();
        var lista = await assinaturaService.ListarFaturasAsync(tenantId);
        return Ok(lista);
    }

    [HttpPost("faturas/{id:guid}/pagar")]
    public async Task<IActionResult> PagarFatura(Guid id, [FromBody] PagarFaturaSimuladaRequest req)
    {
        var tenantId = ObterTenantId();
        var fatura = await assinaturaService.PagarFaturaAsync(tenantId, id, req.Metodo);
        if (fatura == null) return NotFound(new { erro = "Fatura não encontrada." });
        return Ok(fatura);
    }
}

