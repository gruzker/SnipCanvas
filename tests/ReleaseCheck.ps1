param([string]$ReleaseFolder = (Join-Path (Split-Path -Parent $PSScriptRoot) 'dist'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$names = @('SnipCanvas.exe', 'SnipCanvas-Setup.exe', 'SnipCanvas-Windows-x64.zip')
$lines = @(Get-Content -LiteralPath (Join-Path $ReleaseFolder 'SHA256.txt'))
if ($lines.Count -ne $names.Count) { throw 'Expected a checksum for each release artifact.' }
for ($i = 0; $i -lt $names.Count; $i++) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $ReleaseFolder $names[$i]) -Algorithm SHA256).Hash
    if ($lines[$i] -ne "$hash  $($names[$i])") { throw "Incorrect checksum: $($names[$i])" }
}
$zip = [IO.Compression.ZipFile]::OpenRead((Join-Path $ReleaseFolder 'SnipCanvas-Windows-x64.zip'))
try {
    $expected = @('SnipCanvas.exe', 'README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'licenses/dotnet-LICENSE.txt', 'licenses/dotnet-THIRD-PARTY-NOTICES.txt')
    $entries = @($zip.Entries | Where-Object { $_.Name } | ForEach-Object { $_.FullName.Replace('\', '/') })
    if (Compare-Object ($expected | Sort-Object) ($entries | Sort-Object)) { throw 'Portable ZIP contents differ from the release file list.' }
    foreach ($name in $expected) {
        $entry = $zip.Entries | Where-Object { $_.FullName.Replace('\', '/') -eq $name }
        $stream = $entry.Open()
        try {
            $sha = [Security.Cryptography.SHA256]::Create()
            try { $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
            finally { $sha.Dispose() }
        } finally { $stream.Dispose() }
        if ($hash -ne (Get-FileHash -LiteralPath (Join-Path $ReleaseFolder $name) -Algorithm SHA256).Hash) { throw "Stale ZIP entry: $name" }
    }
} finally { $zip.Dispose() }
Write-Output 'PASS: release checksums, portable ZIP contents, and bundled license notices'
