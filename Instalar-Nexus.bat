@echo off
chcp 65001 >nul
title Nexus TI — Instalador do Agente Remoto
color 0B

:: -------------------------------------------------------------------------
:: 1. VERIFICACAO DE PRIVILEGIOS DE ADMINISTRADOR
:: -------------------------------------------------------------------------
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo.
    echo ====================================================================
    echo   NEXUS TI — INSTALADOR DO AGENTE CORPORATIVO & SUPORTE
    echo ====================================================================
    echo.
    echo  [!] Solicitando privilegios de Administrador para configurar o sistema...
    echo.
    powershell -NoProfile -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

:: -------------------------------------------------------------------------
:: 2. IDENTIFICACAO E CADASTRO DO ATIVO
:: -------------------------------------------------------------------------
cd /d "%~dp0"
cls
echo ====================================================================
echo        NEXUS TI — INSTALADOR DO AGENTE CORPORATIVO & SUPORTE
echo ====================================================================
echo.
echo  Por favor, identifique este computador para o Inventario:
echo  (Exemplos: ADM-01, QUALIDADE-02, FINANCEIRO-01, DIRETORIA)
echo.

set "NOME_PADRAO=%COMPUTERNAME%"
set /p "CUSTOM_NAME= [?] Nome do Ativo [%NOME_PADRAO%]: "
if "%CUSTOM_NAME%"=="" set "CUSTOM_NAME=%NOME_PADRAO%"

set /p "CUSTOM_EMPRESA= [?] Empresa (ex: Aco Moveis, JPL, teste) [Aco Moveis]: "
if "%CUSTOM_EMPRESA%"=="" set "CUSTOM_EMPRESA=Aco Moveis"

set /p "CUSTOM_TAG= [?] Setor ou Tag (ex: #Qualidade, #Financeiro, #TI) [#Geral]: "
if "%CUSTOM_TAG%"=="" set "CUSTOM_TAG=#Geral"

echo.
echo  [+] Ativo: %CUSTOM_NAME%
echo  [+] Empresa: %CUSTOM_EMPRESA%
echo  [+] Setor: %CUSTOM_TAG%
echo.
echo  Instalando arquivos em C:\Nexus-Agent ...
echo.

set "TARGET_DIR=C:\Nexus-Agent"

if not exist "%TARGET_DIR%" mkdir "%TARGET_DIR%"
if not exist "%TARGET_DIR%\res" mkdir "%TARGET_DIR%\res"

taskkill /f /im NexusRemoteAgent.exe >nul 2>&1
taskkill /f /im FSCRemoteAgent.exe >nul 2>&1

echo %CUSTOM_NAME%> "%TARGET_DIR%\machine_name.txt"
echo %CUSTOM_TAG%> "%TARGET_DIR%\machine_tag.txt"
echo %CUSTOM_EMPRESA%> "%TARGET_DIR%\machine_empresa.txt"

copy /y "%~dp0NexusRemoteAgent.exe" "%TARGET_DIR%\NexusRemoteAgent.exe" >nul 2>&1
copy /y "%~dp0NexusRemoteAgent.exe" "%TARGET_DIR%\FSCRemoteAgent.exe" >nul 2>&1
if exist "%~dp0Instalador-Nexus-Agent.exe" copy /y "%~dp0Instalador-Nexus-Agent.exe" "%TARGET_DIR%\Desinstalador-Nexus-Agent.exe" >nul 2>&1
if exist "%~dp0chamado.html" copy /y "%~dp0chamado.html" "%TARGET_DIR%\chamado.html" >nul 2>&1
if exist "%~dp0res\logo_nexus.ico" copy /y "%~dp0res\logo_nexus.ico" "%TARGET_DIR%\res\logo_nexus.ico" >nul 2>&1
if exist "%~dp0logo_nexus.ico" copy /y "%~dp0logo_nexus.ico" "%TARGET_DIR%\res\logo_nexus.ico" >nul 2>&1
if exist "%~dp0res\spotlight.jpg" copy /y "%~dp0res\spotlight.jpg" "%TARGET_DIR%\res\spotlight.jpg" >nul 2>&1
if exist "%~dp0agent_token.txt" copy /y "%~dp0agent_token.txt" "%TARGET_DIR%\agent_token.txt" >nul 2>&1

reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "NexusRemoteAgent" /t REG_SZ /d "\"%TARGET_DIR%\NexusRemoteAgent.exe\"" /f >nul 2>&1

netsh advfirewall firewall delete rule name="NexusTI Remote Agent" >nul 2>&1
netsh advfirewall firewall delete rule name="FSC Remote Agent" >nul 2>&1
netsh advfirewall firewall add rule name="NexusTI Remote Agent" dir=in action=allow protocol=TCP localport=8090 profile=any >nul 2>&1

set "DESKTOP_DIR=%USERPROFILE%\Desktop"
if exist "%PUBLIC%\Desktop" (
    echo [InternetShortcut]> "%DESKTOP_DIR%\Abrir Chamado T.I (Web).url"
    echo URL=https://nexus-ten-brown.vercel.app/chamado.html>> "%DESKTOP_DIR%\Abrir Chamado T.I (Web).url"
    echo IconFile=%TARGET_DIR%\NexusRemoteAgent.exe>> "%DESKTOP_DIR%\Abrir Chamado T.I (Web).url"
    echo IconIndex=0>> "%DESKTOP_DIR%\Abrir Chamado T.I (Web).url"
)

start "" "%TARGET_DIR%\NexusRemoteAgent.exe"

timeout /t 2 >nul

echo.
echo ====================================================================
echo   OK: INSTALACAO DO AGENTE CONCLUIDA COM SUCESSO!
echo ====================================================================
echo.
echo  - O Nexus TI Remote Agent ja esta rodando em segundo plano.
echo  - Porta de comunicacao TCP 8090 liberada.
echo  - Atalho criado na Area de Trabalho.
echo.
pause
