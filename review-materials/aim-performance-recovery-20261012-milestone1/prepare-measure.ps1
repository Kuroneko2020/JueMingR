param([string]$Commit='75a1072cba59fbb84f587ec5615b8f32a9cedcb5',[string]$PackageDirectory='outputs/aim-root-fix-20261012-75a1072')
$ErrorActionPreference='Stop'
$taskRepo=(git rev-parse --show-toplevel).Trim()
$taskRoot=Join-Path $taskRepo '.local/aim-performance-recovery-20261012'
$taskZip=Join-Path $taskRepo ($PackageDirectory+'/JueMingR-Continuous-Processing-'+$Commit+'.zip')
$taskExtract=Join-Path $taskRoot ($Commit.Substring(0,3)+'-z')
Add-Type -AssemblyName System.IO.Compression.FileSystem
if(-not(Test-Path -LiteralPath $taskExtract)){[IO.Compression.ZipFile]::ExtractToDirectory($taskZip,$taskExtract)}
$taskPackage=Join-Path $taskExtract ('JueMingR-Continuous-Processing-'+$Commit)
$taskManifest=Get-Content -LiteralPath (Join-Path $taskPackage 'phase-0-s-package.manifest.json') -Raw -Encoding utf8|ConvertFrom-Json
if($taskManifest.sourceCommit -cne $Commit){throw 'Wrong fixed package source'}
foreach($taskEntry in $taskManifest.payload){$taskPath=Join-Path (Join-Path $taskPackage 'payload') $taskEntry.installRelativePath;if((Get-FileHash -LiteralPath $taskPath).Hash -cne $taskEntry.sha256 -or (Get-Item -LiteralPath $taskPath).Length -ne $taskEntry.length){throw 'Fixed package payload mismatch'}}
$taskPayload=Join-Path $taskPackage 'payload/JueMingR.Validation'
$taskRefs=@('System.Core.dll',(Join-Path $taskPayload 'JueMingR.Features.dll'),(Join-Path $taskPayload 'JueMingR.Platform.dll'),(Join-Path $taskPayload '0Harmony.dll'))
foreach($taskFile in @('Terraria.exe','ReLogic.dll','Microsoft.Xna.Framework.dll','Microsoft.Xna.Framework.Game.dll','Microsoft.Xna.Framework.Graphics.dll')){$taskRefs+=Join-Path $taskRepo ('external/TerrariaRefs/'+$taskFile)}
$taskArgs=@('/nologo','/target:exe','/platform:x86','/optimize+',('/out:'+(Join-Path $taskRoot 'ContinuousCombatMeasure.exe')))
$taskArgs+=($taskRefs|ForEach-Object {'/reference:'+$_});$taskArgs+=(Join-Path $taskRoot 'ContinuousCombatMeasure.cs')
& 'C:/Windows/Microsoft.NET/Framework/v4.0.30319/csc.exe' $taskArgs
if($LASTEXITCODE -ne 0){throw 'Private measurement compile failed'}
Write-Output ('PAYLOAD '+$taskPayload)
