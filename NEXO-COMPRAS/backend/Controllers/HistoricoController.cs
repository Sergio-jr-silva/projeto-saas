using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.MultiTenancy;

namespace Nexo.Api.Controllers;

[ApiController]
[Route("api/historico")]
[Authorize]
public class HistoricoController : ControllerBase
{
    private readonly NexoDbContext _db;
    private readonly ITenantContext _tenant;

    public HistoricoController(NexoDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet("compras")]
    public async Task<IActionResult> ListarCompras(
        [FromQuery] Guid? produtoId,
        [FromQuery] Guid? fornecedorId,
        [FromQuery] DateTime? dataInicio,
        [FromQuery] DateTime? dataFim,
        [FromQuery] string? busca)
    {
        var q = _db.HistoricoPrecos
            .Include(h => h.Produto)
            .Include(h => h.Fornecedor)
            .AsNoTracking()
            .AsQueryable();

        if (produtoId.HasValue)
            q = q.Where(h => h.ProdutoId == produtoId.Value);

        if (fornecedorId.HasValue)
            q = q.Where(h => h.FornecedorId == fornecedorId.Value);

        if (dataInicio.HasValue)
            q = q.Where(h => h.Data >= dataInicio.Value.Date);

        if (dataFim.HasValue)
            q = q.Where(h => h.Data <= dataFim.Value.Date);

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var b = busca.Trim().ToLower();
            q = q.Where(h => h.Produto!.Descricao.ToLower().Contains(b) ||
                             h.Produto!.CodigoErp.ToLower().Contains(b) ||
                             (h.Fornecedor != null && (h.Fornecedor.RazaoSocial.ToLower().Contains(b) || h.Fornecedor.NomeFantasia.ToLower().Contains(b))));
        }

        var lista = await q.OrderByDescending(h => h.Data)
            .ThenByDescending(h => h.DataCriacao)
            .Take(200)
            .Select(h => new
            {
                h.Id,
                h.Data,
                h.ProdutoId,
                ProdutoCodigo = h.Produto != null ? h.Produto.CodigoErp : "",
                ProdutoDescricao = h.Produto != null ? h.Produto.Descricao : "",
                ProdutoUnidade = h.Produto != null ? h.Produto.Unidade : "UN",
                UltimoCustoReferencia = h.Produto != null ? h.Produto.UltimoCusto : 0m,
                PrecoVenda = h.Produto != null ? h.Produto.PrecoVenda : 0m,
                h.FornecedorId,
                FornecedorNome = h.Fornecedor != null ? (string.IsNullOrEmpty(h.Fornecedor.NomeFantasia) ? h.Fornecedor.RazaoSocial : h.Fornecedor.NomeFantasia) : "Não especificado",
                h.Valor,
                h.Quantidade,
                ValorTotal = h.Valor * h.Quantidade,
                h.Origem
            })
            .ToListAsync();

        return Ok(lista);
    }

    [HttpGet("evolucao/{produtoId:guid}")]
    public async Task<IActionResult> EvolucaoPreco(Guid produtoId)
    {
        var produto = await _db.Produtos.FindAsync(produtoId);
        if (produto == null) return NotFound(new { erro = "Produto não encontrado." });

        var pontos = await _db.HistoricoPrecos
            .Include(h => h.Fornecedor)
            .Where(h => h.ProdutoId == produtoId)
            .OrderBy(h => h.Data)
            .Select(h => new
            {
                Data = h.Data.ToString("yyyy-MM-dd"),
                Valor = h.Valor,
                Quantidade = h.Quantidade,
                Fornecedor = h.Fornecedor != null ? (string.IsNullOrEmpty(h.Fornecedor.NomeFantasia) ? h.Fornecedor.RazaoSocial : h.Fornecedor.NomeFantasia) : "ERP",
                Origem = h.Origem
            })
            .ToListAsync();

        return Ok(new
        {
            produto = new { produto.Id, produto.CodigoErp, produto.Descricao, produto.UltimoCusto, produto.PrecoVenda },
            historico = pontos
        });
    }

    [HttpGet("indicadores")]
    public async Task<IActionResult> IndicadoresCompras()
    {
        var pedidos = await _db.PedidosCompra
            .Include(p => p.Fornecedor)
            .Where(p => p.Status != "Cancelado")
            .ToListAsync();

        var totalComprado = pedidos.Sum(p => p.ValorTotal);
        var totalPedidos = pedidos.Count;
        var pedidosRecebidos = pedidos.Count(p => p.Status == "Recebido");

        var cotacoesAprovadas = await _db.Cotacoes
            .Where(c => c.Status == "Aprovada")
            .ToListAsync();

        var economiaRealizada = cotacoesAprovadas.Sum(c => c.EconomiaRealizada);
        var economiaPotencial = cotacoesAprovadas.Sum(c => c.EconomiaPotencial);

        var comprasPorFornecedor = pedidos
            .GroupBy(p => p.Fornecedor != null ? (string.IsNullOrEmpty(p.Fornecedor.NomeFantasia) ? p.Fornecedor.RazaoSocial : p.Fornecedor.NomeFantasia) : "Desconhecido")
            .Select(g => new
            {
                Fornecedor = g.Key,
                Total = g.Sum(x => x.ValorTotal),
                QtdPedidos = g.Count()
            })
            .OrderByDescending(x => x.Total)
            .ToList();

        var maisComprado = comprasPorFornecedor.FirstOrDefault();
        var menosComprado = comprasPorFornecedor.LastOrDefault();

        return Ok(new
        {
            totalComprado,
            totalPedidos,
            pedidosRecebidos,
            economiaRealizada,
            economiaPotencial,
            fornecedorMaisComprado = maisComprado,
            fornecedorMenosComprado = menosComprado,
            rankingFornecedores = comprasPorFornecedor
        });
    }

    /// <summary>
    /// Comparação de pedidos realizados por produto: histórico de preços, variação percentual, prazos e fornecedor mais vantajoso.
    /// </summary>
    [HttpGet("comparativo-pedidos")]
    public async Task<IActionResult> ComparativoPedidos([FromQuery] Guid? produtoId, [FromQuery] string? busca)
    {
        var qPedidos = _db.PedidosCompra
            .Include(p => p.Fornecedor)
            .Include(p => p.Itens)
            .Where(p => p.Status != "Cancelado")
            .AsNoTracking()
            .AsQueryable();

        var pedidos = await qPedidos.OrderByDescending(p => p.DataCriacao).ToListAsync();

        var todosItens = pedidos
            .SelectMany(p => p.Itens.Select(i => new
            {
                Item = i,
                Pedido = p
            }))
            .ToList();

        if (produtoId.HasValue)
            todosItens = todosItens.Where(x => x.Item.ProdutoId == produtoId.Value).ToList();

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var b = busca.Trim().ToLower();
            todosItens = todosItens.Where(x => x.Item.DescricaoProduto.ToLower().Contains(b) || x.Item.CodigoProduto.ToLower().Contains(b)).ToList();
        }

        var grupos = todosItens
            .GroupBy(x => new { x.Item.ProdutoId, x.Item.CodigoProduto, x.Item.DescricaoProduto, x.Item.Unidade })
            .Select(g =>
            {
                var historicoCompras = g.OrderBy(x => x.Pedido.DataCriacao).Select(x => new
                {
                    PedidoId = x.Pedido.Id,
                    PedidoNumero = x.Pedido.Numero,
                    DataCriacao = x.Pedido.DataCriacao.ToString("yyyy-MM-dd HH:mm"),
                    FornecedorId = x.Pedido.FornecedorId,
                    FornecedorNome = x.Pedido.Fornecedor != null ? (string.IsNullOrEmpty(x.Pedido.Fornecedor.NomeFantasia) ? x.Pedido.Fornecedor.RazaoSocial : x.Pedido.Fornecedor.NomeFantasia) : "-",
                    Quantidade = x.Item.Quantidade,
                    PrecoUnitario = x.Item.PrecoUnitario,
                    DescontoPerc = x.Item.DescontoPerc,
                    PrecoLiquidoUnit = x.Item.PrecoLiquidoUnit,
                    Subtotal = x.Item.Subtotal,
                    PrazoEntregaDias = x.Pedido.PrazoEntregaDias,
                    StatusPedido = x.Pedido.Status
                }).ToList();

                var precos = historicoCompras.Select(h => h.PrecoLiquidoUnit).ToList();
                var menorPreco = precos.Count > 0 ? precos.Min() : 0m;
                var maiorPreco = precos.Count > 0 ? precos.Max() : 0m;
                var precoMedio = precos.Count > 0 ? Math.Round(precos.Average(), 4) : 0m;
                var ultimoPreco = precos.Count > 0 ? precos.Last() : 0m;
                var primeiroPreco = precos.Count > 0 ? precos.First() : 0m;

                decimal variacaoGeralPerc = primeiroPreco > 0
                    ? Math.Round(((ultimoPreco - primeiroPreco) / primeiroPreco) * 100, 2)
                    : 0m;

                var melhorCompra = historicoCompras.OrderBy(h => h.PrecoLiquidoUnit).FirstOrDefault();

                return new
                {
                    ProdutoId = g.Key.ProdutoId,
                    Codigo = g.Key.CodigoProduto,
                    Descricao = g.Key.DescricaoProduto,
                    Unidade = g.Key.Unidade,
                    TotalCompras = historicoCompras.Count,
                    TotalQuantidade = g.Sum(x => x.Item.Quantidade),
                    TotalGasto = g.Sum(x => x.Item.Subtotal),
                    MenorPreco = menorPreco,
                    MaiorPreco = maiorPreco,
                    PrecoMedio = precoMedio,
                    UltimoPreco = ultimoPreco,
                    VariacaoGeralPerc = variacaoGeralPerc,
                    FornecedorMaisVantajoso = melhorCompra?.FornecedorNome ?? "-",
                    MenorPrecoRegistrado = melhorCompra?.PrecoLiquidoUnit ?? 0m,
                    Historico = historicoCompras
                };
            })
            .OrderByDescending(x => x.TotalGasto)
            .ToList();

        return Ok(new
        {
            totalProdutos = grupos.Count,
            comparativo = grupos
        });
    }
}

