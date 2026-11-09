# Nexo Compras — Procedimento de Atualização do SINc

Este guia descreve os mecanismos e procedimentos recomendados para atualizar o **SINc (Sincronizador Local de ERP)** em computadores de clientes para versões mais recentes, garantindo **zero perda de dados e zero tempo de reconfiguração**.

---

## 1. Princípios da Arquitetura de Atualização

O instalador do SINc (`NexoCompras_SINc_Setup.exe`) foi concebido com os seguintes princípios de engenharia:

1. **Identificador de Aplicação Fixo (AppId Constante)**:
   - Todas as versões compartilham o mesmo `AppId = {{8B2372A5-88A0-44BC-8F35-1F034B769D9E}}`.
   - O Windows reconhece automaticamente a versão instalada e executa um fluxo de atualização (*In-Place Upgrade*), substituindo apenas os binários necessários sem duplicar entradas no sistema.

2. **Fechamento Automático de Processos em Execução**:
   - A diretiva `CloseApplications=yes` detecta se o `SINc.exe` está aberto na bandeja do sistema e solicita seu fechamento seguro antes de sobrescrever o arquivo, evitando erros de arquivo bloqueado (`sharing violation`).

3. **Preservação Absoluta das Credenciais Locais**:
   - O arquivo `appsettings.json` possui a flag `onlyifdoesntexist`. Portanto, os parâmetros de banco de dados e IP do cliente **nunca são sobrescritos** por valores padrão durante uma atualização.
   - O diretório `data/` (onde residem as credenciais protegidas por Windows DPAPI `agent_credentials.dpapi` e a fila de contingência offline `offline_queue.json`) não é alterado pelo instalador.

---

## 2. Passo a Passo de Atualização no Cliente

1. **Obter a nova versão do instalador**:
   - Baixe a versão atualizada do instalador (ex: `NexoCompras_SINc_Setup.exe` v2.4.0).
2. **Executar o instalador sobre a versão antiga**:
   - Dê um duplo clique no instalador com privilégios de Administrador.
3. **Detecção Automática**:
   - O assistente informará que uma versão anterior do SINc foi detectada na pasta `C:\Program Files\Nexo Compras\SINc\`.
   - Se o SINc estiver em execução, o instalador solicitará permissão para fechá-lo automaticamente.
4. **Instalação dos novos binários**:
   - Os novos executáveis, bibliotecas e melhorias de código serão aplicados em menos de 10 segundos.
5. **Reinício do SINc**:
   - Ao clicar em **Concluir**, o SINc é reiniciado automaticamente.
   - O agente se conectará à nuvem informando sua nova versão (ex: `2.4.0`) via ciclo de Heartbeat, atualizando instantaneamente o status no Painel Administrativo Web da empresa.

---

## 3. Rollback (Retorno à Versão Anterior)

Se for necessário retornar para uma versão anterior por qualquer motivo operacional:
1. Feche o SINc na bandeja do sistema.
2. Execute o instalador da versão anterior.
3. Conclua o assistente normalmente.
4. O SINc iniciará mantendo todas as credenciais e histórico da empresa intactos.
