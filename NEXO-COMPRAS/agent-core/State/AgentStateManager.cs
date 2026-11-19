using System.Text.Json;

namespace Nexo.Agent.Core.State;

public class EmpresaVinculadaInfo
{
    public string TenantId { get; set; } = string.Empty;
    public string RazaoSocial { get; set; } = "Não vinculada";
    public string NomeFantasia { get; set; } = "Aguardando ativação";
    public string Cnpj { get; set; } = string.Empty;
    public string Plano { get; set; } = string.Empty;
    public string FilialId { get; set; } = string.Empty;
    public string FilialNome { get; set; } = string.Empty;
    public string FilialCodigo { get; set; } = string.Empty;
}

public class BancoDadosInfo
{
    public string Tipo { get; set; } = "Firebird";
    public string Host { get; set; } = "127.0.0.1";
    public int Porta { get; set; } = 3050;
    public string CaminhoOuNome { get; set; } = @"C:\DATACASH\BM.FDB";
    public string Status { get; set; } = "Desconhecido"; // Conectado, Falha, Simulado
}

public class HistoricoSincronizacaoItem
{
    public DateTime Data { get; set; } = DateTime.UtcNow;
    public string Tipo { get; set; } = string.Empty;
    public int QtdItens { get; set; }
    public long DuracaoMs { get; set; }
    public string Status { get; set; } = "Sucesso"; // Sucesso, Offline (Enfileirado), Erro
    public string Mensagem { get; set; } = string.Empty;
}

public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Level { get; set; } = "INFO"; // INFO, SUCCESS, WARN, ERROR
    public string Message { get; set; } = string.Empty;
}

public class AgentState
{
    public string Status { get; set; } = "Iniciando"; // Online, Sincronizando, Offline, Erro
    public DateTime UltimaAtualizacao { get; set; } = DateTime.UtcNow;
    public EmpresaVinculadaInfo Empresa { get; set; } = new();
    public BancoDadosInfo Banco { get; set; } = new();
    public DateTime? UltimaSincronizacao { get; set; }
    public string UltimaSincronizacaoDescricao { get; set; } = "Nenhuma sincronização concluída";
    public DateTime? ProximaSincronizacao { get; set; }
    public int FilaOfflinePendente { get; set; } = 0;
    public long LatenciaSaaSMs { get; set; } = 0;
    public int TotalProdutosSincronizados { get; set; }
    public int TotalEstoqueSincronizado { get; set; }
    public int TotalFornecedoresSincronizados { get; set; }
    public bool CargaInicialConcluida { get; set; }
    public string SituacaoCargaInicial { get; set; } = "Pendente de carga inicial";
    public int UltimosProdutosNovos { get; set; }
    public int UltimosProdutosModificados { get; set; }
    public int UltimosProdutosEnviados { get; set; }
    public List<HistoricoSincronizacaoItem> Historico { get; set; } = new();
    public List<LogEntry> LogsRecentes { get; set; } = new();
}

public class AgentStateManager
{
    private readonly string _stateFilePath;
    private readonly object _lock = new();
    private AgentState _currentState = new();

    public AgentState CurrentState => _currentState;

    public AgentStateManager(string? customPath = null)
    {
        var dir = customPath != null
            ? Path.GetDirectoryName(customPath)!
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        _stateFilePath = customPath ?? Path.Combine(dir, "agent_state.json");
        LoadState();
    }

    public void UpdateStatus(string status)
    {
        lock (_lock)
        {
            _currentState.Status = status;
            _currentState.UltimaAtualizacao = DateTime.UtcNow;
            SaveState();
        }
    }

    public void UpdateEmpresa(string tenantId, string razaoSocial, string nomeFantasia, string cnpj, string plano, string? filialId = null, string? filialNome = null, string? filialCodigo = null)
    {
        lock (_lock)
        {
            _currentState.Empresa.TenantId = tenantId;
            _currentState.Empresa.RazaoSocial = razaoSocial;
            _currentState.Empresa.NomeFantasia = nomeFantasia;
            _currentState.Empresa.Cnpj = cnpj;
            _currentState.Empresa.Plano = plano;
            if (!string.IsNullOrEmpty(filialId)) _currentState.Empresa.FilialId = filialId;
            if (!string.IsNullOrEmpty(filialNome)) _currentState.Empresa.FilialNome = filialNome;
            if (!string.IsNullOrEmpty(filialCodigo)) _currentState.Empresa.FilialCodigo = filialCodigo;
            SaveState();
        }
    }

    public void UpdateBanco(string tipo, string host, int porta, string caminhoOuNome, string status)
    {
        lock (_lock)
        {
            _currentState.Banco.Tipo = tipo;
            _currentState.Banco.Host = host;
            _currentState.Banco.Porta = porta;
            _currentState.Banco.CaminhoOuNome = caminhoOuNome;
            _currentState.Banco.Status = status;
            SaveState();
        }
    }

    public void RegisterSyncDone(string tipo, int qtdItens, long duracaoMs, string status, string mensagem, DateTime? proximaSync = null)
    {
        lock (_lock)
        {
            _currentState.UltimaSincronizacao = DateTime.UtcNow;
            _currentState.UltimaSincronizacaoDescricao = $"{tipo}: {qtdItens} itens ({status})";
            if (proximaSync.HasValue)
                _currentState.ProximaSincronizacao = proximaSync.Value;

            if (tipo.Equals("Estoque", StringComparison.OrdinalIgnoreCase) && status == "Sucesso")
                _currentState.TotalEstoqueSincronizado = qtdItens;
            else if (tipo.Equals("Produtos", StringComparison.OrdinalIgnoreCase) && status == "Sucesso")
                _currentState.TotalProdutosSincronizados = qtdItens;
            else if (tipo.Equals("Fornecedores", StringComparison.OrdinalIgnoreCase) && status == "Sucesso")
                _currentState.TotalFornecedoresSincronizados = qtdItens;

            _currentState.Historico.Insert(0, new HistoricoSincronizacaoItem
            {
                Data = DateTime.UtcNow,
                Tipo = tipo,
                QtdItens = qtdItens,
                DuracaoMs = duracaoMs,
                Status = status,
                Mensagem = mensagem
            });

            if (_currentState.Historico.Count > 50)
                _currentState.Historico = _currentState.Historico.Take(50).ToList();

            SaveState();
        }
    }

    public void UpdateFilaOffline(int pendentes)
    {
        lock (_lock)
        {
            _currentState.FilaOfflinePendente = pendentes;
            SaveState();
        }
    }

    public void UpdateLatencia(long ms)
    {
        lock (_lock)
        {
            _currentState.LatenciaSaaSMs = ms;
            SaveState();
        }
    }

    public void AddLog(string level, string message)
    {
        lock (_lock)
        {
            _currentState.LogsRecentes.Add(new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Message = message
            });

            if (_currentState.LogsRecentes.Count > 100)
                _currentState.LogsRecentes = _currentState.LogsRecentes.TakeLast(100).ToList();

            SaveState();
        }
    }

    public AgentState Current => _currentState;

    public AgentState Reload()
    {
        LoadState();
        return _currentState;
    }

    private void LoadState()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_stateFilePath))
                {
                    var json = File.ReadAllText(_stateFilePath);
                    _currentState = JsonSerializer.Deserialize<AgentState>(json) ?? new AgentState();
                }
            }
            catch
            {
                _currentState = new AgentState();
            }
        }
    }

    private void SaveState()
    {
        try
        {
            var json = JsonSerializer.Serialize(_currentState, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_stateFilePath, json);
        }
        catch { /* concorrência de leitura */ }
    }
}

