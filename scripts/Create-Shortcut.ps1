param([switch]$Desktop)
$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path -Parent $PSScriptRoot
$executable = Join-Path $projectDirectory 'app\AvtoClicker.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Build the application with build.ps1 first.' }
$shortcutDirectory = $projectDirectory
if ($Desktop) { $shortcutDirectory = [Environment]::GetFolderPath('Desktop') }
$shortcutPath = Join-Path $shortcutDirectory 'Auto Clicker.lnk'
$shortcutShell = New-Object -ComObject WScript.Shell
$shortcut = $shortcutShell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $executable
$shortcut.WorkingDirectory = Split-Path -Parent $executable
$shortcut.IconLocation = $executable + ',0'
$shortcut.Description = 'Auto Clicker - mouse and keyboard automation'
$shortcut.Save()
Write-Output ('Shortcut created: ' + $shortcutPath)
