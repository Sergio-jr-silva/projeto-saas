using System.Data;
using Microsoft.Data.Sqlite;

namespace Nexo.Agent.Core.Sync;

public class SQLiteSyncRepository : IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private readonly object _lock = new();

    public SQLiteSyncRepository(string? customDbPath = null)
    {
        var dir = customDbPath != null
            ? Path.GetDirectoryName(customDbPath)!
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        _dbPath = customDbPath ?? Path.Combine(dir, "sinc_local.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        InitializeDatabase();
    }

    private SqliteConnection CreateConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private void InitializeDatabase()
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;

                CREATE TABLE IF NOT EXISTS SincControl (
                    TenantId TEXT NOT NULL,
                    BranchId TEXT NOT NULL DEFAULT '',
                    EntityType TEXT NOT NULL,
                    DatabaseSource TEXT NOT NULL DEFAULT '',
                    InitialSyncCompleted INTEGER NOT NULL DEFAULT 0,
                    InitialSyncDate TEXT,
                    InitialSyncTotal INTEGER NOT NULL DEFAULT 0,
                    LastSyncDate TEXT,
                    LastSyncCount INTEGER NOT NULL DEFAULT 0,
                    LastSyncNewCount INTEGER NOT NULL DEFAULT 0,
                    LastSyncModifiedCount INTEGER NOT NULL DEFAULT 0,
                    EngineVersion TEXT NOT NULL DEFAULT '2.6.0',
                    PRIMARY KEY (TenantId, BranchId, EntityType)
                );

                CREATE TABLE IF NOT EXISTS SincEntityFingerprints (
                    TenantId TEXT NOT NULL,
                    BranchId TEXT NOT NULL DEFAULT '',
                    EntityType TEXT NOT NULL,
                    ExternalId TEXT NOT NULL,
                    FingerprintHash TEXT NOT NULL,
                    LastDetectedChangeDate TEXT NOT NULL,
                    LastConfirmedSyncDate TEXT,
                    SyncStatus TEXT NOT NULL DEFAULT 'Synced',
                    PRIMARY KEY (TenantId, BranchId, EntityType, ExternalId)
                );

                CREATE INDEX IF NOT EXISTS IX_SincFingerprints_Lookup 
                ON SincEntityFingerprints (TenantId, BranchId, EntityType, ExternalId);
            ";
            cmd.ExecuteNonQuery();
        }
    }

    private static string FormatBranch(Guid? branchId) => branchId?.ToString("D") ?? string.Empty;
    private static string FormatTenant(Guid tenantId) => tenantId.ToString("D");

    public bool IsInitialSyncCompleted(Guid tenantId, Guid? branchId, string entityType)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT InitialSyncCompleted 
                FROM SincControl 
                WHERE TenantId = @t AND BranchId = @b AND EntityType = @e;
            ";
            cmd.Parameters.AddWithValue("@t", FormatTenant(tenantId));
            cmd.Parameters.AddWithValue("@b", FormatBranch(branchId));
            cmd.Parameters.AddWithValue("@e", entityType);

            var val = cmd.ExecuteScalar();
            return val != null && Convert.ToInt64(val) == 1;
        }
    }

    public void MarkInitialSyncCompleted(Guid tenantId, Guid? branchId, string entityType, int totalInitial, string databaseSource = "")
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO SincControl (
                    TenantId, BranchId, EntityType, DatabaseSource, 
                    InitialSyncCompleted, InitialSyncDate, InitialSyncTotal, 
                    LastSyncDate, LastSyncCount, LastSyncNewCount, LastSyncModifiedCount, EngineVersion
                )
                VALUES (
                    @t, @b, @e, @src, 
                    1, @now, @tot, 
                    @now, @tot, @tot, 0, '2.6.0'
                )
                ON CONFLICT(TenantId, BranchId, EntityType) DO UPDATE SET
                    InitialSyncCompleted = 1,
                    InitialSyncDate = COALESCE(InitialSyncDate, excluded.InitialSyncDate),
                    InitialSyncTotal = excluded.InitialSyncTotal,
                    LastSyncDate = excluded.LastSyncDate,
                    LastSyncCount = excluded.LastSyncCount,
                    LastSyncNewCount = excluded.LastSyncNewCount,
                    LastSyncModifiedCount = excluded.LastSyncModifiedCount,
                    DatabaseSource = CASE WHEN excluded.DatabaseSource <> '' THEN excluded.DatabaseSource ELSE DatabaseSource END;
            ";
            var nowStr = DateTime.UtcNow.ToString("o");
            cmd.Parameters.AddWithValue("@t", FormatTenant(tenantId));
            cmd.Parameters.AddWithValue("@b", FormatBranch(branchId));
            cmd.Parameters.AddWithValue("@e", entityType);
            cmd.Parameters.AddWithValue("@src", databaseSource);
            cmd.Parameters.AddWithValue("@now", nowStr);
            cmd.Parameters.AddWithValue("@tot", totalInitial);

            cmd.ExecuteNonQuery();
        }
    }

    public Dictionary<string, string> GetStoredFingerprints(Guid tenantId, Guid? branchId, string entityType)
    {
        lock (_lock)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT ExternalId, FingerprintHash 
                FROM SincEntityFingerprints 
                WHERE TenantId = @t AND BranchId = @b AND EntityType = @e;
            ";
            cmd.Parameters.AddWithValue("@t", FormatTenant(tenantId));
            cmd.Parameters.AddWithValue("@b", FormatBranch(branchId));
            cmd.Parameters.AddWithValue("@e", entityType);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetString(0);
                var hash = reader.GetString(1);
                dict[id] = hash;
            }

            return dict;
        }
    }

    public void ConfirmSyncedFingerprints(Guid tenantId, Guid? branchId, string entityType, IEnumerable<(string externalId, string hash)> items)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var tx = conn.BeginTransaction();
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO SincEntityFingerprints (
                    TenantId, BranchId, EntityType, ExternalId, 
                    FingerprintHash, LastDetectedChangeDate, LastConfirmedSyncDate, SyncStatus
                )
                VALUES (
                    @t, @b, @e, @id, 
                    @hash, @now, @now, 'Synced'
                )
                ON CONFLICT(TenantId, BranchId, EntityType, ExternalId) DO UPDATE SET
                    FingerprintHash = excluded.FingerprintHash,
                    LastDetectedChangeDate = excluded.LastDetectedChangeDate,
                    LastConfirmedSyncDate = excluded.LastConfirmedSyncDate,
                    SyncStatus = 'Synced';
            ";

            var pTenant = cmd.Parameters.Add("@t", SqliteType.Text);
            var pBranch = cmd.Parameters.Add("@b", SqliteType.Text);
            var pEntity = cmd.Parameters.Add("@e", SqliteType.Text);
            var pId = cmd.Parameters.Add("@id", SqliteType.Text);
            var pHash = cmd.Parameters.Add("@hash", SqliteType.Text);
            var pNow = cmd.Parameters.Add("@now", SqliteType.Text);

            var tStr = FormatTenant(tenantId);
            var bStr = FormatBranch(branchId);
            var nowStr = DateTime.UtcNow.ToString("o");

            pTenant.Value = tStr;
            pBranch.Value = bStr;
            pEntity.Value = entityType;
            pNow.Value = nowStr;

            foreach (var (externalId, hash) in items)
            {
                pId.Value = externalId;
                pHash.Value = hash;
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }
    }

    public void UpdateSyncStats(Guid tenantId, Guid? branchId, string entityType, int sentCount, int newCount, int modifiedCount)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO SincControl (
                    TenantId, BranchId, EntityType, 
                    LastSyncDate, LastSyncCount, LastSyncNewCount, LastSyncModifiedCount, EngineVersion
                )
                VALUES (
                    @t, @b, @e, 
                    @now, @sent, @new, @mod, '2.6.0'
                )
                ON CONFLICT(TenantId, BranchId, EntityType) DO UPDATE SET
                    LastSyncDate = excluded.LastSyncDate,
                    LastSyncCount = excluded.LastSyncCount,
                    LastSyncNewCount = excluded.LastSyncNewCount,
                    LastSyncModifiedCount = excluded.LastSyncModifiedCount;
            ";
            cmd.Parameters.AddWithValue("@t", FormatTenant(tenantId));
            cmd.Parameters.AddWithValue("@b", FormatBranch(branchId));
            cmd.Parameters.AddWithValue("@e", entityType);
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("@sent", sentCount);
            cmd.Parameters.AddWithValue("@new", newCount);
            cmd.Parameters.AddWithValue("@mod", modifiedCount);

            cmd.ExecuteNonQuery();
        }
    }

    public (bool InitialCompleted, DateTime? LastSync, int LastCount, int NewCount, int ModCount) GetSyncSummary(Guid tenantId, Guid? branchId, string entityType)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT InitialSyncCompleted, LastSyncDate, LastSyncCount, LastSyncNewCount, LastSyncModifiedCount 
                FROM SincControl 
                WHERE TenantId = @t AND BranchId = @b AND EntityType = @e;
            ";
            cmd.Parameters.AddWithValue("@t", FormatTenant(tenantId));
            cmd.Parameters.AddWithValue("@b", FormatBranch(branchId));
            cmd.Parameters.AddWithValue("@e", entityType);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var initDone = reader.GetInt64(0) == 1;
                DateTime? lastSync = !reader.IsDBNull(1) && DateTime.TryParse(reader.GetString(1), out var dt) ? dt : null;
                var lastCount = reader.GetInt32(2);
                var newCount = reader.GetInt32(3);
                var modCount = reader.GetInt32(4);
                return (initDone, lastSync, lastCount, newCount, modCount);
            }

            return (false, null, 0, 0, 0);
        }
    }

    public int PurgeForeignData(Guid currentTenantId, Guid? currentBranchId)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                DELETE FROM SincEntityFingerprints WHERE TenantId <> @t;
                DELETE FROM SincControl WHERE TenantId <> @t;
            ";
            cmd.Parameters.AddWithValue("@t", FormatTenant(currentTenantId));
            return cmd.ExecuteNonQuery();
        }
    }

    public void ResetTenantSync(Guid tenantId, Guid? branchId, string? entityType = null)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            if (string.IsNullOrEmpty(entityType))
            {
                cmd.CommandText = @"
                    DELETE FROM SincEntityFingerprints WHERE TenantId = @t AND BranchId = @b;
                    DELETE FROM SincControl WHERE TenantId = @t AND BranchId = @b;
                ";
            }
            else
            {
                cmd.CommandText = @"
                    DELETE FROM SincEntityFingerprints WHERE TenantId = @t AND BranchId = @b AND EntityType = @e;
                    DELETE FROM SincControl WHERE TenantId = @t AND BranchId = @b AND EntityType = @e;
                ";
                cmd.Parameters.AddWithValue("@e", entityType);
            }
            cmd.Parameters.AddWithValue("@t", FormatTenant(tenantId));
            cmd.Parameters.AddWithValue("@b", FormatBranch(branchId));
            cmd.ExecuteNonQuery();
        }
    }

    public void Dispose()
    {
        // SqliteConnection pooled
    }
}
