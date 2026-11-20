# Nexo Compras — Manual de Instalação e Execução do SINc (OneFile)

## Visão Geral
O **SINc (Sincronizador ERP Local)** é o componente desktop independente responsável por conectar o banco de dados local do seu ERP (Firebird, PostgreSQL ou outros) à plataforma SaaS Nexo Compras em nuvem.

Esta versão foi empacotada em formato **Single-File Executable (`SINc.exe`) Self-Contained**, o que significa que:
- **Não requer** a instalação prévia do SDK ou Runtime do .NET no computador de destino.
- **Não abre** janelas pretas de terminal (CMD/Console).
- Pode ser movido ou executado a partir de qualquer pasta local (ex.: `C:\NexoCompras\SINc.exe` ou `Desktop`).
- Todas as dependências nativas e bibliotecas de conexão de banco estão embutidas no executável.

---

## 1. Requisitos do Sistema
- **Sistema Operacional:** Windows 10 (64-bit) ou Windows 11 (64-bit), Windows Server 2016 ou superior.
- **Privilégios:** Execução padrão de usuário (para gravação e criação dos dados em `%ProgramData%\NexoCompras\SINc\`).
- **Conectividade:**
  - Acesso à internet via HTTPS para `https://api.nexoscompras.com.br` (porta 443).
  - Conexão de rede local com o banco de dados do seu ERP (ex.: porta 3050 para Firebird ou 5432 para PostgreSQL).

---

## 2. Instalação e Execução

1. Copie o arquivo `SINc.exe` para o computador onde o ERP está instalado ou em uma máquina da mesma rede local.
2. Crie uma pasta de sua preferência, por exemplo `C:\NexoCompras\`, e cole o `SINc.exe` nela.
3. Dê um duplo clique no `SINc.exe` para abrir a interface gráfica do sincronizador.

> **Importante:** Não é necessário copiar DLLs, pastas temporárias ou arquivos extras. O `SINc.exe` é totalmente autossuficiente.

---

## 3. Configuração Inicial do Sincronizador

Ao abrir o SINc pela primeira vez:

1. **Aba "Identificação & Token":**
   - **Token de Integração da Filial (FLK):** Cole o Token gerado no painel web do Nexo Compras (em *Filiais e Unidades* ou gerado pelo Admin).
   - **CNPJ-Base:** Informe os 8 primeiros dígitos do CNPJ da sua empresa.
   - **URL Base da Plataforma SaaS (API Central):** Certifique-se de que está preenchido com a URL da API:
     ```
     https://api.nexoscompras.com.br
     ```
   - Clique em **Validando...** para verificar a autenticação com o SaaS.

2. **Aba "Banco de Dados ERP":**
   - Selecione o tipo de banco (ex: *Firebird* ou *PostgreSQL*).
   - Informe o Host (ex: `127.0.0.1`), Porta e Caminho do Banco (ex: `C:\Sistemas\DADOS.FDB`).
   - Informe Usuário e Senha do banco ERP.
   - Clique em **Testar Conexão** para assegurar que os dados locais estão acessíveis.

3. **Salvar:**
   - Clique no botão azul **Salvar Configurações** no rodapé.
   - Clique em **Sincronizar Agora** para enviar a primeira carga de estoque e produtos para o Nexo Compras.

---

## 4. Onde os dados e filas são armazenados?

Para garantir que o executável possa ser movido ou atualizado sem perda de histórico, filas offline ou credenciais seguras, todos os dados persistentes ficam armazenados em:

```
%ProgramData%\NexoCompras\SINc\
```
*(Normalmente localizado em `C:\ProgramData\NexoCompras\SINc\`)*

### Estrutura interna:
- `appsettings.json` — Arquivo de configurações de rede e banco de dados.
- `data\sinc_local.db` — Banco local SQLite para controle de hashes e sincronização delta incremental.
- `data\offline_queue.json` — Fila offline resiliente para quedas de conexão de internet.
- `data\agent_credentials.dat` — Chaves de autenticação protegidas por DPAPI do Windows.
- `logs\agent_error.log` — Registro de diagnósticos e logs de erro para suporte técnico.

---

## 5. Execução em Segundo Plano (Bandeja do Sistema / Tray)

- Ao fechar a janela no botão "X", o SINc minimiza para a bandeja do sistema (perto do relógio do Windows), continuando a sincronizar em segundo plano nos intervalos definidos.
- Para reabrir a janela, clique duas vezes no ícone do Nexo Compras na bandeja.
- Para encerrar definitivamente, clique com o botão direito no ícone da bandeja e selecione **Sair**.
