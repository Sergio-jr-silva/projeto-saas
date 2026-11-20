using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Nexo.Agent.Services;

public record ErpProduto(
    string CodigoErp,
    string Descricao,
    string? CodigoBarras,
    string? Unidade,
    string? Grupo,
    string? Categoria,
    string? Ncm,
    decimal PrecoVenda,
    decimal CustoAtual,
    string? FornecedorErpId,
    decimal Consumo90Dias,
    decimal UltimoCusto,
    DateTime? UltimaCompraEm,
    decimal QuantidadeUltimaCompra,
    string? UltimoFornecedorErpId
);

public record ErpEstoque(
    string CodigoErp,
    decimal EstoqueAtual,
    decimal EstoquePendente
);

public record ErpFornecedor(
    string CodigoErp,
    string RazaoSocial,
    string? NomeFantasia,
    string? Cnpj,
    string? InscricaoEstadual,
    string? Email,
    string? Telefone,
    string? Whatsapp,
    string? Contato,
    string? Endereco,
    string? Cidade,
    string? Uf,
    int PrazoEntregaDias
);

public class FirebirdService
{
    private readonly string _connectionString;
    private readonly bool _simularSeIndisponivel;
    private readonly ILogger<FirebirdService> _logger;

    public FirebirdService(IConfiguration config, ILogger<FirebirdService> logger)
    {
        _connectionString = config["Firebird:ConnectionString"] ?? "";
        _simularSeIndisponivel = config.GetValue<bool>("Firebird:SimularSeBancoIndisponivel", true);
        _logger = logger;
    }

    private FbConnection CreateConnection() => new(_connectionString);

    public async Task<List<ErpProduto>> ObterProdutosAsync(CancellationToken ct = default)
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct);

            // Consulta no padrão ERP BM Informática (tabelas PRODUTO, com join ou subselect de histórico)
            var sql = @"
                SELECT 
                    p.CODIGO,
                    p.DESCRICAO,
                    p.CODBARRAS,
                    p.UNIDADE,
                    p.GRUPO,
                    p.CATEGORIA,
                    p.NCM,
                    COALESCE(p.PRECOVENDA, 0) AS PRECOVENDA,
                    COALESCE(p.PRECOCUSTO, 0) AS PRECOCUSTO,
                    p.FORNECEDOR_PADRAO,
                    COALESCE(p.ULTIMO_CUSTO, p.PRECOCUSTO, 0) AS ULTIMO_CUSTO,
                    p.ULTIMA_COMPRA_DATA,
                    COALESCE(p.ULTIMA_COMPRA_QTD, 0) AS ULTIMA_COMPRA_QTD,
                    p.ULTIMO_FORNECEDOR_ID,
                    COALESCE((
                        SELECT SUM(s.QUANTIDADE) 
                        FROM SAIDA_PRODUTO s 
                        WHERE s.PRODUTO_ID = p.CODIGO 
                          AND s.DATA_SAIDA >= CURRENT_DATE - 90
                    ), 0) AS CONSUMO_90_DIAS
                FROM PRODUTO p
                WHERE p.ATIVO = 'S' OR p.ATIVO IS NULL";

            await using var cmd = new FbCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            var lista = new List<ErpProduto>();
            while (await reader.ReadAsync(ct))
            {
                lista.Add(new ErpProduto(
                    CodigoErp: reader["CODIGO"]?.ToString()?.Trim() ?? "",
                    Descricao: reader["DESCRICAO"]?.ToString()?.Trim() ?? "",
                    CodigoBarras: reader["CODBARRAS"]?.ToString()?.Trim(),
                    Unidade: reader["UNIDADE"]?.ToString()?.Trim() ?? "UN",
                    Grupo: reader["GRUPO"]?.ToString()?.Trim() ?? "Geral",
                    Categoria: reader["CATEGORIA"]?.ToString()?.Trim(),
                    Ncm: reader["NCM"]?.ToString()?.Trim(),
                    PrecoVenda: Convert.ToDecimal(reader["PRECOVENDA"]),
                    CustoAtual: Convert.ToDecimal(reader["PRECOCUSTO"]),
                    FornecedorErpId: reader["FORNECEDOR_PADRAO"]?.ToString()?.Trim(),
                    Consumo90Dias: Convert.ToDecimal(reader["CONSUMO_90_DIAS"]),
                    UltimoCusto: Convert.ToDecimal(reader["ULTIMO_CUSTO"]),
                    UltimaCompraEm: reader["ULTIMA_COMPRA_DATA"] is DBNull ? null : Convert.ToDateTime(reader["ULTIMA_COMPRA_DATA"]),
                    QuantidadeUltimaCompra: Convert.ToDecimal(reader["ULTIMA_COMPRA_QTD"]),
                    UltimoFornecedorErpId: reader["ULTIMO_FORNECEDOR_ID"]?.ToString()?.Trim()
                ));
            }

            return lista;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao conectar no Firebird local.");
            if (_simularSeIndisponivel)
            {
                _logger.LogInformation("Modo simulação ativado: retornando produtos locais de demonstração.");
                return ObterProdutosSimulados();
            }
            throw;
        }
    }

    public async Task<List<ErpEstoque>> ObterEstoqueAsync(CancellationToken ct = default)
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct);

            var sql = @"
                SELECT 
                    p.CODIGO,
                    COALESCE(e.ESTOQUE_ATUAL, p.ESTOQUE, 0) AS ESTOQUE_ATUAL,
                    COALESCE(e.ESTOQUE_PENDENTE, 0) AS ESTOQUE_PENDENTE
                FROM PRODUTO p
                LEFT JOIN PRODUTO_ESTOQUE e ON e.PRODUTO_ID = p.CODIGO
                WHERE p.ATIVO = 'S' OR p.ATIVO IS NULL";

            await using var cmd = new FbCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            var lista = new List<ErpEstoque>();
            while (await reader.ReadAsync(ct))
            {
                lista.Add(new ErpEstoque(
                    CodigoErp: reader["CODIGO"]?.ToString()?.Trim() ?? "",
                    EstoqueAtual: Convert.ToDecimal(reader["ESTOQUE_ATUAL"]),
                    EstoquePendente: Convert.ToDecimal(reader["ESTOQUE_PENDENTE"])
                ));
            }

            return lista;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao ler estoque do Firebird.");
            if (_simularSeIndisponivel)
            {
                return ObterEstoqueSimulado();
            }
            throw;
        }
    }

    public async Task<List<ErpFornecedor>> ObterFornecedoresAsync(CancellationToken ct = default)
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct);

            var sql = @"
                SELECT 
                    f.CODIGO,
                    f.RAZAO_SOCIAL,
                    f.NOME_FANTASIA,
                    f.CNPJ_CPF,
                    f.INSCRICAO_ESTADUAL,
                    f.EMAIL,
                    f.TELEFONE,
                    f.CELULAR,
                    f.CONTATO,
                    f.ENDERECO,
                    f.CIDADE,
                    f.UF,
                    COALESCE(f.PRAZO_ENTREGA, 7) AS PRAZO_ENTREGA
                FROM PESSOA f
                WHERE (f.FORNECEDOR = 'S' OR f.TIPO = 'F') AND (f.ATIVO = 'S' OR f.ATIVO IS NULL)";

            await using var cmd = new FbCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            var lista = new List<ErpFornecedor>();
            while (await reader.ReadAsync(ct))
            {
                lista.Add(new ErpFornecedor(
                    CodigoErp: reader["CODIGO"]?.ToString()?.Trim() ?? "",
                    RazaoSocial: reader["RAZAO_SOCIAL"]?.ToString()?.Trim() ?? "",
                    NomeFantasia: reader["NOME_FANTASIA"]?.ToString()?.Trim(),
                    Cnpj: reader["CNPJ_CPF"]?.ToString()?.Trim(),
                    InscricaoEstadual: reader["INSCRICAO_ESTADUAL"]?.ToString()?.Trim(),
                    Email: reader["EMAIL"]?.ToString()?.Trim(),
                    Telefone: reader["TELEFONE"]?.ToString()?.Trim(),
                    Whatsapp: reader["CELULAR"]?.ToString()?.Trim(),
                    Contato: reader["CONTATO"]?.ToString()?.Trim(),
                    Endereco: reader["ENDERECO"]?.ToString()?.Trim(),
                    Cidade: reader["CIDADE"]?.ToString()?.Trim(),
                    Uf: reader["UF"]?.ToString()?.Trim(),
                    PrazoEntregaDias: Convert.ToInt32(reader["PRAZO_ENTREGA"])
                ));
            }

            return lista;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao ler fornecedores do Firebird.");
            if (_simularSeIndisponivel)
            {
                return ObterFornecedoresSimulados();
            }
            throw;
        }
    }

    private static List<ErpProduto> ObterProdutosSimulados() =>
    [
        new("PROD-001", "Arroz Branco Tipo 1 5kg", "7891000100011", "FD", "Alimentos", "Grãos", "1006.30.21", 28.90m, 21.50m, "FORN-001", 180m, 21.00m, DateTime.UtcNow.AddDays(-15), 50m, "FORN-001"),
        new("PROD-002", "Feijão Carioca 1kg", "7891000100028", "FD", "Alimentos", "Grãos", "0713.33.99", 8.49m, 6.10m, "FORN-001", 240m, 5.95m, DateTime.UtcNow.AddDays(-10), 100m, "FORN-001"),
        new("PROD-003", "Óleo de Soja 900ml", "7891000100035", "CX", "Alimentos", "Óleos", "1507.90.11", 6.89m, 5.20m, "FORN-002", 300m, 5.15m, DateTime.UtcNow.AddDays(-8), 80m, "FORN-002"),
        new("PROD-004", "Leite Integral UHT 1L", "7891000100042", "CX", "Laticínios", "Leites", "0401.20.10", 5.29m, 3.90m, "FORN-002", 450m, 3.85m, DateTime.UtcNow.AddDays(-5), 200m, "FORN-002"),
        new("PROD-005", "Café Tradicional 500g", "7891000100059", "FD", "Bebidas", "Cafés", "0901.21.00", 17.50m, 13.20m, "FORN-003", 120m, 12.90m, DateTime.UtcNow.AddDays(-20), 40m, "FORN-003"),
        new("PROD-006", "Açúcar Cristal 1kg", "7891000100066", "FD", "Alimentos", "Açúcar", "1701.99.00", 4.39m, 3.10m, "FORN-001", 320m, 3.05m, DateTime.UtcNow.AddDays(-12), 150m, "FORN-001"),
        new("PROD-007", "Detergente Líquido 500ml", "7891000100073", "CX", "Limpeza", "Lavagem", "3402.20.00", 2.49m, 1.65m, "FORN-003", 260m, 1.60m, DateTime.UtcNow.AddDays(-18), 120m, "FORN-003"),
        new("PROD-008", "Sabão em Pó 1kg", "7891000100080", "CX", "Limpeza", "Lavanderia", "3401.20.90", 11.90m, 8.50m, "FORN-003", 95m, 8.40m, DateTime.UtcNow.AddDays(-22), 60m, "FORN-003")
    ];

    private static List<ErpEstoque> ObterEstoqueSimulado() =>
    [
        new("PROD-001", 12m, 0m),  // Crítico / Baixo
        new("PROD-002", 0m, 0m),   // Zerado
        new("PROD-003", 25m, 0m),  // Baixo
        new("PROD-004", 40m, 0m),  // Baixo
        new("PROD-005", 5m, 0m),   // Crítico
        new("PROD-006", 110m, 0m), // Normal
        new("PROD-007", 18m, 0m),  // Crítico
        new("PROD-008", 85m, 0m)   // Normal
    ];

    private static List<ErpFornecedor> ObterFornecedoresSimulados() =>
    [
        new("FORN-001", "Alimentos Nordeste Distribuidora Ltda", "Nordeste Distribuição", "11222333000144", "012345678", "vendas@nordestedist.com.br", "(81) 3456-7890", "(81) 98765-4321", "Carlos Santos", "Av. Industrial, 1500", "Recife", "PE", 5),
        new("FORN-002", "Laticínios & Óleos Brasil S/A", "Laticínios Brasil", "22333444000155", "987654321", "pedidos@laticiniosbrasil.com.br", "(81) 3222-1111", "(81) 99111-2222", "Mariana Oliveira", "Rod. BR-101 Sul, Km 22", "Jaboatão dos Guararapes", "PE", 7),
        new("FORN-003", "Comercial e Distribuidora Universal Ltda", "Universal Distribuidora", "33444555000166", "112233445", "comercial@universal.com.br", "(81) 3789-0011", "(81) 99988-7766", "Roberto Albuquerque", "Distrito Industrial, Galpão B", "Caruaru", "PE", 10)
    ];
}

