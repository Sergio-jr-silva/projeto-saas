using System.Security.Cryptography;
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
[Route("api/[controller]")]
[Authorize]
public class ServicosController : ControllerBase
{
    private readonly NexoDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;

    public ServicosController(NexoDbContext db, ITenantContext tenant, IAuditService audit)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? status = null,
        [FromQuery] string? categoria = null,
        [FromQuery] string? busca = null,
        [FromQuery] Guid? filialId = null)
    {
        var q = _db.CotacoesServico
            .Include(x => x.Atividades)
            .Include(x => x.Fornecedores)
                .ThenInclude(f => f.Fornecedor)
            .Include(x => x.Fornecedores)
                .ThenInclude(f => f.Proposta)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(x => x.Status == status);

        if (!string.IsNullOrWhiteSpace(categoria))
            q = q.Where(x => x.Categoria == categoria);

        if (filialId.HasValue)
            q = q.Where(x => x.FilialId == filialId.Value);

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var b = busca.Trim().ToLower();
            q = q.Where(x => x.Numero.ToLower().Contains(b) || x.Titulo.ToLower().Contains(b) || x.Categoria.ToLower().Contains(b));
        }

        var lista = await q.OrderByDescending(x => x.DataCriacao).ToListAsync();

        var dtos = lista.Select(c => new
        {
            c.Id,
            c.Numero,
            c.Titulo,
            c.Categoria,
            c.Prioridade,
            c.Status,
            c.DataAbertura,
            c.DataLimiteResposta,
            c.DataDesejadaInicio,
            c.PrazoEstimadoDias,
            c.ResponsavelNome,
            c.Cidade,
            c.Uf,
            QtdAtividades = c.Atividades.Count,
            QtdFornecedores = c.Fornecedores.Count,
            QtdPropostas = c.Fornecedores.Count(f => f.Proposta != null && f.Proposta.Status == "Enviada"),
            TotalEstimado = c.TotalEstimado > 0 ? c.TotalEstimado : c.Atividades.Sum(a => a.Quantidade * a.ValorEstimadoUnitario),
            c.TotalContratado,
            c.EconomiaRealizada,
            FornecedorVencedorNome = c.Fornecedores.FirstOrDefault(f => f.FornecedorId == c.FornecedorVencedorId)?.Fornecedor?.RazaoSocial
        });

        return Ok(dtos);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id)
    {
        var c = await _db.CotacoesServico
            .Include(x => x.Atividades)
            .Include(x => x.Fornecedores)
                .ThenInclude(f => f.Fornecedor)
            .Include(x => x.Fornecedores)
                .ThenInclude(f => f.Acesso)
            .Include(x => x.Fornecedores)
                .ThenInclude(f => f.Proposta)
                    .ThenInclude(p => p!.Etapas)
            .Include(x => x.Anexos)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (c == null) return NotFound(new { erro = "Cotação de serviço não encontrada." });

        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        return Ok(new
        {
            c.Id,
            c.Numero,
            c.Titulo,
            c.Categoria,
            c.Prioridade,
            c.Status,
            c.DataAbertura,
            c.DataLimiteResposta,
            c.DataDesejadaInicio,
            c.PrazoEstimadoDias,
            c.NecessidadeMotivacao,
            c.DescricaoDetalhada,
            c.ObjetivoEsperado,
            c.ResultadoEsperado,
            c.AtendimentoPresencial,
            c.Cep,
            c.Endereco,
            c.NumeroLocal,
            c.Complemento,
            c.Bairro,
            c.Cidade,
            c.Uf,
            c.PontoReferencia,
            c.HorarioPermitido,
            c.MateriaisInclusosExigidos,
            c.EquipamentosExigidos,
            c.GarantiaEsperadaMeses,
            c.CondicoesEspeciais,
            c.CertificacoesExigidas,
            c.PesoPreco,
            c.PesoPrazo,
            c.PesoTecnico,
            c.PesoCondicoes,
            c.TotalEstimado,
            c.TotalContratado,
            c.EconomiaRealizada,
            c.JustificativaEscolha,
            c.AprovadoPorNome,
            c.AprovadoEm,
            c.ObservacaoAprovacao,
            Atividades = c.Atividades.OrderBy(a => a.Ordem).Select(a => new
            {
                a.Id,
                a.Ordem,
                a.Etapa,
                a.Nome,
                a.Descricao,
                a.Quantidade,
                a.UnidadeMedicao,
                a.LocalExecucao,
                a.RequisitosTecnicos,
                a.ResultadoEsperado,
                a.ValorEstimadoUnitario,
                Subtotal = a.Quantidade * a.ValorEstimadoUnitario
            }),
            Fornecedores = c.Fornecedores.Select(f => new
            {
                f.Id,
                f.FornecedorId,
                f.Status,
                RazaoSocial = f.Fornecedor?.RazaoSocial ?? "",
                NomeFantasia = f.Fornecedor?.NomeFantasia ?? "",
                Cnpj = f.Fornecedor?.Cnpj ?? "",
                Email = f.Fornecedor?.Email ?? "",
                Telefone = f.Fornecedor?.Telefone ?? "",
                LinkAcesso = f.Acesso != null ? $"{baseUrl}/portal-servico/{f.Acesso.Token}" : "",
                CodigoHash = f.Acesso != null ? "Gerado com segurança" : "",
                TemProposta = f.Proposta != null,
                PropostaStatus = f.Proposta?.Status,
                ValorTotalProposta = f.Proposta?.ValorTotal ?? 0m,
                PrazoExecucaoDias = f.Proposta?.PrazoExecucaoDias ?? 0,
                PontuacaoPonderada = f.Proposta?.PontuacaoPonderada ?? 0m
            }),
            Anexos = c.Anexos.Select(a => new
            {
                a.Id,
                a.NomeArquivo,
                a.Tamanho,
                a.Descricao,
                a.ContentType
            })
        });
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarCotacaoServicoRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Titulo))
            return BadRequest(new { erro = "Título da cotação de serviços é obrigatório." });

        if (r.Atividades == null || r.Atividades.Count == 0)
            return BadRequest(new { erro = "Informe ao menos uma atividade ou etapa do serviço." });

        var tenantId = _tenant.CurrentTenantId!.Value;
        var anoAtual = DateTime.UtcNow.Year;
        var totalExistentes = await _db.CotacoesServico.IgnoreQueryFilters().CountAsync(c => c.TenantId == tenantId && c.DataCriacao.Year == anoAtual);
        var numeroFormatado = $"SRV-{anoAtual}-{(totalExistentes + 1):D4}";

        var nomeUsuario = _tenant.UserName ?? "Comprador";
        var timeline = new List<object>
        {
            new { Data = DateTime.UtcNow, Status = "Rascunho", Descricao = "Cotação de serviço criada", Usuario = nomeUsuario }
        };

        var totalEstimado = r.Atividades.Sum(a => a.Quantidade * a.ValorEstimadoUnitario);

        var cot = new CotacaoServico
        {
            TenantId = tenantId,
            Numero = numeroFormatado,
            Titulo = r.Titulo.Trim(),
            Categoria = !string.IsNullOrWhiteSpace(r.Categoria) ? r.Categoria.Trim() : "Geral",
            Prioridade = !string.IsNullOrWhiteSpace(r.Prioridade) ? r.Prioridade.Trim() : "Media",
            FilialId = r.FilialId,
            ResponsavelId = _tenant.CurrentUserId,
            ResponsavelNome = nomeUsuario,
            DataAbertura = DateTime.UtcNow,
            DataLimiteResposta = r.DataLimiteResposta > DateTime.UtcNow ? r.DataLimiteResposta : DateTime.UtcNow.AddDays(7),
            DataDesejadaInicio = r.DataDesejadaInicio,
            PrazoEstimadoDias = r.PrazoEstimadoDias > 0 ? r.PrazoEstimadoDias : 30,
            NecessidadeMotivacao = r.NecessidadeMotivacao?.Trim() ?? "",
            DescricaoDetalhada = r.DescricaoDetalhada?.Trim() ?? "",
            ObjetivoEsperado = r.ObjetivoEsperado?.Trim() ?? "",
            ResultadoEsperado = r.ResultadoEsperado?.Trim() ?? "",
            AtendimentoPresencial = r.AtendimentoPresencial,
            Cep = r.Cep?.Trim() ?? "",
            Endereco = r.Endereco?.Trim() ?? "",
            NumeroLocal = r.NumeroLocal?.Trim() ?? "",
            Complemento = r.Complemento?.Trim() ?? "",
            Bairro = r.Bairro?.Trim() ?? "",
            Cidade = r.Cidade?.Trim() ?? "",
            Uf = r.Uf?.Trim() ?? "",
            PontoReferencia = r.PontoReferencia?.Trim() ?? "",
            HorarioPermitido = !string.IsNullOrWhiteSpace(r.HorarioPermitido) ? r.HorarioPermitido : "Horário comercial padrão",
            MateriaisInclusosExigidos = r.MateriaisInclusosExigidos?.Trim() ?? "",
            EquipamentosExigidos = r.EquipamentosExigidos?.Trim() ?? "",
            GarantiaEsperadaMeses = r.GarantiaEsperadaMeses > 0 ? r.GarantiaEsperadaMeses : 12,
            CondicoesEspeciais = r.CondicoesEspeciais?.Trim() ?? "",
            CertificacoesExigidas = r.CertificacoesExigidas?.Trim() ?? "",
            PesoPreco = r.PesoPreco > 0 ? r.PesoPreco : 50m,
            PesoPrazo = r.PesoPrazo > 0 ? r.PesoPrazo : 20m,
            PesoTecnico = r.PesoTecnico > 0 ? r.PesoTecnico : 20m,
            PesoCondicoes = r.PesoCondicoes > 0 ? r.PesoCondicoes : 10m,
            Status = "Rascunho",
            TotalEstimado = totalEstimado,
            TimelineJson = JsonSerializer.Serialize(timeline)
        };

        // Adicionar Atividades
        int ordem = 1;
        foreach (var at in r.Atividades)
        {
            cot.Atividades.Add(new AtividadeServico
            {
                TenantId = tenantId,
                Ordem = ordem++,
                Etapa = at.Etapa?.Trim() ?? "",
                Nome = at.Nome.Trim(),
                Descricao = at.Descricao?.Trim() ?? "",
                Quantidade = at.Quantidade > 0 ? at.Quantidade : 1m,
                UnidadeMedicao = !string.IsNullOrWhiteSpace(at.UnidadeMedicao) ? at.UnidadeMedicao.Trim().ToUpper() : "SV",
                LocalExecucao = at.LocalExecucao?.Trim() ?? "",
                RequisitosTecnicos = at.RequisitosTecnicos?.Trim() ?? "",
                ResultadoEsperado = at.ResultadoEsperado?.Trim() ?? "",
                ValorEstimadoUnitario = at.ValorEstimadoUnitario
            });
        }

        // Adicionar Fornecedores Convidadores (se informados na criação)
        if (r.FornecedoresIds != null)
        {
            foreach (var fornId in r.FornecedoresIds.Distinct())
            {
                var tokenBytes = RandomNumberGenerator.GetBytes(32);
                var token = Convert.ToHexString(tokenBytes).ToLower();
                var codigo = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
                var hash = BCrypt.Net.BCrypt.HashPassword(codigo);

                cot.Fornecedores.Add(new FornecedorServico
                {
                    TenantId = tenantId,
                    FornecedorId = fornId,
                    Status = "Convidado",
                    Acesso = new AcessoFornecedorServico
                    {
                        TenantId = tenantId,
                        Token = token,
                        CodigoHash = hash,
                        ExpiraEm = cot.DataLimiteResposta.AddDays(1)
                    }
                });
            }
        }

        _db.CotacoesServico.Add(cot);
        await _db.SaveChangesAsync();

        _audit.Add("CotacaoServicoCriada", "CotacaoServico", cot.Id.ToString(), new { cot.Numero, cot.Titulo, totalEstimado });

        return CreatedAtAction(nameof(ObterPorId), new { id = cot.Id }, new { cot.Id, cot.Numero, mensagem = "Cotação de serviço criada com sucesso." });
    }

    [HttpPost("{id:guid}/enviar")]
    public async Task<IActionResult> EnviarParaFornecedores(Guid id)
    {
        var c = await _db.CotacoesServico
            .Include(x => x.Fornecedores)
                .ThenInclude(f => f.Acesso)
            .Include(x => x.Fornecedores)
                .ThenInclude(f => f.Fornecedor)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (c == null) return NotFound(new { erro = "Cotação de serviços não encontrada." });
        if (c.Fornecedores.Count == 0) return BadRequest(new { erro = "Adicione ao menos um fornecedor para enviar a cotação." });

        foreach (var forn in c.Fornecedores)
        {
            if (forn.Acesso == null)
            {
                var tokenBytes = RandomNumberGenerator.GetBytes(32);
                var token = Convert.ToHexString(tokenBytes).ToLower();

                var codigo = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
                var hash = BCrypt.Net.BCrypt.HashPassword(codigo);

                var acesso = new AcessoFornecedorServico
                {
                    TenantId = c.TenantId,
                    FornecedorServicoId = forn.Id,
                    Token = token,
                    CodigoHash = hash,
                    ExpiraEm = c.DataLimiteResposta.AddDays(1)
                };
                _db.AcessosFornecedorServico.Add(acesso);
                forn.Acesso = acesso;
            }
            forn.Status = "Convidado";
        }

        c.Status = "AguardandoPropostas";
        await _db.SaveChangesAsync();

        _audit.Add("CotacaoServicoEnviada", "CotacaoServico", c.Id.ToString(), new { c.Numero, TotalFornecedores = c.Fornecedores.Count });

        return Ok(new { mensagem = "Cotação enviada aos fornecedores com sucesso.", status = c.Status });
    }

    [HttpPost("{id:guid}/fornecedores")]
    public async Task<IActionResult> AdicionarFornecedor(Guid id, [FromBody] AdicionarFornecedorServicoRequest r)
    {
        var c = await _db.CotacoesServico.Include(x => x.Fornecedores).FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound(new { erro = "Cotação não encontrada." });

        var forn = await _db.Fornecedores.FirstOrDefaultAsync(f => f.Id == r.FornecedorId);
        if (forn == null) return NotFound(new { erro = "Fornecedor não encontrado." });

        if (c.Fornecedores.Any(f => f.FornecedorId == r.FornecedorId))
            return BadRequest(new { erro = "Este fornecedor já está adicionado à cotação." });

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToHexString(tokenBytes).ToLower();
        var codigo = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var hash = BCrypt.Net.BCrypt.HashPassword(codigo);

        var fornServico = new FornecedorServico
        {
            TenantId = c.TenantId,
            CotacaoServicoId = c.Id,
            FornecedorId = r.FornecedorId,
            Status = "Convidado",
            Acesso = new AcessoFornecedorServico
            {
                TenantId = c.TenantId,
                Token = token,
                CodigoHash = hash,
                ExpiraEm = c.DataLimiteResposta.AddDays(1)
            }
        };

        _db.FornecedoresServico.Add(fornServico);
        await _db.SaveChangesAsync();

        return Ok(new { mensagem = "Fornecedor adicionado com sucesso.", fornServico.Id, Token = fornServico.Acesso.Token });
    }

    [HttpGet("{id:guid}/comparacao")]
    public async Task<IActionResult> ObterComparacao(Guid id)
    {
        var c = await _db.CotacoesServico
            .Include(x => x.Atividades)
            .Include(x => x.Fornecedores)
                .ThenInclude(f => f.Fornecedor)
            .Include(x => x.Fornecedores)
                .ThenInclude(f => f.Proposta)
                    .ThenInclude(p => p!.Etapas)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (c == null) return NotFound(new { erro = "Cotação de serviço não encontrada." });

        var propostasEnviadas = c.Fornecedores
            .Where(f => f.Proposta != null && (f.Proposta.Status == "Enviada" || f.Proposta.Status == "Vencedora"))
            .Select(f => f.Proposta!)
            .ToList();

        if (propostasEnviadas.Count == 0)
        {
            return Ok(new
            {
                c.Id,
                c.Numero,
                c.Titulo,
                c.Status,
                TemPropostas = false,
                Pesos = new { c.PesoPreco, c.PesoPrazo, c.PesoTecnico, c.PesoCondicoes },
                Comparativo = new List<object>()
            });
        }

        // Determinar melhor preço e menor prazo para cálculo de score relativo
        var minPreco = propostasEnviadas.Min(p => p.ValorTotal);
        var minPrazo = propostasEnviadas.Min(p => p.PrazoExecucaoDias);
        if (minPrazo <= 0) minPrazo = 1;

        var matriz = propostasEnviadas.Select(p =>
        {
            var f = c.Fornecedores.First(x => x.Id == p.FornecedorServicoId);

            // Cálculo automatizado do score comercial (preço mais baixo = 100)
            decimal scoreComercial = 100m;
            if (p.ValorTotal > 0 && minPreco > 0)
            {
                scoreComercial = Math.Round((minPreco / p.ValorTotal) * 100m, 2);
            }

            // Cálculo automatizado do score de prazo (menor prazo = 100)
            decimal scorePrazo = 100m;
            if (p.PrazoExecucaoDias > 0 && minPrazo > 0)
            {
                scorePrazo = Math.Round(((decimal)minPrazo / p.PrazoExecucaoDias) * 100m, 2);
            }

            // Se o comprador já lançou notas manuais específicas, preserva ou mescla
            var notaTecnica = p.NotaTecnica > 0 ? p.NotaTecnica : 85m;
            var notaCondicoes = p.NotaCondicoes > 0 ? p.NotaCondicoes : 80m;

            // Pontuação ponderada:
            // (Preco * PesoPreco + Prazo * PesoPrazo + Tecnica * PesoTecnico + Cond * PesoCond) / 100
            var pontuacao = Math.Round(
                (scoreComercial * (c.PesoPreco / 100m)) +
                (scorePrazo * (c.PesoPrazo / 100m)) +
                (notaTecnica * (c.PesoTecnico / 100m)) +
                (notaCondicoes * (c.PesoCondicoes / 100m)),
                2
            );

            // Atualiza no banco
            p.NotaComercial = scoreComercial;
            p.NotaPrazo = scorePrazo;
            p.PontuacaoPonderada = pontuacao;

            return new
            {
                PropostaId = p.Id,
                FornecedorServicoId = f.Id,
                FornecedorId = f.FornecedorId,
                RazaoSocial = f.Fornecedor?.RazaoSocial ?? "",
                NomeFantasia = f.Fornecedor?.NomeFantasia ?? "",
                Cnpj = f.Fornecedor?.Cnpj ?? "",
                p.ValorTotal,
                p.ValorMaoDeObra,
                p.ValorMateriaisInclusos,
                p.ValorEquipamentos,
                p.Deslocamento,
                p.PrazoInicioDias,
                p.PrazoExecucaoDias,
                p.CondicoesPagamento,
                p.GarantiaMeses,
                p.DescricaoSolucao,
                p.Metodologia,
                p.EquipeAlocada,
                p.MateriaisInclusosTexto,
                p.MateriaisNaoInclusosTexto,
                p.CertificacoesEquipe,
                ScoreComercial = scoreComercial,
                ScorePrazo = scorePrazo,
                ScoreTecnico = notaTecnica,
                ScoreCondicoes = notaCondicoes,
                PontuacaoPonderada = pontuacao,
                p.ParecerAvaliacao,
                IsVencedora = c.PropostaVencedoraId == p.Id,
                Etapas = p.Etapas.Select(e => new
                {
                    e.AtividadeServicoId,
                    e.ValorTotal,
                    e.DetalhesExecucao
                })
            };
        }).OrderByDescending(x => x.PontuacaoPonderada).ToList();

        _db.SaveChanges();

        return Ok(new
        {
            c.Id,
            c.Numero,
            c.Titulo,
            c.Status,
            TemPropostas = true,
            Pesos = new { c.PesoPreco, c.PesoPrazo, c.PesoTecnico, c.PesoCondicoes },
            Atividades = c.Atividades.OrderBy(a => a.Ordem).Select(a => new { a.Id, a.Ordem, a.Nome, a.Quantidade, a.UnidadeMedicao }),
            Propostas = matriz
        });
    }

    [HttpPost("{id:guid}/avaliar-proposta/{propostaId:guid}")]
    public async Task<IActionResult> AvaliarProposta(Guid id, Guid propostaId, [FromBody] AvaliarPropostaRequest r)
    {
        var p = await _db.PropostasServico
            .Include(x => x.FornecedorServico)
            .FirstOrDefaultAsync(x => x.Id == propostaId && x.FornecedorServico!.CotacaoServicoId == id);

        if (p == null) return NotFound(new { erro = "Proposta não encontrada nesta cotação." });

        if (r.NotaTecnica.HasValue) p.NotaTecnica = Math.Clamp(r.NotaTecnica.Value, 0, 100);
        if (r.NotaCondicoes.HasValue) p.NotaCondicoes = Math.Clamp(r.NotaCondicoes.Value, 0, 100);
        if (!string.IsNullOrWhiteSpace(r.ParecerAvaliacao)) p.ParecerAvaliacao = r.ParecerAvaliacao.Trim();

        var cot = await _db.CotacoesServico.FindAsync(id);
        if (cot != null)
        {
            p.PontuacaoPonderada = Math.Round(
                (p.NotaComercial * (cot.PesoPreco / 100m)) +
                (p.NotaPrazo * (cot.PesoPrazo / 100m)) +
                (p.NotaTecnica * (cot.PesoTecnico / 100m)) +
                (p.NotaCondicoes * (cot.PesoCondicoes / 100m)),
                2
            );
        }

        await _db.SaveChangesAsync();
        return Ok(new { mensagem = "Avaliação da proposta registrada.", p.PontuacaoPonderada });
    }

    [HttpPost("{id:guid}/aprovar")]
    public async Task<IActionResult> AprovarCotacao(Guid id, [FromBody] AprovarCotacaoServicoRequest r)
    {
        var c = await _db.CotacoesServico
            .Include(x => x.Atividades)
            .Include(x => x.Fornecedores)
                .ThenInclude(f => f.Proposta)
                    .ThenInclude(p => p!.Etapas)
            .Include(x => x.Fornecedores)
                .ThenInclude(f => f.Fornecedor)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (c == null) return NotFound(new { erro = "Cotação de serviço não encontrada." });

        var fs = c.Fornecedores.FirstOrDefault(f => f.Id == r.FornecedorServicoId || f.FornecedorId == r.FornecedorId);
        if (fs == null || fs.Proposta == null)
            return BadRequest(new { erro = "Proposta selecionada não é válida para esta cotação." });

        var proposta = fs.Proposta;
        var nomeUsuario = _tenant.UserName ?? "Comprador";

        // Marcar proposta e fornecedor vencedor
        c.PropostaVencedoraId = proposta.Id;
        c.FornecedorVencedorId = fs.FornecedorId;
        c.TotalContratado = proposta.ValorTotal;
        c.EconomiaRealizada = c.TotalEstimado > proposta.ValorTotal ? (c.TotalEstimado - proposta.ValorTotal) : 0m;
        c.JustificativaEscolha = !string.IsNullOrWhiteSpace(r.Justificativa) ? r.Justificativa.Trim() : "Melhor proposta técnica e comercial conforme pontuação ponderada.";
        c.AprovadoPorId = _tenant.CurrentUserId;
        c.AprovadoPorNome = nomeUsuario;
        c.AprovadoEm = DateTime.UtcNow;
        c.ObservacaoAprovacao = r.Observacao?.Trim() ?? "";
        c.Status = "Contratada";

        foreach (var f in c.Fornecedores)
        {
            if (f.Id == fs.Id)
            {
                f.Status = "Selecionado";
                if (f.Proposta != null) f.Proposta.Status = "Vencedora";
            }
            else
            {
                f.Status = "NaoSelecionado";
                if (f.Proposta != null) f.Proposta.Status = "NaoSelecionada";
            }
        }

        // Criar Ordem de Serviço Oficial
        var anoAtual = DateTime.UtcNow.Year;
        var totalOrdensAno = await _db.OrdensServico.IgnoreQueryFilters().CountAsync(o => o.TenantId == c.TenantId && o.DataCriacao.Year == anoAtual);
        var numeroOs = $"OS-{anoAtual}-{(totalOrdensAno + 1):D4}";

        var timelineOs = new List<object>
        {
            new { Data = DateTime.UtcNow, Status = "Criada", Descricao = $"Ordem de Serviço gerada a partir da cotação {c.Numero}", Usuario = nomeUsuario }
        };

        var os = new OrdemServico
        {
            TenantId = c.TenantId,
            Numero = numeroOs,
            CotacaoServicoId = c.Id,
            PropostaServicoId = proposta.Id,
            FornecedorId = fs.FornecedorId,
            FilialId = c.FilialId,
            Status = "Criada",
            ValorTotal = proposta.ValorTotal,
            ValorMaoDeObra = proposta.ValorMaoDeObra,
            ValorMateriais = proposta.ValorMateriaisInclusos,
            Deslocamento = proposta.Deslocamento,
            ImpostosTotal = proposta.ImpostosTotal,
            CondicaoPagamento = proposta.CondicoesPagamento,
            PrazoExecucaoDias = proposta.PrazoExecucaoDias,
            PrevisaoInicio = DateTime.UtcNow.AddDays(proposta.PrazoInicioDias),
            PrevisaoTermino = DateTime.UtcNow.AddDays(proposta.PrazoInicioDias + proposta.PrazoExecucaoDias),
            GarantiaMeses = proposta.GarantiaMeses,
            EscopoAprovadoResumo = $"{c.Titulo} - {c.DescricaoDetalhada}",
            ClausulasEspeciais = c.CondicoesEspeciais,
            Observacoes = c.JustificativaEscolha,
            AprovadoPorId = _tenant.CurrentUserId,
            AprovadoPorNome = nomeUsuario,
            AprovadoEm = DateTime.UtcNow,
            TimelineJson = JsonSerializer.Serialize(timelineOs)
        };

        _db.OrdensServico.Add(os);
        await _db.SaveChangesAsync();

        _audit.Add("CotacaoServicoAprovada", "CotacaoServico", c.Id.ToString(), new { CotacaoNumero = c.Numero, OrdemNumero = os.Numero, c.TotalContratado, Fornecedor = fs.Fornecedor?.RazaoSocial });

        return Ok(new
        {
            mensagem = "Cotação aprovada e Ordem de Serviço gerada com sucesso.",
            c.Numero,
            OrdemServicoId = os.Id,
            OrdemServicoNumero = os.Numero,
            c.TotalContratado,
            c.EconomiaRealizada
        });
    }

    [HttpGet("ordens")]
    public async Task<IActionResult> ListarOrdens([FromQuery] string? status = null)
    {
        var q = _db.OrdensServico
            .Include(x => x.Fornecedor)
            .Include(x => x.CotacaoServico)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(x => x.Status == status);

        var ordens = await q.OrderByDescending(x => x.DataCriacao).ToListAsync();

        return Ok(ordens.Select(o => new
        {
            o.Id,
            o.Numero,
            CotacaoNumero = o.CotacaoServico?.Numero ?? "",
            CotacaoTitulo = o.CotacaoServico?.Titulo ?? "",
            FornecedorRazaoSocial = o.Fornecedor?.RazaoSocial ?? "",
            o.Status,
            o.ValorTotal,
            o.PrazoExecucaoDias,
            o.PrevisaoInicio,
            o.PrevisaoTermino,
            o.GarantiaMeses,
            o.AprovadoPorNome,
            o.DataCriacao
        }));
    }

    [HttpGet("ordens/{ordemId:guid}")]
    public async Task<IActionResult> ObterOrdemPorId(Guid ordemId)
    {
        var o = await _db.OrdensServico
            .Include(x => x.Fornecedor)
            .Include(x => x.CotacaoServico)
                .ThenInclude(c => c!.Atividades)
            .Include(x => x.PropostaServico)
                .ThenInclude(p => p!.Etapas)
            .FirstOrDefaultAsync(x => x.Id == ordemId);

        if (o == null) return NotFound(new { erro = "Ordem de serviço não encontrada." });

        return Ok(o);
    }

    [HttpPut("ordens/{ordemId:guid}/status")]
    public async Task<IActionResult> AtualizarStatusOrdem(Guid ordemId, [FromBody] AtualizarStatusOrdemRequest r)
    {
        var o = await _db.OrdensServico.FirstOrDefaultAsync(x => x.Id == ordemId);
        if (o == null) return NotFound(new { erro = "Ordem de serviço não encontrada." });

        var statusValidos = new[] { "Criada", "AguardandoAssinatura", "EmExecucao", "Concluida", "Cancelada" };
        if (!statusValidos.Contains(r.NovoStatus))
            return BadRequest(new { erro = $"Status inválido. Permitidos: {string.Join(", ", statusValidos)}" });

        o.Status = r.NovoStatus;

        var timeline = JsonSerializer.Deserialize<List<object>>(o.TimelineJson) ?? new();
        timeline.Add(new { Data = DateTime.UtcNow, Status = r.NovoStatus, Descricao = r.Descricao ?? $"Status alterado para {r.NovoStatus}", Usuario = _tenant.UserName ?? "Comprador" });
        o.TimelineJson = JsonSerializer.Serialize(timeline);

        await _db.SaveChangesAsync();
        return Ok(new { mensagem = "Status da Ordem de Serviço atualizado.", o.Status });
    }

    [HttpGet("{id:guid}/imprimir")]
    [HttpGet("{id:guid}/pdf")]
    [AllowAnonymous]
    public async Task<IActionResult> ImprimirCotacaoServico(Guid id)
    {
        var cs = await _db.CotacoesServico
            .IgnoreQueryFilters()
            .Include(x => x.Atividades)
            .Include(x => x.Fornecedores)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (cs == null) return NotFound(new { erro = "Cotação de serviço não encontrada." });

        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == cs.TenantId);
        Usuario? comprador = null;
        if (cs.ResponsavelId.HasValue)
        {
            comprador = await _db.Usuarios.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == cs.ResponsavelId.Value);
        }

        var html = PdfDocumentService.GerarHtmlCotacaoServico(cs, tenant, comprador);
        return Content(html, "text/html; charset=utf-8");
    }

    [HttpGet("ordens/{ordemId:guid}/imprimir")]
    [HttpGet("ordens/{ordemId:guid}/pdf")]
    [AllowAnonymous]
    public async Task<IActionResult> ImprimirOrdemServico(Guid ordemId)
    {
        var os = await _db.OrdensServico
            .IgnoreQueryFilters()
            .Include(x => x.Fornecedor)
            .Include(x => x.CotacaoServico)
            .Include(x => x.PropostaServico)
                .ThenInclude(p => p!.Etapas)
                    .ThenInclude(e => e.AtividadeServico)
            .FirstOrDefaultAsync(x => x.Id == ordemId);

        if (os == null) return NotFound(new { erro = "Ordem de serviço não encontrada." });

        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == os.TenantId);
        Usuario? comprador = null;
        if (os.AprovadoPorId.HasValue)
        {
            comprador = await _db.Usuarios.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == os.AprovadoPorId.Value);
        }

        var html = PdfDocumentService.GerarHtmlOrdemServico(os, tenant, comprador);
        return Content(html, "text/html; charset=utf-8");
    }
}

// Request DTOs
public record CriarCotacaoServicoRequest(
    string Titulo,
    string? Categoria,
    string? Prioridade,
    Guid? FilialId,
    DateTime DataLimiteResposta,
    DateTime? DataDesejadaInicio,
    int PrazoEstimadoDias,
    string? NecessidadeMotivacao,
    string? DescricaoDetalhada,
    string? ObjetivoEsperado,
    string? ResultadoEsperado,
    bool AtendimentoPresencial,
    string? Cep,
    string? Endereco,
    string? NumeroLocal,
    string? Complemento,
    string? Bairro,
    string? Cidade,
    string? Uf,
    string? PontoReferencia,
    string? HorarioPermitido,
    string? MateriaisInclusosExigidos,
    string? EquipamentosExigidos,
    int GarantiaEsperadaMeses,
    string? CondicoesEspeciais,
    string? CertificacoesExigidas,
    decimal PesoPreco,
    decimal PesoPrazo,
    decimal PesoTecnico,
    decimal PesoCondicoes,
    List<AtividadeServicoDto> Atividades,
    List<Guid>? FornecedoresIds
);

public record AtividadeServicoDto(
    string? Etapa,
    string Nome,
    string? Descricao,
    decimal Quantidade,
    string? UnidadeMedicao,
    string? LocalExecucao,
    string? RequisitosTecnicos,
    string? ResultadoEsperado,
    decimal ValorEstimadoUnitario
);

public record AdicionarFornecedorServicoRequest(Guid FornecedorId);
public record AvaliarPropostaRequest(decimal? NotaTecnica, decimal? NotaCondicoes, string? ParecerAvaliacao);
public record AprovarCotacaoServicoRequest(Guid? FornecedorServicoId, Guid? FornecedorId, string? Justificativa, string? Observacao);
public record AtualizarStatusOrdemRequest(string NovoStatus, string? Descricao);
