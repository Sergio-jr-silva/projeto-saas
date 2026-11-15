using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.DTOs;
using Nexo.Api.Models;
using Nexo.Api.Services;

namespace Nexo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "TenantAdmin,Buyer,Viewer,PlatformAdmin")]
public class ProdutosController(NexoDbContext db, ITenantConfigService cfgService, IAuditService audit) : ControllerBase
{
    private static ProdutoDto ToDto(Produto p, ConfiguracaoTenant cfg)
    {
        var a = EstoqueService.Avaliar(p, cfg, p.FornecedorVinculado?.PrazoEntregaPadrao);
        var custo = p.UltimoCusto > 0 ? p.UltimoCusto : p.CustoAtual;
        decimal? margem = p.PrecoVenda > 0 && custo > 0 ? Math.Round((p.PrecoVenda - custo) / p.PrecoVenda * 100, 2) : null;
        return new ProdutoDto(p.Id, p.CodigoErp, p.Descricao, p.CodigoBarras, p.Unidade, p.Grupo, p.Categoria, p.PrecoVenda, p.CustoAtual, p.UltimoCusto,
            p.UltimaCompraEm, p.EstoqueAtual, p.EstoquePendente, p.EstoqueMinimo, p.Consumo90Dias, p.FornecedorVinculadoId,
            p.FornecedorVinculado == null ? null : (string.IsNullOrWhiteSpace(p.FornecedorVinculado.NomeFantasia) ? p.FornecedorVinculado.RazaoSocial : p.FornecedorVinculado.NomeFantasia),
            p.Origem, p.UltimaSincronizacaoEm, p.DataAtualizacao ?? p.DataCriacao, a.Nivel, a.CoberturaDias, a.Sugerida, margem, p.EstoqueCritico);
    }

    private static readonly HashSet<string> NiveisValidos = new(StringComparer.Ordinal)
        { NivelEstoque.Zerado, NivelEstoque.Critico, NivelEstoque.Baixo, NivelEstoque.Normal, NivelEstoque.Indeterminado, "Alerta" };
    private static readonly HashSet<string> OrdensValidas = new(StringComparer.Ordinal) { "estoque", "consumo", "alerta", "descricao" };

    /// <summary>
    /// nivel: Zerado | Critico | Baixo | Normal | Indeterminado | Alerta (= Zerado + Critico + Baixo).
    /// estoqueBaixo=true equivale a nivel=Alerta. O filtro é aplicado ANTES da paginação, sobre o conjunto completo do tenant.
    /// Produtos com estoque desconhecido (NULL) ficam como "Indeterminado" e nunca entram no alerta como se fossem zerados.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] string? grupo, [FromQuery] string? nivel, [FromQuery] string? origem,
        [FromQuery] Guid? fornecedorId, [FromQuery] string? ordem, [FromQuery] bool? estoqueBaixo, [FromQuery] int page = 1, [FromQuery] int size = 50)
    {
        size = Math.Clamp(size, 1, 500); page = Math.Clamp(page, 1, 100_000);
        if (!string.IsNullOrWhiteSpace(nivel) && !NiveisValidos.Contains(nivel)) return BadRequest(new { erro = "Nível de estoque inválido." });
        if (!string.IsNullOrWhiteSpace(ordem) && !OrdensValidas.Contains(ordem)) return BadRequest(new { erro = "Ordenação inválida." });
        if (q is { Length: > 100 }) return BadRequest(new { erro = "Termo de busca muito longo." });

        var cfg = await cfgService.GetAsync();
        var query = db.Produtos.AsNoTracking().Include(p => p.FornecedorVinculado).Where(p => p.Ativo);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim().ToLower();
            query = query.Where(p => p.Descricao.ToLower().Contains(s) || p.CodigoErp.ToLower().Contains(s) || p.CodigoBarras.ToLower().Contains(s));
        }
        if (!string.IsNullOrWhiteSpace(grupo)) query = query.Where(p => p.Grupo == grupo);
        if (!string.IsNullOrWhiteSpace(origem)) query = query.Where(p => p.Origem == origem);
        if (fornecedorId.HasValue) query = query.Where(p => p.FornecedorVinculadoId == fornecedorId);

        var todos = (await query.ToListAsync()).Select(p => ToDto(p, cfg)).ToList();
        var resumo = new
        {
            total = todos.Count,
            zerado = todos.Count(p => p.NivelEstoque == NivelEstoque.Zerado),
            critico = todos.Count(p => p.NivelEstoque == NivelEstoque.Critico),
            baixo = todos.Count(p => p.NivelEstoque == NivelEstoque.Baixo),
            normal = todos.Count(p => p.NivelEstoque == NivelEstoque.Normal),
            indeterminado = todos.Count(p => p.NivelEstoque == NivelEstoque.Indeterminado),
        };

        var filtroNivel = estoqueBaixo == true ? "Alerta" : nivel;
        if (!string.IsNullOrWhiteSpace(filtroNivel))
            todos = filtroNivel == "Alerta"
                ? todos.Where(p => NivelEstoque.EhAlerta(p.NivelEstoque)).ToList()
                : todos.Where(p => p.NivelEstoque == filtroNivel).ToList();

        var peso = new Dictionary<string, int>
            { [NivelEstoque.Zerado] = 0, [NivelEstoque.Critico] = 1, [NivelEstoque.Baixo] = 2, [NivelEstoque.Indeterminado] = 3, [NivelEstoque.Normal] = 4 };
        todos = ordem switch
        {
            "estoque" => todos.OrderBy(p => p.EstoqueAtual is null).ThenBy(p => p.EstoqueAtual).ThenBy(p => p.Descricao).ToList(),
            "consumo" => todos.OrderByDescending(p => p.Consumo90Dias).ThenBy(p => p.Descricao).ToList(),
            "alerta" => todos.OrderBy(p => peso[p.NivelEstoque]).ThenByDescending(p => p.Consumo90Dias).ThenBy(p => p.Descricao).ToList(),
            _ => todos.OrderBy(p => p.Descricao).ToList(),
        };
        return Ok(new { total = todos.Count, page, size, resumo, itens = todos.Skip((page - 1) * size).Take(size).ToList() });
    }
    [HttpGet("grupos")]
    public async Task<IActionResult> Grupos() =>
        Ok(await db.Produtos.Where(p => p.Ativo && p.Grupo != "").Select(p => p.Grupo).Distinct().OrderBy(g => g).ToListAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var p = await db.Produtos.Include(x => x.FornecedorVinculado).FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound(new { erro = "Produto não encontrado." });
        var cfg = await cfgService.GetAsync();
        var hist = await db.HistoricoPrecos.AsNoTracking().Include(h => h.Fornecedor).Where(h => h.ProdutoId == id)
            .OrderByDescending(h => h.Data).Take(60)
            .Select(h => new { h.Data, h.Valor, h.Quantidade, h.Origem, Fornecedor = h.Fornecedor == null ? null : (h.Fornecedor.NomeFantasia != "" ? h.Fornecedor.NomeFantasia : h.Fornecedor.RazaoSocial) })
            .ToListAsync();
        var fornecedores = await db.HistoricoPrecos.AsNoTracking().Where(h => h.ProdutoId == id && h.FornecedorId != null)
            .GroupBy(h => h.FornecedorId).Select(g => new { FornecedorId = g.Key, Compras = g.Count(), UltimoPreco = g.OrderByDescending(x => x.Data).First().Valor, Ultima = g.Max(x => x.Data) }).ToListAsync();
        return Ok(new { produto = ToDto(p, cfg), historicoPrecos = hist, fornecedoresHistorico = fornecedores });
    }

    [HttpPut("{id:guid}/estoque-minimo")]
    [Authorize(Roles = "TenantAdmin,Buyer")]
    public async Task<IActionResult> SetMinimo(Guid id, [FromBody] EstoqueMinimoRequest r)
    {
        if (r.EstoqueMinimo < 0 || r.EstoqueMinimo > 99_999_999) return BadRequest(new { erro = "Estoque mínimo inválido." });
        var p = await db.Produtos.FindAsync(id);
        if (p == null) return NotFound(new { erro = "Produto não encontrado." });
        var antes = p.EstoqueMinimo;
        p.EstoqueMinimo = r.EstoqueMinimo;
        audit.Add("EstoqueMinimoAlterado", "Produto", p.Id.ToString(), new { de = antes, para = r.EstoqueMinimo });
        await db.SaveChangesAsync();
        return Ok(new { p.Id, p.EstoqueMinimo });
    }

    [HttpPut("{id:guid}/estoque-critico")]
    [Authorize(Roles = "TenantAdmin,Buyer")]
    public async Task<IActionResult> SetCritico(Guid id, [FromBody] EstoqueCriticoRequest r)
    {
        if (r.EstoqueCritico is < 0 or > 99_999_999) return BadRequest(new { erro = "Estoque crítico inválido." });
        var p = await db.Produtos.FindAsync(id);
        if (p == null) return NotFound(new { erro = "Produto não encontrado." });
        if (r.EstoqueCritico > p.EstoqueMinimo && p.EstoqueMinimo > 0) return BadRequest(new { erro = "O estoque crítico não pode ser maior que o estoque mínimo." });
        var antes = p.EstoqueCritico;
        p.EstoqueCritico = r.EstoqueCritico;
        audit.Add("EstoqueCriticoAlterado", "Produto", p.Id.ToString(), new { de = antes, para = r.EstoqueCritico });
        await db.SaveChangesAsync();
        return Ok(new { p.Id, p.EstoqueCritico });
    }

    [HttpPost]
    [Authorize(Roles = "TenantAdmin,Buyer")]
    public async Task<IActionResult> Create([FromBody] CreateProdutoRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Descricao)) return BadRequest(new { erro = "Descrição do produto é obrigatória." });
        if (r.PrecoVenda < 0 || r.CustoAtual < 0 || r.EstoqueMinimo < 0 || r.EstoqueCritico < 0) return BadRequest(new { erro = "Valores não podem ser negativos." });
        if (r.PrecoVenda > 999_999_999 || r.CustoAtual > 999_999_999 || r.EstoqueMinimo > 99_999_999 || r.EstoqueAtual is > 99_999_999 or < -99_999_999) return BadRequest(new { erro = "Valor fora do intervalo permitido." });
        if (r.Descricao.Length > 255) return BadRequest(new { erro = "Descrição excede 255 caracteres." });
        var codigo = r.CodigoErp?.Trim() ?? "";
        if (codigo != "" && await db.Produtos.AnyAsync(p => p.CodigoErp == codigo)) return BadRequest(new { erro = "Já existe um produto com este código." });
        if (r.FornecedorVinculadoId.HasValue && !await db.Fornecedores.AnyAsync(f => f.Id == r.FornecedorVinculadoId)) return BadRequest(new { erro = "Fornecedor vinculado inválido." });
        var p = new Produto
        {
            CodigoErp = codigo, Descricao = r.Descricao.Trim(), CodigoBarras = r.CodigoBarras?.Trim() ?? "", Unidade = string.IsNullOrWhiteSpace(r.Unidade) ? "UN" : r.Unidade.Trim().ToUpperInvariant(),
            Grupo = r.Grupo?.Trim() ?? "", Categoria = r.Categoria?.Trim() ?? "", PrecoVenda = r.PrecoVenda, CustoAtual = r.CustoAtual, UltimoCusto = r.CustoAtual,
            EstoqueAtual = r.EstoqueAtual, EstoqueCritico = r.EstoqueCritico, EstoqueMinimo = r.EstoqueMinimo, FornecedorVinculadoId = r.FornecedorVinculadoId, Origem = "manual",
        };
        db.Produtos.Add(p);
        audit.Add("ProdutoCriado", "Produto", p.Id.ToString(), new { p.Descricao });
        await db.SaveChangesAsync();
        var cfg = await cfgService.GetAsync();
        return CreatedAtAction(nameof(Get), new { id = p.Id }, ToDto(p, cfg));
    }
}
