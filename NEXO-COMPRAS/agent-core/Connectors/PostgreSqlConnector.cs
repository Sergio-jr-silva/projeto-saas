using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nexo.Agent.Core.Mapping;
using Nexo.Agent.Core.Schema;
using Npgsql;

namespace Nexo.Agent.Core.Connectors;

public class PostgreSqlConnector : IDatabaseConnector
{
    private readonly string _connectionString;
    private readonly string _databaseName;
    private readonly string _schema;
    private readonly ILogger<PostgreSqlConnector>? _logger;
    private readonly SchemaMappingStore _mappingStore;
    private readonly ISchemaDiscoveryService _discoveryService;
    private ResolvedTableMapping _mapping = new();

    public string ConnectorName => "PostgreSQL ERP Connector";
    public string DatabaseType => "PostgreSQL";
    public string DatabasePathOrName => _databaseName;
    public ResolvedTableMapping CurrentMapping => _mapping;

    public PostgreSqlConnector(
        IConfiguration config,
        FieldMappingConfig? legacyMapping = null,
        ILogger<PostgreSqlConnector>? logger = null)
    {
        var pgHost = config["PostgreSql:Host"] ?? "127.0.0.1";
        var pgPort = config["PostgreSql:Port"] ?? "5432";
        _databaseName = config["PostgreSql:Database"] ?? "sistema_comercial";
        var pgUser = config["PostgreSql:User"] ?? "postgres";
        var pgPass = config["PostgreSql:Password"] ?? "postgres";
        var sslMode = config["PostgreSql:SslMode"] ?? "Prefer";
        _schema = config["PostgreSql:Schema"] ?? "public";
        var timeout = config["PostgreSql:Timeout"] ?? "15";

        _connectionString = $"Host={pgHost};Port={pgPort};Database={_databaseName};Username={pgUser};Password={pgPass};SSL Mode={sslMode};Search Path={_schema};Timeout={timeout};Command Timeout={timeout};";
        _logger = logger;
        _mappingStore = new SchemaMappingStore();
        _discoveryService = new PostgreSqlSchemaDiscoveryService(_connectionString, _databaseName, _schema, _logger);

        var saved = _mappingStore.LoadMapping();
        if (saved != null && saved.DatabaseType == "PostgreSQL" && saved.Produtos.Valido)
        {
            _mapping = saved;
            _logger?.LogInformation("Mapeamento de schema PostgreSQL carregado do cache local (Tabelas: Prod='{Prod}', Est='{Est}', Forn='{Forn}').",
                _mapping.Produtos.TableName, _mapping.Estoque.TableName, _mapping.Fornecedores.TableName);
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("Teste de conexão PostgreSQL falhou: {Msg}", ex.Message);
            return false;
        }
    }

    public async Task<ResolvedTableMapping> DiscoverAndApplySchemaAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        if (!forceRefresh && _mapping != null && _mapping.Produtos.Valido && _mapping.Estoque.Valido)
        {
            return _mapping;
        }

        try
        {
            _logger?.LogInformation("Executando descoberta automática de metadados no banco PostgreSQL...");
            var schema = await _discoveryService.DiscoverSchemaAsync(ct);
            var resolved = SchemaMappingResolver.Resolve(schema);

            _mapping = resolved;
            _mappingStore.SaveMapping(resolved);

            _logger?.LogInformation("Mapeamento inteligente PostgreSQL concluído com sucesso: Produtos='{Prod}', Estoque='{Est}' (Campo Qtd='{Qtd}'), Fornecedores='{Forn}'.",
                resolved.Produtos.TableName, resolved.Estoque.TableName, resolved.Estoque.EstoqueAtualCol, resolved.Fornecedores.TableName);

            return resolved;
        }
        catch (Exception ex)
        {
            _logger?.LogError("Erro na descoberta do schema PostgreSQL: {Msg}", ex.Message);
            throw;
        }
    }

    private async Task EnsureMappingLoadedAsync(CancellationToken ct)
    {
        if (_mapping == null || !_mapping.Produtos.Valido)
        {
            await DiscoverAndApplySchemaAsync(false, ct);
        }
    }

    public async Task<IEnumerable<ErpProdutoDto>> GetProductsAsync(DateTime? since = null, CancellationToken ct = default)
    {
        await EnsureMappingLoadedAsync(ct);
        var p = _mapping.Produtos;

        if (!p.Valido)
        {
            throw new InvalidOperationException($"Estrutura de Produtos não identificada no banco PostgreSQL: {p.MotivoInvalido}");
        }

        var list = new List<ErpProdutoDto>();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        var sb = new StringBuilder();
        sb.Append($"SELECT \"p\".\"{p.CodigoCol}\" AS \"CODIGO\", \"p\".\"{p.DescricaoCol}\" AS \"NOME\" ");

        if (!string.IsNullOrEmpty(p.CodigoBarrasCol)) sb.Append($", \"p\".\"{p.CodigoBarrasCol}\" AS \"GTIN\" ");
        if (!string.IsNullOrEmpty(p.UnidadeCol)) sb.Append($", \"p\".\"{p.UnidadeCol}\" AS \"UNIDADE\" ");
        if (!string.IsNullOrEmpty(p.PrecoVendaCol)) sb.Append($", \"p\".\"{p.PrecoVendaCol}\" AS \"PRECO_VENDA\" ");
        if (!string.IsNullOrEmpty(p.CustoAtualCol)) sb.Append($", \"p\".\"{p.CustoAtualCol}\" AS \"CUSTO_TOTAL\" ");
        if (!string.IsNullOrEmpty(p.EstoqueMinimoCol)) sb.Append($", \"p\".\"{p.EstoqueMinimoCol}\" AS \"ESTOQUE_MINIMO\" ");
        if (!string.IsNullOrEmpty(p.FornecedorErpIdCol)) sb.Append($", \"p\".\"{p.FornecedorErpIdCol}\" AS \"ID_FORNECEDOR\" ");

        sb.Append($"FROM \"{_schema}\".\"{p.TableName}\" \"p\" ");

        var whereAdded = false;
        if (!string.IsNullOrEmpty(p.EliminadoCol))
        {
            sb.Append($"WHERE (\"p\".\"{p.EliminadoCol}\"::text IS NULL OR \"p\".\"{p.EliminadoCol}\"::text <> 'S') ");
            whereAdded = true;
        }

        if (since.HasValue && !string.IsNullOrEmpty(p.DataAtualizacaoCol))
        {
            sb.Append(whereAdded ? "AND " : "WHERE ");
            sb.Append($"\"p\".\"{p.DataAtualizacaoCol}\" >= @since ");
            cmd.Parameters.AddWithValue("since", since.Value);
        }

        sb.Append("LIMIT 3000");

        cmd.CommandText = sb.ToString();
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var cod = reader["CODIGO"]?.ToString()?.Trim() ?? "";
            var nome = reader["NOME"]?.ToString()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(cod) || string.IsNullOrWhiteSpace(nome)) continue;

            string? gtin = null;
            if (!string.IsNullOrEmpty(p.CodigoBarrasCol) && HasColumn(reader, "GTIN"))
            {
                gtin = reader["GTIN"]?.ToString()?.Trim();
                if (gtin == "N") gtin = null;
            }

            var un = (!string.IsNullOrEmpty(p.UnidadeCol) && HasColumn(reader, "UNIDADE"))
                ? reader["UNIDADE"]?.ToString()?.Trim() ?? "UN"
                : "UN";

            decimal preco = 0m;
            if (!string.IsNullOrEmpty(p.PrecoVendaCol) && HasColumn(reader, "PRECO_VENDA") && reader["PRECO_VENDA"] != DBNull.Value)
                preco = Convert.ToDecimal(reader["PRECO_VENDA"]);

            decimal custo = 0m;
            if (!string.IsNullOrEmpty(p.CustoAtualCol) && HasColumn(reader, "CUSTO_TOTAL") && reader["CUSTO_TOTAL"] != DBNull.Value)
                custo = Convert.ToDecimal(reader["CUSTO_TOTAL"]);

            decimal estMin = 0m;
            if (!string.IsNullOrEmpty(p.EstoqueMinimoCol) && HasColumn(reader, "ESTOQUE_MINIMO") && reader["ESTOQUE_MINIMO"] != DBNull.Value)
                estMin = Convert.ToDecimal(reader["ESTOQUE_MINIMO"]);

            string? fornId = null;
            if (!string.IsNullOrEmpty(p.FornecedorErpIdCol) && HasColumn(reader, "ID_FORNECEDOR") && reader["ID_FORNECEDOR"] != DBNull.Value)
                fornId = reader["ID_FORNECEDOR"]?.ToString();

            list.Add(new ErpProdutoDto(
                CodigoErp: cod,
                Descricao: nome,
                CodigoBarras: gtin,
                Unidade: un,
                Grupo: null,
                Categoria: null,
                Ncm: null,
                PrecoVenda: preco,
                CustoAtual: custo,
                FornecedorErpId: fornId,
                Consumo90Dias: 0m,
                UltimoCusto: custo,
                UltimaCompraEm: null,
                QuantidadeUltimaCompra: 0m,
                UltimoFornecedorErpId: fornId,
                EstoqueMinimo: estMin,
                UltimaAtualizacao: DateTime.UtcNow,
                Ativo: true
            ));
        }

        return list;
    }

    public async Task<IEnumerable<ErpFornecedorDto>> GetSuppliersAsync(DateTime? since = null, CancellationToken ct = default)
    {
        await EnsureMappingLoadedAsync(ct);
        var f = _mapping.Fornecedores;

        if (!f.Valido)
        {
            throw new InvalidOperationException($"Estrutura de Fornecedores não identificada no banco PostgreSQL: {f.MotivoInvalido}");
        }

        var list = new List<ErpFornecedorDto>();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        var sb = new StringBuilder();
        sb.Append($"SELECT \"f\".\"{f.CodigoCol}\" AS \"ID\", \"f\".\"{f.RazaoSocialCol}\" AS \"NOME\" ");

        if (!string.IsNullOrEmpty(f.NomeFantasiaCol)) sb.Append($", \"f\".\"{f.NomeFantasiaCol}\" AS \"NOME_FANTASIA\" ");
        if (!string.IsNullOrEmpty(f.CnpjCol)) sb.Append($", \"f\".\"{f.CnpjCol}\" AS \"CNPJ_CPF\" ");
        if (!string.IsNullOrEmpty(f.TelefoneCol)) sb.Append($", \"f\".\"{f.TelefoneCol}\" AS \"FONE\" ");
        if (!string.IsNullOrEmpty(f.EmailCol)) sb.Append($", \"f\".\"{f.EmailCol}\" AS \"EMAIL\" ");
        if (!string.IsNullOrEmpty(f.CidadeCol)) sb.Append($", \"f\".\"{f.CidadeCol}\" AS \"CIDADE\" ");
        if (!string.IsNullOrEmpty(f.UfCol)) sb.Append($", \"f\".\"{f.UfCol}\" AS \"UF\" ");

        sb.Append($"FROM \"{_schema}\".\"{f.TableName}\" \"f\" ");

        var whereClauses = new List<string>();
        if (!string.IsNullOrEmpty(f.FiltroTipoFornecedorCol))
        {
            whereClauses.Add($"(\"f\".\"{f.FiltroTipoFornecedorCol}\"::text = 'S' OR \"f\".\"{f.FiltroTipoFornecedorCol}\"::text = '1')");
        }
        if (!string.IsNullOrEmpty(f.EliminadoCol))
        {
            whereClauses.Add($"(\"f\".\"{f.EliminadoCol}\"::text IS NULL OR \"f\".\"{f.EliminadoCol}\"::text <> 'S')");
        }

        if (whereClauses.Count > 0)
        {
            sb.Append("WHERE ").Append(string.Join(" AND ", whereClauses)).Append(' ');
        }

        sb.Append("LIMIT 1000");

        cmd.CommandText = sb.ToString();
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var id = reader["ID"]?.ToString()?.Trim() ?? "";
            var nome = reader["NOME"]?.ToString()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(nome)) continue;

            var fantasia = (!string.IsNullOrEmpty(f.NomeFantasiaCol) && HasColumn(reader, "NOME_FANTASIA"))
                ? reader["NOME_FANTASIA"]?.ToString()?.Trim()
                : null;

            var cnpj = (!string.IsNullOrEmpty(f.CnpjCol) && HasColumn(reader, "CNPJ_CPF"))
                ? reader["CNPJ_CPF"]?.ToString()?.Trim()
                : null;

            var fone = (!string.IsNullOrEmpty(f.TelefoneCol) && HasColumn(reader, "FONE"))
                ? reader["FONE"]?.ToString()?.Trim()
                : null;

            var email = (!string.IsNullOrEmpty(f.EmailCol) && HasColumn(reader, "EMAIL"))
                ? reader["EMAIL"]?.ToString()?.Trim()
                : null;

            var cidade = (!string.IsNullOrEmpty(f.CidadeCol) && HasColumn(reader, "CIDADE"))
                ? reader["CIDADE"]?.ToString()?.Trim()
                : null;

            var uf = (!string.IsNullOrEmpty(f.UfCol) && HasColumn(reader, "UF"))
                ? reader["UF"]?.ToString()?.Trim()
                : null;

            list.Add(new ErpFornecedorDto(
                CodigoErp: id,
                RazaoSocial: nome,
                NomeFantasia: fantasia ?? nome,
                Cnpj: cnpj,
                InscricaoEstadual: null,
                Email: email,
                Telefone: fone,
                Whatsapp: null,
                Contato: null,
                Endereco: null,
                Cidade: cidade,
                Uf: uf,
                PrazoEntregaDias: 7
            ));
        }

        return list;
    }

    public async Task<IEnumerable<ErpEstoqueDto>> GetStockAsync(CancellationToken ct = default)
    {
        await EnsureMappingLoadedAsync(ct);
        var est = _mapping.Estoque;
        var prod = _mapping.Produtos;

        if (!est.Valido)
        {
            throw new InvalidOperationException($"Não foi possível identificar automaticamente a estrutura de estoque no PostgreSQL. Motivo: {est.MotivoInvalido}");
        }

        var list = new List<ErpEstoqueDto>();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();

        if (est.HasDedicatedTable)
        {
            var pendenteSql = !string.IsNullOrEmpty(est.EstoquePendenteCol)
                ? $"COALESCE(\"pe\".\"{est.EstoquePendenteCol}\", 0)"
                : "0";

            var joinCol = !string.IsNullOrEmpty(est.ProdutoJoinCol) ? est.ProdutoJoinCol : prod.CodigoCol;

            var sb = new StringBuilder();
            sb.Append($"SELECT \"p\".\"{prod.CodigoCol}\" AS \"CODIGO\", ");
            sb.Append($"\"pe\".\"{est.EstoqueAtualCol}\" AS \"ESTOQUE_ATUAL\", ");
            sb.Append($"{pendenteSql} AS \"PENDENTE\" ");
            sb.Append($"FROM \"{_schema}\".\"{prod.TableName}\" \"p\" ");
            sb.Append($"LEFT JOIN \"{_schema}\".\"{est.TableName}\" \"pe\" ON \"pe\".\"{est.ProdutoRefCol}\" = \"p\".\"{joinCol}\" ");

            if (!string.IsNullOrEmpty(prod.EliminadoCol))
            {
                sb.Append($"WHERE (\"p\".\"{prod.EliminadoCol}\"::text IS NULL OR \"p\".\"{prod.EliminadoCol}\"::text <> 'S') ");
            }

            sb.Append("LIMIT 3000");
            cmd.CommandText = sb.ToString();
        }
        else
        {
            var pendenteSql = !string.IsNullOrEmpty(est.EstoquePendenteCol)
                ? $"COALESCE(\"p\".\"{est.EstoquePendenteCol}\", 0)"
                : "0";

            var sb = new StringBuilder();
            sb.Append($"SELECT \"p\".\"{prod.CodigoCol}\" AS \"CODIGO\", ");
            sb.Append($"\"p\".\"{est.EstoqueAtualCol}\" AS \"ESTOQUE_ATUAL\", ");
            sb.Append($"{pendenteSql} AS \"PENDENTE\" ");
            sb.Append($"FROM \"{_schema}\".\"{prod.TableName}\" \"p\" ");

            if (!string.IsNullOrEmpty(prod.EliminadoCol))
            {
                sb.Append($"WHERE (\"p\".\"{prod.EliminadoCol}\"::text IS NULL OR \"p\".\"{prod.EliminadoCol}\"::text <> 'S') ");
            }

            sb.Append("LIMIT 3000");
            cmd.CommandText = sb.ToString();
        }

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var cod = reader["CODIGO"]?.ToString()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(cod)) continue;

            decimal? saldo = reader["ESTOQUE_ATUAL"] != DBNull.Value ? Convert.ToDecimal(reader["ESTOQUE_ATUAL"]) : null;
            var pend = reader["PENDENTE"] != DBNull.Value ? Convert.ToDecimal(reader["PENDENTE"]) : 0m;

            list.Add(new ErpEstoqueDto(cod, saldo, pend));
        }

        return list;
    }

    public async Task<IEnumerable<ErpPrecoDto>> GetPricesAsync(DateTime? since = null, CancellationToken ct = default)
    {
        var prods = await GetProductsAsync(since, ct);
        return prods.Select(p => new ErpPrecoDto(p.CodigoErp, p.CustoAtual, p.PrecoVenda, p.UltimaAtualizacao ?? DateTime.UtcNow, p.FornecedorErpId));
    }

    private static bool HasColumn(System.Data.Common.DbDataReader reader, string columnName)
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
