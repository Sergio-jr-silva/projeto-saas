using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.Models;
using Nexo.Api.MultiTenancy;
using Nexo.Api.Services;

namespace Nexo.Api.Controllers;

[ApiController]
[Route("api/pedidos")]
[Authorize]
public class PedidosController : ControllerBase
{
    private readonly NexoDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;

    public PedidosController(NexoDbContext db, ITenantContext tenant, IAuditService audit)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
    }

    public record AtualizarStatusRequest(string NovoStatus, string? Descricao);

    public record TimelineEventoDto(DateTime Data, string Status, string Descricao, string Usuario);

    public record ItemPedidoDiretoRequest(Guid ProdutoId, decimal Quantidade, decimal PrecoUnitario, decimal? DescontoPerc, string? Observacao);
    public record CriarPedidoDiretoRequest(Guid FornecedorId, string? CondicaoPagamento, int? PrazoEntregaDias, decimal? Frete, decimal? OutrasDespesas, string? Observacoes, List<ItemPedidoDiretoRequest> Itens);

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? status,
        [FromQuery] Guid? fornecedorId,
        [FromQuery] string? busca)
    {
        var q = _db.PedidosCompra
            .Include(p => p.Fornecedor)
            .Include(p => p.Cotacao)
            .Include(p => p.Itens)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("todos", StringComparison.OrdinalIgnoreCase))
        {
            var s = status.Trim().ToLower();
            if (s is "rascunho" or "criado")
                q = q.Where(p => p.Status.ToLower() == "criado");
            else if (s is "aguardando" or "aguardandoaprovacao" or "aguardandoconfirmacao")
                q = q.Where(p => p.Status.ToLower() == "aguardandoconfirmacao" || p.Status.ToLower() == "criado");
            else if (s is "aprovado" or "confirmado")
                q = q.Where(p => p.Status.ToLower() == "confirmado");
            else if (s is "enviado")
                q = q.Where(p => p.Status.ToLower() == "enviado");
            else if (s is "ematendimento" or "empreparacao" or "emtransporte")
                q = q.Where(p => p.Status.ToLower() == "empreparacao" || p.Status.ToLower() == "emtransporte");
            else if (s is "concluido" or "recebido")
                q = q.Where(p => p.Status.ToLower() == "recebido");
            else if (s is "cancelado")
                q = q.Where(p => p.Status.ToLower() == "cancelado");
            else
                q = q.Where(p => p.Status.ToLower() == s);
        }

        if (fornecedorId.HasValue)
            q = q.Where(p => p.FornecedorId == fornecedorId.Value);

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var b = busca.Trim().ToLower();
            q = q.Where(p => p.Numero.ToLower().Contains(b) ||
                             (p.Fornecedor != null && (p.Fornecedor.RazaoSocial.ToLower().Contains(b) ||
                             p.Fornecedor.NomeFantasia.ToLower().Contains(b))));
        }

        var lista = await q.OrderByDescending(p => p.DataCriacao)
            .Select(p => new
            {
                p.Id,
                p.Numero,
                p.Status,
                p.CotacaoId,
                CotacaoNumero = p.Cotacao != null ? p.Cotacao.Numero : null,
                p.FornecedorId,
                FornecedorNome = p.Fornecedor != null ? (string.IsNullOrEmpty(p.Fornecedor.NomeFantasia) ? p.Fornecedor.RazaoSocial : p.Fornecedor.NomeFantasia) : "",
                FornecedorCnpj = p.Fornecedor != null ? p.Fornecedor.Cnpj : "",
                p.Subtotal,
                p.Desconto,
                p.Frete,
                p.OutrasDespesas,
                p.ImpostosTotal,
                p.ValorTotal,
                p.CondicaoPagamento,
                p.PrazoEntregaDias,
                p.PrevisaoEntrega,
                p.AprovadoPorNome,
                p.AprovadoEm,
                p.DataCriacao,
                QtdItens = p.Itens.Count
            })
            .ToListAsync();

        return Ok(lista);
    }

    [HttpPost]
    [Authorize(Roles = "TenantAdmin,Buyer")]
    public async Task<IActionResult> CriarDireto([FromBody] CriarPedidoDiretoRequest r)
    {
        if (r.Itens == null || r.Itens.Count == 0) return BadRequest(new { erro = "Selecione ao menos um produto." });
        var forn = await _db.Fornecedores.AsNoTracking().FirstOrDefaultAsync(f => f.Id == r.FornecedorId && f.Ativo);
        if (forn == null) return BadRequest(new { erro = "Fornecedor não encontrado ou inativo." });

        var pids = r.Itens.Select(i => i.ProdutoId).Distinct().ToList();
        var prods = await _db.Produtos.AsNoTracking().Where(p => pids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        var ano = DateTime.UtcNow.Year;
        var qtdExistente = await _db.PedidosCompra.CountAsync(p => p.Numero.StartsWith($"PED-{ano}-"));
        qtdExistente++;
        var numeroPedido = $"PED-{ano}-{qtdExistente:D4}";

        var itensPedido = new List<PedidoCompraItem>();
        decimal subtotal = 0;
        decimal descontoTotal = 0;

        foreach (var item in r.Itens)
        {
            if (!prods.TryGetValue(item.ProdutoId, out var prod)) continue;
            var descPerc = item.DescontoPerc ?? 0m;
            var precoLiq = Math.Round(item.PrecoUnitario * (1 - descPerc / 100m), 4);
            var itemSubtotal = Math.Round(precoLiq * item.Quantidade, 2);

            subtotal += Math.Round(item.PrecoUnitario * item.Quantidade, 2);
            descontoTotal += Math.Round((item.PrecoUnitario - precoLiq) * item.Quantidade, 2);

            itensPedido.Add(new PedidoCompraItem
            {
                TenantId = _tenant.CurrentTenantId!.Value,
                ProdutoId = prod.Id,
                CodigoProduto = prod.CodigoErp,
                DescricaoProduto = prod.Descricao,
                Unidade = prod.Unidade,
                Quantidade = item.Quantidade,
                PrecoUnitario = item.PrecoUnitario,
                DescontoPerc = descPerc,
                PrecoLiquidoUnit = precoLiq,
                Subtotal = itemSubtotal,
                Cst = "000",
                Cfop = "5102",
                CustoEfetivoUnit = precoLiq,
                CustoEfetivoTotal = itemSubtotal
            });
        }

        var frete = r.Frete ?? 0m;
        var outrasDesp = r.OutrasDespesas ?? 0m;
        var valorTotal = (subtotal - descontoTotal) + frete + outrasDesp;
        var nomeUsuario = _tenant.UserName ?? "Comprador";
        var timeline = new List<TimelineEventoDto>
        {
            new(DateTime.UtcNow, "Criado", $"Pedido de compra criado manualmente", nomeUsuario)
        };

        var pedido = new PedidoCompra
        {
            TenantId = _tenant.CurrentTenantId!.Value,
            Numero = numeroPedido,
            FornecedorId = forn.Id,
            Status = "Criado",
            Subtotal = subtotal,
            Desconto = descontoTotal,
            Frete = frete,
            OutrasDespesas = outrasDesp,
            ImpostosTotal = 0m,
            ValorTotal = valorTotal,
            CondicaoPagamento = string.IsNullOrWhiteSpace(r.CondicaoPagamento) ? (forn.CondicoesPagamento ?? "A combinar") : r.CondicaoPagamento,
            PrazoEntregaDias = r.PrazoEntregaDias ?? (forn.PrazoEntregaPadrao > 0 ? forn.PrazoEntregaPadrao : 3),
            PrevisaoEntrega = DateTime.UtcNow.AddDays(r.PrazoEntregaDias ?? (forn.PrazoEntregaPadrao > 0 ? forn.PrazoEntregaPadrao : 3)),
            Observacoes = r.Observacoes ?? "",
            AprovadoPorId = _tenant.CurrentUserId,
            AprovadoPorNome = nomeUsuario,
            AprovadoEm = DateTime.UtcNow,
            TimelineJson = JsonSerializer.Serialize(timeline),
            Itens = itensPedido
        };

        _db.PedidosCompra.Add(pedido);
        await _db.SaveChangesAsync();
        _audit.Add("PedidoCriadoDireto", "PedidoCompra", pedido.Id.ToString(), new { pedido.Numero, pedido.ValorTotal });

        return Ok(new { mensagem = "Pedido criado com sucesso.", id = pedido.Id, numero = pedido.Numero, valorTotal = pedido.ValorTotal });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id)
    {
        var p = await _db.PedidosCompra
            .Include(x => x.Fornecedor)
            .Include(x => x.Cotacao)
            .Include(x => x.Itens)
                .ThenInclude(i => i.Produto)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (p == null) return NotFound(new { erro = "Pedido de compra não encontrado." });

        List<TimelineEventoDto> timeline;
        try
        {
            timeline = JsonSerializer.Deserialize<List<TimelineEventoDto>>(p.TimelineJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch
        {
            timeline = new();
        }

        return Ok(new
        {
            p.Id,
            p.Numero,
            p.Status,
            p.CotacaoId,
            CotacaoNumero = p.Cotacao?.Numero,
            p.FornecedorId,
            Fornecedor = p.Fornecedor != null ? new
            {
                p.Fornecedor.Id,
                p.Fornecedor.RazaoSocial,
                p.Fornecedor.NomeFantasia,
                p.Fornecedor.Cnpj,
                p.Fornecedor.Email,
                p.Fornecedor.Telefone,
                p.Fornecedor.Contato,
                p.Fornecedor.Endereco,
                p.Fornecedor.Cidade,
                p.Fornecedor.Uf
            } : null,
            p.Subtotal,
            p.Desconto,
            p.Frete,
            p.OutrasDespesas,
            p.ImpostosTotal,
            p.ValorTotal,
            p.CondicaoPagamento,
            p.PrazoEntregaDias,
            p.PrevisaoEntrega,
            p.Observacoes,
            p.AprovadoPorNome,
            p.AprovadoEm,
            p.DataCriacao,
            Timeline = timeline,
            Itens = p.Itens.Select(i => new
            {
                i.Id,
                i.ProdutoId,
                i.CodigoProduto,
                i.DescricaoProduto,
                i.Unidade,
                i.Ncm,
                i.Quantidade,
                i.PrecoUnitario,
                i.DescontoPerc,
                i.PrecoLiquidoUnit,
                i.Subtotal,
                i.Cst,
                i.Cfop,
                i.IcmsAliq,
                i.IpiAliq,
                i.Mva,
                i.IcmsStAliq,
                i.IcmsStValor,
                i.CustoEfetivoUnit,
                i.CustoEfetivoTotal
            })
        });
    }

    [HttpPost("gerar-da-cotacao/{cotacaoId:guid}")]
    [Authorize(Roles = "TenantAdmin,Buyer")]
    public async Task<IActionResult> GerarDaCotacao(Guid cotacaoId)
    {
        var cot = await _db.Cotacoes
            .Include(c => c.Itens)
            .Include(c => c.Fornecedores)
                .ThenInclude(f => f.Fornecedor)
            .Include(c => c.Fornecedores)
                .ThenInclude(f => f.Resposta!)
                    .ThenInclude(r => r.Itens)
            .FirstOrDefaultAsync(c => c.Id == cotacaoId);

        if (cot == null) return NotFound(new { erro = "Cotação não encontrada." });
        if (cot.Status != "Aprovada") return BadRequest(new { erro = "A cotação precisa estar Aprovada para gerar pedidos." });

        // Identificar fornecedores vencedores
        var escolhas = cot.Itens
            .Where(i => i.CotacaoFornecedorEscolhidoId.HasValue)
            .GroupBy(i => i.CotacaoFornecedorEscolhidoId!.Value);

        if (!escolhas.Any())
            return BadRequest(new { erro = "Não foram encontrados itens com fornecedores vencedores nesta cotação." });

        var pedidosCriados = new List<PedidoCompra>();
        var ano = DateTime.UtcNow.Year;
        var qtdExistente = await _db.PedidosCompra.CountAsync(p => p.Numero.StartsWith($"PED-{ano}-"));

        var nomeUsuario = _tenant.UserName ?? "Comprador";

        foreach (var grupo in escolhas)
        {
            var cfId = grupo.Key;
            var cf = cot.Fornecedores.FirstOrDefault(f => f.Id == cfId);
            if (cf == null) continue;

            // Verificar se já existe pedido para esta cotação e este fornecedor
            var jaExiste = await _db.PedidosCompra
                .AnyAsync(p => p.CotacaoId == cot.Id && p.FornecedorId == cf.FornecedorId && p.Status != "Cancelado");

            if (jaExiste) continue;

            qtdExistente++;
            var numeroPedido = $"PED-{ano}-{qtdExistente:D4}";

            var resp = cf.Resposta;
            var itensPedido = new List<PedidoCompraItem>();
            decimal subtotal = 0;
            decimal descontoTotal = 0;
            decimal impostosTotal = 0;

            foreach (var cotItem in grupo)
            {
                var respItem = resp?.Itens.FirstOrDefault(ri => ri.CotacaoItemId == cotItem.Id);
                var precoBruto = respItem?.PrecoUnitario ?? cotItem.UltimoCustoRef;
                var descPerc = respItem?.DescontoPerc ?? 0m;
                var precoLiq = Math.Round(precoBruto * (1 - descPerc / 100m), 4);
                var qtd = cotItem.Quantidade;
                var itemSubtotal = Math.Round(precoLiq * qtd, 2);

                subtotal += Math.Round(precoBruto * qtd, 2);
                descontoTotal += Math.Round((precoBruto - precoLiq) * qtd, 2);

                var stValor = (respItem?.IcmsStValor ?? 0m) * qtd;
                impostosTotal += stValor;

                itensPedido.Add(new PedidoCompraItem
                {
                    TenantId = _tenant.CurrentTenantId!.Value,
                    ProdutoId = cotItem.ProdutoId,
                    CodigoProduto = cotItem.Codigo,
                    DescricaoProduto = cotItem.Descricao,
                    Unidade = cotItem.Unidade,
                    Quantidade = qtd,
                    PrecoUnitario = precoBruto,
                    DescontoPerc = descPerc,
                    PrecoLiquidoUnit = precoLiq,
                    Subtotal = itemSubtotal,
                    Cst = respItem?.Cst ?? "",
                    Cfop = respItem?.Cfop ?? "",
                    IcmsAliq = respItem?.IcmsAliq ?? 0m,
                    IpiAliq = respItem?.IpiAliq ?? 0m,
                    Mva = respItem?.Mva ?? 0m,
                    IcmsStAliq = respItem?.IcmsStAliq ?? 0m,
                    IcmsStValor = respItem?.IcmsStValor ?? 0m,
                    CustoEfetivoUnit = precoLiq + (respItem?.IcmsStValor ?? 0m),
                    CustoEfetivoTotal = itemSubtotal + stValor
                });
            }

            var frete = resp?.Frete ?? 0m;
            var outrasDesp = resp?.OutrasDespesas ?? 0m;
            var valorTotal = (subtotal - descontoTotal) + frete + outrasDesp + impostosTotal;

            var timeline = new List<TimelineEventoDto>
            {
                new(DateTime.UtcNow, "Criado", $"Pedido de compra gerado a partir da cotação {cot.Numero}", nomeUsuario)
            };

            var pedido = new PedidoCompra
            {
                TenantId = _tenant.CurrentTenantId!.Value,
                Numero = numeroPedido,
                CotacaoId = cot.Id,
                FornecedorId = cf.FornecedorId,
                Status = "Criado",
                Subtotal = subtotal,
                Desconto = descontoTotal,
                Frete = frete,
                OutrasDespesas = outrasDesp,
                ImpostosTotal = impostosTotal,
                ValorTotal = valorTotal,
                CondicaoPagamento = resp?.CondicaoPagamento ?? cf.Fornecedor?.CondicoesPagamento ?? "A combinar",
                PrazoEntregaDias = resp?.PrazoEntregaDias ?? cf.Fornecedor?.PrazoEntregaPadrao ?? 3,
                PrevisaoEntrega = DateTime.UtcNow.AddDays(resp?.PrazoEntregaDias ?? 3),
                Observacoes = resp?.Observacoes ?? "",
                AprovadoPorId = _tenant.CurrentUserId,
                AprovadoPorNome = nomeUsuario,
                AprovadoEm = DateTime.UtcNow,
                TimelineJson = JsonSerializer.Serialize(timeline),
                Itens = itensPedido
            };

            _db.PedidosCompra.Add(pedido);
            pedidosCriados.Add(pedido);
        }

        if (pedidosCriados.Count > 0)
        {
            await _db.SaveChangesAsync();
            _audit.Add("PedidosGerados", "PedidoCompra", cot.Id.ToString(), new { cot.Numero, Quantidade = pedidosCriados.Count });
        }

        return Ok(new
        {
            mensagem = $"{pedidosCriados.Count} pedido(s) de compra gerado(s) com sucesso.",
            pedidos = pedidosCriados.Select(p => new { p.Id, p.Numero, p.ValorTotal })
        });
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = "TenantAdmin,Buyer")]
    public async Task<IActionResult> AtualizarStatus(Guid id, [FromBody] AtualizarStatusRequest r)
    {
        var p = await _db.PedidosCompra
            .Include(x => x.Itens)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (p == null) return NotFound(new { erro = "Pedido de compra não encontrado." });

        var statusValidos = new[]
        {
            "Criado", "Enviado", "AguardandoConfirmacao", "Confirmado",
            "EmPreparacao", "EmTransporte", "Recebido", "Cancelado"
        };

        if (!statusValidos.Any(s => s.Equals(r.NovoStatus, StringComparison.OrdinalIgnoreCase)))
            return BadRequest(new { erro = $"Status inválido. Permitidos: {string.Join(", ", statusValidos)}" });

        var statusNormalizado = statusValidos.First(s => s.Equals(r.NovoStatus, StringComparison.OrdinalIgnoreCase));
        if (p.Status == statusNormalizado)
            return Ok(new { mensagem = "Status já atualizado.", p.Status });

        p.Status = statusNormalizado;

        List<TimelineEventoDto> timeline;
        try
        {
            timeline = JsonSerializer.Deserialize<List<TimelineEventoDto>>(p.TimelineJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch
        {
            timeline = new();
        }

        var nomeUsuario = _tenant.UserName ?? "Comprador";
        var descricao = string.IsNullOrWhiteSpace(r.Descricao)
            ? $"Status alterado para '{statusNormalizado}'"
            : r.Descricao.Trim();

        timeline.Add(new TimelineEventoDto(DateTime.UtcNow, statusNormalizado, descricao, nomeUsuario));
        p.TimelineJson = JsonSerializer.Serialize(timeline);

        // Se o pedido foi recebido, registrar no histórico de preços se ainda não registrado
        if (statusNormalizado == "Recebido")
        {
            foreach (var item in p.Itens)
            {
                var jaGravado = await _db.HistoricoPrecos
                    .AnyAsync(h => h.ProdutoId == item.ProdutoId && h.FornecedorId == p.FornecedorId && h.Data == DateTime.UtcNow.Date && h.Valor == item.PrecoLiquidoUnit);

                if (!jaGravado)
                {
                    _db.HistoricoPrecos.Add(new HistoricoPreco
                    {
                        TenantId = _tenant.CurrentTenantId!.Value,
                        ProdutoId = item.ProdutoId,
                        FornecedorId = p.FornecedorId,
                        Data = DateTime.UtcNow.Date,
                        Valor = item.PrecoLiquidoUnit,
                        Quantidade = item.Quantidade,
                        Origem = "pedido"
                    });
                }
            }
        }

        _audit.Add("PedidoStatusAlterado", "PedidoCompra", p.Id.ToString(), new { p.Numero, NovoStatus = statusNormalizado, Descricao = descricao });
        await _db.SaveChangesAsync();

        return Ok(new { mensagem = "Status atualizado com sucesso.", status = p.Status, timeline });
    }

    [HttpGet("{id:guid}/imprimir")]
    [HttpGet("{id:guid}/pdf")]
    [AllowAnonymous]
    public async Task<IActionResult> VisualizarImpressao(Guid id)
    {
        var p = await _db.PedidosCompra
            .IgnoreQueryFilters()
            .Include(x => x.Fornecedor)
            .Include(x => x.Cotacao)
            .Include(x => x.Itens)
                .ThenInclude(i => i.Produto)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (p == null) return NotFound(new { erro = "Pedido não encontrado." });

        var tenantInfo = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == p.TenantId);
        Usuario? comprador = null;
        if (p.AprovadoPorId.HasValue)
        {
            comprador = await _db.Usuarios.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == p.AprovadoPorId.Value);
        }

        var html = Nexo.Api.Services.PdfDocumentService.GerarHtmlPedido(p, tenantInfo, comprador);
        return Content(html, "text/html; charset=utf-8");
    }
}

