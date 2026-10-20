param([switch]$Test)
$ErrorActionPreference = 'Stop'
$projectDirectory = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x compiler was not found.' }
$outputDirectory = Join-Path $projectDirectory 'app'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$executable = Join-Path $outputDirectory 'AvtoClicker.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectDirectory 'src') -Filter '*.cs' | ForEach-Object FullName)
$compilerArguments = @('/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/utf8output',
    '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll', '/reference:System.Runtime.Serialization.dll',
    ('/win32manifest:' + (Join-Path $projectDirectory 'app.manifest')), ('/out:' + $executable))
$iconPath = Join-Path $outputDirectory 'AvtoClicker.ico'
if (Test-Path -LiteralPath $iconPath) { $compilerArguments += '/win32icon:' + $iconPath }
& $compiler $compilerArguments $sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
if (-not (Test-Path -LiteralPath $iconPath)) {
    $iconProcess = Start-Process -FilePath $executable -ArgumentList ('--make-icon "' + $iconPath + '"') -WindowStyle Hidden -Wait -PassThru
    if ($iconProcess.ExitCode -ne 0) { throw 'Icon creation failed.' }
    $compilerArguments += '/win32icon:' + $iconPath
    & $compiler $compilerArguments $sources
    if ($LASTEXITCODE -ne 0) { throw 'Compilation with icon failed.' }
}
$shell = New-Object -ComObject WScript.Shell
$shortcutPath = Join-Path $projectDirectory 'Автоклікер.lnk'
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $executable
$shortcut.WorkingDirectory = $outputDirectory
$shortcut.IconLocation = $executable + ',0'
$shortcut.Description = 'Автоклікер: миша та клавіатура'
$shortcut.Save()
Write-Output ('Built: ' + $executable)
Write-Output ('Shortcut: ' + $shortcutPath)
if ($Test) {
    $resultPath = Join-Path $projectDirectory 'test-results.txt'
    $testProcess = Start-Process -FilePath $executable -ArgumentList ('--self-test "' + $resultPath + '"') -WindowStyle Hidden -Wait -PassThru
    Get-Content -LiteralPath $resultPath -Encoding UTF8
    if ($testProcess.ExitCode -ne 0) { throw 'Self-tests failed.' }
}
