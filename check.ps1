param(
    [ValidateSet('self-test', 'escape-check', 'window-check', 'tray-check', 'text-check', 'editable-text-check', 'visual-check', 'redesign-check', 'capture-preview-check')]
    [string[]]$Checks = @('self-test', 'escape-check', 'window-check', 'tray-check', 'text-check', 'editable-text-check', 'visual-check', 'redesign-check', 'capture-preview-check')
)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot

dotnet build SnipCanvas.csproj -c Release -r win-x64 --self-contained true -p:EnableDiagnostics=true
if ($LASTEXITCODE -ne 0) { throw 'Diagnostic build failed.' }
$testExe = Join-Path $PSScriptRoot 'bin/Release/net8.0-windows10.0.19041.0/win-x64/SnipCanvas.exe'
$checkFolder = Join-Path $PSScriptRoot '.checks'
New-Item -ItemType Directory -Path $checkFolder -Force | Out-Null
foreach ($check in $Checks) {
    $process = Start-Process -FilePath $testExe -ArgumentList "--$check" -WorkingDirectory $checkFolder -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(45000)) {
        $process.Kill()
        throw "$check timed out. See .checks for details."
    }
    if ($process.ExitCode -ne 0) { throw "$check failed. See .checks for details." }
    Write-Output "PASS: $check"
}
