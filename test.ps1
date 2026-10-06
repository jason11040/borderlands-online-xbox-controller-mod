param([string]$GameDirectory=(Join-Path (Split-Path -Parent $PSScriptRoot) 'Game Files (Do Not Touch)'),[string]$OriginalAssembly)
$ErrorActionPreference='Stop'
$controllerCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$controllerTests=Join-Path $PSScriptRoot 'tests'
$controllerDist=Join-Path $PSScriptRoot 'dist'
$controllerHost=Join-Path $GameDirectory 'BOL_Data\Managed\Assembly-CSharp.dll'
$controllerOriginal=if($OriginalAssembly) { $OriginalAssembly } else { $controllerHost }
if(-not (Test-Path -LiteralPath $controllerOriginal)) { throw 'Supply -OriginalAssembly with your local compatible, unmodified Assembly-CSharp.dll.' }
function Invoke-ControllerCheck { param([string]$Program,[string[]]$Arguments); & $Program @Arguments; if($LASTEXITCODE -ne 0) { throw ('Check failed: '+$Program) } }
Invoke-ControllerCheck $controllerCompiler @('/nologo','/target:exe','/platform:x86',('/out:'+(Join-Path $controllerTests 'PadSmoke.exe')),(Join-Path $PSScriptRoot 'src\PadCore.cs'),(Join-Path $PSScriptRoot 'src\NativeInput.cs'),(Join-Path $controllerTests 'PadSmoke.cs'))
Invoke-ControllerCheck (Join-Path $controllerTests 'PadSmoke.exe') @()
Invoke-ControllerCheck $controllerCompiler @('/nologo','/target:exe','/platform:x86',('/out:'+(Join-Path $controllerTests 'RuntimeSmoke.exe')),(Join-Path $PSScriptRoot 'src\PadCore.cs'),(Join-Path $PSScriptRoot 'src\ControllerSupport.cs'),(Join-Path $controllerTests 'RuntimeSmoke.cs'))
Invoke-ControllerCheck (Join-Path $controllerTests 'RuntimeSmoke.exe') @()
$controllerPatcher=Join-Path $controllerDist 'ControllerPatcher.exe'
$controllerDll=Join-Path $controllerDist 'ControllerSupport.dll'
$controllerBaseline=Join-Path $controllerTests 'Assembly-CSharp.baseline.dll'
$controllerStandalone=Join-Path $controllerTests 'Assembly-CSharp.standalone.dll'
$controllerCombined=Join-Path $controllerTests 'Assembly-CSharp.combined.dll'
$controllerReinstalled=Join-Path $controllerTests 'Assembly-CSharp.reinstalled.dll'
$controllerRemoved=Join-Path $controllerTests 'Assembly-CSharp.removed.dll'
Invoke-ControllerCheck $controllerPatcher @('patch',$controllerOriginal,$controllerDll,$controllerStandalone)
Invoke-ControllerCheck $controllerPatcher @('remove',$controllerHost,$controllerDll,$controllerBaseline)
Invoke-ControllerCheck $controllerPatcher @('patch',$controllerBaseline,$controllerDll,$controllerCombined)
Invoke-ControllerCheck $controllerPatcher @('patch',$controllerCombined,$controllerDll,$controllerReinstalled)
Invoke-ControllerCheck $controllerPatcher @('remove',$controllerCombined,$controllerDll,$controllerRemoved)
Invoke-ControllerCheck $controllerCompiler @('/nologo','/target:exe',('/out:'+(Join-Path $controllerTests 'AssemblySmoke.exe')),('/reference:'+(Join-Path $controllerDist 'Mono.Cecil.dll')),(Join-Path $controllerTests 'AssemblySmoke.cs'))
Copy-Item -LiteralPath (Join-Path $controllerDist 'Mono.Cecil.dll') -Destination (Join-Path $controllerTests 'Mono.Cecil.dll') -Force
Invoke-ControllerCheck (Join-Path $controllerTests 'AssemblySmoke.exe') @($controllerBaseline,$controllerCombined,$controllerReinstalled,$controllerRemoved,$controllerStandalone,$controllerDll)
Write-Output 'All standalone controller tests passed. Hardware and in-game UI verification remains required.'
