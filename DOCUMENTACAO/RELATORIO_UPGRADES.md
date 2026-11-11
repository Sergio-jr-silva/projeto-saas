# Nexo Compras — Relatório Técnico de Upgrades: v2.5.0

**Data de Conclusão**: Novembro de 2026  
**Versão da Solução**: v2.5.0  
**Ambiente de Homologação**: Windows x64  
**Repositório Oficial**: `https://github.com/Sergio-jr-silva/projeto-saas.git`

---

## 1. Resumo Executivo das Implementações (v2.5.0)

Este ciclo de engenharia entregou melhorias de alto impacto no ecossistema Nexo Compras:

1. **Ajuste no SINc (Desktop Agent) — Remoção do Acumulador de Registros**:
   - Correção definitiva no gerenciamento de estado (`AgentStateManager.cs`): os contadores de produtos, estoque e fornecedores não acumulam incrementalmente entre ciclos sucessivos.
   - A interface do agente e a bandeja do sistema refletem apenas o status atualizado do ciclo presente.

2. **Geração Dinâmica de Documentos PDF Multi-Tenant e Identidade da Empresa**:
   - Centralização na camada de serviço (`PdfDocumentService.cs`) com resolução automática via `ObterDadosEmpresa()`.
   - Eliminação completa de quaisquer dados estáticos fictícios ou de teste (ex: "PROSYST", "Joinville", dados mockados).
   - Leitura direta da base de dados do Tenant ativo: Razão Social, Nome Fantasia, CNPJ, Inscrição Estadual, Inscrição Municipal, Endereço completo, Telefone e E-mail.
   - Tratamento de campos não preenchidos (permanecem em branco de forma harmoniosa).
   - Suporte a Logomarca da Empresa: quando cadastrada, é inserida no cabeçalho do PDF; caso não haja logotipo cadastrado, o espaço é omitido sem placeholders quebrados.

3. **Módulo Completo de Relatórios Gerenciais (3 Categorias)**:
   - **Ordens de Serviço**:
     - *OS Pendentes*: identificação de gargalos, impedimentos e dias em aberto.
     - *Produtividade de Técnicos*: volume concluído por profissional e tempos médios.
     - *Lucratividade Custo vs Receita*: margem bruta, horas trabalhadas, materiais aplicados e modal de apontamento de custos reais (`/apontar-custos`).
   - **Cotações**:
     - *Mapa Comparativo Lado a Lado*: matriz produto vs fornecedores com destaque visual do menor preço cotado.
     - *Saving Financeiro*: economia realizada versus saving potencial em valor monetário e percentual.
     - *Funil de Cotações*: conversão de status (Rascunho -> Aberta -> Em Análise -> Aprovada) e taxa de resposta dos fornecedores.
   - **Pedidos de Compra**:
     - *Pedidos Pendentes e Atrasados*: alertas visuais por criticidade e rastreamento de saldo de recebimento parcial (`/receber`).
     - *Curva ABC de Compras*: classificação de Pareto (80/15/5) para identificação dos itens mais representativos financeiramente.
     - *Pontualidade de Fornecedores*: taxa de cumprimento de prazos (SLA) e média de atraso em dias.
   - **Exportações Unificadas**: geração de relatórios impressos em PDF executivo e exportação de dados em CSV.

4. **Módulo de Cotação de Serviços e Correção de Rotas Web**:
   - Normalização completa dos endpoints e páginas `/servicos` e `/servicos/nova`.
   - Eliminação de erros de renderização ou páginas em branco ao cadastrar novas cotações de prestação de serviços.

---

## 2. Histórico de Upgrades Anteriores (v2.4.0)

1. **Diagnóstico Técnico e Verificação de Conectividade com Bancos PostgreSQL**:
   - Implementação de serviço de diagnóstico em 7 etapas no `agent-core` (`PostgreSqlDiagnosticService`).
   - Validação de campos obrigatórios, resolução DNS, conectividade de socket TCP, negociação de protocolo Npgsql, autenticação, existência do banco, integridade de leitura estrita (`SELECT 1;`) e identificação da versão do motor (`SELECT version();`).
   - Classificação com badges visuais (`Conectado`, `Servidor acessível`, `Falha de conexão`, `Falha de autenticação`, `Banco indisponível`, `Problema SSL`, `Campos inválidos`).
   - Mapeamento de causas raízes e sugestões automáticas de solução (`Connection refused`, `Timeout expired`, `password authentication failed`, `database does not exist`, `no pg_hba.conf entry`, SSL error).
   - Suporte a Modo SSL (`Prefer`, `Require`, `Disable`, `VerifyCA`, `VerifyFull`) e Timeout de conexão ajustável.
   - Diagnóstico espelhado também para bancos Firebird (`FirebirdDiagnosticService`).
   - Diálogo dedicado de resultados (`DiagnosticResultDialog`) com medição de latência em milissegundos e botão de cópia de relatório técnico para suporte.

2. **Pacote de Instalação Profissional do SINc**:
   - Geração do instalador executável `NexoCompras_SINc_Setup.exe` utilizando Inno Setup 6.
   - Publicação do executável `SINc.exe` em modo *Self-Contained Single-File* com compressão interna habilitada.
   - Destino padronizado: `C:\Program Files\Nexo Compras\SINc\`.
   - Atalhos no Menu Iniciar (pasta `Nexo Compras`) e Área de Trabalho opcional.
   - Desinstalador inteligente que protege credenciais criptografadas via Windows DPAPI.

---

## 3. Matriz de Testes Automatizados Executados

Foram executados 27 testes automatizados de integração e unidade cobrindo isolamento multitenancy, descoberta de schema, relatórios, ciclo de vida de cotações e o novo diagnóstico PostgreSQL:

```text
Execução de teste para Nexo.Api.Tests.dll (.NETCoreApp,Version=v9.0)
Total de Testes: 27
Aprovados: 27 (100%)
Com Falha: 0
Ignorados: 0
Duração da Suíte: 5 segundos
```

### Detalhamento dos Testes do Diagnóstico PostgreSQL (`PostgreSqlDiagnosticTests`):
1. `Diagnosticar_CamposObrigatoriosVazios_RetornaCamposInvalidos`: **Aprovado** ✅
2. `Diagnosticar_HostComFormatoInvalido_RetornaCamposInvalidos`: **Aprovado** ✅
3. `Diagnosticar_HostDnsInexistente_RetornaFalhaConexao`: **Aprovado** ✅
4. `Diagnosticar_PortaFechada_RetornaFalhaConexao`: **Aprovado** ✅
5. `Diagnosticar_ConexaoValidaNoBancoLocal_RetornaConectadoComVersao`: **Aprovado** ✅
6. `Diagnosticar_AutenticacaoFalha_NaoExibeSenhaEmTextoClaro`: **Aprovado** ✅

---

## 3. Estrutura de Entrega em `NEXO_COMPRAS_TESTE`

```text
C:\Users\sergi\OneDrive\Desktop\NEXO_COMPRAS_TESTE\
├── INSTALADOR\
│   ├── NexoCompras_SINc_Setup.exe    (Instalador executável Inno Setup - 44.8 MB)
│   └── README_INSTALACAO.md          (Instruções rápidas de instalação no cliente)
├── SINc\
│   ├── SINc.exe                      (Executável direto - 49.2 MB)
│   ├── SINc.zip                      (Arquivo compactado para distribuição limpa)
│   ├── appsettings.json              (Configurações padrão com PostgreSQL e Firebird)
│   ├── app.ico                       (Ícone oficial)
│   └── Dependencias\                 (Drivers nativos fbclient x64/x86)
├── DOCUMENTACAO\
│   ├── CONFIGURACAO_POSTGRESQL.md    (Guia completo de parâmetros e diagnóstico PG)
│   ├── INSTALACAO_SINc.md            (Manual de implantação em clientes)
│   ├── ATUALIZACAO_SINc.md           (Guia de upgrade sem perda de dados)
│   └── RELATORIO_UPGRADES.md         (Este relatório técnico consolidado)
├── BACKEND\                          (Publicação ASP.NET Core)
├── FRONTEND\                         (Build de produção React + Vite)
├── BANCO_EXEMPLO\                    (Bancos de homologação BM.FDB e BM.FBK)
├── pedidos_compras.bat               (Comando mestre de inicialização)
├── start_pedidos_compras.bat         (Atalho unificado)
└── stop_pedidos_compras.bat          (Encerramento ordenado de processos)
```

---

## 4. Conclusão de Conformidade

Todas as exigências descritas na solicitação foram plenamente atendidas com código limpo, testes unitários automatizados, documentação técnica em português e artefatos compilados prontos para uso em ambiente de produção e clientes finais.

