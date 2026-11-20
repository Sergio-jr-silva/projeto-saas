using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Nexo.Agent.Core.Connectors;

public enum PostgreSqlDiagnosticStatus
{
    Conectado,
    ServidorAcessivel,
    FalhaConexao,
    FalhaAutenticacao,
    BancoIndisponivel,
    ProblemaSsl,
    CamposInvalidos
}

public class PostgreSqlDiagnosticParams
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = string.Empty;
    public string User { get; set; } = "postgres";
    public string Password { get; set; } = string.Empty;
    public string SslMode { get; set; } = "Prefer";
    public int TimeoutSeconds { get; set; } = 15;
    public string Schema { get; set; } = "public";

    public static PostgreSqlDiagnosticParams FromConfiguration(IConfiguration config)
    {
        return new PostgreSqlDiagnosticParams
        {
            Host = config["PostgreSql:Host"] ?? "127.0.0.1",
            Port = int.TryParse(config["PostgreSql:Port"], out var p) ? p : 5432,
            Database = config["PostgreSql:Database"] ?? string.Empty,
            User = config["PostgreSql:User"] ?? "postgres",
            Password = config["PostgreSql:Password"] ?? string.Empty,
            SslMode = config["PostgreSql:SslMode"] ?? "Prefer",
            TimeoutSeconds = int.TryParse(config["PostgreSql:Timeout"], out var t) ? t : 15,
            Schema = config["PostgreSql:Schema"] ?? "public"
        };
    }
}

public class PostgreSqlDiagnosticResult
{
    public PostgreSqlDiagnosticStatus Status { get; set; } = PostgreSqlDiagnosticStatus.CamposInvalidos;
    public bool Sucesso => Status == PostgreSqlDiagnosticStatus.Conectado;

    public string StatusBadge => Status switch
    {
        PostgreSqlDiagnosticStatus.Conectado => "Conectado",
        PostgreSqlDiagnosticStatus.ServidorAcessivel => "Servidor acessível",
        PostgreSqlDiagnosticStatus.FalhaConexao => "Falha de conexão",
        PostgreSqlDiagnosticStatus.FalhaAutenticacao => "Falha de autenticação",
        PostgreSqlDiagnosticStatus.BancoIndisponivel => "Banco indisponível",
        PostgreSqlDiagnosticStatus.ProblemaSsl => "Problema SSL",
        PostgreSqlDiagnosticStatus.CamposInvalidos => "Campos inválidos",
        _ => "Desconhecido"
    };

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Database { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string SslMode { get; set; } = "Prefer";
    public int TimeoutSeconds { get; set; } = 15;
    public long ElapsedMilliseconds { get; set; }
    public string EtapaFalha { get; set; } = string.Empty;
    public string MensagemTecnica { get; set; } = string.Empty;
    public string SugestaoDiagnostico { get; set; } = string.Empty;
    public string? VersaoPostgreSql { get; set; }
    public List<string> LogsEtapas { get; set; } = new();

    public void AddLog(string msg)
    {
        LogsEtapas.Add($"[{DateTime.Now:HH:mm:ss.fff}] {msg}");
    }
}

public class PostgreSqlDiagnosticService
{
    private readonly ILogger<PostgreSqlDiagnosticService>? _logger;

    public PostgreSqlDiagnosticService(ILogger<PostgreSqlDiagnosticService>? logger = null)
    {
        _logger = logger;
    }

    public async Task<PostgreSqlDiagnosticResult> DiagnosticarAsync(
        PostgreSqlDiagnosticParams parameters,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new PostgreSqlDiagnosticResult
        {
            Host = parameters.Host?.Trim() ?? string.Empty,
            Port = parameters.Port,
            Database = parameters.Database?.Trim() ?? string.Empty,
            User = parameters.User?.Trim() ?? string.Empty,
            SslMode = string.IsNullOrWhiteSpace(parameters.SslMode) ? "Prefer" : parameters.SslMode.Trim(),
            TimeoutSeconds = parameters.TimeoutSeconds > 0 ? parameters.TimeoutSeconds : 15
        };

        // -------------------------------------------------------------
        // ETAPA 1: Validação de campos obrigatórios
        // -------------------------------------------------------------
        result.AddLog("Etapa 1/7: Validando preenchimento dos campos obrigatórios...");
        if (string.IsNullOrWhiteSpace(result.Host))
        {
            return FinalizarFalha(result, sw,
                PostgreSqlDiagnosticStatus.CamposInvalidos,
                "Validação de Parâmetros (Host)",
                "O campo 'Host / Servidor' é obrigatório e não foi informado.",
                "Informe o endereço IP (ex: 127.0.0.1 ou 192.168.1.100) ou nome de host do servidor PostgreSQL.");
        }

        if (result.Port <= 0 || result.Port > 65535)
        {
            return FinalizarFalha(result, sw,
                PostgreSqlDiagnosticStatus.CamposInvalidos,
                "Validação de Parâmetros (Porta)",
                $"Porta '{result.Port}' inválida. A porta deve ser um valor numérico entre 1 e 65535.",
                "Utilize a porta padrão do PostgreSQL (5432) ou a porta configurada no seu ambiente.");
        }

        if (string.IsNullOrWhiteSpace(result.Database))
        {
            return FinalizarFalha(result, sw,
                PostgreSqlDiagnosticStatus.CamposInvalidos,
                "Validação de Parâmetros (Banco)",
                "O campo 'Nome do Banco de Dados' é obrigatório e não foi informado.",
                "Informe o nome do banco de dados existente no servidor PostgreSQL (ex: 'erp_dados' ou 'sistema_comercial').");
        }

        if (string.IsNullOrWhiteSpace(result.User))
        {
            return FinalizarFalha(result, sw,
                PostgreSqlDiagnosticStatus.CamposInvalidos,
                "Validação de Parâmetros (Usuário)",
                "O campo 'Usuário' é obrigatório e não foi informado.",
                "Informe o nome do usuário do PostgreSQL autorizado a ler o banco (ex: 'postgres').");
        }
        result.AddLog($"Campos válidos: Host='{result.Host}', Porta={result.Port}, Banco='{result.Database}', Usuário='{result.User}', SSL={result.SslMode}, Timeout={result.TimeoutSeconds}s.");

        // -------------------------------------------------------------
        // ETAPA 2: Validação de formato de Host
        // -------------------------------------------------------------
        result.AddLog("Etapa 2/7: Validando formato do Host e caracteres...");
        if (result.Host.Contains(" ") || result.Host.Contains("/") || result.Host.Contains("\\"))
        {
            return FinalizarFalha(result, sw,
                PostgreSqlDiagnosticStatus.CamposInvalidos,
                "Formato de Host Inválido",
                $"O Host '{result.Host}' contém caracteres inválidos (espaços ou barras).",
                "Certifique-se de preencher apenas o hostname ou IP puro, sem protocolo (http://) ou caminhos de diretório.");
        }

        // -------------------------------------------------------------
        // ETAPA 3: Resolução de DNS
        // -------------------------------------------------------------
        result.AddLog($"Etapa 3/7: Testando resolução de DNS para '{result.Host}'...");
        IPAddress[] hostAddresses;
        try
        {
            hostAddresses = await Dns.GetHostAddressesAsync(result.Host, ct);
            if (hostAddresses == null || hostAddresses.Length == 0)
            {
                return FinalizarFalha(result, sw,
                    PostgreSqlDiagnosticStatus.FalhaConexao,
                    "Resolução DNS",
                    $"O resolvedor DNS não retornou nenhum endereço IP para '{result.Host}'.",
                    $"Verifique se o nome do host '{result.Host}' está correto no arquivo hosts ou no servidor DNS da rede local.");
            }
            result.AddLog($"DNS resolvido com sucesso: {string.Join(", ", (IEnumerable<IPAddress>)hostAddresses)}.");
        }
        catch (SocketException sockEx)
        {
            return FinalizarFalha(result, sw,
                PostgreSqlDiagnosticStatus.FalhaConexao,
                "Resolução DNS",
                $"Falha de DNS ({sockEx.SocketErrorCode}): {sockEx.Message}",
                $"Não foi possível resolver o endereço '{result.Host}'. Verifique se o computador possui conectividade de rede e se o DNS está operando.");
        }
        catch (Exception ex)
        {
            return FinalizarFalha(result, sw,
                PostgreSqlDiagnosticStatus.FalhaConexao,
                "Resolução DNS",
                ex.Message,
                $"Erro ao tentar resolver o nome do host '{result.Host}'. Verifique a digitação do servidor.");
        }

        // -------------------------------------------------------------
        // ETAPA 4: Acessibilidade da porta TCP
        // -------------------------------------------------------------
        result.AddLog($"Etapa 4/7: Testando acessibilidade da porta TCP {result.Port} em '{result.Host}' (Timeout: {result.TimeoutSeconds}s)...");
        try
        {
            using var tcpClient = new TcpClient();
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(result.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            var connectTask = tcpClient.ConnectAsync(result.Host, result.Port);
            var delayTask = Task.Delay(TimeSpan.FromSeconds(result.TimeoutSeconds), linkedCts.Token);

            var completedTask = await Task.WhenAny(connectTask, delayTask);
            if (completedTask == delayTask)
            {
                return FinalizarFalha(result, sw,
                    PostgreSqlDiagnosticStatus.FalhaConexao,
                    "Porta TCP (Timeout)",
                    $"Tempo limite expirado ({result.TimeoutSeconds}s) aguardando resposta da porta TCP {result.Port} em '{result.Host}'.",
                    $"A porta {result.Port} não respondeu. Verifique se o Firewall do Windows no servidor libera conexões de entrada na porta {result.Port}, se o servidor está ligado e se a rota ou VPN está ativa.");
            }

            await connectTask; // Garante lançamento de exceção de recusa de conexão se houver
            result.AddLog($"Porta TCP {result.Port} aberta e alcançada com sucesso.");
        }
        catch (SocketException sockEx)
        {
            var isRefused = sockEx.SocketErrorCode == SocketError.ConnectionRefused ||
                            sockEx.NativeErrorCode == 10061 ||
                            sockEx.Message.Contains("refused", StringComparison.OrdinalIgnoreCase) ||
                            sockEx.Message.Contains("recusada", StringComparison.OrdinalIgnoreCase);

            var sugestao = isRefused
                ? $"A conexão foi recusada na porta {result.Port}. Verifique se o serviço do PostgreSQL está iniciado e rodando no computador de destino e se o parâmetro 'port' no postgresql.conf é realmente {result.Port}."
                : $"Falha ao estabelecer conexão de rede TCP ({sockEx.SocketErrorCode}): {sockEx.Message}. Verifique a rota de rede e o cabo/roteador.";

            return FinalizarFalha(result, sw,
                PostgreSqlDiagnosticStatus.FalhaConexao,
                "Porta TCP (Conexão Recusada)",
                sockEx.Message,
                sugestao);
        }
        catch (Exception ex)
        {
            return FinalizarFalha(result, sw,
                PostgreSqlDiagnosticStatus.FalhaConexao,
                "Porta TCP",
                ex.Message,
                $"Erro inesperado ao conectar à porta TCP {result.Port}: {ex.Message}");
        }

        // -------------------------------------------------------------
        // ETAPA 5: Negociação de Conexão Npgsql e Autenticação
        // -------------------------------------------------------------
        result.AddLog("Etapa 5/7: Estabelecendo handshake Npgsql e autenticando credenciais...");
        var npgsqlSslMode = NormalizarSslMode(result.SslMode);

        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = result.Host,
            Port = result.Port,
            Database = result.Database,
            Username = result.User,
            Password = parameters.Password ?? string.Empty,
            SslMode = npgsqlSslMode,
            Timeout = result.TimeoutSeconds,
            CommandTimeout = Math.Min(15, result.TimeoutSeconds),
            SearchPath = string.IsNullOrWhiteSpace(parameters.Schema) ? "public" : parameters.Schema.Trim()
        };

        // Redigir senha para logs
        result.AddLog($"ConnectionString configurada: Host={csb.Host};Port={csb.Port};Database={csb.Database};Username={csb.Username};Password=***;SslMode={csb.SslMode};Timeout={csb.Timeout}s");

        NpgsqlConnection? conn = null;
        try
        {
            conn = new NpgsqlConnection(csb.ConnectionString);
            await conn.OpenAsync(ct);
            result.AddLog("Handshake de conexão e autenticação com o PostgreSQL bem-sucedidos!");
        }
        catch (PostgresException pgEx)
        {
            result.AddLog($"PostgreSQL retornou erro de protocolo: SqlState={pgEx.SqlState}, Msg={pgEx.MessageText}");
            var safeMsg = $"[{pgEx.SqlState}] {pgEx.MessageText}";

            // 28P01: password authentication failed
            if (pgEx.SqlState == "28P01" ||
                pgEx.MessageText.Contains("password authentication failed", StringComparison.OrdinalIgnoreCase) ||
                pgEx.MessageText.Contains("autenticação por senha falhou", StringComparison.OrdinalIgnoreCase))
            {
                return FinalizarFalha(result, sw,
                    PostgreSqlDiagnosticStatus.FalhaAutenticacao,
                    "Autenticação de Usuário",
                    safeMsg,
                    $"A senha informada para o usuário '{result.User}' é incorreta ou o usuário não tem permissão de login no PostgreSQL. Verifique usuário e senha.");
            }

            // 3D000: database does not exist
            if (pgEx.SqlState == "3D000" ||
                pgEx.MessageText.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
                pgEx.MessageText.Contains("não existe", StringComparison.OrdinalIgnoreCase))
            {
                return FinalizarFalha(result, sw,
                    PostgreSqlDiagnosticStatus.BancoIndisponivel,
                    "Existência do Banco de Dados",
                    safeMsg,
                    $"O banco de dados '{result.Database}' não existe no servidor PostgreSQL. Verifique a digitação do nome do banco ou crie o banco no PostgreSQL.");
            }

            // 28000: invalid_authorization_specification / no pg_hba.conf entry
            if (pgEx.SqlState == "28000" ||
                pgEx.MessageText.Contains("no pg_hba.conf entry", StringComparison.OrdinalIgnoreCase) ||
                pgEx.MessageText.Contains("pg_hba.conf", StringComparison.OrdinalIgnoreCase))
            {
                return FinalizarFalha(result, sw,
                    PostgreSqlDiagnosticStatus.ServidorAcessivel,
                    "Regras de Acesso pg_hba.conf",
                    safeMsg,
                    $"O PostgreSQL recusou a conexão vinda deste endereço de IP. Abra o arquivo 'pg_hba.conf' no diretório de dados do servidor PostgreSQL e adicione uma regra 'host' permitindo o IP do cliente.");
            }

            // Outras exceções do PostgreSQL
            return FinalizarFalha(result, sw,
                PostgreSqlDiagnosticStatus.ServidorAcessivel,
                $"PostgreSql Error ({pgEx.SqlState})",
                safeMsg,
                $"O servidor PostgreSQL respondeu, mas rejeitou a conexão: {pgEx.MessageText}. Verifique as permissões do usuário e configuração do servidor.");
        }
        catch (NpgsqlException npgEx)
        {
            var msg = SanitizeMessage(npgEx.Message, parameters.Password);
            result.AddLog($"Driver Npgsql capturou exceção: {msg}");

            if (msg.Contains("SSL", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("certificate", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("security package", StringComparison.OrdinalIgnoreCase) ||
                npgEx.InnerException is System.Security.Authentication.AuthenticationException)
            {
                return FinalizarFalha(result, sw,
                    PostgreSqlDiagnosticStatus.ProblemaSsl,
                    "Negociação SSL/TLS",
                    msg,
                    "Falha na negociação de segurança SSL/TLS. Tente alterar o parâmetro 'Modo SSL' para 'Disable' (caso o servidor não use certificados criptografados) ou revise os certificados instalados.");
            }

            if (msg.Contains("Timeout", StringComparison.OrdinalIgnoreCase) ||
                npgEx.InnerException is TimeoutException)
            {
                return FinalizarFalha(result, sw,
                    PostgreSqlDiagnosticStatus.FalhaConexao,
                    "Timeout Npgsql",
                    msg,
                    $"Tempo limite esgotado ({result.TimeoutSeconds}s) durante a negociação com o PostgreSQL. O servidor pode estar sobrecarregado ou bloqueando pacotes.");
            }

            return FinalizarFalha(result, sw,
                PostgreSqlDiagnosticStatus.ServidorAcessivel,
                "Driver Npgsql",
                msg,
                $"Falha ao negociar protocolo com o PostgreSQL: {msg}. Verifique a compatibilidade do driver.");
        }
        catch (Exception ex)
        {
            var safeMsg = SanitizeMessage(ex.Message, parameters.Password);
            return FinalizarFalha(result, sw,
                PostgreSqlDiagnosticStatus.FalhaConexao,
                "Conexão",
                safeMsg,
                $"Erro inesperado na conexão: {safeMsg}");
        }

        // -------------------------------------------------------------
        // ETAPA 6: Consulta de verificação de leitura leve (SELECT 1;)
        // -------------------------------------------------------------
        result.AddLog("Etapa 6/7: Executando consulta de validação de leitura estrita (SELECT 1;)...");
        try
        {
            await using var cmdPing = new NpgsqlCommand("SELECT 1;", conn);
            cmdPing.CommandTimeout = Math.Min(10, result.TimeoutSeconds);
            var pingVal = await cmdPing.ExecuteScalarAsync(ct);
            if (pingVal == null || Convert.ToInt32(pingVal) != 1)
            {
                await conn.DisposeAsync();
                return FinalizarFalha(result, sw,
                    PostgreSqlDiagnosticStatus.ServidorAcessivel,
                    "Execução SELECT 1",
                    "A consulta 'SELECT 1;' retornou um valor inesperado ou nulo.",
                    "O banco foi acessado, mas a resposta da consulta básica falhou. Verifique o estado do cluster PostgreSQL.");
            }
            result.AddLog("Consulta 'SELECT 1;' executada com sucesso absoluto!");
        }
        catch (Exception ex)
        {
            await conn.DisposeAsync();
            var safeMsg = SanitizeMessage(ex.Message, parameters.Password);
            return FinalizarFalha(result, sw,
                PostgreSqlDiagnosticStatus.ServidorAcessivel,
                "Execução SELECT 1",
                safeMsg,
                $"Falha ao executar consulta simples de leitura no banco '{result.Database}': {safeMsg}. Verifique as permissões de leitura (SELECT) do usuário.");
        }

        // -------------------------------------------------------------
        // ETAPA 7: Identificação da Versão (SELECT version();)
        // -------------------------------------------------------------
        result.AddLog("Etapa 7/7: Identificando versão do PostgreSQL (SELECT version();)...");
        try
        {
            await using var cmdVer = new NpgsqlCommand("SELECT version();", conn);
            cmdVer.CommandTimeout = Math.Min(10, result.TimeoutSeconds);
            var verVal = await cmdVer.ExecuteScalarAsync(ct);
            result.VersaoPostgreSql = verVal?.ToString()?.Trim();
            result.AddLog($"Versão identificada: {result.VersaoPostgreSql}");
        }
        catch (Exception ex)
        {
            result.VersaoPostgreSql = "Versão não obtida (" + SanitizeMessage(ex.Message, parameters.Password) + ")";
            result.AddLog($"Aviso: não foi possível ler 'SELECT version()': {ex.Message}");
        }
        finally
        {
            await conn.DisposeAsync();
        }

        // -------------------------------------------------------------
        // SUCESSO COMPLETO
        // -------------------------------------------------------------
        sw.Stop();
        result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
        result.Status = PostgreSqlDiagnosticStatus.Conectado;
        result.EtapaFalha = string.Empty;
        result.MensagemTecnica = "Conexão aberta e validada com sucesso.";
        result.SugestaoDiagnostico = "Conexão com PostgreSQL estabelecida com sucesso! Todos os testes de resolução DNS, porta TCP, autenticação e leitura foram aprovados com êxito.";
        result.AddLog($"Diagnóstico finalizado com sucesso em {result.ElapsedMilliseconds} ms.");

        _logger?.LogInformation("Diagnóstico PostgreSQL para {Host}:{Port}/{Db} concluído com status {Status} em {Ms}ms.",
            result.Host, result.Port, result.Database, result.Status, result.ElapsedMilliseconds);

        return result;
    }

    private static SslMode NormalizarSslMode(string mode)
    {
        if (Enum.TryParse<SslMode>(mode, true, out var parsed))
            return parsed;
        return SslMode.Prefer;
    }

    private static string SanitizeMessage(string message, string? password)
    {
        if (string.IsNullOrWhiteSpace(message)) return string.Empty;
        if (!string.IsNullOrWhiteSpace(password) && password.Length > 2)
        {
            message = message.Replace(password, "***");
        }
        message = Regex.Replace(message, "(?i)password=[^;]+", "password=***");
        return message;
    }

    private static PostgreSqlDiagnosticResult FinalizarFalha(
        PostgreSqlDiagnosticResult result,
        Stopwatch sw,
        PostgreSqlDiagnosticStatus status,
        string etapaFalha,
        string msgTecnica,
        string sugestao)
    {
        sw.Stop();
        result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
        result.Status = status;
        result.EtapaFalha = etapaFalha;
        result.MensagemTecnica = msgTecnica;
        result.SugestaoDiagnostico = sugestao;
        result.AddLog($"[FALHA] Etapa '{etapaFalha}' falhou ({result.StatusBadge}): {msgTecnica}");
        result.AddLog($"[SUGESTÃO] {sugestao}");
        return result;
    }
}
