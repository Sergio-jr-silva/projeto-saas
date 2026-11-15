# Nexo Compras — Plataforma SaaS v2.0
## Gestão Inteligente de Compras e Fornecedores

Plataforma SaaS multi-tenant desenvolvida para conectar empresas a fornecedores, comparar propostas fiscais e automatizar compras a partir de ERPs locais (Firebird/BM Informática e outros).

---

## 🚀 Status do Projeto: FASES 1 A 5 CONCLUÍDAS E TESTADAS

### ✅ Fase 1 — Fundação SaaS Multi-Tenant
- **Isolamento de Dados**: Modelo `ITenantEntity` com Global Query Filters automáticos no EF Core 9 e suporte a Row-Level Security (RLS) no PostgreSQL.
- **Autenticação Multi-Scheme**:
  - JWT Bearer padrão para operadores e administradores das empresas (`TenantAdmin`, `Buyer`, `Viewer`).
  - JWT PlatformAdmin para gestão SaaS global.
  - Rate Limiting integrado contra ataques de força bruta.
- **Painéis**: Login, Onboarding / Cadastro de Empresas com Trial de 15 dias, Dashboard Administrativo SaaS (`/admin`).

### ✅ Fase 2 — Integração ERP BM Informática (Firebird) & Agente Windows
- **Projeto Nexo.Agent (.NET 9 Worker Service)** em `agent/`:
  - Conexão segura com o Firebird local (`BM.FDB`) via `FirebirdSql.Data.FirebirdClient`.
  - Leitura otimizada das tabelas `PRODUTO`, `PRODUTO_ESTOQUE`, `PESSOA (FORNECEDOR='S')`, `ENTRADA_PRODUTO` e `SAIDA_PRODUTO`.
  - Autenticação com o SaaS via cabeçalho `X-Agent-Key: nxk_{agentId}_{segredo}` (token derivado criptograficamente).
  - Ciclo de Heartbeat periódico (60s) com sincronização dinâmica de parâmetros e telemetria da máquina.
  - Sincronização incremental de estoque (5 min) e catálogo de produtos e fornecedores (30 min).
  - Modo de resiliência e relatório de falhas em `/api/agent/v1/erro`.

### ✅ Fase 3 — Produtos, Fornecedores e Monitoramento de Estoque
- **Catálogo Inteligente**:
  - Níveis de estoque automáticos: `Zerado`, `Crítico`, `Baixo` e `Normal`.
  - Cálculo de consumo dos últimos 90 dias, cobertura em dias e sugestão de reposição inteligente considerando lead time e margem de segurança.
  - Edição inline direta do estoque mínimo por item.
  - Seleção múltipla de produtos em alerta para criação imediata de cotação.
  - Gestão de Fornecedores com histórico de preços, taxa de resposta e prazos médios de entrega.

### ✅ Fase 4 — Módulo de Cotações e Portal Seguro do Fornecedor
- **Criação de Cotações Multi-Fornecedor**:
  - Definição de itens, quantidades sugeridas e múltiplos fornecedores convidados.
  - Geração de tokens únicos criptografados e códigos de acesso individuais de 12 caracteres (`XXXX-XXXX-XXXX`).
- **Portal Público do Fornecedor (`/cotacao/{token}`)**:
  - Autenticação sem necessidade de cadastro no SaaS: token + código de segurança de 12 dígitos.
  - Preenchimento interativo com tributação brasileira completa: Preço unitário, Desconto %, CST/CSOSN, CFOP, ICMS %, IPI %, MVA %, ICMS ST (R$).
  - Condições gerais: Prazo de entrega (dias), condição de pagamento, validade da proposta, frete e despesas acessórias.
  - Upload de propostas em PDF, espelhos de pedido e notas fiscais XML.
  - Suporte a salvamento de rascunho e envio final definitivo.
  - Validação fiscal em tempo real de CFOP (5xxx/6xxx) e CSTs.

### ✅ Fase 5 — Inteligência Comercial, Comparação de Ofertas e Aprovação
- **Calculadora de Custo Efetivo Tributário**:
  - Fórmula tributária: $Custo Efetivo = Preço Líquido + IPI + ICMS ST + Frete/Despesas - Créditos (ICMS, IPI, PIS/COFINS)$ conforme o regime tributário da empresa (Simples Nacional, Lucro Presumido ou Lucro Real).
- **Matriz Comparativa Interativa**:
  - Visualização lado a lado de todas as propostas recebidas.
  - Destaque automático do **Melhor Custo Efetivo** e do **Menor Preço Bruto**.
  - Comparativo de prazos de entrega e condições de pagamento.
- **Motor de Recomendação Inteligente**:
  - Recomendação de **Fornecedor Único** ou **Combinação de Itens (Split Order)** para máxima economia financeira.
  - Cálculo em tempo real do Total Aprovado e Economia Realizada ($ e %).
- **Fluxo Formal de Aprovação**:
  - Seleção das ofertas vencedoras pelo comprador com registro de auditoria (`LogAuditoria`).

---

## 🧪 Testes Automatizados (xUnit)

Todos os testes de integração do backend estão 100% aprovados:

```bash
cd NEXO-COMPRAS
dotnet test
```

Resultados:
- `MultiTenancyIsolationTests`: Garantia de isolamento multi-tenant de dados e proteção de rotas restritas.
- `QuotationAndAgentLifecycleTests`:
  1. Criação de cotação multi-fornecedor com tokens e códigos de 12 caracteres.
  2. Autenticação e preenchimento de propostas no Portal do Fornecedor.
  3. Cálculo de comparativo e custo efetivo na matriz.
  4. Aprovação formal de cotação com auditoria.
  5. Autenticação do Agente Local via `X-Agent-Key`, envio de Heartbeat e ingestão de catálogo.

---

## 🛠️ Como Iniciar a Aplicação

### Início Rápido com o Script Automático (Windows)
Basta dar duplo clique no arquivo:
```cmd
iniciar.bat
```
Ou iniciar individualmente:

1. **PostgreSQL** (Porta 5433):
   Utiliza o cluster local já inicializado em `scratch/pgdata`.

2. **Backend API** (Porta 5000):
   ```bash
   cd NEXO-COMPRAS/backend
   dotnet run
   ```

3. **Frontend Web** (Porta 5173):
   ```bash
   cd NEXO-COMPRAS/frontend
   npm run dev
   ```

4. **Agente Windows** (Opcional para sincronizar com Firebird local):
   ```bash
   cd NEXO-COMPRAS/agent
   dotnet run
   ```

---

## 🔑 Credenciais Padrão

- **Administrador Global do SaaS**:
  - E-mail: `admin@nexocompras.com.br`
  - Senha: `Admin@Nexo2026!`
  - Painel: `http://localhost:5173/admin`

- **Novas Empresas**:
  - Cadastre-se em `http://localhost:5173/cadastro` para testar todas as funcionalidades das Fases 1 a 5 com isolamento completo.
