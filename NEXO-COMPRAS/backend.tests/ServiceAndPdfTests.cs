using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Nexo.Api.Controllers;
using Nexo.Api.DTOs;
using Nexo.Api.Models;
using Nexo.Api.Services;
using Xunit;

namespace Nexo.Api.Tests;

public class ServiceAndPdfTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ServiceAndPdfTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> RegistrarTenantAsync(string prefixo)
    {
        var randomNum = Random.Shared.Next(10000000, 99999999);
        var cnpjGerado = $"{randomNum:D8}0001{Random.Shared.Next(10, 99):D2}";

        var req = new RegisterTenantRequest(
            RazaoSocial: $"{prefixo} SERVICOS LTDA",
            NomeFantasia: $"{prefixo} Matriz",
            Cnpj: cnpjGerado,
            Email: $"contato@{prefixo.ToLower()}{Random.Shared.Next(100, 999)}.com.br",
            Telefone: "(47) 3456-0000",
            AdminNome: $"Diretor {prefixo}",
            AdminEmail: $"diretor@{prefixo.ToLower()}{Random.Shared.Next(1000, 9999)}.com.br",
            AdminSenha: "SenhaForte@2026",
            PlanoId: null
        );

        var res = await _client.PostAsJsonAsync("/api/auth/register-tenant", req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var login = await res.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);
        return login.Token;
    }

    [Fact]
    public void PdfDocumentService_Deve_Gerar_Html_Com_Estrutura_Fiel_Ao_Modelo_ProSyst()
    {
        var tenant = new Tenant
        {
            RazaoSocial = "EMPRESA MODELO LTDA",
            NomeFantasia = "EMPRESA MODELO",
            Cnpj = "06170038000199"
        };

        var fornecedor = new Fornecedor
        {
            RazaoSocial = "SUPERMERCADO NOVA ESPERANCA LTDA",
            Contato = "ED",
            Endereco = "RUA OLAVO BILAC, 91",
            Cidade = "JOINVILLE",
            Uf = "SC",
            Cnpj = "00252713000142",
            Telefone = "(47) 3424-0282",
            CodigoErp = "10-4"
        };

        var pedido = new PedidoCompra
        {
            Numero = "PED-5296",
            DataCriacao = new DateTime(2026, 10, 4),
            PrevisaoEntrega = new DateTime(2026, 10, 24),
            CondicaoPagamento = "002 - 28 DDL.",
            Frete = 5m,
            Subtotal = 100m,
            ValorTotal = 105m,
            Fornecedor = fornecedor,
            AprovadoPorNome = "GUILHERME",
            Itens = new List<PedidoCompraItem>
            {
                new PedidoCompraItem
                {
                    CodigoProduto = "39115",
                    DescricaoProduto = "ACUCAR CRISTAL",
                    Unidade = "UN",
                    Quantidade = 10m,
                    PrecoUnitario = 10m,
                    Subtotal = 100m
                }
            }
        };

        var html = PdfDocumentService.GerarHtmlPedido(pedido, tenant);

        Assert.Contains("Pedido de Compra", html);
        Assert.Contains("5296", html);
        Assert.Contains("EMPRESA MODELO", html);
        Assert.Contains("SUPERMERCADO NOVA ESPERANCA LTDA", html);
        Assert.Contains("39115", html);
        Assert.Contains("ACUCAR CRISTAL", html);
        Assert.Contains("Valor Mercad.:", html);
        Assert.Contains("Total Pedido:", html);
        Assert.Contains("105,00", html);
        Assert.Contains("Comprador Responsável", html);
        Assert.Contains("Aceite do Fornecedor", html);
    }

    [Fact]
    public async Task Ciclo_Completo_Cotacao_De_Servicos_E_Contratacao()
    {
        var tokenTenantA = await RegistrarTenantAsync("ConstrutoraAlpha");

        // 1. Cadastrar um Fornecedor de Serviços
        var reqForn = new HttpRequestMessage(HttpMethod.Post, "/api/fornecedores");
        reqForn.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenTenantA);
        reqForn.Content = JsonContent.Create(new
        {
            RazaoSocial = "Eletro Instalações Pro LTDA",
            NomeFantasia = "Eletro Pro",
            Cnpj = $"{Random.Shared.Next(10000000, 99999999):D8}000199",
            Email = "comercial@eletropro.com.br",
            Telefone = "(47) 3434-1111",
            Contato = "Carlos Engenheiro"
        });
        var resForn = await _client.SendAsync(reqForn);
        Assert.True(resForn.StatusCode == HttpStatusCode.OK || resForn.StatusCode == HttpStatusCode.Created);
        var jsonForn = await resForn.Content.ReadFromJsonAsync<JsonElement>();
        var fornecedorId = jsonForn.GetProperty("id").GetGuid();

        // 2. Criar Cotação de Serviço com Escopo e Atividades
        var reqCriar = new HttpRequestMessage(HttpMethod.Post, "/api/servicos");
        reqCriar.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenTenantA);
        reqCriar.Content = JsonContent.Create(new CriarCotacaoServicoRequest(
            Titulo: "Reforma Elétrica e Cabeamento Estruturado do Galpão 03",
            Categoria: "ManutencaoPredial",
            Prioridade: "Alta",
            FilialId: null,
            DataLimiteResposta: DateTime.UtcNow.AddDays(5),
            DataDesejadaInicio: DateTime.UtcNow.AddDays(10),
            PrazoEstimadoDias: 20,
            NecessidadeMotivacao: "Adequação às normas NR-10 e ampliação da capacidade de TI",
            DescricaoDetalhada: "Substituição completa do quadro elétrico, passagem de eletrocalhas e 48 pontos de rede Cat6",
            ObjetivoEsperado: "Certificação e entrega de memorial as-built",
            ResultadoEsperado: "Rede em funcionamento e laudo assinado",
            AtendimentoPresencial: true,
            Cep: "89216-200",
            Endereco: "Rua das Indústrias",
            NumeroLocal: "1000",
            Complemento: "Galpão 03",
            Bairro: "Distrito Industrial",
            Cidade: "Joinville",
            Uf: "SC",
            PontoReferencia: "Próximo à BR-101",
            HorarioPermitido: "Segunda a sexta das 07h às 18h",
            MateriaisInclusosExigidos: "Todos os materiais e cabos homologados",
            EquipamentosExigidos: "Andaimes, EPIs completos e certificador de cabos",
            GarantiaEsperadaMeses: 24,
            CondicoesEspeciais: "Exigência de ART de execução assinada por engenheiro",
            CertificacoesExigidas: "NR-10, NR-35",
            PesoPreco: 50,
            PesoPrazo: 20,
            PesoTecnico: 20,
            PesoCondicoes: 10,
            Atividades: new List<AtividadeServicoDto>
            {
                new AtividadeServicoDto(
                    Etapa: "Fase 1",
                    Nome: "Desmontagem do quadro antigo",
                    Descricao: "Desconexão e remoção segura",
                    Quantidade: 1,
                    UnidadeMedicao: "SV",
                    LocalExecucao: "Galpão 03",
                    RequisitosTecnicos: "Bloqueio e etiquetagem LOTO",
                    ResultadoEsperado: "Área limpa e desenergizada",
                    ValorEstimadoUnitario: 3000m
                ),
                new AtividadeServicoDto(
                    Etapa: "Fase 2",
                    Nome: "Montagem do novo QGBT e cabeamento Cat6",
                    Descricao: "Passagem de 1200m de cabos e conectorização",
                    Quantidade: 48,
                    UnidadeMedicao: "Pontos",
                    LocalExecucao: "Galpão 03",
                    RequisitosTecnicos: "Certificação Fluke",
                    ResultadoEsperado: "Pontos testados 100% aprovados",
                    ValorEstimadoUnitario: 250m
                )
            },
            FornecedoresIds: new List<Guid> { fornecedorId }
        ));

        var resCriar = await _client.SendAsync(reqCriar);
        if (resCriar.StatusCode != HttpStatusCode.Created)
        {
            var err = await resCriar.Content.ReadAsStringAsync();
            throw new Exception($"Falha ao criar servico: {resCriar.StatusCode} - {err}");
        }
        var jsonCriar = await resCriar.Content.ReadFromJsonAsync<JsonElement>();
        var cotacaoId = jsonCriar.GetProperty("id").GetGuid();

        // 3. Enviar Cotação aos Fornecedores
        var reqEnviar = new HttpRequestMessage(HttpMethod.Post, $"/api/servicos/{cotacaoId}/enviar");
        reqEnviar.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenTenantA);
        var resEnviar = await _client.SendAsync(reqEnviar);
        if (resEnviar.StatusCode != HttpStatusCode.OK)
        {
            var err = await resEnviar.Content.ReadAsStringAsync();
            throw new Exception($"Falha ao enviar servico: {resEnviar.StatusCode} - {err}");
        }

        // Obter detalhes da cotação com o token gerado para o portal do fornecedor
        var reqGet = new HttpRequestMessage(HttpMethod.Get, $"/api/servicos/{cotacaoId}");
        reqGet.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenTenantA);
        var resGet = await _client.SendAsync(reqGet);
        if (resGet.StatusCode != HttpStatusCode.OK)
        {
            var err = await resGet.Content.ReadAsStringAsync();
            throw new Exception($"Falha ao obter servico: {resGet.StatusCode} - {err}");
        }
        var jsonDetalhe = await resGet.Content.ReadFromJsonAsync<JsonElement>();
        var fornArr = jsonDetalhe.GetProperty("fornecedores").EnumerateArray().ToList();
        Assert.Single(fornArr);
        var linkAcesso = fornArr[0].GetProperty("linkAcesso").GetString()!;
        var tokenAcesso = linkAcesso.Split('/').Last();
        var fornecedorServicoId = fornArr[0].GetProperty("id").GetGuid();

        // 4. Portal Público do Fornecedor de Serviços: Validar Token e Consultar Detalhes
        var resPortalValidar = await _client.GetAsync($"/api/portal-servico/{tokenAcesso}/validar");
        Assert.Equal(HttpStatusCode.OK, resPortalValidar.StatusCode);

        var resPortalDetalhes = await _client.GetAsync($"/api/portal-servico/{tokenAcesso}/detalhes");
        Assert.Equal(HttpStatusCode.OK, resPortalDetalhes.StatusCode);
        var jsonPortal = await resPortalDetalhes.Content.ReadFromJsonAsync<JsonElement>();
        var atividadesPortal = jsonPortal.GetProperty("cotacao").GetProperty("atividades").EnumerateArray().ToList();
        Assert.Equal(2, atividadesPortal.Count);
        var ativ1Id = atividadesPortal[0].GetProperty("id").GetGuid();
        var ativ2Id = atividadesPortal[1].GetProperty("id").GetGuid();

        // 5. Fornecedor envia Proposta Comercial e Técnica
        var reqProposta = new SalvarPropostaServicoRequest(
            ValorMaoDeObra: 10000m,
            ValorMateriaisInclusos: 4000m,
            ValorEquipamentos: 500m,
            Deslocamento: 300m,
            OutrasDespesas: 200m,
            Desconto: 1000m,
            ImpostosTotal: 1000m,
            ValorTotal: 15000m,
            PrazoInicioDias: 3,
            PrazoExecucaoDias: 15,
            CondicoesPagamento: "50% entrada, 50% medição final",
            ValidadeProposta: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            GarantiaMeses: 24,
            DescricaoSolucao: "Equipe de 4 técnicos liderados por eng. elétrico",
            Metodologia: "Instalação por trechos com minimização de paradas",
            EquipeAlocada: "1 Engenheiro, 2 Eletricistas, 1 Cabista",
            EquipamentosFerramental: "Certificador Fluke DSX-5000, Andaimes com travas",
            MateriaisInclusosTexto: "Cabos Furukawa Cat6, Disjuntores Schneider",
            MateriaisNaoInclusosTexto: "Alvenaria pesada externa",
            CertificacoesEquipe: "NR-10, NR-35 e Furukawa Certified",
            LimitacoesExclusoes: "Disponibilidade de energia provisória por conta do cliente",
            Observacoes: "Valores com emissão de NF de serviço",
            ContatoNome: "Carlos Engenheiro",
            ContatoTelefone: "(47) 99999-1234",
            ContatoEmail: "carlos@eletropro.com.br",
            FinalizarEnvio: true,
            Etapas: new List<EtapaPropostaDto>
            {
                new EtapaPropostaDto(ativ1Id, 2800m, 2800m, "Remoção em 1 dia útil", ""),
                new EtapaPropostaDto(ativ2Id, 250m, 12000m, "Execução e certificação Fluke", "")
            }
        );

        var resSalvarProp = await _client.PostAsJsonAsync($"/api/portal-servico/{tokenAcesso}/proposta", reqProposta);
        Assert.Equal(HttpStatusCode.OK, resSalvarProp.StatusCode);

        // 6. Tenant Buyer Acessa a Comparação Técnica e Comercial
        var reqComp = new HttpRequestMessage(HttpMethod.Get, $"/api/servicos/{cotacaoId}/comparacao");
        reqComp.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenTenantA);
        var resComp = await _client.SendAsync(reqComp);
        Assert.Equal(HttpStatusCode.OK, resComp.StatusCode);
        var jsonComp = await resComp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(jsonComp.GetProperty("temPropostas").GetBoolean());
        var propostasArr = jsonComp.GetProperty("propostas").EnumerateArray().ToList();
        Assert.Single(propostasArr);
        var propObj = propostasArr[0];
        Assert.True(propObj.GetProperty("pontuacaoPonderada").GetDecimal() > 0);

        // 7. Avaliar Proposta com Notas Técnicas
        var propostaId = propObj.GetProperty("propostaId").GetGuid();
        var reqAval = new HttpRequestMessage(HttpMethod.Post, $"/api/servicos/{cotacaoId}/avaliar-proposta/{propostaId}");
        reqAval.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenTenantA);
        reqAval.Content = JsonContent.Create(new AvaliarPropostaRequest(95m, 90m, "Excelente capacitação técnica"));
        var resAval = await _client.SendAsync(reqAval);
        Assert.Equal(HttpStatusCode.OK, resAval.StatusCode);

        // 8. Aprovar Cotação e Gerar Ordem de Serviço
        var reqAprov = new HttpRequestMessage(HttpMethod.Post, $"/api/servicos/{cotacaoId}/aprovar");
        reqAprov.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenTenantA);
        reqAprov.Content = JsonContent.Create(new AprovarCotacaoServicoRequest(
            FornecedorServicoId: fornecedorServicoId,
            FornecedorId: fornecedorId,
            Justificativa: "Melhor pontuação técnica comprovada e preço competitivo",
            Observacao: "Início aprovado para a próxima segunda-feira"
        ));
        var resAprov = await _client.SendAsync(reqAprov);
        Assert.Equal(HttpStatusCode.OK, resAprov.StatusCode);
        var jsonAprov = await resAprov.Content.ReadFromJsonAsync<JsonElement>();
        var ordemServicoId = jsonAprov.GetProperty("ordemServicoId").GetGuid();
        Assert.NotEmpty(jsonAprov.GetProperty("ordemServicoNumero").GetString()!);

        // 9. Validar Geração do PDF / Impressão da Ordem de Serviço
        var resPdfOs = await _client.GetAsync($"/api/servicos/ordens/{ordemServicoId}/pdf");
        Assert.Equal(HttpStatusCode.OK, resPdfOs.StatusCode);
        var htmlOs = await resPdfOs.Content.ReadAsStringAsync();
        Assert.Contains("Ordem de Serviço", htmlOs);
        Assert.Contains("ELETRO INSTALAÇÕES PRO LTDA", htmlOs);
        Assert.Contains("Total Pedido:", htmlOs);

        // 10. Validar Relatório de Serviços com os novos dados
        var reqRel = new HttpRequestMessage(HttpMethod.Get, "/api/relatorios/servicos");
        reqRel.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenTenantA);
        var resRel = await _client.SendAsync(reqRel);
        Assert.Equal(HttpStatusCode.OK, resRel.StatusCode);
        var jsonRel = await resRel.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(jsonRel.GetProperty("kpis").GetProperty("total").GetInt32() >= 1);
        Assert.True(jsonRel.GetProperty("kpis").GetProperty("contratadas").GetInt32() >= 1);

        // 11. Testar Isolamento Multi-Tenancy: Tenant B não pode acessar cotação de Tenant A
        var tokenTenantB = await RegistrarTenantAsync("ComercialBeta");
        var reqTenantB = new HttpRequestMessage(HttpMethod.Get, $"/api/servicos/{cotacaoId}");
        reqTenantB.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenTenantB);
        var resTenantB = await _client.SendAsync(reqTenantB);
        Assert.Equal(HttpStatusCode.NotFound, resTenantB.StatusCode);
    }

    [Fact]
    public void Documentos_Sem_Logomarca_Devem_Ter_Espaco_Completamente_Vazio()
    {
        var tenantSemLogo = new Tenant
        {
            RazaoSocial = "EMPRESA TRANSPARENTE SEM LOGO S.A.",
            NomeFantasia = "TRANSPARENTE",
            Cnpj = "11222333000199",
            LogoUrl = "",
            LogoStoragePath = ""
        };

        var fornecedor = new Fornecedor
        {
            RazaoSocial = "FORNECEDOR DE TESTE",
            Cnpj = "44555666000188"
        };

        var pedido = new PedidoCompra
        {
            Numero = "PED-9999",
            DataCriacao = DateTime.UtcNow,
            Fornecedor = fornecedor,
            ValorTotal = 500m
        };

        var cotacaoServico = new CotacaoServico
        {
            Numero = "SRV-2026-0001",
            Titulo = "Serviço de Manutenção Predial",
            Categoria = "ManutencaoPredial",
            DataAbertura = DateTime.UtcNow,
            DataLimiteResposta = DateTime.UtcNow.AddDays(7),
            Atividades = new List<AtividadeServico>
            {
                new AtividadeServico { Ordem = 1, Nome = "Etapa 1", Quantidade = 1, ValorEstimadoUnitario = 500m }
            }
        };

        var htmlPedido = PdfDocumentService.GerarHtmlPedido(pedido, tenantSemLogo);
        var htmlServico = PdfDocumentService.GerarHtmlCotacaoServico(cotacaoServico, tenantSemLogo);

        // Regra Seção 2.4:
        // Não deve ter placeholder de logo, nem "Indústria por projeto", nem "Sua logo", nem célula de imagem
        Assert.DoesNotContain("Indústria por projeto", htmlPedido);
        Assert.DoesNotContain("Sua logo", htmlPedido);
        Assert.DoesNotContain("Logo não cadastrada", htmlPedido);
        Assert.DoesNotContain("<td class=\"header-logo-cell\">", htmlPedido);
        Assert.Contains("width: 78%", htmlPedido); // Ajuste automático de layout para 78%

        Assert.DoesNotContain("Indústria por projeto", htmlServico);
        Assert.DoesNotContain("Sua logo", htmlServico);
        Assert.DoesNotContain("<td class=\"header-logo-cell\">", htmlServico);
        Assert.Contains("width: 78%", htmlServico);
    }

    [Fact]
    public void Documentos_Com_Logomarca_Devem_Renderizar_Tag_Img_Com_Aspect_Ratio()
    {
        var tenantComLogo = new Tenant
        {
            RazaoSocial = "EMPRESA INDUSTRIAL ALPHA S.A.",
            NomeFantasia = "ALPHA INDÚSTRIA",
            Cnpj = "99888777000155",
            LogoUrl = "/api/tenant/logo?v=123456"
        };

        var pedido = new PedidoCompra
        {
            Numero = "PED-8888",
            DataCriacao = DateTime.UtcNow,
            ValorTotal = 1500m
        };

        var html = PdfDocumentService.GerarHtmlPedido(pedido, tenantComLogo);

        Assert.Contains("<td class=\"header-logo-cell\">", html);
        Assert.Contains("<img src=\"/api/tenant/logo?v=123456\"", html);
        Assert.Contains("object-fit: contain", html);
        Assert.Contains("max-width: 140px", html);
        Assert.Contains("max-height: 65px", html);
        Assert.Contains("width: 58%", html); // Ajuste automático de layout para 58% quando há logo
    }
}
