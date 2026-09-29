param([string]$Compiler, [string]$ReleaseFolder = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
if (!$Compiler) {
    $installed = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    $candidates = @(
        $installed.Source,
        (Join-Path $env:LOCALAPPDATA 'SnipCanvasBuildTools\InnoSetup6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    )
    $Compiler = $candidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
}
if (!$Compiler) { throw 'Install Inno Setup 6 or pass -Compiler with the path to ISCC.exe.' }
if (!(Test-Path -LiteralPath (Join-Path $ReleaseFolder 'SnipCanvas.exe'))) { throw 'Run release.ps1 to build the app first.' }
$ReleaseFolder = (Resolve-Path -LiteralPath $ReleaseFolder).Path
& $Compiler "/DReleaseDir=$ReleaseFolder" 'installer/SnipCanvas.iss'
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$lines = foreach ($name in @('SnipCanvas.exe', 'SnipCanvas-Setup.exe')) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $ReleaseFolder $name) -Algorithm SHA256).Hash
    "$hash  $name"
}
Set-Content -LiteralPath (Join-Path $ReleaseFolder 'SHA256.txt') -Value $lines -Encoding ASCII
Write-Output "Installer ready: $ReleaseFolder/SnipCanvas-Setup.exe"
