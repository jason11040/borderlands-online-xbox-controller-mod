$ErrorActionPreference='Stop'
if(Get-Process -Name BOL -ErrorAction SilentlyContinue) { throw 'Installer fixture tests require the game closed, matching the production installer.' }
$controllerProject=Split-Path -Parent $PSScriptRoot
$controllerFixture=Join-Path $PSScriptRoot ('installer-'+[Guid]::NewGuid().ToString('N'))
$controllerPackage=Join-Path $controllerFixture 'package'
$controllerFixtureGame=Join-Path $controllerFixture 'game'
$controllerFixtureManaged=Join-Path $controllerFixtureGame 'BOL_Data\Managed'
New-Item -ItemType Directory -Force $controllerPackage,$controllerFixtureManaged | Out-Null
Get-ChildItem -LiteralPath (Join-Path $controllerProject 'dist') -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $controllerPackage }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Assembly-CSharp.baseline.dll') -Destination (Join-Path $controllerFixtureManaged 'Assembly-CSharp.dll')
Set-Content -LiteralPath (Join-Path $controllerFixtureManaged 'ControllerSupport.ini') -Value 'user-customization=preserve'
& (Join-Path $controllerPackage 'install.ps1') -GameDirectory $controllerFixtureGame
$controllerFirst=(Get-FileHash -LiteralPath (Join-Path $controllerFixtureManaged 'Assembly-CSharp.dll')).Hash
& (Join-Path $controllerPackage 'install.ps1') -GameDirectory $controllerFixtureGame
$controllerSecond=(Get-FileHash -LiteralPath (Join-Path $controllerFixtureManaged 'Assembly-CSharp.dll')).Hash
if($controllerFirst -ne $controllerSecond) { throw 'Repeated installation changed the assembly.' }
if((Get-Content -LiteralPath (Join-Path $controllerFixtureManaged 'ControllerSupport.ini') -Raw).Trim() -ne 'user-customization=preserve') { throw 'Installer overwrote user configuration.' }
& (Join-Path $controllerPackage 'uninstall.ps1') -GameDirectory $controllerFixtureGame
if(Test-Path -LiteralPath (Join-Path $controllerFixtureManaged 'ControllerSupport.dll')) { throw 'Uninstaller left runtime DLL behind.' }
& (Join-Path $PSScriptRoot 'AssemblySmoke.exe') (Join-Path $PSScriptRoot 'Assembly-CSharp.baseline.dll') (Join-Path $PSScriptRoot 'Assembly-CSharp.combined.dll') (Join-Path $PSScriptRoot 'Assembly-CSharp.reinstalled.dll') (Join-Path $controllerFixtureManaged 'Assembly-CSharp.dll') (Join-Path $PSScriptRoot 'Assembly-CSharp.standalone.dll') (Join-Path $controllerPackage 'ControllerSupport.dll')
if($LASTEXITCODE -ne 0) { throw 'Installer/uninstaller did not preserve other methods or mods.' }
if((Get-Content -LiteralPath (Join-Path $controllerFixtureManaged 'ControllerSupport.ini') -Raw).Trim() -ne 'user-customization=preserve') { throw 'Uninstaller deleted user configuration.' }
Write-Output 'PASS: Portable installer, repeat installation, uninstall, config preservation and unrelated mod preservation.'
