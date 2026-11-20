using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.DTOs;
using Nexo.Api.Models;
using Nexo.Api.MultiTenancy;

namespace Nexo.Api.Controllers;

/// <summary>
/// API consumida exclusivamente pelo Nexo Compras Agent. A autenticação é pela credencial do agente (X-Agent-Key):
/// o tenant é deduzido do agente cadastrado, e qualquer identificador de empresa enviado no corpo é ignorado.
/// </summary>
[ApiController]
[Route("api/agent/v1")]
[Authorize(AuthenticationSchemes = "Agent", Roles = "Agent")]
public class AgentController(NexoDbContext db, ITenantContext tenant) : ControllerBase
{
    private const int MaxBatch = 50000;
    private Guid AgentId => Guid.Parse(User.FindFirstValue("agent_id")!);

    private static string T(string? s, int max) { s = (s ?? "").Trim(); return s.Length > max ? s[..max] : s; }
    private static decimal R(decimal v, int scale) => Math.Round(v, scale, MidpointRounding.AwayFromZero);

    // =========================================================================
    // SEÇÃO 7.3: Ativação Segura do Agente via ID da Loja (Tenant ID) ou Código Temporário
    // =========================================================================
    [HttpPost("activate")]
    [AllowAnonymous]
    public async Task<IActionResult> Activate([FromBody] AtivarAgenteRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.CodigoAtivacao))
            return BadRequest(new { erro = "ID da Filial, Token ou Código de ativação não informado." });

        var inputCode = r.CodigoAtivacao.Trim();
        Agente? agente = null;
        Tenant? empresa = null;
        Filial? filial = null;

        using (tenant.SystemScope())
        {
            // 1. SUPORTE PRIORITÁRIO AO TOKEN EXCLUSIVO DO WEB ID FILIAL OU ID DA FILIAL
            if (Guid.TryParse(inputCode, out var branchGuidCandidate))
            {
                filial = await db.Filiais.Include(f => f.Tenant).FirstOrDefaultAsync(f => f.Id == branchGuidCandidate && f.Ativa && !f.TokenRevogado);
            }
            if (filial == null)
            {
                filial = await db.Filiais.Include(f => f.Tenant).FirstOrDefaultAsync(f => f.Token == inputCode && f.Ativa && !f.TokenRevogado);
            }

            if (filial != null)
            {
                empresa = filial.Tenant ?? await db.Tenants.FirstOrDefaultAsync(t => t.Id == filial.TenantId);
                if (empresa == null)
                    return NotFound(new { erro = "Empresa vinculada à filial não encontrada." });
                if (empresa.Status == "Bloqueado")
                    return StatusCode(403, new { erro = "Esta empresa está com o acesso suspenso ou bloqueado." });

                agente = await db.Agentes.FirstOrDefaultAsync(a => a.FilialId == filial.Id && !a.Revogado);
                if (agente == null)
                {
                    agente = new Agente
                    {
                        Id = Guid.NewGuid(),
                        TenantId = filial.TenantId,
                        FilialId = filial.Id,
                        Nome = $"Sincronizador - {filial.Nome} ({filial.Codigo})",
                        NomeMaquina = T(r.NomeMaquina, 100),
                        TipoBanco = "Firebird",
                        Status = "Online",
                        UltimoHeartbeat = DateTime.UtcNow,
                        BancoConfigJson = JsonSerializer.Serialize(new
                        {
                            tipo = "Firebird",
                            host = "127.0.0.1",
                            porta = 3050,
                            caminhoOuBanco = "",
                            usuario = "SYSDBA",
                            senha = "",
                            charset = "WIN1252",
                            dialeto = 3
                        })
                    };
                    await db.Agentes.AddAsync(agente);
                }
                filial.AgenteId = agente.Id;
            }
            else if (Guid.TryParse(inputCode, out var storeGuid))
            {
                // 2. ATIVAÇÃO DIRETA PELO ID DA LOJA (TENANT ID / UUID) OU ID DO USUÁRIO
                empresa = await db.Tenants.FirstOrDefaultAsync(t => t.Id == storeGuid);
                if (empresa == null)
                {
                    var usuario = await db.Usuarios.Include(u => u.Tenant).FirstOrDefaultAsync(u => u.Id == storeGuid);
                    if (usuario != null)
                    {
                        empresa = usuario.Tenant ?? await db.Tenants.FirstOrDefaultAsync(t => t.Status == "Ativo");
                        if (empresa != null) storeGuid = empresa.Id;
                    }
                }

                if (empresa == null)
                {
                    return NotFound(new { erro = $"Filial ou Loja não encontrada com o ID/Token '{inputCode}'. Copie o Token da Filial exibido no painel Web." });
                }

                if (empresa.Status == "Bloqueado")
                {
                    return StatusCode(403, new { erro = "Esta empresa/loja está com o acesso suspenso ou bloqueado." });
                }

                agente = await db.Agentes.FirstOrDefaultAsync(a => a.TenantId == storeGuid && !a.Revogado);
                if (agente == null)
                {
                    agente = new Agente
                    {
                        Id = Guid.NewGuid(),
                        TenantId = storeGuid,
                        Nome = $"Sincronizador Local - {empresa.NomeFantasia ?? empresa.RazaoSocial}",
                        NomeMaquina = T(r.NomeMaquina, 100),
                        TipoBanco = "Firebird",
                        Status = "Online",
                        UltimoHeartbeat = DateTime.UtcNow,
                        BancoConfigJson = JsonSerializer.Serialize(new
                        {
                            tipo = "Firebird",
                            host = "127.0.0.1",
                            porta = 3050,
                            caminhoOuBanco = "",
                            usuario = "SYSDBA",
                            senha = "",
                            charset = "WIN1252",
                            dialeto = 3
                        })
                    };
                    await db.Agentes.AddAsync(agente);
                }
            }
            else
            {
                // 3. ATIVAÇÃO VIA CÓDIGO TEMPORÁRIO (ACTV-XXXX-XXXX)
                var normalizedCode = Security.Secrets.NormalizeCode(inputCode);
                var codeHash = Security.Secrets.Sha256Hex(normalizedCode);
                agente = await db.Agentes.FirstOrDefaultAsync(a => a.CodigoAtivacaoHash == codeHash);
                if (agente == null)
                    return Unauthorized(new { erro = "Token da Filial ou Código de ativação inválido ou não encontrado." });

                empresa = await db.Tenants.FirstOrDefaultAsync(t => t.Id == agente.TenantId);
                if (empresa == null) return NotFound(new { erro = "Empresa vinculada não encontrada." });

                if (agente.BloqueadoAte.HasValue && agente.BloqueadoAte.Value > DateTime.UtcNow)
                    return StatusCode(429, new { erro = $"Instalação bloqueada temporariamente até {agente.BloqueadoAte.Value:dd/MM/yyyy HH:mm:ss}." });

                if (agente.CodigoAtivacaoUsado)
                    return BadRequest(new { erro = "Este código temporário já foi utilizado." });

                if (agente.CodigoAtivacaoExpiracao.HasValue && agente.CodigoAtivacaoExpiracao.Value < DateTime.UtcNow)
                    return BadRequest(new { erro = "Este código temporário expirou." });

                if (agente.Revogado)
                    return StatusCode(403, new { erro = "Esta instalação foi revogada." });
            }
        }

        if (empresa == null || agente == null)
            return NotFound(new { erro = "Empresa ou agente não identificado para o token fornecido." });

        // Validação adicional de segurança por CNPJ-base
        if (!string.IsNullOrWhiteSpace(r.CnpjBase))
        {
            var inputBaseDigits = new string(r.CnpjBase.Where(char.IsDigit).Take(8).ToArray());
            var empresaBaseDigits = new string((empresa.Cnpj ?? "").Where(char.IsDigit).Take(8).ToArray());
            if (inputBaseDigits.Length > 0 && empresaBaseDigits.Length > 0 && inputBaseDigits != empresaBaseDigits)
            {
                return BadRequest(new { erro = $"CNPJ-base informado ('{inputBaseDigits}') diverge do CNPJ-base da empresa autorizada ('{empresaBaseDigits}')." });
            }
        }

        // Marca código como utilizado e emite credencial exclusiva da instalação
        agente.CodigoAtivacaoUsado = true;
        agente.TentativasAtivacaoInvalidas = 0;
        agente.BloqueadoAte = null;

        var (chaveExclusiva, hash, prefix) = Security.AgentKey.Generate(agente.Id);
        agente.ChaveHash = hash;
        agente.ChavePrefixo = prefix;
        agente.ChaveRotacionadaEm = DateTime.UtcNow;
        agente.Status = "Online";
        agente.UltimoHeartbeat = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(r.NomeMaquina)) agente.NomeMaquina = T(r.NomeMaquina, 100);

        await db.SaveChangesAsync();

        // Desserializa configurações autorizadas do banco para retornar ao agente
        BancoConfigDto bancoConfig;
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(agente.BancoConfigJson) ? "{}" : agente.BancoConfigJson);
            var root = doc.RootElement;
            bancoConfig = new BancoConfigDto(
                Tipo: root.TryGetProperty("tipo", out var tProp) ? tProp.GetString() ?? agente.TipoBanco : agente.TipoBanco,
                Host: root.TryGetProperty("host", out var hProp) ? hProp.GetString() ?? "127.0.0.1" : "127.0.0.1",
                Porta: root.TryGetProperty("porta", out var pProp) ? pProp.GetInt32() : (agente.TipoBanco == "Firebird" ? 3050 : 5432),
                CaminhoOuBanco: root.TryGetProperty("caminhoOuBanco", out var cProp) ? cProp.GetString() ?? "" : "",
                Usuario: root.TryGetProperty("usuario", out var uProp) ? uProp.GetString() ?? "SYSDBA" : "SYSDBA",
                Senha: root.TryGetProperty("senha", out var sProp) ? sProp.GetString() ?? "" : "",
                Charset: root.TryGetProperty("charset", out var chProp) ? chProp.GetString() ?? "WIN1252" : "WIN1252",
                Dialeto: root.TryGetProperty("dialeto", out var dProp) ? dProp.GetInt32() : 3
            );
        }
        catch
        {
            bancoConfig = new BancoConfigDto(
                agente.TipoBanco,
                "127.0.0.1",
                agente.TipoBanco == "Firebird" ? 3050 : 5432,
                agente.TipoBanco == "Firebird" ? "" : "sistema_comercial",
                agente.TipoBanco == "Firebird" ? "SYSDBA" : "postgres",
                "",
                "WIN1252",
                3
            );
        }

        var nomeFantasiaExibicao = filial != null
            ? $"{empresa.NomeFantasia ?? empresa.RazaoSocial} - {filial.Nome}"
            : (empresa.NomeFantasia ?? empresa.RazaoSocial);

        return Ok(new AtivarAgenteResponse(
            agente.Id,
            empresa.Id,
            chaveExclusiva,
            new EmpresaAtivadaDto(
                empresa.RazaoSocial,
                nomeFantasiaExibicao,
                empresa.Cnpj,
                filial?.Id.ToString(),
                filial?.Nome,
                filial?.Codigo
            ),
            bancoConfig,
            new IntervalosConfigDto(agente.IntervaloEstoqueMin, agente.IntervaloProdutosMin, agente.IntervaloFornecedoresMin, agente.IntervaloHeartbeatSeg)
        ));
    }

    [HttpPost("rotate-key")]
    public async Task<IActionResult> RotateKey()
    {
        var a = await db.Agentes.FirstAsync(x => x.Id == AgentId);
        var (novaChave, hash, prefix) = Security.AgentKey.Generate(a.Id);
        a.ChaveHash = hash;
        a.ChavePrefixo = prefix;
        a.ChaveRotacionadaEm = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new RotacionarCredencialResponse(a.Id, novaChave, prefix, a.ChaveRotacionadaEm.Value));
    }

    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] AgentHeartbeatRequest r)
    {
        var a = await db.Agentes.FirstAsync(x => x.Id == AgentId);
        a.UltimoHeartbeat = DateTime.UtcNow;
        a.Status = "Online";
        if (!string.IsNullOrWhiteSpace(r.Versao)) a.Versao = T(r.Versao, 30);
        if (!string.IsNullOrWhiteSpace(r.NomeMaquina)) a.NomeMaquina = T(r.NomeMaquina, 100);
        if (r.Ambiente is { ValueKind: JsonValueKind.Object } amb) a.AmbienteJson = amb.GetRawText().Length <= 8000 ? amb.GetRawText() : a.AmbienteJson;
        await db.SaveChangesAsync();
        return Ok(new AgentConfigDto(a.IntervaloEstoqueMin, a.IntervaloProdutosMin, a.IntervaloFornecedoresMin, a.IntervaloHeartbeatSeg));
    }

    private async Task<Guid?> ObterBranchIdAsync()
    {
        var branchClaim = User.FindFirst("branch_id")?.Value;
        if (Guid.TryParse(branchClaim, out var bGuid)) return bGuid;
        var a = await db.Agentes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == AgentId);
        return a?.FilialId;
    }

    [HttpGet("config")]
    public async Task<IActionResult> Config()
    {
        var a = await db.Agentes.AsNoTracking().FirstAsync(x => x.Id == AgentId);
        return Ok(new AgentConfigDto(a.IntervaloEstoqueMin, a.IntervaloProdutosMin, a.IntervaloFornecedoresMin, a.IntervaloHeartbeatSeg));
    }

    [HttpPost("sync/fornecedores")]
    public async Task<IActionResult> SyncFornecedores([FromBody] SyncBatch<FornecedorSync> batch)
    {
        if (batch.TenantId.HasValue && batch.TenantId.Value != Guid.Empty && tenant.CurrentTenantId.HasValue && batch.TenantId.Value != tenant.CurrentTenantId.Value)
            return StatusCode(403, new { erro = $"Tenant ID do lote ({batch.TenantId}) diverge do tenant autenticado ({tenant.CurrentTenantId}). Operação bloqueada por isolamento multi-empresa." });

        if (batch.Itens == null || batch.Itens.Count > MaxBatch) return BadRequest(new { erro = $"Lote inválido (máximo {MaxBatch} itens)." });
        var codigos = batch.Itens.Select(i => (i.CodigoErp ?? "").Trim()).Where(c => c != "").Distinct().ToList();
        var existentes = await db.Fornecedores.Where(f => codigos.Contains(f.CodigoErp)).ToDictionaryAsync(f => f.CodigoErp);
        var branchId = await ObterBranchIdAsync();

        int criados = 0, atualizados = 0, ignorados = 0;
        foreach (var i in batch.Itens)
        {
            var cod = T(i.CodigoErp, 100);
            if (cod == "" || string.IsNullOrWhiteSpace(i.RazaoSocial)) { ignorados++; continue; }
            var novo = !existentes.TryGetValue(cod, out var f);
            if (novo)
            {
                f = new Fornecedor
                {
                    CodigoErp = cod,
                    Origem = "erp",
                    FilialId = branchId,
                    TenantId = tenant.CurrentTenantId ?? Guid.Empty
                };
                db.Fornecedores.Add(f);
                existentes[cod] = f;
            }
            f!.RazaoSocial = T(i.RazaoSocial, 200);
            f.NomeFantasia = T(string.IsNullOrWhiteSpace(i.NomeFantasia) ? i.RazaoSocial : i.NomeFantasia, 200);
            f.Cnpj = new string((i.Cnpj ?? "").Where(char.IsDigit).ToArray());
            f.InscricaoEstadual = T(i.InscricaoEstadual, 30); f.Endereco = T(i.Endereco, 250); f.Cidade = T(i.Cidade, 100); f.Uf = T(i.Uf, 2).ToUpperInvariant();
            if (branchId.HasValue && !f.FilialId.HasValue) f.FilialId = branchId;
            // contatos: o ERP só sobrescreve quando traz valor (não apaga o que o comprador complementou)
            if (!string.IsNullOrWhiteSpace(i.Email)) f.Email = T(i.Email, 150).ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(i.Telefone)) f.Telefone = T(i.Telefone, 30);
            if (!string.IsNullOrWhiteSpace(i.Whatsapp)) f.Whatsapp = T(i.Whatsapp, 30);
            if (!string.IsNullOrWhiteSpace(i.Contato)) f.Contato = T(i.Contato, 100);
            if (i.PrazoEntregaDias > 0) f.PrazoEntregaPadrao = Math.Min(i.PrazoEntregaDias, 365);

            if (novo) criados++;
            else if (db.Entry(f).State == EntityState.Modified) atualizados++;
            else ignorados++;
        }
        await db.SaveChangesAsync();
        // vincula produtos ao fornecedor padrão do ERP (idempotente; respeita RLS)
        try
        {
            await db.Database.ExecuteSqlRawAsync(@"UPDATE ""Produtos"" p SET ""FornecedorVinculadoId"" = f.""Id""
                FROM ""Fornecedores"" f WHERE f.""TenantId"" = p.""TenantId"" AND f.""CodigoErp"" = p.""FornecedorErpId"" AND p.""FornecedorErpId"" <> ''
                AND p.""FornecedorVinculadoId"" IS DISTINCT FROM f.""Id""");
        }
        catch
        {
            // Vinculação opcional pós-sincronização
        }
        return await Finish("Fornecedores", batch.Itens.Count, criados, atualizados, ignorados);
    }

    [HttpPost("sync/produtos")]
    public async Task<IActionResult> SyncProdutos([FromBody] SyncBatch<ProdutoSync> batch)
    {
        if (batch.TenantId.HasValue && batch.TenantId.Value != Guid.Empty && tenant.CurrentTenantId.HasValue && batch.TenantId.Value != tenant.CurrentTenantId.Value)
            return StatusCode(403, new { erro = $"Tenant ID do lote ({batch.TenantId}) diverge do tenant autenticado ({tenant.CurrentTenantId}). Operação bloqueada por isolamento multi-empresa." });

        if (batch.Itens == null || batch.Itens.Count > MaxBatch) return BadRequest(new { erro = $"Lote inválido (máximo {MaxBatch} itens)." });
        var codigos = batch.Itens.Select(i => (i.CodigoErp ?? "").Trim()).Where(c => c != "").Distinct().ToList();
        var existentes = await db.Produtos.Where(p => codigos.Contains(p.CodigoErp)).ToDictionaryAsync(p => p.CodigoErp);
        var forn = await db.Fornecedores.AsNoTracking().Where(f => f.CodigoErp != "").Select(f => new { f.CodigoErp, f.Id }).ToDictionaryAsync(f => f.CodigoErp, f => f.Id);
        var idsExistentes = existentes.Values.Select(p => p.Id).ToList();
        var historico = (await db.HistoricoPrecos.AsNoTracking().Where(h => idsExistentes.Contains(h.ProdutoId) && h.Origem == "erp").Select(h => new { h.ProdutoId, h.Data, h.Valor }).ToListAsync())
            .Select(h => (h.ProdutoId, h.Data, h.Valor)).ToHashSet();
        var branchId = await ObterBranchIdAsync();

        int criados = 0, atualizados = 0, ignorados = 0;
        var agora = DateTime.UtcNow;
        foreach (var i in batch.Itens)
        {
            var cod = T(i.CodigoErp, 100);
            if (cod == "" || string.IsNullOrWhiteSpace(i.Descricao)) { ignorados++; continue; }
            var novo = !existentes.TryGetValue(cod, out var p);
            if (novo)
            {
                p = new Produto
                {
                    CodigoErp = cod,
                    Origem = "erp",
                    TenantId = tenant.CurrentTenantId ?? Guid.Empty
                };
                db.Produtos.Add(p);
                existentes[cod] = p;
            }
            p!.Descricao = T(i.Descricao, 255); p.CodigoBarras = T(i.CodigoBarras, 50); p.Unidade = string.IsNullOrWhiteSpace(i.Unidade) ? "UN" : T(i.Unidade, 10).ToUpperInvariant();
            p.Grupo = T(i.Grupo, 100); p.Categoria = T(i.Categoria, 100); p.Ncm = T(i.Ncm, 12);
            p.PrecoVenda = R(i.PrecoVenda, 4); p.CustoAtual = R(i.CustoAtual, 4); p.Consumo90Dias = R(i.Consumo90Dias, 3);
            p.FornecedorErpId = T(i.FornecedorErpId, 100);
            if (p.FornecedorErpId != "" && forn.TryGetValue(p.FornecedorErpId, out var fid)) p.FornecedorVinculadoId = fid;
            if (i.UltimoCusto > 0 && i.UltimaCompraEm != null)
            {
                p.UltimoCusto = R(i.UltimoCusto, 4); p.UltimaCompraEm = i.UltimaCompraEm.Value.Date;
                var data = i.UltimaCompraEm.Value.Date;
                if (!historico.Contains((p.Id, data, p.UltimoCusto)))
                {
                    Guid? hf = !string.IsNullOrWhiteSpace(i.UltimoFornecedorErpId) && forn.TryGetValue(T(i.UltimoFornecedorErpId, 100), out var x) ? x : null;
                    db.HistoricoPrecos.Add(new HistoricoPreco { ProdutoId = p.Id, FornecedorId = hf, Data = data, Valor = p.UltimoCusto, Quantidade = R(i.QuantidadeUltimaCompra, 3), Origem = "erp" });
                    historico.Add((p.Id, data, p.UltimoCusto));
                }
            }

            if (branchId.HasValue)
            {
                var currentTenantId = tenant.CurrentTenantId ?? p.TenantId;
                var estFilial = await db.EstoquesFilial.FirstOrDefaultAsync(e => e.FilialId == branchId.Value && e.ProdutoId == p.Id);
                if (estFilial == null)
                {
                    db.EstoquesFilial.Add(new EstoqueFilial
                    {
                        TenantId = currentTenantId,
                        FilialId = branchId.Value,
                        ProdutoId = p.Id,
                        EstoqueAtual = p.EstoqueAtual ?? 0m,
                        EstoqueMinimo = p.EstoqueMinimo,
                        EstoquePendente = 0,
                        UltimaAtualizacao = agora
                    });
                }
            }

            if (novo) { criados++; p.UltimaSincronizacaoEm = agora; }
            else if (db.Entry(p).State == EntityState.Modified) { atualizados++; p.UltimaSincronizacaoEm = agora; }
            else ignorados++;
        }
        await db.SaveChangesAsync();
        return await Finish("Produtos", batch.Itens.Count, criados, atualizados, ignorados);
    }

    [HttpPost("sync/estoque")]
    public async Task<IActionResult> SyncEstoque([FromBody] SyncBatch<EstoqueSync> batch)
    {
        if (batch.TenantId.HasValue && batch.TenantId.Value != Guid.Empty && tenant.CurrentTenantId.HasValue && batch.TenantId.Value != tenant.CurrentTenantId.Value)
            return StatusCode(403, new { erro = $"Tenant ID do lote ({batch.TenantId}) diverge do tenant autenticado ({tenant.CurrentTenantId}). Operação bloqueada por isolamento multi-empresa." });
        if (batch.Itens == null || batch.Itens.Count > MaxBatch) return BadRequest(new { erro = $"Lote inválido (máximo {MaxBatch} itens)." });
        var codigos = batch.Itens.Select(i => (i.CodigoErp ?? "").Trim()).Where(c => c != "").Distinct().ToList();
        var existentes = await db.Produtos.Where(p => codigos.Contains(p.CodigoErp)).ToDictionaryAsync(p => p.CodigoErp);
        var branchId = await ObterBranchIdAsync();

        int atualizados = 0, ignorados = 0;
        foreach (var i in batch.Itens)
        {
            if (!existentes.TryGetValue(T(i.CodigoErp, 100), out var p)) { ignorados++; continue; }
            // NULL do ERP = estoque desconhecido: nunca vira zero (um zero real chega como 0).
            decimal? estAtual = i.EstoqueAtual.HasValue ? R(i.EstoqueAtual.Value, 3) : null;
            var estPend = R(i.EstoquePendente, 3);
            p.EstoqueAtual = estAtual;
            p.EstoquePendente = estPend;
            if (db.Entry(p).State == EntityState.Modified) { atualizados++; p.UltimaSincronizacaoEm = DateTime.UtcNow; } else ignorados++;

            if (branchId.HasValue && estAtual.HasValue)
            {
                var currentTenantId = tenant.CurrentTenantId ?? p.TenantId;
                var estFilial = await db.EstoquesFilial.FirstOrDefaultAsync(e => e.FilialId == branchId.Value && e.ProdutoId == p.Id);
                if (estFilial == null)
                {
                    db.EstoquesFilial.Add(new EstoqueFilial
                    {
                        TenantId = currentTenantId,
                        FilialId = branchId.Value,
                        ProdutoId = p.Id,
                        EstoqueAtual = estAtual.Value,
                        EstoqueMinimo = p.EstoqueMinimo,
                        EstoquePendente = estPend,
                        UltimaAtualizacao = DateTime.UtcNow
                    });
                }
                else
                {
                    estFilial.EstoqueAtual = estAtual.Value;
                    estFilial.EstoquePendente = estPend;
                    estFilial.UltimaAtualizacao = DateTime.UtcNow;
                }
            }
        }
        await db.SaveChangesAsync();
        return await Finish("Estoque", batch.Itens.Count, 0, atualizados, ignorados);
    }

    [HttpPost("erro")]
    public async Task<IActionResult> Erro([FromBody] AgentErroRequest r)
    {
        db.SincronizacaoErros.Add(new SincronizacaoErro { AgenteId = AgentId, Origem = T(r.Origem, 60), Mensagem = T(r.Mensagem, 500), Detalhe = T(r.Detalhe, 4000) });
        var a = await db.Agentes.FirstAsync(x => x.Id == AgentId);
        a.Status = "Erro"; a.UltimoHeartbeat = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    private async Task<IActionResult> Finish(string tipo, int recebidos, int criados, int atualizados, int ignorados)
    {
        var a = await db.Agentes.FirstAsync(x => x.Id == AgentId);
        a.UltimaSincronizacao = DateTime.UtcNow; a.UltimoHeartbeat = DateTime.UtcNow; a.Status = "Online";
        if (tipo != "Estoque" || criados + atualizados > 0)
            db.Sincronizacoes.Add(new Sincronizacao { AgenteId = a.Id, Tipo = tipo, Recebidos = recebidos, Criados = criados, Atualizados = atualizados, Ignorados = ignorados, Status = "Sucesso" });
        await db.SaveChangesAsync();
        return Ok(new SyncResult(recebidos, criados, atualizados, ignorados));
    }
}

