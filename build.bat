@echo off
setlocal
cd /d "%~dp0"
echo Building Coldy.exe ...
echo If the pendant is running, exit it from the tray menu first.
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
"%CSC%" /nologo /unsafe /target:winexe /platform:anycpu /win32manifest:src\app.manifest /win32icon:assets\coldy.ico /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /out:Coldy.exe src\Program.cs
if errorlevel 1 goto fail
copy /Y src\app.config Coldy.exe.config >nul
echo Build OK: Coldy.exe
goto end
:fail
echo Build FAILED. See the compiler errors above.
exit /b 1
:end
