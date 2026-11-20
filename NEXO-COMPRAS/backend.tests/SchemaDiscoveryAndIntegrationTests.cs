using System;
using System.Collections.Generic;
using System.Linq;
using Nexo.Agent.Core.Schema;
using Xunit;

namespace Nexo.Api.Tests;

public class SchemaDiscoveryAndIntegrationTests
{
    [Fact]
    public void SchemaMappingResolver_DataCashBM_CorrectlyIdentifies_Loja_And_Never_Lquant()
    {
        // Simula schema real do banco Firebird BM / DataCash (como em BM.FDB)
        var schema = new DiscoveredSchema
        {
            DatabaseType = "Firebird",
            DatabaseName = @"C:\DATACASH\BM.FDB"
        };

        // 1. Tabela PRODUTO (sem LQUANT)
        var prodTable = new DbTableInfo
        {
            Name = "PRODUTO",
            Columns = new List<DbColumnInfo>
            {
                new("CODIGO", "VARCHAR", 30, 0, false),
                new("ID", "INTEGER", 4, 0, false),
                new("NOME", "VARCHAR", 100, 0, false),
                new("UNIDADE", "VARCHAR", 6, 0, true),
                new("PRECO_VENDA", "NUMERIC", 15, 2, true),
                new("CUSTO_TOTAL", "NUMERIC", 15, 2, true),
                new("ESTOQUE_MINIMO", "NUMERIC", 15, 2, true),
                new("ID_FORNECEDOR", "INTEGER", 4, 0, true),
                new("GTIN", "VARCHAR", 20, 0, true),
                new("ELIMINADO", "CHAR", 1, 0, true)
            },
            PrimaryKeys = new List<string> { "ID" }
        };
        schema.Tables["PRODUTO"] = prodTable;

        // 2. Tabela PRODUTO_ESTOQUE (com LOJA, PENDENCIA, ID_PRODUTO)
        var estTable = new DbTableInfo
        {
            Name = "PRODUTO_ESTOQUE",
            Columns = new List<DbColumnInfo>
            {
                new("ID_PRODUTO", "INTEGER", 4, 0, false),
                new("LOJA", "NUMERIC", 15, 3, false),
                new("DEPOSITO", "NUMERIC", 15, 3, true),
                new("PENDENCIA", "NUMERIC", 15, 3, true)
            }
        };
        schema.Tables["PRODUTO_ESTOQUE"] = estTable;

        // 3. Tabela PESSOA (Fornecedores do DataCash)
        var fornTable = new DbTableInfo
        {
            Name = "PESSOA",
            Columns = new List<DbColumnInfo>
            {
                new("ID", "INTEGER", 4, 0, false),
                new("NOME", "VARCHAR", 100, 0, false),
                new("NOME_FANTASIA", "VARCHAR", 100, 0, true),
                new("CNPJ_CPF", "VARCHAR", 20, 0, true),
                new("FONE", "VARCHAR", 20, 0, true),
                new("EMAIL", "VARCHAR", 100, 0, true),
                new("CIDADE", "VARCHAR", 60, 0, true),
                new("UF", "CHAR", 2, 0, true),
                new("FORNECEDOR", "CHAR", 1, 0, true),
                new("ELIMINADO", "CHAR", 1, 0, true)
            },
            PrimaryKeys = new List<string> { "ID" }
        };
        schema.Tables["PESSOA"] = fornTable;

        // Executa resolução automática do schema
        var mapping = SchemaMappingResolver.Resolve(schema);

        // Asserções Produtos
        Assert.True(mapping.Produtos.Valido);
        Assert.Equal("PRODUTO", mapping.Produtos.TableName);
        Assert.Equal("CODIGO", mapping.Produtos.CodigoCol);
        Assert.Equal("NOME", mapping.Produtos.DescricaoCol);
        Assert.Equal("PRECO_VENDA", mapping.Produtos.PrecoVendaCol);
        Assert.Equal("CUSTO_TOTAL", mapping.Produtos.CustoAtualCol);

        // Asserções Estoque (deve usar LOJA e NÃO LQUANT)
        Assert.True(mapping.Estoque.Valido);
        Assert.True(mapping.Estoque.HasDedicatedTable);
        Assert.Equal("PRODUTO_ESTOQUE", mapping.Estoque.TableName);
        Assert.Equal("LOJA", mapping.Estoque.EstoqueAtualCol);
        Assert.NotEqual("LQUANT", mapping.Estoque.EstoqueAtualCol);
        Assert.Equal("PENDENCIA", mapping.Estoque.EstoquePendenteCol);
        Assert.Equal("ID_PRODUTO", mapping.Estoque.ProdutoRefCol);
        Assert.Equal("ID", mapping.Estoque.ProdutoJoinCol);

        // Asserções Fornecedores
        Assert.True(mapping.Fornecedores.Valido);
        Assert.Equal("PESSOA", mapping.Fornecedores.TableName);
        Assert.Equal("ID", mapping.Fornecedores.CodigoCol);
        Assert.Equal("NOME", mapping.Fornecedores.RazaoSocialCol);
        Assert.Equal("CNPJ_CPF", mapping.Fornecedores.CnpjCol);
        Assert.Equal("FORNECEDOR", mapping.Fornecedores.FiltroTipoFornecedorCol);
    }

    [Fact]
    public void SchemaMappingResolver_GenericLayout_StockDirectInProductTable()
    {
        // Simula ERP com estoque diretamente na tabela PRODUTOS
        var schema = new DiscoveredSchema
        {
            DatabaseType = "PostgreSQL",
            DatabaseName = "sistema_comercial"
        };

        var prodTable = new DbTableInfo
        {
            Name = "PRODUTOS",
            Columns = new List<DbColumnInfo>
            {
                new("CODIGO", "VARCHAR", 30, 0, false),
                new("DESCRICAO", "VARCHAR", 100, 0, false),
                new("QUANTIDADE", "NUMERIC", 15, 2, false),
                new("PRECOVENDA", "NUMERIC", 15, 2, true),
                new("PRECOCUSTO", "NUMERIC", 15, 2, true)
            }
        };
        schema.Tables["PRODUTOS"] = prodTable;

        var fornTable = new DbTableInfo
        {
            Name = "FORNECEDORES",
            Columns = new List<DbColumnInfo>
            {
                new("CODIGO", "VARCHAR", 20, 0, false),
                new("RAZAO", "VARCHAR", 100, 0, false),
                new("CNPJ", "VARCHAR", 14, 0, false)
            }
        };
        schema.Tables["FORNECEDORES"] = fornTable;

        var mapping = SchemaMappingResolver.Resolve(schema);

        Assert.True(mapping.Produtos.Valido);
        Assert.Equal("PRODUTOS", mapping.Produtos.TableName);
        Assert.Equal("CODIGO", mapping.Produtos.CodigoCol);
        Assert.Equal("DESCRICAO", mapping.Produtos.DescricaoCol);

        Assert.True(mapping.Estoque.Valido);
        Assert.False(mapping.Estoque.HasDedicatedTable);
        Assert.Equal("PRODUTOS", mapping.Estoque.TableName);
        Assert.Equal("QUANTIDADE", mapping.Estoque.EstoqueAtualCol);

        Assert.True(mapping.Fornecedores.Valido);
        Assert.Equal("FORNECEDORES", mapping.Fornecedores.TableName);
    }

    [Fact]
    public void SchemaMappingResolver_UnknownStock_ReportsDiagnosticWithoutCrashing()
    {
        var schema = new DiscoveredSchema
        {
            DatabaseType = "Firebird",
            DatabaseName = "custom_erp"
        };

        var prodTable = new DbTableInfo
        {
            Name = "PRODUTO",
            Columns = new List<DbColumnInfo>
            {
                new("CODIGO", "VARCHAR", 30, 0, false),
                new("NOME", "VARCHAR", 100, 0, false)
            }
        };
        schema.Tables["PRODUTO"] = prodTable;

        var mapping = SchemaMappingResolver.Resolve(schema);

        Assert.True(mapping.Produtos.Valido);
        Assert.False(mapping.Estoque.Valido);
        Assert.NotNull(mapping.Estoque.MotivoInvalido);
        Assert.Contains("quantidade em estoque", mapping.Estoque.MotivoInvalido);
    }

    [Theory]
    [InlineData("12.345.678/0001-90", "12.345.678", true)]
    [InlineData("12.345.678/0001-90", "12345678", true)]
    [InlineData("12.345.678/0001-90", "12.345.678/0001-90", true)]
    [InlineData("12.345.678/0001-90", "98.765.432", false)]
    [InlineData("12.345.678/0001-90", "98765432", false)]
    public void CnpjBase_Extraction_And_Validation_Matches(string cnpjEmpresa, string cnpjBaseInput, bool esperadoMatch)
    {
        var baseEmpresa = new string(cnpjEmpresa.Where(char.IsDigit).Take(8).ToArray());
        var baseInput = new string(cnpjBaseInput.Where(char.IsDigit).Take(8).ToArray());

        var match = baseEmpresa == baseInput;
        Assert.Equal(esperadoMatch, match);
    }
}

