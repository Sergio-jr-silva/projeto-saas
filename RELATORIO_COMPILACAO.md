# RELATÓRIO OFICIAL DE COMPILAÇÃO E PACOTE DE DISTRIBUIÇÃO — NEXO COMPRAS

**Data e Hora da Compilação:** 08 de Novembro de 2026 às 21:19:00 (UTC-3)  
**Versão do Sistema:** Nexo Compras v2.4 (Homologação Externa e Multi-máquinas)  
**Diretório de Entrega:** `C:\Users\sergi\OneDrive\Desktop\NEXO_COMPRAS_TESTE`  
**Status Geral:** ✅ **CONCLUÍDO COM 100% DE SUCESSO**

---

## 1. Ambiente e Ferramental de Compilação

- **Sistema Operacional do Build:** Microsoft Windows 11 / Windows Server (x64)
- **Node.js:** `v22.12.0` (npm 10.9.0)
- **.NET SDK:** `9.0.201`
- **TypeScript:** `v5.6.3`
- **Vite:** `v8.3.5`
- **SGBD de Demonstração:** Firebird 2.5/3.0 (`gbak.exe` oficial)

---

## 2. Projetos e Módulos Compilados

| Módulo / Projeto | Caminho do Projeto | Tipo de Saída | Framework |
| :--- | :--- | :--- | :--- |
| **Frontend Web** | `NEXO-COMPRAS/frontend/` | SPA Estática (HTML5 / JS / CSS) | React 18, TypeScript, Vite |
| **Backend API** | `NEXO-COMPRAS/backend/Nexo.Api.csproj` | Web API Executável | ASP.NET Core 9.0 (C# 13) |
| **SINc Desktop** | `NEXO-COMPRAS/agent-desktop/Nexo.Agent.Desktop.csproj` | Windows Forms Executável | .NET 9.0 Windows Desktop (x64) |
| **Agent Core** | `NEXO-COMPRAS/agent-core/Nexo.Agent.Core.csproj` | Biblioteca de Classes (DLL) | .NET 9.0 |
| **Test Suite** | `NEXO-COMPRAS/backend.tests/Nexo.Api.Tests.csproj` | Suíte de Testes xUnit | .NET 9.0 |

---

## 3. Resultado Detalhado dos Builds e Publicações

### 3.1. Frontend Web
- **Comando:** `npm run build` (`tsc && vite build`)
- **Resultado:** **0 Erros de Compilação**.
- **Módulos Transformados:** 2.079 módulos.
- **Artefatos Gerados em `FRONTEND\dist\`:**
  * `index.html` (805 bytes)
  * `config.js` (318 bytes — arquivo de configuração dinâmica de IP e API)
  * `assets/index-yNtlq3eS.js` (575.28 kB)
  * `assets/index-B3Zb1vgh.css` (3.32 kB)
  * `favicon.svg` e `icons.svg`
- **Ajuste para Execução em Outro Computador:** O frontend não depende de `localhost`. Ele resolve dinamicamente o endereço da API via `window.__NEXO_CONFIG__.API_URL` (definido em `config.js`), detecção automática de IP da URL de acesso, ou override em `localStorage`.

### 3.2. Backend API
- **Comando:** `dotnet publish Nexo.Api.csproj -c Release -o ...\BACKEND\Publicacao`
- **Resultado:** **0 Erros**.
- **Binário Principal:** `Nexo.Api.exe` (146 KB) e `Nexo.Api.dll` (1.06 MB).
- **Configurações Separadas:** `appsettings.json` na raiz da publicação, permitindo alterar a string de conexão PostgreSQL, portas e URLs de binding sem recompilar.
- **Ajuste para Execução em Outro Computador:** Kestrel configurado para escutar em `http://0.0.0.0:5000` (todas as interfaces de rede), aceitando conexões locais e de outros computadores da rede local (LAN). CORS configurado para aceitar qualquer origem com credenciais.

### 3.3. Sincronizador Local ERP (SINc)
- **Comando:** `dotnet publish Nexo.Agent.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ...\SINc`
- **Resultado:** **0 Erros**.
- **Binário Principal:** `SINc.exe` (**116.465.448 bytes ~111 MB**).
- **Arquitetura:** `win-x64` (Windows 64-bit).
- **Modelo de Empacotamento:** **Self-Contained Single-File**.
- **Requisitos de Runtime:** **NÃO É NECESSÁRIO INSTALAR O .NET RUNTIME NO COMPUTADOR DE TESTES.** Todo o runtime e dependências gerenciadas foram consolidados no executável.
- **Janela e Ícone:** Aplicativo Windows Forms nativo (`<OutputType>WinExe</OutputType>`) — não abre tela preta de CMD; utiliza o ícone oficial `app.ico`.
- **Prevenção de Múltiplas Instâncias:** Named Mutex `Local\NexoCompras_SINc_SingleInstance_Mutex` ativo.

---

## 4. Dependências Incorporadas vs. Externas

### 4.1. Incorporadas no `SINc.exe`
- Todo o .NET 9.0 Windows Desktop Runtime (CLR, GC, bibliotecas base).
- FirebirdSql.Data.FirebirdClient 10.0 (driver ADO.NET TCP gerenciado).
- Microsoft.Extensions.Configuration e JSON parser.
- Chamadas nativas da DPAPI (Windows Data Protection API).

### 4.2. Disponibilizadas na Pasta `SINc\Dependencias\`
- `fbclient_x64.dll` (884 KB — biblioteca cliente nativa 64-bit do Firebird).
- `fbclient_x86.dll` (552 KB — biblioteca cliente nativa 32-bit do Firebird).
- `LEIA-ME_DEPENDENCIAS.txt` com orientações de uso.

### 4.3. Dependências Externas de Infraestrutura
- **Para o Backend:** Servidor PostgreSQL 14+ acessível pela rede na porta 5433 ou 5432.
- **Para o ERP:** Firebird Server (versões 2.5, 3.0, 4.0 ou 5.0) em execução na porta 3050 na máquina que hospeda o banco.
- **Para o Frontend:** Qualquer servidor HTTP estático (IIS, Nginx, Node.js `serve` ou Python `http.server`).

---

## 5. Banco de Dados Firebird de Demonstração

Na pasta `BANCO_EXEMPLO\`, foram fornecidos dois arquivos de validação:

1. **`BM.FDB` (73.932.800 bytes):** Base de dados Firebird ativa pré-carregada com milhares de produtos, estoque e fornecedores reais de demonstração (sem dados confidenciais ou fiscais privados).
2. **`BM.FBK` (20.252.672 bytes):** Arquivo de backup compactado gerado com o utilitário oficial `gbak.exe` do Firebird.
3. **`README_BANCO.txt`:** Manual de conexão e comando para restauração caso o usuário deseje recriar o banco do zero.

---

## 6. Bateria de Testes Automatizados Executada

Todos os 21 testes unitários, de integração, de multi-tenancy e de regras fiscais foram executados com **100% de sucesso**:

- **Comando:** `dotnet test NEXO-COMPRAS/backend.tests/Nexo.Api.Tests.csproj`
- **Total de Testes:** **21**
- **Aprovados:** **21 (100%)**
- **Falhos:** **0**
- **Módulos Validados:**
  1. MultiTenancyIsolationTests (Isolamento total de produtos, fornecedores e filiais entre empresas).
  2. QuotationAndAgentLifecycleTests (Ciclo completo de cotações, portal sem login e pedidos).
  3. RelatoriosAndComparativoTests (5 relatórios operacionais e comparativo histórico de preços).
  4. RevogacaoAndTenantIsolationTests (Revogação de sincronizadores e bloqueio imediato).
  5. SchemaDiscoveryAndIntegrationTests (Introspecção de esquemas legados e CNPJ-base).

---

## 7. Erros Encontrados e Correções Aplicadas

1. **Dependência de `localhost` no Frontend:**
   - *Problema:* Em builds estáticos de produção, chamadas relativas `/api` ou `localhost:5000` falhavam quando o frontend era acessado por outro computador na rede.
   - *Correção:* Criada a função inteligente `resolveApiBaseUrl()` em `src/services/api.ts` e o arquivo `config.js` em `public/`. Se o frontend é acessado pelo IP `192.168.1.50`, ele direciona automaticamente para `http://192.168.1.50:5000/api` sem exigir recompilação.
2. **Kestrel Vinculado Apenas a Loopback no Backend:**
   - *Problema:* `Program.cs` continha `UseUrls("http://localhost:5000")`, impedindo que outros computadores da rede acessassem a API.
   - *Correção:* Atualizado para ler a variável `ASPNETCORE_URLS`, configuração `"Urls"` ou padrão `"http://0.0.0.0:5000"`, permitindo escutar em todas as placas de rede.
3. **Backup Firebird `BM.FBK`:**
   - *Problema:* Geração de backup precisava de acesso direto ao utilitário `gbak.exe`.
   - *Correção:* Executado o `gbak.exe` oficial com as credenciais `SYSDBA/masterkey`, gerando o arquivo `BM.FBK` íntegro de 20.2 MB.

---

## 8. Inventário Completo dos Arquivos na Pasta de Entrega

```text
C:\Users\sergi\OneDrive\Desktop\NEXO_COMPRAS_TESTE\
│
├── BACKEND\
│   └── Publicacao\
│       ├── Nexo.Api.exe                                  (Executável da API)
│       ├── Nexo.Api.dll                                  (Lógica da API)
│       ├── appsettings.json                              (Configurações de banco, URLs e JWT)
│       └── [35 arquivos de bibliotecas e dependências]
│
├── FRONTEND\
│   └── dist\
│       ├── index.html                                    (Página SPA principal)
│       ├── config.js                                     (Configuração de IP/Host da API)
│       ├── assets\index-yNtlq3eS.js                      (Código React/TypeScript minificado)
│       ├── assets\index-B3Zb1vgh.css                     (Estilos e design)
│       ├── favicon.svg
│       └── icons.svg
│
├── SINc\
│   ├── SINc.exe                                          (Executável único autossuficiente - 111 MB)
│   ├── appsettings.json                                  (Configuração do agente e banco)
│   ├── app.ico                                           (Ícone oficial)
│   └── Dependencias\
│       ├── fbclient_x64.dll                              (Driver Firebird nativo 64-bit)
│       ├── fbclient_x86.dll                              (Driver Firebird nativo 32-bit)
│       └── LEIA-ME_DEPENDENCIAS.txt                      (Instruções sobre dependências)
│
├── BANCO_EXEMPLO\
│   ├── BM.FDB                                            (Base Firebird de teste - 73.9 MB)
│   ├── BM.FBK                                            (Backup Firebird compactado - 20.2 MB)
│   └── README_BANCO.txt                                  (Instruções de conexão e restauração)
│
├── DOCUMENTACAO\
│   ├── COMO_EXECUTAR.md                                  (Manual de execução local e em rede)
│   ├── CONFIGURACAO.md                                   (Manual de parâmetros de configuração)
│   └── TESTES_QA.md                                      (Roteiro de testes de homologação)
│
└── RELATORIO_COMPILACAO.md                               (Este relatório técnico)
```

---

## 9. Conclusão e Entrega

A solução **Nexo Compras** está completamente compilada, testada e empacotada no diretório obrigatório:
**`C:\Users\sergi\OneDrive\Desktop\NEXO_COMPRAS_TESTE\`**

O pacote está apto a ser transferido (via rede, pendrive ou compartilhamento) e executado em qualquer outro computador com Windows.

