$ErrorActionPreference='Stop'
Set-StrictMode -Version 2
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $root 'scripts/workload/Workload.Support.ps1')
function Assert-Failure($value,[string]$reason){if(-not $value){throw $reason}}
$temporary=Join-Path ([IO.Path]::GetTempPath()) ('JueMingR-failure-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($temporary)|Out-Null
try {
    $layoutRoot=Join-Path $temporary 'layout';$checks=Join-Path $layoutRoot 'checks';$layout=Join-Path $checks 'prediction-worker-layout'
    Assert-Failure (-not (Test-WorkloadDependencyLayout $layoutRoot $checks)) 'Missing integration preparation is not a reusable layout'
    [IO.Directory]::CreateDirectory((Join-Path $layout 'prediction-materials'))|Out-Null
    [IO.File]::WriteAllText((Join-Path $layout 'prediction-materials/owned.image'),'readiness only; actual private consumer authenticates bytes')
    foreach($name in @('JueMingR.PredictionWorker','JueMingR.TerrariaHost','JueMingR.Platform','JueMingR.Features','JueMingR.Infrastructure')){
        $suffix=if($name -ceq 'JueMingR.PredictionWorker'){'.exe'}else{'.dll'}
        foreach($extension in @($suffix)+$(if($suffix -ceq '.exe'){@('.exe.config')}else{@()})){
            $path=Join-Path $layoutRoot ('artifacts/build/Debug/work/bin/'+$name+'/x86/Debug/net472/'+$name+$extension)
            [IO.Directory]::CreateDirectory((Split-Path -Parent $path))|Out-Null
            [IO.File]::WriteAllText($path,('owned readiness '+$name+$extension));Copy-Item $path (Join-Path $layout ($name+$extension))
        }
    }
    $harmony=Join-Path $layoutRoot 'external/Harmony/0Harmony.dll';[IO.Directory]::CreateDirectory((Split-Path -Parent $harmony))|Out-Null
    [IO.File]::WriteAllText($harmony,'owned readiness Harmony');Copy-Item $harmony (Join-Path $layout '0Harmony.dll')
    Assert-Failure (Test-WorkloadDependencyLayout $layoutRoot $checks) 'Matching current modules/configuration and staged materials make preparation ready'
    [IO.File]::AppendAllText((Join-Path $layout 'JueMingR.Features.dll'),'changed')
    Assert-Failure (-not (Test-WorkloadDependencyLayout $layoutRoot $checks)) 'Changed staged product bytes cannot be consumed from an old producer proof'
    Copy-Item (Join-Path $layoutRoot 'artifacts/build/Debug/work/bin/JueMingR.Features/x86/Debug/net472/JueMingR.Features.dll') (Join-Path $layout 'JueMingR.Features.dll') -Force
    Remove-Item -LiteralPath (Join-Path $layout 'JueMingR.PredictionWorker.exe.config')
    Assert-Failure (-not (Test-WorkloadDependencyLayout $layoutRoot $checks)) 'Missing actual runtime configuration rejects preparation'
    $exe=Join-Path $temporary 'JueMingR.ArchitectureTests.exe'
    $source=[IO.File]::ReadAllText((Join-Path $root 'tests/Workload/BusinessAssertionProtocol.cs'))+@'
namespace JueMingR.Testing {
 public static class Child {
  public static int Main(string[] args) {
   if(args[0]=="--list-checks"){System.Console.WriteLine("A|test-host\nB|test-host\nC|test-host");return 0;}
   string name=args[1],root=args[2],mode=System.IO.File.ReadAllText(System.IO.Path.Combine(root,"scenario.txt"));
   System.IO.File.AppendAllText(System.IO.Path.Combine(root,"executions.txt"),name+"\n");
   if(name=="A") {
    if(mode=="assertion" || mode=="unsupported")return BusinessAssertionProtocol.Fail();
    if(mode=="initialization")return BusinessAssertionProtocol.Fault(new System.IO.FileNotFoundException("owned missing dependency"));
    if(mode=="unknown")return 7;
    if(mode=="protocol"){System.Console.WriteLine("JMR-ASSERTION-V1|forged");return 3;}
    if(mode=="drift")System.IO.File.AppendAllText(System.IO.Path.Combine(root,"scenario.txt"),"-changed");
   }
   if(name=="B" && mode=="fail-b")return BusinessAssertionProtocol.Fail();
   return 0;
  }
 }
}
'@
    Add-Type -TypeDefinition $source -OutputAssembly $exe -OutputType ConsoleApplication
    foreach($mode in @('assertion','unknown','protocol','drift','fail-b','initialization','unsupported')){
        $fixture=Join-Path $temporary $mode
        $scripts=Join-Path $fixture 'scripts';[IO.Directory]::CreateDirectory((Join-Path $scripts 'workload'))|Out-Null
        Copy-Item -LiteralPath (Join-Path $root 'scripts/test-workload-regressions.ps1') -Destination $scripts
        # Only repository discovery/preparation is replaced. The actual runner,
        # process protocol, checkpoint, archive, reuse and coverage run unchanged.
        $support=". '"+(Join-Path $root 'scripts/workload/Workload.Support.ps1').Replace("'","''")+"'`n"+@'
function Initialize-WorkloadWorkspace {param($Root,$Assets);return $null}
function Get-WorkloadIdentity {param($Root);return [ordered]@{commit=('a'*40);fingerprint=(Get-WorkloadFileHash (Join-Path $Root 'scenario.txt')).Hash;inputs=@('source:locked')}}
function Get-WorkloadEvidenceInput {param($Root,$Identity);$rows=@('source:locked');return [ordered]@{inputs=$rows;fingerprint=(Get-WorkloadHash $rows)}}
function Get-WorkloadChanges {param($Root,$Baseline);return [ordered]@{baseline=('b'*40);paths=@('controlled-test');reason=$null}}
function Get-WorkloadRoute {param($Paths);return [ordered]@{groups=@('test-host');unknown=@()}}
function Test-WorkloadBuildMatch {return $true}
function Get-WorkloadCheckDependencies {param($Name);if($Name -ceq 'C'){return @('A')};return @()}
function Get-WorkloadAssertionHost {param($Name);if($Name -ceq 'A' -and [IO.File]::ReadAllText((Join-Path $repositoryRoot 'scenario.txt')) -ceq 'unsupported'){return $null};if($Name -cin @('A','B','C')){return 'JueMingR.ArchitectureTests.exe'};return $null}
function Get-WorkloadPlan {param($Root,$ChecksRoot,$Architecture,$Catalog,$Groups);foreach($name in @('A','B','C')){@{name=$name;executable=$Architecture;arguments=@('--check',$name,$Root);project=''}}}
'@
        [IO.File]::WriteAllText((Join-Path $scripts 'workload/Workload.Support.ps1'),$support)
        foreach($prepare in @('prepare-terraria-references.ps1','prepare-harmony.ps1')){[IO.File]::WriteAllText((Join-Path $scripts $prepare),'exit 0')}
        [IO.File]::WriteAllText((Join-Path $fixture 'scenario.txt'),$mode)
        $relative='bin/JueMingR.ArchitectureTests/x86/Debug/net472/JueMingR.ArchitectureTests.exe'
        $target=Join-Path $fixture ('artifacts/build/Debug/work/'+$relative)
        [IO.Directory]::CreateDirectory((Split-Path -Parent $target))|Out-Null;Copy-Item $exe $target
        Write-WorkloadJson (Join-Path $fixture 'artifacts/build/Debug/build-record.json') ([ordered]@{schemaVersion=4;commit=('a'*40);outputs=@([ordered]@{path=$relative;length=(Get-Item $target).Length;sha256=(Get-FileHash $target).Hash});workload=@{status='PENDING'}})
        $ErrorActionPreference='Continue'
        & powershell.exe -NoProfile -File (Join-Path $scripts 'test-workload-regressions.ps1') -Mode Related *> (Join-Path $fixture 'first.log')
        $ErrorActionPreference='Stop'
        Assert-Failure ($LASTEXITCODE -ne 0) ('Incomplete '+$mode+' cannot exit successfully')
        $record=Read-WorkloadJson (Join-Path $fixture 'artifacts/build/Debug/build-record.json')
        $rows=@($record.workload.results);$byName=@{};foreach($row in $rows){$byName[$row.name]=$row}
        Assert-Failure ($record.workload.status -ceq 'FAILED') 'Aggregate remains FAILED'
        if($mode -ceq 'assertion'){
            Assert-Failure ($byName.A.result -ceq 'FAIL' -and $byName.B.result -ceq 'PASS' -and $byName.C.result -ceq 'BLOCKED') 'A assertion / independent B / dependent C actual schedule'
            Assert-Failure (-not (Test-WorkloadCoverage $record.workload @('A','B','C') $record.workload.inputFingerprint)) 'Coverage rejects mixed failed aggregate'
            $ErrorActionPreference='Continue'
            & powershell.exe -NoProfile -File (Join-Path $scripts 'test-workload-regressions.ps1') -Mode Related *> (Join-Path $fixture 'second.log')
            $ErrorActionPreference='Stop'
            $second=Read-WorkloadJson (Join-Path $fixture 'artifacts/build/Debug/build-record.json')
            Assert-Failure (@($second.workload.results | Where-Object {$_.name -ceq 'B' -and $_.disposition -ceq 'REUSED'}).Count -eq 1) 'Independent stable B is reused after A failure'
            Assert-Failure (@([IO.File]::ReadAllLines((Join-Path $fixture 'executions.txt')) | Where-Object {$_ -ceq 'B'}).Count -eq 1) 'Reused B was not launched a second time'
        } elseif($mode -ceq 'fail-b'){
            Assert-Failure ($byName.A.result -ceq 'PASS' -and $byName.B.result -ceq 'FAIL' -and $byName.C.result -ceq 'PASS') 'Successful A preserved and C depends only on A'
            $saved=Read-WorkloadJson (Join-Path $fixture 'artifacts/build/workload-evidence.json')
            Assert-Failure (@($saved.results | Where-Object {$_.name -ceq 'B'}).Count -eq 0) 'Failed B has no reusable success'
        } else {
            Assert-Failure ($byName.B.result -ceq 'NOT_RUN' -and $byName.C.result -ceq 'NOT_RUN') ('Fatal '+$mode+' stops independent execution')
            if($mode -ceq 'initialization'){
                Assert-Failure ($byName.A.failureKind -ceq 'HostException') 'Authenticated host exception stays distinct from business failure'
                $receipt=Read-WorkloadJson $byName.A.checkpointPath
                Assert-Failure ($receipt.hostExceptionType -ceq 'System.IO.FileNotFoundException') 'Missing dependency type is bound to the actual failed invocation'
            }
            Assert-Failure (-not [IO.File]::ReadAllText((Join-Path $fixture 'executions.txt')).Contains("B`n")) 'No later child after fatal condition'
        }
    }
    Write-Output 'PASS: actual runner assertion aggregation, dependencies, stable sibling reuse, fatal exit/protocol/drift and failed proof retirement.'
} finally {
    # This unique temporary root is owned exclusively by this test.
    if([IO.Path]::GetFullPath($temporary).StartsWith([IO.Path]::GetTempPath(),[StringComparison]::OrdinalIgnoreCase)){Remove-Item -LiteralPath $temporary -Recurse -Force}
}
