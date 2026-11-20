using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.DTOs;
using Nexo.Api.Models;
using Nexo.Api.MultiTenancy;
using Nexo.Api.Security;
using Nexo.Api.Services;
using Nexo.Api.Services.Fiscal;

namespace Nexo.Api.Controllers;

/// <summary>
/// Portal público do fornecedor. Acesso em duas etapas: link com token aleatório + código independente. A resposta do
/// código é uma sessão curta (JWT próprio, com chave e audiência distintas dos usuários) que só enxerga a própria solicitação.
/// </summary>
[ApiController]
[Route("api/portal")]
public class PortalController(NexoDbContext db, ITenantContext tenant, IJwtService jwt, ITenantConfigService cfgService, IAuditService audit, IFileStorage storage, IConfiguration config) : ControllerBase
{
    private const int MaxTentativas = 5;
    private const long MaxAnexo = 5 * 1024 * 1024;
    private static readonly string[] ExtPermitidas = { ".pdf", ".png", ".jpg", ".jpeg", ".xml" };
    private static readonly string[] StatusEditaveis = { "Enviada", "Visualizada", "Respondida" };

    private IActionResult Negado() => Unauthorized(new { erro = "Link ou código inválido, expirado ou revogado." });

    // ---------- 1ª etapa: link + código ----------
    [HttpPost("cotacao/{token}/acessar")]
    [AllowAnonymous]
    [EnableRateLimiting("portal-acesso")]
    public async Task<IActionResult> Acessar(string token, [FromBody] AcessarPortalRequest r)
    {
        var codigo = Secrets.NormalizeCode(r.Codigo);
        if (token.Length is < 20 or > 64 || codigo.Length != 12) return Negado();

        using var _ = tenant.SystemScope();
        var a = await db.AcessosFornecedor.Include(x => x.CotacaoFornecedor).FirstOrDefaultAsync(x => x.Token == token);
        if (a == null || a.Revogado || a.ExpiraEm < DateTime.UtcNow) return Negado();
        if (a.BloqueadoAte > DateTime.UtcNow)
            return StatusCode(429, new { erro = "Muitas tentativas incorretas. Aguarde alguns minutos e tente novamente." });

        if (!BCrypt.Net.BCrypt.Verify(codigo, a.CodigoHash))
        {
            a.Tentativas++;
            if (a.Tentativas >= MaxTentativas) { a.BloqueadoAte = DateTime.UtcNow.AddMinutes(15); a.Tentativas = 0; audit.Add("AcessoFornecedorBloqueado", "AcessoFornecedor", a.Id.ToString(), null, a.TenantId); }
            await db.SaveChangesAsync();
            return Negado();
        }

        a.Tentativas = 0; a.BloqueadoAte = null; a.Visualizacoes++;
        a.PrimeiraVisualizacaoEm ??= DateTime.UtcNow; a.UltimaVisualizacaoEm = DateTime.UtcNow;
        if (a.CotacaoFornecedor!.Status == "Enviada") a.CotacaoFornecedor.Status = "Visualizada";
        audit.Add("PortalAcesso", "CotacaoFornecedor", a.CotacaoFornecedorId.ToString(), new { visualizacao = a.Visualizacoes }, a.TenantId);
        await db.SaveChangesAsync();

        var sessao = jwt.GeneratePortalToken(a.Id, a.CotacaoFornecedorId, a.TenantId);
        return Ok(new AcessarPortalResponse(sessao, DateTime.UtcNow.AddMinutes(120)));
    }

    // ---------- sessão autenticada ----------
    private Guid CfId => Guid.Parse(User.FindFirstValue("cf_id")!);
    private Guid AcessoId => Guid.Parse(User.FindFirstValue("acesso_id")!);

    private async Task<(CotacaoFornecedor? cf, bool ok)> Sessao(bool tracking = true)
    {
        var q = db.CotacaoFornecedores.Include(c => c.Acesso).Include(c => c.Cotacao).ThenInclude(c => c!.Itens).Include(c => c.Fornecedor)
            .Include(c => c.Resposta).ThenInclude(r => r!.Itens).Include(c => c.Resposta).ThenInclude(r => r!.Anexos).AsSplitQuery();
        if (!tracking) q = q.AsNoTracking();
        var cf = await q.FirstOrDefaultAsync(c => c.Id == CfId);
        // revogação e expiração valem imediatamente, mesmo com sessão ainda dentro do prazo
        if (cf?.Acesso == null || cf.Acesso.Id != AcessoId || cf.Acesso.Revogado || cf.Acesso.ExpiraEm < DateTime.UtcNow) return (null, false);
        return (cf, true);
    }

    private static bool Editavel(CotacaoFornecedor cf) => cf.Cotacao!.Status == "Aberta" && StatusEditaveis.Contains(cf.Status);

    private static RespostaDto? ToDto(RespostaCotacao? r) => r == null ? null : new RespostaDto(r.Status, r.Frete, r.OutrasDespesas, r.PrazoEntregaDias, r.CondicaoPagamento, r.ValidadeProposta,
        r.ContatoNome, r.Observacoes, r.EnviadaEm, r.Itens.Select(i => new RespostaItemDto(i.CotacaoItemId, i.Disponivel, i.QuantidadeOfertada, i.PrecoUnitario, i.DescontoPerc, i.Cst, i.Cfop,
            i.IcmsAliq, i.IpiAliq, i.Mva, i.IcmsStAliq, i.IcmsStValor, i.Observacao)).ToList());

    private async Task<PortalCotacaoDto> Montar(CotacaoFornecedor cf)
    {
        var empresa = (await db.Tenants.AsNoTracking().FirstOrDefaultAsync())?.NomeFantasia ?? "";
        string? msg = cf.Status switch
        {
            "Aprovada" => "Parabéns! Sua proposta foi aprovada pelo comprador. Ele entrará em contato para formalizar o pedido.",
            "NaoSelecionada" => "Agradecemos a participação. Desta vez sua proposta não foi selecionada.",
            "Cancelada" => "Esta solicitação foi cancelada pelo comprador.",
            _ => cf.Cotacao!.Status != "Aberta" ? "Esta cotação foi encerrada." : null,
        };
        var avisos = new List<string>();
        return new PortalCotacaoDto(empresa, cf.Cotacao!.Numero, cf.Cotacao.Titulo, cf.Cotacao.Observacoes, cf.Acesso!.ExpiraEm, cf.Fornecedor!.NomeFantasia != "" ? cf.Fornecedor.NomeFantasia : cf.Fornecedor.RazaoSocial,
            cf.Status, Editavel(cf), msg, cf.Cotacao.Itens.OrderBy(i => i.Descricao).Select(i => new PortalItemDto(i.Id, i.Codigo, i.Descricao, i.Unidade, i.Quantidade)).ToList(),
            ToDto(cf.Resposta), cf.Resposta?.Anexos.Select(a => new AnexoDto(a.Id, a.NomeArquivo, a.ContentType, a.Tamanho, a.DataCriacao)).ToList() ?? new(), avisos);
    }

    [HttpGet("cotacao")]
    [Authorize(AuthenticationSchemes = "Portal", Roles = "Supplier")]
    [EnableRateLimiting("portal")]
    public async Task<IActionResult> Get()
    {
        var (cf, ok) = await Sessao(false);
        return ok ? Ok(await Montar(cf!)) : Negado();
    }

    private async Task<(ResultadoValidacao v, string? erro)> Validar(CotacaoFornecedor cf, SalvarRespostaRequest r, bool final)
    {
        var cfg = await cfgService.GetAsync();
        var itens = new List<ItemParaValidar>();
        foreach (var it in cf.Cotacao!.Itens)
        {
            var x = r.Itens?.FirstOrDefault(i => i.CotacaoItemId == it.Id);
            itens.Add(x == null ? new ItemParaValidar(it.Descricao, it.Quantidade, false, 0, 0, 0, "", "", 0, 0, 0, 0, 0)
                : new ItemParaValidar(it.Descricao, it.Quantidade, x.Disponivel, x.QuantidadeOfertada, x.PrecoUnitario, x.DescontoPerc, x.Cst ?? "", x.Cfop ?? "", x.IcmsAliq, x.IpiAliq, x.Mva, x.IcmsStAliq, x.IcmsStValor));
        }
        var v = PropostaValidator.Validar(itens, r.Frete, r.OutrasDespesas, r.PrazoEntregaDias, r.CondicaoPagamento ?? "", r.ValidadeProposta, cf.Fornecedor!.Uf, cfg.Uf, final);
        return (v, null);
    }

    private static string T(string? s, int max) { s = (s ?? "").Trim(); return s.Length > max ? s[..max] : s; }

    private static void Aplicar(RespostaCotacao resp, Cotacao cot, SalvarRespostaRequest r)
    {
        resp.Frete = Math.Round(r.Frete, 2); resp.OutrasDespesas = Math.Round(r.OutrasDespesas, 2); resp.PrazoEntregaDias = r.PrazoEntregaDias;
        resp.CondicaoPagamento = T(r.CondicaoPagamento, 100); resp.ValidadeProposta = r.ValidadeProposta; resp.ContatoNome = T(r.ContatoNome, 120); resp.Observacoes = T(r.Observacoes, 2000);
        foreach (var it in cot.Itens)
        {
            var x = r.Itens?.FirstOrDefault(i => i.CotacaoItemId == it.Id);
            var linha = resp.Itens.FirstOrDefault(l => l.CotacaoItemId == it.Id);
            if (linha == null) { linha = new RespostaCotacaoItem { CotacaoItemId = it.Id, TenantId = resp.TenantId }; resp.Itens.Add(linha); }
            if (x == null) { linha.Disponivel = false; continue; }
            linha.Disponivel = x.Disponivel; linha.QuantidadeOfertada = x.Disponivel ? Math.Round(x.QuantidadeOfertada, 3) : 0; linha.PrecoUnitario = x.Disponivel ? Math.Round(x.PrecoUnitario, 4) : 0;
            linha.DescontoPerc = Math.Round(x.DescontoPerc, 4); linha.Cst = new string((x.Cst ?? "").Where(char.IsDigit).ToArray()); linha.Cfop = new string((x.Cfop ?? "").Where(char.IsDigit).ToArray());
            linha.IcmsAliq = Math.Round(x.IcmsAliq, 4); linha.IpiAliq = Math.Round(x.IpiAliq, 4); linha.Mva = Math.Round(x.Mva, 4); linha.IcmsStAliq = Math.Round(x.IcmsStAliq, 4);
            linha.IcmsStValor = Math.Round(x.IcmsStValor, 2); linha.Observacao = T(x.Observacao, 300);
        }
    }

    private async Task<IActionResult> Salvar(SalvarRespostaRequest r, bool final)
    {
        var (cf, ok) = await Sessao();
        if (!ok) return Negado();
        if (!Editavel(cf!)) return Conflict(new { erro = "Esta solicitação não aceita mais alterações." });
        if (!final && cf!.Resposta is { Status: "Enviada" }) return Conflict(new { erro = "A proposta já foi enviada. Edite e envie novamente para atualizá-la." });

        var (v, _) = await Validar(cf!, r, final);
        if (!v.Valido) return BadRequest(new { erro = "Corrija os campos destacados antes de continuar.", erros = v.Erros, avisos = v.Avisos });

        var resp = cf!.Resposta;
        if (resp == null) { resp = new RespostaCotacao { TenantId = cf.TenantId, CotacaoFornecedorId = cf.Id }; cf.Resposta = resp; db.RespostasCotacao.Add(resp); }
        Aplicar(resp, cf.Cotacao!, r);
        if (final)
        {
            var reenvio = resp.Status == "Enviada";
            resp.Status = "Enviada"; resp.EnviadaEm = DateTime.UtcNow; cf.Status = "Respondida";
            audit.Add(reenvio ? "PropostaReenviada" : "PropostaEnviada", "CotacaoFornecedor", cf.Id.ToString(), null, cf.TenantId);
        }
        else { resp.Status = "Rascunho"; }
        await db.SaveChangesAsync();
        return Ok(new { status = resp.Status, avisos = v.Avisos, mensagem = final ? "Proposta enviada com sucesso." : "Rascunho salvo." });
    }

    [HttpPut("cotacao/resposta")]
    [Authorize(AuthenticationSchemes = "Portal", Roles = "Supplier")]
    [EnableRateLimiting("portal")]
    public Task<IActionResult> Rascunho([FromBody] SalvarRespostaRequest r) => Salvar(r, false);

    [HttpPost("cotacao/resposta/enviar")]
    [Authorize(AuthenticationSchemes = "Portal", Roles = "Supplier")]
    [EnableRateLimiting("portal")]
    public Task<IActionResult> Enviar([FromBody] SalvarRespostaRequest r) => Salvar(r, true);

    [HttpPost("cotacao/resposta/validar")]
    [Authorize(AuthenticationSchemes = "Portal", Roles = "Supplier")]
    [EnableRateLimiting("portal")]
    public async Task<IActionResult> ValidarEnvio([FromBody] SalvarRespostaRequest r)
    {
        var (cf, ok) = await Sessao(false);
        if (!ok) return Negado();
        var (v, _) = await Validar(cf!, r, true);
        return Ok(new ValidacaoDto(v.Valido, v.Erros, v.Avisos));
    }

    // ---------- anexos ----------
    private static bool ConteudoValido(string ext, byte[] head) => ext switch
    {
        ".pdf" => head.Length >= 4 && head[0] == 0x25 && head[1] == 0x50 && head[2] == 0x44 && head[3] == 0x46,
        ".png" => head.Length >= 4 && head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47,
        ".jpg" or ".jpeg" => head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF,
        ".xml" => head.Length >= 1 && (head[0] == (byte)'<' || (head.Length >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF)),
        _ => false,
    };

    [HttpPost("cotacao/anexos")]
    [Authorize(AuthenticationSchemes = "Portal", Roles = "Supplier")]
    [EnableRateLimiting("portal")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Anexar(IFormFile arquivo)
    {
        var (cf, ok) = await Sessao();
        if (!ok) return Negado();
        if (!Editavel(cf!)) return Conflict(new { erro = "Esta solicitação não aceita mais alterações." });
        if (arquivo == null || arquivo.Length == 0) return BadRequest(new { erro = "Arquivo vazio." });
        if (arquivo.Length > MaxAnexo) return BadRequest(new { erro = "Arquivo acima de 5 MB." });
        var ext = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
        if (!ExtPermitidas.Contains(ext)) return BadRequest(new { erro = "Tipo de arquivo não permitido. Envie PDF, PNG, JPG ou XML." });

        await using var s = arquivo.OpenReadStream();
        var head = new byte[8]; var n = await s.ReadAsync(head);
        if (!ConteudoValido(ext, head[..n])) return BadRequest(new { erro = "O conteúdo do arquivo não corresponde à extensão informada." });
        s.Position = 0;

        var resp = cf!.Resposta;
        if (resp == null) { resp = new RespostaCotacao { TenantId = cf.TenantId, CotacaoFornecedorId = cf.Id }; cf.Resposta = resp; db.RespostasCotacao.Add(resp); await db.SaveChangesAsync(); }
        if (resp.Anexos.Count >= 5) return BadRequest(new { erro = "Máximo de 5 anexos por proposta." });

        var nome = Path.GetFileName(arquivo.FileName); nome = nome.Length > 120 ? nome[^120..] : nome;
        var path = await storage.SaveAsync(cf.TenantId, resp.Id, nome, s);
        var ct = ext switch { ".pdf" => "application/pdf", ".png" => "image/png", ".xml" => "application/xml", _ => "image/jpeg" };
        var anexo = new AnexoResposta { TenantId = cf.TenantId, RespostaCotacaoId = resp.Id, NomeArquivo = nome, ContentType = ct, Tamanho = arquivo.Length, CaminhoStorage = path };
        db.AnexosResposta.Add(anexo);
        audit.Add("AnexoEnviado", "AnexoResposta", anexo.Id.ToString(), new { nome, arquivo.Length }, cf.TenantId);
        await db.SaveChangesAsync();
        return Ok(new AnexoDto(anexo.Id, anexo.NomeArquivo, anexo.ContentType, anexo.Tamanho, anexo.DataCriacao));
    }

    [HttpDelete("cotacao/anexos/{id:guid}")]
    [Authorize(AuthenticationSchemes = "Portal", Roles = "Supplier")]
    [EnableRateLimiting("portal")]
    public async Task<IActionResult> RemoverAnexo(Guid id)
    {
        var (cf, ok) = await Sessao();
        if (!ok) return Negado();
        if (!Editavel(cf!)) return Conflict(new { erro = "Esta solicitação não aceita mais alterações." });
        var a = cf!.Resposta?.Anexos.FirstOrDefault(x => x.Id == id); // restrito à própria resposta
        if (a == null) return NotFound(new { erro = "Anexo não encontrado." });
        storage.Delete(a.CaminhoStorage);
        db.AnexosResposta.Remove(a);
        await db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}

