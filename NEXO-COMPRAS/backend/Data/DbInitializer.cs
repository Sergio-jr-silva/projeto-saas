using Microsoft.EntityFrameworkCore;
using Nexo.Api.Models;

namespace Nexo.Api.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(NexoDbContext context)
    {
        await context.Database.EnsureCreatedAsync();

        // Garantir que as colunas e tabelas necessárias existam no banco de dados
        var colunasAgente = new[]
        {
            @"ALTER TABLE IF EXISTS ""Agentes"" ADD COLUMN IF NOT EXISTS ""TipoBanco"" character varying(30) NOT NULL DEFAULT 'Firebird';",
            @"ALTER TABLE IF EXISTS ""agentes"" ADD COLUMN IF NOT EXISTS ""TipoBanco"" character varying(30) NOT NULL DEFAULT 'Firebird';",
            @"ALTER TABLE IF EXISTS ""Agentes"" ADD COLUMN IF NOT EXISTS ""BancoConfigJson"" text NOT NULL DEFAULT '{{}}';",
            @"ALTER TABLE IF EXISTS ""agentes"" ADD COLUMN IF NOT EXISTS ""BancoConfigJson"" text NOT NULL DEFAULT '{{}}';",
            @"ALTER TABLE IF EXISTS ""Agentes"" ADD COLUMN IF NOT EXISTS ""CodigoAtivacaoHash"" character varying(100) NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""agentes"" ADD COLUMN IF NOT EXISTS ""CodigoAtivacaoHash"" character varying(100) NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""Agentes"" ADD COLUMN IF NOT EXISTS ""CodigoAtivacaoExpiracao"" timestamp with time zone;",
            @"ALTER TABLE IF EXISTS ""agentes"" ADD COLUMN IF NOT EXISTS ""CodigoAtivacaoExpiracao"" timestamp with time zone;",
            @"ALTER TABLE IF EXISTS ""Agentes"" ADD COLUMN IF NOT EXISTS ""CodigoAtivacaoUsado"" boolean NOT NULL DEFAULT FALSE;",
            @"ALTER TABLE IF EXISTS ""agentes"" ADD COLUMN IF NOT EXISTS ""CodigoAtivacaoUsado"" boolean NOT NULL DEFAULT FALSE;",
            @"ALTER TABLE IF EXISTS ""Agentes"" ADD COLUMN IF NOT EXISTS ""TentativasAtivacaoInvalidas"" integer NOT NULL DEFAULT 0;",
            @"ALTER TABLE IF EXISTS ""agentes"" ADD COLUMN IF NOT EXISTS ""TentativasAtivacaoInvalidas"" integer NOT NULL DEFAULT 0;",
            @"ALTER TABLE IF EXISTS ""Agentes"" ADD COLUMN IF NOT EXISTS ""BloqueadoAte"" timestamp with time zone;",
            @"ALTER TABLE IF EXISTS ""agentes"" ADD COLUMN IF NOT EXISTS ""BloqueadoAte"" timestamp with time zone;",
            @"ALTER TABLE IF EXISTS ""Agentes"" ADD COLUMN IF NOT EXISTS ""RevogadoEm"" timestamp with time zone;",
            @"ALTER TABLE IF EXISTS ""agentes"" ADD COLUMN IF NOT EXISTS ""RevogadoEm"" timestamp with time zone;",
            @"ALTER TABLE IF EXISTS ""Agentes"" ADD COLUMN IF NOT EXISTS ""RevogadoPor"" text;",
            @"ALTER TABLE IF EXISTS ""agentes"" ADD COLUMN IF NOT EXISTS ""RevogadoPor"" text;",
            @"CREATE INDEX IF NOT EXISTS ""IX_Agentes_CodigoAtivacaoHash"" ON ""Agentes"" (""CodigoAtivacaoHash"");",

            // Fase 8: Assinaturas e Planos
            @"ALTER TABLE IF EXISTS ""Planos"" ADD COLUMN IF NOT EXISTS ""PrecoAnual"" numeric(12,2) NOT NULL DEFAULT 0;",
            @"ALTER TABLE IF EXISTS ""Planos"" ADD COLUMN IF NOT EXISTS ""DiasTeste"" integer NOT NULL DEFAULT 14;",
            @"ALTER TABLE IF EXISTS ""Assinaturas"" ADD COLUMN IF NOT EXISTS ""Ciclo"" character varying(20) NOT NULL DEFAULT 'Mensal';",
            @"ALTER TABLE IF EXISTS ""Assinaturas"" ADD COLUMN IF NOT EXISTS ""DataRenovacao"" timestamp with time zone;",
            @"ALTER TABLE IF EXISTS ""Assinaturas"" ADD COLUMN IF NOT EXISTS ""DataCancelamento"" timestamp with time zone;",
            @"ALTER TABLE IF EXISTS ""Assinaturas"" ADD COLUMN IF NOT EXISTS ""MotivoCancelamento"" text NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""Assinaturas"" ADD COLUMN IF NOT EXISTS ""FormaPagamento"" character varying(30) NOT NULL DEFAULT 'Cartao';",
            @"ALTER TABLE IF EXISTS ""Assinaturas"" ADD COLUMN IF NOT EXISTS ""Gateway"" character varying(30) NOT NULL DEFAULT 'Asaas';",
            @"ALTER TABLE IF EXISTS ""Assinaturas"" ADD COLUMN IF NOT EXISTS ""GatewayAssinaturaId"" character varying(100) NOT NULL DEFAULT '';",

            // Fase 9: Múltiplas Filiais
            @"ALTER TABLE IF EXISTS ""Filiais"" ADD COLUMN IF NOT EXISTS ""Endereco"" text NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""Filiais"" ADD COLUMN IF NOT EXISTS ""Telefone"" text NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""Filiais"" ADD COLUMN IF NOT EXISTS ""Email"" text NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""Filiais"" ADD COLUMN IF NOT EXISTS ""Responsavel"" text NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""Filiais"" ADD COLUMN IF NOT EXISTS ""Ativa"" boolean NOT NULL DEFAULT TRUE;",
            @"ALTER TABLE IF EXISTS ""Filiais"" ADD COLUMN IF NOT EXISTS ""Token"" text NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""Filiais"" ADD COLUMN IF NOT EXISTS ""TokenGeradoEm"" timestamp with time zone;",
            @"ALTER TABLE IF EXISTS ""Filiais"" ADD COLUMN IF NOT EXISTS ""TokenRevogado"" boolean NOT NULL DEFAULT FALSE;",
            @"ALTER TABLE IF EXISTS ""Filiais"" ADD COLUMN IF NOT EXISTS ""AgenteId"" uuid;",
            @"ALTER TABLE IF EXISTS ""Agentes"" ADD COLUMN IF NOT EXISTS ""FilialId"" uuid;",
            @"ALTER TABLE IF EXISTS ""Usuarios"" ADD COLUMN IF NOT EXISTS ""FilialId"" uuid;",
            @"ALTER TABLE IF EXISTS ""Fornecedores"" ADD COLUMN IF NOT EXISTS ""FilialId"" uuid;",
            @"ALTER TABLE IF EXISTS ""Cotacoes"" ADD COLUMN IF NOT EXISTS ""FilialId"" uuid;",
            @"ALTER TABLE IF EXISTS ""Cotacoes"" ADD COLUMN IF NOT EXISTS ""TipoCompra"" character varying(30) NOT NULL DEFAULT 'Centralizada';",
            @"ALTER TABLE IF EXISTS ""PedidosCompra"" ADD COLUMN IF NOT EXISTS ""FilialId"" uuid;",
            @"ALTER TABLE IF EXISTS ""AnexosServico"" ADD COLUMN IF NOT EXISTS ""CotacaoServicoId"" uuid;",
            @"ALTER TABLE IF EXISTS ""AnexosServico"" ADD COLUMN IF NOT EXISTS ""PropostaServicoId"" uuid;",

            // Logomarca da Empresa (Multi-tenant)
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""LogoUrl"" text NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""tenants"" ADD COLUMN IF NOT EXISTS ""LogoUrl"" text NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""LogoStoragePath"" text NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""tenants"" ADD COLUMN IF NOT EXISTS ""LogoStoragePath"" text NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""LogoMimeType"" character varying(50) NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""tenants"" ADD COLUMN IF NOT EXISTS ""LogoMimeType"" character varying(50) NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""LogoAtualizadaEm"" timestamp with time zone;",
            @"ALTER TABLE IF EXISTS ""tenants"" ADD COLUMN IF NOT EXISTS ""LogoAtualizadaEm"" timestamp with time zone;",

            // Dados Cadastrais e Fiscais da Empresa (Multi-tenant)
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""InscricaoEstadual"" character varying(50) NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""tenants"" ADD COLUMN IF NOT EXISTS ""InscricaoEstadual"" character varying(50) NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""InscricaoMunicipal"" character varying(50) NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""tenants"" ADD COLUMN IF NOT EXISTS ""InscricaoMunicipal"" character varying(50) NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""Endereco"" character varying(250) NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""tenants"" ADD COLUMN IF NOT EXISTS ""Endereco"" character varying(250) NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""Numero"" character varying(30) NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""tenants"" ADD COLUMN IF NOT EXISTS ""Numero"" character varying(30) NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""Complemento"" character varying(100) NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""tenants"" ADD COLUMN IF NOT EXISTS ""Complemento"" character varying(100) NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""Bairro"" character varying(100) NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""tenants"" ADD COLUMN IF NOT EXISTS ""Bairro"" character varying(100) NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""Cidade"" character varying(100) NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""tenants"" ADD COLUMN IF NOT EXISTS ""Cidade"" character varying(100) NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""Uf"" character varying(10) NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""tenants"" ADD COLUMN IF NOT EXISTS ""Uf"" character varying(10) NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""Cep"" character varying(20) NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""tenants"" ADD COLUMN IF NOT EXISTS ""Cep"" character varying(20) NOT NULL DEFAULT '';",

            // Entregas e Recebimentos Parciais em Pedidos de Compra
            @"ALTER TABLE IF EXISTS ""PedidosCompra"" ADD COLUMN IF NOT EXISTS ""DataEntregaEfetiva"" timestamp with time zone;",
            @"ALTER TABLE IF EXISTS ""pedidos_compra"" ADD COLUMN IF NOT EXISTS ""DataEntregaEfetiva"" timestamp with time zone;",
            @"ALTER TABLE IF EXISTS ""PedidoCompraItens"" ADD COLUMN IF NOT EXISTS ""QuantidadeRecebida"" numeric(14,4) NOT NULL DEFAULT 0;",
            @"ALTER TABLE IF EXISTS ""pedido_compra_itens"" ADD COLUMN IF NOT EXISTS ""QuantidadeRecebida"" numeric(14,4) NOT NULL DEFAULT 0;",
            @"ALTER TABLE IF EXISTS ""PedidoCompraItens"" ADD COLUMN IF NOT EXISTS ""DataUltimoRecebimento"" timestamp with time zone;",
            @"ALTER TABLE IF EXISTS ""pedido_compra_itens"" ADD COLUMN IF NOT EXISTS ""DataUltimoRecebimento"" timestamp with time zone;",

            // Gestão de Ordens de Serviço: Impedimentos, Responsáveis e Apontamento de Custos Reais
            @"ALTER TABLE IF EXISTS ""OrdensServico"" ADD COLUMN IF NOT EXISTS ""StatusImpedimento"" character varying(50) NOT NULL DEFAULT 'Nenhum';",
            @"ALTER TABLE IF EXISTS ""ordens_servico"" ADD COLUMN IF NOT EXISTS ""StatusImpedimento"" character varying(50) NOT NULL DEFAULT 'Nenhum';",
            @"ALTER TABLE IF EXISTS ""OrdensServico"" ADD COLUMN IF NOT EXISTS ""MotivoImpedimento"" text NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""ordens_servico"" ADD COLUMN IF NOT EXISTS ""MotivoImpedimento"" text NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""OrdensServico"" ADD COLUMN IF NOT EXISTS ""TecnicoResponsavelNome"" character varying(150) NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""ordens_servico"" ADD COLUMN IF NOT EXISTS ""TecnicoResponsavelNome"" character varying(150) NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""OrdensServico"" ADD COLUMN IF NOT EXISTS ""TecnicoResponsavelId"" uuid;",
            @"ALTER TABLE IF EXISTS ""ordens_servico"" ADD COLUMN IF NOT EXISTS ""TecnicoResponsavelId"" uuid;",
            @"ALTER TABLE IF EXISTS ""OrdensServico"" ADD COLUMN IF NOT EXISTS ""CustoRealMaoDeObra"" numeric(14,2) NOT NULL DEFAULT 0;",
            @"ALTER TABLE IF EXISTS ""ordens_servico"" ADD COLUMN IF NOT EXISTS ""CustoRealMaoDeObra"" numeric(14,2) NOT NULL DEFAULT 0;",
            @"ALTER TABLE IF EXISTS ""OrdensServico"" ADD COLUMN IF NOT EXISTS ""CustoRealPecas"" numeric(14,2) NOT NULL DEFAULT 0;",
            @"ALTER TABLE IF EXISTS ""ordens_servico"" ADD COLUMN IF NOT EXISTS ""CustoRealPecas"" numeric(14,2) NOT NULL DEFAULT 0;",
            @"ALTER TABLE IF EXISTS ""OrdensServico"" ADD COLUMN IF NOT EXISTS ""CustoRealOutros"" numeric(14,2) NOT NULL DEFAULT 0;",
            @"ALTER TABLE IF EXISTS ""ordens_servico"" ADD COLUMN IF NOT EXISTS ""CustoRealOutros"" numeric(14,2) NOT NULL DEFAULT 0;",
            @"ALTER TABLE IF EXISTS ""OrdensServico"" ADD COLUMN IF NOT EXISTS ""DataConclusaoEfetiva"" timestamp with time zone;",
            @"ALTER TABLE IF EXISTS ""ordens_servico"" ADD COLUMN IF NOT EXISTS ""DataConclusaoEfetiva"" timestamp with time zone;",
            // Estoque: NULL = desconhecido (antes forçado a 0) e limite crítico configurável por produto
            @"ALTER TABLE IF EXISTS ""Produtos"" ALTER COLUMN ""EstoqueAtual"" DROP NOT NULL;",
            @"ALTER TABLE IF EXISTS ""Produtos"" ALTER COLUMN ""EstoqueAtual"" DROP DEFAULT;",
            @"ALTER TABLE IF EXISTS ""Produtos"" ADD COLUMN IF NOT EXISTS ""EstoqueCritico"" numeric(15,3);",

            // Fase SaaS Multi-Tenant: Slug, Precificação Dinâmica e Comercial (R$ 50 a R$ 250)
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""Slug"" character varying(80) NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""ModuloContratado"" character varying(40) NOT NULL DEFAULT 'Basico';",
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""VolumeUtilizacao"" character varying(20) NOT NULL DEFAULT 'Baixo';",
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""MensalidadeCalculada"" numeric(12,2) NOT NULL DEFAULT 50.00;",
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""DescontoComercial"" numeric(12,2) NOT NULL DEFAULT 0.00;",
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""AjusteManual"" numeric(12,2) NOT NULL DEFAULT 0.00;",
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""MensalidadeFinal"" numeric(12,2) NOT NULL DEFAULT 50.00;",
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""JustificativaPreco"" text NOT NULL DEFAULT '';",
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""DataInicioCobranca"" timestamp with time zone;",
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""ProximoVencimento"" timestamp with time zone;",
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""SituacaoFinanceira"" character varying(30) NOT NULL DEFAULT 'Regular';",
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""MaxUsuariosContratado"" integer NOT NULL DEFAULT 10;",
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""MaxFiliaisContratado"" integer NOT NULL DEFAULT 5;",
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""MaxProdutosContratado"" integer NOT NULL DEFAULT 10000;",
            @"ALTER TABLE IF EXISTS ""Tenants"" ADD COLUMN IF NOT EXISTS ""MaxSincronizadoresContratado"" integer NOT NULL DEFAULT 3;",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Tenants_Slug"" ON ""Tenants"" (""Slug"") WHERE ""Slug"" IS NOT NULL AND ""Slug"" <> '';"
        };

        foreach (var sql in colunasAgente)
        {
            try { await context.Database.ExecuteSqlRawAsync(sql); } catch { }
        }

        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""RefreshTokens"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""DataCriacao"" timestamp with time zone NOT NULL,
                    ""DataAtualizacao"" timestamp with time zone,
                    ""UsuarioId"" uuid NOT NULL,
                    ""FamilyId"" uuid NOT NULL,
                    ""TokenHash"" character varying(64) NOT NULL,
                    ""ExpiraEm"" timestamp with time zone NOT NULL,
                    ""RevogadoEm"" timestamp with time zone,
                    ""SubstituidoPorId"" uuid,
                    ""Motivo"" text NOT NULL DEFAULT '',
                    ""Ip"" character varying(60) NOT NULL DEFAULT ''
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_RefreshTokens_TokenHash"" ON ""RefreshTokens"" (""TokenHash"");
                CREATE INDEX IF NOT EXISTS ""IX_RefreshTokens_FamilyId"" ON ""RefreshTokens"" (""FamilyId"");
                CREATE INDEX IF NOT EXISTS ""IX_RefreshTokens_UsuarioId"" ON ""RefreshTokens"" (""UsuarioId"");
            ");
        }
        catch { }

        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""PedidosCompra"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""DataCriacao"" timestamp with time zone NOT NULL,
                    ""DataAtualizacao"" timestamp with time zone,
                    ""TenantId"" uuid NOT NULL,
                    ""Numero"" character varying(30) NOT NULL,
                    ""CotacaoId"" uuid,
                    ""FornecedorId"" uuid NOT NULL,
                    ""Status"" character varying(30) NOT NULL,
                    ""Subtotal"" numeric(15,2) NOT NULL,
                    ""Desconto"" numeric(15,2) NOT NULL,
                    ""Frete"" numeric(15,2) NOT NULL,
                    ""OutrasDespesas"" numeric(15,2) NOT NULL,
                    ""ImpostosTotal"" numeric(15,2) NOT NULL,
                    ""ValorTotal"" numeric(15,2) NOT NULL,
                    ""CondicaoPagamento"" text NOT NULL DEFAULT '',
                    ""PrazoEntregaDias"" integer NOT NULL DEFAULT 0,
                    ""PrevisaoEntrega"" timestamp with time zone,
                    ""Observacoes"" text NOT NULL DEFAULT '',
                    ""AprovadoPorId"" uuid,
                    ""AprovadoPorNome"" text NOT NULL DEFAULT '',
                    ""AprovadoEm"" timestamp with time zone,
                    ""TimelineJson"" text NOT NULL DEFAULT '[]'
                );
                CREATE INDEX IF NOT EXISTS ""IX_PedidosCompra_TenantId_Numero"" ON ""PedidosCompra"" (""TenantId"", ""Numero"");
                CREATE INDEX IF NOT EXISTS ""IX_PedidosCompra_FornecedorId"" ON ""PedidosCompra"" (""FornecedorId"");

                CREATE TABLE IF NOT EXISTS ""PedidoCompraItens"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""DataCriacao"" timestamp with time zone NOT NULL,
                    ""DataAtualizacao"" timestamp with time zone,
                    ""TenantId"" uuid NOT NULL,
                    ""PedidoCompraId"" uuid NOT NULL,
                    ""ProdutoId"" uuid NOT NULL,
                    ""CodigoProduto"" text NOT NULL DEFAULT '',
                    ""DescricaoProduto"" text NOT NULL DEFAULT '',
                    ""Unidade"" text NOT NULL DEFAULT 'UN',
                    ""Ncm"" text NOT NULL DEFAULT '',
                    ""Quantidade"" numeric(15,3) NOT NULL,
                    ""PrecoUnitario"" numeric(15,4) NOT NULL,
                    ""DescontoPerc"" numeric(7,4) NOT NULL,
                    ""PrecoLiquidoUnit"" numeric(15,4) NOT NULL,
                    ""Subtotal"" numeric(15,2) NOT NULL,
                    ""Cst"" character varying(4) NOT NULL DEFAULT '',
                    ""Cfop"" character varying(4) NOT NULL DEFAULT '',
                    ""IcmsAliq"" numeric(7,4) NOT NULL,
                    ""IpiAliq"" numeric(7,4) NOT NULL,
                    ""Mva"" numeric(8,4) NOT NULL,
                    ""IcmsStAliq"" numeric(7,4) NOT NULL,
                    ""IcmsStValor"" numeric(15,2) NOT NULL,
                    ""CustoEfetivoUnit"" numeric(15,4) NOT NULL,
                    ""CustoEfetivoTotal"" numeric(15,2) NOT NULL,
                    CONSTRAINT ""FK_PedidoCompraItens_PedidosCompra"" FOREIGN KEY (""PedidoCompraId"") REFERENCES ""PedidosCompra"" (""Id"") ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS ""IX_PedidoCompraItens_PedidoCompraId"" ON ""PedidoCompraItens"" (""PedidoCompraId"");
                CREATE INDEX IF NOT EXISTS ""IX_PedidoCompraItens_ProdutoId"" ON ""PedidoCompraItens"" (""ProdutoId"");
                CREATE TABLE IF NOT EXISTS ""Faturas"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""DataCriacao"" timestamp with time zone NOT NULL,
                    ""DataAtualizacao"" timestamp with time zone,
                    ""TenantId"" uuid NOT NULL,
                    ""AssinaturaId"" uuid,
                    ""Numero"" character varying(50) NOT NULL,
                    ""Descricao"" text NOT NULL DEFAULT '',
                    ""Valor"" numeric(12,2) NOT NULL,
                    ""Status"" character varying(30) NOT NULL DEFAULT 'Pendente',
                    ""DataEmissao"" timestamp with time zone NOT NULL,
                    ""DataVencimento"" timestamp with time zone NOT NULL,
                    ""DataPagamento"" timestamp with time zone,
                    ""MetodoPagamento"" character varying(30) NOT NULL DEFAULT 'Pix',
                    ""PixQrCode"" text NOT NULL DEFAULT '',
                    ""PixCopiaECola"" text NOT NULL DEFAULT '',
                    ""UrlBoleto"" text NOT NULL DEFAULT '',
                    ""GatewayTransacaoId"" character varying(100) NOT NULL DEFAULT '',
                    ""ComprovanteUrl"" text NOT NULL DEFAULT ''
                );
                CREATE INDEX IF NOT EXISTS ""IX_Faturas_TenantId_Numero"" ON ""Faturas"" (""TenantId"", ""Numero"");

                CREATE TABLE IF NOT EXISTS ""EstoquesFilial"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""DataCriacao"" timestamp with time zone NOT NULL,
                    ""DataAtualizacao"" timestamp with time zone,
                    ""TenantId"" uuid NOT NULL,
                    ""FilialId"" uuid NOT NULL,
                    ""ProdutoId"" uuid NOT NULL,
                    ""EstoqueAtual"" numeric(15,3) NOT NULL DEFAULT 0,
                    ""EstoqueMinimo"" numeric(15,3) NOT NULL DEFAULT 0,
                    ""EstoquePendente"" numeric(15,3) NOT NULL DEFAULT 0,
                    ""UltimaAtualizacao"" timestamp with time zone NOT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_EstoquesFilial_Tenant_Filial_Prod"" ON ""EstoquesFilial"" (""TenantId"", ""FilialId"", ""ProdutoId"");

                CREATE TABLE IF NOT EXISTS ""SolicitacoesTransferencia"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""DataCriacao"" timestamp with time zone NOT NULL,
                    ""DataAtualizacao"" timestamp with time zone,
                    ""TenantId"" uuid NOT NULL,
                    ""Numero"" character varying(50) NOT NULL,
                    ""FilialOrigemId"" uuid NOT NULL,
                    ""FilialDestinoId"" uuid NOT NULL,
                    ""Status"" character varying(30) NOT NULL DEFAULT 'Pendente',
                    ""Observacoes"" text NOT NULL DEFAULT '',
                    ""SolicitadoPorId"" uuid,
                    ""SolicitadoPorNome"" text NOT NULL DEFAULT '',
                    ""AprovadoPorId"" uuid,
                    ""AprovadoPorNome"" text NOT NULL DEFAULT '',
                    ""DataAprovacao"" timestamp with time zone,
                    ""DataConclusao"" timestamp with time zone
                );
                CREATE INDEX IF NOT EXISTS ""IX_SolicitacoesTransferencia_TenantId"" ON ""SolicitacoesTransferencia"" (""TenantId"");

                CREATE TABLE IF NOT EXISTS ""SolicitacaoTransferenciaItens"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""DataCriacao"" timestamp with time zone NOT NULL,
                    ""DataAtualizacao"" timestamp with time zone,
                    ""TenantId"" uuid NOT NULL,
                    ""SolicitacaoTransferenciaId"" uuid NOT NULL,
                    ""ProdutoId"" uuid NOT NULL,
                    ""CodigoProduto"" text NOT NULL DEFAULT '',
                    ""DescricaoProduto"" text NOT NULL DEFAULT '',
                    ""QuantidadeSolicitada"" numeric(15,3) NOT NULL,
                    ""QuantidadeAtendida"" numeric(15,3) NOT NULL DEFAULT 0
                );
                CREATE INDEX IF NOT EXISTS ""IX_SolicitacaoTransferenciaItens_TransfId"" ON ""SolicitacaoTransferenciaItens"" (""SolicitacaoTransferenciaId"");

                -- Tabelas do Módulo de Cotação de Serviços
                CREATE TABLE IF NOT EXISTS ""CotacoesServico"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""DataCriacao"" timestamp with time zone NOT NULL,
                    ""DataAtualizacao"" timestamp with time zone,
                    ""TenantId"" uuid NOT NULL,
                    ""Numero"" character varying(30) NOT NULL,
                    ""Titulo"" character varying(200) NOT NULL,
                    ""Categoria"" character varying(50) NOT NULL DEFAULT 'Geral',
                    ""Prioridade"" character varying(20) NOT NULL DEFAULT 'Media',
                    ""FilialId"" uuid,
                    ""ResponsavelId"" uuid,
                    ""ResponsavelNome"" text NOT NULL DEFAULT '',
                    ""ResponsavelEmail"" text NOT NULL DEFAULT '',
                    ""DataAbertura"" timestamp with time zone NOT NULL,
                    ""DataLimiteResposta"" timestamp with time zone NOT NULL,
                    ""DataDesejadaInicio"" timestamp with time zone,
                    ""PrazoEstimadoDias"" integer NOT NULL DEFAULT 30,
                    ""NecessidadeMotivacao"" text NOT NULL DEFAULT '',
                    ""DescricaoDetalhada"" text NOT NULL DEFAULT '',
                    ""ObjetivoEsperado"" text NOT NULL DEFAULT '',
                    ""ResultadoEsperado"" text NOT NULL DEFAULT '',
                    ""AtendimentoPresencial"" boolean NOT NULL DEFAULT TRUE,
                    ""Cep"" character varying(20) NOT NULL DEFAULT '',
                    ""Endereco"" text NOT NULL DEFAULT '',
                    ""NumeroLocal"" character varying(50) NOT NULL DEFAULT '',
                    ""Complemento"" character varying(100) NOT NULL DEFAULT '',
                    ""Bairro"" character varying(100) NOT NULL DEFAULT '',
                    ""Cidade"" character varying(100) NOT NULL DEFAULT '',
                    ""Uf"" character varying(10) NOT NULL DEFAULT '',
                    ""PontoReferencia"" text NOT NULL DEFAULT '',
                    ""HorarioPermitido"" text NOT NULL DEFAULT 'Horário comercial padrão',
                    ""MateriaisInclusosExigidos"" text NOT NULL DEFAULT '',
                    ""EquipamentosExigidos"" text NOT NULL DEFAULT '',
                    ""GarantiaEsperadaMeses"" integer NOT NULL DEFAULT 12,
                    ""CondicoesEspeciais"" text NOT NULL DEFAULT '',
                    ""CertificacoesExigidas"" text NOT NULL DEFAULT '',
                    ""PesoPreco"" numeric(5,2) NOT NULL DEFAULT 50,
                    ""PesoPrazo"" numeric(5,2) NOT NULL DEFAULT 20,
                    ""PesoTecnico"" numeric(5,2) NOT NULL DEFAULT 20,
                    ""PesoCondicoes"" numeric(5,2) NOT NULL DEFAULT 10,
                    ""Status"" character varying(30) NOT NULL DEFAULT 'Rascunho',
                    ""PropostaVencedoraId"" uuid,
                    ""FornecedorVencedorId"" uuid,
                    ""TotalEstimado"" numeric(15,2) NOT NULL DEFAULT 0,
                    ""TotalContratado"" numeric(15,2) NOT NULL DEFAULT 0,
                    ""EconomiaRealizada"" numeric(15,2) NOT NULL DEFAULT 0,
                    ""JustificativaEscolha"" text NOT NULL DEFAULT '',
                    ""AprovadoPorId"" uuid,
                    ""AprovadoPorNome"" text NOT NULL DEFAULT '',
                    ""AprovadoEm"" timestamp with time zone,
                    ""ObservacaoAprovacao"" text NOT NULL DEFAULT '',
                    ""TimelineJson"" text NOT NULL DEFAULT '[]'
                );
                CREATE INDEX IF NOT EXISTS ""IX_CotacoesServico_TenantId_Numero"" ON ""CotacoesServico"" (""TenantId"", ""Numero"");

                CREATE TABLE IF NOT EXISTS ""AtividadesServico"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""DataCriacao"" timestamp with time zone NOT NULL,
                    ""DataAtualizacao"" timestamp with time zone,
                    ""TenantId"" uuid NOT NULL,
                    ""CotacaoServicoId"" uuid NOT NULL,
                    ""Ordem"" integer NOT NULL DEFAULT 1,
                    ""Etapa"" text NOT NULL DEFAULT '',
                    ""Nome"" character varying(200) NOT NULL,
                    ""Descricao"" text NOT NULL DEFAULT '',
                    ""Quantidade"" numeric(15,3) NOT NULL DEFAULT 1,
                    ""UnidadeMedicao"" text NOT NULL DEFAULT 'SV',
                    ""LocalExecucao"" text NOT NULL DEFAULT '',
                    ""RequisitosTecnicos"" text NOT NULL DEFAULT '',
                    ""ResultadoEsperado"" text NOT NULL DEFAULT '',
                    ""ValorEstimadoUnitario"" numeric(15,2) NOT NULL DEFAULT 0,
                    CONSTRAINT ""FK_AtividadesServico_Cotacao"" FOREIGN KEY (""CotacaoServicoId"") REFERENCES ""CotacoesServico"" (""Id"") ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS ""IX_AtividadesServico_CotacaoServicoId"" ON ""AtividadesServico"" (""CotacaoServicoId"");

                CREATE TABLE IF NOT EXISTS ""FornecedoresServico"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""DataCriacao"" timestamp with time zone NOT NULL,
                    ""DataAtualizacao"" timestamp with time zone,
                    ""TenantId"" uuid NOT NULL,
                    ""CotacaoServicoId"" uuid NOT NULL,
                    ""FornecedorId"" uuid NOT NULL,
                    ""Status"" character varying(30) NOT NULL DEFAULT 'Convidado',
                    ""MotivoRecusa"" text NOT NULL DEFAULT '',
                    ""RecusadoEm"" timestamp with time zone,
                    CONSTRAINT ""FK_FornecedoresServico_Cotacao"" FOREIGN KEY (""CotacaoServicoId"") REFERENCES ""CotacoesServico"" (""Id"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_FornecedoresServico_Fornecedor"" FOREIGN KEY (""FornecedorId"") REFERENCES ""Fornecedores"" (""Id"") ON DELETE RESTRICT
                );
                CREATE INDEX IF NOT EXISTS ""IX_FornecedoresServico_CotacaoId_FornecedorId"" ON ""FornecedoresServico"" (""CotacaoServicoId"", ""FornecedorId"");

                CREATE TABLE IF NOT EXISTS ""AcessosFornecedorServico"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""DataCriacao"" timestamp with time zone NOT NULL,
                    ""DataAtualizacao"" timestamp with time zone,
                    ""TenantId"" uuid NOT NULL,
                    ""FornecedorServicoId"" uuid NOT NULL,
                    ""Token"" character varying(64) NOT NULL,
                    ""CodigoHash"" text NOT NULL DEFAULT '',
                    ""ExpiraEm"" timestamp with time zone NOT NULL,
                    ""Revogado"" boolean NOT NULL DEFAULT FALSE,
                    ""Tentativas"" integer NOT NULL DEFAULT 0,
                    ""BloqueadoAte"" timestamp with time zone,
                    ""Visualizacoes"" integer NOT NULL DEFAULT 0,
                    ""PrimeiraVisualizacaoEm"" timestamp with time zone,
                    ""UltimaVisualizacaoEm"" timestamp with time zone,
                    CONSTRAINT ""FK_AcessosFornecedorServico"" FOREIGN KEY (""FornecedorServicoId"") REFERENCES ""FornecedoresServico"" (""Id"") ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS ""IX_AcessosFornecedorServico_Token"" ON ""AcessosFornecedorServico"" (""Token"");

                CREATE TABLE IF NOT EXISTS ""PropostasServico"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""DataCriacao"" timestamp with time zone NOT NULL,
                    ""DataAtualizacao"" timestamp with time zone,
                    ""TenantId"" uuid NOT NULL,
                    ""FornecedorServicoId"" uuid NOT NULL,
                    ""Status"" character varying(30) NOT NULL DEFAULT 'Rascunho',
                    ""ValorMaoDeObra"" numeric(15,2) NOT NULL DEFAULT 0,
                    ""ValorMateriaisInclusos"" numeric(15,2) NOT NULL DEFAULT 0,
                    ""ValorEquipamentos"" numeric(15,2) NOT NULL DEFAULT 0,
                    ""Deslocamento"" numeric(15,2) NOT NULL DEFAULT 0,
                    ""OutrasDespesas"" numeric(15,2) NOT NULL DEFAULT 0,
                    ""Desconto"" numeric(15,2) NOT NULL DEFAULT 0,
                    ""ImpostosTotal"" numeric(15,2) NOT NULL DEFAULT 0,
                    ""ValorTotal"" numeric(15,2) NOT NULL DEFAULT 0,
                    ""PrazoInicioDias"" integer NOT NULL DEFAULT 3,
                    ""PrazoExecucaoDias"" integer NOT NULL DEFAULT 15,
                    ""CondicoesPagamento"" text NOT NULL DEFAULT '30 dias',
                    ""ValidadeProposta"" date,
                    ""GarantiaMeses"" integer NOT NULL DEFAULT 12,
                    ""DescricaoSolucao"" text NOT NULL DEFAULT '',
                    ""Metodologia"" text NOT NULL DEFAULT '',
                    ""EquipeAlocada"" text NOT NULL DEFAULT '',
                    ""EquipamentosFerramental"" text NOT NULL DEFAULT '',
                    ""MateriaisInclusosTexto"" text NOT NULL DEFAULT '',
                    ""MateriaisNaoInclusosTexto"" text NOT NULL DEFAULT '',
                    ""CertificacoesEquipe"" text NOT NULL DEFAULT '',
                    ""LimitacoesExclusoes"" text NOT NULL DEFAULT '',
                    ""Observacoes"" text NOT NULL DEFAULT '',
                    ""ContatoNome"" text NOT NULL DEFAULT '',
                    ""ContatoTelefone"" text NOT NULL DEFAULT '',
                    ""ContatoEmail"" text NOT NULL DEFAULT '',
                    ""EnviadaEm"" timestamp with time zone,
                    ""NotaComercial"" numeric(5,2) NOT NULL DEFAULT 0,
                    ""NotaTecnica"" numeric(5,2) NOT NULL DEFAULT 0,
                    ""NotaPrazo"" numeric(5,2) NOT NULL DEFAULT 0,
                    ""NotaCondicoes"" numeric(5,2) NOT NULL DEFAULT 0,
                    ""PontuacaoPonderada"" numeric(5,2) NOT NULL DEFAULT 0,
                    ""ParecerAvaliacao"" text NOT NULL DEFAULT '',
                    CONSTRAINT ""FK_PropostasServico_FornecedorServico"" FOREIGN KEY (""FornecedorServicoId"") REFERENCES ""FornecedoresServico"" (""Id"") ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS ""IX_PropostasServico_FornecedorServicoId"" ON ""PropostasServico"" (""FornecedorServicoId"");

                CREATE TABLE IF NOT EXISTS ""PropostaServicoEtapas"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""DataCriacao"" timestamp with time zone NOT NULL,
                    ""DataAtualizacao"" timestamp with time zone,
                    ""TenantId"" uuid NOT NULL,
                    ""PropostaServicoId"" uuid NOT NULL,
                    ""AtividadeServicoId"" uuid NOT NULL,
                    ""ValorUnitario"" numeric(15,2) NOT NULL DEFAULT 0,
                    ""ValorTotal"" numeric(15,2) NOT NULL DEFAULT 0,
                    ""DetalhesExecucao"" text NOT NULL DEFAULT '',
                    ""Observacao"" text NOT NULL DEFAULT '',
                    CONSTRAINT ""FK_PropostaServicoEtapas_Proposta"" FOREIGN KEY (""PropostaServicoId"") REFERENCES ""PropostasServico"" (""Id"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_PropostaServicoEtapas_Atividade"" FOREIGN KEY (""AtividadeServicoId"") REFERENCES ""AtividadesServico"" (""Id"") ON DELETE RESTRICT
                );
                CREATE INDEX IF NOT EXISTS ""IX_PropostaServicoEtapas_Proposta_Ativ"" ON ""PropostaServicoEtapas"" (""PropostaServicoId"", ""AtividadeServicoId"");

                CREATE TABLE IF NOT EXISTS ""AnexosServico"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""DataCriacao"" timestamp with time zone NOT NULL,
                    ""DataAtualizacao"" timestamp with time zone,
                    ""TenantId"" uuid NOT NULL,
                    ""Origem"" character varying(30) NOT NULL DEFAULT 'Cotacao',
                    ""ReferenciaId"" uuid NOT NULL,
                    ""CotacaoServicoId"" uuid,
                    ""PropostaServicoId"" uuid,
                    ""NomeArquivo"" character varying(255) NOT NULL,
                    ""ContentType"" character varying(100) NOT NULL DEFAULT '',
                    ""Tamanho"" bigint NOT NULL DEFAULT 0,
                    ""CaminhoStorage"" text NOT NULL DEFAULT '',
                    ""Descricao"" text NOT NULL DEFAULT ''
                );
                CREATE INDEX IF NOT EXISTS ""IX_AnexosServico_Origem_Ref"" ON ""AnexosServico"" (""Origem"", ""ReferenciaId"");

                CREATE TABLE IF NOT EXISTS ""OrdensServico"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""DataCriacao"" timestamp with time zone NOT NULL,
                    ""DataAtualizacao"" timestamp with time zone,
                    ""TenantId"" uuid NOT NULL,
                    ""Numero"" character varying(30) NOT NULL,
                    ""CotacaoServicoId"" uuid NOT NULL,
                    ""PropostaServicoId"" uuid NOT NULL,
                    ""FornecedorId"" uuid NOT NULL,
                    ""FilialId"" uuid,
                    ""Status"" character varying(30) NOT NULL DEFAULT 'Criada',
                    ""ValorTotal"" numeric(15,2) NOT NULL DEFAULT 0,
                    ""ValorMaoDeObra"" numeric(15,2) NOT NULL DEFAULT 0,
                    ""ValorMateriais"" numeric(15,2) NOT NULL DEFAULT 0,
                    ""Deslocamento"" numeric(15,2) NOT NULL DEFAULT 0,
                    ""ImpostosTotal"" numeric(15,2) NOT NULL DEFAULT 0,
                    ""CondicaoPagamento"" text NOT NULL DEFAULT '',
                    ""PrazoExecucaoDias"" integer NOT NULL DEFAULT 30,
                    ""PrevisaoInicio"" timestamp with time zone,
                    ""PrevisaoTermino"" timestamp with time zone,
                    ""GarantiaMeses"" integer NOT NULL DEFAULT 12,
                    ""EscopoAprovadoResumo"" text NOT NULL DEFAULT '',
                    ""ClausulasEspeciais"" text NOT NULL DEFAULT '',
                    ""Observacoes"" text NOT NULL DEFAULT '',
                    ""AprovadoPorId"" uuid,
                    ""AprovadoPorNome"" text NOT NULL DEFAULT '',
                    ""AprovadoEm"" timestamp with time zone,
                    ""TimelineJson"" text NOT NULL DEFAULT '[]'
                );
                CREATE INDEX IF NOT EXISTS ""IX_OrdensServico_TenantId_Numero"" ON ""OrdensServico"" (""TenantId"", ""Numero"");
                CREATE INDEX IF NOT EXISTS ""IX_OrdensServico_CotacaoServicoId"" ON ""OrdensServico"" (""CotacaoServicoId"");
                CREATE INDEX IF NOT EXISTS ""IX_OrdensServico_FornecedorId"" ON ""OrdensServico"" (""FornecedorId"");

                CREATE TABLE IF NOT EXISTS ""ConfiguracoesPrecificacao"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""DataCriacao"" timestamp with time zone NOT NULL,
                    ""DataAtualizacao"" timestamp with time zone,
                    ""Versao"" integer NOT NULL DEFAULT 1,
                    ""Ativa"" boolean NOT NULL DEFAULT TRUE,
                    ""MensalidadeBase"" numeric(12,2) NOT NULL DEFAULT 50.00,
                    ""ValorMinimo"" numeric(12,2) NOT NULL DEFAULT 50.00,
                    ""ValorMaximo"" numeric(12,2) NOT NULL DEFAULT 250.00,
                    ""AdicProdFaixa1"" numeric(12,2) NOT NULL DEFAULT 0.00,
                    ""AdicProdFaixa2"" numeric(12,2) NOT NULL DEFAULT 15.00,
                    ""AdicProdFaixa3"" numeric(12,2) NOT NULL DEFAULT 30.00,
                    ""AdicProdFaixa4"" numeric(12,2) NOT NULL DEFAULT 45.00,
                    ""AdicFilialFaixa1"" numeric(12,2) NOT NULL DEFAULT 0.00,
                    ""AdicFilialFaixa2"" numeric(12,2) NOT NULL DEFAULT 20.00,
                    ""AdicFilialFaixa3"" numeric(12,2) NOT NULL DEFAULT 40.00,
                    ""AdicFilialFaixa4"" numeric(12,2) NOT NULL DEFAULT 60.00,
                    ""AdicUsuarioFaixa1"" numeric(12,2) NOT NULL DEFAULT 0.00,
                    ""AdicUsuarioFaixa2"" numeric(12,2) NOT NULL DEFAULT 10.00,
                    ""AdicUsuarioFaixa3"" numeric(12,2) NOT NULL DEFAULT 20.00,
                    ""AdicUsuarioFaixa4"" numeric(12,2) NOT NULL DEFAULT 30.00,
                    ""AdicSincFaixa1"" numeric(12,2) NOT NULL DEFAULT 0.00,
                    ""AdicSincFaixa2"" numeric(12,2) NOT NULL DEFAULT 10.00,
                    ""AdicSincFaixa3"" numeric(12,2) NOT NULL DEFAULT 20.00,
                    ""AdicVolumeBaixo"" numeric(12,2) NOT NULL DEFAULT 0.00,
                    ""AdicVolumeMedio"" numeric(12,2) NOT NULL DEFAULT 10.00,
                    ""AdicVolumeAlto"" numeric(12,2) NOT NULL DEFAULT 25.00,
                    ""AdicModuloBasico"" numeric(12,2) NOT NULL DEFAULT 0.00,
                    ""AdicModuloRelatorios"" numeric(12,2) NOT NULL DEFAULT 15.00,
                    ""AdicModuloCompleto"" numeric(12,2) NOT NULL DEFAULT 30.00,
                    ""RegrasJson"" text NOT NULL DEFAULT '{{}}',
                    ""AtualizadoPor"" text NOT NULL DEFAULT 'Sistema',
                    ""MotivoAlteracao"" text NOT NULL DEFAULT 'Configuração inicial'
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ConfiguracoesPrecificacao_Versao"" ON ""ConfiguracoesPrecificacao"" (""Versao"");

                CREATE TABLE IF NOT EXISTS ""HistoricosComerciaisTenant"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""DataCriacao"" timestamp with time zone NOT NULL,
                    ""DataAtualizacao"" timestamp with time zone,
                    ""TenantId"" uuid NOT NULL,
                    ""TipoAlteracao"" text NOT NULL DEFAULT 'Precificacao',
                    ""MensalidadeAnterior"" numeric(12,2) NOT NULL DEFAULT 50.00,
                    ""MensalidadeCalculada"" numeric(12,2) NOT NULL DEFAULT 50.00,
                    ""DescontoAplicado"" numeric(12,2) NOT NULL DEFAULT 0.00,
                    ""AjusteManual"" numeric(12,2) NOT NULL DEFAULT 0.00,
                    ""MensalidadeFinal"" numeric(12,2) NOT NULL DEFAULT 50.00,
                    ""ModuloContratado"" text NOT NULL DEFAULT 'Basico',
                    ""VolumeUtilizacao"" text NOT NULL DEFAULT 'Baixo',
                    ""Justificativa"" text NOT NULL DEFAULT '',
                    ""AlteradoPor"" text NOT NULL DEFAULT '',
                    ""NotificadoCliente"" boolean NOT NULL DEFAULT TRUE,
                    ""VersaoTabelaPrecos"" integer NOT NULL DEFAULT 1,
                    ""SnapshotConsumoJson"" text NOT NULL DEFAULT '{{}}'
                );
                CREATE INDEX IF NOT EXISTS ""IX_HistoricosComerciaisTenant_TenantId"" ON ""HistoricosComerciaisTenant"" (""TenantId"", ""DataCriacao"");

                CREATE TABLE IF NOT EXISTS ""TransacoesPagamento"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""DataCriacao"" timestamp with time zone NOT NULL,
                    ""DataAtualizacao"" timestamp with time zone,
                    ""TenantId"" uuid NOT NULL,
                    ""FaturaId"" uuid,
                    ""AssinaturaId"" uuid,
                    ""Gateway"" text NOT NULL DEFAULT 'Configuravel',
                    ""GatewayTransacaoId"" text NOT NULL DEFAULT '',
                    ""Evento"" text NOT NULL DEFAULT '',
                    ""Valor"" numeric(12,2) NOT NULL DEFAULT 0.00,
                    ""Status"" text NOT NULL DEFAULT 'Pendente',
                    ""MetodoPagamento"" text NOT NULL DEFAULT '',
                    ""ComprovanteUrl"" text NOT NULL DEFAULT '',
                    ""PayloadJson"" text NOT NULL DEFAULT '{{}}',
                    ""ConfirmadoEm"" timestamp with time zone
                );
                CREATE INDEX IF NOT EXISTS ""IX_TransacoesPagamento_TenantId"" ON ""TransacoesPagamento"" (""TenantId"", ""DataCriacao"");

                CREATE TABLE IF NOT EXISTS ""SolicitacoesAlteracaoPlano"" (
                    ""Id"" uuid NOT NULL PRIMARY KEY,
                    ""DataCriacao"" timestamp with time zone NOT NULL,
                    ""DataAtualizacao"" timestamp with time zone,
                    ""TenantId"" uuid NOT NULL,
                    ""SolicitadoPorUsuarioId"" uuid,
                    ""SolicitadoPorNome"" text NOT NULL DEFAULT '',
                    ""ModuloAtual"" text NOT NULL DEFAULT 'Basico',
                    ""ModuloSolicitado"" text NOT NULL DEFAULT 'CompletoServicos',
                    ""ObservacoesCliente"" text NOT NULL DEFAULT '',
                    ""Status"" text NOT NULL DEFAULT 'Pendente',
                    ""RespostaAdmin"" text NOT NULL DEFAULT '',
                    ""AnalisadoPor"" text NOT NULL DEFAULT '',
                    ""AnalisadoEm"" timestamp with time zone
                );
                CREATE INDEX IF NOT EXISTS ""IX_SolicitacoesAlteracaoPlano_TenantId"" ON ""SolicitacoesAlteracaoPlano"" (""TenantId"", ""DataCriacao"");
            ");
        }
        catch { }

        // Garantir que tabelas operacionais utilizem o isolamento multitenant de alto desempenho via EF Core Global Query Filters
        var tablesWithTenant = new[]
        {
            "Produtos", "Fornecedores", "Cotacoes", "PedidosCompra", "Agentes",
            "Assinaturas", "Filiais", "ConfiguracoesTenant", "Sincronizacoes", "SincronizacaoErros",
            "Faturas", "EstoquesFilial", "SolicitacoesTransferencia", "SolicitacaoTransferenciaItem",
            "CotacoesServico", "AtividadesServico", "FornecedoresServico", "AcessosFornecedorServico",
            "PropostasServico", "PropostaServicoEtapas", "AnexosServico", "OrdensServico"
        };

        foreach (var table in tablesWithTenant)
        {
            try
            {
                await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"" + table + "\" DISABLE ROW LEVEL SECURITY;");
            }
            catch { }
        }

        // 1. Seed Planos
        if (!await context.Planos.IgnoreQueryFilters().AnyAsync())
        {
            var planos = new List<Plano>
            {
                new()
                {
                    Nome = "Inicial",
                    Mensalidade = 99m,
                    PrecoAnual = 990m,
                    DiasTeste = 14,
                    MaxUsuarios = 2,
                    MaxFiliais = 1,
                    MaxFornecedores = 50,
                    MaxCotacoesMes = 100,
                    RecursosJson = "{\"relatorios\": true, \"api_erp\": true, \"portal_fornecedor\": true}",
                    Ativo = true
                },
                new()
                {
                    Nome = "Profissional",
                    Mensalidade = 199m,
                    PrecoAnual = 1990m,
                    DiasTeste = 14,
                    MaxUsuarios = 5,
                    MaxFiliais = 3,
                    MaxFornecedores = 200,
                    MaxCotacoesMes = 500,
                    RecursosJson = "{\"relatorios\": true, \"api_erp\": true, \"portal_fornecedor\": true, \"ia_precos\": true}",
                    Ativo = true
                },
                new()
                {
                    Nome = "Empresarial",
                    Mensalidade = 399m,
                    PrecoAnual = 3990m,
                    DiasTeste = 14,
                    MaxUsuarios = 15,
                    MaxFiliais = 10,
                    MaxFornecedores = 9999,
                    MaxCotacoesMes = 9999,
                    RecursosJson = "{\"relatorios\": true, \"api_erp\": true, \"portal_fornecedor\": true, \"ia_precos\": true, \"suporte_dedicado\": true}",
                    Ativo = true
                }
            };

            await context.Planos.AddRangeAsync(planos);
            try { await context.SaveChangesAsync(); } catch { /* Concorrência em testes */ }
        }
        else
        {
            var planList = await context.Planos.IgnoreQueryFilters().ToListAsync();
            bool modified = false;
            foreach (var p in planList)
            {
                if (p.PrecoAnual == 0)
                {
                    p.PrecoAnual = p.Mensalidade * 10m;
                    p.DiasTeste = 14;
                    modified = true;
                }
            }
            if (modified) try { await context.SaveChangesAsync(); } catch { }
        }

        // 2. Seed Administrador Geral da Plataforma (Conta Principal Exclusiva)
        var masterEmail = "juniosergio034@gmail.com";
        var masterAdmin = await context.Usuarios.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == masterEmail);
        if (masterAdmin == null)
        {
            var admin = new Usuario
            {
                Nome = "Administrador Geral",
                Email = masterEmail,
                SenhaHash = BCrypt.Net.BCrypt.HashPassword("34777126Pe@"),
                Perfil = "AdministradorGeral",
                Ativo = true,
                TenantId = null
            };

            await context.Usuarios.AddAsync(admin);
            try { await context.SaveChangesAsync(); } catch { /* Concorrência em testes */ }
        }
        else
        {
            if (masterAdmin.Perfil != "AdministradorGeral" || !masterAdmin.Ativo || masterAdmin.TenantId != null)
            {
                masterAdmin.Perfil = "AdministradorGeral";
                masterAdmin.Ativo = true;
                masterAdmin.TenantId = null;
                try { await context.SaveChangesAsync(); } catch { }
            }
        }

        // 3. Seed Tabela de Precificação Dinâmica (R$ 50,00 a R$ 250,00)
        if (!await context.ConfiguracoesPrecificacao.IgnoreQueryFilters().AnyAsync())
        {
            var cfgPrecificacao = new ConfiguracaoPrecificacao
            {
                Versao = 1,
                Ativa = true,
                MensalidadeBase = 50.00m,
                ValorMinimo = 50.00m,
                ValorMaximo = 250.00m,
                AdicProdFaixa1 = 0.00m,
                AdicProdFaixa2 = 15.00m,
                AdicProdFaixa3 = 30.00m,
                AdicProdFaixa4 = 45.00m,
                AdicFilialFaixa1 = 0.00m,
                AdicFilialFaixa2 = 20.00m,
                AdicFilialFaixa3 = 40.00m,
                AdicFilialFaixa4 = 60.00m,
                AdicUsuarioFaixa1 = 0.00m,
                AdicUsuarioFaixa2 = 10.00m,
                AdicUsuarioFaixa3 = 20.00m,
                AdicUsuarioFaixa4 = 30.00m,
                AdicSincFaixa1 = 0.00m,
                AdicSincFaixa2 = 10.00m,
                AdicSincFaixa3 = 20.00m,
                AdicVolumeBaixo = 0.00m,
                AdicVolumeMedio = 10.00m,
                AdicVolumeAlto = 25.00m,
                AdicModuloBasico = 0.00m,
                AdicModuloRelatorios = 15.00m,
                AdicModuloCompleto = 30.00m,
                AtualizadoPor = "Administrador Geral",
                MotivoAlteracao = "Tabela oficial de precificação dinâmica inicial (R$ 50,00 a R$ 250,00)"
            };
            await context.ConfiguracoesPrecificacao.AddAsync(cfgPrecificacao);
            try { await context.SaveChangesAsync(); } catch { }
        }

        // 4. Garantir Slug único para todas as empresas existentes
        try
        {
            var tenantsSemSlug = await context.Tenants.IgnoreQueryFilters()
                .Where(t => string.IsNullOrEmpty(t.Slug))
                .ToListAsync();
            if (tenantsSemSlug.Count > 0)
            {
                var slugsUsados = (await context.Tenants.IgnoreQueryFilters()
                    .Where(t => !string.IsNullOrEmpty(t.Slug))
                    .Select(t => t.Slug)
                    .ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var t in tenantsSemSlug)
                {
                    var baseSlug = Nexo.Api.Services.SlugHelper.NormalizeSlug(!string.IsNullOrWhiteSpace(t.NomeFantasia) ? t.NomeFantasia : t.RazaoSocial);
                    if (string.IsNullOrWhiteSpace(baseSlug) || Nexo.Api.Services.SlugHelper.IsReservedSlug(baseSlug))
                        baseSlug = $"empresa-{t.Id.ToString("N")[..6]}";

                    var candidate = baseSlug;
                    int suffix = 2;
                    while (slugsUsados.Contains(candidate))
                    {
                        candidate = $"{baseSlug}-{suffix++}";
                    }
                    t.Slug = candidate;
                    slugsUsados.Add(candidate);
                }
                await context.SaveChangesAsync();
            }
        }
        catch { }
    }
}

