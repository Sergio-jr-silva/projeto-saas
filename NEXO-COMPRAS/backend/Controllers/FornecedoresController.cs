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
public class FornecedoresController(NexoDbContext db, IAuditService audit) : ControllerBase
{
    private static FornecedorDto ToDto(Fornecedor f) => new(f.Id, f.CodigoErp, f.RazaoSocial, f.NomeFantasia, f.Cnpj, f.InscricaoEstadual, f.Email, f.Telefone,
        f.Whatsapp, f.Contato, f.Endereco, f.Cidade, f.Uf, f.PrazoEntregaPadrao, f.CondicoesPagamento, f.Observacoes, f.Origem, f.Ativo);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] bool incluirInativos = false)
    {
        var query = db.Fornecedores.AsNoTracking().AsQueryable();
        if (!incluirInativos) query = query.Where(f => f.Ativo);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim().ToLower();
            query = query.Where(f => f.RazaoSocial.ToLower().Contains(s) || f.NomeFantasia.ToLower().Contains(s) || f.Cnpj.Contains(s));
        }
        var lista = await query.OrderBy(f => f.NomeFantasia == "" ? f.RazaoSocial : f.NomeFantasia).ToListAsync();
        var dtos = lista.Select(ToDto).ToList();
        return Ok(new
        {
            total = dtos.Count,
            itens = dtos
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var f = await db.Fornecedores.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return f == null ? NotFound(new { erro = "Fornecedor não encontrado." }) : Ok(ToDto(f));
    }

    private static string? Validate(SaveFornecedorRequest r, bool creating)
    {
        if (creating && string.IsNullOrWhiteSpace(r.RazaoSocial) && string.IsNullOrWhiteSpace(r.NomeFantasia)) return "Razão social ou nome fantasia é obrigatório.";
        if (!string.IsNullOrWhiteSpace(r.Cnpj) && new string(r.Cnpj.Where(char.IsDigit).ToArray()).Length is not (11 or 14)) return "CNPJ/CPF inválido.";
        if (!string.IsNullOrWhiteSpace(r.Email) && !r.Email.Contains('@')) return "E-mail inválido.";
        if (r.PrazoEntregaPadrao is < 0 or > 365) return "Prazo de entrega inválido.";
        if (!string.IsNullOrWhiteSpace(r.Uf) && r.Uf.Trim().Length != 2) return "UF deve ter 2 letras.";
        return null;
    }

    private static string T(string? s, int max = 200) { s = (s ?? "").Trim(); return s.Length > max ? s[..max] : s; }

    [HttpPost]
    [Authorize(Roles = "TenantAdmin,Buyer")]
    public async Task<IActionResult> Create([FromBody] SaveFornecedorRequest r)
    {
        var erro = Validate(r, true);
        if (erro != null) return BadRequest(new { erro });
        var plano = (await db.Tenants.Include(t => t.Plano).FirstAsync()).Plano;
        if (plano != null && await db.Fornecedores.CountAsync(f => f.Ativo) >= plano.MaxFornecedores)
            return BadRequest(new { erro = $"Limite de {plano.MaxFornecedores} fornecedores do plano '{plano.Nome}' atingido." });
        var f = new Fornecedor
        {
            RazaoSocial = T(string.IsNullOrWhiteSpace(r.RazaoSocial) ? r.NomeFantasia : r.RazaoSocial), NomeFantasia = T(string.IsNullOrWhiteSpace(r.NomeFantasia) ? r.RazaoSocial : r.NomeFantasia),
            Cnpj = new string((r.Cnpj ?? "").Where(char.IsDigit).ToArray()), InscricaoEstadual = T(r.InscricaoEstadual, 30), Email = T(r.Email, 150).ToLowerInvariant(),
            Telefone = T(r.Telefone, 30), Whatsapp = T(r.Whatsapp, 30), Contato = T(r.Contato, 100), Endereco = T(r.Endereco, 250), Cidade = T(r.Cidade, 100),
            Uf = T(r.Uf, 2).ToUpperInvariant(), PrazoEntregaPadrao = r.PrazoEntregaPadrao is > 0 ? r.PrazoEntregaPadrao.Value : 3,
            CondicoesPagamento = T(r.CondicoesPagamento, 100), Observacoes = T(r.Observacoes, 1000), Origem = "manual",
        };
        db.Fornecedores.Add(f);
        audit.Add("FornecedorCriado", "Fornecedor", f.Id.ToString(), new { f.RazaoSocial });
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = f.Id }, ToDto(f));
    }

    /// <summary>Fornecedores importados do ERP têm os dados cadastrais controlados pelo ERP; o comprador complementa contatos e condições.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "TenantAdmin,Buyer")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveFornecedorRequest r)
    {
        var erro = Validate(r, false);
        if (erro != null) return BadRequest(new { erro });
        var f = await db.Fornecedores.FindAsync(id);
        if (f == null) return NotFound(new { erro = "Fornecedor não encontrado." });
        f.Email = T(r.Email ?? f.Email, 150).ToLowerInvariant(); f.Telefone = T(r.Telefone ?? f.Telefone, 30); f.Whatsapp = T(r.Whatsapp ?? f.Whatsapp, 30);
        f.Contato = T(r.Contato ?? f.Contato, 100); f.CondicoesPagamento = T(r.CondicoesPagamento ?? f.CondicoesPagamento, 100); f.Observacoes = T(r.Observacoes ?? f.Observacoes, 1000);
        if (r.PrazoEntregaPadrao is > 0) f.PrazoEntregaPadrao = r.PrazoEntregaPadrao.Value;
        if (f.Origem == "manual")
        {
            f.RazaoSocial = T(r.RazaoSocial ?? f.RazaoSocial); f.NomeFantasia = T(r.NomeFantasia ?? f.NomeFantasia); f.InscricaoEstadual = T(r.InscricaoEstadual ?? f.InscricaoEstadual, 30);
            if (r.Cnpj != null) f.Cnpj = new string(r.Cnpj.Where(char.IsDigit).ToArray());
            f.Endereco = T(r.Endereco ?? f.Endereco, 250); f.Cidade = T(r.Cidade ?? f.Cidade, 100); f.Uf = T(r.Uf ?? f.Uf, 2).ToUpperInvariant();
        }
        audit.Add("FornecedorAlterado", "Fornecedor", f.Id.ToString());
        await db.SaveChangesAsync();
        return Ok(ToDto(f));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "TenantAdmin,Buyer")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var f = await db.Fornecedores.FindAsync(id);
        if (f == null) return NotFound(new { erro = "Fornecedor não encontrado." });
        f.Ativo = false;
        audit.Add("FornecedorInativado", "Fornecedor", f.Id.ToString());
        await db.SaveChangesAsync();
        return Ok(new { mensagem = "Fornecedor inativado." });
    }

    [HttpGet("{id:guid}/historico")]
    public async Task<IActionResult> Historico(Guid id)
    {
        var f = await db.Fornecedores.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (f == null) return NotFound(new { erro = "Fornecedor não encontrado." });

        var cots = await db.CotacaoFornecedores.AsNoTracking().Include(c => c.Cotacao).Include(c => c.Resposta).Include(c => c.Acesso)
            .Where(c => c.FornecedorId == id).OrderByDescending(c => c.DataCriacao).Take(50).ToListAsync();
        var enviadas = cots.Count;
        var respondidas = cots.Count(c => c.Resposta is { Status: "Enviada" });
        var prazos = cots.Where(c => c.Resposta is { Status: "Enviada", PrazoEntregaDias: > 0 }).Select(c => (decimal)c.Resposta!.PrazoEntregaDias).ToList();

        var precos = await db.HistoricoPrecos.AsNoTracking().Include(h => h.Produto).Where(h => h.FornecedorId == id).OrderByDescending(h => h.Data).Take(300).ToListAsync();
        var porProduto = precos.GroupBy(p => p.ProdutoId).Select(g => new
        {
            produtoId = g.Key, produto = g.First().Produto!.Descricao, unidade = g.First().Produto!.Unidade, ultimoPreco = g.First().Valor, ultimaCompra = g.First().Data,
            menorPreco = g.Min(x => x.Valor), maiorPreco = g.Max(x => x.Valor), registros = g.Count(),
        }).OrderByDescending(x => x.ultimaCompra).ToList();

        return Ok(new
        {
            fornecedor = ToDto(f),
            resumo = new
            {
                cotacoesRecebidas = enviadas, respondidas, taxaResposta = enviadas > 0 ? Math.Round(100m * respondidas / enviadas, 1) : (decimal?)null,
                prazoMedioPropostoDias = prazos.Count > 0 ? Math.Round(prazos.Average(), 1) : (decimal?)null,
                aprovadas = cots.Count(c => c.Status == "Aprovada"), produtosFornecidos = porProduto.Count,
                totalComprasRegistradas = Math.Round(precos.Sum(p => p.Valor * p.Quantidade), 2),
            },
            cotacoes = cots.Select(c => new { cotacaoId = c.CotacaoId, numero = c.Cotacao!.Numero, titulo = c.Cotacao.Titulo, status = c.Status, data = c.DataCriacao,
                respondidaEm = c.Resposta?.EnviadaEm, visualizacoes = c.Acesso?.Visualizacoes ?? 0 }),
            precosPorProduto = porProduto,
            historicoPrecos = precos.Take(100).Select(p => new { p.Data, produto = p.Produto!.Descricao, p.Valor, p.Quantidade, p.Origem }),
        });
    }
}

