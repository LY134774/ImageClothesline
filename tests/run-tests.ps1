param([switch] $Ui, [switch] $NativeRecycle)
. (Join-Path (Split-Path $PSScriptRoot -Parent) 'src\build-common.ps1')
$ClotheslineTestOutput = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Path $ClotheslineTestOutput -Force | Out-Null
$ClotheslineSrc = Join-Path (Split-Path $PSScriptRoot -Parent) 'src'
$ClotheslineAllSources = @(
    Get-ChildItem -LiteralPath $ClotheslineSrc -Filter '*.cs' | ForEach-Object { $_.FullName }
    Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | ForEach-Object { $_.FullName }
)
$ClotheslineTestExe = Join-Path $ClotheslineTestOutput 'ImageClothesline.Tests.exe'
Invoke-ClotheslineCompiler (@('/nologo', '/utf8output', '/target:exe', '/platform:x64',
    '/main:ImageClothesline.Tests.TestRunner', ('/out:' + $ClotheslineTestExe),
    ('/win32manifest:' + (Join-Path $ClotheslineSrc 'app.manifest'))) +
    @(Get-ClotheslineReferences) + $ClotheslineAllSources)
Copy-Item -LiteralPath (Join-Path $ClotheslineSrc 'App.config') -Destination ($ClotheslineTestExe + '.config') -Force
$ClotheslineTestArguments = @()
if ($Ui) { $ClotheslineTestArguments += '--ui' }
if ($NativeRecycle) { $ClotheslineTestArguments += '--native-recycle' }
& $ClotheslineTestExe @ClotheslineTestArguments
if ($LASTEXITCODE -ne 0) { throw '测试没有通过。' }
