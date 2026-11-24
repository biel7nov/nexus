@echo off
chcp 65001 >nul
title NexusTI — Instalador do Agente Remoto
color 0B

:: -------------------------------------------------------------------------
:: 1. VERIFICAÇÃO DE PRIVILÉGIOS DE ADMINISTRADOR (Necessário para Firewall)
:: -------------------------------------------------------------------------
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo.
    echo ====================================================================
    echo   NEXUSTI — INSTALADOR DO AGENTE REMOTO & SUPORTE
    echo ====================================================================
    echo.
    echo  [!] Solicitando privilegios de Administrador para configurar o Firewall...
    echo.
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

:: -------------------------------------------------------------------------
:: 2. IDENTIFICAÇÃO E CADASTRO DO ATIVO NO INVENTÁRIO
:: -------------------------------------------------------------------------
cd /d "%~dp0"
cls
echo ====================================================================
echo        NEXUSTI — INSTALADOR DO AGENTE REMOTO & SUPORTE
echo ====================================================================
echo.
echo  Por favor, identifique este computador para o Inventario de Ativos:
echo  (Exemplos: QUA-01, FINANCEIRO-02, NOTEBOOK-DIRETORIA, PRODUCAO-05)
echo.

set "NOME_PADRAO=%COMPUTERNAME%"
set /p "CUSTOM_NAME= [?] Nome do Ativo no Inventario [%NOME_PADRAO%]: "
if "%CUSTOM_NAME%"=="" set "CUSTOM_NAME=%NOME_PADRAO%"

set /p "CUSTOM_TAG= [?] Setor ou Tag (ex: #Qualidade, #Financeiro, #TI, #Producao) [#Geral]: "
if "%CUSTOM_TAG%"=="" set "CUSTOM_TAG=#Geral"

echo.
echo  ✓ Identificado como: %CUSTOM_NAME% (%CUSTOM_TAG%)
echo.
echo  Iniciando instalacao do agente NexusTI em 2º plano...
echo.

set "TARGET_DIR=C:\Nexus-Agent"

echo  [1/6] Preparando diretorio: %TARGET_DIR%
if not exist "%TARGET_DIR%" mkdir "%TARGET_DIR%"
if not exist "%TARGET_DIR%\res" mkdir "%TARGET_DIR%\res"

echo  [2/6] Encerrando processos anteriores...
taskkill /f /im NexusRemoteAgent.exe >nul 2>&1
taskkill /f /im FSCRemoteAgent.exe >nul 2>&1

echo  [3/6] Gravando identificacao e instalando arquivos...
echo %CUSTOM_NAME%> "%TARGET_DIR%\machine_name.txt"
echo %CUSTOM_TAG%> "%TARGET_DIR%\machine_tag.txt"
echo %CUSTOM_NAME%> "%~dp0machine_name.txt"
echo %CUSTOM_TAG%> "%~dp0machine_tag.txt"

copy /y "%~dp0NexusRemoteAgent.exe" "%TARGET_DIR%\NexusRemoteAgent.exe" >nul
copy /y "%~dp0NexusRemoteAgent.exe" "%TARGET_DIR%\FSCRemoteAgent.exe" >nul 2>&1
if exist "%~dp0res\logo_nexus.ico" copy /y "%~dp0res\logo_nexus.ico" "%TARGET_DIR%\res\logo_nexus.ico" >nul 2>&1
if exist "%~dp0logo_nexus.ico" copy /y "%~dp0logo_nexus.ico" "%TARGET_DIR%\res\logo_nexus.ico" >nul 2>&1
if exist "%~dp0res\logo_nexus.svg" copy /y "%~dp0res\logo_nexus.svg" "%TARGET_DIR%\res\logo_nexus.svg" >nul 2>&1
if exist "%~dp0chamado.html" copy /y "%~dp0chamado.html" "%TARGET_DIR%\chamado.html" >nul 2>&1
if exist "%~dp0res\spotlight.jpg" copy /y "%~dp0res\spotlight.jpg" "%TARGET_DIR%\res\spotlight.jpg" >nul 2>&1
if exist "%~dp0agent_token.txt" copy /y "%~dp0agent_token.txt" "%TARGET_DIR%\agent_token.txt" >nul 2>&1

:: Registra para iniciar sempre com o Windows
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "NexusRemoteAgent" /t REG_SZ /d "\"%TARGET_DIR%\NexusRemoteAgent.exe\"" /f >nul

echo  [4/6] Liberando porta TCP 8090 no Firewall do Windows...
netsh advfirewall firewall delete rule name="NexusTI Remote Agent" >nul 2>&1
netsh advfirewall firewall delete rule name="FSC Remote Agent" >nul 2>&1
netsh advfirewall firewall add rule name="NexusTI Remote Agent" dir=in action=allow protocol=TCP localport=8090 profile=any >nul

echo  [5/6] Criando atalhos na Área de Trabalho com o novo ícone Nexus...
powershell -NoProfile -Command "$ws = New-Object -ComObject WScript.Shell; $d = [Environment]::GetFolderPath('Desktop'); $s = $ws.CreateShortcut((Join-Path $d 'NexusTI - Suporte & Chamados.lnk')); $s.TargetPath = '%TARGET_DIR%\NexusRemoteAgent.exe'; $s.Arguments = '-chamado'; $ico = '%TARGET_DIR%\res\logo_nexus.ico'; if (Test-Path $ico) { $s.IconLocation = $ico + ',0' } else { $s.IconLocation = '%TARGET_DIR%\NexusRemoteAgent.exe,0' }; $s.Description = 'NexusTI — Abrir Chamado com Suporte de T.I'; $s.WorkingDirectory = '%TARGET_DIR%'; $s.Save()"

echo  [6/6] Iniciando o NexusTI Remote Agent em segundo plano...
start "" "%TARGET_DIR%\NexusRemoteAgent.exe"

timeout /t 2 >nul

echo.
echo ====================================================================
echo   ✓ INSTALAÇÃO NEXUSTI CONCLUÍDA COM SUCESSO!
echo ====================================================================
echo.
echo  O agente ja esta em execucao em segundo plano:
echo  - Ativo: %CUSTOM_NAME% (%CUSTOM_TAG%)
echo  - Porta TCP: 8090 (Liberada no Firewall)
echo  - Icone: Verifique o novo icone Nexus perto do relogio do Windows
echo  - Atalho: 'NexusTI - Suporte & Chamados' criado na Area de Trabalho
echo  - Inicializacao: Automatica com o Windows
echo.
echo ====================================================================

powershell -Command "$ip = (Get-NetIPAddress -AddressFamily IPv4 -InterfaceAlias 'Wi-Fi*','Ethernet*' -ErrorAction SilentlyContinue | Where-Object { $_.IPAddress -notlike '169.254*' -and $_.IPAddress -notlike '127.*' } | Select-Object -First 1).IPAddress; if (-not $ip) { $ip = 'IP_DESTE_PC' }; [System.Reflection.Assembly]::LoadWithPartialName('System.Windows.Forms') | Out-Null; [System.Windows.Forms.MessageBox]::Show('NexusTI Remote Agent instalado com sucesso!`n`nAtivo: %CUSTOM_NAME% (%CUSTOM_TAG%)`nFirewall: Porta 8090 Liberada.`nAtalho criado na Área de Trabalho!`n`nPara conectar neste computador através do console web em outra máquina, use:`nhttp://' + $ip + ':8090', 'NexusTI — Agente Ativo', [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)"

echo.
echo Pressione qualquer tecla para encerrar...
pause >nul
