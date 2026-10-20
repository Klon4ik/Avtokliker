param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path -Parent $PSScriptRoot
if (-not $SkipBuild) { & (Join-Path $projectDirectory 'build.ps1') -Test }
$stageDirectory = Join-Path $projectDirectory ('dist\package-' + [Guid]::NewGuid().ToString('N'))
$packageDirectory = Join-Path $stageDirectory 'Avtokliker'
New-Item -ItemType Directory -Path (Join-Path $packageDirectory 'app') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $packageDirectory 'scripts') -Force | Out-Null
foreach ($file in @('Start.cmd', 'Create-Shortcut.cmd', 'README.md', 'README.en.md')) {
    Copy-Item -LiteralPath (Join-Path $projectDirectory $file) -Destination (Join-Path $packageDirectory $file)
}
foreach ($file in @('AvtoClicker.exe', 'AvtoClicker.ico')) {
    Copy-Item -LiteralPath (Join-Path $projectDirectory ('app\' + $file)) -Destination (Join-Path $packageDirectory ('app\' + $file))
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Create-Shortcut.ps1') -Destination (Join-Path $packageDirectory 'scripts\Create-Shortcut.ps1')
$downloadDirectory = Join-Path $projectDirectory 'downloads'
New-Item -ItemType Directory -Path $downloadDirectory -Force | Out-Null
$zipPath = Join-Path $downloadDirectory 'Avtokliker-Windows.zip'
Compress-Archive -LiteralPath $packageDirectory -DestinationPath $zipPath -Force
Write-Output ('Portable package: ' + $zipPath)
