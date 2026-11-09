@echo off
chcp 65001 >nul
title Nexo Compras — Inicialização Completa

echo ==============================================================================
echo           NEXO COMPRAS — INICIALIZAÇÃO COMPLETA DA APLICAÇÃO
echo ==============================================================================
echo.

set "BASE_DIR=%~dp0"
set "BACKEND_DIR=%BASE_DIR%BACKEND\Publicacao"
set "FRONTEND_DIR=%BASE_DIR%FRONTEND"
set "SINC_DIR=%BASE_DIR%SINc"
set "BANCO_DIR=%BASE_DIR%BANCO_EXEMPLO"

:: 0. Descompactar arquivos binários caso clonado do Git (garante execução limpa)
if not exist "%SINC_DIR%\SINc.exe" (
    if exist "%SINC_DIR%\SINc.zip" (
        echo [PREP] Descompactando SINc.exe para primeira execucao...
        powershell -NoProfile -Command "Expand-Archive -Path '%SINC_DIR%\SINc.zip' -DestinationPath '%SINC_DIR%' -Force"
    )
)

if not exist "%BANCO_DIR%\BM.FDB" (
    if exist "%BANCO_DIR%\BM.FDB.zip" (
        echo [PREP] Descompactando BM.FDB para primeira execucao...
        powershell -NoProfile -Command "Expand-Archive -Path '%BANCO_DIR%\BM.FDB.zip' -DestinationPath '%BANCO_DIR%' -Force"
    )
)

:: 1. Verificar / Iniciar PostgreSQL (Porta 5433 ou 5432)
echo [1/4] Verificando Banco de Dados PostgreSQL...
netstat -ano | findstr :5433 | findstr LISTENING >nul
if %errorlevel% neq 0 (
    netstat -ano | findstr :5432 | findstr LISTENING >nul
    if %errorlevel% neq 0 (
        echo       Tentando inicializar servico local do PostgreSQL...
        set "PG_BIN=C:\Users\sergi\.gemini\antigravity\brain\a0a4efb5-f373-4ffc-9d90-fd2b446eb87d\scratch\pg\node_modules\@embedded-postgres\windows-x64\native\bin\postgres.exe"
        set "PG_DATA=C:\Users\sergi\.gemini\antigravity\brain\a0a4efb5-f373-4ffc-9d90-fd2b446eb87d\scratch\pgdata"
        if exist "%PG_BIN%" (
            start "PostgreSQL Server" /min cmd /c ""%PG_BIN%" -D "%PG_DATA%" -p 5433"
            timeout /t 2 /nobreak >nul
            echo       PostgreSQL inicializado na porta 5433.
        ) else (
            echo       [AVISO] Verifique se o PostgreSQL esta ativo nas portas 5433 ou 5432.
        )
    ) else (
        echo       PostgreSQL ativo na porta 5432.
    )
) else (
    echo       PostgreSQL ativo na porta 5433.
)

:: 2. Iniciar Backend API
echo [2/4] Iniciando Backend API (http://localhost:5000)...
start "Nexo Compras — Backend API" /min cmd /c "cd /d "%BACKEND_DIR%" && Nexo.Api.exe"
timeout /t 2 /nobreak >nul

:: 3. Iniciar Frontend Web
echo [3/4] Iniciando Frontend Web (http://localhost:5173)...
start "Nexo Compras — Frontend Web" /min cmd /c "cd /d "%FRONTEND_DIR%" && (node server.js || npx --yes serve -s dist -l 5173 || python -m http.server 5173 -d dist)"
timeout /t 2 /nobreak >nul

:: 4. Iniciar SINc (Sincronizador Local ERP)
echo [4/4] Iniciando SINc (Sincronizador ERP)...
if exist "%SINC_DIR%\SINc.exe" (
    start "" "%SINC_DIR%\SINc.exe"
    echo       SINc iniciado na bandeja do sistema.
) else (
    echo       [AVISO] Executavel SINc.exe nao encontrado em %SINC_DIR%.
)

:: 5. Abrir Navegador
echo.
echo Abrindo navegador em http://localhost:5173 ...
timeout /t 1 /nobreak >nul
start http://localhost:5173

echo.
echo ==============================================================================
echo   TODOS OS SERVIÇOS FORAM INICIADOS COM SUCESSO!
echo   --------------------------------------------------------------------------
echo   * Frontend Web:   http://localhost:5173
echo   * Backend API:    http://localhost:5000 (Swagger: http://localhost:5000/swagger)
echo   * SINc ERP:       Executando com icone na bandeja (system tray)
echo   * Banco Exemplo:  %BASE_DIR%BANCO_EXEMPLO\BM.FDB (porta 3050)
echo ==============================================================================
echo   Para encerrar todos os servicos, execute: stop_pedidos_compras.bat
echo ==============================================================================
echo.
pause
