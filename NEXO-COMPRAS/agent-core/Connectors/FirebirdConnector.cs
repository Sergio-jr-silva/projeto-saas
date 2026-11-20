using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nexo.Agent.Core.Mapping;
using Nexo.Agent.Core.Schema;

namespace Nexo.Agent.Core.Connectors;

public class FirebirdConnector : IDatabaseConnector
{
    private string _connectionString;
    private readonly string _databasePath;
    private readonly string _configuredCharset;
    private readonly ILogger<FirebirdConnector>? _logger;
    private readonly SchemaMappingStore _mappingStore;
    private readonly ISchemaDiscoveryService _discoveryService;
    private ResolvedTableMapping _mapping = new();

    public string ConnectorName => "Firebird ERP Connector";
    public string DatabaseType => "Firebird";
    public string DatabasePathOrName => _databasePath;
    public ResolvedTableMapping CurrentMapping => _mapping;

    static FirebirdConnector()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
        catch { }
    }

    public FirebirdConnector(
        IConfiguration config,
        FieldMappingConfig? legacyMapping = null,
        ILogger<FirebirdConnector>? logger = null)
    {
        var fbHost = config["Firebird:Host"] ?? "127.0.0.1";
        var fbPort = config["Firebird:Port"] ?? "3050";
        var rawDb = config["Firebird:Database"] ?? @"C:\DATACASH\DADOS\BM.FDB";
        _databasePath = ResolveDatabasePath(rawDb);
        var fbUser = config["Firebird:User"] ?? "SYSDBA";
        var fbPass = config["Firebird:Password"] ?? "masterkey";
        var fbChar = config["Firebird:Charset"] ?? "NONE";
        var dialect = config["Firebird:Dialect"] ?? "3";

        _configuredCharset = string.IsNullOrWhiteSpace(fbChar) ? "NONE" : fbChar.Trim();
        _connectionString = BuildConnectionString(fbHost, fbPort, _databasePath, fbUser, fbPass, _configuredCharset, dialect);
        _logger = logger;
        _mappingStore = new SchemaMappingStore();
        _discoveryService = new FirebirdSchemaDiscoveryService(_connectionString, _databasePath, _logger);

        // Tenta carregar mapeamento salvo em disco correspondente a esta base específica
        var saved = _mappingStore.LoadMapping();
        if (saved != null && saved.DatabaseType == "Firebird" && saved.Produtos.Valido &&
            string.Equals(saved.DatabasePathOrName, _databasePath, StringComparison.OrdinalIgnoreCase))
        {
            _mapping = saved;
            _logger?.LogInformation("Mapeamento de schema Firebird carregado do cache local (Tabelas: Prod='{Prod}', Est='{Est}', Forn='{Forn}').",
                _mapping.Produtos.TableName, _mapping.Estoque.TableName, _mapping.Fornecedores.TableName);
        }

        if (!string.Equals(rawDb, _databasePath, StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogInformation("Caminho do banco Firebird ajustado para o arquivo existente no disco: {ResolvedPath}", _databasePath);
        }
    }

    private static string ResolveDatabasePath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath)) return rawPath;
        var trimmed = rawPath.Trim();

        if (File.Exists(trimmed)) return trimmed;

        try
        {
            var dir = Path.GetDirectoryName(trimmed);
            var file = Path.GetFileName(trimmed);
            if (!string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(file))
            {
                var candidateInDados = Path.Combine(dir, "DADOS", file);
                if (File.Exists(candidateInDados)) return candidateInDados;

                var candidateRoot = Path.Combine(dir, file);
                if (File.Exists(candidateRoot)) return candidateRoot;
            }
        }
        catch { }

        return trimmed;
    }

    private static string BuildConnectionString(string host, string port, string database, string user, string pass, string charset, string dialect)
    {
        var csb = new FbConnectionStringBuilder
        {
            ServerType = FbServerType.Default,
            DataSource = host,
            Port = int.TryParse(port, out var p) ? p : 3050,
            Database = database,
            UserID = user,
            Password = pass,
            Dialect = int.TryParse(dialect, out var d) ? d : 3,
            Pooling = true
        };

        var norm = charset.ToUpperInvariant().Trim();
        if (norm == "WIN1252" || norm == "WINDOWS-1252")
            csb.Charset = "WIN1252";
        else if (norm == "UTF8" || norm == "UTF-8")
            csb.Charset = "UTF8";
        else if (norm == "ISO8859_1" || norm == "ISO-8859-1" || norm == "LATIN1")
            csb.Charset = "ISO8859_1";
        else if (norm == "NONE" || string.IsNullOrEmpty(norm))
            csb.Charset = "NONE";
        else
            csb.Charset = norm;

        return csb.ToString();
    }

    private async Task<FbConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var conn = new FbConnection(_connectionString);
            await conn.OpenAsync(ct);
            return conn;
        }
        catch (Exception ex) when (ex.Message.Contains("character set", StringComparison.OrdinalIgnoreCase) ||
                                  ex.Message.Contains("charset", StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogWarning("Charset '{Charset}' não suportado pelo banco local ({Msg}). Aplicando fallback automático para 'NONE'...", _configuredCharset, ex.Message);
            var fallbackCsb = new FbConnectionStringBuilder(_connectionString) { Charset = "NONE" };
            var fallbackConn = new FbConnection(fallbackCsb.ToString());
            await fallbackConn.OpenAsync(ct);

            _connectionString = fallbackCsb.ToString();
            return fallbackConn;
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("Teste de conexão Firebird falhou: {Msg}", ex.Message);
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
            _logger?.LogInformation("Executando descoberta automática de metadados no banco Firebird...");
            var schema = await _discoveryService.DiscoverSchemaAsync(ct);
            var resolved = SchemaMappingResolver.Resolve(schema);

            _mapping = resolved;
            _mappingStore.SaveMapping(resolved);

            _logger?.LogInformation("Mapeamento inteligente concluído com sucesso: Produtos='{Prod}', Estoque='{Est}' (Campo Qtd='{Qtd}'), Fornecedores='{Forn}'.",
                resolved.Produtos.TableName, resolved.Estoque.TableName, resolved.Estoque.EstoqueAtualCol, resolved.Fornecedores.TableName);

            return resolved;
        }
        catch (Exception ex)
        {
            _logger?.LogError("Erro na descoberta do schema Firebird: {Msg}", ex.Message);
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
            throw new InvalidOperationException($"Estrutura de Produtos não identificada no banco Firebird: {p.MotivoInvalido}");
        }

        var list = new List<ErpProdutoDto>();

        try
        {
            await using var conn = await OpenConnectionAsync(ct);
            using var cmd = conn.CreateCommand();

            var sb = new StringBuilder();
            sb.Append("SELECT FIRST 50000 ");
            sb.Append($"p.{p.CodigoCol} AS CODIGO, ");
            sb.Append($"p.{p.DescricaoCol} AS NOME ");

            if (!string.IsNullOrEmpty(p.CodigoBarrasCol)) sb.Append($", p.{p.CodigoBarrasCol} AS GTIN ");
            if (!string.IsNullOrEmpty(p.UnidadeCol)) sb.Append($", p.{p.UnidadeCol} AS UNIDADE ");
            if (!string.IsNullOrEmpty(p.PrecoVendaCol)) sb.Append($", p.{p.PrecoVendaCol} AS PRECO_VENDA ");
            if (!string.IsNullOrEmpty(p.CustoAtualCol)) sb.Append($", p.{p.CustoAtualCol} AS CUSTO_TOTAL ");
            if (!string.IsNullOrEmpty(p.EstoqueMinimoCol)) sb.Append($", p.{p.EstoqueMinimoCol} AS ESTOQUE_MINIMO ");
            if (!string.IsNullOrEmpty(p.FornecedorErpIdCol)) sb.Append($", p.{p.FornecedorErpIdCol} AS ID_FORNECEDOR ");

            sb.Append($" FROM {p.TableName} p ");

            var whereAdded = false;
            if (!string.IsNullOrEmpty(p.EliminadoCol))
            {
                sb.Append($" WHERE (p.{p.EliminadoCol} IS NULL OR p.{p.EliminadoCol} <> 'S') ");
                whereAdded = true;
            }

            if (since.HasValue && !string.IsNullOrEmpty(p.DataAtualizacaoCol) && !p.DataAtualizacaoCol.Equals("DT_CADASTRO", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(whereAdded ? " AND " : " WHERE ");
                sb.Append($"p.{p.DataAtualizacaoCol} >= @p_since ");
                cmd.Parameters.Add("@p_since", FirebirdSql.Data.FirebirdClient.FbDbType.TimeStamp).Value = since.Value;
            }

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
        catch (Exception ex)
        {
            _logger?.LogError("Erro ao consultar produtos no Firebird: {Msg}", ex.Message);
            throw;
        }
    }

    public async Task<IEnumerable<ErpFornecedorDto>> GetSuppliersAsync(DateTime? since = null, CancellationToken ct = default)
    {
        await EnsureMappingLoadedAsync(ct);
        var f = _mapping.Fornecedores;

        if (!f.Valido)
        {
            throw new InvalidOperationException($"Estrutura de Fornecedores não identificada no banco Firebird: {f.MotivoInvalido}");
        }

        var list = new List<ErpFornecedorDto>();

        try
        {
            await using var conn = await OpenConnectionAsync(ct);
            using var cmd = conn.CreateCommand();

            var sb = new StringBuilder();
            sb.Append("SELECT FIRST 50000 ");
            sb.Append($"f.{f.CodigoCol} AS ID, ");
            sb.Append($"f.{f.RazaoSocialCol} AS NOME ");

            if (!string.IsNullOrEmpty(f.NomeFantasiaCol)) sb.Append($", f.{f.NomeFantasiaCol} AS NOME_FANTASIA ");
            if (!string.IsNullOrEmpty(f.CnpjCol)) sb.Append($", f.{f.CnpjCol} AS CNPJ_CPF ");
            if (!string.IsNullOrEmpty(f.TelefoneCol)) sb.Append($", f.{f.TelefoneCol} AS FONE ");
            if (!string.IsNullOrEmpty(f.EmailCol)) sb.Append($", f.{f.EmailCol} AS EMAIL ");
            if (!string.IsNullOrEmpty(f.CidadeCol)) sb.Append($", f.{f.CidadeCol} AS CIDADE ");
            if (!string.IsNullOrEmpty(f.UfCol)) sb.Append($", f.{f.UfCol} AS UF ");

            sb.Append($" FROM {f.TableName} f ");

            var whereClauses = new List<string>();
            if (!string.IsNullOrEmpty(f.FiltroTipoFornecedorCol))
            {
                whereClauses.Add($"(UPPER(TRIM(f.{f.FiltroTipoFornecedorCol})) IN ('S', '1', 'T', 'TRUE', 'F') OR UPPER(f.{f.FiltroTipoFornecedorCol}) LIKE 'FOR%')");
            }
            if (!string.IsNullOrEmpty(f.EliminadoCol))
            {
                whereClauses.Add($"(f.{f.EliminadoCol} IS NULL OR f.{f.EliminadoCol} <> 'S')");
            }

            if (whereClauses.Count > 0)
            {
                sb.Append(" WHERE ").Append(string.Join(" AND ", whereClauses));
            }

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
        catch (Exception ex)
        {
            _logger?.LogError("Erro ao consultar fornecedores no Firebird: {Msg}", ex.Message);
            throw;
        }
    }

    public async Task<IEnumerable<ErpEstoqueDto>> GetStockAsync(CancellationToken ct = default)
    {
        await EnsureMappingLoadedAsync(ct);
        var est = _mapping.Estoque;
        var prod = _mapping.Produtos;

        if (!est.Valido)
        {
            throw new InvalidOperationException($"Não foi possível identificar automaticamente a estrutura de estoque no Firebird. Motivo: {est.MotivoInvalido}");
        }

        var list = new List<ErpEstoqueDto>();

        try
        {
            await using var conn = await OpenConnectionAsync(ct);
            using var cmd = conn.CreateCommand();

            if (est.HasDedicatedTable)
            {
                // Tabela dedicada (ex: PRODUTO_ESTOQUE pe LEFT JOIN PRODUTO p)
                // NUNCA usar campos fixos como p.LQUANT! Usa est.EstoqueAtualCol identificado nos metadados!
                var pendenteSql = !string.IsNullOrEmpty(est.EstoquePendenteCol)
                    ? $"COALESCE(pe.{est.EstoquePendenteCol}, 0)"
                    : "0";

                var joinCol = !string.IsNullOrEmpty(est.ProdutoJoinCol) ? est.ProdutoJoinCol : prod.CodigoCol;

                var sb = new StringBuilder();
                sb.Append("SELECT FIRST 50000 ");
                sb.Append($"p.{prod.CodigoCol} AS CODIGO, ");
                sb.Append($"pe.{est.EstoqueAtualCol} AS ESTOQUE_ATUAL, ");
                sb.Append($"{pendenteSql} AS PENDENTE ");
                sb.Append($"FROM {prod.TableName} p ");
                sb.Append($"LEFT JOIN {est.TableName} pe ON pe.{est.ProdutoRefCol} = p.{joinCol} ");

                if (!string.IsNullOrEmpty(prod.EliminadoCol))
                {
                    sb.Append($"WHERE (p.{prod.EliminadoCol} IS NULL OR p.{prod.EliminadoCol} <> 'S')");
                }

                cmd.CommandText = sb.ToString();
            }
            else
            {
                // Estoque armazenado diretamente na própria tabela de produtos
                var pendenteSql = !string.IsNullOrEmpty(est.EstoquePendenteCol)
                    ? $"COALESCE(p.{est.EstoquePendenteCol}, 0)"
                    : "0";

                var sb = new StringBuilder();
                sb.Append("SELECT FIRST 50000 ");
                sb.Append($"p.{prod.CodigoCol} AS CODIGO, ");
                sb.Append($"p.{est.EstoqueAtualCol} AS ESTOQUE_ATUAL, ");
                sb.Append($"{pendenteSql} AS PENDENTE ");
                sb.Append($"FROM {prod.TableName} p ");

                if (!string.IsNullOrEmpty(prod.EliminadoCol))
                {
                    sb.Append($"WHERE (p.{prod.EliminadoCol} IS NULL OR p.{prod.EliminadoCol} <> 'S')");
                }

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
        catch (Exception ex)
        {
            _logger?.LogError("Erro ao consultar estoque no Firebird: {Msg}", ex.Message);
            throw;
        }
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
