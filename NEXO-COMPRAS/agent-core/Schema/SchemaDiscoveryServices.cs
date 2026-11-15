using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Nexo.Agent.Core.Schema;

public interface ISchemaDiscoveryService
{
    Task<DiscoveredSchema> DiscoverSchemaAsync(CancellationToken ct = default);
}

public class FirebirdSchemaDiscoveryService : ISchemaDiscoveryService
{
    private readonly string _connectionString;
    private readonly string _databasePath;
    private readonly ILogger? _logger;

    public FirebirdSchemaDiscoveryService(string connectionString, string databasePath, ILogger? logger = null)
    {
        _connectionString = connectionString;
        _databasePath = databasePath;
        _logger = logger;
    }

    public async Task<DiscoveredSchema> DiscoverSchemaAsync(CancellationToken ct = default)
    {
        var schema = new DiscoveredSchema
        {
            DatabaseType = "Firebird",
            DatabaseName = _databasePath,
            DiscoveredAt = DateTime.UtcNow
        };

        try
        {
            await using var conn = new FbConnection(_connectionString);
            await conn.OpenAsync(ct);

            // 1. Obter tabelas de usuário (não de sistema)
            using var cmdTables = conn.CreateCommand();
            cmdTables.CommandText = @"
                SELECT TRIM(RDB$RELATION_NAME) AS TABLE_NAME
                FROM RDB$RELATIONS
                WHERE (RDB$SYSTEM_FLAG = 0 OR RDB$SYSTEM_FLAG IS NULL)
                  AND RDB$VIEW_BLR IS NULL
                ORDER BY 1";

            await using var rdrTables = await cmdTables.ExecuteReaderAsync(ct);
            while (await rdrTables.ReadAsync(ct))
            {
                var tName = rdrTables["TABLE_NAME"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(tName))
                {
                    schema.Tables[tName] = new DbTableInfo { Name = tName };
                }
            }
            await rdrTables.CloseAsync();

            // 2. Obter colunas
            using var cmdCols = conn.CreateCommand();
            cmdCols.CommandText = @"
                SELECT 
                    TRIM(rf.RDB$RELATION_NAME) AS TABLE_NAME,
                    TRIM(rf.RDB$FIELD_NAME) AS COLUMN_NAME,
                    f.RDB$FIELD_TYPE AS FIELD_TYPE,
                    f.RDB$FIELD_LENGTH AS FIELD_LENGTH,
                    f.RDB$FIELD_SCALE AS FIELD_SCALE
                FROM RDB$RELATION_FIELDS rf
                JOIN RDB$FIELDS f ON f.RDB$FIELD_NAME = rf.RDB$FIELD_SOURCE
                JOIN RDB$RELATIONS r ON r.RDB$RELATION_NAME = rf.RDB$RELATION_NAME
                WHERE (r.RDB$SYSTEM_FLAG = 0 OR r.RDB$SYSTEM_FLAG IS NULL)
                ORDER BY rf.RDB$RELATION_NAME, rf.RDB$FIELD_POSITION";

            await using var rdrCols = await cmdCols.ExecuteReaderAsync(ct);
            while (await rdrCols.ReadAsync(ct))
            {
                var tName = rdrCols["TABLE_NAME"]?.ToString()?.Trim() ?? "";
                var cName = rdrCols["COLUMN_NAME"]?.ToString()?.Trim() ?? "";
                if (schema.Tables.TryGetValue(tName, out var table) && !string.IsNullOrEmpty(cName))
                {
                    var len = rdrCols["FIELD_LENGTH"] != DBNull.Value ? Convert.ToInt32(rdrCols["FIELD_LENGTH"]) : 0;
                    var scale = rdrCols["FIELD_SCALE"] != DBNull.Value ? Convert.ToInt32(rdrCols["FIELD_SCALE"]) : 0;
                    var typeCode = rdrCols["FIELD_TYPE"] != DBNull.Value ? Convert.ToInt32(rdrCols["FIELD_TYPE"]) : 0;
                    var typeName = MapFbType(typeCode, scale);

                    table.Columns.Add(new DbColumnInfo(cName, typeName, len, scale, true));
                }
            }
            await rdrCols.CloseAsync();

            // 3. Obter chaves primárias e relacionamentos
            try
            {
                using var cmdKeys = conn.CreateCommand();
                cmdKeys.CommandText = @"
                    SELECT 
                        TRIM(rc.RDB$RELATION_NAME) AS TABLE_NAME,
                        TRIM(rc.RDB$CONSTRAINT_NAME) AS CONSTRAINT_NAME,
                        TRIM(rc.RDB$CONSTRAINT_TYPE) AS CONSTRAINT_TYPE,
                        TRIM(s.RDB$FIELD_NAME) AS FIELD_NAME
                    FROM RDB$RELATION_CONSTRAINTS rc
                    JOIN RDB$INDEX_SEGMENTS s ON s.RDB$INDEX_NAME = rc.RDB$INDEX_NAME
                    WHERE rc.RDB$CONSTRAINT_TYPE IN ('PRIMARY KEY', 'FOREIGN KEY')";

                await using var rdrKeys = await cmdKeys.ExecuteReaderAsync(ct);
                while (await rdrKeys.ReadAsync(ct))
                {
                    var tName = rdrKeys["TABLE_NAME"]?.ToString()?.Trim() ?? "";
                    var fName = rdrKeys["FIELD_NAME"]?.ToString()?.Trim() ?? "";
                    var cType = rdrKeys["CONSTRAINT_TYPE"]?.ToString()?.Trim() ?? "";

                    if (schema.Tables.TryGetValue(tName, out var table) && !string.IsNullOrEmpty(fName))
                    {
                        if (cType == "PRIMARY KEY" && !table.PrimaryKeys.Contains(fName))
                        {
                            table.PrimaryKeys.Add(fName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("Aviso ao ler constraints do Firebird: {Msg}", ex.Message);
            }

            _logger?.LogInformation("Descoberta de metadados Firebird concluída: {Total} tabelas identificadas.", schema.Tables.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError("Erro na descoberta de schema Firebird: {Msg}", ex.Message);
            throw;
        }

        return schema;
    }

    private static string MapFbType(int typeCode, int scale)
    {
        return typeCode switch
        {
            7 => "SMALLINT",
            8 => scale < 0 ? "NUMERIC" : "INTEGER",
            10 => "FLOAT",
            12 => "DATE",
            13 => "TIME",
            14 => "CHAR",
            16 => scale < 0 ? "DECIMAL" : "BIGINT",
            27 => "DOUBLE PRECISION",
            35 => "TIMESTAMP",
            37 => "VARCHAR",
            261 => "BLOB",
            _ => "UNKNOWN"
        };
    }
}

public class PostgreSqlSchemaDiscoveryService : ISchemaDiscoveryService
{
    private readonly string _connectionString;
    private readonly string _databaseName;
    private readonly string _schema;
    private readonly ILogger? _logger;

    public PostgreSqlSchemaDiscoveryService(string connectionString, string databaseName, string schema = "public", ILogger? logger = null)
    {
        _connectionString = connectionString;
        _databaseName = databaseName;
        _schema = string.IsNullOrWhiteSpace(schema) ? "public" : schema.Trim();
        _logger = logger;
    }

    public async Task<DiscoveredSchema> DiscoverSchemaAsync(CancellationToken ct = default)
    {
        var schema = new DiscoveredSchema
        {
            DatabaseType = "PostgreSQL",
            DatabaseName = _databaseName,
            DiscoveredAt = DateTime.UtcNow
        };

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            // 1. Obter tabelas base do schema
            using var cmdTables = conn.CreateCommand();
            cmdTables.CommandText = @"
                SELECT table_name
                FROM information_schema.tables
                WHERE table_schema = @schema AND table_type = 'BASE TABLE'
                ORDER BY table_name";
            cmdTables.Parameters.AddWithValue("schema", _schema);

            await using var rdrTables = await cmdTables.ExecuteReaderAsync(ct);
            while (await rdrTables.ReadAsync(ct))
            {
                var tName = rdrTables["table_name"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(tName))
                {
                    schema.Tables[tName] = new DbTableInfo { Name = tName };
                }
            }
            await rdrTables.CloseAsync();

            // 2. Obter colunas
            using var cmdCols = conn.CreateCommand();
            cmdCols.CommandText = @"
                SELECT table_name, column_name, data_type, character_maximum_length, numeric_scale, is_nullable
                FROM information_schema.columns
                WHERE table_schema = @schema
                ORDER BY table_name, ordinal_position";
            cmdCols.Parameters.AddWithValue("schema", _schema);

            await using var rdrCols = await cmdCols.ExecuteReaderAsync(ct);
            while (await rdrCols.ReadAsync(ct))
            {
                var tName = rdrCols["table_name"]?.ToString()?.Trim() ?? "";
                var cName = rdrCols["column_name"]?.ToString()?.Trim() ?? "";
                if (schema.Tables.TryGetValue(tName, out var table) && !string.IsNullOrEmpty(cName))
                {
                    var len = rdrCols["character_maximum_length"] != DBNull.Value ? Convert.ToInt32(rdrCols["character_maximum_length"]) : 0;
                    var scale = rdrCols["numeric_scale"] != DBNull.Value ? Convert.ToInt32(rdrCols["numeric_scale"]) : 0;
                    var dType = rdrCols["data_type"]?.ToString() ?? "TEXT";
                    var isNull = rdrCols["is_nullable"]?.ToString() == "YES";

                    table.Columns.Add(new DbColumnInfo(cName, dType, len, scale, isNull));
                }
            }
            await rdrCols.CloseAsync();

            // 3. Obter Primary Keys
            try
            {
                using var cmdKeys = conn.CreateCommand();
                cmdKeys.CommandText = @"
                    SELECT tc.table_name, kcu.column_name
                    FROM information_schema.table_constraints tc
                    JOIN information_schema.key_column_usage kcu 
                      ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
                    WHERE tc.table_schema = @schema AND tc.constraint_type = 'PRIMARY KEY'";
                cmdKeys.Parameters.AddWithValue("schema", _schema);

                await using var rdrKeys = await cmdKeys.ExecuteReaderAsync(ct);
                while (await rdrKeys.ReadAsync(ct))
                {
                    var tName = rdrKeys["table_name"]?.ToString()?.Trim() ?? "";
                    var cName = rdrKeys["column_name"]?.ToString()?.Trim() ?? "";
                    if (schema.Tables.TryGetValue(tName, out var table) && !table.PrimaryKeys.Contains(cName))
                    {
                        table.PrimaryKeys.Add(cName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("Aviso ao ler primary keys PostgreSQL: {Msg}", ex.Message);
            }

            _logger?.LogInformation("Descoberta de metadados PostgreSQL concluída: {Total} tabelas no schema '{Schema}'.", schema.Tables.Count, _schema);
        }
        catch (Exception ex)
        {
            _logger?.LogError("Erro na descoberta de schema PostgreSQL: {Msg}", ex.Message);
            throw;
        }

        return schema;
    }
}

