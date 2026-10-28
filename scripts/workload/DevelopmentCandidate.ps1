# Stable owner-test records reuse the formal route, plan and evidence readers.
# This reader records incomplete qualification; it never grants installation or
# converts a caller's list/authorization text into product or release approval.
function Get-DevelopmentCandidateCatalogue {
    param([string] $Architecture)
    $catalog=@(& $Architecture --list-checks)
    if($LASTEXITCODE -ne 0 -or $catalog.Count -eq 0){throw 'Development candidate catalogue unavailable.'}
    return $catalog
}
function Get-DevelopmentCandidatePlan {
    param([string] $Root,[string] $ProductBase,$Identity,$InputIdentity)
    if($ProductBase -cnotmatch '^[0-9a-f]{40}$'){throw 'Development candidate requires its full task base.'}
    $ancestor=[string](Invoke-WorkloadGit $Root @('merge-base',$ProductBase,$Identity.commit))
    if($ancestor -cne $ProductBase){throw 'Development candidate does not retain its task base.'}
    $changes=Get-WorkloadChanges $Root ''
    $extra=Get-WorkloadChanges $Root $ProductBase
    if($changes.reason -or $extra.reason){throw 'Development candidate comparison baseline unavailable.'}
    $paths=@($changes.paths+$extra.paths | Sort-Object -Unique)
    $retained=if($null -ne $script:WorkloadRetainedAssets){@($script:WorkloadRetainedAssets.files.path)}else{@()}
    $paths=@($paths | Where-Object {$_ -cnotin $retained})
    $route=Get-WorkloadRoute $paths
    if(@($route.unknown).Count){throw ('Unclassified development candidate inputs: '+($route.unknown -join ', '))}
    $debug=Read-WorkloadJson (Join-Path $Root 'artifacts/build/Debug/build-record.json')
    if(-not (Test-WorkloadBuildMatch $Root $debug $Identity $InputIdentity)){throw 'Development candidate Debug catalogue/outputs do not match current inputs.'}
    $architecture=Join-Path $Root 'artifacts/build/Debug/work/bin/JueMingR.ArchitectureTests/x86/Debug/net472/JueMingR.ArchitectureTests.exe'
    $catalog=@(Get-DevelopmentCandidateCatalogue $architecture)
    $plan=@(Get-WorkloadPlan $Root (Join-Path $Root 'artifacts/build/Debug/checks') $architecture $catalog $route.groups)
    if(@($plan.name | Sort-Object -Unique).Count -ne $plan.Count){throw 'Duplicate development candidate plan identity.'}
    $rows=@('baseline:'+$changes.baseline)+@('taskBase:'+$ProductBase)+@($paths|ForEach-Object {'path:'+$_})+@($route.groups|Sort-Object|ForEach-Object {'group:'+$_})
    foreach($check in $plan){$rows+=($check.name+':'+(Get-WorkloadHash (@($check.executable)+@($check.arguments))))}
    return [ordered]@{baseline=$changes.baseline;paths=$paths;groups=@($route.groups);checks=$plan;fingerprint=Get-WorkloadHash $rows}
}

function Read-DevelopmentCandidate {
    param([string] $Root,[string] $Path,$Identity,$InputIdentity,$Record,[string] $Relative)
    if($Record.schema -cne 'development-candidate-1' -or $Record.purpose -cne 'OWNER_TEST' -or
        $Record.taskReference -cnotmatch '^https://github\.com/Kuroneko2020/JueMingR/issues/[1-9][0-9]*$' -or
        [string]::IsNullOrWhiteSpace($Record.testPurpose) -or
        $Record.candidateCommit -cne $Identity.commit -or $Record.sourceFingerprint -cne $Identity.fingerprint -or
        $Record.inputFingerprint -cne $InputIdentity.fingerprint -or $Record.fullQualification -cne 'INCOMPLETE' -or
        $Record.releaseEligible -isnot [bool] -or $Record.releaseEligible -ne $false -or
        $null -eq $Record.PSObject.Properties['openFailures']){throw 'Development candidate identity, purpose or qualification mismatch.'}
    if($null -ne $Record.PSObject.Properties['installationAuthorized'] -and
        ($Record.installationAuthorized -isnot [bool] -or $Record.installationAuthorized -ne $false)){throw 'Development candidate cannot grant installation authority.'}
    # A retained owner instruction is provenance, not a machine signature of the
    # human. The coordinator still verifies scope and actual task permissions.
    if($null -eq $Record.authorization -or $Record.authorization.reference -cne $Record.taskReference){throw 'Development candidate lacks its existing task authorization reference.'}
    $null=Read-AimStageArtifact $Root $Record.authorization.artifact
    if($Record.authorization.artifact.length -le 0){throw 'Empty development candidate authorization source.'}
    $retained=if($null -ne $script:WorkloadRetainedAssets){@($script:WorkloadRetainedAssets.files.path)}else{@()}
    if(@((Get-WorkloadChanges $Root 'HEAD').paths | Where-Object {$_ -cnotin $retained}).Count){throw 'Development candidate has uncommitted or unknown inputs.'}
    if($Record.package.profile -cne 'ContinuousProcessing' -or $Record.package.content -cne 'ORDINARY_COMPLETE' -or
        $Record.package.packageId -cne ('continuous-processing-'+$Identity.commit)){throw 'Development candidate must use the ordinary complete package and current product identity.'}
    $formal=Get-DevelopmentCandidatePlan $Root $Record.productBase $Identity $InputIdentity
    $required=@($formal.checks.name)
    if($Record.planFingerprint -cne $formal.fingerprint -or
        (@($Record.checks.name|Sort-Object)-join '|') -cne (@($required|Sort-Object)-join '|')){throw 'Development candidate formal plan/check responsibility mismatch.'}
    if($null -ne $Record.PSObject.Properties['requiredChecks'] -and
        (@($Record.requiredChecks|Sort-Object)-join '|') -cne (@($required|Sort-Object)-join '|')){throw 'Caller-supplied checks cannot narrow formal development candidate responsibility.'}
    # The full catalogue remains an observation ledger. An explicitly reviewed
    # task subset selects acceptance, not a second full/release qualification.
    # Legacy records without this field retain their original strict semantics.
    $scoped=$null -ne $Record.PSObject.Properties['taskAcceptance']
    $accepted=$required
    if($scoped){
        $task=$Record.taskAcceptance
        if($null -eq $task -or $task.status -cne 'PASS' -or
            $null -eq $task.PSObject.Properties['deliveryFailures'] -or @($task.deliveryFailures).Count){throw 'Task acceptance has an unresolved target or delivery failure.'}
        $responsibilities=@($task.responsibilities)
        $accepted=@($responsibilities|ForEach-Object {$_.name})
        if(-not $accepted.Count -or @($accepted|Sort-Object -Unique).Count -ne $accepted.Count -or
            @($accepted|Where-Object {$_ -cnotin $required}).Count){throw 'Task acceptance must reference a nonempty unique subset of the actual catalogue.'}
        foreach($responsibility in $responsibilities){if([string]::IsNullOrWhiteSpace($responsibility.responsibility)){throw 'Task acceptance needs each concrete behavior responsibility.'}}
    } elseif(@($Record.openFailures).Count){throw 'Legacy development candidate has an open failure.'}
    $pending=New-Object 'System.Collections.Generic.List[string]'
    $failures=New-Object 'System.Collections.Generic.List[string]'
    $observations=New-Object 'System.Collections.Generic.List[object]'
    $proved=0
    $acceptedProved=0
    foreach($check in $formal.checks){
        $item=@($Record.checks | Where-Object {$_.name -ceq $check.name})[0]
        $necessary=$check.name -cin $accepted
        if($item.status -ceq 'NOT_EXECUTED' -or ($scoped -and $item.status -ceq 'NOT_RUN')){
            if([string]::IsNullOrWhiteSpace($item.reason)){throw 'Unfinished development candidate responsibility needs a reason.'}
            if($scoped -and $necessary){throw ('Task acceptance responsibility was not executed: '+$check.name)}
            $pending.Add($check.name)
            $observations.Add([ordered]@{name=$check.name;status=$item.status;reason=$item.reason})
            continue
        }
        if($scoped -and $item.status -ceq 'FAIL'){
            if($necessary -or [string]::IsNullOrWhiteSpace($item.reason)){throw ('Failed task acceptance responsibility: '+$check.name)}
            $failedPath=Read-AimStageArtifact $Root $item.evidence
            $failed=Read-WorkloadJson $failedPath
            if($null -eq $failed -or $failed.name -cne $check.name -or $failed.status -cnotin @('FAIL','FAILED')){throw 'Historical failure must retain its original named failure artifact.'}
            $failures.Add($check.name)
            # Selection does not prove task irrelevance. A related aggregate
            # may retain its original FAIL while finite repairs are reviewed
            # separately; all existing admission and evidence gates stay above.
            $observations.Add([ordered]@{name=$check.name;status='FAIL';reason=$item.reason;evidence=$item.evidence;acceptance='NOT_SELECTED_FOR_AGGREGATE_ACCEPTANCE'})
            continue
        }
        if($item.status -cnotin @('PASS','REUSED')){throw ('Failed/unknown development candidate responsibility: '+$check.name)}
        $evidencePath=Read-AimStageArtifact $Root $item.evidence
        $original=Read-WorkloadJson $evidencePath
        if($null -eq $original -or $original.name -cne $check.name -or $original.status -cne 'PASS'){throw 'Development candidate pass must retain its named original evidence.'}
        $signature=Get-WorkloadHash (@($check.executable)+@($check.arguments))
        $applicable=$null
        if(-not $scoped -or $necessary){
            $applicable=Test-WorkloadReusable $Root $original $InputIdentity $check.name $signature
            if(-not $applicable){throw ('Development candidate evidence missing, stale or not qualified: '+$check.name)}
            $proved++;if($necessary){$acceptedProved++}
        }
        $observations.Add([ordered]@{name=$check.name;status=$item.status;evidence=$item.evidence;applicable=$applicable;
            applicability=$(if($null -eq $applicable){'HISTORICAL_ONLY_NOT_REASSESSED'}else{'CURRENT_PROOF'})})
    }
    if($scoped -and ((@($Record.openFailures|Sort-Object)-join '|') -cne (@($failures|Sort-Object)-join '|') -or $acceptedProved -ne $accepted.Count)){throw 'Task acceptance evidence or retained open failure list differs from the actual catalogue.'}
    if($proved -eq 0){throw 'Development candidate has no applicable executed evidence.'}
    if($null -eq $Record.PSObject.Properties['unfinishedResponsibilities'] -or
        (@($Record.unfinishedResponsibilities|Sort-Object)-join '|') -cne (@($pending|Sort-Object)-join '|')){throw 'Development candidate unfinished responsibility list differs from its formal plan.'}
    if(@($Record.reviews).Count -eq 0 -or @($Record.limits).Count -eq 0){throw 'Development candidate needs independent review and explicit remaining limits.'}
    foreach($review in $Record.reviews){
        if($review.result -cne 'APPROVED_FOR_STAGE' -or $review.sourceFingerprint -cne $Identity.fingerprint -or
            [string]::IsNullOrWhiteSpace($review.coverage)){throw 'Development candidate independent review does not cover current inputs.'}
        $null=Read-AimStageArtifact $Root $review.artifact
    }
    # Even with every check present this remains incomplete: Related/Full owns
    # formal completion, and installation is independently authorized outside.
    return [ordered]@{status='STAGE_DEVELOPMENT';mode='DevelopmentCandidate';fullQualification='INCOMPLETE';releaseEligible=$false;
        installationAuthorized=$false;taskReference=$Record.taskReference;purpose=$Record.testPurpose;productBase=$Record.productBase;
        recordPath=$Relative;recordSha256=(Get-WorkloadFileHash -LiteralPath $Path).Hash;planFingerprint=$formal.fingerprint;
        necessaryScopes=$accepted;catalogueScopes=$required;limits=@($Record.limits);checkCount=$required.Count;provedCheckCount=$proved;
        unfinishedResponsibilities=@($pending);packageId=$Record.package.packageId;
        taskAcceptance=$(if($scoped){[ordered]@{status='PASS';checkCount=$accepted.Count;responsibilities=@($Record.taskAcceptance.responsibilities)}}else{$null});
        regressionObservations=@($observations.ToArray());openFailures=@($failures.ToArray())}
}
