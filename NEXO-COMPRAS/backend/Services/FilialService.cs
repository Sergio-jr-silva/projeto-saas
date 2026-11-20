using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.DTOs;
using Nexo.Api.Models;
using Nexo.Api.Security;

namespace Nexo.Api.Services;

public interface IFilialService
{
    Task<List<FilialDto>> ListarFiliaisAsync(Guid tenantId);
    Task<FilialDto?> ObterFilialPorIdAsync(Guid tenantId, Guid filialId);
    Task<(FilialDto? Filial, string? Erro)> SalvarFilialAsync(Guid tenantId, Guid? filialId, SalvarFilialRequest req);
    Task<bool> DesativarFilialAsync(Guid tenantId, Guid filialId);
    Task<List<EstoqueFilialDto>> ObterEstoqueFilialAsync(Guid tenantId, Guid filialId);
    Task<bool> AtualizarEstoqueFilialAsync(Guid tenantId, Guid filialId, AtualizarEstoqueFilialRequest req);
    Task<RelatorioConsolidadoFiliaisDto> ObterRelatorioConsolidadoAsync(Guid tenantId);
    Task<(SolicitacaoTransferenciaDto? Transferencia, string? Erro)> CriarTransferenciaAsync(Guid tenantId, Guid usuarioId, string usuarioNome, CriarTransferenciaRequest req);
    Task<List<SolicitacaoTransferenciaDto>> ListarTransferenciasAsync(Guid tenantId);
    Task<SolicitacaoTransferenciaDto?> AtualizarStatusTransferenciaAsync(Guid tenantId, Guid transferenciaId, Guid usuarioId, string usuarioNome, AtualizarStatusTransferenciaRequest req);
    Task<(string? NovoToken, string? Erro)> RenovarTokenAsync(Guid tenantId, Guid filialId);
    Task<bool> RevogarTokenAsync(Guid tenantId, Guid filialId);
}

public class FilialService(NexoDbContext db, IAssinaturaService assinaturaService) : IFilialService
{
    public async Task<List<FilialDto>> ListarFiliaisAsync(Guid tenantId)
    {
        var filiais = await db.Filiais.Include(f => f.Agente)
            .Where(f => f.TenantId == tenantId && f.Ativa)
            .OrderBy(f => f.Codigo)
            .ToListAsync();

        return filiais.Select(ToFilialDto).ToList();
    }

    public async Task<FilialDto?> ObterFilialPorIdAsync(Guid tenantId, Guid filialId)
    {
        var filial = await db.Filiais.Include(f => f.Agente)
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Id == filialId && f.Ativa);

        return filial == null ? null : ToFilialDto(filial);
    }

    public async Task<(FilialDto? Filial, string? Erro)> SalvarFilialAsync(Guid tenantId, Guid? filialId, SalvarFilialRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Nome) || string.IsNullOrWhiteSpace(req.Codigo))
            return (null, "Nome e Código da filial são obrigatórios.");

        Filial? filial = null;
        if (filialId.HasValue && filialId.Value != Guid.Empty)
        {
            filial = await db.Filiais.FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Id == filialId.Value);
            if (filial == null) return (null, "Filial não encontrada.");
        }
        else
        {
            // Valida limite de filiais do plano contratado (Fase 8 e 9)
            var (permitido, msgErro) = await assinaturaService.ValidarLimiteAsync(tenantId, "Filial");
            if (!permitido) return (null, msgErro);

            filial = new Filial { TenantId = tenantId };
            db.Filiais.Add(filial);
        }

        filial.Codigo = req.Codigo.Trim().ToUpperInvariant();
        filial.Nome = req.Nome.Trim();
        filial.Cnpj = CnpjValidator.Clean(req.Cnpj);
        filial.Endereco = req.Endereco?.Trim() ?? string.Empty;
        filial.Cidade = req.Cidade?.Trim() ?? string.Empty;
        filial.Uf = req.Uf?.Trim().ToUpperInvariant() ?? string.Empty;
        filial.Telefone = req.Telefone?.Trim() ?? string.Empty;
        filial.Email = req.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        filial.Responsavel = req.Responsavel?.Trim() ?? string.Empty;
        filial.AgenteId = req.AgenteId;
        filial.Ativa = true;

        if (string.IsNullOrWhiteSpace(filial.Token))
        {
            filial.Token = $"FLK-{Guid.NewGuid():N}"[..24].ToUpperInvariant();
            filial.TokenGeradoEm = DateTime.UtcNow;
            filial.TokenRevogado = false;
        }

        await db.SaveChangesAsync();

        var carregada = await db.Filiais.IgnoreQueryFilters().Include(f => f.Agente).FirstAsync(f => f.Id == filial.Id);
        return (ToFilialDto(carregada), null);
    }

    public async Task<(string? NovoToken, string? Erro)> RenovarTokenAsync(Guid tenantId, Guid filialId)
    {
        var filial = await db.Filiais.FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Id == filialId && f.Ativa);
        if (filial == null) return (null, "Filial não encontrada.");

        filial.Token = $"FLK-{Guid.NewGuid():N}"[..24].ToUpperInvariant();
        filial.TokenGeradoEm = DateTime.UtcNow;
        filial.TokenRevogado = false;
        await db.SaveChangesAsync();
        return (filial.Token, null);
    }

    public async Task<bool> RevogarTokenAsync(Guid tenantId, Guid filialId)
    {
        var filial = await db.Filiais.FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Id == filialId);
        if (filial == null) return false;

        filial.TokenRevogado = true;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DesativarFilialAsync(Guid tenantId, Guid filialId)
    {
        var filial = await db.Filiais.FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Id == filialId);
        if (filial == null) return false;

        filial.Ativa = false;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<List<EstoqueFilialDto>> ObterEstoqueFilialAsync(Guid tenantId, Guid filialId)
    {
        var estoques = await db.EstoquesFilial.Include(e => e.Produto)
            .Where(e => e.TenantId == tenantId && e.FilialId == filialId)
            .OrderBy(e => e.Produto!.Descricao)
            .ToListAsync();

        return estoques.Select(e => new EstoqueFilialDto(
            ProdutoId: e.ProdutoId,
            CodigoErp: e.Produto?.CodigoErp ?? string.Empty,
            Descricao: e.Produto?.Descricao ?? "Produto",
            Unidade: e.Produto?.Unidade ?? "UN",
            EstoqueAtual: e.EstoqueAtual,
            EstoqueMinimo: e.EstoqueMinimo,
            EstoquePendente: e.EstoquePendente,
            CustoAtual: e.Produto?.CustoAtual ?? 0,
            PrecoVenda: e.Produto?.PrecoVenda ?? 0,
            UltimaAtualizacao: e.UltimaAtualizacao
        )).ToList();
    }

    public async Task<bool> AtualizarEstoqueFilialAsync(Guid tenantId, Guid filialId, AtualizarEstoqueFilialRequest req)
    {
        if (req.Itens == null || req.Itens.Count == 0) return true;

        var prodIds = req.Itens.Select(i => i.ProdutoId).ToList();
        var existentes = await db.EstoquesFilial
            .Where(e => e.TenantId == tenantId && e.FilialId == filialId && prodIds.Contains(e.ProdutoId))
            .ToDictionaryAsync(e => e.ProdutoId);

        var agora = DateTime.UtcNow;
        foreach (var item in req.Itens)
        {
            if (existentes.TryGetValue(item.ProdutoId, out var ef))
            {
                ef.EstoqueAtual = item.EstoqueAtual;
                if (item.EstoqueMinimo.HasValue) ef.EstoqueMinimo = item.EstoqueMinimo.Value;
                if (item.EstoquePendente.HasValue) ef.EstoquePendente = item.EstoquePendente.Value;
                ef.UltimaAtualizacao = agora;
            }
            else
            {
                db.EstoquesFilial.Add(new EstoqueFilial
                {
                    TenantId = tenantId,
                    FilialId = filialId,
                    ProdutoId = item.ProdutoId,
                    EstoqueAtual = item.EstoqueAtual,
                    EstoqueMinimo = item.EstoqueMinimo ?? 0,
                    EstoquePendente = item.EstoquePendente ?? 0,
                    UltimaAtualizacao = agora
                });
            }
        }

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<RelatorioConsolidadoFiliaisDto> ObterRelatorioConsolidadoAsync(Guid tenantId)
    {
        var filiais = await db.Filiais.Include(f => f.Agente)
            .Where(f => f.TenantId == tenantId && f.Ativa)
            .ToListAsync();

        var estoques = await db.EstoquesFilial.Include(e => e.Produto)
            .Where(e => e.TenantId == tenantId)
            .ToListAsync();

        var resumos = new List<FilialResumoDto>();
        foreach (var f in filiais)
        {
            var estoquesDaFilial = estoques.Where(e => e.FilialId == f.Id).ToList();
            var totalItens = estoquesDaFilial.Count;
            var somaQtd = estoquesDaFilial.Sum(e => e.EstoqueAtual);
            var valorTotal = estoquesDaFilial.Sum(e => e.EstoqueAtual * (e.Produto?.CustoAtual ?? 0));
            var statusSync = f.Agente != null && f.Agente.UltimoHeartbeat.HasValue && (DateTime.UtcNow - f.Agente.UltimoHeartbeat.Value).TotalMinutes < 10
                ? "Sincronizado"
                : "Aguardando";

            resumos.Add(new FilialResumoDto(
                FilialId: f.Id,
                Nome: f.Nome,
                Codigo: f.Codigo,
                Cidade: f.Cidade,
                Uf: f.Uf,
                TotalItensEstoque: totalItens,
                EstoqueFisicoTotal: somaQtd,
                ValorEstoqueEstimado: valorTotal,
                StatusSincronizacao: statusSync
            ));
        }

        var totalProdutos = await db.Produtos.CountAsync(p => p.TenantId == tenantId && p.Ativo);
        var valorTotalRede = resumos.Sum(r => r.ValorEstoqueEstimado);

        return new RelatorioConsolidadoFiliaisDto(
            TotalFiliais: filiais.Count,
            TotalProdutos: totalProdutos,
            ValorEstoqueTotal: valorTotalRede,
            Filiais: resumos
        );
    }

    public async Task<(SolicitacaoTransferenciaDto? Transferencia, string? Erro)> CriarTransferenciaAsync(Guid tenantId, Guid usuarioId, string usuarioNome, CriarTransferenciaRequest req)
    {
        if (req.FilialOrigemId == req.FilialDestinoId)
            return (null, "A filial de origem e a filial de destino não podem ser iguais.");

        if (req.Itens == null || req.Itens.Count == 0)
            return (null, "A solicitação deve conter ao menos um item para transferência.");

        var fOrigem = await db.Filiais.FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Id == req.FilialOrigemId);
        var fDestino = await db.Filiais.FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Id == req.FilialDestinoId);
        if (fOrigem == null || fDestino == null) return (null, "Filial de origem ou destino não encontrada.");

        var prodIds = req.Itens.Select(i => i.ProdutoId).ToList();
        var produtos = await db.Produtos.Where(p => p.TenantId == tenantId && prodIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        var num = $"TRF-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(100, 999)}";
        var transf = new SolicitacaoTransferencia
        {
            TenantId = tenantId,
            Numero = num,
            FilialOrigemId = req.FilialOrigemId,
            FilialDestinoId = req.FilialDestinoId,
            Status = "Pendente",
            Observacoes = req.Observacoes?.Trim() ?? string.Empty,
            SolicitadoPorId = usuarioId,
            SolicitadoPorNome = string.IsNullOrWhiteSpace(usuarioNome) ? "Operador Filial" : usuarioNome
        };

        foreach (var item in req.Itens)
        {
            if (produtos.TryGetValue(item.ProdutoId, out var prod))
            {
                transf.Itens.Add(new SolicitacaoTransferenciaItem
                {
                    TenantId = tenantId,
                    ProdutoId = prod.Id,
                    CodigoProduto = prod.CodigoErp,
                    DescricaoProduto = prod.Descricao,
                    QuantidadeSolicitada = item.Quantidade,
                    QuantidadeAtendida = item.Quantidade
                });
            }
        }

        db.SolicitacoesTransferencia.Add(transf);
        await db.SaveChangesAsync();

        return (ToTransferenciaDto(transf, fOrigem.Nome, fDestino.Nome), null);
    }

    public async Task<List<SolicitacaoTransferenciaDto>> ListarTransferenciasAsync(Guid tenantId)
    {
        var transfs = await db.SolicitacoesTransferencia
            .Include(t => t.FilialOrigem)
            .Include(t => t.FilialDestino)
            .Include(t => t.Itens)
            .Where(t => t.TenantId == tenantId)
            .OrderByDescending(t => t.DataCriacao)
            .ToListAsync();

        return transfs.Select(t => ToTransferenciaDto(t, t.FilialOrigem?.Nome ?? "", t.FilialDestino?.Nome ?? "")).ToList();
    }

    public async Task<SolicitacaoTransferenciaDto?> AtualizarStatusTransferenciaAsync(Guid tenantId, Guid transferenciaId, Guid usuarioId, string usuarioNome, AtualizarStatusTransferenciaRequest req)
    {
        var transf = await db.SolicitacoesTransferencia
            .Include(t => t.FilialOrigem)
            .Include(t => t.FilialDestino)
            .Include(t => t.Itens)
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == transferenciaId);

        if (transf == null) return null;

        var statusAnterior = transf.Status;
        transf.Status = req.NovoStatus;

        if (req.NovoStatus == "Aprovada")
        {
            transf.AprovadoPorId = usuarioId;
            transf.AprovadoPorNome = usuarioNome;
            transf.DataAprovacao = DateTime.UtcNow;
        }
        else if (req.NovoStatus == "Concluida" && statusAnterior != "Concluida")
        {
            transf.DataConclusao = DateTime.UtcNow;

            // Movimenta estoque físico entre as filiais no Nexo Compras
            foreach (var item in transf.Itens)
            {
                // Abate de origem
                var estOrigem = await db.EstoquesFilial.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.FilialId == transf.FilialOrigemId && e.ProdutoId == item.ProdutoId);
                if (estOrigem != null) estOrigem.EstoqueAtual = Math.Max(0, estOrigem.EstoqueAtual - item.QuantidadeAtendida);

                // Incrementa no destino
                var estDestino = await db.EstoquesFilial.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.FilialId == transf.FilialDestinoId && e.ProdutoId == item.ProdutoId);
                if (estDestino != null)
                {
                    estDestino.EstoqueAtual += item.QuantidadeAtendida;
                }
                else
                {
                    db.EstoquesFilial.Add(new EstoqueFilial
                    {
                        TenantId = tenantId,
                        FilialId = transf.FilialDestinoId,
                        ProdutoId = item.ProdutoId,
                        EstoqueAtual = item.QuantidadeAtendida,
                        UltimaAtualizacao = DateTime.UtcNow
                    });
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(req.Observacoes))
            transf.Observacoes += $" [Atualização: {req.Observacoes}]";

        await db.SaveChangesAsync();

        return ToTransferenciaDto(transf, transf.FilialOrigem?.Nome ?? "", transf.FilialDestino?.Nome ?? "");
    }

    private static FilialDto ToFilialDto(Filial f) => new(
        f.Id,
        f.Codigo,
        f.Nome,
        f.Cnpj,
        f.Endereco,
        f.Cidade,
        f.Uf,
        f.Telefone,
        f.Email,
        f.Responsavel,
        f.Ativa,
        f.AgenteId,
        f.Agente?.Nome,
        string.IsNullOrWhiteSpace(f.Token) ? f.Id.ToString() : f.Token,
        f.TokenRevogado
    );

    private static SolicitacaoTransferenciaDto ToTransferenciaDto(SolicitacaoTransferencia t, string nomeOrigem, string nomeDestino) => new(
        t.Id,
        t.Numero,
        t.FilialOrigemId,
        nomeOrigem,
        t.FilialDestinoId,
        nomeDestino,
        t.Status,
        t.Observacoes,
        t.SolicitadoPorNome,
        t.AprovadoPorNome,
        t.DataCriacao,
        t.DataAprovacao,
        t.DataConclusao,
        t.Itens.Select(i => new SolicitacaoTransferenciaItemDto(
            i.ProdutoId,
            i.CodigoProduto,
            i.DescricaoProduto,
            i.QuantidadeSolicitada,
            i.QuantidadeAtendida
        )).ToList()
    );
}

