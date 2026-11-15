using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Nexo.Api.MultiTenancy;

/// <summary>
/// A cada abertura de conexão, grava no PostgreSQL as variáveis de sessão usadas pelas políticas de
/// Row-Level Security (app.tenant_id / app.bypass_rls). Como as conexões são reaproveitadas pelo pool,
/// os valores são SEMPRE redefinidos (inclusive para vazio), evitando vazamento entre requisições.
/// </summary>
public class TenantConnectionInterceptor(ITenantContext tenant) : DbConnectionInterceptor
{
    private const string Sql = "SELECT set_config('app.tenant_id', @t, false), set_config('app.bypass_rls', @b, false)";

    private DbCommand Build(DbConnection connection)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = Sql;
        var t = cmd.CreateParameter(); t.ParameterName = "t"; t.Value = tenant.CurrentTenantId?.ToString() ?? string.Empty; cmd.Parameters.Add(t);
        var b = cmd.CreateParameter(); b.ParameterName = "b"; b.Value = tenant.BypassRls ? "on" : "off"; cmd.Parameters.Add(b);
        return cmd;
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var cmd = Build(connection);
        cmd.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var cmd = Build(connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}

