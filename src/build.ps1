param()
. (Join-Path $PSScriptRoot 'build-common.ps1')
$ClotheslineOutput = Join-Path $PSScriptRoot 'bin'
$ClotheslineIntermediate = Join-Path $PSScriptRoot 'obj'
New-Item -ItemType Directory -Path $ClotheslineOutput, $ClotheslineIntermediate -Force | Out-Null
$ClotheslineReferences = @(Get-ClotheslineReferences)
$ClotheslineIconWriter = Join-Path $ClotheslineIntermediate 'IconWriter.exe'
$ClotheslineIcon = Join-Path $ClotheslineIntermediate 'app.ico'
$ClotheslineIconSources = @(
    (Join-Path $PSScriptRoot 'IconFactory.cs'),
    (Join-Path $PSScriptRoot 'Native.cs'),
    (Join-Path (Split-Path $PSScriptRoot -Parent) 'tests\IconWriter.cs')
)
Invoke-ClotheslineCompiler (@('/nologo', '/utf8output', '/target:exe', '/platform:x64',
    ('/out:' + $ClotheslineIconWriter)) + $ClotheslineReferences + $ClotheslineIconSources)
& $ClotheslineIconWriter $ClotheslineIcon
if ($LASTEXITCODE -ne 0) { throw '图标生成失败。' }
$ClotheslineSources = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | ForEach-Object { $_.FullName })
$ClotheslineExecutable = Join-Path $ClotheslineOutput 'ImageClothesline.exe'
Invoke-ClotheslineCompiler (@('/nologo', '/utf8output', '/target:winexe', '/platform:x64', '/optimize+',
    '/main:ImageClothesline.Program', ('/win32icon:' + $ClotheslineIcon),
    ('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')),
    ('/out:' + $ClotheslineExecutable)) + $ClotheslineReferences + $ClotheslineSources)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'App.config') -Destination ($ClotheslineExecutable + '.config') -Force
Write-Output ('本地程序已生成：' + $ClotheslineExecutable)
