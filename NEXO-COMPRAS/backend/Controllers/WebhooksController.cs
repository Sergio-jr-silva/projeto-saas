using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexo.Api.DTOs;
using Nexo.Api.Services;

namespace Nexo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class WebhooksController(IAssinaturaService assinaturaService) : ControllerBase
{
    [HttpPost("pagamentos")]
    public async Task<IActionResult> ProcessarWebhookPagamento([FromBody] WebhookPagamentoPayload payload)
    {
        if (payload == null || string.IsNullOrWhiteSpace(payload.Evento))
            return BadRequest(new { erro = "Payload de webhook inválido." });

        var processado = await assinaturaService.ProcessarWebhookAsync(payload);
        if (!processado)
            return NotFound(new { erro = "Fatura ou transação não encontrada para processamento." });

        return Ok(new { ok = true, processado = true, timestamp = DateTime.UtcNow });
    }
}

