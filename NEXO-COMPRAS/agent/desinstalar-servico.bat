@echo off
chcp 65001 >nul
title Nexo Compras - Desinstalador do Servico Windows

echo ========================================================
echo   NEXO COMPRAS - DESINSTALACAO DO SERVICO WINDOWS
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

echo Parando o servico '%SERVICE_NAME%'...
sc.exe stop %SERVICE_NAME% >nul 2>&1

echo Removendo o servico Windows...
sc.exe delete %SERVICE_NAME%

echo.
echo ========================================================
echo   SERVICO WINDOWS REMOVIDO COM SUCESSO!
echo ========================================================
echo.
pause

