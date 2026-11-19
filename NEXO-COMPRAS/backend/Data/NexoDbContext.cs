using Microsoft.EntityFrameworkCore;
using Nexo.Api.Models;
using Nexo.Api.MultiTenancy;

namespace Nexo.Api.Data;

public class NexoDbContext : DbContext
{
    private readonly ITenantContext _t;

    public NexoDbContext(DbContextOptions<NexoDbContext> options, ITenantContext tenantContext) : base(options)
    {
        _t = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Plano> Planos => Set<Plano>();
    public DbSet<Assinatura> Assinaturas => Set<Assinatura>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Filial> Filiais => Set<Filial>();
    public DbSet<ConfiguracaoTenant> ConfiguracoesTenant => Set<ConfiguracaoTenant>();
    public DbSet<Agente> Agentes => Set<Agente>();
    public DbSet<Sincronizacao> Sincronizacoes => Set<Sincronizacao>();
    public DbSet<SincronizacaoErro> SincronizacaoErros => Set<SincronizacaoErro>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<Fornecedor> Fornecedores => Set<Fornecedor>();
    public DbSet<HistoricoPreco> HistoricoPrecos => Set<HistoricoPreco>();
    public DbSet<Cotacao> Cotacoes => Set<Cotacao>();
    public DbSet<CotacaoItem> CotacaoItens => Set<CotacaoItem>();
    public DbSet<CotacaoFornecedor> CotacaoFornecedores => Set<CotacaoFornecedor>();
    public DbSet<AcessoFornecedor> AcessosFornecedor => Set<AcessoFornecedor>();
    public DbSet<RespostaCotacao> RespostasCotacao => Set<RespostaCotacao>();
    public DbSet<RespostaCotacaoItem> RespostaCotacaoItens => Set<RespostaCotacaoItem>();
    public DbSet<AnexoResposta> AnexosResposta => Set<AnexoResposta>();
    public DbSet<PedidoCompra> PedidosCompra => Set<PedidoCompra>();
    public DbSet<PedidoCompraItem> PedidoCompraItens => Set<PedidoCompraItem>();
    public DbSet<Fatura> Faturas => Set<Fatura>();
    public DbSet<EstoqueFilial> EstoquesFilial => Set<EstoqueFilial>();
    public DbSet<SolicitacaoTransferencia> SolicitacoesTransferencia => Set<SolicitacaoTransferencia>();
    public DbSet<SolicitacaoTransferenciaItem> SolicitacaoTransferenciaItens => Set<SolicitacaoTransferenciaItem>();
    public DbSet<LogAuditoria> LogsAuditoria => Set<LogAuditoria>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Módulo de Cotação de Serviços
    public DbSet<CotacaoServico> CotacoesServico => Set<CotacaoServico>();
    public DbSet<AtividadeServico> AtividadesServico => Set<AtividadeServico>();
    public DbSet<FornecedorServico> FornecedoresServico => Set<FornecedorServico>();
    public DbSet<AcessoFornecedorServico> AcessosFornecedorServico => Set<AcessoFornecedorServico>();
    public DbSet<PropostaServico> PropostasServico => Set<PropostaServico>();
    public DbSet<PropostaServicoEtapa> PropostaServicoEtapas => Set<PropostaServicoEtapa>();
    public DbSet<AnexoServico> AnexosServico => Set<AnexoServico>();
    public DbSet<OrdemServico> OrdensServico => Set<OrdemServico>();

    // Precificação Dinâmica, Histórico Comercial e Cobrança
    public DbSet<ConfiguracaoPrecificacao> ConfiguracoesPrecificacao => Set<ConfiguracaoPrecificacao>();
    public DbSet<HistoricoComercialTenant> HistoricosComerciaisTenant => Set<HistoricoComercialTenant>();
    public DbSet<TransacaoPagamento> TransacoesPagamento => Set<TransacaoPagamento>();
    public DbSet<SolicitacaoAlteracaoPlano> SolicitacoesAlteracaoPlano => Set<SolicitacaoAlteracaoPlano>();

    /// <summary>Tabelas que possuem tenant_id (usadas para RLS na migration e nos testes).</summary>
    public static readonly string[] TenantTables =
    {
        "assinaturas", "faturas", "filiais", "estoques_filial", "solicitacoes_transferencia", "solicitacao_transferencia_itens",
        "configuracoes_tenant", "agentes", "sincronizacoes", "sincronizacao_erros",
        "produtos", "fornecedores", "historico_precos", "cotacoes", "cotacao_itens", "cotacao_fornecedores",
        "acessos_fornecedor", "respostas_cotacao", "resposta_cotacao_itens", "anexos_resposta",
        "pedidos_compra", "pedido_compra_itens",
        "cotacoes_servico", "atividades_servico", "fornecedores_servico", "acessos_fornecedor_servico",
        "propostas_servico", "proposta_servico_etapas", "anexos_servico", "ordens_servico"
    };

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<RefreshToken>(e => { e.ToTable("RefreshTokens"); e.HasIndex(x => x.TokenHash).IsUnique(); e.HasIndex(x => x.FamilyId); e.HasIndex(x => x.UsuarioId); e.Property(x => x.TokenHash).HasMaxLength(64).IsRequired(); });
        base.OnModelCreating(b);

        b.Entity<Tenant>(e =>
        {
            e.HasIndex(x => x.Cnpj).IsUnique();
            e.HasIndex(x => x.Slug).IsUnique().HasFilter("\"Slug\" IS NOT NULL AND \"Slug\" <> ''");
            e.Property(x => x.Slug).HasMaxLength(80);
            e.Property(x => x.RazaoSocial).HasMaxLength(200).IsRequired();
            e.Property(x => x.NomeFantasia).HasMaxLength(200);
            e.Property(x => x.Cnpj).HasMaxLength(20).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.ModuloContratado).HasMaxLength(40);
            e.Property(x => x.VolumeUtilizacao).HasMaxLength(20);
            e.Property(x => x.SituacaoFinanceira).HasMaxLength(30);
            e.Property(x => x.MensalidadeCalculada).HasPrecision(12, 2);
            e.Property(x => x.DescontoComercial).HasPrecision(12, 2);
            e.Property(x => x.AjusteManual).HasPrecision(12, 2);
            e.Property(x => x.MensalidadeFinal).HasPrecision(12, 2);
        });
        b.Entity<Plano>(e => { e.Property(x => x.Nome).HasMaxLength(60); e.Property(x => x.Mensalidade).HasPrecision(12, 2); e.Property(x => x.PrecoAnual).HasPrecision(12, 2); });
        b.Entity<Assinatura>(e => { e.Property(x => x.Valor).HasPrecision(12, 2); e.HasIndex(x => x.TenantId); });
        b.Entity<Fatura>(e => { e.Property(x => x.Valor).HasPrecision(12, 2); e.HasIndex(x => x.TenantId); e.HasIndex(x => x.Numero); });
        b.Entity<Usuario>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.TenantId);
            e.Property(x => x.Email).HasMaxLength(150).IsRequired();
            e.Property(x => x.Nome).HasMaxLength(150).IsRequired();
            e.Property(x => x.Perfil).HasMaxLength(20);
            e.HasOne(x => x.Tenant).WithMany(t => t.Usuarios).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Filial>(e =>
        {
            e.HasIndex(x => x.TenantId);
            e.HasIndex(x => new { x.TenantId, x.Codigo });
            e.HasOne(x => x.Agente).WithMany().HasForeignKey(x => x.AgenteId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<EstoqueFilial>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.FilialId, x.ProdutoId }).IsUnique();
            e.Property(x => x.EstoqueAtual).HasPrecision(15, 3);
            e.Property(x => x.EstoqueMinimo).HasPrecision(15, 3);
            e.Property(x => x.EstoquePendente).HasPrecision(15, 3);
        });
        b.Entity<SolicitacaoTransferencia>(e => { e.HasIndex(x => new { x.TenantId, x.Numero }); e.HasIndex(x => x.FilialOrigemId); e.HasIndex(x => x.FilialDestinoId); });
        b.Entity<SolicitacaoTransferenciaItem>(e => { e.HasIndex(x => x.SolicitacaoTransferenciaId); e.Property(x => x.QuantidadeSolicitada).HasPrecision(15, 3); e.Property(x => x.QuantidadeAtendida).HasPrecision(15, 3); });
        b.Entity<ConfiguracaoTenant>(e =>
        {
            e.HasIndex(x => x.TenantId).IsUnique();
            e.Property(x => x.PercCreditoPisCofins).HasPrecision(7, 4);
            e.Property(x => x.RegimeTributario).HasMaxLength(30);
        });
        b.Entity<Agente>(e =>
        {
            e.HasIndex(x => x.TenantId);
            e.HasIndex(x => x.CodigoAtivacaoHash);
            e.Property(x => x.ChaveHash).HasMaxLength(100);
            e.Property(x => x.CodigoAtivacaoHash).HasMaxLength(100);
            e.Property(x => x.TipoBanco).HasMaxLength(30);
            e.HasOne(x => x.Filial).WithMany().HasForeignKey(x => x.FilialId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<Sincronizacao>(e => { e.HasIndex(x => new { x.TenantId, x.DataCriacao }); e.HasOne(x => x.Agente).WithMany().HasForeignKey(x => x.AgenteId).OnDelete(DeleteBehavior.Cascade); });
        b.Entity<SincronizacaoErro>(e => e.HasIndex(x => new { x.TenantId, x.DataCriacao }));

        b.Entity<Produto>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.CodigoErp }).IsUnique().HasFilter("\"CodigoErp\" IS NOT NULL AND \"CodigoErp\" <> ''");
            e.HasIndex(x => new { x.TenantId, x.Grupo });
            e.Property(x => x.Descricao).HasMaxLength(255).IsRequired();
            e.Property(x => x.CodigoErp).HasMaxLength(100);
            e.Property(x => x.PrecoVenda).HasPrecision(15, 4);
            e.Property(x => x.CustoAtual).HasPrecision(15, 4);
            e.Property(x => x.UltimoCusto).HasPrecision(15, 4);
            e.Property(x => x.EstoqueCritico).HasPrecision(15, 3); e.Property(x => x.EstoqueAtual).HasPrecision(15, 3);
            e.Property(x => x.EstoquePendente).HasPrecision(15, 3);
            e.Property(x => x.EstoqueMinimo).HasPrecision(15, 3);
            e.Property(x => x.Consumo90Dias).HasPrecision(15, 3);
            e.HasOne(x => x.FornecedorVinculado).WithMany().HasForeignKey(x => x.FornecedorVinculadoId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<Fornecedor>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.CodigoErp }).IsUnique().HasFilter("\"CodigoErp\" IS NOT NULL AND \"CodigoErp\" <> ''");
            e.HasIndex(x => new { x.TenantId, x.Cnpj });
            e.Property(x => x.RazaoSocial).HasMaxLength(200).IsRequired();
            e.Property(x => x.NomeFantasia).HasMaxLength(200);
            e.Property(x => x.CodigoErp).HasMaxLength(100);
        });
        b.Entity<HistoricoPreco>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.ProdutoId, x.Data });
            e.Property(x => x.Valor).HasPrecision(15, 4);
            e.Property(x => x.Quantidade).HasPrecision(15, 3);
            e.Property(x => x.Data).HasColumnType("date");
            e.HasOne(x => x.Produto).WithMany().HasForeignKey(x => x.ProdutoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Fornecedor).WithMany().HasForeignKey(x => x.FornecedorId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Cotacao>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Numero }).IsUnique();
            e.Property(x => x.Numero).HasMaxLength(30);
            e.Property(x => x.Titulo).HasMaxLength(200);
            e.Property(x => x.TotalAprovado).HasPrecision(15, 2);
            e.Property(x => x.EconomiaPotencial).HasPrecision(15, 2);
            e.Property(x => x.EconomiaRealizada).HasPrecision(15, 2);
        });
        b.Entity<CotacaoItem>(e =>
        {
            e.HasIndex(x => x.CotacaoId);
            e.Property(x => x.Quantidade).HasPrecision(15, 3);
            e.Property(x => x.EstoqueNoMomento).HasPrecision(15, 3);
            e.Property(x => x.UltimoCustoRef).HasPrecision(15, 4);
            e.Property(x => x.PrecoVendaRef).HasPrecision(15, 4);
            e.HasOne(x => x.Cotacao).WithMany(c => c.Itens).HasForeignKey(x => x.CotacaoId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<CotacaoFornecedor>(e =>
        {
            e.HasIndex(x => new { x.CotacaoId, x.FornecedorId }).IsUnique();
            e.HasOne(x => x.Cotacao).WithMany(c => c.Fornecedores).HasForeignKey(x => x.CotacaoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Fornecedor).WithMany().HasForeignKey(x => x.FornecedorId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AcessoFornecedor>(e =>
        {
            e.HasIndex(x => x.Token).IsUnique();
            e.HasIndex(x => x.CotacaoFornecedorId).IsUnique();
            e.Property(x => x.Token).HasMaxLength(64);
            e.HasOne(x => x.CotacaoFornecedor).WithOne(c => c.Acesso).HasForeignKey<AcessoFornecedor>(x => x.CotacaoFornecedorId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<RespostaCotacao>(e =>
        {
            e.HasIndex(x => x.CotacaoFornecedorId).IsUnique();
            e.Property(x => x.Frete).HasPrecision(15, 2);
            e.Property(x => x.OutrasDespesas).HasPrecision(15, 2);
            e.HasOne(x => x.CotacaoFornecedor).WithOne(c => c.Resposta).HasForeignKey<RespostaCotacao>(x => x.CotacaoFornecedorId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<RespostaCotacaoItem>(e =>
        {
            e.HasIndex(x => new { x.RespostaCotacaoId, x.CotacaoItemId }).IsUnique();
            foreach (var p in new[] { nameof(RespostaCotacaoItem.QuantidadeOfertada) }) e.Property(p).HasPrecision(15, 3);
            e.Property(x => x.PrecoUnitario).HasPrecision(15, 4);
            e.Property(x => x.DescontoPerc).HasPrecision(7, 4);
            e.Property(x => x.IcmsAliq).HasPrecision(7, 4);
            e.Property(x => x.IpiAliq).HasPrecision(7, 4);
            e.Property(x => x.Mva).HasPrecision(8, 4);
            e.Property(x => x.IcmsStAliq).HasPrecision(7, 4);
            e.Property(x => x.IcmsStValor).HasPrecision(15, 2);
            e.Property(x => x.Cst).HasMaxLength(4);
            e.Property(x => x.Cfop).HasMaxLength(4);
            e.HasOne(x => x.Resposta).WithMany(r => r.Itens).HasForeignKey(x => x.RespostaCotacaoId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<AnexoResposta>(e =>
        {
            e.HasOne(x => x.Resposta).WithMany(r => r.Anexos).HasForeignKey(x => x.RespostaCotacaoId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<LogAuditoria>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.DataCriacao });
            e.Property(x => x.DadosJson).HasColumnType("text");
        });

        b.Entity<PedidoCompra>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Numero }).IsUnique();
            e.Property(x => x.Numero).HasMaxLength(30);
            e.Property(x => x.Status).HasMaxLength(30);
            e.Property(x => x.Subtotal).HasPrecision(15, 2);
            e.Property(x => x.Desconto).HasPrecision(15, 2);
            e.Property(x => x.Frete).HasPrecision(15, 2);
            e.Property(x => x.OutrasDespesas).HasPrecision(15, 2);
            e.Property(x => x.ImpostosTotal).HasPrecision(15, 2);
            e.Property(x => x.ValorTotal).HasPrecision(15, 2);
            e.Property(x => x.TimelineJson).HasColumnType("text");
            e.HasOne(x => x.Cotacao).WithMany().HasForeignKey(x => x.CotacaoId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Fornecedor).WithMany().HasForeignKey(x => x.FornecedorId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<PedidoCompraItem>(e =>
        {
            e.HasIndex(x => x.PedidoCompraId);
            e.Property(x => x.Quantidade).HasPrecision(15, 3);
            e.Property(x => x.PrecoUnitario).HasPrecision(15, 4);
            e.Property(x => x.DescontoPerc).HasPrecision(7, 4);
            e.Property(x => x.PrecoLiquidoUnit).HasPrecision(15, 4);
            e.Property(x => x.Subtotal).HasPrecision(15, 2);
            e.Property(x => x.Cst).HasMaxLength(4);
            e.Property(x => x.Cfop).HasMaxLength(4);
            e.Property(x => x.IcmsAliq).HasPrecision(7, 4);
            e.Property(x => x.IpiAliq).HasPrecision(7, 4);
            e.Property(x => x.Mva).HasPrecision(8, 4);
            e.Property(x => x.IcmsStAliq).HasPrecision(7, 4);
            e.Property(x => x.IcmsStValor).HasPrecision(15, 2);
            e.Property(x => x.CustoEfetivoUnit).HasPrecision(15, 4);
            e.Property(x => x.CustoEfetivoTotal).HasPrecision(15, 2);
            e.HasOne(x => x.PedidoCompra).WithMany(p => p.Itens).HasForeignKey(x => x.PedidoCompraId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Produto).WithMany().HasForeignKey(x => x.ProdutoId).OnDelete(DeleteBehavior.Restrict);
        });

        // Configuração de Cotação de Serviços
        b.Entity<CotacaoServico>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Numero }).IsUnique();
            e.Property(x => x.Numero).HasMaxLength(30);
            e.Property(x => x.Titulo).HasMaxLength(200);
            e.Property(x => x.Categoria).HasMaxLength(50);
            e.Property(x => x.Prioridade).HasMaxLength(20);
            e.Property(x => x.Status).HasMaxLength(30);
            e.Property(x => x.TotalEstimado).HasPrecision(15, 2);
            e.Property(x => x.TotalContratado).HasPrecision(15, 2);
            e.Property(x => x.EconomiaRealizada).HasPrecision(15, 2);
            e.Property(x => x.PesoPreco).HasPrecision(5, 2);
            e.Property(x => x.PesoPrazo).HasPrecision(5, 2);
            e.Property(x => x.PesoTecnico).HasPrecision(5, 2);
            e.Property(x => x.PesoCondicoes).HasPrecision(5, 2);
            e.Property(x => x.TimelineJson).HasColumnType("text");
            e.HasOne(x => x.Filial).WithMany().HasForeignKey(x => x.FilialId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.FornecedorVencedor).WithMany().HasForeignKey(x => x.FornecedorVencedorId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<AtividadeServico>(e =>
        {
            e.HasIndex(x => x.CotacaoServicoId);
            e.Property(x => x.Nome).HasMaxLength(200);
            e.Property(x => x.Quantidade).HasPrecision(15, 3);
            e.Property(x => x.ValorEstimadoUnitario).HasPrecision(15, 2);
            e.HasOne(x => x.CotacaoServico).WithMany(c => c.Atividades).HasForeignKey(x => x.CotacaoServicoId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<FornecedorServico>(e =>
        {
            e.HasIndex(x => new { x.CotacaoServicoId, x.FornecedorId }).IsUnique();
            e.Property(x => x.Status).HasMaxLength(30);
            e.HasOne(x => x.CotacaoServico).WithMany(c => c.Fornecedores).HasForeignKey(x => x.CotacaoServicoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Fornecedor).WithMany().HasForeignKey(x => x.FornecedorId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AcessoFornecedorServico>(e =>
        {
            e.HasIndex(x => x.FornecedorServicoId).IsUnique();
            e.Property(x => x.Token).HasMaxLength(64);
            e.HasOne(x => x.FornecedorServico).WithOne(c => c.Acesso).HasForeignKey<AcessoFornecedorServico>(x => x.FornecedorServicoId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PropostaServico>(e =>
        {
            e.HasIndex(x => x.FornecedorServicoId).IsUnique();
            e.Property(x => x.Status).HasMaxLength(30);
            e.Property(x => x.ValorMaoDeObra).HasPrecision(15, 2);
            e.Property(x => x.ValorMateriaisInclusos).HasPrecision(15, 2);
            e.Property(x => x.ValorEquipamentos).HasPrecision(15, 2);
            e.Property(x => x.Deslocamento).HasPrecision(15, 2);
            e.Property(x => x.OutrasDespesas).HasPrecision(15, 2);
            e.Property(x => x.Desconto).HasPrecision(15, 2);
            e.Property(x => x.ImpostosTotal).HasPrecision(15, 2);
            e.Property(x => x.ValorTotal).HasPrecision(15, 2);
            e.Property(x => x.NotaComercial).HasPrecision(5, 2);
            e.Property(x => x.NotaTecnica).HasPrecision(5, 2);
            e.Property(x => x.NotaPrazo).HasPrecision(5, 2);
            e.Property(x => x.NotaCondicoes).HasPrecision(5, 2);
            e.Property(x => x.PontuacaoPonderada).HasPrecision(5, 2);
            e.HasOne(x => x.FornecedorServico).WithOne(f => f.Proposta).HasForeignKey<PropostaServico>(x => x.FornecedorServicoId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PropostaServicoEtapa>(e =>
        {
            e.HasIndex(x => new { x.PropostaServicoId, x.AtividadeServicoId }).IsUnique();
            e.Property(x => x.ValorUnitario).HasPrecision(15, 2);
            e.Property(x => x.ValorTotal).HasPrecision(15, 2);
            e.HasOne(x => x.PropostaServico).WithMany(p => p.Etapas).HasForeignKey(x => x.PropostaServicoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.AtividadeServico).WithMany().HasForeignKey(x => x.AtividadeServicoId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AnexoServico>(e =>
        {
            e.HasIndex(x => new { x.Origem, x.ReferenciaId });
            e.Property(x => x.NomeArquivo).HasMaxLength(255);
        });

        b.Entity<OrdemServico>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Numero }).IsUnique();
            e.Property(x => x.Numero).HasMaxLength(30);
            e.Property(x => x.Status).HasMaxLength(30);
            e.Property(x => x.ValorTotal).HasPrecision(15, 2);
            e.Property(x => x.ValorMaoDeObra).HasPrecision(15, 2);
            e.Property(x => x.ValorMateriais).HasPrecision(15, 2);
            e.Property(x => x.Deslocamento).HasPrecision(15, 2);
            e.Property(x => x.ImpostosTotal).HasPrecision(15, 2);
            e.Property(x => x.TimelineJson).HasColumnType("text");
            e.HasOne(x => x.CotacaoServico).WithMany().HasForeignKey(x => x.CotacaoServicoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PropostaServico).WithMany().HasForeignKey(x => x.PropostaServicoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Fornecedor).WithMany().HasForeignKey(x => x.FornecedorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Filial).WithMany().HasForeignKey(x => x.FilialId).OnDelete(DeleteBehavior.SetNull);
        });

        // Precificação Dinâmica, Histórico Comercial e Cobrança
        b.Entity<ConfiguracaoPrecificacao>(e =>
        {
            e.HasIndex(x => x.Versao).IsUnique();
            e.Property(x => x.MensalidadeBase).HasPrecision(12, 2);
            e.Property(x => x.ValorMinimo).HasPrecision(12, 2);
            e.Property(x => x.ValorMaximo).HasPrecision(12, 2);
            e.Property(x => x.AdicProdFaixa1).HasPrecision(12, 2);
            e.Property(x => x.AdicProdFaixa2).HasPrecision(12, 2);
            e.Property(x => x.AdicProdFaixa3).HasPrecision(12, 2);
            e.Property(x => x.AdicProdFaixa4).HasPrecision(12, 2);
            e.Property(x => x.AdicFilialFaixa1).HasPrecision(12, 2);
            e.Property(x => x.AdicFilialFaixa2).HasPrecision(12, 2);
            e.Property(x => x.AdicFilialFaixa3).HasPrecision(12, 2);
            e.Property(x => x.AdicFilialFaixa4).HasPrecision(12, 2);
            e.Property(x => x.AdicUsuarioFaixa1).HasPrecision(12, 2);
            e.Property(x => x.AdicUsuarioFaixa2).HasPrecision(12, 2);
            e.Property(x => x.AdicUsuarioFaixa3).HasPrecision(12, 2);
            e.Property(x => x.AdicUsuarioFaixa4).HasPrecision(12, 2);
            e.Property(x => x.AdicSincFaixa1).HasPrecision(12, 2);
            e.Property(x => x.AdicSincFaixa2).HasPrecision(12, 2);
            e.Property(x => x.AdicSincFaixa3).HasPrecision(12, 2);
            e.Property(x => x.AdicVolumeBaixo).HasPrecision(12, 2);
            e.Property(x => x.AdicVolumeMedio).HasPrecision(12, 2);
            e.Property(x => x.AdicVolumeAlto).HasPrecision(12, 2);
            e.Property(x => x.AdicModuloBasico).HasPrecision(12, 2);
            e.Property(x => x.AdicModuloRelatorios).HasPrecision(12, 2);
            e.Property(x => x.AdicModuloCompleto).HasPrecision(12, 2);
        });

        b.Entity<HistoricoComercialTenant>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.DataCriacao });
            e.Property(x => x.MensalidadeAnterior).HasPrecision(12, 2);
            e.Property(x => x.MensalidadeCalculada).HasPrecision(12, 2);
            e.Property(x => x.DescontoAplicado).HasPrecision(12, 2);
            e.Property(x => x.AjusteManual).HasPrecision(12, 2);
            e.Property(x => x.MensalidadeFinal).HasPrecision(12, 2);
            e.HasOne(x => x.Tenant).WithMany(t => t.HistoricosComerciais).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TransacaoPagamento>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.DataCriacao });
            e.HasIndex(x => x.GatewayTransacaoId);
            e.Property(x => x.Valor).HasPrecision(12, 2);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Fatura).WithMany().HasForeignKey(x => x.FaturaId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Assinatura).WithMany().HasForeignKey(x => x.AssinaturaId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<SolicitacaoAlteracaoPlano>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.DataCriacao });
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Filtros globais (1ª camada de isolamento; o RLS do PostgreSQL é a 2ª) ----
        b.Entity<Assinatura>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<Fatura>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<Filial>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<EstoqueFilial>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<SolicitacaoTransferencia>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<SolicitacaoTransferenciaItem>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<ConfiguracaoTenant>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<Agente>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<Sincronizacao>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<SincronizacaoErro>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<Produto>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<Fornecedor>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<HistoricoPreco>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<Cotacao>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<CotacaoItem>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<CotacaoFornecedor>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<AcessoFornecedor>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<RespostaCotacao>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<RespostaCotacaoItem>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<AnexoResposta>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<PedidoCompra>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<PedidoCompraItem>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<Usuario>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<Tenant>().HasQueryFilter(e => _t.BypassRls || e.Id == _t.CurrentTenantId);
        b.Entity<LogAuditoria>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);

        // Isolamento Multitenancy de Serviços
        b.Entity<CotacaoServico>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<AtividadeServico>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<FornecedorServico>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<AcessoFornecedorServico>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<PropostaServico>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<PropostaServicoEtapa>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<AnexoServico>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<OrdemServico>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);

        // Isolamento Multitenancy de Histórico Comercial, Pagamentos e Solicitações
        b.Entity<HistoricoComercialTenant>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<TransacaoPagamento>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
        b.Entity<SolicitacaoAlteracaoPlano>().HasQueryFilter(e => _t.BypassRls || e.TenantId == _t.CurrentTenantId);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is BaseEntity be && entry.State == EntityState.Modified)
                be.DataAtualizacao = DateTime.UtcNow;

            if (entry.Entity is ITenantEntity te && entry.State is EntityState.Added or EntityState.Modified)
            {
                if (te.TenantId == Guid.Empty)
                {
                    if (!_t.CurrentTenantId.HasValue)
                        throw new InvalidOperationException("Entidade operacional sem tenant associado.");
                    te.TenantId = _t.CurrentTenantId.Value;
                }
                // defesa extra: uma requisição de tenant jamais grava dados de outro tenant
                if (!_t.BypassRls && _t.CurrentTenantId.HasValue && te.TenantId != _t.CurrentTenantId.Value)
                    throw new InvalidOperationException("Tentativa de gravar dados de outro tenant.");
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
