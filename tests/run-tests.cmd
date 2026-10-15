@echo off
setlocal
pushd "%~dp0"
set "CL_FRAMEWORK=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
if not exist "%CL_FRAMEWORK%\csc.exe" (echo C# compiler not found. & popd & exit /b 1)
if not exist bin mkdir bin
set CL_REFS=/r:"%CL_FRAMEWORK%\System.dll" /r:"%CL_FRAMEWORK%\System.Core.dll" /r:"%CL_FRAMEWORK%\System.Drawing.dll" /r:"%CL_FRAMEWORK%\System.Windows.Forms.dll" /r:"%CL_FRAMEWORK%\System.Runtime.Serialization.dll" /r:"%CL_FRAMEWORK%\System.Xaml.dll" /r:"%CL_FRAMEWORK%\WPF\WindowsBase.dll" /r:"%CL_FRAMEWORK%\WPF\PresentationCore.dll" /r:"%CL_FRAMEWORK%\WPF\PresentationFramework.dll"
"%CL_FRAMEWORK%\csc.exe" /nologo /utf8output /target:exe /platform:x64 /main:ImageClothesline.Tests.TestRunner /win32manifest:..\src\app.manifest /out:bin\ImageClothesline.Tests.exe %CL_REFS% ..\src\*.cs *.cs
if errorlevel 1 (popd & exit /b 1)
copy /y ..\src\App.config bin\ImageClothesline.Tests.exe.config >nul
bin\ImageClothesline.Tests.exe %*
set "CL_RESULT=%ERRORLEVEL%"
popd
exit /b %CL_RESULT%
