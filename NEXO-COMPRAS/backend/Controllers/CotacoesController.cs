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

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "TenantAdmin,Buyer,Viewer,PlatformAdmin")]
public class CotacoesController(NexoDbContext db, ITenantContext tenant, ITenantConfigService cfgService, IAuditService audit, IConfiguration config, IFileStorage storage) : ControllerBase
{
    private string LinkFor(string token) => $"{(config["Portal:BaseUrl"] ?? "http://localhost:5173").TrimEnd('/')}/cotacao/{token}";

    private static string NomeForn(Fornecedor f) => string.IsNullOrWhiteSpace(f.NomeFantasia) ? f.RazaoSocial : f.NomeFantasia;

    private static string StatusEfetivo(CotacaoFornecedor cf)
    {
        if (cf.Status is "Enviada" or "Visualizada" && cf.Acesso != null && cf.Acesso.ExpiraEm < DateTime.UtcNow) return "Expirada";
        return cf.Status;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status)
    {
        var q = db.Cotacoes.AsNoTracking().Include(c => c.Itens).Include(c => c.Fornecedores).ThenInclude(f => f.Resposta).Include(c => c.Fornecedores).ThenInclude(f => f.Acesso).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("Todas", StringComparison.OrdinalIgnoreCase))
        {
            if (status.Equals("Aberta", StringComparison.OrdinalIgnoreCase))
                q = q.Where(c => c.Status == "Aberta");
            else if (status.Equals("EmAnalise", StringComparison.OrdinalIgnoreCase))
                q = q.Where(c => c.Status == "EmAnalise");
            else if (status.Equals("Aprovada", StringComparison.OrdinalIgnoreCase) || status.Equals("Encerrada", StringComparison.OrdinalIgnoreCase))
                q = q.Where(c => c.Status == "Aprovada" || c.Status == "Encerrada");
            else if (status.Equals("Cancelada", StringComparison.OrdinalIgnoreCase))
                q = q.Where(c => c.Status == "Cancelada");
            else
                q = q.Where(c => c.Status == status);
        }
        var lista = await q.OrderByDescending(c => c.DataCriacao).Take(200).ToListAsync();
        var dtos = lista.Select(c => new CotacaoResumoDto(c.Id, c.Numero, c.Titulo, c.Status, c.DataCriacao, c.DataLimiteResposta, c.Itens.Count, c.Fornecedores.Count,
            c.Fornecedores.Count(f => f.Resposta is { Status: "Enviada" }), c.Fornecedores.Count(f => f.Acesso is { Visualizacoes: > 0 }), c.TotalAprovado, c.EconomiaRealizada)).ToList();
        return Ok(new { total = dtos.Count, itens = dtos });
    }

    [HttpPost]
    [Authorize(Roles = "TenantAdmin,Buyer")]
    public async Task<IActionResult> Create([FromBody] CriarCotacaoRequest r)
    {
        if (r.Itens == null || r.Itens.Count == 0) return BadRequest(new { erro = "Selecione ao menos um produto." });
        if (r.Itens.Count > 300) return BadRequest(new { erro = "Máximo de 300 itens por cotação." });
        if (r.Itens.Any(i => i.Quantidade <= 0 || i.Quantidade > 99_999_999)) return BadRequest(new { erro = "Todas as quantidades devem ser maiores que zero." });
        if (r.Itens.GroupBy(i => i.ProdutoId).Any(g => g.Count() > 1)) return BadRequest(new { erro = "Há produtos repetidos na cotação." });
        var fids = (r.FornecedorIds ?? new()).Distinct().ToList();
        if (fids.Count == 0) return BadRequest(new { erro = "Selecione ao menos um fornecedor." });
        if (fids.Count > 30) return BadRequest(new { erro = "Máximo de 30 fornecedores por cotação." });

        var cfg = await cfgService.GetAsync();
        var pids = r.Itens.Select(i => i.ProdutoId).ToList();
        var produtos = await db.Produtos.AsNoTracking().Where(p => pids.Contains(p.Id) && p.Ativo).ToDictionaryAsync(p => p.Id);
        if (produtos.Count != pids.Count) return BadRequest(new { erro = "Há produtos inválidos na solicitação." });
        var forns = await db.Fornecedores.AsNoTracking().Where(f => fids.Contains(f.Id) && f.Ativo).ToListAsync();
        if (forns.Count != fids.Count) return BadRequest(new { erro = "Há fornecedores inválidos na solicitação." });

        var plano = (await db.Tenants.Include(t => t.Plano).FirstAsync()).Plano;
        if (plano != null)
        {
            var inicioMes = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            if (await db.Cotacoes.CountAsync(c => c.DataCriacao >= inicioMes) >= plano.MaxCotacoesMes)
                return BadRequest(new { erro = $"Limite de {plano.MaxCotacoesMes} cotações por mês do plano '{plano.Nome}' atingido." });
        }

        var dias = Math.Clamp(r.PrazoRespostaDias ?? cfg.ValidadeCotacaoDias, 1, 60);
        var limite = DateTime.UtcNow.AddDays(dias);
        var prefixo = $"COT-{DateTime.UtcNow.Year}-";
        var ultimo = await db.Cotacoes.Where(c => c.Numero.StartsWith(prefixo)).OrderByDescending(c => c.Numero).Select(c => c.Numero).FirstOrDefaultAsync();
        var seq = ultimo != null && int.TryParse(ultimo[prefixo.Length..], out var n) ? n + 1 : 1;

        var cot = new Cotacao
        {
            Numero = $"{prefixo}{seq:0000}", Titulo = string.IsNullOrWhiteSpace(r.Titulo) ? "Cotação de compra" : r.Titulo.Trim()[..Math.Min(200, r.Titulo.Trim().Length)],
            Observacoes = (r.Observacoes ?? "").Trim()[..Math.Min(2000, (r.Observacoes ?? "").Trim().Length)], CriadaPorId = tenant.CurrentUserId, DataLimiteResposta = limite,
            FilialId = r.FilialId,
            TipoCompra = string.IsNullOrWhiteSpace(r.TipoCompra) ? "Centralizada" : r.TipoCompra
        };
        foreach (var i in r.Itens)
        {
            var p = produtos[i.ProdutoId];
            cot.Itens.Add(new CotacaoItem { ProdutoId = p.Id, Codigo = p.CodigoErp, Descricao = p.Descricao, Unidade = p.Unidade, Quantidade = Math.Round(i.Quantidade, 3),
                EstoqueNoMomento = p.EstoqueAtual ?? 0m, UltimoCustoRef = p.UltimoCusto > 0 ? p.UltimoCusto : p.CustoAtual, PrecoVendaRef = p.PrecoVenda });
        }
        var acessos = new List<AcessoCriadoDto>();
        foreach (var f in forns)
        {
            var codigo = Secrets.AccessCode();
            var acesso = new AcessoFornecedor { Token = Secrets.RandomToken(), CodigoHash = BCrypt.Net.BCrypt.HashPassword(Secrets.NormalizeCode(codigo)), ExpiraEm = limite };
            var cf = new CotacaoFornecedor { FornecedorId = f.Id, Acesso = acesso };
            cot.Fornecedores.Add(cf);
            acessos.Add(new AcessoCriadoDto(cf.Id, f.Id, NomeForn(f), LinkFor(acesso.Token), codigo, limite));
        }
        db.Cotacoes.Add(cot);
        audit.Add("CotacaoCriada", "Cotacao", cot.Id.ToString(), new { cot.Numero, itens = cot.Itens.Count, fornecedores = forns.Count });
        await db.SaveChangesAsync();
        return Ok(new CotacaoCriadaDto(cot.Id, cot.Numero, acessos));
    }

    private IQueryable<Cotacao> Full() => db.Cotacoes.Include(c => c.Itens).Include(c => c.Fornecedores).ThenInclude(f => f.Fornecedor)
        .Include(c => c.Fornecedores).ThenInclude(f => f.Acesso).Include(c => c.Fornecedores).ThenInclude(f => f.Resposta).ThenInclude(r => r!.Itens)
        .AsSplitQuery();

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var c = await Full().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound(new { erro = "Cotação não encontrada." });
        return Ok(new CotacaoDetalheDto(c.Id, c.Numero, c.Titulo, c.Observacoes, c.Status, c.DataCriacao, c.DataLimiteResposta, string.IsNullOrEmpty(c.AprovadaPorNome) ? null : c.AprovadaPorNome,
            c.AprovadaEm, c.ObservacaoAprovacao, c.TotalAprovado, c.EconomiaPotencial, c.EconomiaRealizada,
            c.Itens.OrderBy(i => i.Descricao).Select(i => new CotacaoItemDto(i.Id, i.ProdutoId, i.Codigo, i.Descricao, i.Unidade, i.Quantidade, i.EstoqueNoMomento, i.UltimoCustoRef, i.PrecoVendaRef)).ToList(),
            c.Fornecedores.OrderBy(f => NomeForn(f.Fornecedor!)).Select(f => new CotacaoFornecedorDto(f.Id, f.FornecedorId, NomeForn(f.Fornecedor!), f.Fornecedor!.Email, string.IsNullOrEmpty(f.Fornecedor.Whatsapp) ? f.Fornecedor.Telefone : f.Fornecedor.Whatsapp,
                StatusEfetivo(f), f.Acesso == null ? null : new AcessoInfoDto(f.Acesso.Token[..6] + "…", f.Acesso.ExpiraEm, f.Acesso.Revogado, f.Acesso.BloqueadoAte > DateTime.UtcNow, f.Acesso.Tentativas,
                    f.Acesso.Visualizacoes, f.Acesso.PrimeiraVisualizacaoEm, f.Acesso.UltimaVisualizacaoEm), f.Resposta?.EnviadaEm, f.Resposta is { Status: "Enviada" })).ToList()));
    }

    [HttpGet("{id:guid}/fornecedores/{cfId:guid}/resposta")]
    public async Task<IActionResult> Resposta(Guid id, Guid cfId)
    {
        var cf = await db.CotacaoFornecedores.AsNoTracking().Include(f => f.Fornecedor).Include(f => f.Resposta).ThenInclude(r => r!.Itens).Include(f => f.Resposta).ThenInclude(r => r!.Anexos)
            .FirstOrDefaultAsync(f => f.Id == cfId && f.CotacaoId == id);
        if (cf?.Resposta == null || cf.Resposta.Status != "Enviada") return NotFound(new { erro = "Proposta ainda não enviada pelo fornecedor." });
        var r = cf.Resposta;
        return Ok(new
        {
            fornecedor = NomeForn(cf.Fornecedor!),
            resposta = new RespostaDto(r.Status, r.Frete, r.OutrasDespesas, r.PrazoEntregaDias, r.CondicaoPagamento, r.ValidadeProposta, r.ContatoNome, r.Observacoes, r.EnviadaEm,
                r.Itens.Select(i => new RespostaItemDto(i.CotacaoItemId, i.Disponivel, i.QuantidadeOfertada, i.PrecoUnitario, i.DescontoPerc, i.Cst, i.Cfop, i.IcmsAliq, i.IpiAliq, i.Mva, i.IcmsStAliq, i.IcmsStValor, i.Observacao)).ToList()),
            anexos = r.Anexos.Select(a => new AnexoDto(a.Id, a.NomeArquivo, a.ContentType, a.Tamanho, a.DataCriacao)),
        });
    }

    [HttpGet("anexos/{anexoId:guid}")]
    public async Task<IActionResult> DownloadAnexo(Guid anexoId)
    {
        var a = await db.AnexosResposta.AsNoTracking().FirstOrDefaultAsync(x => x.Id == anexoId);
        if (a == null) return NotFound();
        return File(storage.Open(a.CaminhoStorage), a.ContentType, a.NomeArquivo);
    }

    private async Task<(Cotacao?, CotacaoFornecedor?)> LoadCf(Guid id, Guid cfId)
    {
        var cot = await db.Cotacoes.FirstOrDefaultAsync(c => c.Id == id);
        var cf = await db.CotacaoFornecedores.Include(f => f.Acesso).Include(f => f.Fornecedor).FirstOrDefaultAsync(f => f.Id == cfId && f.CotacaoId == id);
        return (cot, cf);
    }

    /// <summary>Gera novo link + código (o anterior deixa de funcionar). O código só é exibido neste momento.</summary>
    [HttpPost("{id:guid}/fornecedores/{cfId:guid}/novo-codigo")]
    [Authorize(Roles = "TenantAdmin,Buyer")]
    public async Task<IActionResult> NovoCodigo(Guid id, Guid cfId)
    {
        var (cot, cf) = await LoadCf(id, cfId);
        if (cot == null || cf?.Acesso == null) return NotFound(new { erro = "Solicitação não encontrada." });
        if (cot.Status != "Aberta") return BadRequest(new { erro = "A cotação não está aberta." });
        var cfg = await cfgService.GetAsync();
        var codigo = Secrets.AccessCode();
        var a = cf.Acesso;
        a.Token = Secrets.RandomToken(); a.CodigoHash = BCrypt.Net.BCrypt.HashPassword(Secrets.NormalizeCode(codigo));
        a.Revogado = false; a.Tentativas = 0; a.BloqueadoAte = null;
        var novoPrazo = DateTime.UtcNow.AddDays(cfg.ValidadeCotacaoDias);
        a.ExpiraEm = novoPrazo > cot.DataLimiteResposta ? novoPrazo : cot.DataLimiteResposta;
        audit.Add("AcessoFornecedorRenovado", "CotacaoFornecedor", cf.Id.ToString());
        await db.SaveChangesAsync();
        return Ok(new NovoCodigoDto(cf.Id, LinkFor(a.Token), codigo, a.ExpiraEm));
    }

    [HttpPost("{id:guid}/fornecedores/{cfId:guid}/revogar")]
    [Authorize(Roles = "TenantAdmin,Buyer")]
    public async Task<IActionResult> Revogar(Guid id, Guid cfId)
    {
        var (cot, cf) = await LoadCf(id, cfId);
        if (cot == null || cf?.Acesso == null) return NotFound(new { erro = "Solicitação não encontrada." });
        cf.Acesso.Revogado = true;
        audit.Add("AcessoFornecedorRevogado", "CotacaoFornecedor", cf.Id.ToString());
        await db.SaveChangesAsync();
        return Ok(new { mensagem = "Acesso revogado." });
    }

    [HttpPost("{id:guid}/cancelar")]
    [Authorize(Roles = "TenantAdmin,Buyer")]
    public async Task<IActionResult> Cancelar(Guid id)
    {
        var cot = await db.Cotacoes.Include(c => c.Fornecedores).ThenInclude(f => f.Acesso).FirstOrDefaultAsync(c => c.Id == id);
        if (cot == null) return NotFound(new { erro = "Cotação não encontrada." });
        if (cot.Status != "Aberta") return BadRequest(new { erro = "Só é possível cancelar cotações abertas." });
        cot.Status = "Cancelada";
        foreach (var f in cot.Fornecedores) { f.Status = "Cancelada"; if (f.Acesso != null) f.Acesso.Revogado = true; }
        audit.Add("CotacaoCancelada", "Cotacao", cot.Id.ToString());
        await db.SaveChangesAsync();
        return Ok(new { mensagem = "Cotação cancelada." });
    }

    // ---------------- Fase 5: comparação e aprovação ----------------

    [HttpGet("{id:guid}/comparacao")]
    public async Task<IActionResult> Comparacao(Guid id)
    {
        var cot = await Full().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (cot == null) return NotFound(new { erro = "Cotação não encontrada." });
        var cfg = await cfgService.GetAsync();
        return Ok(ComparacaoService.Comparar(cot, ITenantConfigService.ToTributacao(cfg)));
    }

    /// <summary>A aprovação final é sempre do comprador (TenantAdmin/Buyer) e pode escolher um fornecedor para tudo ou fornecedores diferentes por item.</summary>
    [HttpPost("{id:guid}/aprovar")]
    [Authorize(Roles = "TenantAdmin,Buyer")]
    public async Task<IActionResult> Aprovar(Guid id, [FromBody] AprovarCotacaoRequest r)
    {
        var cot = await Full().FirstOrDefaultAsync(c => c.Id == id);
        if (cot == null) return NotFound(new { erro = "Cotação não encontrada." });
        if (cot.Status != "Aberta") return BadRequest(new { erro = "Esta cotação já foi encerrada." });
        if (r.Escolhas == null || r.Escolhas.Count == 0) return BadRequest(new { erro = "Escolha ao menos um item e o fornecedor vencedor." });

        var cfg = await cfgService.GetAsync();
        var cmp = ComparacaoService.Comparar(cot, ITenantConfigService.ToTributacao(cfg));
        if (!cmp.TemPropostas) return BadRequest(new { erro = "Não há propostas enviadas para aprovar." });

        foreach (var (itemId, cfId) in r.Escolhas)
        {
            var item = cmp.Itens.FirstOrDefault(i => i.CotacaoItemId == itemId);
            if (item == null) return BadRequest(new { erro = "Item não pertence a esta cotação." });
            if (!item.Ofertas.Any(o => o.CotacaoFornecedorId == cfId)) return BadRequest(new { erro = $"O fornecedor escolhido não ofertou o item '{item.Descricao}'." });
        }

        var eco = ComparacaoService.CalcularEconomia(cmp, r.Escolhas);
        var nomeUsuario = tenant.UserName ?? "";
        foreach (var item in cot.Itens) item.CotacaoFornecedorEscolhidoId = r.Escolhas.TryGetValue(item.Id, out var cfId) ? cfId : null;
        var vencedores = r.Escolhas.Values.ToHashSet();
        foreach (var f in cot.Fornecedores)
            f.Status = vencedores.Contains(f.Id) ? "Aprovada" : f.Status == "Cancelada" ? f.Status : "NaoSelecionada";

        foreach (var (itemId, cfId) in r.Escolhas)
        {
            var o = cmp.Itens.FirstOrDefault(i => i.CotacaoItemId == itemId)?.Ofertas.FirstOrDefault(x => x.CotacaoFornecedorId == cfId);
            var cf = cot.Fornecedores.FirstOrDefault(f => f.Id == cfId);
            var it = cot.Itens.FirstOrDefault(i => i.Id == itemId);
            if (o != null && cf != null && it != null)
            {
                db.HistoricoPrecos.Add(new HistoricoPreco { ProdutoId = it.ProdutoId, FornecedorId = cf.FornecedorId, Data = DateTime.UtcNow.Date, Valor = o.PrecoLiquidoUnit,
                    Quantidade = Math.Min(it.Quantidade, o.QuantidadeOfertada), Origem = "cotacao" });
            }
        }

        cot.Status = "Aprovada"; cot.AprovadaPorId = tenant.CurrentUserId; cot.AprovadaPorNome = nomeUsuario; cot.AprovadaEm = DateTime.UtcNow;
        cot.ObservacaoAprovacao = (r.Observacao ?? "").Trim()[..Math.Min(1000, (r.Observacao ?? "").Trim().Length)];
        cot.TotalAprovado = eco.TotalAprovado; cot.EconomiaPotencial = eco.Potencial; cot.EconomiaRealizada = eco.Realizada;

        // Auto-geração dos Pedidos de Compra para os fornecedores vencedores
        var ano = DateTime.UtcNow.Year;
        var qtdExistente = await db.PedidosCompra.CountAsync(p => p.Numero.StartsWith($"PED-{ano}-"));
        var pedidosCriados = new List<PedidoCompra>();

        var grupos = r.Escolhas.GroupBy(kv => kv.Value);
        foreach (var grupo in grupos)
        {
            var cfId = grupo.Key;
            var cf = cot.Fornecedores.First(f => f.Id == cfId);
            qtdExistente++;
            var numeroPedido = $"PED-{ano}-{qtdExistente:D4}";

            var resp = cf.Resposta;
            var itensPedido = new List<PedidoCompraItem>();
            decimal subtotal = 0;
            decimal descontoTotal = 0;
            decimal impostosTotal = 0;

            foreach (var (itemId, _) in grupo)
            {
                var cotItem = cot.Itens.FirstOrDefault(i => i.Id == itemId);
                if (cotItem == null) continue;
                var o = cmp.Itens.FirstOrDefault(i => i.CotacaoItemId == itemId)?.Ofertas.FirstOrDefault(x => x.CotacaoFornecedorId == cfId);
                var respItem = resp?.Itens.FirstOrDefault(ri => ri.CotacaoItemId == itemId);

                var precoBruto = o?.PrecoUnitario ?? respItem?.PrecoUnitario ?? cotItem.UltimoCustoRef;
                var descPerc = respItem?.DescontoPerc ?? 0m;
                var precoLiq = o?.PrecoLiquidoUnit ?? (precoBruto * (1 - descPerc / 100m));
                var qtd = cotItem.Quantidade;
                var itemSubtotal = Math.Round(precoLiq * qtd, 2);

                subtotal += Math.Round(precoBruto * qtd, 2);
                descontoTotal += Math.Round((precoBruto - precoLiq) * qtd, 2);
                var stValor = (respItem?.IcmsStValor ?? (o?.St ?? 0m)) * qtd;
                impostosTotal += stValor;

                itensPedido.Add(new PedidoCompraItem
                {
                    TenantId = tenant.CurrentTenantId!.Value,
                    ProdutoId = cotItem.ProdutoId,
                    CodigoProduto = cotItem.Codigo,
                    DescricaoProduto = cotItem.Descricao,
                    Unidade = cotItem.Unidade,
                    Quantidade = qtd,
                    PrecoUnitario = precoBruto,
                    DescontoPerc = descPerc,
                    PrecoLiquidoUnit = precoLiq,
                    Subtotal = itemSubtotal,
                    Cst = o?.Cst ?? respItem?.Cst ?? "",
                    Cfop = o?.Cfop ?? respItem?.Cfop ?? "",
                    IcmsAliq = respItem?.IcmsAliq ?? (o?.Icms ?? 0m),
                    IpiAliq = respItem?.IpiAliq ?? (o?.Ipi ?? 0m),
                    Mva = respItem?.Mva ?? 0m,
                    IcmsStAliq = respItem?.IcmsStAliq ?? 0m,
                    IcmsStValor = respItem?.IcmsStValor ?? 0m,
                    CustoEfetivoUnit = o?.CustoEfetivoUnit ?? precoLiq,
                    CustoEfetivoTotal = Math.Round((o?.CustoEfetivoUnit ?? precoLiq) * qtd, 2)
                });
            }

            var frete = resp?.Frete ?? 0m;
            var outrasDesp = resp?.OutrasDespesas ?? 0m;
            var valorTotal = (subtotal - descontoTotal) + frete + outrasDesp + impostosTotal;

            var timeline = new[]
            {
                new { Data = DateTime.UtcNow, Status = "Criado", Descricao = $"Pedido gerado na aprovação da cotação {cot.Numero}", Usuario = nomeUsuario }
            };

            var pedido = new PedidoCompra
            {
                TenantId = tenant.CurrentTenantId!.Value,
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
                CondicaoPagamento = resp?.CondicaoPagamento ?? "A combinar",
                PrazoEntregaDias = resp?.PrazoEntregaDias ?? 3,
                PrevisaoEntrega = DateTime.UtcNow.AddDays(resp?.PrazoEntregaDias ?? 3),
                Observacoes = resp?.Observacoes ?? "",
                AprovadoPorId = tenant.CurrentUserId,
                AprovadoPorNome = nomeUsuario,
                AprovadoEm = DateTime.UtcNow,
                TimelineJson = System.Text.Json.JsonSerializer.Serialize(timeline),
                Itens = itensPedido
            };

            db.PedidosCompra.Add(pedido);
            pedidosCriados.Add(pedido);
        }

        audit.Add("CotacaoAprovada", "Cotacao", cot.Id.ToString(), new { cot.Numero, eco.TotalAprovado, eco.Potencial, eco.Realizada, escolhas = r.Escolhas, r.Observacao, PedidosGerados = pedidosCriados.Count });
        await db.SaveChangesAsync();
        return Ok(new { mensagem = "Cotação aprovada.", totalAprovado = eco.TotalAprovado, economiaPotencial = eco.Potencial, economiaRealizada = eco.Realizada, margemPerc = eco.MargemPerc, pedidosGerados = pedidosCriados.Select(p => new { p.Id, p.Numero, p.ValorTotal }) });
    }

    [HttpGet("{id:guid}/imprimir")]
    [HttpGet("{id:guid}/pdf")]
    [AllowAnonymous]
    public async Task<IActionResult> VisualizarImpressao(Guid id, [FromQuery] Guid? fornecedorId = null)
    {
        var c = await db.Cotacoes
            .IgnoreQueryFilters()
            .Include(x => x.Itens)
            .Include(x => x.Fornecedores)
                .ThenInclude(f => f.Fornecedor)
            .Include(x => x.Fornecedores)
                .ThenInclude(f => f.Resposta)
                    .ThenInclude(r => r!.Itens)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (c == null) return NotFound(new { erro = "Cotação não encontrada." });

        var tenantInfo = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == c.TenantId);
        Usuario? comprador = null;
        if (c.CriadaPorId.HasValue)
        {
            comprador = await db.Usuarios.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == c.CriadaPorId.Value);
        }

        CotacaoFornecedor? cf = null;
        if (fornecedorId.HasValue)
        {
            cf = c.Fornecedores.FirstOrDefault(f => f.FornecedorId == fornecedorId.Value);
        }
        else
        {
            cf = c.Fornecedores.FirstOrDefault(f => f.Status == "Aprovada") ?? c.Fornecedores.FirstOrDefault();
        }

        var html = Nexo.Api.Services.PdfDocumentService.GerarHtmlCotacao(c, tenantInfo, comprador, cf);
        return Content(html, "text/html; charset=utf-8");
    }
}
