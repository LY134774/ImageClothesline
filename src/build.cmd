@echo off
setlocal
pushd "%~dp0"
set "CL_FRAMEWORK=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
if not exist "%CL_FRAMEWORK%\csc.exe" (echo C# compiler not found. & popd & exit /b 1)
if not exist bin mkdir bin
if not exist obj mkdir obj
set CL_REFS=/r:"%CL_FRAMEWORK%\System.dll" /r:"%CL_FRAMEWORK%\System.Core.dll" /r:"%CL_FRAMEWORK%\System.Drawing.dll" /r:"%CL_FRAMEWORK%\System.Windows.Forms.dll" /r:"%CL_FRAMEWORK%\System.Runtime.Serialization.dll" /r:"%CL_FRAMEWORK%\System.Xaml.dll" /r:"%CL_FRAMEWORK%\WPF\WindowsBase.dll" /r:"%CL_FRAMEWORK%\WPF\PresentationCore.dll" /r:"%CL_FRAMEWORK%\WPF\PresentationFramework.dll"
"%CL_FRAMEWORK%\csc.exe" /nologo /utf8output /target:exe /platform:x64 /out:obj\IconWriter.exe %CL_REFS% IconFactory.cs Native.cs ..\tests\IconWriter.cs
if errorlevel 1 (popd & exit /b 1)
obj\IconWriter.exe obj\app.ico
if errorlevel 1 (popd & exit /b 1)
"%CL_FRAMEWORK%\csc.exe" /nologo /utf8output /target:winexe /platform:x64 /optimize+ /main:ImageClothesline.Program /win32icon:obj\app.ico /win32manifest:app.manifest /out:bin\ImageClothesline.exe %CL_REFS% *.cs
if errorlevel 1 (popd & exit /b 1)
copy /y App.config bin\ImageClothesline.exe.config >nul
if errorlevel 1 (popd & exit /b 1)
echo Local build created: src\bin\ImageClothesline.exe
popd
exit /b 0
