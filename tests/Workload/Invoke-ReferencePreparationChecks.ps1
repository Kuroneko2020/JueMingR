[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$TerrariaExePath,
    [Parameter(Mandatory=$true)][string]$XnaGameAssemblyPath,
    [Parameter(Mandatory=$true)][string]$XnaFrameworkAssemblyPath,
    [Parameter(Mandatory=$true)][string]$XnaGraphicsAssemblyPath
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2.0
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $repository 'scripts/workload/Workload.Support.ps1')
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('JueMingR-reference-preparation-'+[Guid]::NewGuid().ToString('N'))
$checks=0
function Assert-Preparation([bool]$Pass,[string]$Reason){if(!$Pass){throw ('Reference preparation: '+$Reason)};$script:checks++}
function Invoke-Preparation([switch]$Verify,[switch]$ExpectFailure){
    $arguments=@('-NoProfile','-File',(Join-Path $fixture 'scripts/prepare-terraria-references.ps1'))
    if($Verify){$arguments+='-VerifyOnly'}else{
        $arguments+=@('-TerrariaExePath',(Join-Path $fixture 'original/Terraria.exe'),'-XnaGameAssemblyPath',$XnaGameAssemblyPath,'-XnaFrameworkAssemblyPath',$XnaFrameworkAssemblyPath,'-XnaGraphicsAssemblyPath',$XnaGraphicsAssemblyPath)
    }
    $previousPreference=$ErrorActionPreference
    try{$ErrorActionPreference='Continue';$lines=@(& powershell.exe @arguments 2>&1);$code=$LASTEXITCODE}
    finally{$ErrorActionPreference=$previousPreference}
    foreach($line in $lines){Write-Host ([string]$line)}
    Assert-Preparation (($ExpectFailure -and $code -ne 0) -or (!$ExpectFailure -and $code -eq 0)) ('actual prepare exit='+$code+' verify='+$Verify+' expectedFailure='+$ExpectFailure)
}
function Get-ReferenceState {
    return (@(Get-ChildItem -LiteralPath (Join-Path $fixture 'external/TerrariaRefs') -File | Sort-Object Name | ForEach-Object {$_.Name+':'+$_.Length+':'+(Get-FileHash -LiteralPath $_.FullName).Hash}) -join '|')
}
try{
    foreach($directory in @('eng','scripts','original')){[IO.Directory]::CreateDirectory((Join-Path $fixture $directory))|Out-Null}
    Copy-Item -LiteralPath (Join-Path $repository 'eng/TerrariaReferences.baseline.json') -Destination (Join-Path $fixture 'eng/TerrariaReferences.baseline.json')
    Copy-Item -LiteralPath (Join-Path $repository 'scripts/prepare-terraria-references.ps1') -Destination (Join-Path $fixture 'scripts/prepare-terraria-references.ps1')
    # Only explicit, already authorized original bytes enter this disposable
    # source. No real installation, Content or reference set is modified.
    Copy-Item -LiteralPath $TerrariaExePath -Destination (Join-Path $fixture 'original/Terraria.exe')
    foreach($asset in @(Get-WorkloadNativeContentScopes|ForEach-Object{Get-WorkloadNativeContentFiles $_}|Sort-Object -Unique)){
        $target=Join-Path $fixture ('original/Content/'+$asset)
        [IO.Directory]::CreateDirectory((Split-Path -Parent $target))|Out-Null
        Copy-Item -LiteralPath (Join-Path (Split-Path -Parent $TerrariaExePath) ('Content/'+$asset)) -Destination $target
    }
    $redirect=Join-Path $fixture 'redirected-external';[IO.Directory]::CreateDirectory($redirect)|Out-Null
    $external=Join-Path $fixture 'external'
    New-Item -ItemType Junction -Path $external -Target $redirect|Out-Null
    try{
        Invoke-Preparation -ExpectFailure
        Assert-Preparation (@(Get-ChildItem -LiteralPath $redirect -Force).Count -eq 0) 'reparse parent rejected before creating any stage or reference bytes'
    }finally{[IO.Directory]::Delete($external)}
    Invoke-Preparation
    $content=Get-WorkloadNativeContentArgument $fixture 'BlueprintInput'
    Assert-Preparation ($content -ceq (Join-Path $fixture 'original/Content')) 'fresh formal prepare publishes provenance consumed by the actual reader'
    $receiptPath=Join-Path $fixture 'external/TerrariaRefs/.juemingr-reference-set.json'
    $receipt=Get-Content -LiteralPath $receiptPath -Raw|ConvertFrom-Json
    Assert-Preparation ($receipt.sourceHashesUnchanged -and @($receipt.files).Count -eq 5) 'five verified references and rechecked explicit source'
    foreach($scope in @(Get-WorkloadNativeContentScopes)){Assert-Preparation ((Get-WorkloadNativeContentArgument $fixture $scope) -ceq $content) ('formal consumer '+$scope)}
    $before=Get-ReferenceState;Invoke-Preparation -Verify;Assert-Preparation ((Get-ReferenceState) -ceq $before) 'VerifyOnly writes no reference or receipt bytes'
    Remove-Item -LiteralPath $receiptPath
    Invoke-Preparation -Verify
    Assert-Preparation (![IO.File]::Exists($receiptPath)) 'VerifyOnly does not silently repair missing provenance'
    Invoke-Preparation
    Assert-Preparation ((Get-WorkloadNativeContentArgument $fixture 'NpcBetsy') -ceq $content) 'already valid five-reference branch repairs missing receipt through formal producer'
    $witness=Join-Path $fixture 'external/TerrariaRefs/unrelated-owner-note.txt';[IO.File]::WriteAllText($witness,'keep exact unrelated bytes')
    [IO.File]::AppendAllText((Join-Path $fixture 'external/TerrariaRefs/Microsoft.Xna.Framework.dll'),'isolated incomplete set')
    Invoke-Preparation
    Assert-Preparation ([IO.File]::ReadAllText($witness) -ceq 'keep exact unrelated bytes') 'incomplete-set restart replaces only owned references and preserves unrelated files'
    Assert-Preparation ((Get-WorkloadNativeContentArgument $fixture 'NpcBetsy') -ceq $content) 'repaired partial set publishes provenance only after all five identities validate'
    [IO.File]::WriteAllText($receiptPath,'{broken')
    $rejected=$false;try{Get-WorkloadNativeContentArgument $fixture 'NpcBetsy'|Out-Null}catch{$rejected=$true}
    Assert-Preparation $rejected 'corrupt receipt rejected'
    Invoke-Preparation
    $receipt=Get-Content -LiteralPath $receiptPath -Raw|ConvertFrom-Json;$receipt.profileId='wrong-profile'
    [IO.File]::WriteAllText($receiptPath,($receipt|ConvertTo-Json -Depth 8))
    $rejected=$false;try{Get-WorkloadNativeContentArgument $fixture 'NpcBetsy'|Out-Null}catch{$rejected=$true}
    Assert-Preparation $rejected 'wrong profile rejected'
    Invoke-Preparation
    $assetPath=Join-Path $fixture 'original/Content/Images/Item_26.xnb';$beforeRows=Get-WorkloadNativeContentRows $fixture
    [IO.File]::AppendAllText($assetPath,'isolated byte mutation')
    Assert-Preparation ((Get-WorkloadHash $beforeRows) -cne (Get-WorkloadHash @(Get-WorkloadNativeContentRows $fixture))) 'actual original resource bytes remain bound after preparation'
    Remove-Item -LiteralPath $assetPath
    $rejected=$false;try{Get-WorkloadNativeContentArgument $fixture 'BlueprintInput'|Out-Null}catch{$rejected=$true}
    Assert-Preparation $rejected 'missing consuming resource rejected'
    Assert-Preparation ((Get-WorkloadNativeContentArgument $fixture 'NpcProduction') -ceq '--cpu') 'independent CPU scope does not require Content'
    $oldReceipt=(Get-FileHash -LiteralPath $receiptPath).Hash
    [IO.File]::AppendAllText((Join-Path $fixture 'original/Terraria.exe'),'isolated source mutation')
    Invoke-Preparation -ExpectFailure
    Assert-Preparation ((Get-FileHash -LiteralPath $receiptPath).Hash -ceq $oldReceipt) 'failed source validation does not publish a new receipt'
    $rejected=$false;try{Get-WorkloadNativeContentArgument $fixture 'NpcBetsy'|Out-Null}catch{$rejected=$true}
    Assert-Preparation $rejected 'source change is rejected independently of prepared reference validity'
    Copy-Item -LiteralPath $TerrariaExePath -Destination (Join-Path $fixture 'original/Terraria.exe') -Force
    Invoke-Preparation
    Assert-Preparation ((Get-WorkloadNativeContentArgument $fixture 'NpcBetsy') -ceq $content) 'restart after partial failure returns to verified producer'
    Write-Output ('PASS reference preparation checks='+$checks+'; actual prepare/reader, explicit original bytes, no installation or game execution.')
}finally{
    $absolute=[IO.Path]::GetFullPath($fixture);$temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if(!$absolute.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -or (Split-Path -Leaf $absolute) -notlike 'JueMingR-reference-preparation-*'){throw 'Refuse preparation fixture cleanup outside exact temporary root'}
    if([IO.Directory]::Exists($absolute)){Remove-Item -LiteralPath $absolute -Recurse -Force}
}
