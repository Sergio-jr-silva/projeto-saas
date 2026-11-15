using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.Models;
using Nexo.Api.Services;

namespace Nexo.Api.Controllers;

[ApiController]
[Route("api/portal-servico")]
[AllowAnonymous]
public class PortalServicoController : ControllerBase
{
    private readonly NexoDbContext _db;
    private readonly IAuditService _audit;

    public PortalServicoController(NexoDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet("{token}/validar")]
    public async Task<IActionResult> ValidarAcesso(string token)
    {
        var acesso = await _db.AcessosFornecedorServico
            .IgnoreQueryFilters()
            .Include(a => a.FornecedorServico)
                .ThenInclude(f => f!.CotacaoServico)
            .Include(a => a.FornecedorServico)
                .ThenInclude(f => f!.Fornecedor)
            .FirstOrDefaultAsync(a => a.Token == token);

        if (acesso == null || acesso.Revogado)
            return NotFound(new { erro = "Link de cotação inválido ou revogado." });

        if (acesso.ExpiraEm < DateTime.UtcNow)
            return BadRequest(new { erro = "O prazo limite para esta cotação de serviços expirou." });

        acesso.Visualizacoes++;
        if (acesso.PrimeiraVisualizacaoEm == null)
            acesso.PrimeiraVisualizacaoEm = DateTime.UtcNow;
        acesso.UltimaVisualizacaoEm = DateTime.UtcNow;

        if (acesso.FornecedorServico != null && acesso.FornecedorServico.Status == "Convidado")
        {
            acesso.FornecedorServico.Status = "Visualizado";
        }

        await _db.SaveChangesAsync();

        var cs = acesso.FornecedorServico?.CotacaoServico;
        var forn = acesso.FornecedorServico?.Fornecedor;

        return Ok(new
        {
            valido = true,
            fornecedorNome = forn?.RazaoSocial ?? "",
            cotacaoNumero = cs?.Numero ?? "",
            cotacaoTitulo = cs?.Titulo ?? "",
            categoria = cs?.Categoria ?? "",
            dataLimite = cs?.DataLimiteResposta,
            status = acesso.FornecedorServico?.Status ?? ""
        });
    }

    [HttpGet("{token}/detalhes")]
    public async Task<IActionResult> ObterDetalhes(string token)
    {
        var acesso = await _db.AcessosFornecedorServico
            .IgnoreQueryFilters()
            .Include(a => a.FornecedorServico)
                .ThenInclude(f => f!.Fornecedor)
            .Include(a => a.FornecedorServico)
                .ThenInclude(f => f!.CotacaoServico)
                    .ThenInclude(c => c!.Atividades)
            .Include(a => a.FornecedorServico)
                .ThenInclude(f => f!.Proposta)
                    .ThenInclude(p => p!.Etapas)
            .FirstOrDefaultAsync(a => a.Token == token);

        if (acesso == null || acesso.Revogado)
            return NotFound(new { erro = "Acesso não encontrado." });

        var fs = acesso.FornecedorServico;
        var cs = fs?.CotacaoServico;
        if (cs == null) return NotFound(new { erro = "Cotação não encontrada." });

        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == cs.TenantId);

        var proposta = fs?.Proposta;

        return Ok(new
        {
            Fornecedor = new
            {
                fs?.Fornecedor?.RazaoSocial,
                fs?.Fornecedor?.NomeFantasia,
                fs?.Fornecedor?.Cnpj
            },
            Cotacao = new
            {
                cs.Id,
                cs.Numero,
                cs.Titulo,
                cs.Categoria,
                cs.Prioridade,
                cs.DataAbertura,
                cs.DataLimiteResposta,
                cs.DataDesejadaInicio,
                cs.PrazoEstimadoDias,
                cs.NecessidadeMotivacao,
                cs.DescricaoDetalhada,
                cs.ObjetivoEsperado,
                cs.ResultadoEsperado,
                cs.AtendimentoPresencial,
                cs.Endereco,
                cs.NumeroLocal,
                cs.Complemento,
                cs.Bairro,
                cs.Cidade,
                cs.Uf,
                cs.PontoReferencia,
                cs.HorarioPermitido,
                cs.MateriaisInclusosExigidos,
                cs.EquipamentosExigidos,
                cs.GarantiaEsperadaMeses,
                cs.CondicoesEspeciais,
                cs.CertificacoesExigidas,
                StatusFornecedor = fs?.Status,
                EmpresaContratante = tenant?.NomeFantasia ?? tenant?.RazaoSocial ?? "Nexo Compras",
                Atividades = cs.Atividades.OrderBy(a => a.Ordem).Select(a => new
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
                    a.ResultadoEsperado
                })
            },
            Proposta = proposta == null ? null : new
            {
                proposta.Id,
                proposta.Status,
                proposta.ValorMaoDeObra,
                proposta.ValorMateriaisInclusos,
                proposta.ValorEquipamentos,
                proposta.Deslocamento,
                proposta.OutrasDespesas,
                proposta.Desconto,
                proposta.ImpostosTotal,
                proposta.ValorTotal,
                proposta.PrazoInicioDias,
                proposta.PrazoExecucaoDias,
                proposta.CondicoesPagamento,
                proposta.ValidadeProposta,
                proposta.GarantiaMeses,
                proposta.DescricaoSolucao,
                proposta.Metodologia,
                proposta.EquipeAlocada,
                proposta.EquipamentosFerramental,
                proposta.MateriaisInclusosTexto,
                proposta.MateriaisNaoInclusosTexto,
                proposta.CertificacoesEquipe,
                proposta.LimitacoesExclusoes,
                proposta.Observacoes,
                proposta.ContatoNome,
                proposta.ContatoTelefone,
                proposta.ContatoEmail,
                proposta.EnviadaEm,
                Etapas = proposta.Etapas.Select(e => new
                {
                    e.AtividadeServicoId,
                    e.ValorUnitario,
                    e.ValorTotal,
                    e.DetalhesExecucao,
                    e.Observacao
                })
            }
        });
    }

    [HttpPost("{token}/proposta")]
    public async Task<IActionResult> SalvarProposta(string token, [FromBody] SalvarPropostaServicoRequest r)
    {
        var acesso = await _db.AcessosFornecedorServico
            .IgnoreQueryFilters()
            .Include(a => a.FornecedorServico)
                .ThenInclude(f => f!.CotacaoServico)
            .Include(a => a.FornecedorServico)
                .ThenInclude(f => f!.Proposta)
                    .ThenInclude(p => p!.Etapas)
            .FirstOrDefaultAsync(a => a.Token == token);

        if (acesso == null || acesso.Revogado)
            return NotFound(new { erro = "Acesso inválido." });

        var fs = acesso.FornecedorServico;
        var cs = fs?.CotacaoServico;
        if (cs == null) return NotFound(new { erro = "Cotação não encontrada." });

        if (cs.DataLimiteResposta < DateTime.UtcNow)
            return BadRequest(new { erro = "O prazo limite para envio de propostas já encerrou." });

        var proposta = fs.Proposta;
        if (proposta == null)
        {
            proposta = new PropostaServico
            {
                TenantId = cs.TenantId,
                FornecedorServicoId = fs.Id
            };
            _db.PropostasServico.Add(proposta);
            fs.Proposta = proposta;
        }

        proposta.ValorMaoDeObra = r.ValorMaoDeObra;
        proposta.ValorMateriaisInclusos = r.ValorMateriaisInclusos;
        proposta.ValorEquipamentos = r.ValorEquipamentos;
        proposta.Deslocamento = r.Deslocamento;
        proposta.OutrasDespesas = r.OutrasDespesas;
        proposta.Desconto = r.Desconto;
        proposta.ImpostosTotal = r.ImpostosTotal;

        // Cálculo do valor total:
        // Se informado explicitamente ou calculado pelas etapas/parcelas
        var somaPartes = (r.ValorMaoDeObra + r.ValorMateriaisInclusos + r.ValorEquipamentos + r.Deslocamento + r.OutrasDespesas + r.ImpostosTotal) - r.Desconto;
        proposta.ValorTotal = r.ValorTotal > 0 ? r.ValorTotal : somaPartes;

        proposta.PrazoInicioDias = r.PrazoInicioDias > 0 ? r.PrazoInicioDias : 3;
        proposta.PrazoExecucaoDias = r.PrazoExecucaoDias > 0 ? r.PrazoExecucaoDias : 15;
        proposta.CondicoesPagamento = !string.IsNullOrWhiteSpace(r.CondicoesPagamento) ? r.CondicoesPagamento.Trim() : "30 dias";
        proposta.ValidadeProposta = r.ValidadeProposta;
        proposta.GarantiaMeses = r.GarantiaMeses > 0 ? r.GarantiaMeses : 12;

        proposta.DescricaoSolucao = r.DescricaoSolucao?.Trim() ?? "";
        proposta.Metodologia = r.Metodologia?.Trim() ?? "";
        proposta.EquipeAlocada = r.EquipeAlocada?.Trim() ?? "";
        proposta.EquipamentosFerramental = r.EquipamentosFerramental?.Trim() ?? "";
        proposta.MateriaisInclusosTexto = r.MateriaisInclusosTexto?.Trim() ?? "";
        proposta.MateriaisNaoInclusosTexto = r.MateriaisNaoInclusosTexto?.Trim() ?? "";
        proposta.CertificacoesEquipe = r.CertificacoesEquipe?.Trim() ?? "";
        proposta.LimitacoesExclusoes = r.LimitacoesExclusoes?.Trim() ?? "";
        proposta.Observacoes = r.Observacoes?.Trim() ?? "";
        proposta.ContatoNome = r.ContatoNome?.Trim() ?? "";
        proposta.ContatoTelefone = r.ContatoTelefone?.Trim() ?? "";
        proposta.ContatoEmail = r.ContatoEmail?.Trim() ?? "";

        // Atualizar etapas
        proposta.Etapas.Clear();
        if (r.Etapas != null)
        {
            foreach (var et in r.Etapas)
            {
                var etapa = new PropostaServicoEtapa
                {
                    TenantId = cs.TenantId,
                    PropostaServicoId = proposta.Id,
                    AtividadeServicoId = et.AtividadeServicoId,
                    ValorUnitario = et.ValorUnitario,
                    ValorTotal = et.ValorTotal > 0 ? et.ValorTotal : et.ValorUnitario,
                    DetalhesExecucao = et.DetalhesExecucao?.Trim() ?? "",
                    Observacao = et.Observacao?.Trim() ?? ""
                };
                _db.PropostaServicoEtapas.Add(etapa);
                proposta.Etapas.Add(etapa);
            }
        }

        if (r.FinalizarEnvio)
        {
            proposta.Status = "Enviada";
            proposta.EnviadaEm = DateTime.UtcNow;
            fs.Status = "PropostaEnviada";

            _audit.Add("PropostaServicoEnviada", "PropostaServico", proposta.Id.ToString(), new
            {
                Cotacao = cs.Numero,
                proposta.ValorTotal,
                proposta.PrazoExecucaoDias
            });
        }
        else
        {
            proposta.Status = "Rascunho";
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            mensagem = r.FinalizarEnvio ? "Proposta enviada com sucesso! A contratante foi notificada." : "Rascunho da proposta salvo com sucesso.",
            proposta.Status,
            proposta.ValorTotal
        });
    }

    [HttpPost("{token}/recusar")]
    public async Task<IActionResult> RecusarCotacao(string token, [FromBody] RecusarCotacaoServicoRequest r)
    {
        var acesso = await _db.AcessosFornecedorServico
            .IgnoreQueryFilters()
            .Include(a => a.FornecedorServico)
            .FirstOrDefaultAsync(a => a.Token == token);

        if (acesso == null || acesso.Revogado)
            return NotFound(new { erro = "Acesso inválido." });

        var fs = acesso.FornecedorServico;
        if (fs == null) return NotFound(new { erro = "Registro não encontrado." });

        fs.Status = "Recusado";
        fs.MotivoRecusa = r.Motivo?.Trim() ?? "Não informado pelo fornecedor.";
        fs.RecusadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new { mensagem = "Agradecemos o retorno. Sua recusa foi registrada." });
    }
}

public record SalvarPropostaServicoRequest(
    decimal ValorMaoDeObra,
    decimal ValorMateriaisInclusos,
    decimal ValorEquipamentos,
    decimal Deslocamento,
    decimal OutrasDespesas,
    decimal Desconto,
    decimal ImpostosTotal,
    decimal ValorTotal,
    int PrazoInicioDias,
    int PrazoExecucaoDias,
    string? CondicoesPagamento,
    DateOnly? ValidadeProposta,
    int GarantiaMeses,
    string? DescricaoSolucao,
    string? Metodologia,
    string? EquipeAlocada,
    string? EquipamentosFerramental,
    string? MateriaisInclusosTexto,
    string? MateriaisNaoInclusosTexto,
    string? CertificacoesEquipe,
    string? LimitacoesExclusoes,
    string? Observacoes,
    string? ContatoNome,
    string? ContatoTelefone,
    string? ContatoEmail,
    bool FinalizarEnvio,
    List<EtapaPropostaDto>? Etapas
);

public record EtapaPropostaDto(
    Guid AtividadeServicoId,
    decimal ValorUnitario,
    decimal ValorTotal,
    string? DetalhesExecucao,
    string? Observacao
);

public record RecusarCotacaoServicoRequest(string? Motivo);
