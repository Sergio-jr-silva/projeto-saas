using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Nexo.Agent.Core.Connectors;

public enum FirebirdDiagnosticStatus
{
    Conectado,
    ServidorAcessivel,
    FalhaConexao,
    FalhaAutenticacao,
    BancoIndisponivel,
    ArquivoNaoEncontrado,
    CamposInvalidos
}

public class FirebirdDiagnosticParams
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 3050;
    public string Database { get; set; } = string.Empty;
    public string User { get; set; } = "SYSDBA";
    public string Password { get; set; } = "masterkey";
    public string Charset { get; set; } = "NONE";
    public int TimeoutSeconds { get; set; } = 15;

    public static FirebirdDiagnosticParams FromConfiguration(IConfiguration config)
    {
        return new FirebirdDiagnosticParams
        {
            Host = config["Firebird:Host"] ?? "127.0.0.1",
            Port = int.TryParse(config["Firebird:Port"], out var p) ? p : 3050,
            Database = config["Firebird:Database"] ?? string.Empty,
            User = config["Firebird:User"] ?? "SYSDBA",
            Password = config["Firebird:Password"] ?? "masterkey",
            Charset = config["Firebird:Charset"] ?? "NONE",
            TimeoutSeconds = int.TryParse(config["Firebird:Timeout"], out var t) ? t : 15
        };
    }
}

public class FirebirdDiagnosticResult
{
    public FirebirdDiagnosticStatus Status { get; set; } = FirebirdDiagnosticStatus.CamposInvalidos;
    public bool Sucesso => Status == FirebirdDiagnosticStatus.Conectado;

    public string StatusBadge => Status switch
    {
        FirebirdDiagnosticStatus.Conectado => "Conectado",
        FirebirdDiagnosticStatus.ServidorAcessivel => "Servidor acessível",
        FirebirdDiagnosticStatus.FalhaConexao => "Falha de conexão",
        FirebirdDiagnosticStatus.FalhaAutenticacao => "Falha de autenticação",
        FirebirdDiagnosticStatus.BancoIndisponivel => "Banco indisponível",
        FirebirdDiagnosticStatus.ArquivoNaoEncontrado => "Arquivo não encontrado",
        FirebirdDiagnosticStatus.CamposInvalidos => "Campos inválidos",
        _ => "Desconhecido"
    };

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Database { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public long ElapsedMilliseconds { get; set; }
    public string EtapaFalha { get; set; } = string.Empty;
    public string MensagemTecnica { get; set; } = string.Empty;
    public string SugestaoDiagnostico { get; set; } = string.Empty;
    public string? VersaoFirebird { get; set; }
    public List<string> LogsEtapas { get; set; } = new();

    public void AddLog(string msg)
    {
        LogsEtapas.Add($"[{DateTime.Now:HH:mm:ss.fff}] {msg}");
    }
}

public class FirebirdDiagnosticService
{
    private readonly ILogger<FirebirdDiagnosticService>? _logger;

    public FirebirdDiagnosticService(ILogger<FirebirdDiagnosticService>? logger = null)
    {
        _logger = logger;
    }

    public async Task<FirebirdDiagnosticResult> DiagnosticarAsync(
        FirebirdDiagnosticParams parameters,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new FirebirdDiagnosticResult
        {
            Host = parameters.Host?.Trim() ?? string.Empty,
            Port = parameters.Port > 0 ? parameters.Port : 3050,
            Database = parameters.Database?.Trim() ?? string.Empty,
            User = parameters.User?.Trim() ?? string.Empty
        };

        result.AddLog("Etapa 1/6: Validando campos de configuração Firebird...");
        if (string.IsNullOrWhiteSpace(result.Host))
        {
            return FinalizarFalha(result, sw, FirebirdDiagnosticStatus.CamposInvalidos,
                "Validação de Parâmetros (Host)", "O campo Host é obrigatório.", "Informe o Host ou IP onde o Firebird está instalado (ex: 127.0.0.1).");
        }

        if (string.IsNullOrWhiteSpace(result.Database))
        {
            return FinalizarFalha(result, sw, FirebirdDiagnosticStatus.CamposInvalidos,
                "Validação de Parâmetros (Banco)", "O caminho do banco .FDB é obrigatório.", "Informe o caminho completo do arquivo de banco de dados (ex: C:\\DATACASH\\DADOS\\BM.FDB).");
        }

        var isLocal = result.Host == "127.0.0.1" || result.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
        if (isLocal && !File.Exists(result.Database))
        {
            result.AddLog($"Aviso: Arquivo '{result.Database}' não encontrado diretamente no caminho informado.");
            var dir = Path.GetDirectoryName(result.Database);
            var file = Path.GetFileName(result.Database);
            var found = false;
            if (!string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(file))
            {
                var candidateInDados = Path.Combine(dir, "DADOS", file);
                if (File.Exists(candidateInDados))
                {
                    result.Database = candidateInDados;
                    found = true;
                    result.AddLog($"Arquivo localizado com sucesso no subdiretório: '{candidateInDados}'.");
                }
            }

            if (!found)
            {
                return FinalizarFalha(result, sw, FirebirdDiagnosticStatus.ArquivoNaoEncontrado,
                    "Arquivo do Banco", $"O arquivo '{result.Database}' não existe no caminho local especificado.",
                    "Verifique se o caminho do arquivo .FDB está correto e se o arquivo não foi movido ou renomeado.");
            }
        }

        result.AddLog($"Etapa 2/6: Testando acessibilidade da porta TCP {result.Port} em '{result.Host}'...");
        try
        {
            using var tcp = new TcpClient();
            var connectTask = tcp.ConnectAsync(result.Host, result.Port);
            var delayTask = Task.Delay(TimeSpan.FromSeconds(parameters.TimeoutSeconds), ct);
            var completed = await Task.WhenAny(connectTask, delayTask);
            if (completed == delayTask)
            {
                return FinalizarFalha(result, sw, FirebirdDiagnosticStatus.FalhaConexao,
                    "Porta TCP (Timeout)", $"Timeout ao tentar alcançar o servidor Firebird na porta {result.Port}.",
                    "Verifique se o serviço Firebird Server está em execução e se a porta 3050 está liberada no Firewall.");
            }
            await connectTask;
            result.AddLog($"Porta TCP {result.Port} aberta e respondendo.");
        }
        catch (SocketException sockEx)
        {
            return FinalizarFalha(result, sw, FirebirdDiagnosticStatus.FalhaConexao,
                "Porta TCP (Conexão Recusada)", sockEx.Message,
                "Verifique se o serviço Firebird Server (Guardian/Server) está iniciado nos Serviços do Windows.");
        }
        catch (Exception ex)
        {
            return FinalizarFalha(result, sw, FirebirdDiagnosticStatus.FalhaConexao,
                "Porta TCP", ex.Message, "Falha de rede ao tentar acessar o servidor Firebird.");
        }

        result.AddLog("Etapa 3/6: Estabelecendo conexão FirebirdClient e autenticando...");
        var csb = new FbConnectionStringBuilder
        {
            ServerType = FbServerType.Default,
            DataSource = result.Host,
            Port = result.Port,
            Database = result.Database,
            UserID = result.User,
            Password = parameters.Password ?? string.Empty,
            Charset = string.IsNullOrWhiteSpace(parameters.Charset) ? "NONE" : parameters.Charset.Trim(),
            ConnectionTimeout = parameters.TimeoutSeconds > 0 ? parameters.TimeoutSeconds : 15,
            Pooling = false
        };

        FbConnection? conn = null;
        try
        {
            conn = new FbConnection(csb.ToString());
            await conn.OpenAsync(ct);
            result.AddLog("Conexão com Firebird aberta com sucesso!");
        }
        catch (FbException fbEx)
        {
            var msg = Sanitize(fbEx.Message, parameters.Password);
            if (msg.Contains("user name and password are not defined", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("Your user name and password", StringComparison.OrdinalIgnoreCase) ||
                fbEx.ErrorCode == 335544472)
            {
                return FinalizarFalha(result, sw, FirebirdDiagnosticStatus.FalhaAutenticacao,
                    "Autenticação", msg, "Usuário ou senha incorretos para o banco Firebird. O padrão comum é SYSDBA / masterkey.");
            }

            if (msg.Contains("I/O error", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("database", StringComparison.OrdinalIgnoreCase) ||
                fbEx.ErrorCode == 335544344)
            {
                return FinalizarFalha(result, sw, FirebirdDiagnosticStatus.BancoIndisponivel,
                    "Acesso ao Arquivo do Banco", msg, "O Firebird Server não conseguiu abrir o arquivo do banco. Verifique permissões de leitura no arquivo .FDB.");
            }

            return FinalizarFalha(result, sw, FirebirdDiagnosticStatus.ServidorAcessivel,
                "Driver FirebirdClient", msg, $"O Firebird respondeu com erro: {msg}");
        }
        catch (Exception ex)
        {
            var msg = Sanitize(ex.Message, parameters.Password);
            return FinalizarFalha(result, sw, FirebirdDiagnosticStatus.FalhaConexao,
                "Conexão Firebird", msg, $"Erro inesperado ao conectar ao Firebird: {msg}");
        }

        result.AddLog("Etapa 4/6: Executando consulta de validação (SELECT 1 FROM RDB$DATABASE)...");
        try
        {
            await using var cmdPing = new FbCommand("SELECT 1 FROM RDB$DATABASE", conn);
            cmdPing.CommandTimeout = 10;
            var val = await cmdPing.ExecuteScalarAsync(ct);
            result.AddLog("Consulta SELECT 1 executada com sucesso!");
        }
        catch (Exception ex)
        {
            await conn.DisposeAsync();
            var msg = Sanitize(ex.Message, parameters.Password);
            return FinalizarFalha(result, sw, FirebirdDiagnosticStatus.ServidorAcessivel,
                "Consulta SELECT 1", msg, $"Falha ao executar query no Firebird: {msg}");
        }

        result.AddLog("Etapa 5/6: Obtendo versão do motor Firebird...");
        try
        {
            result.VersaoFirebird = conn.ServerVersion;
            result.AddLog($"Versão Firebird: {result.VersaoFirebird}");
        }
        catch
        {
            result.VersaoFirebird = "Firebird Server";
        }
        finally
        {
            await conn.DisposeAsync();
        }

        sw.Stop();
        result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
        result.Status = FirebirdDiagnosticStatus.Conectado;
        result.EtapaFalha = string.Empty;
        result.MensagemTecnica = "Conexão aberta e validada com sucesso.";
        result.SugestaoDiagnostico = "Conexão com Firebird estabelecida com sucesso! Todos os testes de leitura e autenticação foram aprovados com êxito.";
        result.AddLog($"Diagnóstico finalizado com sucesso em {result.ElapsedMilliseconds} ms.");

        return result;
    }

    private static string Sanitize(string msg, string? pass)
    {
        if (string.IsNullOrWhiteSpace(msg)) return string.Empty;
        if (!string.IsNullOrWhiteSpace(pass) && pass.Length > 2)
            msg = msg.Replace(pass, "***");
        return msg;
    }

    private static FirebirdDiagnosticResult FinalizarFalha(
        FirebirdDiagnosticResult result,
        Stopwatch sw,
        FirebirdDiagnosticStatus status,
        string etapa,
        string msg,
        string sugestao)
    {
        sw.Stop();
        result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
        result.Status = status;
        result.EtapaFalha = etapa;
        result.MensagemTecnica = msg;
        result.SugestaoDiagnostico = sugestao;
        result.AddLog($"[FALHA] {etapa}: {msg}");
        result.AddLog($"[SUGESTÃO] {sugestao}");
        return result;
    }
}

