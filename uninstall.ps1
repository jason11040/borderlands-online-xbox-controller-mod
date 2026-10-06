param([string]$GameDirectory)
$ErrorActionPreference='Stop'
if(-not $GameDirectory -and (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'installation.json'))) { $GameDirectory=(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'installation.json') -Raw | ConvertFrom-Json).game }
if(-not $GameDirectory) { throw 'Use -GameDirectory with the folder containing BOL.exe.' }
if(Get-Process -Name BOL -ErrorAction SilentlyContinue) { throw 'Close the game before uninstalling controller support.' }
$controllerManaged=Join-Path (Resolve-Path -LiteralPath $GameDirectory).Path 'BOL_Data\Managed'
$controllerHost=Join-Path $controllerManaged 'Assembly-CSharp.dll'
$controllerDll=Join-Path $controllerManaged 'ControllerSupport.dll'
$controllerBinary=$PSScriptRoot
if(-not (Test-Path -LiteralPath (Join-Path $controllerBinary 'ControllerPatcher.exe'))) { $controllerBinary=Join-Path $PSScriptRoot 'dist' }
$controllerBackup=Join-Path $PSScriptRoot ('backups\uninstall-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6))
New-Item -ItemType Directory -Force $controllerBackup | Out-Null
Copy-Item -LiteralPath $controllerHost,$controllerDll -Destination $controllerBackup
$controllerOutput=Join-Path $controllerManaged ('controller-remove-'+[Guid]::NewGuid().ToString('N')+'.tmp')
try {
    & (Join-Path $controllerBinary 'ControllerPatcher.exe') remove $controllerHost $controllerDll $controllerOutput
    if($LASTEXITCODE -ne 0) { throw 'Uninstall validation failed; installed files preserved.' }
    [IO.File]::Replace($controllerOutput,$controllerHost,(Join-Path $controllerBackup 'Assembly-CSharp.atomic-backup.dll'))
    # Unhook current assembly rather than restoring an old backup, preserving other mods installed later.
    Move-Item -LiteralPath $controllerDll -Destination (Join-Path $controllerBackup 'ControllerSupport.removed.dll')
    Write-Output 'Controller hooks removed. Other mods and controller settings were preserved.'
} catch {
    Copy-Item -LiteralPath (Join-Path $controllerBackup 'Assembly-CSharp.dll'),(Join-Path $controllerBackup 'ControllerSupport.dll') -Destination $controllerManaged -Force
    throw
} finally { if(Test-Path -LiteralPath $controllerOutput) { Remove-Item -LiteralPath $controllerOutput } }
