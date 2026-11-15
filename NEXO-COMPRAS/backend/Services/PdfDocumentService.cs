using System.Globalization;
using System.Text;
using Nexo.Api.Models;

namespace Nexo.Api.Services;

/// <summary>
/// DTO com os dados cadastrais da empresa centralizados para impressão em documentos e relatórios.
/// </summary>
public class DadosEmpresaDocumento
{
    public string RazaoSocial { get; set; } = string.Empty;
    public string NomeFantasia { get; set; } = string.Empty;
    public string CnpjFormatado { get; set; } = string.Empty;
    public string InscricaoEstadual { get; set; } = string.Empty;
    public string InscricaoMunicipal { get; set; } = string.Empty;
    public string EnderecoLinha1 { get; set; } = string.Empty;
    public string EnderecoLinha2 { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string LogoCellHtml { get; set; } = string.Empty;
}

/// <summary>
/// Serviço centralizado de geração de documentos e relatórios PDF/HTML profissionais.
/// Carrega e renderiza dinamicamente os dados reais do Tenant autenticado (sem dados fictícios/demo).
/// Se um campo não estiver preenchido, deixa em branco/omitido.
/// Se a empresa não tiver logomarca cadastrada, o espaço da logo fica completamente vazio.
/// </summary>
public static class PdfDocumentService
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public static string H(string? text) => System.Net.WebUtility.HtmlEncode(text ?? "");

    public static DadosEmpresaDocumento ObterDadosEmpresa(Tenant? tenant, Filial? filial = null)
    {
        var dados = new DadosEmpresaDocumento();
        if (tenant == null) return dados;

        dados.RazaoSocial = !string.IsNullOrWhiteSpace(tenant.RazaoSocial) ? tenant.RazaoSocial.Trim() : "";
        dados.NomeFantasia = !string.IsNullOrWhiteSpace(tenant.NomeFantasia) ? tenant.NomeFantasia.Trim() : dados.RazaoSocial;
        dados.CnpjFormatado = FormatCnpj(tenant.Cnpj);
        dados.InscricaoEstadual = tenant.InscricaoEstadual?.Trim() ?? "";
        dados.InscricaoMunicipal = tenant.InscricaoMunicipal?.Trim() ?? "";

        // Endereço linha 1: Logradouro + Número + Complemento + Bairro
        var endPartes = new List<string>();
        if (!string.IsNullOrWhiteSpace(tenant.Endereco))
        {
            var log = tenant.Endereco.Trim();
            if (!string.IsNullOrWhiteSpace(tenant.Numero)) log += $", Nº {tenant.Numero.Trim()}";
            if (!string.IsNullOrWhiteSpace(tenant.Complemento)) log += $" - {tenant.Complemento.Trim()}";
            endPartes.Add(log);
        }
        if (!string.IsNullOrWhiteSpace(tenant.Bairro)) endPartes.Add(tenant.Bairro.Trim());
        dados.EnderecoLinha1 = string.Join(" - ", endPartes);

        // Linha 2: CEP + Cidade + UF
        var cidPartes = new List<string>();
        if (!string.IsNullOrWhiteSpace(tenant.Cep)) cidPartes.Add(tenant.Cep.Trim());
        if (!string.IsNullOrWhiteSpace(tenant.Cidade)) cidPartes.Add(tenant.Cidade.Trim());
        if (!string.IsNullOrWhiteSpace(tenant.Uf)) cidPartes.Add(tenant.Uf.Trim().ToUpperInvariant());
        dados.EnderecoLinha2 = string.Join(" - ", cidPartes);

        dados.Telefone = !string.IsNullOrWhiteSpace(filial?.Telefone) ? filial.Telefone.Trim() : (tenant.Telefone?.Trim() ?? "");
        dados.Email = !string.IsNullOrWhiteSpace(filial?.Email) ? filial.Email.Trim() : (tenant.Email?.Trim() ?? "");
        dados.LogoCellHtml = ResolveLogoHtml(tenant);

        return dados;
    }

    private static string ResolveLogoHtml(Tenant? tenant)
    {
        if (tenant == null) return string.Empty;

        string? imgSrc = null;

        if (!string.IsNullOrWhiteSpace(tenant.LogoStoragePath))
        {
            var candidates = new[]
            {
                tenant.LogoStoragePath,
                Path.Combine(AppContext.BaseDirectory, "storage", tenant.LogoStoragePath),
                Path.Combine(Directory.GetCurrentDirectory(), "storage", tenant.LogoStoragePath),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "backend", "storage", tenant.LogoStoragePath),
                Path.Combine("c:\\Users\\sergi\\OneDrive\\Desktop\\pedido_compras\\NEXO-COMPRAS\\backend\\storage", tenant.LogoStoragePath)
            };

            foreach (var cand in candidates)
            {
                try
                {
                    if (File.Exists(cand))
                    {
                        var bytes = File.ReadAllBytes(cand);
                        var mime = !string.IsNullOrWhiteSpace(tenant.LogoMimeType) ? tenant.LogoMimeType : "image/png";
                        imgSrc = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
                        break;
                    }
                }
                catch { }
            }
        }

        if (string.IsNullOrWhiteSpace(imgSrc) && !string.IsNullOrWhiteSpace(tenant.LogoUrl))
        {
            imgSrc = tenant.LogoUrl;
        }

        if (string.IsNullOrWhiteSpace(imgSrc))
        {
            // Espaço destinado à imagem completamente vazio quando não existir logo cadastrada
            return string.Empty;
        }

        return $@"<td class=""header-logo-cell"">
            <img src=""{imgSrc}"" alt=""Logo"" style=""max-width: 140px; max-height: 65px; width: auto; height: auto; object-fit: contain; display: block;"" />
        </td>";
    }

    public static string GerarHtmlPedido(PedidoCompra p, Tenant? tenant, Usuario? comprador = null)
    {
        var dadosEmpresa = ObterDadosEmpresa(tenant, p.Filial);
        var nomeComprador = !string.IsNullOrWhiteSpace(p.AprovadoPorNome)
            ? p.AprovadoPorNome.Trim().ToUpper()
            : (comprador != null ? comprador.Nome.Trim().ToUpper() : "");

        var fornecedor = p.Fornecedor;
        var fornNome = (fornecedor?.RazaoSocial ?? fornecedor?.NomeFantasia ?? "").Trim().ToUpper();
        var fornVendedor = (!string.IsNullOrWhiteSpace(fornecedor?.Contato) ? fornecedor.Contato.Trim() : "").ToUpper();
        
        var fornEndPartes = new List<string>();
        if (!string.IsNullOrWhiteSpace(fornecedor?.Endereco)) fornEndPartes.Add(fornecedor.Endereco.Trim());
        if (!string.IsNullOrWhiteSpace(fornecedor?.Cidade)) fornEndPartes.Add(fornecedor.Cidade.Trim());
        if (!string.IsNullOrWhiteSpace(fornecedor?.Uf)) fornEndPartes.Add(fornecedor.Uf.Trim().ToUpperInvariant());
        var fornEndereco = string.Join(" - ", fornEndPartes).ToUpper();

        var fornCnpj = FormatCnpj(fornecedor?.Cnpj);
        var fornFone = fornecedor?.Telefone?.Trim() ?? "";
        var fornCodigo = fornecedor?.CodigoErp?.Trim() ?? "";
        var fornCelular = fornecedor?.Whatsapp?.Trim() ?? "";
        var fornIe = fornecedor?.InscricaoEstadual?.Trim() ?? "";

        var condPagto = !string.IsNullOrWhiteSpace(p.CondicaoPagamento) ? p.CondicaoPagamento : "";
        var freteTipo = p.Frete > 0 ? "A Pagar (FOB)" : "Pago (CIF)";
        var dataCadastro = p.DataCriacao.ToString("dd/MM/yy", PtBr);
        var dataEntrega = p.PrevisaoEntrega.HasValue
            ? p.PrevisaoEntrega.Value.ToString("dd/MM/yy", PtBr)
            : (p.PrazoEntregaDias > 0 ? p.DataCriacao.AddDays(p.PrazoEntregaDias).ToString("dd/MM/yy", PtBr) : "");
        var mesAtual = DateTime.UtcNow.ToString("MMMM", PtBr);
        mesAtual = char.ToUpper(mesAtual[0]) + mesAtual[1..];

        var sbItens = new StringBuilder();
        int idx = 1;
        foreach (var it in p.Itens)
        {
            var cod = !string.IsNullOrWhiteSpace(it.CodigoProduto) ? it.CodigoProduto : idx.ToString();
            var desc = (it.DescricaoProduto ?? "ITEM").ToUpper();
            var um = !string.IsNullOrWhiteSpace(it.Unidade) ? it.Unidade.ToUpper() : "UN";
            var saldoEst = it.Produto?.EstoqueAtual ?? 0m;
            var demMes = it.Produto?.Consumo90Dias > 0 ? (it.Produto.Consumo90Dias / 3m) : 0m;
            var demMedia = it.Produto?.Consumo90Dias > 0 ? (it.Produto.Consumo90Dias / 3m) : 0m;
            var prUltCompra = it.Produto?.UltimoCusto ?? 0m;
            var codUf = "0,00";

            sbItens.Append($@"
            <tr>
                <td class=""c-center"">{cod}</td>
                <td class=""c-left"">{desc}</td>
                <td class=""c-center"">{um}</td>
                <td class=""c-right"">{saldoEst.ToString("N2", PtBr)}</td>
                <td class=""c-right"">{demMes.ToString("N2", PtBr)}</td>
                <td class=""c-right"">{demMedia.ToString("N2", PtBr)}</td>
                <td class=""c-right"">{prUltCompra.ToString("N2", PtBr)}</td>
                <td class=""c-center"">{codUf}</td>
                <td class=""c-right"">{it.Quantidade.ToString("N4", PtBr)}</td>
                <td class=""c-right"">{it.PrecoUnitario.ToString("N4", PtBr)}</td>
                <td class=""c-right"">{it.Subtotal.ToString("N2", PtBr)}</td>
            </tr>");
            idx++;
        }

        var valorMercadorias = p.Subtotal > 0 ? p.Subtotal : p.ValorTotal;
        var totalPedido = p.ValorTotal;
        var valorFrete = p.Frete;
        var despAcessorias = p.OutrasDespesas;

        return BuildDocumentHtml(
            tipoDocumento: "Pedido de Compra",
            numeroDoc: p.Numero.Replace("PED-", "").Replace("PED", ""),
            dataCadastro: dataCadastro,
            dataLimiteEntrega: dataEntrega,
            labelDataEntrega: "Data Entrega:",
            empresa: dadosEmpresa,
            nomeComprador: nomeComprador,
            fornNome: fornNome,
            fornVendedor: fornVendedor,
            fornEndereco: fornEndereco,
            fornCnpj: fornCnpj,
            fornFone: fornFone,
            fornCodigo: fornCodigo,
            fornCelular: fornCelular,
            fornIe: fornIe,
            condPagto: condPagto,
            freteTipo: freteTipo,
            transportadora: "-",
            propostaRef: p.Cotacao?.Numero ?? "-",
            mesAtual: mesAtual,
            itensHtml: sbItens.ToString(),
            valorMercadorias: valorMercadorias,
            totalGeral: totalPedido,
            valorFrete: valorFrete,
            despAcessorias: despAcessorias,
            custoFinanceiro: 0m,
            observacoes: p.Observacoes
        );
    }

    public static string GerarHtmlCotacao(Cotacao c, Tenant? tenant, Usuario? comprador = null, CotacaoFornecedor? cf = null)
    {
        var dadosEmpresa = ObterDadosEmpresa(tenant, null);
        var nomeComprador = !string.IsNullOrWhiteSpace(c.AprovadaPorNome)
            ? c.AprovadaPorNome.Trim().ToUpper()
            : (comprador != null ? comprador.Nome.Trim().ToUpper() : "");

        var forn = cf?.Fornecedor;
        var fornNome = (forn?.RazaoSocial ?? forn?.NomeFantasia ?? "TODOS OS FORNECEDORES CONVIDADOS").Trim().ToUpper();
        var fornVendedor = (!string.IsNullOrWhiteSpace(forn?.Contato) ? forn.Contato.Trim() : "").ToUpper();
        
        var fornEndPartes = new List<string>();
        if (!string.IsNullOrWhiteSpace(forn?.Endereco)) fornEndPartes.Add(forn.Endereco.Trim());
        if (!string.IsNullOrWhiteSpace(forn?.Cidade)) fornEndPartes.Add(forn.Cidade.Trim());
        if (!string.IsNullOrWhiteSpace(forn?.Uf)) fornEndPartes.Add(forn.Uf.Trim().ToUpperInvariant());
        var fornEndereco = string.Join(" - ", fornEndPartes).ToUpper();

        var fornCnpj = FormatCnpj(forn?.Cnpj);
        var fornFone = forn?.Telefone?.Trim() ?? "";
        var fornCodigo = forn?.CodigoErp?.Trim() ?? "";
        var fornCelular = forn?.Whatsapp?.Trim() ?? "";
        var fornIe = forn?.InscricaoEstadual?.Trim() ?? "";

        var condPagto = cf?.Resposta?.CondicaoPagamento ?? "A combinar";
        var freteTipo = (cf?.Resposta?.Frete ?? 0) > 0 ? "A Pagar (FOB)" : "Pago (CIF)";
        var dataCadastro = c.DataCriacao.ToString("dd/MM/yy", PtBr);
        var dataLimite = c.DataLimiteResposta.ToString("dd/MM/yy", PtBr);
        var mesAtual = DateTime.UtcNow.ToString("MMMM", PtBr);
        mesAtual = char.ToUpper(mesAtual[0]) + mesAtual[1..];

        var sbItens = new StringBuilder();
        decimal totalMercad = 0m;
        foreach (var it in c.Itens)
        {
            var cod = !string.IsNullOrWhiteSpace(it.Codigo) ? it.Codigo : "ITEM";
            var desc = (it.Descricao ?? "PRODUTO").ToUpper();
            var um = !string.IsNullOrWhiteSpace(it.Unidade) ? it.Unidade.ToUpper() : "UN";
            var saldoEst = it.EstoqueNoMomento;
            var demMes = 0m;
            var demMedia = 0m;
            var prUltCompra = it.UltimoCustoRef;

            decimal precoUnit = 0m;
            if (cf?.Resposta != null)
            {
                var respItem = cf.Resposta.Itens.FirstOrDefault(ri => ri.CotacaoItemId == it.Id);
                if (respItem != null) precoUnit = respItem.PrecoUnitario;
            }
            if (precoUnit == 0) precoUnit = it.PrecoVendaRef > 0 ? it.PrecoVendaRef : it.UltimoCustoRef;

            var subtotalItem = it.Quantidade * precoUnit;
            totalMercad += subtotalItem;

            sbItens.Append($@"
            <tr>
                <td class=""c-center"">{cod}</td>
                <td class=""c-left"">{desc}</td>
                <td class=""c-center"">{um}</td>
                <td class=""c-right"">{saldoEst.ToString("N2", PtBr)}</td>
                <td class=""c-right"">{demMes.ToString("N2", PtBr)}</td>
                <td class=""c-right"">{demMedia.ToString("N2", PtBr)}</td>
                <td class=""c-right"">{prUltCompra.ToString("N2", PtBr)}</td>
                <td class=""c-center"">0,00</td>
                <td class=""c-right"">{it.Quantidade.ToString("N4", PtBr)}</td>
                <td class=""c-right"">{precoUnit.ToString("N4", PtBr)}</td>
                <td class=""c-right"">{subtotalItem.ToString("N2", PtBr)}</td>
            </tr>");
        }

        var totalGeral = totalMercad + (cf?.Resposta?.Frete ?? 0) + (cf?.Resposta?.OutrasDespesas ?? 0);

        return BuildDocumentHtml(
            tipoDocumento: "Orçamento de Compra",
            numeroDoc: c.Numero.Replace("COT-", "").Replace("COT", ""),
            dataCadastro: dataCadastro,
            dataLimiteEntrega: dataLimite,
            labelDataEntrega: "Data Limite:",
            empresa: dadosEmpresa,
            nomeComprador: nomeComprador,
            fornNome: fornNome,
            fornVendedor: fornVendedor,
            fornEndereco: fornEndereco,
            fornCnpj: fornCnpj,
            fornFone: fornFone,
            fornCodigo: fornCodigo,
            fornCelular: fornCelular,
            fornIe: fornIe,
            condPagto: condPagto,
            freteTipo: freteTipo,
            transportadora: "-",
            propostaRef: c.Titulo,
            mesAtual: mesAtual,
            itensHtml: sbItens.ToString(),
            valorMercadorias: totalMercad,
            totalGeral: totalGeral,
            valorFrete: cf?.Resposta?.Frete ?? 0m,
            despAcessorias: cf?.Resposta?.OutrasDespesas ?? 0m,
            custoFinanceiro: 0m,
            observacoes: c.Observacoes
        );
    }

    public static string GerarHtmlCotacaoServico(CotacaoServico cs, Tenant? tenant, Usuario? comprador = null)
    {
        var dadosEmpresa = ObterDadosEmpresa(tenant, null);
        var nomeComprador = !string.IsNullOrWhiteSpace(cs.ResponsavelNome)
            ? cs.ResponsavelNome.Trim().ToUpper()
            : (comprador != null ? comprador.Nome.Trim().ToUpper() : "");

        var dataCadastro = cs.DataAbertura.ToString("dd/MM/yy", PtBr);
        var dataLimite = cs.DataLimiteResposta.ToString("dd/MM/yy", PtBr);

        var locPartes = new List<string>();
        if (!string.IsNullOrWhiteSpace(cs.Endereco)) locPartes.Add(cs.Endereco.Trim());
        if (!string.IsNullOrWhiteSpace(cs.NumeroLocal)) locPartes.Add($"Nº {cs.NumeroLocal.Trim()}");
        if (!string.IsNullOrWhiteSpace(cs.Complemento)) locPartes.Add(cs.Complemento.Trim());
        if (!string.IsNullOrWhiteSpace(cs.Bairro)) locPartes.Add(cs.Bairro.Trim());
        if (!string.IsNullOrWhiteSpace(cs.Cidade)) locPartes.Add(cs.Cidade.Trim());
        if (!string.IsNullOrWhiteSpace(cs.Uf)) locPartes.Add(cs.Uf.Trim().ToUpperInvariant());
        var localCompleto = locPartes.Count > 0 ? string.Join(", ", locPartes) : "";

        var sbItens = new StringBuilder();
        decimal totalEstimado = 0m;
        foreach (var ativ in cs.Atividades.OrderBy(a => a.Ordem))
        {
            var cod = ativ.Ordem.ToString("D2");
            var etapaTitulo = !string.IsNullOrWhiteSpace(ativ.Etapa) ? $"[{ativ.Etapa}] " : "";
            var desc = $"{etapaTitulo}{ativ.Nome} - {ativ.Descricao}";
            if (desc.Length > 80) desc = desc[..77] + "...";
            var um = !string.IsNullOrWhiteSpace(ativ.UnidadeMedicao) ? ativ.UnidadeMedicao.ToUpper() : "SV";
            var valorUnit = ativ.ValorEstimadoUnitario;
            var subtotal = ativ.Quantidade * valorUnit;
            totalEstimado += subtotal;

            sbItens.Append($@"
            <tr>
                <td class=""c-center"">{cod}</td>
                <td class=""c-left"">{desc.ToUpper()}</td>
                <td class=""c-center"">{um}</td>
                <td class=""c-center"">-</td>
                <td class=""c-center"">-</td>
                <td class=""c-center"">-</td>
                <td class=""c-right"">{valorUnit.ToString("N2", PtBr)}</td>
                <td class=""c-center"">SV</td>
                <td class=""c-right"">{ativ.Quantidade.ToString("N2", PtBr)}</td>
                <td class=""c-right"">{valorUnit.ToString("N2", PtBr)}</td>
                <td class=""c-right"">{subtotal.ToString("N2", PtBr)}</td>
            </tr>");
        }

        var obsBuilder = new StringBuilder();
        obsBuilder.AppendLine($"CATEGORIA: {cs.Categoria.ToUpper()} | PRIORIDADE: {cs.Prioridade.ToUpper()} | PRAZO ESTIMADO: {cs.PrazoEstimadoDias} DIAS");
        if (!string.IsNullOrWhiteSpace(localCompleto))
            obsBuilder.AppendLine($"LOCAL EXECUÇÃO: {localCompleto}");
        if (!string.IsNullOrWhiteSpace(cs.NecessidadeMotivacao))
            obsBuilder.AppendLine($"MOTIVAÇÃO: {cs.NecessidadeMotivacao}");
        if (!string.IsNullOrWhiteSpace(cs.CondicoesEspeciais))
            obsBuilder.AppendLine($"CONDIÇÕES ESPECIAIS: {cs.CondicoesEspeciais}");
        if (!string.IsNullOrWhiteSpace(cs.CertificacoesExigidas))
            obsBuilder.AppendLine($"EXIGÊNCIAS TÉCNICAS: {cs.CertificacoesExigidas}");

        return BuildDocumentHtml(
            tipoDocumento: "Cotação de Serviços",
            numeroDoc: cs.Numero.Replace("SRV-", "").Replace("SRV", ""),
            dataCadastro: dataCadastro,
            dataLimiteEntrega: dataLimite,
            labelDataEntrega: "Data Limite:",
            empresa: dadosEmpresa,
            nomeComprador: nomeComprador,
            fornNome: "FORNECEDORES / PRESTADORES CONVIDADOS",
            fornVendedor: "",
            fornEndereco: localCompleto,
            fornCnpj: "",
            fornFone: "",
            fornCodigo: "SRV",
            fornCelular: "",
            fornIe: "",
            condPagto: "Medição / Conforme Proposta",
            freteTipo: "Local da Contratante",
            transportadora: "-",
            propostaRef: cs.Titulo,
            mesAtual: DateTime.UtcNow.ToString("MMMM", PtBr),
            itensHtml: sbItens.ToString(),
            valorMercadorias: totalEstimado,
            totalGeral: totalEstimado,
            valorFrete: 0m,
            despAcessorias: 0m,
            custoFinanceiro: 0m,
            observacoes: obsBuilder.ToString()
        );
    }

    public static string GerarHtmlOrdemServico(OrdemServico os, Tenant? tenant, Usuario? comprador = null)
    {
        var dadosEmpresa = ObterDadosEmpresa(tenant, os.Filial);
        var nomeComprador = !string.IsNullOrWhiteSpace(os.AprovadoPorNome)
            ? os.AprovadoPorNome.Trim().ToUpper()
            : (comprador != null ? comprador.Nome.Trim().ToUpper() : "");

        var forn = os.Fornecedor;
        var fornNome = (forn?.RazaoSocial ?? forn?.NomeFantasia ?? "FORNECEDOR CONTRATADO").Trim().ToUpper();
        var fornVendedor = (!string.IsNullOrWhiteSpace(forn?.Contato) ? forn.Contato.Trim() : "").ToUpper();
        
        var fornEndPartes = new List<string>();
        if (!string.IsNullOrWhiteSpace(forn?.Endereco)) fornEndPartes.Add(forn.Endereco.Trim());
        if (!string.IsNullOrWhiteSpace(forn?.Cidade)) fornEndPartes.Add(forn.Cidade.Trim());
        if (!string.IsNullOrWhiteSpace(forn?.Uf)) fornEndPartes.Add(forn.Uf.Trim().ToUpperInvariant());
        var fornEndereco = string.Join(" - ", fornEndPartes).ToUpper();

        var fornCnpj = FormatCnpj(forn?.Cnpj);
        var fornFone = forn?.Telefone?.Trim() ?? "";
        var fornCodigo = forn?.CodigoErp?.Trim() ?? "";
        var fornCelular = forn?.Whatsapp?.Trim() ?? "";
        var fornIe = forn?.InscricaoEstadual?.Trim() ?? "";

        var dataCadastro = os.DataCriacao.ToString("dd/MM/yy", PtBr);
        var dataEntrega = os.PrevisaoTermino.HasValue
            ? os.PrevisaoTermino.Value.ToString("dd/MM/yy", PtBr)
            : (os.PrazoExecucaoDias > 0 ? os.DataCriacao.AddDays(os.PrazoExecucaoDias).ToString("dd/MM/yy", PtBr) : "");

        var sbItens = new StringBuilder();
        var proposta = os.PropostaServico;
        if (proposta?.Etapas != null && proposta.Etapas.Count > 0)
        {
            int i = 1;
            foreach (var et in proposta.Etapas)
            {
                var ativNome = et.AtividadeServico?.Nome ?? $"Etapa {i}";
                var desc = $"{ativNome} - {et.DetalhesExecucao}".ToUpper();
                if (desc.Length > 80) desc = desc[..77] + "...";
                sbItens.Append($@"
                <tr>
                    <td class=""c-center"">{i:D2}</td>
                    <td class=""c-left"">{desc}</td>
                    <td class=""c-center"">SV</td>
                    <td class=""c-center"">-</td>
                    <td class=""c-center"">-</td>
                    <td class=""c-center"">-</td>
                    <td class=""c-right"">{et.ValorTotal.ToString("N2", PtBr)}</td>
                    <td class=""c-center"">SV</td>
                    <td class=""c-right"">1,0000</td>
                    <td class=""c-right"">{et.ValorTotal.ToString("N2", PtBr)}</td>
                    <td class=""c-right"">{et.ValorTotal.ToString("N2", PtBr)}</td>
                </tr>");
                i++;
            }
        }
        else
        {
            sbItens.Append($@"
            <tr>
                <td class=""c-center"">01</td>
                <td class=""c-left"">{(!string.IsNullOrWhiteSpace(os.EscopoAprovadoResumo) ? os.EscopoAprovadoResumo.ToUpper() : "PRESTAÇÃO DE SERVIÇOS CONFORME ESCOPO APROVADO")}</td>
                <td class=""c-center"">SV</td>
                <td class=""c-center"">-</td>
                <td class=""c-center"">-</td>
                <td class=""c-center"">-</td>
                <td class=""c-right"">{os.ValorTotal.ToString("N2", PtBr)}</td>
                <td class=""c-center"">SV</td>
                <td class=""c-right"">1,0000</td>
                <td class=""c-right"">{os.ValorTotal.ToString("N2", PtBr)}</td>
                <td class=""c-right"">{os.ValorTotal.ToString("N2", PtBr)}</td>
            </tr>");
        }

        var obsPartes = new List<string>();
        if (os.GarantiaMeses > 0) obsPartes.Add($"GARANTIA EXIGIDA: {os.GarantiaMeses} MESES");
        if (!string.IsNullOrWhiteSpace(os.CondicaoPagamento)) obsPartes.Add($"CONDIÇÃO: {os.CondicaoPagamento}");
        if (!string.IsNullOrWhiteSpace(os.Observacoes)) obsPartes.Add(os.Observacoes);
        if (!string.IsNullOrWhiteSpace(os.ClausulasEspeciais)) obsPartes.Add(os.ClausulasEspeciais);
        var obs = string.Join(" | ", obsPartes);

        return BuildDocumentHtml(
            tipoDocumento: "Ordem de Serviço",
            numeroDoc: os.Numero.Replace("OS-", "").Replace("OS", ""),
            dataCadastro: dataCadastro,
            dataLimiteEntrega: dataEntrega,
            labelDataEntrega: "Término Previsto:",
            empresa: dadosEmpresa,
            nomeComprador: nomeComprador,
            fornNome: fornNome,
            fornVendedor: fornVendedor,
            fornEndereco: fornEndereco,
            fornCnpj: fornCnpj,
            fornFone: fornFone,
            fornCodigo: fornCodigo,
            fornCelular: fornCelular,
            fornIe: fornIe,
            condPagto: os.CondicaoPagamento,
            freteTipo: "Incluso",
            transportadora: "-",
            propostaRef: os.CotacaoServico?.Numero ?? "-",
            mesAtual: DateTime.UtcNow.ToString("MMMM", PtBr),
            itensHtml: sbItens.ToString(),
            valorMercadorias: os.ValorTotal,
            totalGeral: os.ValorTotal,
            valorFrete: os.Deslocamento,
            despAcessorias: 0m,
            custoFinanceiro: 0m,
            observacoes: obs
        );
    }

    /// <summary>
    /// Constrói o HTML fiel da folha de documento no padrão visual com o cabeçalho dinâmico da empresa.
    /// </summary>
    private static string BuildDocumentHtml(
        string tipoDocumento,
        string numeroDoc,
        string dataCadastro,
        string dataLimiteEntrega,
        string labelDataEntrega,
        DadosEmpresaDocumento empresa,
        string nomeComprador,
        string fornNome,
        string fornVendedor,
        string fornEndereco,
        string fornCnpj,
        string fornFone,
        string fornCodigo,
        string fornCelular,
        string fornIe,
        string condPagto,
        string freteTipo,
        string transportadora,
        string propostaRef,
        string mesAtual,
        string itensHtml,
        decimal valorMercadorias,
        decimal totalGeral,
        decimal valorFrete,
        decimal despAcessorias,
        decimal custoFinanceiro,
        string observacoes
    )
    {
        var dataHoraImpressao = DateTime.UtcNow;
        var dataFormatada = dataHoraImpressao.ToString("dd/MM/yy", PtBr);
        var horaFormatada = dataHoraImpressao.ToString("HH:mm:ss", PtBr);
        var hasLogo = !string.IsNullOrWhiteSpace(empresa.LogoCellHtml);
        var centerWidth = hasLogo ? "58%" : "78%";

        // Montagem dinâmica dos dados cadastrais da empresa no centro
        var sbEmpresaCenter = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(empresa.RazaoSocial))
            sbEmpresaCenter.Append($@"<div class=""empresa-razao"">{empresa.RazaoSocial}</div>");
        if (!string.IsNullOrWhiteSpace(empresa.EnderecoLinha1))
            sbEmpresaCenter.Append($@"<div>{empresa.EnderecoLinha1}</div>");
        if (!string.IsNullOrWhiteSpace(empresa.EnderecoLinha2))
            sbEmpresaCenter.Append($@"<div>{empresa.EnderecoLinha2}</div>");

        var fiscais = new List<string>();
        if (!string.IsNullOrWhiteSpace(empresa.InscricaoEstadual))
            fiscais.Add($"Insc. Estadual: {empresa.InscricaoEstadual}");
        if (!string.IsNullOrWhiteSpace(empresa.InscricaoMunicipal))
            fiscais.Add($"Insc. Municipal: {empresa.InscricaoMunicipal}");
        if (!string.IsNullOrWhiteSpace(empresa.CnpjFormatado))
            fiscais.Add($"CNPJ: {empresa.CnpjFormatado}");
        if (fiscais.Count > 0)
            sbEmpresaCenter.Append($@"<div>{string.Join(" - ", fiscais)}</div>");

        if (!string.IsNullOrWhiteSpace(empresa.Telefone))
            sbEmpresaCenter.Append($@"<div>Fone/Fax: {empresa.Telefone}</div>");
        if (!string.IsNullOrWhiteSpace(empresa.Email))
            sbEmpresaCenter.Append($@"<div>E-mail: {empresa.Email}</div>");
        if (!string.IsNullOrWhiteSpace(nomeComprador))
            sbEmpresaCenter.Append($@"<div>Comprador: <strong>{nomeComprador}</strong></div>");

        return $@"<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{tipoDocumento} Nº {numeroDoc}</title>
    <style>
        * {{
            box-sizing: border-box;
            -webkit-print-color-adjust: exact !important;
            print-color-adjust: exact !important;
        }}
        @page {{
            size: A4 portrait;
            margin: 8mm 8mm 8mm 8mm;
        }}
        body {{
            margin: 0;
            padding: 10px;
            font-family: Arial, 'Segoe UI', Helvetica, sans-serif;
            color: #000;
            background-color: #f3f4f6;
            font-size: 11px;
            line-height: 1.25;
        }}
        @media print {{
            body {{
                background-color: #fff;
                padding: 0;
            }}
            .action-bar {{
                display: none !important;
            }}
            .document-container {{
                box-shadow: none !important;
                border: 2px solid #000 !important;
                margin: 0 !important;
                width: 100% !important;
            }}
        }}

        .action-bar {{
            max-width: 1040px;
            margin: 0 auto 12px auto;
            background: #1e293b;
            color: #fff;
            padding: 10px 18px;
            border-radius: 8px;
            display: flex;
            align-items: center;
            justify-content: space-between;
            box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1);
        }}
        .action-title {{
            font-size: 14px;
            font-weight: 700;
            display: flex;
            align-items: center;
            gap: 8px;
        }}
        .btn {{
            display: inline-flex;
            align-items: center;
            gap: 6px;
            padding: 7px 14px;
            border-radius: 6px;
            font-weight: 600;
            font-size: 13px;
            cursor: pointer;
            border: none;
            transition: all 0.2s;
            text-decoration: none;
        }}
        .btn-primary {{
            background: #2563eb;
            color: #fff;
        }}
        .btn-primary:hover {{
            background: #1d4ed8;
        }}
        .btn-secondary {{
            background: #475569;
            color: #fff;
        }}
        .btn-secondary:hover {{
            background: #334155;
        }}

        .document-container {{
            max-width: 1040px;
            margin: 0 auto;
            background: #fff;
            border: 2px solid #000;
            padding: 12px 14px 14px 14px;
            box-shadow: 0 10px 15px -3px rgba(0,0,0,0.1);
        }}

        .header-table {{
            width: 100%;
            border-collapse: collapse;
            margin-bottom: 6px;
        }}
        .header-logo-cell {{
            width: 20%;
            vertical-align: middle;
            padding-right: 8px;
        }}
        .header-center-cell {{
            width: {centerWidth};
            text-align: center;
            vertical-align: top;
            font-size: 10px;
            line-height: 1.35;
        }}
        .header-right-cell {{
            width: 22%;
            text-align: right;
            vertical-align: top;
            font-size: 9.5px;
            line-height: 1.35;
        }}
        .empresa-razao {{
            font-size: 13px;
            font-weight: 800;
            letter-spacing: 0.5px;
            margin-bottom: 2px;
        }}

        .doc-title-bar {{
            border-top: 3.5px solid #000;
            border-bottom: 1.5px solid #000;
            padding: 6px 4px;
            display: flex;
            align-items: baseline;
            justify-content: space-between;
            margin-bottom: 8px;
        }}
        .doc-title-left {{
            display: flex;
            align-items: baseline;
            gap: 16px;
        }}
        .doc-title-text {{
            font-size: 22px;
            font-weight: 900;
            letter-spacing: -0.5px;
        }}
        .doc-title-num {{
            font-size: 22px;
            font-weight: 900;
            margin-left: 10px;
        }}
        .doc-title-dates {{
            display: flex;
            gap: 32px;
            font-size: 11px;
            font-weight: 500;
        }}

        .supplier-block {{
            width: 100%;
            border-collapse: collapse;
            margin-bottom: 6px;
            font-size: 10px;
            line-height: 1.45;
        }}
        .supplier-block td {{
            padding: 1px 4px;
            vertical-align: top;
        }}
        .supplier-col-left {{
            width: 60%;
        }}
        .supplier-col-right {{
            width: 40%;
        }}
        .lbl {{
            display: inline-block;
            min-width: 82px;
            font-weight: normal;
        }}
        .val {{
            font-weight: 700;
        }}

        .conditions-bar {{
            border-top: 1px solid #000;
            padding: 5px 4px 6px 4px;
            font-size: 10px;
            line-height: 1.45;
        }}
        .cond-row {{
            display: flex;
            justify-content: space-between;
            align-items: center;
        }}

        .items-grid {{
            width: 100%;
            border-collapse: collapse;
            border: 2px solid #000;
            margin-top: 6px;
            margin-bottom: 0;
            font-size: 9.5px;
        }}
        .items-grid th {{
            border: 1.5px solid #000;
            background-color: #fff;
            padding: 3px 2px;
            font-size: 8px;
            font-weight: 800;
            text-align: center;
            line-height: 1.15;
        }}
        .items-grid td {{
            border-left: 1px solid #000;
            border-right: 1px solid #000;
            border-top: none;
            border-bottom: 1px solid #ccc;
            padding: 3px 4px;
            font-size: 9px;
            height: 20px;
        }}
        .c-left {{ text-align: left; }}
        .c-center {{ text-align: center; }}
        .c-right {{ text-align: right; }}

        .totals-section {{
            width: 100%;
            display: flex;
            justify-content: flex-end;
            margin-top: 0;
        }}
        .totals-table {{
            border-collapse: collapse;
            width: 240px;
            border: 2px solid #000;
            border-top: none;
            font-size: 9.5px;
        }}
        .totals-table td {{
            padding: 3px 6px;
            border: 1px solid #000;
        }}
        .totals-table .t-label {{
            font-weight: 700;
            width: 55%;
        }}
        .totals-table .t-val {{
            text-align: right;
            font-weight: 800;
        }}

        .footer-terms {{
            margin-top: 8px;
            font-size: 10px;
            line-height: 1.5;
        }}
        .expenses-row {{
            display: flex;
            gap: 40px;
            margin-bottom: 4px;
        }}
        .obs-box {{
            margin-top: 6px;
            border: 1px solid #000;
            padding: 6px 8px;
            min-height: 52px;
            font-size: 9.5px;
            line-height: 1.4;
            background: #fff;
            white-space: pre-wrap;
        }}

        .signatures-grid {{
            margin-top: 36px;
            display: flex;
            justify-content: space-around;
            page-break-inside: avoid;
        }}
        .sig-col {{
            width: 320px;
            text-align: center;
        }}
        .sig-line {{
            border-top: 1.5px solid #000;
            margin-bottom: 4px;
        }}
        .sig-label {{
            font-size: 10px;
            font-weight: 700;
            text-transform: uppercase;
        }}
        .sig-sub {{
            font-size: 8.5px;
            color: #555;
        }}
    </style>
</head>
<body>

    <div class=""action-bar"">
        <div class=""action-title"">
            <svg width=""18"" height=""18"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2""><path d=""M6 9V2h12v7M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2""/><path d=""M6 14h12v8H6z""/></svg>
            <span>{tipoDocumento} • Nº {numeroDoc}</span>
        </div>
        <div style=""display: flex; gap: 10px;"">
            <button class=""btn btn-primary"" onclick=""window.print()"">
                <svg width=""16"" height=""16"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2""><polyline points=""6 9 6 2 18 2 18 9""/><path d=""M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2""/><rect x=""6"" y=""14"" width=""12"" height=""8""/></svg>
                Imprimir / Salvar PDF
            </button>
            <button class=""btn btn-secondary"" onclick=""window.close()"">Fechar</button>
        </div>
    </div>

    <div class=""document-container"">

        <!-- CABEÇALHO DA EMPRESA -->
        <table class=""header-table"">
            <tr>
                {(hasLogo ? empresa.LogoCellHtml : "")}
                <td class=""header-center-cell"">
                    {sbEmpresaCenter}
                </td>
                <td class=""header-right-cell"">
                    <table style=""margin-left: auto; border-collapse: collapse;"">
                        <tr><td style=""text-align: right; padding-right: 6px;"">Página:</td><td style=""text-align: right; font-weight: 700;"">1/1</td></tr>
                        <tr><td style=""text-align: right; padding-right: 6px;"">Data:</td><td style=""text-align: right; font-weight: 700;"">{dataFormatada}</td></tr>
                        <tr><td style=""text-align: right; padding-right: 6px;"">Hora:</td><td style=""text-align: right; font-weight: 700;"">{horaFormatada}</td></tr>
                        {(string.IsNullOrWhiteSpace(nomeComprador) ? "" : $@"<tr><td style=""text-align: right; padding-right: 6px;"">Usuário:</td><td style=""text-align: right; font-weight: 700;"">{nomeComprador}</td></tr>")}
                    </table>
                </td>
            </tr>
        </table>

        <!-- BARRA DO TÍTULO DO DOCUMENTO -->
        <div class=""doc-title-bar"">
            <div class=""doc-title-left"">
                <span class=""doc-title-text"">{tipoDocumento}</span>
                <span class=""doc-title-num"">Nº: {numeroDoc}</span>
            </div>
            <div class=""doc-title-dates"">
                <span>Data Cadastro: <strong>{dataCadastro}</strong></span>
                {(!string.IsNullOrWhiteSpace(dataLimiteEntrega) ? $@"<span>{labelDataEntrega} <strong>{dataLimiteEntrega}</strong></span>" : "")}
            </div>
        </div>

        <!-- DADOS DO FORNECEDOR / PARCEIRO (2 COLUNAS) -->
        <table class=""supplier-block"">
            <tr>
                <td class=""supplier-col-left"">
                    <div><span class=""lbl"">Fornecedor:</span> <span class=""val"">{fornNome}</span></div>
                    {(!string.IsNullOrWhiteSpace(fornVendedor) ? $@"<div><span class=""lbl"">Vendedor(a):</span> <span class=""val"">{fornVendedor}</span></div>" : "")}
                    {(!string.IsNullOrWhiteSpace(fornEndereco) ? $@"<div><span class=""lbl"">Endereço:</span> <span>{fornEndereco}</span></div>" : "")}
                    {(!string.IsNullOrWhiteSpace(fornCnpj) ? $@"<div><span class=""lbl"">CNPJ:</span> <span>{fornCnpj}</span></div>" : "")}
                    {(!string.IsNullOrWhiteSpace(fornFone) ? $@"<div><span class=""lbl"">Fone:</span> <span>{fornFone}</span></div>" : "")}
                </td>
                <td class=""supplier-col-right"">
                    {(!string.IsNullOrWhiteSpace(fornCodigo) ? $@"<div><span class=""lbl"">Código:</span> <span class=""val"">{fornCodigo}</span></div>" : "")}
                    {(!string.IsNullOrWhiteSpace(fornCelular) ? $@"<div><span class=""lbl"">Celular:</span> <span>{fornCelular}</span></div>" : "")}
                    {(!string.IsNullOrWhiteSpace(fornIe) ? $@"<div><span class=""lbl"">Insc. Estadual:</span> <span>{fornIe}</span></div>" : "")}
                </td>
            </tr>
        </table>

        <!-- CONDIÇÕES COMERCIAIS & FRETE -->
        <div class=""conditions-bar"">
            <div class=""cond-row"">
                <div>Cond. Pagamento: <strong>{condPagto}</strong></div>
                <div>Frete: <strong>{freteTipo}</strong></div>
            </div>
            {(!string.IsNullOrWhiteSpace(transportadora) && transportadora != "-" ? $@"<div class=""cond-row"" style=""margin-top: 2px;""><div>Transportadora: <strong>{transportadora}</strong></div></div>" : "")}
            {(!string.IsNullOrWhiteSpace(propostaRef) && propostaRef != "-" ? $@"<div class=""cond-row"" style=""margin-top: 2px;""><div>Referência: <strong>{propostaRef}</strong></div></div>" : "")}
        </div>

        <!-- GRADE DE ITENS -->
        <table class=""items-grid"">
            <thead>
                <tr>
                    <th style=""width: 8%;"">Código</th>
                    <th style=""width: 30%;"">Descrição do Material</th>
                    <th style=""width: 4%;"">UM</th>
                    <th style=""width: 6%;"">Saldo Est.</th>
                    <th style=""width: 7%;"">Dem.Mês<br>({mesAtual})</th>
                    <th style=""width: 8%;"">Dem.Média</th>
                    <th style=""width: 7%;"">Pr.Ú.Compra</th>
                    <th style=""width: 5%;"">Cód.U.F.</th>
                    <th style=""width: 8%;"">Quantidade</th>
                    <th style=""width: 9%;"">Preço Unit.</th>
                    <th style=""width: 10%;"">Preço Total</th>
                </tr>
            </thead>
            <tbody>
                {itensHtml}
            </tbody>
        </table>

        <!-- TOTAIS ALINHADOS À DIREITA -->
        <div class=""totals-section"">
            <table class=""totals-table"">
                <tr>
                    <td class=""t-label"">Valor Mercad.:</td>
                    <td class=""t-val"">{valorMercadorias.ToString("N2", PtBr)}</td>
                </tr>
                <tr style=""background-color: #fafafa;"">
                    <td class=""t-label"">Total Pedido:</td>
                    <td class=""t-val"" style=""font-size: 11px;"">{totalGeral.ToString("N2", PtBr)}</td>
                </tr>
            </table>
        </div>

        <!-- DESPESAS E CUSTOS FINANCEIROS -->
        <div class=""footer-terms"">
            <div class=""expenses-row"">
                {(valorFrete > 0 ? $@"<div>Valor frete: <strong>{valorFrete.ToString("N2", PtBr)}</strong></div>" : "")}
                {(despAcessorias > 0 ? $@"<div>Despesas acessórias: <strong>{despAcessorias.ToString("N2", PtBr)}</strong></div>" : "")}
                {(custoFinanceiro > 0 ? $@"<div>Custo Financeiro: <strong>{custoFinanceiro.ToString("N2", PtBr)}</strong></div>" : "")}
            </div>

            <!-- OBSERVAÇÕES -->
            <div>Observações:</div>
            <div class=""obs-box"">{(string.IsNullOrWhiteSpace(observacoes) ? "Nenhuma observação registrada." : observacoes)}</div>
        </div>

        <!-- ASSINATURAS -->
        <div class=""signatures-grid"">
            <div class=""sig-col"">
                <div class=""sig-line""></div>
                <div class=""sig-label"">{(!string.IsNullOrWhiteSpace(nomeComprador) ? nomeComprador : "Comprador Responsável")}</div>
                <div class=""sig-sub"">Comprador Responsável / Nexo Compras</div>
            </div>
            <div class=""sig-col"">
                <div class=""sig-line""></div>
                <div class=""sig-label"">{(!string.IsNullOrWhiteSpace(fornNome) ? fornNome : "Aceite do Fornecedor")}</div>
                <div class=""sig-sub"">Aceite do Fornecedor / Data de Confirmação</div>
            </div>
        </div>

    </div>

</body>
</html>";
    }

    /// <summary>
    /// Gera o relatório analítico formatado em layout profissional para visualização ou impressão PDF.
    /// </summary>
    public static string GerarHtmlRelatorio(
        string tituloRelatorio,
        string subtitulo,
        string conteudoHtml,
        string? kpisHtml,
        Tenant? tenant,
        Usuario? usuario = null)
    {
        var dadosEmpresa = ObterDadosEmpresa(tenant, null);
        var dataHoraImpressao = DateTime.UtcNow;
        var dataFormatada = dataHoraImpressao.ToString("dd/MM/yyyy", PtBr);
        var horaFormatada = dataHoraImpressao.ToString("HH:mm:ss", PtBr);
        var nomeUsuario = usuario?.Nome?.Trim().ToUpper() ?? "";
        var hasLogo = !string.IsNullOrWhiteSpace(dadosEmpresa.LogoCellHtml);
        var centerWidth = hasLogo ? "58%" : "78%";

        var sbEmpresaCenter = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(dadosEmpresa.RazaoSocial))
            sbEmpresaCenter.Append($@"<div style=""font-size: 14px; font-weight: 800; letter-spacing: 0.5px;"">{dadosEmpresa.RazaoSocial}</div>");
        if (!string.IsNullOrWhiteSpace(dadosEmpresa.EnderecoLinha1))
            sbEmpresaCenter.Append($@"<div>{dadosEmpresa.EnderecoLinha1}</div>");
        if (!string.IsNullOrWhiteSpace(dadosEmpresa.EnderecoLinha2))
            sbEmpresaCenter.Append($@"<div>{dadosEmpresa.EnderecoLinha2}</div>");

        var fiscais = new List<string>();
        if (!string.IsNullOrWhiteSpace(dadosEmpresa.InscricaoEstadual))
            fiscais.Add($"IE: {dadosEmpresa.InscricaoEstadual}");
        if (!string.IsNullOrWhiteSpace(dadosEmpresa.CnpjFormatado))
            fiscais.Add($"CNPJ: {dadosEmpresa.CnpjFormatado}");
        if (fiscais.Count > 0)
            sbEmpresaCenter.Append($@"<div>{string.Join(" - ", fiscais)}</div>");

        return $@"<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
    <meta charset=""UTF-8"">
    <title>{tituloRelatorio}</title>
    <style>
        * {{ box-sizing: border-box; -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; }}
        @page {{ size: A4 landscape; margin: 8mm; }}
        body {{ margin: 0; padding: 12px; font-family: Arial, sans-serif; font-size: 11px; background: #f8fafc; color: #1e293b; }}
        @media print {{ body {{ background: #fff; padding: 0; }} .no-print {{ display: none !important; }} .container {{ border: none !important; box-shadow: none !important; }} }}
        .no-print {{ max-width: 1100px; margin: 0 auto 12px auto; display: flex; justify-content: space-between; align-items: center; background: #0f172a; color: #fff; padding: 10px 16px; border-radius: 8px; }}
        .btn-print {{ background: #2563eb; color: #fff; border: none; padding: 8px 16px; border-radius: 6px; font-weight: 700; cursor: pointer; }}
        .container {{ max-width: 1100px; margin: 0 auto; background: #fff; padding: 16px; border: 1px solid #cbd5e1; border-radius: 8px; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.05); }}
        .rep-title-bar {{ border-top: 2px solid #0f172a; border-bottom: 2px solid #0f172a; padding: 8px 4px; margin: 12px 0; display: flex; justify-content: space-between; align-items: center; }}
        .rep-title {{ font-size: 18px; font-weight: 900; color: #0f172a; text-transform: uppercase; }}
        .kpi-row {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 10px; margin-bottom: 16px; }}
        .kpi-card {{ background: #f1f5f9; border: 1px solid #e2e8f0; border-radius: 6px; padding: 10px 12px; }}
        .kpi-lbl {{ font-size: 10px; font-weight: 700; color: #64748b; text-transform: uppercase; }}
        .kpi-val {{ font-size: 18px; font-weight: 800; color: #0f172a; margin-top: 4px; }}
        table.data-table {{ width: 100%; border-collapse: collapse; font-size: 10px; margin-top: 8px; }}
        table.data-table th {{ background: #f8fafc; border: 1px solid #cbd5e1; padding: 6px 8px; text-align: left; font-weight: 700; color: #334155; }}
        table.data-table td {{ border: 1px solid #e2e8f0; padding: 5px 8px; }}
        table.data-table tr:nth-child(even) {{ background: #fbfcfe; }}
        .text-right {{ text-align: right; }}
        .text-center {{ text-align: center; }}
        .badge {{ display: inline-block; padding: 2px 6px; border-radius: 4px; font-size: 9px; font-weight: 700; }}
        .badge-green {{ background: #dcfce7; color: #166534; }}
        .badge-red {{ background: #fee2e2; color: #991b1b; }}
        .badge-blue {{ background: #dbeafe; color: #1e40af; }}
    </style>
</head>
<body>
    <div class=""no-print"">
        <div><strong>{tituloRelatorio}</strong> • {subtitulo}</div>
        <button class=""btn-print"" onclick=""window.print()"">🖨️ Imprimir / Salvar PDF</button>
    </div>

    <div class=""container"">
        <!-- CABEÇALHO -->
        <table style=""width: 100%; border-collapse: collapse; margin-bottom: 8px;"">
            <tr>
                {(hasLogo ? dadosEmpresa.LogoCellHtml : "")}
                <td style=""width: {centerWidth}; text-align: center; vertical-align: top; font-size: 10.5px; line-height: 1.4;"">
                    {sbEmpresaCenter}
                </td>
                <td style=""width: 22%; text-align: right; vertical-align: top; font-size: 9.5px; line-height: 1.4;"">
                    <div>Data: <strong>{dataFormatada}</strong></div>
                    <div>Hora: <strong>{horaFormatada}</strong></div>
                    {(!string.IsNullOrWhiteSpace(nomeUsuario) ? $@"<div>Usuário: <strong>{nomeUsuario}</strong></div>" : "")}
                </td>
            </tr>
        </table>

        <div class=""rep-title-bar"">
            <div>
                <div class=""rep-title"">{tituloRelatorio}</div>
                <div style=""font-size: 11px; color: #64748b; margin-top: 2px;"">{subtitulo}</div>
            </div>
        </div>

        {(!string.IsNullOrWhiteSpace(kpisHtml) ? kpisHtml : "")}

        <div style=""overflow-x: auto;"">
            {conteudoHtml}
        </div>
    </div>
</body>
</html>";
    }

    private static string FormatCnpj(string? cnpj)
    {
        if (string.IsNullOrWhiteSpace(cnpj)) return string.Empty;
        var digits = new string(cnpj.Where(char.IsDigit).ToArray());
        if (digits.Length == 14)
            return $"{digits[..2]}.{digits.Substring(2, 3)}.{digits.Substring(5, 3)}/{digits.Substring(8, 4)}-{digits.Substring(12, 2)}";
        if (digits.Length == 11)
            return $"{digits[..3]}.{digits.Substring(3, 3)}.{digits.Substring(6, 3)}-{digits.Substring(9, 2)}";
        return cnpj.Trim();
    }
}
