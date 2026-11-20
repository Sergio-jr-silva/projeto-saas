# Pedidos de Compra

Aplicativo web para criar pedidos de compra a partir dos produtos do **BM.FDB** (Firebird), cotar com vários fornecedores e comparar as propostas.

## Como usar

1. Mantenha o servidor Firebird em execução (padrão `127.0.0.1:3050`, `SYSDBA/masterkey`).
2. `npm install` (uma vez) e `npm start`.
3. Abra http://localhost:3000. Na primeira vez o app lê o BM.FDB automaticamente (botão **Sincronizar** em Configurações).

Variáveis opcionais: `PORT`, `FB_HOST`, `FB_PORT`, `FB_DATABASE`, `FB_USER`, `FB_PASSWORD`, `FB_ENCODING` (padrão `WIN1252`), `DATA_DIR`.

## Funcionalidades

- **Dashboard**: economia obtida, total comprado, margem, fornecedor que mais/menos vende, compras × vendas.
- **Alertas de estoque**: calculados pelo giro (vendas dos últimos 90 dias) × estoque × prazo de entrega. O BM não tem estoque mínimo preenchido, por isso o app usa o consumo médio.
- **Novo pedido**: matriz produto × fornecedor (sugere o fornecedor do BM e os do histórico de compras); fornecedores podem ser cadastrados no app.
- **Link seguro por fornecedor**: `/f/<id>#c=<código>`. O código tem 16 caracteres aleatórios (80 bits); o conteúdo do pedido e a resposta são gravados com AES-256-GCM, com chave derivada do código (scrypt). Sem o código não há como abrir ou decifrar. Bloqueio após 8 tentativas erradas, validade configurável e "novo código" para revogar.
- **Resposta do fornecedor**: preço, quantidade, desconto, CST, ICMS, IPI, MVA, ICMS ST, frete, prazos.
- **Comparativo**: custo efetivo (líquido + IPI + ICMS ST + frete, opcionalmente − crédito de ICMS), recomendação (fornecedor único × melhor combinação item a item), margem por item e cálculo de economia.
- **Acompanhamento**: aprovado → confirmado → faturado → em transporte → recebido, com linha do tempo.
- **Histórico por fornecedor**: notas de entrada do BM, produtos comprados, pedidos feitos pelo app.

## Segurança

- O BM.FDB só é **lido**. Dados do app ficam em `data/` (`store.json`, `secret.key`, `bm_cache.json`) — proteja/faça backup dessa pasta.
- O painel do comprador só responde a acessos diretos em `localhost`. Apenas `/f/*` e `/api/f/*` ficam acessíveis a fornecedores. Para expor à internet use um proxy/túnel HTTPS e informe a URL em Configurações.

## Testes

`npm test` roda um teste de ponta a ponta (em pasta temporária, usando o cache do BM).

# NEXO COMPRAS
## Plataforma SaaS de Gestão Inteligente de Compras e Fornecedores

**Versão:** 2.0  
**Produto:** Nexo Compras  
**Modelo de negócio:** Software as a Service (SaaS)  
**Arquitetura:** Multi-tenant (multicliente)  
**Idioma principal:** Português (Brasil)  
**Objetivo:** Automatizar compras, conectar empresas a fornecedores, comparar propostas comerciais e gerar economia por meio de inteligência de dados.

---

# 1. VISÃO GERAL DO PROJETO

O Nexo Compras é uma plataforma SaaS de gestão inteligente de compras, desenvolvida para atender supermercados, mercadinhos, distribuidoras, lojas de varejo e empresas de diferentes segmentos.

A plataforma tem como objetivo automatizar o processo de reposição de estoque, solicitação de cotações, negociação com fornecedores, comparação de propostas e geração de pedidos de compra.

O sistema será integrado inicialmente ao ERP BM Informática, utilizando o banco de dados Firebird (`BM.FDB`), que permanece instalado localmente no servidor de cada empresa.

Entretanto, a arquitetura deverá permitir futuras integrações com outros ERPs e bancos de dados, sem necessidade de reconstruir a plataforma.

O produto será comercializado como SaaS, permitindo que diversas empresas utilizem a mesma aplicação hospedada na nuvem, mantendo seus dados, usuários, fornecedores, produtos e operações completamente isolados.

## 1.1. Proposta de valor

O Nexo Compras deverá permitir que uma empresa:

1. Identifique automaticamente produtos com estoque baixo.
2. Gere solicitações de cotação para diversos fornecedores.
3. Envie solicitações por links seguros.
4. Permita que fornecedores preencham suas propostas pela internet.
5. Receba automaticamente os preços e informações fiscais.
6. Compare propostas comerciais.
7. Identifique os fornecedores mais vantajosos.
8. Aprove cotações e gere pedidos de compra.
9. Acompanhe pedidos e entregas.
10. Analise o histórico de compras e a economia gerada.

**O objetivo é transformar o processo de compras em uma operação automatizada, inteligente e orientada por dados.**

---

# 2. MODELO DE NEGÓCIO: SAAS MULTIEMPRESA

O Nexo Compras será uma plataforma centralizada na nuvem.

Não deverá existir uma instalação independente da aplicação para cada cliente.

Uma única infraestrutura atenderá várias empresas, mantendo os dados de cada organização separados logicamente e protegidos por mecanismos de isolamento.

Cada empresa será identificada como um TENANT.

Exemplo:

| Empresa | Tenant ID | Banco ERP |
|---|---|---|
| Mercado A | UUID A | Firebird local |
| Mercado B | UUID B | Firebird local |
| Distribuidora C | UUID C | Firebird local |
| Loja D | UUID D | Firebird local |

Cada empresa terá:

- Conta própria.
- Usuários próprios.
- Produtos próprios.
- Estoque próprio.
- Fornecedores próprios.
- Cotações próprias.
- Pedidos próprios.
- Histórico próprio.
- Dashboard próprio.
- Configurações próprias.
- Plano de assinatura próprio.

É obrigatório garantir que uma empresa nunca consiga visualizar ou acessar informações de outra empresa.

## 2.1. Endereços da plataforma

Domínio principal:

`https://nexocompras.com.br`

Aplicação:

`https://app.nexocompras.com.br`

Portal do fornecedor:

`https://fornecedor.nexocompras.com.br`

Painel administrativo:

`https://admin.nexocompras.com.br`

A identificação do tenant deverá ocorrer através da sessão autenticada e do contexto de organização, não apenas por um parâmetro enviado pelo navegador.

---

# 3. ARQUITETURA GERAL

A arquitetura deverá ser dividida em três módulos principais:

1. Nexo Compras SaaS.
2. Nexo Compras Agent.
3. Nexo Compras Admin.

## 3.1. Nexo Compras SaaS

É a aplicação principal.

Responsável por:

- Dashboard.
- Produtos.
- Estoque.
- Fornecedores.
- Cotações.
- Portal de fornecedores.
- Comparação de propostas.
- Pedidos de compra.
- Histórico.
- Relatórios.
- Indicadores.
- Usuários e permissões.

## 3.2. Nexo Compras Agent

Serviço auxiliar instalado localmente no servidor de cada empresa.

Responsável por:

- Conectar ao banco Firebird local.
- Consultar produtos.
- Consultar fornecedores.
- Consultar estoque.
- Consultar preços de compra.
- Sincronizar informações com a nuvem.
- Registrar erros.
- Controlar atualizações.
- Reestabelecer a conexão após falhas.

O agente deverá funcionar como um serviço do Windows, iniciando automaticamente junto com o sistema operacional.

## 3.3. Nexo Compras Admin

Painel administrativo exclusivo do proprietário da plataforma.

Responsável por:

- Cadastrar empresas.
- Ativar e bloquear contas.
- Gerenciar planos.
- Gerenciar assinaturas.
- Monitorar clientes.
- Monitorar integrações.
- Controlar licenças.
- Consultar consumo de recursos.
- Consultar pagamentos.
- Gerenciar configurações globais.
- Acompanhar disponibilidade da plataforma.

O painel administrativo deverá possuir autenticação e permissões próprias.

---

# 4. TECNOLOGIAS

## 4.1. Frontend

Tecnologias:

- React.
- TypeScript.
- HTML5.
- CSS3.
- Vite.
- Biblioteca moderna de componentes.
- Biblioteca de gráficos.
- React Router.
- Axios ou Fetch API.

O frontend deverá ser responsivo e funcionar em computadores, tablets e celulares.

## 4.2. Backend

Tecnologias:

- C#.
- ASP.NET Core.
- API REST.
- Entity Framework Core.
- Autenticação e autorização.
- Serviços de aplicação.
- Repositórios.
- Validações.
- Logs.

O backend será responsável pelas regras de negócio, autenticação, controle multi-tenant, cálculos comerciais, cotações, pedidos e integração entre os módulos.

## 4.3. Banco de dados

Banco principal:

**PostgreSQL**

Responsável por armazenar:

- Empresas.
- Usuários.
- Assinaturas.
- Planos.
- Produtos sincronizados.
- Fornecedores.
- Cotações.
- Respostas.
- Pedidos.
- Histórico.
- Logs.
- Configurações.
- Notificações.
- Registros de sincronização.

O banco PostgreSQL será centralizado na nuvem.

## 4.4. Integração ERP

Banco inicial:

**Firebird — BM.FDB**

Tecnologia do agente:

- C#.
- .NET Worker Service.
- Firebird ADO.NET Provider.
- Comunicação HTTPS.
- Processamento em segundo plano.

O agente deverá ser independente do frontend e do backend.

## 4.5. Hospedagem

Infraestrutura inicial recomendada:

- Microsoft Azure.
- Azure App Service ou serviço equivalente para API.
- PostgreSQL gerenciado.
- Azure Blob Storage.
- Domínio próprio.
- Certificados SSL.
- Backup automatizado.
- Monitoramento.

A arquitetura deverá permitir crescimento horizontal e vertical conforme a quantidade de clientes.

---

# 5. ARQUITETURA DE INTEGRAÇÃO COM FIREBIRD

Esta é uma das partes mais importantes do projeto.

Cada cliente possui seu próprio servidor local, onde está instalado o ERP e o banco Firebird.

O Nexo Compras não deverá tentar acessar diretamente o `localhost` do cliente através da internet.

Para isso, deverá existir um agente local.

## 5.1. Exemplo de ambiente de um cliente

```text
SERVIDOR DO CLIENTE
│
├── Windows Server / Windows 10 / Windows 11
│
├── ERP BM Informática
│
├── Firebird Server
│
├── BM.FDB
│   └── C:\DATACASH\BM.FDB
│
└── Nexo Compras Agent
    ├── Configuração
    ├── Conexão Firebird
    ├── Sincronização
    ├── Logs
    └── Comunicação HTTPS
```

## 5.2. Funcionamento do agente

1. O cliente contrata o Nexo Compras.
2. Cria sua empresa na plataforma.
3. Recebe uma chave de ativação.
4. Baixa o instalador do Nexo Compras Agent.
5. Instala o agente no servidor da loja.
6. Configura o caminho do banco Firebird.
7. Informa os parâmetros necessários da conexão.
8. O agente valida a conexão.
9. O agente inicia a sincronização.
10. Os dados passam a aparecer na plataforma.

## 5.3. Comunicação

A comunicação deverá ser iniciada pelo agente local.

Fluxo:

```text
Firebird Local
      |
      v
Nexo Compras Agent
      |
      v
HTTPS / TLS
      |
      v
API Nexo Compras
      |
      v
PostgreSQL na Nuvem
      |
      v
Dashboard Web
```

Não abrir a porta 3050 do Firebird para a internet.

Não disponibilizar o arquivo BM.FDB publicamente.

Não transmitir o banco inteiro para a nuvem.

Enviar somente as informações necessárias para o funcionamento da plataforma.

## 5.4. Sincronização

Frequências iniciais sugeridas:

| Informação | Frequência |
|---|---|
| Estoque | 5 minutos |
| Produtos | 30 minutos |
| Fornecedores | 30 minutos |
| Preços | Conforme atualização |
| Status do agente | 1 minuto |

Os intervalos deverão ser configuráveis.

Implementar sincronização incremental sempre que possível.

O agente deverá armazenar alterações pendentes quando a internet estiver indisponível e sincronizar novamente após a reconexão.

A sincronização deverá ser idempotente, evitando registros duplicados.

## 5.5. Segurança do agente

Cada instalação deverá possuir:

- UUID próprio.
- Identificação do tenant.
- Credencial exclusiva.
- Autenticação segura.
- Rotação de credenciais.
- Registro de atividade.
- Controle de última sincronização.
- Possibilidade de revogação.
- Atualização segura.

O backend deverá identificar o tenant por meio da credencial autenticada do agente.

Não confiar em identificadores de empresa enviados livremente pelo cliente.

## 5.6. Compatibilidade

O agente deverá ser preparado para diferentes versões do Firebird.

A implementação deverá verificar:

- Versão do Firebird.
- Caminho do banco.
- Charset.
- Dialeto.
- Compatibilidade do driver.
- Permissões.
- Conexão.
- Estrutura das tabelas.

Não presumir que todos os clientes utilizam a mesma estrutura de banco.

---

# 6. ARQUITETURA MULTI-TENANT

O isolamento dos dados é obrigatório.

O sistema deverá permitir que várias empresas utilizem a mesma aplicação sem compartilhar informações indevidamente.

## 6.1. Identificação das empresas

Criar uma tabela `tenants`.

Campos sugeridos:

- id (UUID).
- razao_social.
- nome_fantasia.
- cnpj.
- email.
- telefone.
- status.
- plano_id.
- data_criacao.
- data_expiracao.

## 6.2. Isolamento de dados

Todas as tabelas operacionais deverão possuir uma referência ao tenant.

Exemplo:

```sql
CREATE TABLE produtos (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    codigo_erp VARCHAR(100),
    descricao VARCHAR(255),
    estoque NUMERIC(15,3),
    custo NUMERIC(15,4)
);
```

Consultas deverão respeitar o tenant autenticado:

```sql
SELECT *
FROM produtos
WHERE tenant_id = @TenantId;
```

Não permitir que o frontend determine livremente o tenant utilizado nas consultas.

O contexto deverá ser validado no backend através da identidade autenticada.

Implementar PostgreSQL Row-Level Security (RLS) como camada adicional de proteção.

Todas as operações de leitura e escrita deverão respeitar o isolamento.

Criar testes automatizados que tentem acessar dados de outros tenants e garantam o bloqueio.

## 6.3. Estratégia de crescimento

Inicialmente utilizar:

- Um PostgreSQL central.
- Um schema ou conjunto de tabelas compartilhadas.
- Identificação por tenant_id.
- RLS.
- Índices adequados.

Futuramente, permitir bancos separados para clientes empresariais que necessitem de isolamento adicional.

---

# 7. IDENTIDADE VISUAL

O Nexo Compras deverá possuir uma identidade visual moderna, profissional, clara e intuitiva.

Evitar o uso de interfaces escuras como padrão.

## 7.1. Paleta de cores

| Cor | Código | Aplicação |
|---|---|---|
| Azul principal | #2563EB | Sidebar e identidade |
| Azul claro | #DBEAFE | Destaques |
| Branco | #FFFFFF | Cards |
| Fundo | #F8FAFC | Fundo geral |
| Verde | #16A34A | Sucesso e economia |
| Vermelho | #DC2626 | Erros e alertas |
| Amarelo | #EAB308 | Avisos |
| Texto | #1E293B | Textos principais |
| Cinza | #64748B | Textos secundários |

## 7.2. Diretrizes de interface

- Sidebar lateral.
- Cards com bordas suaves.
- Tabelas modernas.
- Gráficos interativos.
- Indicadores coloridos.
- Ícones consistentes.
- Tipografia moderna.
- Espaçamento padronizado.
- Boa responsividade.
- Feedback visual nas ações.
- Estados de carregamento.
- Mensagens de sucesso e erro.

---

# 8. ESTRUTURA DA SIDEBAR

## PRINCIPAL

- Dashboard.
- Produtos e Estoque.
- Fornecedores.

## COMPRAS

- Nova Cotação.
- Cotações em Andamento.
- Comparação de Ofertas.
- Pedidos de Compra.
- Histórico de Compras.

## ANÁLISE

- Relatórios.
- Economia e Indicadores.
- Análise de Fornecedores.

## SISTEMA

- Integração ERP.
- Status de Sincronização.
- Usuários e Permissões.
- Configurações.

---

# 9. FUNCIONALIDADES DO NEXO COMPRAS

## 9.1. Dashboard

Criar uma tela inicial com visão geral da operação.

Indicadores:

- Total comprado no mês.
- Economia realizada.
- Economia potencial.
- Cotações em andamento.
- Cotações aguardando respostas.
- Pedidos pendentes.
- Fornecedores ativos.
- Produtos com estoque baixo.
- Produtos com estoque zerado.

Gráficos:

1. Evolução mensal das compras.
2. Economia mensal.
3. Fornecedores com maior volume.
4. Comparação de preços.
5. Produtos mais comprados.
6. Distribuição das compras.
7. Variação dos custos de aquisição.

Todos os indicadores deverão respeitar o tenant autenticado.

## 9.2. Produtos e Estoque

Consultar os produtos sincronizados do ERP.

Informações:

- Código.
- Descrição.
- Código de barras.
- Unidade.
- Grupo.
- Categoria.
- Estoque atual.
- Estoque mínimo.
- Custo atual.
- Último preço de compra.
- Fornecedor vinculado.
- Última atualização.

Permitir:

- Pesquisar produtos.
- Filtrar por categoria.
- Filtrar por estoque.
- Visualizar detalhes.
- Configurar estoque mínimo.
- Gerar cotação.
- Consultar histórico de preços.

Identificar automaticamente produtos com estoque baixo.

A sugestão de compra deverá considerar estoque atual, estoque mínimo, estoque desejado e pedidos ainda não recebidos.

## 9.3. Fornecedores

Importar fornecedores do ERP e permitir novos cadastros.

Informações:

- Razão social.
- Nome fantasia.
- CNPJ.
- Inscrição estadual.
- Contato.
- Telefone.
- E-mail.
- Endereço.
- Cidade.
- Estado.
- Condições de pagamento.
- Prazo de entrega.
- Observações.

Histórico individual:

- Produtos fornecidos.
- Total comprado.
- Últimas cotações.
- Últimos pedidos.
- Preços históricos.
- Taxa de resposta.
- Prazo médio de entrega.

## 9.4. Nova Cotação

Permitir criar cotações para diversos fornecedores simultaneamente.

Fluxo:

1. Selecionar produtos.
2. Informar quantidades.
3. Selecionar fornecedores.
4. Definir prazo de resposta.
5. Informar observações.
6. Revisar solicitação.
7. Gerar cotações individuais.
8. Enviar solicitações.

Exemplo:

| Produto | Quantidade | Estoque |
|---|---:|---:|
| Arroz 5 kg | 100 | 12 |
| Feijão 1 kg | 80 | 5 |
| Óleo 900 ml | 60 | 8 |

Cada fornecedor deverá receber uma solicitação independente.

As respostas não poderão ser visualizadas pelos demais fornecedores.

## 9.5. Portal do fornecedor

Criar um portal público controlado por autenticação de acesso à cotação.

Exemplo:

`https://fornecedor.nexocompras.com.br/cotacao/8XK29P`

O fornecedor receberá:

- Link exclusivo.
- Código de acesso independente.

O link e o código não deverão conter informações sensíveis.

Requisitos:

- Identificador aleatório e imprevisível.
- Código de acesso seguro.
- Armazenamento do código em formato protegido.
- Validade configurável.
- Limite de tentativas.
- Rate limiting.
- Revogação de acesso.
- HTTPS obrigatório.
- Registro de visualizações.
- Proteção contra acesso cruzado.

O fornecedor somente poderá visualizar a própria cotação.

### Campos da proposta

| Campo | Descrição |
|---|---|
| Produto | Produto solicitado |
| Quantidade | Quantidade ofertada |
| Preço unitário | Valor por unidade |
| Desconto | Desconto concedido |
| ICMS | Alíquota e valor |
| ICMS ST | Alíquota e valor |
| CST/CSOSN | Classificação tributária |
| CFOP | Código fiscal |
| IPI | Alíquota e valor |
| Frete | Valor do frete |
| Prazo de entrega | Tempo para entrega |
| Pagamento | Condições comerciais |
| Validade | Validade da proposta |
| Observações | Informações adicionais |

O fornecedor poderá:

- Ofertar todos os produtos.
- Ofertar somente alguns itens.
- Alterar a quantidade disponível.
- Informar indisponibilidade.
- Adicionar observações.
- Anexar documentos.
- Salvar rascunho.
- Enviar resposta final.

Os campos fiscais deverão ser validados conforme o cenário tributário da operação.

## 9.6. Comparação de fornecedores

Criar uma tela de comparação das propostas recebidas.

Exemplo:

| Produto | Fornecedor A | Fornecedor B | Fornecedor C |
|---|---:|---:|---:|
| Arroz | R$ 24,50 | R$ 23,90 | R$ 25,00 |
| Feijão | R$ 8,50 | R$ 8,90 | R$ 8,20 |
| Óleo | R$ 7,50 | R$ 7,20 | R$ 7,40 |

Destacar:

- Menor preço.
- Melhor custo efetivo.
- Melhor prazo.
- Melhor condição de pagamento.
- Economia potencial.
- Economia realizada.

O cálculo deverá considerar:

- Preço unitário.
- Quantidade.
- Descontos.
- ICMS.
- ICMS ST.
- IPI.
- PIS/COFINS, quando aplicável.
- Frete.
- Outras despesas.

Não presumir que todos os tributos são recuperáveis ou possuem o mesmo tratamento.

A lógica tributária deverá ser configurável e validada.

Permitir selecionar:

1. Um fornecedor para todos os produtos.
2. Fornecedores diferentes para produtos diferentes.

A aprovação final será sempre realizada pelo comprador.

## 9.7. Pedidos de Compra

Após a aprovação, permitir transformar a cotação em pedido.

Informações:

- Número do pedido.
- Fornecedor.
- Produtos.
- Quantidades.
- Valores.
- Descontos.
- Impostos.
- Frete.
- Total.
- Condições de pagamento.
- Previsão de entrega.
- Observações.

Permitir gerar PDF e registrar o responsável pela aprovação.

## 9.8. Acompanhamento de pedidos

Status:

1. Criado.
2. Enviado.
3. Aguardando confirmação.
4. Confirmado.
5. Em preparação.
6. Em transporte.
7. Recebido.
8. Cancelado.

Registrar todos os eventos em uma linha do tempo.

## 9.9. Histórico de compras

Permitir consultar:

- Compras realizadas.
- Fornecedores.
- Produtos.
- Quantidades.
- Preços.
- Períodos.
- Pedidos.
- Cotações anteriores.

Criar filtros e possibilidade de exportação.

## 9.10. Relatórios e indicadores

Criar relatórios de:

- Total comprado.
- Compras por fornecedor.
- Compras por produto.
- Economia acumulada.
- Economia por cotação.
- Evolução de preços.
- Produtos mais comprados.
- Fornecedores mais utilizados.
- Fornecedores menos utilizados.
- Margem bruta estimada.
- Variação de custos.

Diferenciar economia potencial de economia efetivamente realizada.

A margem bruta deverá ser calculada sobre o preço de venda e o custo correspondente, considerando as limitações dos dados disponíveis.

---

# 10. GESTÃO SAAS E ASSINATURAS

O Nexo Compras deverá possuir um módulo de gestão comercial da plataforma.

## 10.1. Painel administrativo

Funcionalidades:

- Cadastro de clientes.
- Ativação de empresas.
- Bloqueio de empresas.
- Gerenciamento de planos.
- Controle de assinaturas.
- Histórico de pagamentos.
- Gestão de licenças.
- Controle de usuários.
- Monitoramento de consumo.
- Status de integrações.
- Monitoramento de agentes.

## 10.2. Planos comerciais

Exemplo inicial:

| Plano | Mensalidade | Usuários |
|---|---:|---:|
| Inicial | R$ 99 | 2 |
| Profissional | R$ 199 | 5 |
| Empresarial | R$ 399 | 15 |

Os valores são apenas referências comerciais.

Permitir configurar:

- Preço.
- Quantidade de usuários.
- Quantidade de filiais.
- Limite de fornecedores.
- Limite de cotações.
- Armazenamento.
- Integrações disponíveis.
- Recursos adicionais.

## 10.3. Assinaturas

Criar estrutura para:

- Teste gratuito.
- Assinatura mensal.
- Assinatura anual.
- Renovação.
- Cancelamento.
- Inadimplência.
- Suspensão.
- Reativação.

Preparar integração futura com gateway de pagamento.

O sistema deverá registrar pagamentos e alterações de assinatura através de eventos confiáveis, com validação de webhooks.

---

# 11. CENTRAL DE INTEGRAÇÕES

O Nexo Compras deverá ser independente de um único ERP.

A primeira integração será Firebird/BM Informática.

Entretanto, deverá existir uma arquitetura de conectores.

Possíveis integrações futuras:

- Firebird.
- PostgreSQL.
- SQL Server.
- MySQL.
- Outros ERPs.

Cada integração deverá transformar os dados externos para um modelo interno padronizado.

O frontend não deverá depender diretamente da estrutura do ERP.

---

# 12. ESTRUTURA DO BANCO POSTGRESQL

Modelagem inicial sugerida.

## SaaS

- tenants
- planos
- assinaturas
- pagamentos
- usuarios
- perfis
- permissoes
- filiais

## Integração

- agentes
- conexoes_erp
- sincronizacoes
- sincronizacao_erros
- mapeamento_campos

## Compras

- produtos
- estoque
- fornecedores
- cotacoes
- cotacao_itens
- cotacao_fornecedores
- respostas_cotacao
- resposta_cotacao_itens
- pedidos_compra
- pedido_compra_itens
- historico_precos
- historico_compras

## Segurança

- acessos_fornecedor
- sessoes
- notificacoes
- logs_auditoria

Todas as tabelas operacionais deverão possuir tenant_id quando aplicável.

Criar relacionamentos, índices, chaves estrangeiras, restrições e migrations.

---

# 13. ESTRUTURA DE PASTAS

```text
NEXO-COMPRAS/
│
├── frontend/
│   ├── src/
│   │   ├── components/
│   │   ├── layouts/
│   │   ├── pages/
│   │   │   ├── Dashboard/
│   │   │   ├── Produtos/
│   │   │   ├── Estoque/
│   │   │   ├── Fornecedores/
│   │   │   ├── Cotacoes/
│   │   │   ├── Comparacao/
│   │   │   ├── Pedidos/
│   │   │   ├── Historico/
│   │   │   └── Relatorios/
│   │   ├── services/
│   │   ├── hooks/
│   │   ├── contexts/
│   │   ├── types/
│   │   └── styles/
│   │
│   └── package.json
│
├── backend/
│   ├── Controllers/
│   ├── Services/
│   ├── Models/
│   ├── DTOs/
│   ├── Repositories/
│   ├── Data/
│   ├── Security/
│   ├── Middleware/
│   ├── MultiTenancy/
│   └── Program.cs
│
├── agent/
│   ├── Services/
│   ├── Firebird/
│   ├── Sync/
│   ├── Security/
│   ├── Configuration/
│   └── Program.cs
│
├── admin/
│   ├── frontend/
│   └── backend/
│
├── database/
│   ├── migrations/
│   ├── seeds/
│   └── scripts/
│
├── docs/
│   ├── arquitetura.md
│   ├── integracao-firebird.md
│   ├── multi-tenancy.md
│   ├── seguranca.md
│   ├── regras-negocio.md
│   └── api.md
│
└── README.md
```

A estrutura poderá ser ajustada durante a implementação, mantendo a separação de responsabilidades.

---

# 14. SEGURANÇA

A segurança deverá ser considerada desde o início.

Implementar:

- HTTPS obrigatório.
- Autenticação segura.
- Controle de permissões.
- Proteção de senhas.
- Proteção de tokens.
- Rate limiting.
- Validação de entradas.
- Proteção contra SQL Injection.
- Proteção contra CSRF quando aplicável.
- Proteção contra XSS.
- Isolamento multi-tenant.
- PostgreSQL RLS.
- Logs de auditoria.
- Backup automatizado.
- Recuperação de dados.
- Controle de sessões.
- Rotação de credenciais.
- Proteção dos links dos fornecedores.

Considerar a LGPD para o tratamento de dados pessoais e empresariais.

Nunca armazenar senhas ou credenciais em texto puro.

Nunca colocar informações sensíveis em URLs.

---

# 15. HOSPEDAGEM E INFRAESTRUTURA

Hospedar a plataforma em infraestrutura de nuvem.

Infraestrutura inicial recomendada:

| Serviço | Finalidade |
|---|---|
| Azure App Service | Backend |
| Hospedagem Web | Frontend |
| Azure Database for PostgreSQL | Banco central |
| Azure Blob Storage | Documentos e arquivos |
| Azure Monitor | Monitoramento |
| Backup | Recuperação |
| Domínio | Endereços da plataforma |
| SSL/TLS | Comunicação segura |

O projeto deverá permitir:

- Atualização independente dos módulos.
- Monitoramento de disponibilidade.
- Escalabilidade.
- Backups automáticos.
- Recuperação em caso de falhas.
- Controle de custos.
- Separação entre desenvolvimento, homologação e produção.

Não contratar infraestrutura excessiva no início.

Começar com recursos compatíveis com os primeiros clientes e ampliar conforme a demanda.

---

# 16. ETAPAS DE DESENVOLVIMENTO

O projeto deverá ser construído progressivamente.

## FASE 1 — Fundação SaaS

- Criar repositório.
- Configurar frontend.
- Configurar backend.
- Configurar PostgreSQL.
- Criar autenticação.
- Criar cadastro de empresas.
- Implementar tenant_id.
- Implementar isolamento multi-tenant.
- Criar layout principal.
- Criar painel administrativo inicial.

## FASE 2 — Integração Firebird

- Analisar estrutura do BM.FDB.
- Identificar tabelas de produtos.
- Identificar tabelas de fornecedores.
- Identificar tabelas de estoque.
- Identificar campos de preços.
- Desenvolver agente Windows.
- Configurar conexão local.
- Implementar autenticação do agente.
- Implementar sincronização.
- Criar painel de status da integração.

## FASE 3 — Produtos e fornecedores

- Importar produtos.
- Importar fornecedores.
- Sincronizar estoque.
- Criar cadastro manual.
- Criar filtros.
- Criar monitoramento de estoque baixo.

## FASE 4 — Cotações

- Criar nova cotação.
- Selecionar produtos.
- Selecionar fornecedores.
- Criar solicitações individuais.
- Gerar links seguros.
- Implementar portal do fornecedor.
- Criar formulários de resposta.
- Receber propostas.

## FASE 5 — Inteligência comercial

- Comparar propostas.
- Calcular custos efetivos.
- Comparar impostos.
- Destacar melhores ofertas.
- Calcular economia.
- Permitir aprovação.
- Registrar decisões.

## FASE 6 — Pedidos e histórico

- Gerar pedidos.
- Criar PDFs.
- Implementar acompanhamento.
- Criar histórico.
- Registrar preços anteriores.

## FASE 7 — Relatórios

- Dashboard completo.
- Economia realizada.
- Economia potencial.
- Indicadores de fornecedores.
- Análise de compras.
- Margem bruta.
- Relatórios exportáveis.



# FASE 8 — Planos, assinaturas e pagamentos

Objetivo: preparar a plataforma para comercialização como SaaS.

Implementações:

Criar planos comerciais.

Criar cadastro de assinaturas.

Criar período de teste gratuito.

Criar mensalidades.

Criar assinaturas anuais.

Implementar renovação.

Implementar cancelamento.

Implementar controle de inadimplência.

Implementar suspensão de acesso.

Implementar reativação.

Integrar gateway de pagamento.

Implementar webhooks de pagamento.

Criar histórico financeiro.

Criar controle de limites por plano.

Criar painel de assinaturas.

Resultado esperado: o proprietário do Nexo Compras poderá comercializar planos e receber pagamentos recorrentes dos clientes.

# FASE 9 — Gestão de múltiplas filiais

Objetivo: permitir que empresas com várias unidades utilizem a mesma conta.

Implementações:

Criar cadastro de filiais.

Associar filiais à empresa principal.

Permitir integração independente por filial.

Permitir diferentes bancos Firebird.

Criar estoque individual por filial.

Criar fornecedores compartilhados ou exclusivos.

Criar cotações por filial.

Criar pedidos por filial.

Permitir compras centralizadas.

Criar transferências e solicitações internas de reposição.

Criar relatórios consolidados.

Criar indicadores por unidade.

Criar controle de permissões por filial.

Resultado esperado: redes de supermercados e empresas com múltiplas unidades poderão centralizar suas compras e acompanhar individualmente cada estabelecimento.

# FASE 10 — Central de notificações

Objetivo: automatizar a comunicação entre a plataforma, compradores e fornecedores.

Implementações:

Criar central de notificações.

Notificações dentro do sistema.

Integração com e-mail.

Preparar integração com WhatsApp Business API.

Notificar fornecedores sobre novas cotações.

Notificar compradores sobre respostas recebidas.

Notificar sobre cotações próximas do vencimento.

Notificar sobre pedidos aprovados.

Notificar sobre atrasos nas entregas.

Notificar sobre falhas de sincronização.

Criar modelos de mensagens.

Permitir configurar preferências de notificação.

Registrar histórico de envios.

Resultado esperado: usuários e fornecedores receberão notificações automáticas sobre os eventos importantes da operação.

# FASE 11 — Integração com novos ERPs

Objetivo: expandir o mercado de atuação do Nexo Compras.

Implementações:

Criar arquitetura padronizada de conectores.

Criar documentação de integração.

Implementar novos conectores.

Preparar integração com PostgreSQL.

Preparar integração com SQL Server.

Preparar integração com MySQL.

Avaliar outros ERPs comerciais.

Criar mapeamento de campos.

Criar configuração de integração por empresa.

Criar validação dos dados importados.

Implementar sincronização independente por ERP.

Criar monitoramento de conectores.

Resultado esperado: o Nexo Compras poderá atender empresas que utilizam diferentes sistemas comerciais, não ficando limitado ao BM Informática e ao Firebird.

# FASE 12 — Aplicativo mobile e PWA

Objetivo: melhorar a experiência de utilização em dispositivos móveis.

Implementações:

Adaptar todas as telas para celulares.

Implementar Progressive Web App (PWA).

Permitir instalação na tela inicial.

Criar notificações compatíveis.

Criar dashboard mobile.

Criar consulta de estoque.

Criar consulta de cotações.

Permitir aprovações pelo celular.

Criar acompanhamento de pedidos.

Permitir consulta de fornecedores.

Otimizar tabelas e gráficos para telas menores.

Futuramente, avaliar o desenvolvimento de aplicativos nativos para Android e iOS.

Resultado esperado: compradores e gestores poderão utilizar as principais funcionalidades da plataforma pelo celular.

# FASE 13 — Inteligência de compras e previsões de estoque

Objetivo: transformar o Nexo Compras em uma ferramenta inteligente de apoio à decisão.

Implementações:

Analisar histórico de vendas.

Analisar histórico de compras.

Identificar sazonalidade.

Identificar produtos com maior saída.

Estimar necessidade de reposição.

Prever possíveis rupturas de estoque.

Sugerir quantidades de compra.

Identificar variações anormais de preços.

Comparar preços históricos.

Identificar fornecedores com melhores condições.

Criar alertas de oportunidades.

Desenvolver recomendações de compra.

As previsões deverão utilizar dados históricos suficientes e apresentar a confiabilidade das estimativas.

Não tratar previsões como garantias de demanda.

Resultado esperado: a plataforma poderá recomendar quando comprar, quanto comprar e quais fornecedores apresentam melhores oportunidades.

# FASE 14 — Segurança avançada, auditoria e escalabilidade

Objetivo: preparar a infraestrutura para crescimento e operação comercial em maior escala.

Implementações:

Revisar arquitetura de segurança.

Realizar testes de isolamento multi-tenant.

Implementar testes de invasão autorizados.

Revisar permissões.

Fortalecer autenticação.

Implementar autenticação multifator.

Revisar gerenciamento de credenciais.

Implementar logs avançados.

Implementar trilhas de auditoria.

Configurar monitoramento de disponibilidade.

Configurar alertas operacionais.

Melhorar políticas de backup.

Testar recuperação de desastres.

Implementar filas de processamento.

Implementar processamento em segundo plano.

Otimizar consultas PostgreSQL.

Implementar cache quando necessário.

Realizar testes de carga.

Avaliar escalabilidade horizontal.

Implementar políticas de retenção de dados.

Revisar conformidade com a LGPD.

Resultado esperado: plataforma preparada para atender um número crescente de empresas com segurança, disponibilidade e desempenho.

FASE 15 — Lançamento comercial, onboarding e suporte SaaS

Objetivo: preparar o produto para comercialização e operação com clientes reais.

Implementações:

Criar site institucional.

Criar landing page comercial.

Criar apresentação do produto.

Criar página de planos e preços.

Criar processo de contratação.

Criar onboarding de empresas.

Criar tutorial de instalação do agente.

Criar documentação para clientes.

Criar central de ajuda.

Criar sistema de suporte.

Criar canal de atendimento.

Criar processo de demonstração comercial.

Criar ambiente de testes.

Criar política de privacidade.

Criar termos de uso.

Criar contratos de assinatura.

Realizar testes com empresas piloto.

Corrigir problemas identificados.

Preparar campanha de lançamento.

Monitorar indicadores de aquisição e retenção.

Resultado esperado: Nexo Compras pronto para ser comercializado como SaaS, com processo de contratação, implantação, cobrança, suporte e evolução contínua.

# 17. DIRETRIZES PARA IA DE PROGRAMAÇÃO

Estas instruções deverão ser respeitadas durante todo o desenvolvimento.

1. Desenvolver um produto SaaS real, não apenas um protótipo visual.
2. Priorizar arquitetura multi-tenant desde o início.
3. Nunca misturar dados entre empresas.
4. Separar frontend, backend e agente local.
5. Manter código modular.
6. Utilizar componentes reutilizáveis.
7. Não presumir estrutura do banco Firebird.
8. Analisar o banco antes de criar consultas.
9. Não alterar o ERP original sem autorização.
10. Não expor Firebird à internet.
11. Não utilizar dados fictícios como se fossem reais.
12. Implementar validações.
13. Implementar tratamento de erros.
14. Criar logs.
15. Documentar APIs.
16. Criar migrations.
17. Criar testes automatizados.
18. Implementar segurança desde o começo.
19. Manter identidade visual clara.
20. Testar funcionalidades antes de avançar.
21. Não desenvolver todas as funcionalidades de uma única vez.
22. Atualizar a documentação conforme o projeto evoluir.

Sempre apresentar quais arquivos foram criados ou modificados e explicar as alterações relevantes.

Não substituir funcionalidades existentes sem necessidade.

Antes de implementar uma nova funcionalidade, verificar a arquitetura e os módulos já existentes.

---

# 18. RESULTADO FINAL ESPERADO

O Nexo Compras deverá ser uma plataforma SaaS capaz de atender múltiplas empresas simultaneamente.

Cada empresa poderá integrar seu ERP local, consultar seus produtos, identificar necessidades de reposição, solicitar cotações, receber propostas de fornecedores, comparar preços e gerar pedidos de compra.

A plataforma deverá centralizar os dados necessários na nuvem, garantindo isolamento, segurança, disponibilidade e escalabilidade.

O proprietário da plataforma deverá conseguir gerenciar clientes, planos, assinaturas e integrações por meio de um painel administrativo exclusivo.

O produto deverá estar preparado para atender:

- Supermercados.
- Mercadinhos.
- Distribuidoras.
- Atacarejos.
- Lojas de varejo.
- Farmácias.
- Empresas de diferentes segmentos.

A arquitetura deverá permitir adicionar novos ERPs e integrações sem reconstruir o sistema.

**Nexo Compras — Conectando empresas, fornecedores e melhores oportunidades de compra.**