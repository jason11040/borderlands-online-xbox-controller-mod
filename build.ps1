param([string]$GameDirectory=(Join-Path (Split-Path -Parent $PSScriptRoot) 'Game Files (Do Not Touch)'))
$ErrorActionPreference='Stop'
$controllerManaged=Join-Path $GameDirectory 'BOL_Data\Managed'
$controllerCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$controllerCecil=Join-Path $env:USERPROFILE '.nuget\packages\mono.cecil\0.11.6\lib\net40\Mono.Cecil.dll'
$controllerDist=Join-Path $PSScriptRoot 'dist'
if(-not (Test-Path -LiteralPath $controllerCecil)) { $controllerCecil=Join-Path $controllerDist 'Mono.Cecil.dll' }
New-Item -ItemType Directory -Force $controllerDist | Out-Null
$controllerReferences=@('mscorlib.dll','System.dll','System.Core.dll','UnityEngine.dll','Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','Photon3Unity3D.dll','LibUniSWF.dll','LibUniSWFInternal.dll') | ForEach-Object { '/reference:'+(Join-Path $controllerManaged $_) }
$controllerSources=@('PadCore.cs','NativeInput.cs','ControllerSupport.cs') | ForEach-Object { Join-Path $PSScriptRoot ('src\'+$_) }
& $controllerCompiler /nologo /noconfig /nostdlib /target:library ('/out:'+(Join-Path $controllerDist 'ControllerSupport.dll')) $controllerReferences $controllerSources
if($LASTEXITCODE -ne 0) { throw 'Controller mod compilation failed.' }
if([IO.Path]::GetFullPath($controllerCecil) -ne [IO.Path]::GetFullPath((Join-Path $controllerDist 'Mono.Cecil.dll'))) { Copy-Item -LiteralPath $controllerCecil -Destination (Join-Path $controllerDist 'Mono.Cecil.dll') -Force }
& $controllerCompiler /nologo /target:exe ('/out:'+(Join-Path $controllerDist 'ControllerPatcher.exe')) ('/reference:'+$controllerCecil) (Join-Path $PSScriptRoot 'tools\ControllerPatcher.cs')
if($LASTEXITCODE -ne 0) { throw 'Controller patcher compilation failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'install.ps1'),(Join-Path $PSScriptRoot 'uninstall.ps1'),(Join-Path $PSScriptRoot 'README.md') -Destination $controllerDist -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Mono.Cecil.LICENSE.txt') -Destination $controllerDist -Force
if(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'TUTORIAL.txt')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'TUTORIAL.txt') -Destination $controllerDist -Force }
Write-Output 'Built independent ControllerSupport.dll and legacy .NET installer tools.'
