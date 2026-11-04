[CmdletBinding()]
param([switch]$SelfTest)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = Split-Path $PSScriptRoot -Parent
$dist = Join-Path $projectRoot 'dist'
$obj = Join-Path $projectRoot 'obj'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '需要 x64 .NET Framework C# 編譯器。' }
[void](New-Item -ItemType Directory -Path $dist,$obj -Force)
$source = Join-Path $projectRoot 'src\EndfieldDirect.cs'
$configSource = Join-Path $projectRoot 'src\AppConfig.cs'
$manifest = Join-Path $projectRoot 'src\app.manifest'
$testExe = Join-Path $obj 'ConfigTests.exe'
$executable = Join-Path $dist 'Endfield-Direct.exe'

& $compiler /nologo /target:exe /platform:x64 /optimize+ /warn:4 /warnaserror+ /reference:System.Web.Extensions.dll "/out:$testExe" $configSource (Join-Path $projectRoot 'tests\ConfigTests.cs')
if ($LASTEXITCODE -ne 0) { throw "測試程式編譯失敗：$LASTEXITCODE" }
& $testExe
if ($LASTEXITCODE -ne 0) { throw "設定測試失敗：$LASTEXITCODE" }

& $compiler /nologo /target:winexe /platform:x64 /optimize+ /warn:4 /warnaserror+ /reference:System.Web.Extensions.dll "/win32manifest:$manifest" "/out:$executable" $source $configSource
if ($LASTEXITCODE -ne 0) { throw "編譯失敗：$LASTEXITCODE" }
if ((Get-Item -LiteralPath $executable).Length -eq 0) { throw '編譯產生的 EXE 為空檔案。' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'config.example.json') -Destination $dist
# 保留使用者的 config.json。
if ($SelfTest) {
    $process = Start-Process -FilePath $executable -ArgumentList '--self-test' -PassThru -Wait -WindowStyle Hidden
    if ($process.ExitCode -ne 0) { throw "程序自測失敗：$($process.ExitCode)，請查看 dist\Endfield-Direct-error.txt。" }
}
$sumLines = foreach ($name in @('Endfield-Direct.exe','config.example.json')) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $dist $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $name"
}
[IO.File]::WriteAllLines((Join-Path $dist 'SHA256SUMS'), [string[]]$sumLines, (New-Object Text.UTF8Encoding($false)))
Write-Output "建置完成：$executable"
