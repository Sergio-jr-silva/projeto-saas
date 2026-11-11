# Nexo Compras — Instalador do Sincronizador Local (SINc)

Este diretório contém o instalador oficial e autocontido do **SINc (Sincronizador Local de ERP)** para implantação em computadores e servidores de clientes.

---

## 📦 Arquivo do Instalador

| Arquivo | Versão | Arquitetura | Tamanho Aprox. | Descrição |
| :--- | :---: | :---: | :---: | :--- |
| **`NexoCompras_SINc_Setup.exe`** | `2.4.0` | `x64` | ~44.8 MB | Instalador completo, self-contained (.NET 9 embutido) |

---

## 🚀 Como Instalar no Computador do Cliente

1. **Executar como Administrador**:
   - Dê um duplo clique em `NexoCompras_SINc_Setup.exe`.
   - Se o Windows SmartScreen solicitar confirmação, clique em *Mais informações* e depois em *Executar assim mesmo*.
   - Conceda permissão de Administrador quando solicitado pelo UAC.

2. **Passo a Passo do Assistente**:
   - **Local de Destino**: O instalador selecionará automaticamente `C:\Program Files\Nexo Compras\SINc\`.
   - **Tarefas Adicionais**:
     - *(Opcional)* Criar atalho na Área de Trabalho.
     - *(Recomendado)* Iniciar automaticamente com o Windows.
   - **Conclusão**: Marque a caixa para iniciar o SINc imediatamente ao clicar em *Concluir*.

3. **Primeira Configuração**:
   - Na primeira execução, o SINc abrirá solicitando o **Token de Integração da Filial** gerado no Painel Web Administrativo do Nexo Compras.
   - Digite o Token e o CNPJ-base da empresa.
   - Na aba **Banco de Dados ERP**, selecione o motor do seu sistema (Firebird ou PostgreSQL).
   - Use o botão **🔌 Executar Diagnóstico de Conectividade** para validar a comunicação antes de salvar.

---

## 🛡️ Atualizações e Preservação de Dados

- **Atualizações Sem Perda de Dados**: Ao executar uma nova versão do instalador em uma máquina que já possui o SINc instalado, o assistente detecta a versão anterior, atualiza apenas os binários executáveis e **mantém intactas todas as credenciais criptografadas via Windows DPAPI, tokens e filas offline**.
- **Desinstalação Segura**: Caso o cliente desinstale o SINc pelo Painel de Controle, o desinstalador pergunta se deseja preservar as credenciais locais ou excluí-las por completo.

