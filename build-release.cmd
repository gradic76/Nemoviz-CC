@echo off
rem Builds the release: self-contained single-file exe, installer (Inno Setup 6) and portable zip.
rem Output: dist\NemovizCC-Setup.exe and dist\NemovizCC-portable.zip
setlocal
set "ROOT=%~dp0"
cd /d "%ROOT%"
if exist dist rmdir /s /q dist
dotnet publish ClaudeChat -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=none -p:AssemblyName=NemovizCC -o dist\NemovizCC || exit /b 1
copy /y LICENSE.txt dist\NemovizCC\ >nul
copy /y README.md dist\NemovizCC\ >nul
"%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" installer\NemovizCC.iss || exit /b 1
rem Portable: settings.json next to the exe keeps the settings with the program.
mkdir dist\zip\NemovizCC
xcopy /e /q dist\NemovizCC dist\zip\NemovizCC\ >nul
echo {}> dist\zip\NemovizCC\settings.json
powershell -NoProfile -Command "Compress-Archive -Path 'dist\zip\NemovizCC' -DestinationPath 'dist\NemovizCC-portable.zip' -Force" || exit /b 1
rmdir /s /q dist\zip
echo Done: dist\NemovizCC-Setup.exe, dist\NemovizCC-portable.zip
