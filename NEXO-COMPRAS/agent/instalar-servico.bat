@echo off
chcp 65001 >nul
title Nexo Compras - Instalador do Servico Windows

echo ========================================================
echo   NEXO COMPRAS - INSTALACAO DO SERVICO WINDOWS EM 2º PLANO
echo ========================================================
echo.

:: Verificar privilégios de Administrador
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERRO] Este script precisa ser executado como Administrador.
    echo Clique com o botao direito e selecione 'Executar como Administrador'.
    echo.
    pause
    exit /b 1
)

set SERVICE_NAME=NexoAgent
set DISPLAY_NAME=Nexo Compras - Sincronizador ERP Local
set AGENT_DIR=%~dp0
set AGENT_EXE=%AGENT_DIR%bin\Debug\net9.0\Nexo.Agent.exe

if not exist "%AGENT_EXE%" (
    echo Compilando binarios do servico Nexo.Agent...
    dotnet build "%AGENT_DIR%Nexo.Agent.csproj" -c Release
    set AGENT_EXE=%AGENT_DIR%bin\Release\net9.0\Nexo.Agent.exe
)

if not exist "%AGENT_EXE%" (
    set AGENT_EXE=%AGENT_DIR%bin\Debug\net9.0\Nexo.Agent.exe
)

echo [1/3] Criando servico Windows '%SERVICE_NAME%'...
sc.exe create %SERVICE_NAME% binPath= "\"%AGENT_EXE%\"" start= auto DisplayName= "%DISPLAY_NAME%"

echo [2/3] Configurando politicas de autorrecuperacao...
sc.exe failure %SERVICE_NAME% reset= 86400 actions= restart/60000/restart/60000/restart/60000
sc.exe description %SERVICE_NAME% "Sincronizador em segundo plano da plataforma Nexo Compras. Realiza a ponte segura e continua entre o ERP local (Firebird/PostgreSQL) e o SaaS Central."

echo [3/3] Iniciando o servico...
sc.exe start %SERVICE_NAME%

echo.
echo ========================================================
echo   SERVICO WINDOWS INSTALADO E CONFIGURADO COM SUCESSO!
echo   O sincronizador agora roda mesmo sem usuario logado.
echo ========================================================
echo.
pause

