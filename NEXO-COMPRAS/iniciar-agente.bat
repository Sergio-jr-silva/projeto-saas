@echo off
chcp 65001 >nul
set "SCRIPT_DIR=%~dp0"
set "AGENT_EXE=%SCRIPT_DIR%agent-desktop\bin\Release\net9.0-windows\Nexo.Agent.Desktop.exe"

if not exist "%AGENT_EXE%" (
    set "AGENT_EXE=%SCRIPT_DIR%agent-desktop\bin\Debug\net9.0-windows\Nexo.Agent.Desktop.exe"
)

if not exist "%AGENT_EXE%" (
    echo [Nexo Compras Agent] Compilando executavel do agente...
    dotnet build "%SCRIPT_DIR%agent-desktop\Nexo.Agent.Desktop.csproj" -c Release --nologo
    set "AGENT_EXE=%SCRIPT_DIR%agent-desktop\bin\Release\net9.0-windows\Nexo.Agent.Desktop.exe"
)

if exist "%AGENT_EXE%" (
    start "" "%AGENT_EXE%"
) else (
    echo [Erro] Nao foi possivel localizar ou compilar o executavel do agente.
    pause
)
