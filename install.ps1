param([string]$GameDirectory)
$ErrorActionPreference='Stop'
if(-not $GameDirectory) {
    $controllerCandidates=@((Join-Path (Split-Path -Parent $PSScriptRoot) 'Game Files (Do Not Touch)'),(Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'Game Files (Do Not Touch)'))
    $GameDirectory=$controllerCandidates | Where-Object { Test-Path -LiteralPath (Join-Path $_ 'BOL_Data\Managed\Assembly-CSharp.dll') } | Select-Object -First 1
}
if(-not $GameDirectory) { throw 'Use -GameDirectory with the folder containing BOL.exe.' }
$controllerGame=(Resolve-Path -LiteralPath $GameDirectory).Path
$controllerManaged=Join-Path $controllerGame 'BOL_Data\Managed'
$controllerHost=Join-Path $controllerManaged 'Assembly-CSharp.dll'
$controllerBinary=$PSScriptRoot
if(-not (Test-Path -LiteralPath (Join-Path $controllerBinary 'ControllerSupport.dll'))) { $controllerBinary=Join-Path $PSScriptRoot 'dist' }
$controllerDll=Join-Path $controllerBinary 'ControllerSupport.dll'
$controllerPatcher=Join-Path $controllerBinary 'ControllerPatcher.exe'
if(-not (Test-Path -LiteralPath $controllerHost)) { throw 'Game Assembly-CSharp.dll was not found.' }
if(-not (Test-Path -LiteralPath $controllerDll)) { throw 'Run build.ps1 first, or use the compiled release package.' }
if(Get-Process -Name BOL -ErrorAction SilentlyContinue) { throw 'Close the game before installing controller support. The server can remain running.' }
$controllerBackup=Join-Path $PSScriptRoot ('backups\install-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6))
New-Item -ItemType Directory -Force $controllerBackup | Out-Null
Copy-Item -LiteralPath $controllerHost -Destination (Join-Path $controllerBackup 'Assembly-CSharp.dll')
$controllerDestination=Join-Path $controllerManaged 'ControllerSupport.dll'
$controllerHadDll=Test-Path -LiteralPath $controllerDestination
if($controllerHadDll) { Copy-Item -LiteralPath $controllerDestination -Destination (Join-Path $controllerBackup 'ControllerSupport.dll') }
$controllerOutput=Join-Path $controllerManaged ('controller-install-'+[Guid]::NewGuid().ToString('N')+'.tmp')
try {
    & $controllerPatcher patch $controllerHost $controllerDll $controllerOutput
    if($LASTEXITCODE -ne 0) { throw 'Controller patch verification failed; existing installation preserved.' }
    if(Get-Process -Name BOL -ErrorAction SilentlyContinue) { throw 'The game started during preparation. Close it and run the installer again.' }
    Copy-Item -LiteralPath $controllerDll -Destination $controllerDestination -Force
    [IO.File]::Replace($controllerOutput,$controllerHost,(Join-Path $controllerBackup 'Assembly-CSharp.atomic-backup.dll'))
    & $controllerPatcher verify $controllerHost $controllerDestination
    if($LASTEXITCODE -ne 0) { throw 'Installed hook verification failed.' }
    [ordered]@{game=$controllerGame;backup=$controllerBackup;installed=(Get-Date).ToString('s');hostHash=(Get-FileHash -LiteralPath $controllerHost).Hash;modHash=(Get-FileHash -LiteralPath $controllerDestination).Hash} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'installation.json')
    Write-Output "Controller support installed. Backup: $controllerBackup"
    Write-Output 'Start the game with your Xbox controller connected. F10 or hold Back+Start opens settings.'
} catch {
    Copy-Item -LiteralPath (Join-Path $controllerBackup 'Assembly-CSharp.dll') -Destination $controllerHost -Force
    if($controllerHadDll) { Copy-Item -LiteralPath (Join-Path $controllerBackup 'ControllerSupport.dll') -Destination $controllerDestination -Force }
    elseif(Test-Path -LiteralPath $controllerDestination) { Remove-Item -LiteralPath $controllerDestination }
    throw
} finally {
    if(Test-Path -LiteralPath $controllerOutput) { Remove-Item -LiteralPath $controllerOutput }
}
