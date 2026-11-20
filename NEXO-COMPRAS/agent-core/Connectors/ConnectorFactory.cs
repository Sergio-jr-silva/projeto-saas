using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nexo.Agent.Core.Mapping;

namespace Nexo.Agent.Core.Connectors;

public static class ConnectorFactory
{
    public static IDatabaseConnector CreateConnector(
        IConfiguration config,
        FieldMappingConfig? mapping = null,
        ILoggerFactory? loggerFactory = null)
    {
        var dbType = config["Database:Type"] ?? (config["Firebird:Database"] != null ? "Firebird" : "PostgreSQL");

        if (dbType.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            var logger = loggerFactory?.CreateLogger<PostgreSqlConnector>();
            return new PostgreSqlConnector(config, mapping, logger);
        }

        // Default: Firebird
        var fbLogger = loggerFactory?.CreateLogger<FirebirdConnector>();
        return new FirebirdConnector(config, mapping, fbLogger);
    }
}

