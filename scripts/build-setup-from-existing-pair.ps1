[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string] $PayloadBuildRecord,
    [Parameter(Mandatory=$true)][string] $StandardDirectory,
    [Parameter(Mandatory=$true)][string] $OutputDirectory
)
$ErrorActionPreference='Stop'
$repositoryRoot=(& git -C $PSScriptRoot rev-parse --show-toplevel).Trim()
if($LASTEXITCODE -ne 0){throw 'Git repository is required.'}
$source=(& git -C $repositoryRoot rev-parse HEAD).Trim()
# Only the installer is rebuilt. An unrelated preserved workspace asset does not become
# installer source; uncommitted installer/build-entry changes must not be labelled as HEAD.
$owned=@('src/JueMingR.Setup','scripts/build-setup-from-existing-pair.ps1','scripts/phase0s/PackageVerification.Support.ps1','scripts/phase0s/Read-TwoFilePackageMetadata.ps1','Directory.Build.props','global.json')
& git -C $repositoryRoot diff --quiet HEAD -- @owned
if($LASTEXITCODE -ne 0){throw 'Commit the installer and its build entry before delivery.'}
$untracked=@(& git -C $repositoryRoot ls-files --others --exclude-standard -- @owned)
if($untracked.Count -ne 0){throw 'Untracked installer source cannot be delivered as committed HEAD.'}
$PayloadBuildRecord=[IO.Path]::GetFullPath($PayloadBuildRecord)
$StandardDirectory=[IO.Path]::GetFullPath($StandardDirectory)
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
$outputPrefix=[IO.Path]::GetFullPath((Join-Path $repositoryRoot 'outputs'))+[IO.Path]::DirectorySeparatorChar
if(-not $OutputDirectory.StartsWith($outputPrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Use a new directory below repository outputs.'}
if(Test-Path -LiteralPath $OutputDirectory){throw 'The output directory must not already exist.'}
$ancestor=Split-Path -Parent $OutputDirectory
while($ancestor){
    if(Test-Path -LiteralPath $ancestor){if((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Output ancestors must not be reparse paths.'}}
    $ancestor=Split-Path -Parent $ancestor
}
$record=Get-Content -LiteralPath $PayloadBuildRecord -Raw -Encoding UTF8 | ConvertFrom-Json
if($record.distributionFormat -cne 'two-file-1'){throw 'Only a verified two-file payload can be reused.'}
$reader=Join-Path $PSScriptRoot 'phase0s/Read-TwoFilePackageMetadata.ps1'
$originalSetup=Join-Path (Split-Path -Parent $PayloadBuildRecord) $record.setup.fileName
$verified=@(& powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $reader -StandardDirectory $StandardDirectory -BuildRecordPath $PayloadBuildRecord -SetupPath $originalSetup)
if($LASTEXITCODE -ne 0){throw 'Original frozen pair/Setup verification failed.'}
$originalRecordHash=(Get-FileHash -LiteralPath $PayloadBuildRecord -Algorithm SHA256).Hash
[void][IO.Directory]::CreateDirectory($OutputDirectory)
$buildRoot=Join-Path $OutputDirectory '.build'
& dotnet.exe build (Join-Path $repositoryRoot 'src/JueMingR.Setup/JueMingR.Setup.csproj') -c Release --nologo -p:Platform=x86 "-p:SourceRevisionId=$source" "-p:JueMingRSetupPayloadDirectory=$StandardDirectory" "-p:JueMingRBuildRoot=$buildRoot"
if($LASTEXITCODE -ne 0){throw 'Installer build failed.'}
$fileName='JueMingR-Setup-'+$source.Substring(0,8)+'.exe'
$setupPath=Join-Path $OutputDirectory $fileName
[IO.File]::Copy((Join-Path $buildRoot 'bin/JueMingR.Setup/x86/Release/net472/JueMingR.Setup.exe'),$setupPath)
# Keep sourceCommit as the actual game payload source for existing consumers. The new
# installer source is explicit and separate; the original record and ZIP remain untouched.
$record.setup.fileName=$fileName
$record.setup.length=(Get-Item -LiteralPath $setupPath).Length
$record.setup.sha256=(Get-FileHash -LiteralPath $setupPath -Algorithm SHA256).Hash
$record | Add-Member -NotePropertyName installerSource -NotePropertyValue $source -Force
$record | Add-Member -NotePropertyName payloadSource -NotePropertyValue $record.sourceCommit -Force
$updatedRecord=Join-Path $OutputDirectory 'payload-package-record.json'
$record | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $updatedRecord -Encoding UTF8
$samePair=@(& powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $reader -StandardDirectory $StandardDirectory -BuildRecordPath $updatedRecord -SetupPath $setupPath)
if($LASTEXITCODE -ne 0){throw 'New Setup differs from the frozen pair.'}
$samePair | Set-Content -LiteralPath (Join-Path $OutputDirectory 'embedded-pair-verification.json') -Encoding UTF8
$result=[ordered]@{
    schema='setup-rebuild-1';status='PASS';payloadSource=$record.sourceCommit;installerSource=$source
    payloadBuildRecord=$PayloadBuildRecord;payloadBuildRecordSha256=$originalRecordHash
    standardDirectory=$StandardDirectory;pair=$record.packageDirectory.files;setup=$record.setup
    validation='Existing strict pair/index/embedded Setup reader; original game payload was not rebuilt.'
    gameStarted=$false;installed=$false
}
$result | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'setup-rebuild-record.json') -Encoding UTF8
$result | ConvertTo-Json -Depth 20
