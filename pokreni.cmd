@echo off
rem Starts Nemoviz CC.
rem The program runs from a copy in "app", so a new build (ClaudeChat\bin\Release)
rem can be made while the window is open; the copy is refreshed here, at the next start.
rem With --restart (the window calls it so after a new build) it first waits, up to
rem 20 seconds, for the running window to close, so the copy is not locked.
setlocal
set "ROOT=%~dp0"
set "BUILD=%ROOT%ClaudeChat\bin\Release\net10.0-windows"
set "APP=%ROOT%app"

if /i not "%~1"=="--restart" goto copy
set N=0
:wait
tasklist /FI "IMAGENAME eq ClaudeChat.exe" /NH | find /I "ClaudeChat.exe" >nul || goto copy
set /a N+=1
if %N% geq 20 goto copy
ping -n 2 127.0.0.1 >nul
goto wait

:copy
if not exist "%BUILD%\ClaudeChat.exe" dotnet build "%ROOT%ClaudeChat\ClaudeChat.csproj" -c Release -nologo -v q
robocopy "%BUILD%" "%APP%" /MIR /XF *.old /R:0 /W:0 /NJH /NJS /NFL /NDL /NP >nul
rem The window reopens the folder last worked in; this folder only when nothing is remembered.
start "" "%APP%\ClaudeChat.exe" --default-project "%ROOT:~0,-1%" --restart-command "%~f0"
