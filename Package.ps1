param([string]$Version = '0.1.0')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Use a semantic version such as 0.1.0' }
$dist = Join-Path $PSScriptRoot 'dist'
$stage = Join-Path $dist ('stage-' + [Guid]::NewGuid().ToString('N'))
$app = Join-Path $stage 'TextSnip'
New-Item -ItemType Directory -Path $app -Force | Out-Null
& (Join-Path $PSScriptRoot 'Build.ps1') -OutputName ($app.Substring($PSScriptRoot.Length + 1) + '\TextSnip.exe')
foreach ($name in @('Ocr.ps1','Start.cmd','Install-OCR.cmd','Setup-OCR.ps1','QUICKSTART.txt','TextSnip.ico')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $app
}
$archive = Join-Path $dist ('TextSnip-' + $Version + '-windows.zip')
Compress-Archive -LiteralPath $app -DestinationPath $archive -Force
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($archive + '.sha256'), $hash + '  ' + [IO.Path]::GetFileName($archive) + "`n", [Text.Encoding]::ASCII)
Write-Output ('ZIP: ' + $archive)
Write-Output ('SHA256: ' + $hash)
