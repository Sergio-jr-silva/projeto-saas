using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nexo.Agent.Core.Schema;

namespace Nexo.Agent.Core.Connectors;

public interface IDatabaseConnector
{
    string ConnectorName { get; }
    string DatabaseType { get; }
    string DatabasePathOrName { get; }
    ResolvedTableMapping CurrentMapping { get; }

    Task<bool> TestConnectionAsync(CancellationToken ct = default);
    Task<ResolvedTableMapping> DiscoverAndApplySchemaAsync(bool forceRefresh = false, CancellationToken ct = default);

    Task<IEnumerable<ErpProdutoDto>> GetProductsAsync(DateTime? since = null, CancellationToken ct = default);
    Task<IEnumerable<ErpFornecedorDto>> GetSuppliersAsync(DateTime? since = null, CancellationToken ct = default);
    Task<IEnumerable<ErpEstoqueDto>> GetStockAsync(CancellationToken ct = default);
    Task<IEnumerable<ErpPrecoDto>> GetPricesAsync(DateTime? since = null, CancellationToken ct = default);
}
