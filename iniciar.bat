@echo off
title Nexo Compras SaaS - Inicializador
echo ===================================================================
echo               NEXO COMPRAS SAAS v2.0 E AGENTE ERP
echo ===================================================================
echo.

set "ROOT_DIR=%~dp0"
if exist "%ROOT_DIR%NEXO-COMPRAS" (
    set "APP_DIR=%ROOT_DIR%NEXO-COMPRAS"
) else (
    set "APP_DIR=%ROOT_DIR%"
)

set "SCRATCH_DIR=C:\Users\sergi\.gemini\antigravity\brain\a0a4efb5-f373-4ffc-9d90-fd2b446eb87d\scratch"
set "PG_BIN=%SCRATCH_DIR%\pg\node_modules\@embedded-postgres\windows-x64\native\bin"
set "PG_DATA=%SCRATCH_DIR%\pgdata"

echo [1/4] Verificando Banco de Dados PostgreSQL (Porta 5433)...
netstat -ano | findstr :5433 >nul
if %errorlevel% equ 0 (
    echo PostgreSQL ja esta operacional na porta 5433!
    goto start_apps
)

echo Inicializando PostgreSQL...
if exist "%PG_DATA%\postmaster.pid" (
    tasklist /fi "imagename eq postgres.exe" | findstr /i "postgres.exe" >nul
    if %errorlevel% neq 0 (
        del /f /q "%PG_DATA%\postmaster.pid" >nul 2>&1
    )
)
start "PostgreSQL Server (Nexo)" /B "%PG_BIN%\postgres.exe" -D "%PG_DATA%" -p 5433

set /a pg_tries=0
:wait_pg
ping 127.0.0.1 -n 2 >nul
netstat -ano | findstr :5433 >nul
if %errorlevel% equ 0 goto pg_ready
set /a pg_tries+=1
if %pg_tries% lss 8 goto wait_pg

:pg_ready
echo PostgreSQL pronto na porta 5433!

:start_apps
echo.
echo [2/4] Iniciando Backend Nexo API .NET 9 (Porta 5000)...
start "Nexo Backend API" cmd /k "cd /d ""%APP_DIR%\backend"" && dotnet run"

echo.
echo [3/4] Iniciando Frontend Nexo Compras (Porta 5173)...
start "Nexo Frontend Web" cmd /k "cd /d ""%APP_DIR%\frontend"" && npm run dev"

echo.
echo [4/4] Iniciando Nexo Compras Agent (SINc) sem prompt de comando...
set "AGENT_EXE=%APP_DIR%\agent-desktop\bin\Release\net9.0-windows\Nexo.Agent.Desktop.exe"
if not exist "%AGENT_EXE%" (
    set "AGENT_EXE=%APP_DIR%\agent-desktop\bin\Debug\net9.0-windows\Nexo.Agent.Desktop.exe"
)
if not exist "%AGENT_EXE%" (
    echo Compilando executavel do agente silencioso...
    dotnet build "%APP_DIR%\agent-desktop\Nexo.Agent.Desktop.csproj" -c Release --nologo -v q >nul 2>&1
    set "AGENT_EXE=%APP_DIR%\agent-desktop\bin\Release\net9.0-windows\Nexo.Agent.Desktop.exe"
)
if exist "%AGENT_EXE%" (
    start "" "%AGENT_EXE%"
) else (
    start "" /b dotnet run --project "%APP_DIR%\agent-desktop\Nexo.Agent.Desktop.csproj" --no-build
)

echo.
echo Aguardando inicializacao dos servicos...
ping 127.0.0.1 -n 5 >nul

echo Abrindo aplicacao no navegador: http://localhost:5173
start http://localhost:5173

echo.
echo ===================================================================
echo   Ecossistema Nexo Compras Inicializado com Sucesso!
echo   - Frontend Web:        http://localhost:5173
echo   - Backend API:         http://localhost:5000
echo   - Sincronizador ERP:   Rodando com icone na bandeja (2o plano)
echo   - Banco PostgreSQL:    Porta 5433
echo ===================================================================
pause
