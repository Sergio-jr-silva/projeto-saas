using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nexo.Agent.Core.Connectors;
using Nexo.Agent.Core.Mapping;
using Nexo.Agent.Core.Queue;
using Nexo.Agent.Core.State;
using Nexo.Agent.Core.Sync;

namespace Nexo.Agent;

public class Worker : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly ILogger<Worker> _logger;
    private readonly ILoggerFactory _loggerFactory;

    private IDatabaseConnector _connector = null!;
    private PersistentTaskQueue _queue = null!;
    private AgentStateManager _state = null!;
    private IncrementalSyncEngine _engine = null!;

    private DateTime _ultimoEstoque = DateTime.MinValue;
    private DateTime _ultimoProdutos = DateTime.MinValue;
    private DateTime _ultimoFornecedores = DateTime.MinValue;

    public Worker(IConfiguration config, ILogger<Worker> logger, ILoggerFactory loggerFactory)
    {
        _config = config;
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=================================================");
        _logger.LogInformation("  Nexo Compras - Serviço Sincronizador Windows");
        _logger.LogInformation("  Versão 2.0 - Operando em Segundo Plano");
        _logger.LogInformation("=================================================");

        var mapping = new FieldMappingConfig();
        _connector = ConnectorFactory.CreateConnector(_config, mapping, _loggerFactory);
        _queue = new PersistentTaskQueue();
        _state = new AgentStateManager();

        var engineLogger = _loggerFactory.CreateLogger<IncrementalSyncEngine>();
        _engine = new IncrementalSyncEngine(_connector, _config, _queue, _state, engineLogger);

        _state.UpdateStatus("Iniciando");
        _state.UpdateBanco(
            _connector.DatabaseType,
            _config[$"{_connector.DatabaseType}:Host"] ?? "127.0.0.1",
            int.TryParse(_config[$"{_connector.DatabaseType}:Port"], out var p) ? p : 3050,
            _connector.DatabasePathOrName,
            "Verificando"
        );

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 1. Testa conectividade inicial com o banco de dados
        var dbOk = await _connector.TestConnectionAsync(stoppingToken);
        _state.UpdateBanco(
            _connector.DatabaseType,
            _config[$"{_connector.DatabaseType}:Host"] ?? "127.0.0.1",
            int.TryParse(_config[$"{_connector.DatabaseType}:Port"], out var p) ? p : 3050,
            _connector.DatabasePathOrName,
            dbOk ? "Conectado" : "Falha na Conexão"
        );

        if (dbOk)
            _logger.LogInformation("Banco de dados local ({Tipo}: {Path}) conectado com sucesso.", _connector.DatabaseType, _connector.DatabasePathOrName);
        else
            _logger.LogWarning("Não foi possível conectar ao banco local ({Tipo}: {Path}).", _connector.DatabaseType, _connector.DatabasePathOrName);

        // 2. Primeiro Heartbeat e primeiro ciclo de sincronização
        await _engine.ProcessarCicloHeartbeatAsync(stoppingToken);
        await ExecutarCicloCompletoAsync(stoppingToken);

        // 3. Loop principal de segundo plano (resiliente e com recuperação automática)
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // A) Envia Heartbeat para monitorar conexão e atualizar parâmetros dinâmicos
                var online = await _engine.ProcessarCicloHeartbeatAsync(stoppingToken);

                // B) Testa conexão com o banco local periodicamente
                dbOk = await _connector.TestConnectionAsync(stoppingToken);
                _state.UpdateBanco(
                    _connector.DatabaseType,
                    _config[$"{_connector.DatabaseType}:Host"] ?? "127.0.0.1",
                    int.TryParse(_config[$"{_connector.DatabaseType}:Port"], out var p2) ? p2 : 3050,
                    _connector.DatabasePathOrName,
                    dbOk ? "Conectado" : "Falha na Conexão"
                );

                // C) Ciclo de Estoque (padrão 5 min ou configurado)
                if (DateTime.UtcNow - _ultimoEstoque >= _engine.IntervaloEstoque)
                {
                    _state.UpdateStatus("Sincronizando");
                    await _engine.SincronizarEstoqueIncrementalAsync(stoppingToken);
                    _ultimoEstoque = DateTime.UtcNow;
                    _state.UpdateStatus(online ? "Online" : "Offline");
                }

                // D) Ciclo de Produtos (padrão 30 min ou configurado)
                if (DateTime.UtcNow - _ultimoProdutos >= _engine.IntervaloProdutos)
                {
                    _state.UpdateStatus("Sincronizando");
                    await _engine.SincronizarProdutosIncrementalAsync(stoppingToken);
                    _ultimoProdutos = DateTime.UtcNow;
                    _state.UpdateStatus(online ? "Online" : "Offline");
                }

                // E) Ciclo de Fornecedores (padrão 30 min ou configurado)
                if (DateTime.UtcNow - _ultimoFornecedores >= _engine.IntervaloFornecedores)
                {
                    _state.UpdateStatus("Sincronizando");
                    await _engine.SincronizarFornecedoresIncrementalAsync(stoppingToken);
                    _ultimoFornecedores = DateTime.UtcNow;
                    _state.UpdateStatus(online ? "Online" : "Offline");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no loop de execução do sincronizador.");
                _state.UpdateStatus("Erro");
                _state.AddLog("ERROR", "Falha no serviço sincronizador: " + ex.Message);
            }

            try
            {
                // Intervalo de heartbeat (padrão 60 segundos)
                await Task.Delay(_engine.IntervaloHeartbeat, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _state.UpdateStatus("Parado");
        _logger.LogInformation("Serviço Sincronizador Nexo Compras finalizado.");
    }

    private async Task ExecutarCicloCompletoAsync(CancellationToken ct)
    {
        _state.UpdateStatus("Sincronizando");
        await _engine.SincronizarProdutosIncrementalAsync(ct);
        await _engine.SincronizarFornecedoresIncrementalAsync(ct);
        await _engine.SincronizarEstoqueIncrementalAsync(ct);
        _ultimoEstoque = DateTime.UtcNow;
        _ultimoProdutos = DateTime.UtcNow;
        _ultimoFornecedores = DateTime.UtcNow;
        _state.UpdateStatus("Online");
    }
}
