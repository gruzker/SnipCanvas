$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot

# Run on an interactive Windows desktop. Installed copies are separate.
if (Get-Process SnipCanvas -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($PSScriptRoot + '\', [StringComparison]::OrdinalIgnoreCase) }) {
    throw 'Quit SnipCanvas running from this checkout before building a release.'
}

$checkOutput = & (Join-Path $PSScriptRoot 'check.ps1')
$checkOutput | Write-Output
$results = @($checkOutput | Where-Object { $_ -like 'PASS:*' } | ForEach-Object { "- $_" })
$checkFolder = Join-Path $PSScriptRoot '.checks'
$stage = Join-Path $checkFolder ('release-' + [Guid]::NewGuid().ToString('N'))

dotnet publish SnipCanvas.csproj -c Release -r win-x64 --self-contained true -p:EnableDiagnostics=false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $stage
if ($LASTEXITCODE -ne 0) { throw 'Release publish failed.' }
$exe = Join-Path $stage 'SnipCanvas.exe'
$hash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
Copy-Item -LiteralPath 'QUICKSTART.md' -Destination (Join-Path $stage 'README.md')
& (Join-Path $PSScriptRoot 'build-installer.ps1') -ReleaseFolder $stage
Compress-Archive -Path (Join-Path $stage 'SnipCanvas.exe'), (Join-Path $stage 'README.md'), (Join-Path $stage 'LICENSE'), (Join-Path $stage 'THIRD-PARTY-NOTICES.md'), (Join-Path $stage 'licenses') -DestinationPath (Join-Path $stage 'SnipCanvas-Windows-x64.zip')
$hashes = foreach ($name in @('SnipCanvas.exe', 'SnipCanvas-Setup.exe', 'SnipCanvas-Windows-x64.zip')) {
    "$((Get-FileHash -LiteralPath (Join-Path $stage $name) -Algorithm SHA256).Hash)  $name"
}
Set-Content -LiteralPath (Join-Path $stage 'SHA256.txt') -Value $hashes -Encoding ASCII
$version = (Get-Item -LiteralPath $exe).VersionInfo.ProductVersion
$report = @(
    '# Release checks', '',
    "Version: $version", "Checked: $([DateTime]::UtcNow.ToString('yyyy-MM-dd HH:mm')) UTC", '',
    'Self-contained Windows x64 diagnostic build; shipping build excludes diagnostic code.', '',
    $results, '',
    "SHA-256: $hash", '',
    'These automated checks do not replace manual verification on Windows 10, mixed-DPI displays, or recipient apps.',
    'The executable is unsigned. Public signing and distribution are not performed by this script.'
)
Set-Content -LiteralPath (Join-Path $stage 'RELEASE-CHECK.md') -Value $report -Encoding UTF8
& (Join-Path $PSScriptRoot 'tests/ReleaseCheck.ps1') -ReleaseFolder $stage
$stage = (Resolve-Path -LiteralPath $stage).Path
$dist = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'dist'))
$temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$previous = [IO.Path]::GetFullPath((Join-Path $temp ('SnipCanvas-previous-release-' + [Guid]::NewGuid().ToString('N'))))
if (!$stage.StartsWith($checkFolder + '\', [StringComparison]::OrdinalIgnoreCase) -or $dist -ne (Join-Path $PSScriptRoot 'dist') -or !$previous.StartsWith($temp, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected release paths.' }
if (Test-Path -LiteralPath $dist) { Move-Item -LiteralPath $dist -Destination $previous }
try { Move-Item -LiteralPath $stage -Destination $dist }
catch {
    if (Test-Path -LiteralPath $previous) { Move-Item -LiteralPath $previous -Destination $dist }
    throw
}
Write-Output "Release ready: $dist"
