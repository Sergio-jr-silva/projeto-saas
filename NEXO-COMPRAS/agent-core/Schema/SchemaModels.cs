using System;
using System.Collections.Generic;
using System.Linq;

namespace Nexo.Agent.Core.Schema;

public record DbColumnInfo(
    string Name,
    string DataType,
    int Length,
    int Scale,
    bool IsNullable
);

public record DbForeignKeyInfo(
    string ConstraintName,
    string ColumnName,
    string ForeignTable,
    string ForeignColumn
);

public class DbTableInfo
{
    public string Name { get; set; } = string.Empty;
    public List<DbColumnInfo> Columns { get; set; } = new();
    public List<string> PrimaryKeys { get; set; } = new();
    public List<DbForeignKeyInfo> ForeignKeys { get; set; } = new();

    public bool HasColumn(string colName) =>
        Columns.Any(c => string.Equals(c.Name, colName, StringComparison.OrdinalIgnoreCase));

    public DbColumnInfo? FindColumn(params string[] candidates)
    {
        foreach (var cand in candidates)
        {
            var match = Columns.FirstOrDefault(c => string.Equals(c.Name, cand, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }
        return null;
    }
}

public class DiscoveredSchema
{
    public string DatabaseType { get; set; } = "Firebird";
    public string DatabaseName { get; set; } = string.Empty;
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, DbTableInfo> Tables { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool HasTable(string tableName) =>
        Tables.ContainsKey(tableName);

    public DbTableInfo? FindTable(params string[] candidates)
    {
        foreach (var cand in candidates)
        {
            if (Tables.TryGetValue(cand, out var t)) return t;
        }
        return null;
    }
}

public class ResolvedProdutoMapping
{
    public bool Valido { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string CodigoCol { get; set; } = string.Empty;
    public string DescricaoCol { get; set; } = string.Empty;
    public string? UnidadeCol { get; set; }
    public string? CodigoBarrasCol { get; set; }
    public string? PrecoVendaCol { get; set; }
    public string? CustoAtualCol { get; set; }
    public string? EstoqueMinimoCol { get; set; }
    public string? FornecedorErpIdCol { get; set; }
    public string? EliminadoCol { get; set; }
    public string? DataAtualizacaoCol { get; set; }
    public string? MotivoInvalido { get; set; }
}

public class ResolvedEstoqueMapping
{
    public bool Valido { get; set; }
    public bool HasDedicatedTable { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string EstoqueAtualCol { get; set; } = string.Empty;
    public string? EstoquePendenteCol { get; set; }
    public string? ProdutoRefCol { get; set; }
    public string? ProdutoJoinCol { get; set; }
    public string? FilialCol { get; set; }
    public string? MotivoInvalido { get; set; }
}

public class ResolvedFornecedorMapping
{
    public bool Valido { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string CodigoCol { get; set; } = string.Empty;
    public string RazaoSocialCol { get; set; } = string.Empty;
    public string? NomeFantasiaCol { get; set; }
    public string? CnpjCol { get; set; }
    public string? TelefoneCol { get; set; }
    public string? EmailCol { get; set; }
    public string? CidadeCol { get; set; }
    public string? UfCol { get; set; }
    public string? FiltroTipoFornecedorCol { get; set; }
    public string? EliminadoCol { get; set; }
    public string? MotivoInvalido { get; set; }
}

public class ResolvedTableMapping
{
    public string DatabaseType { get; set; } = "Firebird";
    public string DatabasePathOrName { get; set; } = string.Empty;
    public DateTime IdentificadoEm { get; set; } = DateTime.UtcNow;
    public string Versao { get; set; } = "2.0";

    public ResolvedProdutoMapping Produtos { get; set; } = new();
    public ResolvedEstoqueMapping Estoque { get; set; } = new();
    public ResolvedFornecedorMapping Fornecedores { get; set; } = new();
}

