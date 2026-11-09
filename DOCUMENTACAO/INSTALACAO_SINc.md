# Nexo Compras — Manual de Implantação e Instalação do SINc

Este manual descreve o procedimento de instalação e implantação do **SINc (Sincronizador Local de ERP)** em estações de trabalho e servidores com sistema operacional Microsoft Windows.

---

## 1. Requisitos do Sistema

- **Sistema Operacional**: Windows 10 (64 bits), Windows 11 (64 bits), Windows Server 2016, 2019 ou 2022.
- **Privilégios**: Direitos de Administrador local durante a execução do instalador.
- **Rede Local**: Acesso TCP/IP ao banco de dados do ERP (porta 3050 para Firebird ou 5432 para PostgreSQL).
- **Conectividade Internet**: Acesso HTTPS de saída para a URL da API da nuvem Nexo Compras.
- **Runtime .NET**: **Não é necessário instalar .NET Framework ou .NET 9 Runtime**, pois o SINc foi compilado em modo autocontido (*Self-Contained Single-File*).

---

## 2. Passo a Passo de Instalação

### Passo 1: Obtenção do Instalador
Localize o arquivo executável gerado:
`NexoCompras_SINc_Setup.exe` (localizado na pasta `INSTALADOR/`).

### Passo 2: Execução
1. Clique com o botão direito sobre `NexoCompras_SINc_Setup.exe` e selecione **"Executar como Administrador"**.
2. O assistente de instalação em Português do Brasil será iniciado.
3. Clique em **Avançar**.
4. O caminho de instalação padrão será definido como:
   ```text
   C:\Program Files\Nexo Compras\SINc\
   ```
5. Na tela de tarefas adicionais, você pode optar por:
   - ✅ **Criar um atalho na Área de Trabalho**.
   - ✅ **Iniciar o SINc automaticamente com o Windows (Recomendado)**.
6. Clique em **Instalar**.
7. Ao término da cópia dos arquivos, certifique-se de que a opção **"Iniciar o Nexo Compras SINc"** esteja marcada e clique em **Concluir**.

---

## 3. Configuração Inicial e Ativação do Agente

1. **Ativação por Token e CNPJ-Base**:
   - Acesse o Painel Administrativo Web do Nexo Compras (`http://localhost:5173/admin/sincronizadores`).
   - Localize a empresa/filial do cliente e gere o **Token de Integração** ou código de ativação.
   - Na janela do SINc, informe o Token de Integração e o CNPJ-base (8 primeiros dígitos).
   - Clique em **"🔑 Validar Token e Conectar"**.
   - O SINc validará as credenciais com o servidor SaaS e salvará a chave criptografada com **Windows DPAPI**.

2. **Conexão com o Banco do ERP**:
   - Acesse a aba **🗄️ Banco de Dados ERP**.
   - Escolha o tipo de banco: **Firebird** ou **PostgreSQL**.
   - Preencha os dados do servidor local do cliente.
   - Clique em **"🔌 Executar Diagnóstico de Conectividade"**.
   - Quando o status exibir 🟢 **Conectado**, clique em **"🔍 Identificar Estrutura do Banco (Auto-Detect)"** para que o SINc descubra automaticamente as tabelas e colunas de produtos, estoque e fornecedores.
   - Clique no botão **"💾 Salvar Configurações"**.

---

## 4. Operação em Segundo Plano (Bandeja do Sistema)

- O SINc é projetado para operar 24/7 de forma não intrusiva.
- Ao fechar a janela principal, ele permanece ativo na **Bandeja do Sistema (System Tray)**, ao lado do relógio do Windows.
- Clicando com o botão direito no ícone da bandeja, o operador tem acesso rápido a:
  - Status em tempo real (Online / Offline).
  - Razão Social e Filial identificada.
  - Horário da última sincronização realizada.
  - Acesso ao Diagnóstico Técnico e Configurações.
  - Sincronização manual imediata.
