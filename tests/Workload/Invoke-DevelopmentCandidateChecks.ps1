[CmdletBinding()]
param([string] $OutputDirectory)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2.0
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $projectRoot 'scripts/workload/Workload.Support.ps1')
if(-not $OutputDirectory){$OutputDirectory=Join-Path $projectRoot ('.local/movement-assists-20261019/closure-candidate-tests/'+[Guid]::NewGuid().ToString('N'))}
if(Test-Path -LiteralPath $OutputDirectory){throw 'Development candidate checks require a fresh isolated output directory.'}
$testRoot=[IO.Path]::GetFullPath($OutputDirectory)
$null=New-Item -ItemType Directory -Path (Join-Path $testRoot '.local') -Force
$null=New-Item -ItemType Directory -Path (Join-Path $testRoot 'artifacts/build/Debug') -Force
$results=New-Object 'System.Collections.Generic.List[string]'
function Assert-Candidate([bool] $Condition,[string] $Message){if(-not $Condition){throw ('Development candidate contract: '+$Message)};$results.Add($Message)}
function Assert-CandidateReject([scriptblock] $Action,[string] $Message){$rejected=$false;try{& $Action|Out-Null}catch{$rejected=$true};Assert-Candidate $rejected $Message}
function Copy-Candidate($Value){return $Value|ConvertTo-Json -Depth 20|ConvertFrom-Json}
function New-CandidateArtifact([string] $Name,[string] $Content){
    $relative='.local/'+$Name
    $full=Join-Path $testRoot $relative
    [IO.File]::WriteAllText($full,$Content,(New-Object Text.UTF8Encoding($false)))
    return [pscustomobject]@{path=$relative;sha256=(Get-WorkloadFileHash -LiteralPath $full).Hash;length=(Get-Item -LiteralPath $full).Length}
}
# Only external Git state, compilation identity and the executable catalogue are
# synthetic. Actual routing, plan, original evidence applicability, artifact
# validation, schema dispatch and qualification rejection remain production code.
$script:candidateDirty=$false
$script:candidateBuildMatches=$true
$script:candidateUnknown=$false
$script:candidateAncestor='a'*40
$script:WorkloadRetainedAssets=$null
function Invoke-WorkloadGit {param([string] $Root,[string[]] $Arguments);return $script:candidateAncestor}
function Get-WorkloadChanges {
    param([string] $Root,[string] $Baseline)
    if($Baseline -ceq 'HEAD'){return [ordered]@{baseline=$Baseline;reason=$null;paths=@($(if($script:candidateDirty){'src/Unknown.cs'}))}}
    return [ordered]@{baseline=('b'*40);reason=$null;paths=@($(if($script:candidateUnknown){'unclassified/unknown.exe'}elseif($Baseline){'src/JueMingR.Features/Movement/MovementFeature.cs'}else{'docs/README.md'}))}
}
function Test-WorkloadBuildMatch {param($Root,$Record,$Identity,$InputIdentity);return $script:candidateBuildMatches}
function Get-DevelopmentCandidateCatalogue {param($Architecture);return @('synthetic-core|core','synthetic-movement|movement-host')}
$commit='c'*40
$identity=[pscustomobject]@{commit=$commit;fingerprint=('A'*64)}
$inputs=[pscustomobject]@{inputs=@('environment:synthetic:A');fingerprint=Get-WorkloadHash @('environment:synthetic:A')}
Write-WorkloadJson (Join-Path $testRoot 'artifacts/build/Debug/build-record.json') ([ordered]@{commit=$commit})
$plan=Get-DevelopmentCandidatePlan $testRoot $script:candidateAncestor $identity $inputs
Assert-Candidate ($plan.paths -contains 'src/JueMingR.Features/Movement/MovementFeature.cs' -and $plan.paths -contains 'docs/README.md') 'formal plan combines task BASE with main cumulative responsibility'
Assert-Candidate ($plan.checks.name -contains 'native-MovementJumpCpu' -and $plan.checks.name -contains 'synthetic-core') 'formal route and generator retain actual movement and catalogue consumers'
$auth=New-CandidateArtifact 'owner-instruction.txt' 'SYNTHETIC owner-test task provenance; never an actual human authorization.'
$review=New-CandidateArtifact 'review.txt' 'SYNTHETIC independent review fixture; no historical execution claim.'
$consumer=New-CandidateArtifact 'consumer.bin' 'Synthetic execution consumer bytes'
$first=@($plan.checks | Where-Object {$_.name -ceq 'synthetic-core'})[0]
$original=[pscustomobject][ordered]@{schemaVersion=2;status='PASS';name=$first.name;signature=Get-WorkloadHash (@($first.executable)+@($first.arguments));
    inputFingerprint=Get-WorkloadCheckFingerprint $inputs $first.name;allInputFingerprint=$inputs.fingerprint;inputs=$inputs.inputs;
    executionId=('d'*32);sourceCommit=$commit;sourceFingerprint=$identity.fingerprint;detectionCommit=$commit;milliseconds=1;
    outputs=@([pscustomobject]@{path=(Join-Path $testRoot $consumer.path);livePath=(Join-Path $testRoot $consumer.path);length=$consumer.length;sha256=$consumer.sha256})}
$evidence=New-CandidateArtifact 'original-check.json' ($original|ConvertTo-Json -Depth 10)
Assert-Candidate (Test-WorkloadReusable $testRoot $original $inputs $first.name $original.signature) 'synthetic original is validated by the existing evidence applicability reader'
$checks=@($plan.checks|ForEach-Object {if($_.name -ceq $first.name){[ordered]@{name=$_.name;status='REUSED';evidence=$evidence}}else{[ordered]@{name=$_.name;status='NOT_EXECUTED';reason='Synthetic pending responsibility for schema test'}}})
$record=[pscustomobject][ordered]@{schema='development-candidate-1';purpose='OWNER_TEST';taskReference='https://github.com/Kuroneko2020/JueMingR/issues/999999';testPurpose='Synthetic schema contract only';
    candidateCommit=$commit;sourceFingerprint=$identity.fingerprint;inputFingerprint=$inputs.fingerprint;productBase=$script:candidateAncestor;
    fullQualification='INCOMPLETE';releaseEligible=$false;openFailures=@();authorization=[ordered]@{reference='https://github.com/Kuroneko2020/JueMingR/issues/999999';artifact=$auth};
    package=[ordered]@{profile='ContinuousProcessing';content='ORDINARY_COMPLETE';packageId=('continuous-processing-'+$commit)};
    planFingerprint=$plan.fingerprint;checks=$checks;unfinishedResponsibilities=@($checks|Where-Object {$_.status -ceq 'NOT_EXECUTED'}|ForEach-Object {$_.name});
    reviews=@([ordered]@{result='APPROVED_FOR_STAGE';sourceFingerprint=$identity.fingerprint;coverage='Synthetic formal plan and incomplete qualification';artifact=$review});limits=@('Synthetic only; no game, product package, install or actual task qualification')}
$recordPath=Join-Path $testRoot '.local/candidate.json'
Write-WorkloadJson $recordPath $record
$stage=Read-AimStageDevelopment $testRoot $recordPath $identity $inputs
Assert-Candidate ($stage.mode -ceq 'DevelopmentCandidate' -and $stage.checkCount -eq $plan.checks.Count -and $stage.provedCheckCount -eq 1) 'stable schema dispatch reads a legal constructed candidate without a dated task branch'
Assert-Candidate ($stage.fullQualification -ceq 'INCOMPLETE' -and -not $stage.releaseEligible -and -not $stage.installationAuthorized) 'legal owner-test record never grants complete qualification, release or automatic installation'
Assert-Candidate (-not (Test-WorkloadCoverage ([pscustomobject]$stage) @($plan.checks.name) $inputs.fingerprint)) 'ordinary complete qualification consumer rejects the stage result'
function Read-CandidateFixture($Value){Write-WorkloadJson $recordPath $Value;Read-AimStageDevelopment $testRoot $recordPath $identity $inputs}
$bad=Copy-Candidate $record;$bad.checks=@($bad.checks|Where-Object {$_.name -ceq $first.name})
Assert-CandidateReject {Read-CandidateFixture $bad} 'narrowed caller check list cannot replace formal route responsibility'
$bad=Copy-Candidate $record;$bad|Add-Member requiredChecks @($first.name)
Assert-CandidateReject {Read-CandidateFixture $bad} 'extra requiredChecks field cannot forge a smaller plan'
$bad=Copy-Candidate $record;$bad.candidateCommit='e'*40
Assert-CandidateReject {Read-CandidateFixture $bad} 'mismatched candidate rejected'
$bad=Copy-Candidate $record;$bad.planFingerprint='F'*64
Assert-CandidateReject {Read-CandidateFixture $bad} 'mismatched formal plan fingerprint rejected'
$bad=Copy-Candidate $record;$bad.checks[0].evidence.sha256='0'*64
Assert-CandidateReject {Read-CandidateFixture $bad} 'missing or altered evidence rejected'
$bad=Copy-Candidate $record;$bad.authorization=$null
Assert-CandidateReject {Read-CandidateFixture $bad} 'missing authorization reference cannot prepare a candidate'
$bad=Copy-Candidate $record;$bad.authorization.artifact.path='.local/missing-authorization.txt'
Assert-CandidateReject {Read-CandidateFixture $bad} 'unavailable original authorization source rejected'
$bad=Copy-Candidate $record;$bad.package.profile='Phase0S'
Assert-CandidateReject {Read-CandidateFixture $bad} 'diagnostic or reduced package cannot masquerade as ordinary complete candidate'
$bad=Copy-Candidate $record;$bad.package.packageId='continuous-processing-wrong'
Assert-CandidateReject {Read-CandidateFixture $bad} 'package product identity remains bound to candidate'
$bad=Copy-Candidate $record;$bad.openFailures=@('known product failure')
Assert-CandidateReject {Read-CandidateFixture $bad} 'known open failure cannot be concealed by owner-test status'
$bad=Copy-Candidate $record;$bad.fullQualification='PASS';$bad.releaseEligible=$true
Assert-CandidateReject {Read-CandidateFixture $bad} 'caller cannot promote stage to full or release qualification'
$bad=Copy-Candidate $record;$bad.unfinishedResponsibilities=@()
Assert-CandidateReject {Read-CandidateFixture $bad} 'pending checks remain explicit and cannot be silently dropped'
$bad=Copy-Candidate $record;$bad.checks[0].status='FAIL'
Assert-CandidateReject {Read-CandidateFixture $bad} 'failed evidence cannot become a legal ordinary owner-test result'
$script:candidateBuildMatches=$false
Assert-CandidateReject {Read-CandidateFixture $record} 'stale Debug/catalogue input rejected before planning'
$script:candidateBuildMatches=$true;$script:candidateUnknown=$true
Assert-CandidateReject {Read-CandidateFixture $record} 'unknown executable path is not zero responsibility'
$script:candidateUnknown=$false;$script:candidateDirty=$true
Assert-CandidateReject {Read-CandidateFixture $record} 'unknown uncommitted inputs are not authorized by record text'
$script:candidateDirty=$false
Write-WorkloadJson $recordPath $record
# Exercise the existing package admission function with a real isolated output
# file. This is an identity/consumer fixture, not a compiler or product build.
function Get-WorkloadIdentity {param($Root);return $identity}
function Get-WorkloadEvidenceInput {param($Root,$Identity);return $inputs}
$releaseRoot=Join-Path $testRoot 'artifacts/build/Release/work'
$null=New-Item -ItemType Directory -Path (Join-Path $releaseRoot 'bin') -Force
$releaseFile=Join-Path $releaseRoot 'bin/synthetic.dll'
[IO.File]::WriteAllText($releaseFile,'Synthetic Release consumer; not a production assembly')
$release=[pscustomobject][ordered]@{schemaVersion=4;configuration='Release';sdk='10.0.203';clean=$true;commit=$commit;sourceFingerprint=$identity.fingerprint;inputFingerprint=$inputs.fingerprint;
    outputs=@([pscustomobject]@{path='bin/synthetic.dll';length=(Get-Item -LiteralPath $releaseFile).Length;sha256=(Get-WorkloadFileHash -LiteralPath $releaseFile).Hash});workload=[pscustomobject]$stage}
Assert-Candidate (Test-AimStageDevelopmentBuild $testRoot $release $recordPath) 'existing stage Release consumer accepts the stable candidate with exact isolated outputs'
$packageTokens=$null;$packageErrors=$null
$packageAst=[Management.Automation.Language.Parser]::ParseFile((Join-Path $projectRoot 'scripts/build-phase0s-validation-package.ps1'),[ref]$packageTokens,[ref]$packageErrors)
$admissionFunction=@($packageAst.FindAll({param($node)$node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq 'Test-PackageBuildAdmission'},$true))
Assert-Candidate ($packageErrors.Count -eq 0 -and $admissionFunction.Count -eq 1) 'actual package entry exposes one parseable existing admission consumer'
. ([scriptblock]::Create($admissionFunction[0].Extent.Text))
$repositoryRoot=$testRoot;$StageDevelopmentRecord=$recordPath;$admission=$null;$WorkloadBaseline=$null
Assert-Candidate (Test-PackageBuildAdmission $release) 'actual package admission forwards stable schema through existing stage validation'
[IO.File]::AppendAllText($releaseFile,'changed')
Assert-Candidate (-not (Test-PackageBuildAdmission $release)) 'actual package admission rejects changed Release payload'
[IO.File]::WriteAllText($releaseFile,'Synthetic Release consumer; not a production assembly')
$StageDevelopmentRecord=$null
Assert-Candidate (-not (Test-PackageBuildAdmission $release)) 'ordinary package admission does not promote a stage result'
$summary=[ordered]@{status='PASS';scope='development-candidate-schema';synthetic=$true;assertions=@($results.ToArray());count=$results.Count;productQualification='NOT_EVALUATED';installation='NOT_PERFORMED'}
Write-WorkloadJson (Join-Path $testRoot 'summary.json') $summary
Write-Output ('Development candidate checks PASS: '+$results.Count+' assertions; synthetic records only; '+$testRoot)
