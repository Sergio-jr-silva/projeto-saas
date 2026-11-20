using System;
using System.Collections.Generic;
using System.Linq;

namespace Nexo.Agent.Core.Schema;

public static class SchemaMappingResolver
{
    private static readonly string[] CandidateProductTables =
    {
        "PRODUTO", "PRODUTOS", "CAD_PRODUTO", "CADPRODUTO", "TB_PRODUTO", "TB_PRODUTOS",
        "TAB_PRODUTO", "ITEM", "ITENS", "ESTOQUE_PRODUTO"
    };

    private static readonly string[] CandidateStockTables =
    {
        "PRODUTO_ESTOQUE", "ESTOQUE", "SALDO_ESTOQUE", "PROD_ESTOQUE", "TB_ESTOQUE",
        "ESTOQUE_LOJA", "ESTOQUES", "SALDO_ESTOQUES"
    };

    private static readonly string[] CandidateSupplierTables =
    {
        "PESSOA", "PESSOAS", "FORNECEDOR", "FORNECEDORES", "CAD_FORNECEDOR", "CADFORNECEDOR",
        "CAD_PESSOA", "CADPESSOA", "CLI_FORN", "PARTICIPANTES", "PARTICIPANTE",
        "PARCEIRO", "PARCEIROS", "TB_FORNECEDOR", "TB_FORNECEDORES"
    };

    public static ResolvedTableMapping Resolve(DiscoveredSchema schema)
    {
        var result = new ResolvedTableMapping
        {
            DatabaseType = schema.DatabaseType,
            DatabasePathOrName = schema.DatabaseName,
            IdentificadoEm = DateTime.UtcNow
        };

        // 1. Resolve Produtos
        result.Produtos = ResolveProdutos(schema);

        // 2. Resolve Estoque (pode usar tabela dedicada ou a própria tabela de produtos)
        result.Estoque = ResolveEstoque(schema, result.Produtos);

        // 3. Resolve Fornecedores
        result.Fornecedores = ResolveFornecedores(schema);

        return result;
    }

    private static ResolvedProdutoMapping ResolveProdutos(DiscoveredSchema schema)
    {
        var m = new ResolvedProdutoMapping();

        // Encontra melhor tabela candidata
        DbTableInfo? table = null;
        foreach (var cand in CandidateProductTables)
        {
            if (schema.Tables.TryGetValue(cand, out var t))
            {
                table = t;
                break;
            }
        }

        // Se não achou pelos nomes exatos, procura por tabela contendo "PROD" com colunas essenciais
        if (table == null)
        {
            table = schema.Tables.Values
                .Where(t => t.Name.Contains("PROD", StringComparison.OrdinalIgnoreCase) || t.Name.Contains("ITEM", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(t => ScoreProductTable(t))
                .FirstOrDefault();
        }

        if (table == null)
        {
            m.Valido = false;
            m.MotivoInvalido = "Nenhuma tabela compatível com catálogo de produtos encontrada no banco de dados.";
            return m;
        }

        m.TableName = table.Name;

        // Código do Produto
        var colCodigo = table.FindColumn("CODIGO", "ID_PRODUTO", "COD_PRODUTO", "CODPROD", "CD_PRODUTO", "REFERENCIA", "REF", "ID");
        if (colCodigo == null)
        {
            m.Valido = false;
            m.MotivoInvalido = $"A tabela '{table.Name}' não possui uma coluna identificadora de código do produto.";
            return m;
        }
        m.CodigoCol = colCodigo.Name;

        // Descrição / Nome
        var colDesc = table.FindColumn("NOME", "DESCRICAO", "DESC", "PRODUTO", "NOME_PRODUTO", "DS_PRODUTO", "DESCR", "NOMEREDUZIDO");
        if (colDesc == null)
        {
            m.Valido = false;
            m.MotivoInvalido = $"A tabela '{table.Name}' não possui uma coluna com a descrição ou nome do produto.";
            return m;
        }
        m.DescricaoCol = colDesc.Name;

        // Colunas adicionais opcionais
        m.UnidadeCol = table.FindColumn("UNIDADE", "UNID", "UND", "UN", "SIGLA_UNIDADE")?.Name;
        m.CodigoBarrasCol = table.FindColumn("GTIN", "CODBARRA", "CODBARRAS", "EAN", "COD_BARRAS", "BARRAS")?.Name;
        m.PrecoVendaCol = table.FindColumn("PRECO_VENDA", "PRECO", "VALOR_VENDA", "PRECOVENDA", "VR_VENDA", "VL_VENDA", "VLR_VENDA")?.Name;
        m.CustoAtualCol = table.FindColumn("CUSTO_TOTAL", "CUSTO", "PRECOCUSTO", "PRECO_CUSTO", "VALOR_CUSTO", "VR_CUSTO", "VL_CUSTO", "CUSTO_MEDIO", "ULTIMO_CUSTO")?.Name;
        m.EstoqueMinimoCol = table.FindColumn("ESTOQUE_MINIMO", "ESTOQUEMINIMO", "EST_MINIMO", "MINIMO", "QTD_MINIMA")?.Name;
        m.FornecedorErpIdCol = table.FindColumn("ID_FORNECEDOR", "FORNECEDOR_ID", "CODFORN", "COD_FORNECEDOR", "FORNECEDOR")?.Name;
        m.EliminadoCol = table.FindColumn("ELIMINADO", "ATIVO", "STATUS", "CANCELADO", "EXCLUIDO")?.Name;
        // Apenas colunas de alteração real (nunca DT_CADASTRO, que é inserção original e causa erro de tipo de data no Firebird)
        m.DataAtualizacaoCol = table.FindColumn("UPDATED_AT", "DATA_ALTERACAO", "DATAATUALIZACAO", "DT_ALTERACAO", "DT_ULTIMA_ALTERACAO", "DT_MODIFICACAO", "DATA_MODIFICACAO")?.Name;

        m.Valido = true;
        return m;
    }

    private static ResolvedEstoqueMapping ResolveEstoque(DiscoveredSchema schema, ResolvedProdutoMapping produtoMapping)
    {
        var m = new ResolvedEstoqueMapping();

        // 1. Procura por tabela dedicada de estoque
        DbTableInfo? stockTable = null;
        foreach (var cand in CandidateStockTables)
        {
            if (schema.Tables.TryGetValue(cand, out var t))
            {
                stockTable = t;
                break;
            }
        }

        if (stockTable == null)
        {
            stockTable = schema.Tables.Values
                .Where(t => t.Name.Contains("ESTOQUE", StringComparison.OrdinalIgnoreCase) || t.Name.Contains("SALDO", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(t => ScoreStockTable(t))
                .FirstOrDefault();
        }

        if (stockTable != null)
        {
            // Tabela dedicada encontrada!
            m.TableName = stockTable.Name;
            m.HasDedicatedTable = true;

            // Coluna de quantidade atual
            // CUIDADO: Procurar por colunas REAIS que existem no banco (ex: LOJA, QUANTIDADE, QTDE, SALDO, ESTOQUE_ATUAL, ESTOQUE, LQUANT)
            var colQtd = stockTable.FindColumn("LOJA", "QUANTIDADE", "QTDE", "SALDO", "ESTOQUE_ATUAL", "ESTOQUE", "QTDE_ESTOQUE", "SALDO_ATUAL", "EST_ATUAL", "LQUANT");
            if (colQtd == null)
            {
                // Se a tabela dedicada não tem campo de quantidade óbvio, verifica tipo numérico
                var numCol = stockTable.Columns.FirstOrDefault(c => c.DataType.Contains("DECIMAL") || c.DataType.Contains("NUMERIC") || c.DataType.Contains("FLOAT") || c.DataType.Contains("DOUBLE"));
                if (numCol != null) colQtd = numCol;
            }

            if (colQtd != null)
            {
                m.EstoqueAtualCol = colQtd.Name;
                m.EstoquePendenteCol = stockTable.FindColumn("PENDENCIA", "PENDENTE", "RESERVA", "RESERVADO", "BLOQUEADO")?.Name;
                m.FilialCol = stockTable.FindColumn("ID_FILIAL", "FILIAL_ID", "FILIAL", "COD_FILIAL")?.Name;

                // Coluna de referência com Produto
                var colRef = stockTable.FindColumn("ID_PRODUTO", "COD_PRODUTO", "CODIGO_PRODUTO", "PRODUTO_ID", "CODIGO", "ID");
                if (colRef != null && produtoMapping.Valido && schema.Tables.TryGetValue(produtoMapping.TableName, out var prodTable))
                {
                    m.ProdutoRefCol = colRef.Name;
                    if (colRef.Name.Equals("ID_PRODUTO", StringComparison.OrdinalIgnoreCase) && prodTable.HasColumn("ID"))
                    {
                        m.ProdutoJoinCol = "ID";
                    }
                    else if (prodTable.HasColumn(colRef.Name))
                    {
                        m.ProdutoJoinCol = colRef.Name;
                    }
                    else
                    {
                        m.ProdutoJoinCol = produtoMapping.CodigoCol;
                    }

                    m.Valido = true;
                    return m;
                }
            }
        }

        // 2. Fallback: Se não tem tabela dedicada válida, verifica se a própria tabela de produtos possui coluna de estoque
        if (produtoMapping.Valido && schema.Tables.TryGetValue(produtoMapping.TableName, out var pTable))
        {
            var colEstNaProd = pTable.FindColumn("ESTOQUE_ATUAL", "QUANTIDADE", "QTDE", "SALDO", "ESTOQUE", "LOJA", "QTDE_ESTOQUE", "SALDO_ATUAL", "LQUANT");
            if (colEstNaProd != null)
            {
                m.TableName = pTable.Name;
                m.HasDedicatedTable = false;
                m.EstoqueAtualCol = colEstNaProd.Name;
                m.EstoquePendenteCol = pTable.FindColumn("PENDENCIA", "PENDENTE", "RESERVA", "BLOQUEADO")?.Name;
                m.ProdutoRefCol = produtoMapping.CodigoCol;
                m.ProdutoJoinCol = produtoMapping.CodigoCol;
                m.Valido = true;
                return m;
            }
        }

        m.Valido = false;
        m.MotivoInvalido = "Não foi encontrado um campo compatível com quantidade em estoque nas tabelas do ERP.";
        return m;
    }

    private static ResolvedFornecedorMapping ResolveFornecedores(DiscoveredSchema schema)
    {
        var m = new ResolvedFornecedorMapping();

        DbTableInfo? table = null;
        foreach (var cand in CandidateSupplierTables)
        {
            if (schema.Tables.TryGetValue(cand, out var t))
            {
                table = t;
                break;
            }
        }

        if (table == null)
        {
            table = schema.Tables.Values
                .Where(t => t.Name.Contains("FORN", StringComparison.OrdinalIgnoreCase) || t.Name.Contains("PESSOA", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(t => ScoreSupplierTable(t))
                .FirstOrDefault();
        }

        if (table == null)
        {
            m.Valido = false;
            m.MotivoInvalido = "Nenhuma tabela compatível com cadastro de fornecedores encontrada no banco de dados.";
            return m;
        }

        m.TableName = table.Name;

        // Código
        var colCodigo = table.FindColumn("ID", "CODIGO", "COD_FORNECEDOR", "ID_FORNECEDOR", "CD_FORNECEDOR", "COD_PESSOA", "CD_PESSOA", "ID_PESSOA");
        if (colCodigo == null)
        {
            m.Valido = false;
            m.MotivoInvalido = $"A tabela '{table.Name}' não possui uma coluna identificadora de fornecedor.";
            return m;
        }
        m.CodigoCol = colCodigo.Name;

        // Razão Social
        var colRazao = table.FindColumn("RAZAO_SOCIAL", "RAZAO", "NOME", "NOME_FANTASIA", "FORNECEDOR");
        if (colRazao == null)
        {
            m.Valido = false;
            m.MotivoInvalido = $"A tabela '{table.Name}' não possui uma coluna de Razão Social ou Nome do fornecedor.";
            return m;
        }
        m.RazaoSocialCol = colRazao.Name;

        m.NomeFantasiaCol = table.FindColumn("NOME_FANTASIA", "FANTASIA", "APELIDO")?.Name;
        m.CnpjCol = table.FindColumn("CNPJ_CPF", "CNPJ", "CPF_CNPJ", "CGC", "DOCUMENTO")?.Name;
        m.TelefoneCol = table.FindColumn("FONE", "TELEFONE", "TEL", "CELULAR", "FONE1")?.Name;
        m.EmailCol = table.FindColumn("EMAIL", "E_MAIL")?.Name;
        m.CidadeCol = table.FindColumn("CIDADE", "MUNICIPIO")?.Name;
        m.UfCol = table.FindColumn("UF", "ESTADO")?.Name;
        m.FiltroTipoFornecedorCol = table.FindColumn("FORNECEDOR", "TIPO_PESSOA", "EH_FORNECEDOR", "E_FORNECEDOR", "IS_FORNECEDOR", "IND_FORN", "TIPO")?.Name;
        m.EliminadoCol = table.FindColumn("ELIMINADO", "ATIVO", "STATUS", "CANCELADO", "EXCLUIDO", "INATIVO")?.Name;

        m.Valido = true;
        return m;
    }

    private static int ScoreProductTable(DbTableInfo t)
    {
        int score = 0;
        if (t.HasColumn("CODIGO") || t.HasColumn("ID_PRODUTO")) score += 10;
        if (t.HasColumn("NOME") || t.HasColumn("DESCRICAO")) score += 10;
        if (t.HasColumn("PRECO_VENDA") || t.HasColumn("PRECO")) score += 5;
        if (t.HasColumn("CUSTO_TOTAL") || t.HasColumn("CUSTO")) score += 5;
        if (t.HasColumn("GTIN") || t.HasColumn("CODBARRA")) score += 5;
        return score;
    }

    private static int ScoreStockTable(DbTableInfo t)
    {
        int score = 0;
        if (t.HasColumn("ID_PRODUTO") || t.HasColumn("COD_PRODUTO")) score += 10;
        if (t.HasColumn("LOJA") || t.HasColumn("QUANTIDADE") || t.HasColumn("QTDE") || t.HasColumn("SALDO") || t.HasColumn("ESTOQUE_ATUAL")) score += 10;
        if (t.HasColumn("PENDENCIA") || t.HasColumn("RESERVA")) score += 5;
        return score;
    }

    private static int ScoreSupplierTable(DbTableInfo t)
    {
        int score = 0;
        if (t.HasColumn("CNPJ_CPF") || t.HasColumn("CNPJ")) score += 10;
        if (t.HasColumn("NOME") || t.HasColumn("RAZAO_SOCIAL")) score += 10;
        if (t.HasColumn("FORNECEDOR")) score += 5;
        return score;
    }
}

