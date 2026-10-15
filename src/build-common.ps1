$ErrorActionPreference = 'Stop'
$ClotheslineFramework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$ClotheslineCompiler = Join-Path $ClotheslineFramework 'csc.exe'
if (-not (Test-Path -LiteralPath $ClotheslineCompiler)) { throw '没有找到系统自带的 C# 编译器。请先确认开发工具安装方案。' }
function Get-ClotheslineReferences {
    @('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll',
      'System.Runtime.Serialization.dll', 'System.Xaml.dll', 'WPF\WindowsBase.dll',
      'WPF\PresentationCore.dll', 'WPF\PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path $ClotheslineFramework $_) }
}
function Invoke-ClotheslineCompiler([string[]] $CompilerArguments) {
    & $ClotheslineCompiler @CompilerArguments
    if ($LASTEXITCODE -ne 0) { throw '编译没有通过，请查看上面的错误。' }
}
