using System.Net.Sockets;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.DTOs;
using Nexo.Api.Models;
using Nexo.Api.Security;
using Nexo.Api.Services;

namespace Nexo.Api.Controllers;

[ApiController]
[Route("api/plans")]
public class PlansController(NexoDbContext db) : ControllerBase
{
    /// <summary>Planos ativos: pÃºblico (usado na tela de cadastro).</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get() =>
        Ok(await db.Planos.Where(p => p.Ativo).OrderBy(p => p.Mensalidade).ToListAsync());
}

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "PlatformAdmin")]
public class AdminController(NexoDbContext db, IAuditService audit) : ControllerBase
{
    private static TenantSummaryDto ToDto(Tenant t) => new(t.Id, t.RazaoSocial, t.NomeFantasia, t.Cnpj, t.Status, t.Plano?.Nome);

    // =========================================================================
    // SEÃ‡ÃƒO A: Dashboard Administrativo Global
    // =========================================================================
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var totalEmpresas = await db.Tenants.CountAsync();
        var empresasAtivas = await db.Tenants.CountAsync(t => t.Status == "Ativo");
        var empresasBloqueadas = await db.Tenants.CountAsync(t => t.Status == "Bloqueado");
        var totalFiliais = await db.Filiais.CountAsync();
        var totalSincronizadores = await db.Agentes.CountAsync();
        var sincronizadoresOnline = await db.Agentes.CountAsync(a => a.Status == "Online" && !a.Revogado);
        var sincronizadoresOffline = await db.Agentes.CountAsync(a => a.Status != "Online" && a.Status != "PendenteAtivacao" && !a.Revogado);
        var sincronizadoresPendentes = await db.Agentes.CountAsync(a => a.Status == "PendenteAtivacao" && !a.Revogado);
        var totalUsuarios = await db.Usuarios.CountAsync();

        var mrr = await db.Assinaturas.Where(a => a.Status == "Ativa" || a.Status == "Teste").SumAsync(a => (decimal?)a.Valor) ?? 0;
        var ultimos = await db.Tenants.Include(t => t.Plano).OrderByDescending(t => t.DataCriacao).Take(6).ToListAsync();

        var falhas = await db.SincronizacaoErros
            .OrderByDescending(e => e.DataCriacao)
            .Take(5)
            .Select(e => new FalhaSincronizacaoRecenteDto(e.Id, "Empresa", e.Origem, e.Mensagem, e.DataCriacao))
            .ToListAsync();

        return Ok(new
        {
            totalEmpresas,
            totalTenants = totalEmpresas,
            empresasAtivas,
            tenantsAtivos = empresasAtivas,
            empresasBloqueadas,
            tenantsBloqueados = empresasBloqueadas,
            totalFiliais,
            totalSincronizadores,
            sincronizadoresOnline,
            sincronizadoresOffline,
            sincronizadoresPendentes,
            totalUsuarios,
            mrrEstimado = mrr,
            ultimasEmpresas = ultimos.Select(ToDto).ToList(),
            ultimosTenants = ultimos.Select(ToDto).ToList(),
            falhasRecentes = falhas,
            statusPlataforma = "Operacional"
        });
    }

    // =========================================================================
    // SEÃ‡ÃƒO B: GestÃ£o de Empresas (Tenants)
    // =========================================================================
    [HttpGet("tenants")]
    public async Task<IActionResult> Tenants([FromQuery] string? q, [FromQuery] string? status)
    {
        var query = db.Tenants.Include(t => t.Plano).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(t => t.Status == status);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim().ToLower();
            query = query.Where(t => t.RazaoSocial.ToLower().Contains(s) || t.NomeFantasia.ToLower().Contains(s) || t.Cnpj.Contains(s));
        }
        return Ok((await query.OrderByDescending(t => t.DataCriacao).ToListAsync()).Select(ToDto));
    }

    [HttpGet("tenants/{id:guid}")]
    public async Task<IActionResult> TenantDetail(Guid id)
    {
        var t = await db.Tenants
            .Include(x => x.Plano)
            .Include(x => x.Usuarios)
            .Include(x => x.Filiais)
            .Include(x => x.Agentes)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (t == null) return NotFound(new { erro = "Empresa nÃ£o encontrada." });

        return Ok(new
        {
            t.Id, t.RazaoSocial, t.NomeFantasia, t.Cnpj, t.Email, t.Telefone, t.Status, t.DataCriacao, t.DataExpiracao,
            Plano = t.Plano == null ? null : new { t.Plano.Id, t.Plano.Nome, t.Plano.Mensalidade },
            Usuarios = t.Usuarios.Select(u => new { u.Id, u.Nome, u.Email, u.Perfil, u.Ativo, u.UltimoLogin }),
            Filiais = t.Filiais.Select(f => new { f.Id, f.Codigo, f.Nome, f.Cnpj, f.Token, f.TokenRevogado, f.Ativa }),
            Agentes = t.Agentes.Select(a => new { a.Id, a.Nome, a.Status, a.TipoBanco, a.Versao, a.UltimoHeartbeat, a.UltimaSincronizacao, a.Revogado })
        });
    }

    [HttpPut("tenants/{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateTenantStatusRequest r)
    {
        if (r.Status is not ("Ativo" or "Bloqueado" or "Pendente")) return BadRequest(new { erro = "Status invÃ¡lido. Use Ativo, Bloqueado ou Pendente." });
        var t = await db.Tenants.FindAsync(id);
        if (t == null) return NotFound(new { erro = "Empresa nÃ£o encontrada." });
        var anterior = t.Status;
        t.Status = r.Status;
        audit.Add("StatusEmpresaAlterado", "Tenant", t.Id.ToString(), new { de = anterior, para = r.Status }, t.Id);
        await db.SaveChangesAsync();
        return Ok(new { mensagem = $"Status da empresa atualizado para {t.Status}." });
    }

    // =========================================================================
    // SEÃ‡ÃƒO B / 4 / 8: Cadastro Completo de Empresa (Wizard em 5 Etapas)
    // =========================================================================
    [HttpPost("empresas/cadastro-completo")]
    public async Task<IActionResult> CadastrarEmpresaCompleta([FromBody] CadastroCompletoEmpresaRequest r)
    {
        // 1. ValidaÃ§Ãµes bÃ¡sicas
        if (string.IsNullOrWhiteSpace(r.RazaoSocial))
            return BadRequest(new { erro = "A RazÃ£o Social da empresa Ã© obrigatÃ³ria." });

        var cnpjLimpo = new string((r.Cnpj ?? "").Where(char.IsDigit).ToArray());
        if (cnpjLimpo.Length != 14)
            return BadRequest(new { erro = "O CNPJ informado Ã© invÃ¡lido (deve conter 14 dÃ­gitos)." });

        if (await db.Tenants.AnyAsync(t => t.Cnpj == cnpjLimpo))
            return BadRequest(new { erro = $"JÃ¡ existe uma empresa cadastrada com o CNPJ {r.Cnpj}." });

        if (string.IsNullOrWhiteSpace(r.EmailLogin) || !r.EmailLogin.Contains('@'))
            return BadRequest(new { erro = "Informe um e-mail de acesso vÃ¡lido para o cliente." });

        var emailCliente = r.EmailLogin.Trim().ToLowerInvariant();
        if (await db.Usuarios.AnyAsync(u => u.Email.ToLower() == emailCliente))
            return BadRequest(new { erro = $"JÃ¡ existe um usuÃ¡rio no sistema com o e-mail '{emailCliente}'." });

        // 2. CriaÃ§Ã£o do Tenant (Empresa)
        Guid? planoValidoId = null;
        Plano? plano = null;
        if (r.PlanoId.HasValue)
        {
            plano = await db.Planos.FindAsync(r.PlanoId.Value);
            if (plano != null) planoValidoId = plano.Id;
        }

        var tenant = new Tenant
        {
            RazaoSocial = r.RazaoSocial.Trim(),
            NomeFantasia = string.IsNullOrWhiteSpace(r.NomeFantasia) ? r.RazaoSocial.Trim() : r.NomeFantasia.Trim(),
            Cnpj = cnpjLimpo,
            Email = string.IsNullOrWhiteSpace(r.Email) ? emailCliente : r.Email.Trim().ToLowerInvariant(),
            Telefone = r.Telefone?.Trim() ?? string.Empty,
            Status = "Ativo",
            PlanoId = planoValidoId
        };
        db.Tenants.Add(tenant);

        // 3. CriaÃ§Ã£o da Filial Inicial (Matriz)
        var tokenFilial = $"FLK-{Guid.NewGuid():N}"[..24].ToUpperInvariant();
        var filial = new Filial
        {
            TenantId = tenant.Id,
            Nome = string.IsNullOrWhiteSpace(r.NomeFilial) ? "Matriz" : r.NomeFilial.Trim(),
            Codigo = string.IsNullOrWhiteSpace(r.CodigoFilial) ? "001" : r.CodigoFilial.Trim(),
            Cnpj = string.IsNullOrWhiteSpace(r.CnpjFilial) ? cnpjLimpo : new string(r.CnpjFilial.Where(char.IsDigit).ToArray()),
            Ativa = true,
            Token = tokenFilial,
            TokenGeradoEm = DateTime.UtcNow,
            TokenRevogado = false
        };
        db.Filiais.Add(filial);

        // 3.1 ConfiguraÃ§Ã£o Fiscal e ParÃ¢metros PadrÃ£o da Empresa
        db.ConfiguracoesTenant.Add(new ConfiguracaoTenant
        {
            TenantId = tenant.Id,
            Uf = "SP",
            RegimeTributario = "LucroReal",
            CreditaIcms = true,
            CreditaIpi = true,
            PercCreditoPisCofins = 0.0925m,
            LeadTimePadraoDias = 3,
            DiasSeguranca = 5,
            DiasCobertura = 30,
            ValidadeCotacaoDias = 3
        });

        // 4. Assinatura do Plano (PerÃ­odo de Teste ou Ativa)
        if (plano != null)
        {
            db.Assinaturas.Add(new Assinatura
            {
                TenantId = tenant.Id,
                PlanoId = plano.Id,
                Status = "Teste",
                DataInicio = DateTime.UtcNow,
                DataFim = DateTime.UtcNow.AddDays(plano.DiasTeste > 0 ? plano.DiasTeste : 14),
                Valor = plano.Mensalidade,
                Ciclo = "Mensal"
            });
        }

        // 5. CriaÃ§Ã£o do Acesso Inicial do Cliente (UsuÃ¡rio)
        var senhaTexto = string.IsNullOrWhiteSpace(r.SenhaInicial)
            ? $"Nexo#{Random.Shared.Next(1000, 9999)}!"
            : r.SenhaInicial.Trim();

        var usuario = new Usuario
        {
            TenantId = tenant.Id,
            FilialId = filial.Id,
            Nome = string.IsNullOrWhiteSpace(r.ResponsavelNome) ? "Administrador da Loja" : r.ResponsavelNome.Trim(),
            Email = emailCliente,
            SenhaHash = BCrypt.Net.BCrypt.HashPassword(senhaTexto),
            Perfil = string.IsNullOrWhiteSpace(r.PerfilAcesso) ? "TenantAdmin" : r.PerfilAcesso.Trim(),
            Ativo = true
        };
        db.Usuarios.Add(usuario);

        // 6. ConfiguraÃ§Ã£o do Agente SINc e CÃ³digo de AtivaÃ§Ã£o
        Agente? agente = null;
        string? rawCode = null;
        DateTime? expiraEm = null;

        if (r.GerarCodigoAtivacao)
        {
            rawCode = $"ACTV-{Secrets.AccessCode()[..9]}";
            var codeHash = Secrets.Sha256Hex(Secrets.NormalizeCode(rawCode));
            expiraEm = DateTime.UtcNow.AddHours(48);

            var tipoBanco = string.Equals(r.TipoBanco, "PostgreSQL", StringComparison.OrdinalIgnoreCase) ? "PostgreSQL" : "Firebird";
            var bancoConfig = new
            {
                tipo = tipoBanco,
                host = string.IsNullOrWhiteSpace(r.HostBanco) ? "127.0.0.1" : r.HostBanco.Trim(),
                porta = r.PortaBanco > 0 ? r.PortaBanco : (tipoBanco == "Firebird" ? 3050 : 5432),
                caminhoOuBanco = string.IsNullOrWhiteSpace(r.CaminhoBanco)
                    ? (tipoBanco == "Firebird" ? @"C:\DATACASH\DADOS\BM.FDB" : "sistema_comercial")
                    : r.CaminhoBanco.Trim(),
                usuario = string.IsNullOrWhiteSpace(r.UsuarioBanco)
                    ? (tipoBanco == "Firebird" ? "SYSDBA" : "postgres")
                    : r.UsuarioBanco.Trim(),
                senha = r.SenhaBanco ?? string.Empty,
                charset = string.IsNullOrWhiteSpace(r.CharsetBanco) ? "NONE" : r.CharsetBanco.Trim(),
                dialeto = 3
            };

            agente = new Agente
            {
                TenantId = tenant.Id,
                FilialId = filial.Id,
                Nome = $"{tenant.NomeFantasia} - Sincronizador Matriz",
                Status = "PendenteAtivacao",
                TipoBanco = tipoBanco,
                BancoConfigJson = JsonSerializer.Serialize(bancoConfig),
                CodigoAtivacaoHash = codeHash,
                CodigoAtivacaoExpiracao = expiraEm,
                CodigoAtivacaoUsado = false,
                IntervaloEstoqueMin = 5,
                IntervaloProdutosMin = 30,
                IntervaloFornecedoresMin = 30,
                IntervaloHeartbeatSeg = 60
            };
            db.Agentes.Add(agente);
        }

        audit.Add("CadastroEmpresaCompleto", "Tenant", tenant.Id.ToString(), new
        {
            tenant.RazaoSocial,
            tenant.Cnpj,
            Usuario = usuario.Email,
            Filial = filial.Nome
        }, tenant.Id);

        try
        {
            // Salva tenant, filial, usuario e agente (sem dependÃªncia circular)
            await db.SaveChangesAsync();

            // Atualiza filial com o id do agente apÃ³s a persistÃªncia inicial
            if (agente != null)
            {
                filial.AgenteId = agente.Id;
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException != null ? $"{ex.Message} -> {ex.InnerException.Message}" : ex.Message;
            return BadRequest(new { erro = $"Falha ao gravar no banco: {msg}" });
        }

        return Ok(new CadastroCompletoEmpresaResponse(
            TenantId: tenant.Id,
            RazaoSocial: tenant.RazaoSocial,
            NomeFantasia: tenant.NomeFantasia,
            Cnpj: tenant.Cnpj,
            Status: tenant.Status,
            FilialId: filial.Id,
            NomeFilial: filial.Nome,
            CodigoFilial: filial.Codigo,
            TokenFilial: filial.Token,
            UsuarioId: usuario.Id,
            UsuarioNome: usuario.Nome,
            UsuarioEmail: usuario.Email,
            SenhaInicial: senhaTexto, // Retornada em texto aberto apenas na resposta de criaÃ§Ã£o para o Administrador copiar
            AgenteId: agente?.Id,
            AgenteNome: agente?.Nome,
            CodigoAtivacao: rawCode,
            CodigoAtivacaoExpiraEm: expiraEm
        ));
    }

    // =========================================================================
    // SEÃ‡ÃƒO D: Teste de ConexÃ£o de Banco ERP (Socket/Conectividade)
    // =========================================================================
    [HttpPost("testar-conexao-erp")]
    public async Task<IActionResult> TestarConexaoErp([FromBody] TestarConexaoErpRequest r)
    {
        var host = string.IsNullOrWhiteSpace(r.Host) ? "127.0.0.1" : r.Host.Trim();
        var port = r.Porta > 0 ? r.Porta : (r.TipoBanco == "Firebird" ? 3050 : 5432);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var tcp = new TcpClient();
            var connectTask = tcp.ConnectAsync(host, port);
            if (await Task.WhenAny(connectTask, Task.Delay(3500)) == connectTask)
            {
                await connectTask;
                sw.Stop();
                return Ok(new
                {
                    sucesso = true,
                    mensagem = $"Servidor {r.TipoBanco} ({host}:{port}) acessÃ­vel na rede (latÃªncia: {sw.ElapsedMilliseconds} ms)."
                });
            }
            else
            {
                return Ok(new
                {
                    sucesso = false,
                    mensagem = $"Tempo limite esgotado ao tentar alcanÃ§ar o servidor {r.TipoBanco} em {host}:{port}. Verifique se o serviÃ§o estÃ¡ em execuÃ§Ã£o e a porta liberada no firewall."
                });
            }
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                sucesso = false,
                mensagem = $"Falha de conexÃ£o com {host}:{port}: {ex.Message}"
            });
        }
    }

    // =========================================================================
    // SEÃ‡ÃƒO C: Filiais Globais
    // =========================================================================
    [HttpGet("filiais")]
    public async Task<IActionResult> ListarFiliais()
    {
        var filiais = await db.Filiais
            .Include(f => f.Tenant)
            .OrderByDescending(f => f.DataCriacao)
            .Select(f => new
            {
                f.Id,
                f.TenantId,
                EmpresaNome = f.Tenant.NomeFantasia,
                f.Codigo,
                f.Nome,
                f.Cnpj,
                f.Cidade,
                f.Uf,
                f.Ativa,
                f.Token,
                f.TokenRevogado,
                f.DataCriacao
            })
            .ToListAsync();

        return Ok(filiais);
    }

    [HttpPost("tenants/{tenantId:guid}/filiais")]
    public async Task<IActionResult> CriarFilialParaTenant(Guid tenantId, [FromBody] CreateBranchRequest r)
    {
        var tenant = await db.Tenants.FindAsync(tenantId);
        if (tenant == null) return NotFound(new { erro = "Empresa nÃ£o encontrada." });

        if (string.IsNullOrWhiteSpace(r.Nome))
            return BadRequest(new { erro = "O nome da filial Ã© obrigatÃ³rio." });

        var codigo = string.IsNullOrWhiteSpace(r.Codigo) ? $"FIL-{Random.Shared.Next(10, 99)}" : r.Codigo.Trim();
        var token = $"FLK-{Guid.NewGuid():N}"[..24].ToUpperInvariant();

        var filial = new Filial
        {
            TenantId = tenant.Id,
            Nome = r.Nome.Trim(),
            Codigo = codigo,
            Cnpj = r.Cnpj?.Trim() ?? string.Empty,
            Cidade = r.Cidade?.Trim() ?? string.Empty,
            Uf = r.Uf?.Trim()?.ToUpperInvariant() ?? string.Empty,
            Ativa = true,
            Token = token,
            TokenGeradoEm = DateTime.UtcNow,
            TokenRevogado = false
        };

        db.Filiais.Add(filial);
        audit.Add("FilialCriadaAdmin", "Filial", filial.Id.ToString(), new { filial.Nome, filial.Codigo }, tenant.Id);
        await db.SaveChangesAsync();

        return Ok(new
        {
            filial.Id,
            filial.TenantId,
            filial.Nome,
            filial.Codigo,
            filial.Cnpj,
            filial.Token,
            filial.Ativa
        });
    }

    // =========================================================================
    // SEÃ‡ÃƒO E: UsuÃ¡rios e Acessos de Clientes
    // =========================================================================
    [HttpGet("usuarios")]
    public async Task<IActionResult> ListarUsuarios()
    {
        var usuarios = await db.Usuarios
            .Include(u => u.Tenant)
            .OrderByDescending(u => u.DataCriacao)
            .Select(u => new
            {
                u.Id,
                u.TenantId,
                EmpresaNome = u.Tenant != null ? u.Tenant.NomeFantasia : "Plataforma SaaS",
                u.Nome,
                u.Email,
                u.Perfil,
                u.Ativo,
                u.UltimoLogin,
                u.DataCriacao
            })
            .ToListAsync();

        return Ok(usuarios);
    }

    [HttpPost("tenants/{tenantId:guid}/usuarios")]
    public async Task<IActionResult> CriarUsuarioParaTenant(Guid tenantId, [FromBody] CreateUserRequest r)
    {
        var tenant = await db.Tenants.FindAsync(tenantId);
        if (tenant == null) return NotFound(new { erro = "Empresa nÃ£o encontrada." });

        if (string.IsNullOrWhiteSpace(r.Email) || !r.Email.Contains('@'))
            return BadRequest(new { erro = "E-mail de acesso invÃ¡lido." });

        var email = r.Email.Trim().ToLowerInvariant();
        if (await db.Usuarios.AnyAsync(u => u.Email.ToLower() == email))
            return BadRequest(new { erro = $"JÃ¡ existe um usuÃ¡rio cadastrado com o e-mail '{email}'." });

        var senhaTexto = string.IsNullOrWhiteSpace(r.Senha)
            ? $"Nexo#{Random.Shared.Next(1000, 9999)}!"
            : r.Senha.Trim();

        var usuario = new Usuario
        {
            TenantId = tenant.Id,
            Nome = string.IsNullOrWhiteSpace(r.Nome) ? "UsuÃ¡rio da Loja" : r.Nome.Trim(),
            Email = email,
            SenhaHash = BCrypt.Net.BCrypt.HashPassword(senhaTexto),
            Perfil = string.IsNullOrWhiteSpace(r.Perfil) ? "TenantAdmin" : r.Perfil.Trim(),
            Ativo = true
        };

        db.Usuarios.Add(usuario);
        audit.Add("UsuarioCriadoAdmin", "Usuario", usuario.Id.ToString(), new { usuario.Email, usuario.Perfil }, tenant.Id);
        await db.SaveChangesAsync();

        return Ok(new
        {
            usuario.Id,
            usuario.TenantId,
            usuario.Nome,
            usuario.Email,
            usuario.Perfil,
            SenhaInicial = senhaTexto
        });
    }

    [HttpPut("usuarios/{userId:guid}/status")]
    public async Task<IActionResult> AlterarStatusUsuario(Guid userId, [FromBody] UpdateTenantStatusRequest r)
    {
        var usuario = await db.Usuarios.FindAsync(userId);
        if (usuario == null) return NotFound(new { erro = "UsuÃ¡rio nÃ£o encontrado." });

        if (usuario.Perfil == "PlatformAdmin")
            return BadRequest(new { erro = "O usuÃ¡rio administrador principal da plataforma nÃ£o pode ser desativado." });

        usuario.Ativo = r.Status.Equals("Ativo", StringComparison.OrdinalIgnoreCase);
        audit.Add("StatusUsuarioAlteradoAdmin", "Usuario", usuario.Id.ToString(), new { usuario.Email, usuario.Ativo }, usuario.TenantId);
        await db.SaveChangesAsync();

        return Ok(new { mensagem = $"Status do usuÃ¡rio '{usuario.Email}' atualizado para {(usuario.Ativo ? "Ativo" : "Inativo")}." });
    }

    [HttpPost("usuarios/{userId:guid}/resetar-senha")]
    public async Task<IActionResult> ResetarSenhaUsuario(Guid userId)
    {
        var usuario = await db.Usuarios.FindAsync(userId);
        if (usuario == null) return NotFound(new { erro = "UsuÃ¡rio nÃ£o encontrado." });

        var novaSenhaTexto = $"Nexo#{Random.Shared.Next(1000, 9999)}!";
        usuario.SenhaHash = BCrypt.Net.BCrypt.HashPassword(novaSenhaTexto);
        usuario.BloqueadoAte = null;
        usuario.FalhasLogin = 0;

        audit.Add("SenhaResetadaAdmin", "Usuario", usuario.Id.ToString(), new { usuario.Email }, usuario.TenantId);
        await db.SaveChangesAsync();

        return Ok(new
        {
            usuario.Id,
            usuario.Email,
            NovaSenhaTemporaria = novaSenhaTexto,
            mensagem = $"Senha redefinida com sucesso para o usuÃ¡rio '{usuario.Email}'."
        });
    }

    // =========================================================================
    // SEÃ‡ÃƒO F: Sincronizadores e Tokens do SINc
    // =========================================================================
    [HttpGet("sincronizadores")]
    public async Task<IActionResult> ListarSincronizadores()
    {
        var agentes = await db.Agentes
            .Include(a => a.Tenant)
            .OrderByDescending(a => a.DataCriacao)
            .Select(a => new
            {
                a.Id,
                a.TenantId,
                EmpresaNome = a.Tenant.NomeFantasia,
                a.FilialId,
                a.Nome,
                a.Status,
                a.TipoBanco,
                a.Versao,
                a.NomeMaquina,
                a.ChavePrefixo,
                a.Revogado,
                a.UltimoHeartbeat,
                a.UltimaSincronizacao,
                a.CodigoAtivacaoUsado,
                CodigoExpirado = a.CodigoAtivacaoExpiracao.HasValue && a.CodigoAtivacaoExpiracao.Value < DateTime.UtcNow,
                a.DataCriacao
            })
            .ToListAsync();

        return Ok(agentes);
    }

    [HttpPost("tenants/{tenantId:guid}/agentes")]
    public async Task<IActionResult> CriarAgenteParaTenant(Guid tenantId, [FromBody] CriarAgenteAdminRequest r)
    {
        var tenant = await db.Tenants.FindAsync(tenantId);
        if (tenant == null) return NotFound(new { erro = "Empresa nÃ£o encontrada." });

        if (string.IsNullOrWhiteSpace(r.Nome))
            return BadRequest(new { erro = "O nome da instalaÃ§Ã£o do sincronizador Ã© obrigatÃ³rio." });

        var tipoBanco = r.TipoBanco.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase) ? "PostgreSQL" : "Firebird";

        var rawCode = $"ACTV-{Secrets.AccessCode()[..9]}";
        var codeHash = Secrets.Sha256Hex(Secrets.NormalizeCode(rawCode));
        var expiraEm = DateTime.UtcNow.AddHours(48);

        var bancoConfigObj = new
        {
            tipo = tipoBanco,
            host = string.IsNullOrWhiteSpace(r.Host) ? (tipoBanco == "Firebird" ? "127.0.0.1" : "localhost") : r.Host.Trim(),
            porta = r.Porta ?? (tipoBanco == "Firebird" ? 3050 : 5432),
            caminhoOuBanco = string.IsNullOrWhiteSpace(r.CaminhoOuBanco) ? (tipoBanco == "Firebird" ? @"C:\DATACASH\DADOS\BM.FDB" : "sistema_comercial") : r.CaminhoOuBanco.Trim(),
            usuario = string.IsNullOrWhiteSpace(r.UsuarioBanco) ? (tipoBanco == "Firebird" ? "SYSDBA" : "postgres") : r.UsuarioBanco.Trim(),
            senha = r.SenhaBanco ?? string.Empty,
            charset = string.IsNullOrWhiteSpace(r.Charset) ? "NONE" : r.Charset.Trim(),
            dialeto = r.Dialeto ?? 3
        };

        var agente = new Agente
        {
            TenantId = tenant.Id,
            Nome = r.Nome.Trim(),
            Status = "PendenteAtivacao",
            TipoBanco = tipoBanco,
            BancoConfigJson = JsonSerializer.Serialize(bancoConfigObj),
            CodigoAtivacaoHash = codeHash,
            CodigoAtivacaoExpiracao = expiraEm,
            CodigoAtivacaoUsado = false,
            IntervaloEstoqueMin = r.IntervaloEstoqueMin is > 0 ? r.IntervaloEstoqueMin.Value : 5,
            IntervaloProdutosMin = r.IntervaloProdutosMin is > 0 ? r.IntervaloProdutosMin.Value : 30,
            IntervaloFornecedoresMin = r.IntervaloFornecedoresMin is > 0 ? r.IntervaloFornecedoresMin.Value : 30,
            IntervaloHeartbeatSeg = r.IntervaloHeartbeatSeg is > 0 ? r.IntervaloHeartbeatSeg.Value : 60
        };

        db.Agentes.Add(agente);
        audit.Add("AgenteInstalacaoCriada", "Agente", agente.Id.ToString(), new
        {
            agente.Nome,
            TipoBanco = tipoBanco,
            ExpiraEm = expiraEm
        }, tenant.Id);

        await db.SaveChangesAsync();

        return Ok(new CriarAgenteAdminResponse(
            agente.Id,
            tenant.Id,
            agente.Nome,
            rawCode,
            expiraEm,
            tipoBanco
        ));
    }

    [HttpGet("tenants/{tenantId:guid}/agentes")]
    public async Task<IActionResult> ListarAgentesTenant(Guid tenantId)
    {
        var agentes = await db.Agentes
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.DataCriacao)
            .Select(a => new
            {
                a.Id,
                a.TenantId,
                a.Nome,
                a.Status,
                a.TipoBanco,
                a.Versao,
                a.NomeMaquina,
                a.ChavePrefixo,
                a.Revogado,
                a.UltimoHeartbeat,
                a.UltimaSincronizacao,
                a.CodigoAtivacaoUsado,
                CodigoExpirado = a.CodigoAtivacaoExpiracao.HasValue && a.CodigoAtivacaoExpiracao.Value < DateTime.UtcNow,
                a.ChaveRotacionadaEm,
                a.DataCriacao
            })
            .ToListAsync();

        return Ok(agentes);
    }

    [HttpPost("sincronizadores/{id:guid}/revogar")]
    [HttpPost("agentes/{id:guid}/revogar")]
    public async Task<IActionResult> RevogarSincronizador(Guid id)
    {
        // 1. Tenta localizar por Id do Agente, por Id da Filial ou por Id do Tenant
        var agente = await db.Agentes.Include(a => a.Filial).Include(a => a.Tenant).FirstOrDefaultAsync(a => a.Id == id)
                     ?? await db.Agentes.Include(a => a.Filial).Include(a => a.Tenant).FirstOrDefaultAsync(a => a.FilialId == id)
                     ?? await db.Agentes.Include(a => a.Filial).Include(a => a.Tenant).FirstOrDefaultAsync(a => a.TenantId == id && !a.Revogado);

        if (agente == null)
        {
            // Verifica se o ID informado pertence a uma filial com token para revogaÃ§Ã£o
            var filial = await db.Filiais.FirstOrDefaultAsync(f => f.Id == id);
            if (filial != null)
            {
                filial.TokenRevogado = true;
                filial.DataAtualizacao = DateTime.UtcNow;
                await db.SaveChangesAsync();
                return Ok(new
                {
                    mensagem = $"Token de integraÃ§Ã£o da filial '{filial.Nome}' revogado com sucesso.",
                    status = "Revogado",
                    filialId = filial.Id
                });
            }

            return NotFound(new { erro = $"Sincronizador ou instalaÃ§Ã£o com identificador '{id}' nÃ£o foi encontrado no sistema." });
        }

        // IdempotÃªncia: se jÃ¡ estiver revogado, responde com sucesso coerente sem inconsistÃªncias
        if (agente.Revogado)
        {
            return Ok(new
            {
                mensagem = $"O sincronizador '{agente.Nome}' jÃ¡ se encontrava previamente revogado.",
                agenteId = agente.Id,
                status = "Revogado",
                revogado = true,
                revogadoEm = agente.RevogadoEm ?? agente.DataAtualizacao
            });
        }

        var responsavel = User.FindFirstValue(ClaimTypes.Name)
                          ?? User.FindFirstValue("name")
                          ?? User.FindFirstValue("uid")
                          ?? "Administrador Geral";

        var agora = DateTime.UtcNow;
        agente.Revogado = true;
        agente.Status = "Revogado";
        agente.RevogadoEm = agora;
        agente.RevogadoPor = responsavel;
        agente.DataAtualizacao = agora;

        // Invalida credenciais ativas para impedir qualquer uso posterior
        agente.ChaveHash = string.Empty;
        agente.CodigoAtivacaoHash = string.Empty;
        agente.CodigoAtivacaoExpiracao = null;

        // Se vinculado a uma filial, revoga tambÃ©m o token da filial
        if (agente.Filial != null)
        {
            agente.Filial.TokenRevogado = true;
            agente.Filial.DataAtualizacao = agora;
        }
        else if (agente.FilialId.HasValue)
        {
            var fil = await db.Filiais.FindAsync(agente.FilialId.Value);
            if (fil != null)
            {
                fil.TokenRevogado = true;
                fil.DataAtualizacao = agora;
            }
        }

        audit.Add("AgenteRevogado", "Agente", agente.Id.ToString(), new
        {
            agente.Nome,
            RevogadoEm = agora,
            Responsavel = responsavel,
            agente.TenantId
        }, agente.TenantId);

        await db.SaveChangesAsync();

        return Ok(new
        {
            mensagem = $"Sincronizador '{agente.Nome}' revogado com sucesso. Todas as sincronizaÃ§Ãµes foram bloqueadas imediatamente.",
            agenteId = agente.Id,
            status = "Revogado",
            revogado = true,
            revogadoEm = agora,
            responsavel
        });
    }

    [HttpPost("sincronizadores/{id:guid}/rotacionar")]
    [HttpPost("agentes/{id:guid}/rotacionar")]
    public async Task<IActionResult> RotacionarCredencialAgente(Guid id)
    {
        var agente = await db.Agentes.FirstOrDefaultAsync(a => a.Id == id)
                     ?? await db.Agentes.FirstOrDefaultAsync(a => a.FilialId == id)
                     ?? await db.Agentes.FirstOrDefaultAsync(a => a.TenantId == id && !a.Revogado);

        if (agente == null) return NotFound(new { erro = "InstalaÃ§Ã£o do sincronizador nÃ£o encontrada." });

        var (chave, hash, prefix) = Security.AgentKey.Generate(agente.Id);
        var agora = DateTime.UtcNow;
        agente.ChaveHash = hash;
        agente.ChavePrefixo = prefix;
        agente.ChaveRotacionadaEm = agora;
        agente.Revogado = false;
        agente.Status = "Online";
        agente.DataAtualizacao = agora;

        audit.Add("CredencialAgenteRotacionada", "Agente", agente.Id.ToString(), new { Prefixo = prefix }, agente.TenantId);
        await db.SaveChangesAsync();

        return Ok(new RotacionarCredencialResponse(
            agente.Id,
            chave,
            prefix,
            agente.ChaveRotacionadaEm.Value
        ));
    }

    [HttpPost("sincronizadores/{id:guid}/gerar-codigo-ativacao")]
    [HttpPost("sincronizadores/{id:guid}/novo-codigo")]
    [HttpPost("agentes/{id:guid}/novo-codigo")]
    public async Task<IActionResult> GerarNovoCodigoAtivacao(Guid id)
    {
        var agente = await db.Agentes.FirstOrDefaultAsync(a => a.Id == id)
                     ?? await db.Agentes.FirstOrDefaultAsync(a => a.FilialId == id)
                     ?? await db.Agentes.FirstOrDefaultAsync(a => a.TenantId == id && !a.Revogado);

        if (agente == null) return NotFound(new { erro = "InstalaÃ§Ã£o do sincronizador nÃ£o encontrada." });

        var rawCode = $"ACTV-{Secrets.AccessCode()[..9]}";
        var agora = DateTime.UtcNow;
        agente.CodigoAtivacaoHash = Secrets.Sha256Hex(Secrets.NormalizeCode(rawCode));
        agente.CodigoAtivacaoExpiracao = agora.AddHours(48);
        agente.CodigoAtivacaoUsado = false;
        agente.TentativasAtivacaoInvalidas = 0;
        agente.BloqueadoAte = null;
        agente.Status = "PendenteAtivacao";
        agente.Revogado = false;
        agente.DataAtualizacao = agora;

        audit.Add("NovoCodigoAtivacaoGerado", "Agente", agente.Id.ToString(), new { ExpiraEm = agente.CodigoAtivacaoExpiracao }, agente.TenantId);
        await db.SaveChangesAsync();

        return Ok(new
        {
            agente.Id,
            agente.TenantId,
            codigoAtivacao = rawCode,
            CodigoAtivacao = rawCode,
            expiraEm = agente.CodigoAtivacaoExpiracao.Value,
            ExpiraEm = agente.CodigoAtivacaoExpiracao.Value
        });
    }

    // =========================================================================
    // SEÃ‡ÃƒO G: Planos Comerciais
    // =========================================================================
    [HttpGet("plans")]
    public async Task<IActionResult> Plans() => Ok(await db.Planos.OrderBy(p => p.Mensalidade).ToListAsync());

    [HttpPost("plans")]
    public async Task<IActionResult> CreatePlan([FromBody] CreatePlanRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Nome) || r.Mensalidade < 0 || r.MaxUsuarios < 1) return BadRequest(new { erro = "Nome, mensalidade e limite de usuÃ¡rios vÃ¡lidos sÃ£o obrigatÃ³rios." });
        var p = new Plano
        {
            Nome = r.Nome.Trim(),
            Mensalidade = r.Mensalidade,
            MaxUsuarios = r.MaxUsuarios,
            MaxFiliais = r.MaxFiliais,
            MaxFornecedores = r.MaxFornecedores,
            MaxCotacoesMes = r.MaxCotacoesMes,
            RecursosJson = string.IsNullOrWhiteSpace(r.RecursosJson) ? "{}" : r.RecursosJson
        };
        db.Planos.Add(p);
        audit.Add("PlanoCriado", "Plano", p.Id.ToString(), new { p.Nome, p.Mensalidade });
        await db.SaveChangesAsync();
        return Ok(p);
    }
}
