# NEXO COMPRAS — GUIA DE EXECUÇÃO EM OUTROS COMPUTADORES

Este documento explica como executar os componentes do **Nexo Compras** (`BACKEND`, `FRONTEND` e `SINc`) em um ou múltiplos computadores Windows conectados em rede local (LAN) ou pela Internet.

---

## 1. Cenários de Execução

Você pode homologar e testar o sistema em dois modelos:

### Cenário A: Todos os componentes no mesmo computador de testes
- **Backend API:** Executa em `http://localhost:5000`
- **Frontend Web:** Executa em `http://localhost:5173`
- **SINc:** Executa na mesma máquina e conecta em `http://localhost:5000`
- **Firebird:** Executa localmente na porta `3050` com a base `BANCO_EXEMPLO\BM.FDB`

### Cenário B: Arquitetura Distribuída (Testes em Outros Computadores)
- **Computador Servidor (onde a API e Banco rodam):** IP da rede local (ex.: `192.168.1.50`).
- **Computador Cliente 1 (Operador de Compras):** Acessa o navegador em `http://192.168.1.50:5173`.
- **Computador Cliente 2 (Servidor do ERP com SINc):** Executa o `SINc.exe` apontando para `http://192.168.1.50:5000` e lê o ERP Firebird local.
- **Fornecedor Externo:** Acessa o link de cotação `http://192.168.1.50:5173/portal/cotacao/{token}` pelo celular ou qualquer outro computador.

---

## 2. Passo a Passo: Iniciando o Backend (API)

1. No computador que atuará como servidor da API, navegue até:
   ```text
   NEXO_COMPRAS_TESTE\BACKEND\Publicacao\
   ```
2. Abra o arquivo `appsettings.json` e verifique a string de conexão com o PostgreSQL:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Host=127.0.0.1;Port=5433;Database=nexo;Username=postgres;Password=postgres"
   }
   ```
   *(Caso seu PostgreSQL esteja na porta 5432 padrão ou em outro IP, altere `Port` e `Host` conforme necessário).*
3. Execute o binário da API:
   ```powershell
   .\Nexo.Api.exe
   ```
4. A API iniciará escutando em **todas as interfaces de rede** (`http://0.0.0.0:5000`).
   - Acesso local: `http://localhost:5000`
   - Acesso por outros computadores: `http://<IP_DO_SERVIDOR>:5000`
   - Documentação Swagger: `http://localhost:5000/swagger`

---

## 3. Passo a Passo: Executando o Frontend Web

A pasta `FRONTEND\dist` contém o aplicativo React/TypeScript 100% estático compilado.

### 3.1. Servindo o Frontend
Você pode disponibilizar o Frontend através de qualquer utilitário de servidor HTTP. Exemplos:

- **Opção 1 — Usando Node.js (npx serve):**
  ```powershell
  npx serve -s FRONTEND\dist -l 5173
  ```
- **Opção 2 — Usando Python:**
  ```powershell
  python -m http.server 5173 -d FRONTEND\dist
  ```
- **Opção 3 — Usando IIS ou Nginx no Windows:**
  Aponte a raiz do site para a pasta `FRONTEND\dist`.

### 3.2. Acesso em Outro Computador
Abra o navegador no computador remoto e acesse:
```text
http://<IP_DO_SERVIDOR>:5173
```
O frontend detecta automaticamente o IP acessado e direciona as chamadas de API para `http://<IP_DO_SERVIDOR>:5000/api`. Se preferir definir um endereço fixo, edite o arquivo `FRONTEND\dist\config.js`.

---

## 4. Passo a Passo: Executando o SINc (Sincronizador Local ERP)

1. No computador onde o ERP ou o banco Firebird está instalado, copie a pasta:
   ```text
   NEXO_COMPRAS_TESTE\SINc\
   ```
2. Dê um duplo-clique no executável `SINc.exe`.
   - **Nota de Compatibilidade:** O `SINc.exe` é **Self-Contained**. Você **NÃO** precisa instalar o .NET SDK ou Runtime neste computador.
   - O aplicativo abre com janela gráfica nativa (Windows Forms) e ícone próprio, sem abrir janela de prompt CMD.
3. Clique no botão de engrenagem no canto superior direito (**"Configurações"**):
   - **Aba "🏢 Identificação & Token":**
     * **URL da API Nexo Compras:** Se o SINc estiver em outro computador, informe `http://<IP_DO_SERVIDOR>:5000` (se estiver no mesmo computador, mantenha `http://localhost:5000`).
     * **Token de Integração da Filial:** Cole o token gerado no painel Web da empresa.
     * **CNPJ-Base:** Informe os 8 primeiros dígitos do CNPJ da empresa.
     * Clique em **"🔑 Validar Token e Conectar"**. O sistema confirmará a identificação e criptografará as credenciais via Windows DPAPI.
   - **Aba "🗄️ Banco de Dados ERP":**
     * Selecione **Firebird**.
     * **Host:** `127.0.0.1` | **Porta:** `3050`.
     * **Caminho do Banco:** Indique o caminho do arquivo `BM.FDB` (disponível na pasta `BANCO_EXEMPLO\BM.FDB`).
     * **Usuário:** `SYSDBA` | **Senha:** `masterkey` | **Charset:** `NONE` | **Dialeto:** `3`.
     * Clique em **"Testar Conexão"**. Ao receber a mensagem de sucesso, clique em **"Salvar Configurações"**.
4. Na tela principal, clique em **"🔄 Sincronizar"**:
   - Os produtos, estoque e fornecedores serão introspeccionados e enviados em lotes para a API.
   - O log exibirá cada etapa em verde com data e hora.

---

## 5. Regras de Firewall do Windows

Caso os computadores não consigam se comunicar na rede local, certifique-se de liberar as seguintes portas no Firewall do Windows do computador Servidor:

| Porta | Protocolo | Aplicação | Direção |
| :---: | :---: | :--- | :---: |
| **5000** | TCP | Backend API (Nexo.Api.exe) | Entrada (Inbound) |
| **5173** | TCP | Frontend Web | Entrada (Inbound) |
| **3050** | TCP | Firebird Server | Entrada (Inbound) |
| **5433 / 5432** | TCP | PostgreSQL | Entrada (Inbound) |

