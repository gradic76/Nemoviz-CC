@echo off
rem Builds the release on .NET Framework 4.8 (part of Windows, so nothing of it ships):
rem the exe with its files, the installer (Inno Setup 6) and the portable zip.
rem Output: dist\NemovizCC-Setup.exe and dist\NemovizCC-portable.zip
setlocal
set "ROOT=%~dp0"
cd /d "%ROOT%"
set "MSBUILD=%ProgramFiles%\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
if exist dist rmdir /s /q dist
"%MSBUILD%" ClaudeChat\ClaudeChat.csproj -restore -nologo -v:m -p:Configuration=Release -p:AssemblyName=NemovizCC -p:DebugType=none -p:OutDir=../dist/NemovizCC/ || exit /b 1
copy /y LICENSE.txt dist\NemovizCC\ >nul
copy /y README.md dist\NemovizCC\ >nul
"%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" installer\NemovizCC.iss || exit /b 1
rem Portable: settings.json next to the exe keeps the settings with the program.
mkdir dist\zip\NemovizCC
xcopy /e /q dist\NemovizCC dist\zip\NemovizCC\ >nul
echo {}> dist\zip\NemovizCC\settings.json
rem tar, because Compress-Archive writes backslashes into the zip's entry names.
tar.exe -a -c -f dist\NemovizCC-portable.zip -C dist\zip NemovizCC || exit /b 1
rmdir /s /q dist\zip
echo Done: dist\NemovizCC-Setup.exe, dist\NemovizCC-portable.zip
