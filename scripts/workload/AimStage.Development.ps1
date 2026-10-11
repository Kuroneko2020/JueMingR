# One explicitly authorized #114 stage candidate. This does not write the
# formal workload cache, grant release qualification, or authorize installation.
function Read-AimStageArtifact {
    param([string] $Root,$Entry)
    if ($null -eq $Entry -or [string]$Entry.path -notmatch '^\.local/' -or
        [string]$Entry.path -match '(^|/)\.\.(/|$)|[\\:]' -or
        [string]$Entry.sha256 -notmatch '^[A-F0-9]{64}$') {throw 'Invalid stage evidence path/digest.'}
    $base=[IO.Path]::GetFullPath($Root).TrimEnd('\')+'\'
    $path=[IO.Path]::GetFullPath((Join-Path $Root $Entry.path))
    if (-not $path.StartsWith($base,[StringComparison]::OrdinalIgnoreCase)) {throw 'Stage evidence escaped repository.'}
    $item=Get-Item -LiteralPath $path -Force -ErrorAction Stop
    for($ancestor=$item;$null -ne $ancestor -and $ancestor.FullName.Length -ge $base.Length-1;$ancestor=$(if($ancestor -is [IO.FileInfo]){$ancestor.Directory}else{$ancestor.Parent})) {
        if($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Reparse stage evidence.'}
    }
    if($item.PSIsContainer -or $item.Length -ne $Entry.length -or (Get-WorkloadFileHash -LiteralPath $path).Hash -cne $Entry.sha256) {throw ('Stage evidence changed/missing: '+$Entry.path)}
    return $path
}
function Read-AimStageDevelopment {
    param([string] $Root,[string] $Path,$Identity,$InputIdentity)
    $repository=[IO.Path]::GetFullPath($Root).TrimEnd('\')+'\'
    $full=[IO.Path]::GetFullPath($Path)
    if(-not $full.StartsWith((Join-Path $repository '.local\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Stage record must be repository-local retained evidence.'}
    $relative=$full.Substring($repository.Length).Replace('\','/')
    $null=Read-AimStageArtifact $Root ([pscustomobject]@{path=$relative;length=(Get-Item -LiteralPath $full).Length;sha256=(Get-WorkloadFileHash -LiteralPath $full).Hash})
    $record=Read-WorkloadJson $Path
    $base='472dea0955b6c9274d8c55fc48dbc7fc44ba108e'
    if($null -eq $record -or $record.schema -cne 'aim-stage-development-20261011-1' -or
        $record.authorization -cne 'owner-issue114-stage-package-20261011' -or $record.productBase -cne $base -or
        $record.candidateCommit -cne $Identity.commit -or $record.sourceFingerprint -cne $Identity.fingerprint -or
        $record.inputFingerprint -cne $InputIdentity.fingerprint -or $record.fullQualification -cne 'INCOMPLETE' -or
        $record.releaseEligible -ne $false -or $null -eq $record.PSObject.Properties['openFailures'] -or @($record.openFailures).Count -ne 0) {throw 'Stage candidate/evidence lock mismatch or unresolved failure.'}
    $retainedPaths=if($null -ne $script:WorkloadRetainedAssets){@($script:WorkloadRetainedAssets.files.path)}else{@()}
    if(@((Get-WorkloadChanges $Root 'HEAD').paths | Where-Object {$_ -cnotin $retainedPaths}).Count){throw 'Stage candidate has uncommitted or unknown changes.'}
    & git -C $Root merge-base --is-ancestor $base $Identity.commit
    if($LASTEXITCODE -ne 0){throw 'Stage candidate must retain the fixed 472dea0 product.'}
    # Exact authorized closing delta, rather than a new general skip/profile.
    $allowed=@(
        'AGENTS.md','docs/规范/工程治理规则.md','docs/决策/ADR-0019-受控并行协作与模型分工.md',
        'docs/计划/辅助瞄准阶段文档.md','docs/设计/可重复构建与项目骨架.md',
        'src/JueMingR.TerrariaHost/Combat/HostWhipAttack.cs',
        'src/JueMingR.TerrariaHost/Combat/HostAttackAim.Preview.cs',
        'src/JueMingR.TerrariaHost/Combat/HostHarpyAttack.cs',
        'src/JueMingR.TerrariaHost/Combat/HostAttackControl.cs',
        'tests/NativeWorldTextProbe/NativeCombatAttackCostChecks.cs',
        'tests/NativeWorldTextProbe/NativeCombatAttackCostChecks.Natural.cs',
        'tests/NativeWorldTextProbe/NativeCombatWhipTerrainChecks.cs',
        'tests/NativeWorldTextProbe/NativeCombatControlChecks.cs','tests/NativeWorldTextProbe/NativeCombatWhipChecks.cs',
        'scripts/build.ps1','scripts/build-phase0s-validation-package.ps1','scripts/verify-existing-package.ps1',
        'scripts/workload/Workload.Support.ps1',
        'scripts/workload/AimStage.Development.ps1')
    $changed=@(Invoke-WorkloadGit $Root @('diff','--name-only','--no-renames',$base,$Identity.commit,'--') | Sort-Object)
    if(@($changed | Where-Object {$allowed -cnotcontains $_}).Count -ne 0 -or
        (@($record.revisions | ForEach-Object {$_.path} | Sort-Object) -join '|') -cne ($changed -join '|')) {throw 'Stage revision scope differs from the approved closing delta.'}
    foreach($revision in $record.revisions){
        if((Get-WorkloadFileHash -LiteralPath (Join-Path $Root $revision.path)).Hash -cne $revision.sha256){throw ('Stage revision changed: '+$revision.path)}
    }
    # These are scoped results and reviewed applicability, never synthetic
    # formal schema-2 PASS/QUALIFIED rows. The independent review binds all
    # of them plus the real failure dispositions to this precise source tree.
    $required=@('integration','attack-costs','navigation','impact-draw','whip-terrain')
    if((@($record.checks | ForEach-Object {$_.name} | Sort-Object) -join '|') -cne (@($required | Sort-Object) -join '|')) {throw 'Stage necessary checks are incomplete/duplicated.'}
    foreach($check in $record.checks){
        if($check.exit -ne 0 -or $check.disposition -cnotin @('CURRENT','REVIEWED_REUSE') -or
            -not $check.scope -or -not $check.applicability -or @($check.artifacts).Count -eq 0) {throw ('Invalid stage scope result: '+$check.name)}
        foreach($artifact in $check.artifacts){$null=Read-AimStageArtifact $Root $artifact}
    }
    if(@($record.componentEvidence).Count -eq 0 -or @($record.failureDispositions).Count -eq 0 -or @($record.reviews).Count -eq 0) {throw 'Stage component applicability, failure dispositions or independent review missing.'}
    foreach($component in $record.componentEvidence){
        if(-not $component.scope -or -not $component.applicability -or @($component.artifacts).Count -eq 0){throw 'Incomplete component evidence applicability.'}
        foreach($artifact in $component.artifacts){$null=Read-AimStageArtifact $Root $artifact}
    }
    foreach($failure in $record.failureDispositions){
        if($failure.disposition -cnotin @('TEST_CORRECTED','PRODUCT_FIXED','ENVIRONMENT_LIMITATION') -or
            -not $failure.cause -or -not $failure.resolution -or @($failure.artifacts).Count -eq 0){throw 'Unresolved stage failure.'}
        foreach($artifact in $failure.artifacts){$null=Read-AimStageArtifact $Root $artifact}
    }
    foreach($review in $record.reviews){
        if($review.result -cne 'APPROVED_FOR_STAGE' -or $review.sourceFingerprint -cne $Identity.fingerprint -or -not $review.coverage){throw 'Independent stage review does not cover this candidate.'}
        $null=Read-AimStageArtifact $Root $review.artifact
    }
    if(@($record.limits).Count -eq 0){throw 'Stage limitations must be recorded.'}
    $debug=Read-WorkloadJson (Join-Path $Root 'artifacts/build/Debug/build-record.json')
    if(-not (Test-WorkloadBuildMatch $Root $debug $Identity $InputIdentity)){throw 'Stage Debug detection build does not match candidate inputs.'}
    # The same-source Debug assertions are allowed to have a different build
    # timestamp/commit metadata only when their actual compile inputs match.
    # Actual loaded product bytes and probe used by the scoped runs are fixed.
    $consumerPaths=@('artifacts/build/Debug/checks/bin/NativeWorldTextProbe/x86/Debug/net472/NativeWorldTextProbe.exe')
    foreach($project in @('Platform','Features','Infrastructure','TerrariaHost')){
        $consumerPaths+=('artifacts/build/Debug/work/bin/JueMingR.'+$project+'/x86/Debug/net472/JueMingR.'+$project+'.dll')
    }
    if((@($record.detectionFiles | ForEach-Object {$_.path} | Sort-Object) -join '|') -cne
        (@($consumerPaths | Sort-Object) -join '|')) {throw 'Stage detection consumer set/path is incomplete or differs from actual scope loaders.'}
    foreach($assembly in $record.detectionFiles){
        if([string]$assembly.path -notmatch '^artifacts/build/Debug/(work/bin/|checks/bin/)' -or
            [string]$assembly.path -match '(^|/)\.\.(/|$)|[\\:]' -or
            (Get-WorkloadFileHash -LiteralPath (Join-Path $Root $assembly.path)).Hash -cne $assembly.sha256){throw 'Stage detection consumer changed.'}
    }
    return [ordered]@{status='STAGE_DEVELOPMENT';mode='ExplicitAimStage20261011';fullQualification='INCOMPLETE';releaseEligible=$false;
        productBase=$base;recordPath=$relative;recordSha256=(Get-WorkloadFileHash -LiteralPath $Path).Hash;
        necessaryScopes=$required;limits=@($record.limits);checkCount=$required.Count}
}
function Test-AimStageDevelopmentBuild {
    param([string] $Root,$Record,[string] $Path,$RetainedAssets=$null)
    try {
        if($null -eq $Record -or $Record.schemaVersion -ne 4 -or $Record.configuration -cne 'Release' -or $Record.sdk -cne '10.0.203' -or
            (-not $Record.clean -and -not (Test-WorkloadRestrictedRecord $Record $RetainedAssets))){return $false}
        $identity=Get-WorkloadIdentity $Root;$inputs=Get-WorkloadEvidenceInput $Root $identity
        if($Record.commit -cne $identity.commit -or $Record.sourceFingerprint -cne $identity.fingerprint -or $Record.inputFingerprint -cne $inputs.fingerprint -or
            -not (Test-WorkloadOutputs (Join-Path $Root 'artifacts/build/Release/work') $Record.outputs)){return $false}
        $stage=Read-AimStageDevelopment $Root $Path $identity $inputs
        return ($Record.workload | ConvertTo-Json -Depth 6 -Compress) -ceq ($stage | ConvertTo-Json -Depth 6 -Compress)
    } catch {return $false}
}
