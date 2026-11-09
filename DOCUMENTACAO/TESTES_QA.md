# NEXO COMPRAS — ROTEIRO E MATRIZ DE TESTES DE HOMOLOGAÇÃO (QA)

Este documento orienta a validação completa do **Nexo Compras** quando executado no mesmo computador ou em estações de trabalho distintas na rede.

---

## 1. Matriz de Casos de Teste

| ID | Área | Descrição do Caso de Teste | Procedimento | Critério de Aceitação / Resultado Esperado | Status |
| :---: | :--- | :--- | :--- | :--- | :---: |
| **CT01** | Portabilidade | Execução do SINc em Windows limpo (Sem .NET Runtime) | Copiar pasta `SINc` para uma máquina Windows 10/11 sem SDK ou Runtime do .NET instalado e dar duplo clique em `SINc.exe`. | O SINc inicializa diretamente, exibindo a tela gráfica sem erros de DLL ou solicitação para instalar .NET. | [ ] |
| **CT02** | Interface | Execução sem janela de prompt CMD e com ícone | Executar `SINc.exe` pelo Windows Explorer ou atalho. | O executável abre sem nenhuma janela de console preta (CMD); Exibe o ícone oficial na barra de tarefas e na bandeja (tray). | [ ] |
| **CT03** | Segurança | Ativação do Agente com Token e CNPJ-Base | No SINc, ir em "Configurações" -> "Identificação & Token". Informar URL da API, Token da filial e CNPJ-base (8 dígitos). Clicar em "Validar Token e Conectar". | A API valida a credencial; A razão social e código da filial são exibidos na tela principal; As credenciais são criptografadas localmente com DPAPI. | [ ] |
| **CT04** | Banco Local | Conexão e Introspecção do Firebird `BM.FDB` | Na aba "Banco de Dados ERP", apontar para `BANCO_EXEMPLO\BM.FDB` (Host: 127.0.0.1, Porta: 3050, Usuário: SYSDBA, Senha: masterkey). Clicar em "Testar Conexão". | Conexão estabelecida com sucesso; Identificação correta do dialeto 3 e das tabelas `PRODUTO`, `ESTOQUE` e `FORNECEDOR`. | [ ] |
| **CT05** | Sincronismo | Sincronização Inicial de Catálogo e Estoque | Na tela principal do SINc, clicar no botão "🔄 Sincronizar". | Extração dos registros em lotes (sem travar a interface); Envio com sucesso para a API; Log exibe cada etapa em verde com data/hora. | [ ] |
| **CT06** | Consistência | Ausência de Duplicações e Sincronismo Incremental | Clicar em "🔄 Sincronizar" uma segunda vez após a primeira carga. | Nenhum produto ou fornecedor é duplicado no PostgreSQL; Atualização de estoque reflete o estado mais recente. | [ ] |
| **CT07** | Multitenant | Isolamento Estrito entre Empresas Distintas | 1. No Admin, cadastrar Empresa B com outro CNPJ.<br>2. Gerar Token para Empresa B.<br>3. Logar no Painel Web com o usuário da Empresa B. | A Empresa B vê ZERO produtos, ZERO fornecedores e ZERO cotações da Empresa A. O isolamento é total e inviolável. | [ ] |
| **CT08** | Cotações | Criação de Cotação e Resposta do Fornecedor Remoto | 1. Na Empresa A, criar cotação.<br>2. Copiar o link do portal do fornecedor.<br>3. Abrir o link em outro computador da rede ou celular (`http://<IP>:5173/portal/cotacao/{token}`). | O portal abre diretamente sem exigir login; O fornecedor informa preços, prazos e frete e confirma o envio com sucesso. | [ ] |
| **CT09** | Comparação | Matriz Comparativa e Identificação de Menor Preço | Acessar "Cotações" -> "Comparar Propostas" na Empresa A. | Visualização lado a lado das propostas; Menores preços de cada item destacados em verde; Economia percentual calculada corretamente. | [ ] |
| **CT10** | Pedidos | Aprovação de Cotação e Geração de Pedido de Compra | Na tela de comparação, aprovar o fornecedor vencedor. | Cotação finalizada; Pedido de compra gerado automaticamente com numeração sequencial e listado na tela de Pedidos. | [ ] |
| **CT11** | Histórico | Comparativo de Pedidos & Preços no Histórico | Acessar tela "Histórico" -> Aba "📊 Comparativo de Pedidos & Preços". | Exibição das variações percentuais de custo entre pedidos anteriores e atuais para cada produto. | [ ] |
| **CT12** | Relatórios | Geração e Exportação Excel (CSV BOM) e PDF | Acessar tela "Relatórios", filtrar por período e clicar em "📥 Exportar Excel" e "🖨️ Imprimir / Salvar PDF". | Arquivo CSV formatado com UTF-8 BOM abre com acentuação perfeita no Microsoft Excel; Janela de impressão renderiza o PDF executivo. | [ ] |
| **CT13** | Resiliência | Operação Offline do SINc e Fila de Recuperação | Interromper a API ou desconectar o cabo de rede do computador do SINc. Clicar em "Sincronizar". | O SINc registra aviso no log e enfileira os dados em `data\offline_queue.json`. Ao reestabelecer a rede, os dados são descarregados automaticamente. | [ ] |
| **CT14** | Concorrência | Bloqueio de Instâncias Duplicadas (Single Instance) | Com o SINc já aberto, tentar executar `SINc.exe` uma segunda vez. | O segundo processo avisa que o SINc já está rodando, traz a janela existente para o primeiro plano e se encerra de imediato. | [ ] |
| **CT15** | Auditoria | Revogação Remota de Agente / Filial via Web | No Painel Admin, localizar a filial e clicar em "Revogar Agente". | A credencial é invalidada imediatamente no PostgreSQL; O SINc é bloqueado na próxima requisição com erro de autenticação. | [ ] |

