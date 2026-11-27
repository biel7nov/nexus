@echo off
taskkill /f /im NexusRemoteAgent.exe >nul 2>&1
taskkill /f /im FSCRemoteAgent.exe >nul 2>&1
ping 127.0.0.1 -n 2 >nul

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

echo Compilando NexusRemoteAgent.exe...
%CSC% /nologo /target:winexe /out:agent\NexusRemoteAgent.exe /optimize+ /win32manifest:agent\agent.manifest /win32icon:agent\logo_nexus.ico /resource:viewer.html,viewer.html /resource:chamado.html,chamado.html /reference:System.Windows.Forms.dll,System.Drawing.dll,System.Web.Extensions.dll agent\AssemblyInfoAgent.cs agent\NexusRemoteAgent.cs
if errorlevel 1 (
    echo ERRO ao compilar NexusRemoteAgent.exe
    exit /b 1
)
echo NexusRemoteAgent.exe compilado com sucesso!

echo Compilando Instalador-Nexus-Agent.exe...
%CSC% /nologo /target:winexe /out:agent\Instalador-Nexus-Agent.exe /optimize+ /win32manifest:agent\installer.manifest /win32icon:agent\logo_nexus.ico /resource:viewer.html,viewer.html /resource:chamado.html,chamado.html /resource:setores.json,setores.json /resource:agent\NexusRemoteAgent.exe,FSCInstaller.NexusRemoteAgent.exe /reference:System.Windows.Forms.dll,System.Drawing.dll,System.Web.Extensions.dll agent\AssemblyInfoInstaller.cs agent\InstaladorNexus.cs
if errorlevel 1 (
    echo ERRO ao compilar Instalador-Nexus-Agent.exe
    exit /b 1
)
echo Instalador-Nexus-Agent.exe compilado com sucesso!
