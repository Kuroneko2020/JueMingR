# Explicit owner-authorized finite #114 / #118 / #120 stage candidates. This does not write the
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
    if($null -ne $record -and $record.schema -ceq 'view-unlock-development-20261018-1'){return Read-ViewUnlockDevelopment $Root $Path $Identity $InputIdentity $record $relative}
    if($null -ne $record -and $record.schema -ceq 'journey-menu-development-20261018-1'){return Read-JourneyMenuDevelopment $Root $Path $Identity $InputIdentity $record $relative}
    if($null -ne $record -and $record.schema -ceq 'hidden-entry-development-20261018-1'){return Read-HiddenEntryDevelopment $Root $Path $Identity $InputIdentity $record $relative}
    if($null -ne $record -and $record.schema -ceq 'footprints-200h-development-20261017-1'){return Read-Footprints200HourDevelopment $Root $Path $Identity $InputIdentity $record $relative}
    # Each owner-authorized successor has its own base and evidence contract;
    # the old qualification cannot be relabelled as a new candidate record.
    $successor=$null -ne $record -and $record.schema -ceq 'aim-root-fix-development-20261012-1'
    $priority=$null -ne $record -and $record.schema -ceq 'aim-priority-recovery-development-20261013-1'
    $continuation=$null -ne $record -and $record.schema -ceq 'aim-continuation-development-20261013-1'
    $dedup=$null -ne $record -and $record.schema -ceq 'aim-dedup-repair-development-20261014-1'
    $costNav=$null -ne $record -and $record.schema -ceq 'aim-cost-nav-development-20261015-1'
    $weaponCompat=$null -ne $record -and $record.schema -ceq 'aim-weapon-compat-development-20261015-1'
    $targetPerf=$null -ne $record -and $record.schema -ceq 'aim-target-perf-development-20261014-1'
    $algorithm=$null -ne $record -and $record.schema -ceq 'aim-algorithm-rework-development-20261015-1'
    $balanced=$null -ne $record -and $record.schema -ceq 'aim-balanced-development-20261017-1'
    $pathDisplay=$null -ne $record -and $record.schema -ceq 'path-display-development-20261017-1'
    $base=if($algorithm){'97ceecf7c8706779bc2cf9acfff795830eb449da'}elseif($costNav){'69ffc7bfd5223069081c848f1646b49e958b2eea'}elseif($weaponCompat){'0f93019526aa334877b4cfb4b5d38c3d1c0c55c4'}elseif($targetPerf){'72970569c11dd09e3e210894c25cc06e4cfc1a7f'}elseif($dedup){'c38b1f941d64b73971a3380aff5d7c75fff24cb7'}elseif($continuation){'569668aa9f4eb75d525709b20652eefd1d8e0e23'}elseif($priority){'01b03386cc288fe8f8a773cecd3a0de564182472'}elseif($successor){'3504c963949d87d6a88ada5a3f5cd9c189f1f0c2'}else{'472dea0955b6c9274d8c55fc48dbc7fc44ba108e'}
    $schema=if($algorithm){'aim-algorithm-rework-development-20261015-1'}elseif($costNav){'aim-cost-nav-development-20261015-1'}elseif($weaponCompat){'aim-weapon-compat-development-20261015-1'}elseif($targetPerf){'aim-target-perf-development-20261014-1'}elseif($dedup){'aim-dedup-repair-development-20261014-1'}elseif($continuation){'aim-continuation-development-20261013-1'}elseif($priority){'aim-priority-recovery-development-20261013-1'}elseif($successor){'aim-root-fix-development-20261012-1'}else{'aim-stage-development-20261011-1'}
    $authorization=if($algorithm){'owner-issue114-algorithm-rework-20261015'}elseif($costNav){'owner-issue114-cost-nav-20261015'}elseif($weaponCompat){'owner-issue114-weapon-compat-20261015'}elseif($targetPerf){'owner-issue114-target-perf-20261014'}elseif($dedup){'owner-issue114-dedup-repair-20261014'}elseif($continuation){'owner-issue114-continuation-20261013'}elseif($priority){'owner-issue114-priority-recovery-20261013'}elseif($successor){'owner-issue114-root-fix-20261012'}else{'owner-issue114-stage-package-20261011'}
    if($balanced){$base='112e9d629e3caf75c5d2ed9d021ac9c255b68e34';$schema='aim-balanced-development-20261017-1';$authorization='owner-issue114-balanced-20261017'}
    if($pathDisplay){$base='1014c7d58f83460bb5a422bf5fc0a80e4f4bd0f8';$schema='path-display-development-20261017-1';$authorization='owner-issue114-path-display-20261017'}
    if($null -eq $record -or $record.schema -cne $schema -or
        $record.authorization -cne $authorization -or $record.productBase -cne $base -or
        $record.candidateCommit -cne $Identity.commit -or $record.sourceFingerprint -cne $Identity.fingerprint -or
        $record.inputFingerprint -cne $InputIdentity.fingerprint -or $record.fullQualification -cne 'INCOMPLETE' -or
        $record.releaseEligible -ne $false -or $null -eq $record.PSObject.Properties['openFailures'] -or @($record.openFailures).Count -ne 0) {throw 'Stage candidate/evidence lock mismatch or unresolved failure.'}
    $retainedPaths=if($null -ne $script:WorkloadRetainedAssets){@($script:WorkloadRetainedAssets.files.path)}else{@()}
    if(@((Get-WorkloadChanges $Root 'HEAD').paths | Where-Object {$_ -cnotin $retainedPaths}).Count){throw 'Stage candidate has uncommitted or unknown changes.'}
    & git -C $Root merge-base --is-ancestor $base $Identity.commit
    if($LASTEXITCODE -ne 0){throw 'Stage candidate must retain its authorized fixed product base.'}
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
    if($successor){
        $allowed=@('docs/计划/辅助瞄准阶段文档.md','docs/设计/可重复构建与项目骨架.md',
            'src/JueMingR.TerrariaHost/Combat/HostAttackWindow.cs',
            'src/JueMingR.TerrariaHost/Combat/HostMeleeAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackAim.FlightPreview.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackAim.Preview.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackAim.cs',
            'src/JueMingR.TerrariaHost/Combat/AttackAmmoSnapshot.cs',
            'tests/NativeWorldTextProbe/NativeCombatUsageChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatOriginalFeedbackChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatOriginalRangedChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatImpactVisualChecks.cs',
            'scripts/workload/Workload.Definitions.ps1',
            'scripts/workload/AimStage.Development.ps1')
    }
    if($priority){
        # The 2026-10-13 owner contract permits this exact incident recovery,
        # not a reusable exemption for arbitrary combat or evidence changes.
        $allowed=@('docs/功能介绍/战斗辅助行为合同.md','docs/计划/辅助瞄准阶段文档.md',
            'docs/设计/战斗辅助实现设计.md','docs/设计/可重复构建与项目骨架.md',
            'src/JueMingR.Features/Combat/CombatPredictionWork.cs',
            'src/JueMingR.Features/Combat/RollingNpcPrediction.cs',
            'src/JueMingR.Features/Combat/RollingNpcPrediction.Progress.cs',
            'src/JueMingR.Platform/Combat/NpcTrajectory.cs',
            'src/JueMingR.TerrariaHost/Combat/CombatImpactMarker.cs',
            'src/JueMingR.TerrariaHost/Combat/CombatObservationWorldLayer.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackAim.FlightPreview.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackAim.Preview.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackAim.Selection.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackAim.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackControl.cs',
            'src/JueMingR.TerrariaHost/Combat/HostCombatObservation.cs',
            'src/JueMingR.TerrariaHost/Combat/NpcPredictionSource.Cultist.cs',
            'src/JueMingR.TerrariaHost/Combat/NpcPredictionSource.Ice.cs',
            'src/JueMingR.TerrariaHost/Combat/NpcPredictionSource.MoonLord.cs',
            'src/JueMingR.TerrariaHost/Combat/NpcPredictionSource.cs',
            'tests/JueMingR.ArchitectureTests/Combat/BasicNpcMotionChecks.cs',
            'tests/JueMingR.ArchitectureTests/Combat/RollingNpcPredictionChecks.cs',
            'tests/JueMingR.ArchitectureTests/Phase0TArchitectureChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatAimChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatAttackCostChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatAttackCostChecks.Incident.cs',
            'tests/NativeWorldTextProbe/NativeCombatImpactVisualChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatYoyoNavigationChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatBasicAimChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatCandidateDemandChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatNaturalImpactChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatOriginalFeedbackChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatUsageChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatOriginalRangedChecks.cs',
            'scripts/workload/AimStage.Development.ps1',
            'review-materials/aim-priority-recovery-20261013/evidence.md',
            'review-materials/aim-priority-recovery-20261013/failure-dispositions.json',
            'review-materials/aim-priority-recovery-20261013/validation-map.json',
            'review-materials/aim-priority-recovery-20261013/natural-cpu-summary.csv',
            'review-materials/aim-priority-recovery-20261013/draw-cpu-summary.csv')
    }
    if($continuation){
        # This owner's continuation replaces only the selected A2 task
        # boundaries. Every actual affected scope and failure still needs
        # hashed execution or reviewed applicability, including full Main.
        $allowed=@('docs/功能介绍/战斗辅助行为合同.md','docs/设计/战斗辅助实现设计.md','docs/计划/辅助瞄准阶段文档.md',
            'scripts/workload/Workload.Definitions.ps1','scripts/workload/AimStage.Development.ps1','scripts/build-phase0s-validation-package.ps1',
            'scripts/phase0s/Continuous-Processing-Owner-Test-Card.zh-CN.md',
            'src/JueMingR.Features/Combat/AttackIntercept.cs','src/JueMingR.Features/Combat/CombatPredictionWork.cs',
            'src/JueMingR.TerrariaHost/Combat/CombatImpactMarker.cs','src/JueMingR.TerrariaHost/Combat/CombatObservationWorldLayer.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackAim.cs','src/JueMingR.TerrariaHost/Combat/HostAttackAim.Preview.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackAim.Selection.cs','src/JueMingR.TerrariaHost/Combat/HostCombatObservation.cs',
            'src/JueMingR.TerrariaHost/Combat/NpcPredictionSource.cs',
            'src/JueMingR.TerrariaHost/Combat/HostProjectileEnvironment.cs',
            'src/JueMingR.TerrariaHost/F5/CombatObservationControls.cs','tests/NativeWorldTextProbe/NativeCombatImpactRetirementChecks.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackControl.cs','tests/JueMingR.ArchitectureTests/Phase0TArchitectureChecks.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackControl.Preview.cs','src/JueMingR.TerrariaHost/Combat/HostYoyoNavigation.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackAim.FlightPreview.cs','src/JueMingR.TerrariaHost/Combat/HostGemStaffAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttachedShotAttack.cs','tests/NativeWorldTextProbe/NativeCombatPreviewLeaseChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatAimChecks.cs','tests/NativeWorldTextProbe/NativeCombatAttackCostChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatBasicAimChecks.cs','tests/NativeWorldTextProbe/NativeCombatAmmoChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatAttackCostChecks.Incident.cs',
            'tests/NativeWorldTextProbe/NativeCombatReceiveChecks.cs','tests/NativeWorldTextProbe/NativeCombatObservationChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatAttackCostChecks.Optional.cs','tests/NativeWorldTextProbe/NativeCombatCandidateDemandChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatCostChecks.cs','tests/NativeWorldTextProbe/NativeCombatFlameChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatGemStaffChecks.cs','tests/NativeWorldTextProbe/NativeCombatSwingChecks.cs','tests/NativeWorldTextProbe/NativeCombatImpactVisualChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatImpactVisualChecks.Geometry.cs','tests/NativeWorldTextProbe/NativeCombatLocalFailureChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatMarkerCapacityChecks.cs','tests/NativeWorldTextProbe/NativeCombatMarkerDemandChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatNaturalImpactChecks.cs','tests/NativeWorldTextProbe/NativeCombatSelectionPreviewChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatTargetMarkerChecks.cs','tests/NativeWorldTextProbe/NativeCombatUsageChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatYoyoChecks.cs','tests/NativeWorldTextProbe/NativeCombatBubbleChecks.cs',
            'review-materials/aim-priority-recovery-20261013/continuation-evidence.md',
            'review-materials/aim-priority-recovery-20261013/continuation-validation-map.json',
            'review-materials/aim-priority-recovery-20261013/continuation-failure-dispositions.json',
            'review-materials/aim-priority-recovery-20261013/continuation-main-summary.csv')
    }
    if($dedup){
        # The owner's 2026-10-14 repair package retains the complete product
        # profile. Only this fixed task delta and its reviewed evidence qualify;
        # it is not an extension of the old continuation's successful record.
        $allowed=@('AGENTS.md','docs/规范/工程治理规则.md','docs/网页Chat协作约定.md',
            'docs/决策/ADR-0019-受控并行协作与模型分工.md','docs/功能介绍/战斗辅助行为合同.md',
            'docs/计划/辅助瞄准阶段文档.md','docs/设计/战斗辅助实现设计.md','docs/设计/可重复构建与项目骨架.md',
            'scripts/workload/AimStage.Development.ps1','scripts/workload/Workload.Definitions.ps1','scripts/workload/Workload.Support.ps1',
            'src/JueMingR.TerrariaHost/Combat/CombatSelection.cs','src/JueMingR.TerrariaHost/Combat/HostAttackAim.HandReuse.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackAim.Preview.cs','src/JueMingR.TerrariaHost/Combat/HostAttackAim.Selection.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackAim.cs','src/JueMingR.TerrariaHost/Combat/HostAttackPlayerPrefix.cs',
            'src/JueMingR.TerrariaHost/Combat/HostMeleeAttack.cs','src/JueMingR.TerrariaHost/Combat/NpcPredictionSource.cs',
            'tests/NativeWorldTextProbe/NativeCombatAttackCostChecks.cs','tests/NativeWorldTextProbe/NativeCombatAttackCostChecks.Incident.cs',
            'tests/NativeWorldTextProbe/NativeCombatAttackCostChecks.Optional.cs','tests/NativeWorldTextProbe/NativeCombatSelectionPreviewChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatDedupChecks.cs','tests/NativeWorldTextProbe/NativeCombatDedupModelChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatDedupPrefixChecks.cs','tests/NativeWorldTextProbe/NativeCombatDedupPremiseChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatDedupReasons.cs','tests/NativeWorldTextProbe/NativeCombatDedupSourceChecks.cs',
            'tests/Workload/Invoke-WorkloadRoutingChecks.ps1',
            'review-materials/aim-dedup-repair-20261014/evidence.md','review-materials/aim-dedup-repair-20261014/validation-map.json',
            'review-materials/aim-dedup-repair-20261014/failure-dispositions.json',
            'review-materials/aim-dedup-repair-20261014/A-birth-directions.csv','review-materials/aim-dedup-repair-20261014/A-time-summary.csv',
            'review-materials/aim-dedup-repair-20261014/B-model-equivalence.csv','review-materials/aim-dedup-repair-20261014/B-natural-births.csv',
            'review-materials/aim-dedup-repair-20261014/B-natural-time-summary.csv','review-materials/aim-dedup-repair-20261014/B-supplement-summary.csv',
            'review-materials/aim-dedup-repair-20261014/F-natural-births.csv','review-materials/aim-dedup-repair-20261014/F-natural-consumer-summary.csv',
            'review-materials/aim-dedup-repair-20261014/F-natural-time-summary.csv')
    }
    if($targetPerf){
        # Only the owner's current-demand / clear-line / performance repair.
        # Unknown executable changes still fail the exact revision check below.
        $allowed=@('src/JueMingR.TerrariaHost/Combat/HostAttackAim.Selection.cs',
            'tests/NativeWorldTextProbe/NativeCombatCandidateDemandChecks.cs','tests/NativeWorldTextProbe/NativeCombatAttackCostChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatCurrentDemandChecks.cs','tests/NativeWorldTextProbe/NativeCombatCandidateFailureChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatSelectionPreviewChecks.cs',
            'scripts/workload/AimStage.Development.ps1','scripts/workload/Workload.Definitions.ps1',
            'docs/设计/可重复构建与项目骨架.md','docs/设计/战斗辅助实现设计.md','docs/计划/辅助瞄准阶段文档.md',
            'review-materials/aim-target-perf-repair-20261014/evidence.md',
            'review-materials/aim-target-perf-repair-20261014/validation-map.json',
            'review-materials/aim-target-perf-repair-20261014/old-B-native-events.csv',
            'review-materials/aim-target-perf-repair-20261014/A-native-uses.csv',
            'review-materials/aim-target-perf-repair-20261014/B-key-events.csv',
            'review-materials/aim-target-perf-repair-20261014/C-main-key-time.csv',
            'review-materials/aim-target-perf-repair-20261014/C-main-quality-summary.csv',
            'review-materials/aim-target-perf-repair-20261014/C-key-native-events.csv',
            'review-materials/aim-target-perf-repair-20261014/C-main-slow-events.csv',
            'review-materials/aim-target-perf-repair-20261014/C-pressure-time-summary.csv',
            'review-materials/aim-target-perf-repair-20261014/C-pressure-quality-summary.csv',
            'review-materials/aim-target-perf-repair-20261014/C-pressure-native-events.csv',
            'review-materials/aim-target-perf-repair-20261014/C-excluded-run-status.csv')
    }
    if($weaponCompat){
        # Fixed owner-authorized weapon compatibility delta; this record does
        # not grant future installs or widen the ordinary qualification route.
        $allowed=@('src/JueMingR.Features/Combat/AttackBallisticCandidates.cs',
            'src/JueMingR.TerrariaHost/Combat/HostPlacementAttack.cs','src/JueMingR.TerrariaHost/Combat/HostAttackAim.cs',
            'src/JueMingR.TerrariaHost/Combat/CombatUse.cs','src/JueMingR.TerrariaHost/Combat/HostAttackControl.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackControl.Preview.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackWindow.cs',
            'src/JueMingR.TerrariaHost/Combat/HostYoyoReleaseAttack.cs','src/JueMingR.TerrariaHost/Combat/HostBetsyFlameAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostBottleAttack.cs',
            'tests/JueMingR.ArchitectureTests/Phase0TArchitectureChecks.cs',
            'tests/NativeWorldTextProbe/NativeChecks.cs','tests/NativeWorldTextProbe/NativeCombatIntegrationChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatEffectChecks.cs','tests/NativeWorldTextProbe/NativeCombatPlacementChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatMagicStringAimChecks.cs','tests/NativeWorldTextProbe/NativeCombatBallisticCandidateChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatBasicAimChecks.cs',
            'scripts/workload/AimStage.Development.ps1','scripts/workload/Workload.Definitions.ps1',
            'docs/网页Chat协作约定.md','docs/规范/工程治理规则.md','docs/设计/可重复构建与项目骨架.md',
            'docs/功能介绍/战斗辅助行为合同.md','docs/设计/战斗辅助实现设计.md','docs/计划/辅助瞄准阶段文档.md',
            'review-materials/aim-weapon-compat-20261015/evidence.md','review-materials/aim-weapon-compat-20261015/validation-map.json')
    }
    if($costNav){
        # One fixed owner-authorized cost/navigation repair. Full qualification
        # and installation permission remain separate from scoped evidence.
        $allowed=@('src/JueMingR.Features/Combat/AttackBallisticCandidates.cs',
            'src/JueMingR.TerrariaHost/Combat/CombatObservationWorldLayer.cs',
            'src/JueMingR.TerrariaHost/Combat/HostCombatObservation.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackAim.cs','src/JueMingR.TerrariaHost/Combat/HostAttackControl.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackObstacles.cs','src/JueMingR.TerrariaHost/Combat/HostBetsyFlameAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostBottleAttack.cs','src/JueMingR.TerrariaHost/Combat/HostYoyoNavigation.cs',
            'src/JueMingR.TerrariaHost/Combat/HostYoyoReleaseAttack.cs',
            'tests/JueMingR.ArchitectureTests/Phase0TArchitectureChecks.cs',
            'tests/NativeWorldTextProbe/NativeChecks.cs','tests/NativeWorldTextProbe/NativeCombatBallisticCandidateChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatBallisticPreparationChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatBetsyFlameChecks.cs','tests/NativeWorldTextProbe/NativeCombatBottleChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatMagicStringAimChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatYoyoNavigationChecks.cs','tests/NativeWorldTextProbe/NativeCombatCostNavigationChecks.cs',
            'tests/NativeWorldTextProbe/Fixtures/MagicReleaseBeforeNpc.bits',
            'tests/NativeWorldTextProbe/NativeCombatPathContinuityChecks.cs',
            'scripts/workload/Workload.Definitions.ps1','scripts/workload/AimStage.Development.ps1',
            'docs/计划/辅助瞄准阶段文档.md','docs/设计/战斗辅助实现设计.md',
            'docs/AI经验笔记/战斗移动与钓鱼机制.md',
            'review-materials/aim-cost-nav-20261015/evidence.md','review-materials/aim-cost-nav-20261015/validation-map.json',
            'review-materials/aim-cost-nav-20261015/main-summary.csv','review-materials/aim-cost-nav-20261015/delivery-receipt.json')
    }
    if($algorithm){
        # Exact complete 2026-10-15 owner work package. New files still require
        # explicit review here; this is not a combat-directory exemption.
        $allowed=@(
            # Owner 2026-10-16 default-silent isolated-test instruction.
            'AGENTS.md','docs/规范/测试与审计规则.md','tests/NativeWorldTextProbe/NativeTestAudio.cs',
            'tests/NativeWorldTextProbe/Program.cs','tests/NativeWorldTextProbe/NativeQuickItemChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatPrivateImageChecks.cs',
            # W1/W2 native four-caller birth/ammo composition repair.
            'src/JueMingR.TerrariaHost/Combat/HostNativeArrowBirthAttack.cs',
            'tests/NativeWorldTextProbe/NativeCombatNativeArrowBirthChecks.cs',
            'docs/计划/辅助瞄准阶段文档.md',
            'review-materials/aim-algorithm-rework-20261015/B-feedback-final-evidence.json',
            'review-materials/aim-algorithm-rework-20261015/07-complete-candidate.md',
            'review-materials/aim-algorithm-rework-20261015/07-fixture-migrations.md',
            'review-materials/aim-algorithm-rework-20261015/07-ordinary-plan.md',
            'review-materials/aim-algorithm-rework-20261015/07-ordinary-results.md',
            'review-materials/aim-algorithm-rework-20261015/attack-lifecycle-map.csv',
            'review-materials/aim-algorithm-rework-20261015/attack-route-inventory.csv',
            'review-materials/aim-algorithm-rework-20261015/attack-routes-m2k.csv',
            'review-materials/aim-algorithm-rework-20261015/baseline-stage-elapsed.csv',
            'review-materials/aim-algorithm-rework-20261015/C-effects-final.csv',
            'review-materials/aim-algorithm-rework-20261015/evidence-map.json',
            'review-materials/aim-algorithm-rework-20261015/evidence.md',
            'review-materials/aim-algorithm-rework-20261015/final-main-summary.csv',
            'review-materials/aim-algorithm-rework-20261015/main-summary.csv',
            'review-materials/aim-algorithm-rework-20261015/mechanism-coverage.md',
            'review-materials/aim-algorithm-rework-20261015/mechanism-evidence-index.json',
            'review-materials/aim-algorithm-rework-20261015/native-event-closure-summary.json',
            'review-materials/aim-algorithm-rework-20261015/native-event-closure.csv',
            'review-materials/aim-algorithm-rework-20261015/native-player-callers-summary.json',
            'review-materials/aim-algorithm-rework-20261015/native-player-callers.csv',
            'review-materials/aim-algorithm-rework-20261015/native-projectile-events.csv',
            'review-materials/aim-algorithm-rework-20261015/npc-default-nonreceiving-routes.csv',
            'review-materials/aim-algorithm-rework-20261015/npc-mechanism-evidence.csv',
            'review-materials/aim-algorithm-rework-20261015/npc-mechanism-map.csv',
            'review-materials/aim-algorithm-rework-20261015/npc-receiving-fallback-routes.csv',
            'review-materials/aim-algorithm-rework-20261015/npc-route-inventory.csv',
            'review-materials/aim-algorithm-rework-20261015/rebuild-attack-map.py',
            'review-materials/aim-algorithm-rework-20261015/rebuild-event-closure.py',
            'review-materials/aim-algorithm-rework-20261015/rebuild-mechanism-evidence.py',
            'review-materials/aim-algorithm-rework-20261015/rebuild-npc-map.ps1',
            'review-materials/aim-algorithm-rework-20261015/rebuild-player-callers.py',
            'review-materials/aim-algorithm-rework-20261015/weapon-mechanisms-A.csv',
            'review-materials/aim-algorithm-rework-20261015/weapon-mechanisms-B.csv',
            'review-materials/aim-algorithm-rework-20261015/weapon-mechanisms-C.csv',
            'scripts/workload/AimStage.Development.ps1',
            'scripts/workload/Workload.Definitions.ps1',
            'src/JueMingR.Features/Combat/AttackMotion.cs',
            'src/JueMingR.Features/Combat/AttackMotionCandidates.cs',
            'src/JueMingR.Features/Combat/RollingNpcPrediction.Continuation.cs',
            'src/JueMingR.Features/Combat/RollingNpcPrediction.cs',
            'src/JueMingR.TerrariaHost/Combat/AttackAmmoSnapshot.cs',
            'src/JueMingR.TerrariaHost/Combat/CombatFacing.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAi29SpellAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAmmoImpactAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAmmoSteeringAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostArcSwingAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostArrowFamilyAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackAim.Basic.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackAim.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackAim.FlightFamilies.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackAim.FlightPreview.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackAim.Preview.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackAim.Release.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackAim.Selection.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackControl.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackControl.Preview.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackClock.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackModels.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackObstacles.AmmoImpact.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackObstacles.Arc.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackObstacles.Ballistics.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackObstacles.BubbleFlail.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackObstacles.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackObstacles.Guitar.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackObstacles.HarpyBatch.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackObstacles.HeldAmmo.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackObstacles.HeldPrefix.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackObstacles.Homing.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackObstacles.Jousting.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackObstacles.Medusa.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackObstacles.MeleeHitFamily.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackObstacles.Monk.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackObstacles.Phantasm.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackObstacles.Piranha.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackObstacles.Pumpkin.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackObstacles.SharpTears.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackObstacles.Solar.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackObstacles.WhipStar.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackObstacles.Yoyo.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackWindow.cs',
            'src/JueMingR.TerrariaHost/Combat/HostBeeAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostBetsyFlameAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostBubbleFlailFamilyAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostClingerWallAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostCloudAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostCombatObservation.cs',
            'src/JueMingR.TerrariaHost/Combat/HostCrystalAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostDirectionalBladeAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostDirectionalControl.cs',
            'src/JueMingR.TerrariaHost/Combat/HostDirectionalStaffAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostDirectionalStormAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostEffectAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostExplosiveAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostFiniteShotAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostFiniteThrowAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostFreeFlightAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostGrassCurveAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostGuitarCurveAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostHarpyAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostHeldAmmoFamilyAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostHeldAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostHomingEffectAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostHomingEffectAttack.Eventide.cs',
            'src/JueMingR.TerrariaHost/Combat/HostHomingEffectAttack.Lightning.cs',
            'src/JueMingR.TerrariaHost/Combat/HostImpactBurstAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostInfluxAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostJoustingLanceAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostLightsBaneAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostMagicBounceAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostMagnetSphereAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostMedusaScanAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostMeleeAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostMeleeHitChildAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostMeleeHitFamilyAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostMonkStaffAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostMonkStaffAttack.Solve.cs',
            'src/JueMingR.TerrariaHost/Combat/HostMotionAttackInputs.cs',
            'src/JueMingR.TerrariaHost/Combat/HostNailAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostNebulaArcanumAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostNorthPoleAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostOnyxBlastAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostPhantasmEchoAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostPhoenixAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostPiranhaAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostPlacedPulseAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostProjectileBirthSlots.cs',
            'src/JueMingR.TerrariaHost/Combat/HostProjectileEnvironment.cs',
            'src/JueMingR.TerrariaHost/Combat/HostProjectileEnvironment.Replay.cs',
            'src/JueMingR.TerrariaHost/Combat/HostProjectileFamilyLease.cs',
            'src/JueMingR.TerrariaHost/Combat/HostProjectileTargets.cs',
            'src/JueMingR.TerrariaHost/Combat/HostPumpkinEffectAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostRainbowAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostRaySpellAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostSeedlerAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostSeedThrowAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostSharpTearsAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostSkyAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostSolarEruptionAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostSpearShardAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostSpiritFlameAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostSuperStarAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostSwingAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostTyphoonAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostVortexRocketAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostWeatherPainAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostWhipStarAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostYoyoFamilyAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostYoyoNavigation.cs',
            'src/JueMingR.TerrariaHost/Combat/HostYoyoReleaseAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/NpcPredictionSource.Continuation.cs',
            'src/JueMingR.TerrariaHost/Combat/NpcPredictionSource.cs',
            'src/JueMingR.TerrariaHost/Combat/PredictionTerrain.Line.cs',
            'src/JueMingR.TerrariaHost/Tools/ToolHooks.cs',
            'tests/JueMingR.ArchitectureTests/Combat/RollingNpcPredictionChecks.cs',
            'tests/JueMingR.ArchitectureTests/Phase0TArchitectureChecks.cs',
            'tests/NativeWorldTextProbe/NativeChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatAi29SpellChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatAimChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatAimQualificationChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatAimUiChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatAmmoImpactChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatAmmoSteeringChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatArcOwnerPoseChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatArcSwingChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatArrowFamilyChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatAttachedShotChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatAttackFailureChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatBallisticCloudChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatBallisticFreeFlightChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatBallisticMotionChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatBallisticPreparationChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatBallisticRapierChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatBallisticSceneChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatBallisticSeedChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatBallisticSpiritChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatBallisticTyphoonChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatBarrelChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatBeamChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatBeeChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatBeeGunChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatBeeHandChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatBetsyFlameChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatBlizzardChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatBookStaffChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatBottleChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatBubbleFlailFamilyChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatCandidateDemandChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatCelebrationChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatClingerWallChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatColdBoltChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatControlPreviewChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatControlChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatDartChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatDirectionalControlChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatEffectChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatElectrosphereChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatExplosiveChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatFiniteShotChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatFixedSkyChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatFlameChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatFlareChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatFlyingKnifeChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatGemStaffChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatGrassCurveChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatGuitarCurveChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatHandFixture.cs',
            'tests/NativeWorldTextProbe/NativeCombatHarpoonChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatHarpyArrowFamilyChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatHarpyChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatHeldAmmoFamilyChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatHomingEffectChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatIceRodChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatImpactBurstChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatImpactVisualChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatInfernoChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatInfluxChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatIntegrationChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatJavelinChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatJoustingLanceChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatLaserChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatLaserPaintChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatLightsBaneChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatLineChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatMagicStringAimChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatMagnetSphereChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatMedusaScanChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatMeleeChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatMeleeHitChildChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatMeleeHitFamilyChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatMixedSwingChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatMonkStaffChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatNailChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatNebulaArcanumChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatNebulaBlazeChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatNorthPoleChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatNoteChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatObservationChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatOnyxBlastChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatOrnamentChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatOriginalFeedbackChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatOriginalRangedChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatPaperAirplaneChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatPathContinuityChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatPhantasmEchoChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatPhasebladeChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatPhaseChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatPhoenixChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatPiranhaChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatPlacedPulseChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatPoisonCloudChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatPredictionChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatRainbowChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatRaySpellChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatScatterChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatScourgeChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatScytheChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatSeedlerChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatSharpTearsChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatSkullChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatSkyChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatSkyFractureChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatSolarEruptionChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatSonicBoomChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatSpearShardChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatSpecialArrowChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatStreamChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatSuperStarChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatSwingChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatSwingSpecialChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatTerrainLineChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatThornChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatUsageChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatVisualChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatWaspBatChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatWeaponPreparationTrace.cs',
            'tests/NativeWorldTextProbe/NativeCombatWeatherPainChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatWhipStarChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatWindDependencyChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatYoyoChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatYoyoFamilyChecks.cs',
            'tests/NativeWorldTextProbe/NativeCombatYoyoFamilyConsumerChecks.cs')
    }
    if($balanced){
        # Fixed successor authorized by the owner's 00/01/05 package. Its
        # representative-input duties replace general full-lifecycle gates;
        # committed bytes, all input identities and independent review remain.
        $allowed=@(
            'docs/功能介绍/战斗辅助行为合同.md','docs/计划/辅助瞄准阶段文档.md',
            'docs/设计/战斗辅助实现设计.md','docs/设计/可重复构建与项目骨架.md',
            'src/JueMingR.Features/Combat/AttackMotion.cs','src/JueMingR.Features/Combat/AttackMotionCandidates.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackSolution.cs','src/JueMingR.TerrariaHost/Combat/HostRepresentativeAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostMotionAttackInputs.cs','src/JueMingR.TerrariaHost/Combat/HostNativeArrowBirthAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostNorthPoleAttack.cs','src/JueMingR.TerrariaHost/Combat/HostSpearShardAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackAim.cs','src/JueMingR.TerrariaHost/Combat/HostAttackAim.Balanced.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackAim.Basic.cs','src/JueMingR.TerrariaHost/Combat/HostAttackAim.Preview.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackAim.FlightPreview.cs','src/JueMingR.TerrariaHost/Combat/HostAttackAim.HandReuse.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackAim.Selection.cs',
            'src/JueMingR.TerrariaHost/Combat/HostAttackControl.cs','src/JueMingR.TerrariaHost/Combat/HostAttackControl.Balanced.cs',
            'src/JueMingR.TerrariaHost/Combat/HostCadenceAim.cs','src/JueMingR.TerrariaHost/Combat/HostHeldAttack.cs',
            'src/JueMingR.TerrariaHost/Combat/HostHeldAmmoFamilyAttack.cs',
            'tests/NativeWorldTextProbe/NativeCombatBalancedAimChecks.cs','tests/NativeWorldTextProbe/NativeCombatBalancedPhaseChecks.cs','tests/NativeWorldTextProbe/NativeCombatBalancedCandidateChecks.cs','tests/NativeWorldTextProbe/NativeCombatAimChecks.cs','tests/NativeWorldTextProbe/NativeCombatBallisticMotionChecks.cs','tests/NativeWorldTextProbe/NativeCombatIntegrationChecks.cs',
            'scripts/workload/AimStage.Development.ps1','scripts/workload/Workload.Definitions.ps1',
            'scripts/phase0s/Continuous-Processing-Owner-Test-Card.zh-CN.md',
            'review-materials/aim-balanced-20261017/evidence.md','review-materials/aim-balanced-20261017/validation-map.json',
            'review-materials/aim-balanced-20261017/mechanism-coverage.md','review-materials/aim-balanced-20261017/main-summary.csv',
            'review-materials/aim-balanced-20261017/owner-test-card.md')
    }
    if($pathDisplay){
        # Owner's 00/01/04/05 authorization is restricted to independent short
        # display supply/continuity. This locks its complete closing delta and
        # actual independent review without changing the default full gate.
        $allowed=@('src/JueMingR.TerrariaHost/Combat/HostCombatObservation.cs',
            'src/JueMingR.TerrariaHost/Combat/PredictionTerrain.cs',
            'tests/NativeWorldTextProbe/NativeCombatPathDisplayChecks.cs',
            'tests/NativeWorldTextProbe/NativeChecks.cs',
            'scripts/workload/AimStage.Development.ps1','scripts/workload/Workload.Definitions.ps1',
            'docs/功能介绍/战斗辅助行为合同.md','docs/计划/辅助瞄准阶段文档.md',
            'docs/设计/可重复构建与项目骨架.md',
            'review-materials/path-display-20261017/evidence.md',
            'review-materials/path-display-20261017/validation-map.json',
            'review-materials/path-display-20261017/main-summary.csv',
            'review-materials/path-display-20261017/reason-excerpt.csv',
            'review-materials/path-display-20261017/owner-test-card.md',
            'review-materials/path-display-20261017/delivery-receipt.json')
    }
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
    if($successor){$required+='original-feedback'}
    if($priority){$required=@('architecture-combat','candidate-demand','basic-aim','natural-draw',
        'natural-cpu','continuous-reference','draw-cpu','integration','attack-costs',
        'navigation','impact-draw','whip-terrain','original-feedback')}
    if($continuation){$required=@('architecture-combat','optional-consumption','marker-demand','candidate-demand',
        'local-failure','aim-input','control-input','effects-receive','selection-preview','attack-costs','impact-draw',
        'main-performance','representative-opportunities','navigation','whip-terrain','original-feedback')}
    if($dedup){$required=@('architecture-combat','optional-consumption','marker-demand','candidate-demand',
        'local-failure','aim-input','control-input','effects-receive','selection-preview','attack-costs','impact-draw',
        'main-performance','representative-opportunities','navigation','whip-terrain','original-feedback',
        'dedup-correctness','dedup-model','dedup-work','dedup-performance','workload-routing')}
    if($targetPerf){$required=@('architecture-combat','candidate-demand','candidate-first-failure',
        'attack-costs','basic-aim','selection-preview','navigation','whip-terrain',
        'main-performance','representative-opportunities','workload-routing')}
    if($weaponCompat){$required=@('architecture-combat','sentry-placement','magic-string-aim','ballistic-candidates',
        'candidate-demand','attack-costs','basic-aim','selection-preview','navigation','whip-terrain',
        'main-performance','representative-opportunities','workload-routing')}
    if($costNav){$required=@('architecture-combat','contact-navigation','magic-release','ballistic-candidates',
        'ballistic-preparation','betsy-native','bottle-native','effects-receive','path-continuity',
        'main-performance','representative-opportunities','workload-routing')}
    if($algorithm){$required=@('architecture-combat','weapon-mechanisms','npc-mechanisms','control-input',
        'selection-modes','source-retirement','impact-draw','path-continuity','main-performance',
        'representative-opportunities','full-coverage','workload-routing')}
    if($balanced){$required=@('architecture-combat','balanced-native-input','mechanism-coverage',
        'selection-modes','npc-yoyo-safety','main-performance','workload-routing')}
    if($pathDisplay){$required=@('architecture-combat','independent-source-draw','same-update-facts',
        'selection-modes','npc-yoyo-safety','main-performance','workload-routing')}
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
    $failureKinds=@('TEST_CORRECTED','PRODUCT_FIXED','ENVIRONMENT_LIMITATION')
    if($continuation -or $dedup -or $targetPerf -or $weaponCompat -or $algorithm){$failureKinds+='OWNER_WITHDRAWN'}
    foreach($failure in $record.failureDispositions){
        if($failure.disposition -ceq 'OWNER_WITHDRAWN' -and
            ($failure.ownerDecision -cne '2026-10-14-impact-single-attempt-stop-loss' -or $failure.capability -cne 'impact-preview')) {throw 'Feature withdrawal requires this owner decision and exact retired capability.'}
        if($failure.disposition -cnotin $failureKinds -or
            -not $failure.cause -or -not $failure.resolution -or @($failure.artifacts).Count -eq 0){throw 'Unresolved stage failure.'}
        foreach($artifact in $failure.artifacts){$null=Read-AimStageArtifact $Root $artifact}
    }
    # #114's complete candidate requires both fresh independent review
    # packages. Two references to one artifact cannot satisfy the two scopes;
    # author independence and actual model provenance remain in the records
    # and the main coordinator's admission review, not inferred from a path.
    if($algorithm -and (@($record.reviews).Count -ne 2 -or
        @($record.reviews | ForEach-Object {$_.artifact.path} | Sort-Object -Unique).Count -ne 2)) {
        throw 'Algorithm candidate requires two distinct independent review artifacts.'
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
    return [ordered]@{status='STAGE_DEVELOPMENT';mode=$(if($pathDisplay){'ExplicitPathDisplay20261017'}elseif($balanced){'ExplicitAimBalanced20261017'}elseif($algorithm){'ExplicitAimAlgorithmRework20261015'}elseif($costNav){'ExplicitAimCostNav20261015'}elseif($weaponCompat){'ExplicitAimWeaponCompat20261015'}elseif($targetPerf){'ExplicitAimTargetPerf20261014'}elseif($dedup){'ExplicitAimDedupRepair20261014'}elseif($continuation){'ExplicitAimContinuation20261013'}elseif($priority){'ExplicitAimPriorityRecovery20261013'}elseif($successor){'ExplicitAimRootFix20261012'}else{'ExplicitAimStage20261011'});fullQualification='INCOMPLETE';releaseEligible=$false;
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

# One owner-authorized #118 candidate contract. This preserves the formal
# default and cannot be reused as #114 qualification or a generic skip mode.
function Read-Footprints200HourDevelopment {
    param([string] $Root,[string] $Path,$Identity,$InputIdentity,$Record,[string] $Relative)
    $base='f50646b083b27248731dbf7afdc53a3876ff4309'
    if($Record.authorization -cne 'owner-issue118-footprints-200h-20261017' -or $Record.productBase -cne $base -or
        $Record.candidateCommit -cne $Identity.commit -or $Record.sourceFingerprint -cne $Identity.fingerprint -or
        $Record.inputFingerprint -cne $InputIdentity.fingerprint -or $Record.fullQualification -cne 'INCOMPLETE' -or
        $Record.releaseEligible -ne $false -or $null -eq $Record.PSObject.Properties['openFailures'] -or @($Record.openFailures).Count){throw 'Footprint candidate/evidence lock mismatch or unresolved failure.'}
    $retained=if($null -ne $script:WorkloadRetainedAssets){@($script:WorkloadRetainedAssets.files.path)}else{@()}
    if(@((Get-WorkloadChanges $Root 'HEAD').paths | Where-Object {$_ -cnotin $retained}).Count){throw 'Footprint candidate has uncommitted inputs.'}
    & git -C $Root merge-base --is-ancestor $base $Identity.commit
    if($LASTEXITCODE -ne 0){throw 'Footprint candidate lost its accepted base.'}
    $allowed=@(
        'src/JueMingR.Platform/Footprints/FootprintSample.cs','src/JueMingR.Platform/Footprints/FootprintOverview.cs',
        'src/JueMingR.Features/Footprints/FootprintRecorder.cs','src/JueMingR.Features/Footprints/FootprintArchive.cs',
        'src/JueMingR.Features/Footprints/FootprintCodec.cs','src/JueMingR.Features/Footprints/FootprintStore.cs',
        'src/JueMingR.Features/Footprints/FootprintPlayback.cs','src/JueMingR.Features/Footprints/FootprintOverviewGeometry.cs',
        'src/JueMingR.Features/Footprints/FootprintOverviewIndex.cs','src/JueMingR.Infrastructure/Footprints/FileFootprintArchive.cs',
        'src/JueMingR.TerrariaHost/Footprints/HostFootprints.cs','src/JueMingR.TerrariaHost/Footprints/FootprintSourceHooks.cs',
        'src/JueMingR.TerrariaHost/Footprints/FootprintPlayingClock.cs','src/JueMingR.TerrariaHost/Footprints/FootprintMapLayer.cs',
        'src/JueMingR.TerrariaHost/F5/FootprintPopup.cs',
        'tests/Footprints/FootprintCoreChecks.cs','tests/Footprints/FootprintFileChecks.cs','tests/Footprints/FootprintWorkerChecks.cs',
        'tests/Footprints/FootprintOverviewChecks.cs','tests/Footprints/FootprintLongHistoryChecks.cs',
        'tests/NativeWorldTextProbe/NativeFootprintChecks.cs','tests/NativeWorldTextProbe/NativeFootprintVisualChecks.cs',
        'tests/NativeWorldTextProbe/NativeFootprintTimingChecks.cs','tests/NativeWorldTextProbe/NativeFootprint200HourVisualChecks.cs',
        'tests/NativeWorldTextProbe/NativeFootprintPathCoexistenceChecks.cs','tests/NativeWorldTextProbe/NativeChecks.cs',
        'tests/JueMingR.ArchitectureTests/JueMingR.ArchitectureTests.csproj','tests/JueMingR.ArchitectureTests/Program.cs',
        'tests/JueMingR.ArchitectureTests/CheckCatalog.cs','tests/JueMingR.ArchitectureTests/OperationContractChecks.cs',
        'tests/JueMingR.ArchitectureTests/Phase0TArchitectureChecks.cs',
        'docs/功能介绍/足迹记录路线与回放行为合同.md','docs/设计/足迹记录路线与回放实现设计.md',
        'docs/功能介绍/功能索引.md','docs/功能介绍/玩家使用指南.md','docs/设计/配置持久化与用户数据布局.md',
        'docs/设计/可重复构建与项目骨架.md','docs/AI经验笔记/信息地图与持久状态.md','docs/AI经验笔记/索引.md',
        'scripts/workload/AimStage.Development.ps1','review-materials/footprints-200h-20261017/evidence.md')
    $changed=@(Invoke-WorkloadGit $Root @('diff','--name-only','--no-renames',$base,$Identity.commit,'--') | Sort-Object)
    if(@($changed | Where-Object {$_ -cnotin $allowed}).Count -or
        (@($Record.revisions.path | Sort-Object) -join '|') -cne ($changed -join '|')){throw 'Footprint revision scope mismatch.'}
    foreach($entry in $Record.revisions){if((Get-WorkloadFileHash -LiteralPath (Join-Path $Root $entry.path)).Hash -cne $entry.sha256){throw 'Footprint revision bytes changed.'}}
    $required=@('build-structure','footprints-core','footprints-overview','rich-200h','native-host-time-input','native-xna-200h','map-path-coexistence','package-entry-contract')
    if((@($Record.checks.name | Sort-Object) -join '|') -cne (@($required | Sort-Object) -join '|')){throw 'Footprint necessary scopes missing/duplicated.'}
    foreach($check in $Record.checks){
        if($check.exit -ne 0 -or $check.disposition -cnotin @('CURRENT','REVIEWED_REUSE') -or -not $check.scope -or -not $check.applicability -or @($check.artifacts).Count -eq 0){throw 'Invalid footprint scoped evidence.'}
        foreach($artifact in $check.artifacts){$null=Read-AimStageArtifact $Root $artifact}
    }
    if(@($Record.failureDispositions).Count -eq 0 -or @($Record.reviews).Count -eq 0 -or @($Record.limits).Count -eq 0){throw 'Footprint failures, independent review or limits missing.'}
    foreach($failure in $Record.failureDispositions){
        if($failure.disposition -cnotin @('PRODUCT_FIXED','TEST_CORRECTED','ENVIRONMENT_LIMITATION') -or -not $failure.cause -or -not $failure.resolution -or @($failure.artifacts).Count -eq 0){throw 'Unresolved footprint failure.'}
        foreach($artifact in $failure.artifacts){$null=Read-AimStageArtifact $Root $artifact}
    }
    foreach($review in $Record.reviews){
        if($review.result -cne 'APPROVED_FOR_STAGE' -or $review.sourceFingerprint -cne $Identity.fingerprint -or -not $review.coverage){throw 'Footprint independent review identity mismatch.'}
        $null=Read-AimStageArtifact $Root $review.artifact
    }
    if($Record.admission.result -cne 'READY_FOR_PACKAGE' -or $Record.admission.sourceFingerprint -cne $Identity.fingerprint){throw 'Footprint coordinator admission missing.'}
    $null=Read-AimStageArtifact $Root $Record.admission.artifact
    $debug=Read-WorkloadJson (Join-Path $Root 'artifacts/build/Debug/build-record.json')
    if(-not (Test-WorkloadBuildMatch $Root $debug $Identity $InputIdentity)){throw 'Footprint Debug detection build changed.'}
    $consumers=@('artifacts/build/Debug/checks/bin/NativeWorldTextProbe/x86/Debug/net472/NativeWorldTextProbe.exe')
    foreach($project in @('Platform','Features','Infrastructure','TerrariaHost','ArchitectureTests')){$consumers+=('artifacts/build/Debug/work/bin/JueMingR.'+$project+'/x86/Debug/net472/JueMingR.'+$project+'.'+$(if($project -ceq 'ArchitectureTests'){'exe'}else{'dll'}))}
    if((@($Record.detectionFiles.path | Sort-Object) -join '|') -cne (@($consumers | Sort-Object) -join '|')){throw 'Footprint detection consumer set mismatch.'}
    foreach($entry in $Record.detectionFiles){if((Get-WorkloadFileHash -LiteralPath (Join-Path $Root $entry.path)).Hash -cne $entry.sha256){throw 'Footprint detection bytes changed.'}}
    return [ordered]@{status='STAGE_DEVELOPMENT';mode='ExplicitFootprints200Hour20261017';fullQualification='INCOMPLETE';releaseEligible=$false;productBase=$base;recordPath=$Relative;recordSha256=(Get-WorkloadFileHash -LiteralPath $Path).Hash;necessaryScopes=$required;limits=@($Record.limits);checkCount=$required.Count}
}

# Exact owner-authorized HIDE #120 candidate; default full qualification stays
# unchanged. Evidence fixes inputs and consumers, never relabels old results.
function Read-HiddenEntryDevelopment {
    param([string] $Root,[string] $Path,$Identity,$InputIdentity,$Record,[string] $Relative)
    $base='66870f3cdb25d5f653fbe67b6235d65a5d8df51e'
    if($Record.authorization -cne 'owner-issue120-hidden-entry-20261018' -or $Record.productBase -cne $base -or
        $Record.candidateCommit -cne $Identity.commit -or $Record.sourceFingerprint -cne $Identity.fingerprint -or
        $Record.inputFingerprint -cne $InputIdentity.fingerprint -or $Record.fullQualification -cne 'INCOMPLETE' -or
        $Record.releaseEligible -ne $false -or $null -eq $Record.PSObject.Properties['openFailures'] -or @($Record.openFailures).Count){throw 'Hidden entry candidate/evidence lock mismatch or unresolved failure.'}
    $retained=if($null -ne $script:WorkloadRetainedAssets){@($script:WorkloadRetainedAssets.files.path)}else{@()}
    if(@((Get-WorkloadChanges $Root 'HEAD').paths | Where-Object {$_ -cnotin $retained}).Count){throw 'Hidden entry candidate has uncommitted inputs.'}
    & git -C $Root merge-base --is-ancestor $base $Identity.commit
    if($LASTEXITCODE -ne 0){throw 'Hidden entry candidate lost its accepted base.'}
    $allowed=@(
        'src/JueMingR.Features/HiddenFeatures/HiddenFeatureAccess.cs',
        'src/JueMingR.TerrariaHost/About/AboutPage.cs',
        'src/JueMingR.TerrariaHost/F5/F5Layout.cs','src/JueMingR.TerrariaHost/F5/F5Interaction.cs',
        'src/JueMingR.TerrariaHost/F5/F5Shell.cs','src/JueMingR.TerrariaHost/F5/GuidanceControls.cs',
        'src/JueMingR.TerrariaHost/Guidance/HostGuidance.cs','src/JueMingR.TerrariaHost/Guidance/MerchantTestOperations.cs',
        'src/JueMingR.TerrariaHost/Phase0SLoadChainHost.cs',
        'tests/JueMingR.ArchitectureTests/HiddenFeatureAccessChecks.cs','tests/JueMingR.ArchitectureTests/GuidanceTests.cs',
        'tests/JueMingR.ArchitectureTests/Phase0TArchitectureChecks.cs',
        'tests/NativeWorldTextProbe/NativeGuidanceChecks.cs','tests/NativeWorldTextProbe/NativeGuidanceInputChecks.cs',
        'tests/NativeWorldTextProbe/NativeAboutChecks.cs','tests/NativeWorldTextProbe/NativeAboutVisualChecks.cs',
        'tests/NativeWorldTextProbe/NativeHiddenEntryChecks.cs','tests/NativeWorldTextProbe/NativeChecks.cs',
        'tests/NativeWorldTextProbe/NativePageCompositionChecks.cs',
        'scripts/test-world-object-text.ps1','scripts/workload/AimStage.Development.ps1',
        'docs/功能介绍/关于帮助与反馈行为合同.md','docs/功能介绍/稀有生物旅商方向与装备提示行为合同.md',
        'docs/功能介绍/功能索引.md','docs/功能介绍/玩家使用指南.md',
        'docs/设计/关于帮助与反馈实现设计.md','docs/设计/方向指引装备提示与游商测试实现设计.md',
        'docs/设计/可重复构建与项目骨架.md','review-materials/hidden-features-entry-20261018/evidence.md')
    $changed=@(Invoke-WorkloadGit $Root @('diff','--name-only','--no-renames',$base,$Identity.commit,'--') | Sort-Object)
    if(@($changed | Where-Object {$_ -cnotin $allowed}).Count -or
        (@($Record.revisions.path | Sort-Object) -join '|') -cne ($changed -join '|')){throw 'Hidden entry revision scope mismatch.'}
    foreach($entry in $Record.revisions){if((Get-WorkloadFileHash -LiteralPath (Join-Path $Root $entry.path)).Hash -cne $entry.sha256){throw 'Hidden entry revision bytes changed.'}}
    $required=@('build-structure','hidden-state-guidance','native-hidden-entry','native-about-xna','native-page-composition','package-entry-contract')
    if((@($Record.checks.name | Sort-Object) -join '|') -cne (@($required | Sort-Object) -join '|')){throw 'Hidden entry necessary scopes missing/duplicated.'}
    foreach($check in $Record.checks){
        if($check.exit -ne 0 -or $check.disposition -cnotin @('CURRENT','REVIEWED_REUSE') -or -not $check.scope -or -not $check.applicability -or @($check.artifacts).Count -eq 0){throw 'Invalid hidden entry scoped evidence.'}
        foreach($artifact in $check.artifacts){$null=Read-AimStageArtifact $Root $artifact}
    }
    if(@($Record.reviews).Count -eq 0 -or @($Record.limits).Count -eq 0 -or $null -eq $Record.PSObject.Properties['failureDispositions']){throw 'Hidden entry independent review, failure dispositions or limits missing.'}
    foreach($failure in $Record.failureDispositions){
        if($failure.disposition -cnotin @('PRODUCT_FIXED','TEST_CORRECTED','ENVIRONMENT_LIMITATION') -or -not $failure.cause -or -not $failure.resolution -or @($failure.artifacts).Count -eq 0){throw 'Unresolved hidden entry failure.'}
        foreach($artifact in $failure.artifacts){$null=Read-AimStageArtifact $Root $artifact}
    }
    foreach($review in $Record.reviews){
        if($review.result -cne 'APPROVED_FOR_STAGE' -or $review.sourceFingerprint -cne $Identity.fingerprint -or -not $review.coverage){throw 'Hidden entry independent review identity mismatch.'}
        $null=Read-AimStageArtifact $Root $review.artifact
    }
    if($Record.admission.result -cne 'READY_FOR_PACKAGE' -or $Record.admission.sourceFingerprint -cne $Identity.fingerprint){throw 'Hidden entry coordinator admission missing.'}
    $null=Read-AimStageArtifact $Root $Record.admission.artifact
    $debug=Read-WorkloadJson (Join-Path $Root 'artifacts/build/Debug/build-record.json')
    if(-not (Test-WorkloadBuildMatch $Root $debug $Identity $InputIdentity)){throw 'Hidden entry Debug detection build changed.'}
    $consumers=@('artifacts/build/Debug/checks/bin/NativeWorldTextProbe/x86/Debug/net472/NativeWorldTextProbe.exe')
    foreach($project in @('Platform','Features','Infrastructure','TerrariaHost','ArchitectureTests')){$consumers+=('artifacts/build/Debug/work/bin/JueMingR.'+$project+'/x86/Debug/net472/JueMingR.'+$project+'.'+$(if($project -ceq 'ArchitectureTests'){'exe'}else{'dll'}))}
    if((@($Record.detectionFiles.path | Sort-Object) -join '|') -cne (@($consumers | Sort-Object) -join '|')){throw 'Hidden entry detection consumer set mismatch.'}
    foreach($entry in $Record.detectionFiles){if((Get-WorkloadFileHash -LiteralPath (Join-Path $Root $entry.path)).Hash -cne $entry.sha256){throw 'Hidden entry detection bytes changed.'}}
    return [ordered]@{status='STAGE_DEVELOPMENT';mode='ExplicitHiddenEntry20261018';fullQualification='INCOMPLETE';releaseEligible=$false;productBase=$base;recordPath=$Relative;recordSha256=(Get-WorkloadFileHash -LiteralPath $Path).Hash;necessaryScopes=$required;limits=@($Record.limits);checkCount=$required.Count}
}

function Read-JourneyMenuDevelopment {
    param([string] $Root,[string] $Path,$Identity,$InputIdentity,$Record,[string] $Relative)
    $base='0ef3c62dcf0c365aee5d5cfcfbb5492b70b841f3'
    if($Record.authorization -cne 'owner-issue122-journey-menu-20261018' -or $Record.productBase -cne $base -or
        $Record.candidateCommit -cne $Identity.commit -or $Record.sourceFingerprint -cne $Identity.fingerprint -or
        $Record.inputFingerprint -cne $InputIdentity.fingerprint -or $Record.fullQualification -cne 'INCOMPLETE' -or
        $Record.releaseEligible -ne $false -or $null -eq $Record.PSObject.Properties['openFailures'] -or @($Record.openFailures).Count){throw 'Journey menu candidate/evidence lock mismatch or unresolved failure.'}
    $retained=if($null -ne $script:WorkloadRetainedAssets){@($script:WorkloadRetainedAssets.files.path)}else{@()}
    if(@((Get-WorkloadChanges $Root 'HEAD').paths | Where-Object {$_ -cnotin $retained}).Count){throw 'Journey menu candidate has uncommitted inputs.'}
    & git -C $Root merge-base --is-ancestor $base $Identity.commit
    if($LASTEXITCODE -ne 0){throw 'Journey menu candidate lost its accepted base.'}
    $allowed=@(
        'src/JueMingR.TerrariaHost/Combat/Prediction/NativeValueSnapshot.cs',
        'src/JueMingR.TerrariaHost/Combat/Prediction/NativePredictionAlignment.cs',
        'src/JueMingR.TerrariaHost/Combat/Prediction/PredictionPipeProtocol.cs',
        'src/JueMingR.TerrariaHost/Combat/Prediction/PredictionWire.cs',
        'src/JueMingR.TerrariaHost/Items/ItemSourceHooks.cs',
        'src/JueMingR.Features/Journey/JourneyActivation.cs',
        'src/JueMingR.TerrariaHost/Journey/JourneySession.cs','src/JueMingR.TerrariaHost/Journey/JourneyHooks.cs',
        'src/JueMingR.TerrariaHost/Journey/JourneyPowers.cs','src/JueMingR.TerrariaHost/Journey/JourneyResearch.cs',
        'src/JueMingR.TerrariaHost/F5/JourneyControls.cs','src/JueMingR.TerrariaHost/F5/GuidanceControls.cs','src/JueMingR.TerrariaHost/F5/F5Layout.cs',
        'src/JueMingR.TerrariaHost/F5/F5Renderer.cs','src/JueMingR.TerrariaHost/F5/F5Shell.cs',
        'src/JueMingR.TerrariaHost/Hotkeys/HostHotkeys.cs','src/JueMingR.TerrariaHost/Hotkeys/JourneyHotkeys.cs',
        'src/JueMingR.TerrariaHost/Phase0SLoadChainHost.cs',
        'tests/JueMingR.ArchitectureTests/Phase0TArchitectureChecks.cs',
        'tests/Phase0U/JourneyUiChecks.cs','tests/Hotkeys/JourneyHotkeyChecks.cs',
        'tests/Phase0SFixtureTerraria/Phase0SFixtureTerraria.csproj','tests/Phase0SFixtureTerraria/Program.cs',
        'tests/NativeWorldTextProbe/NativeJourneyChecks.cs','tests/NativeWorldTextProbe/NativeJourneyUiChecks.cs',
        'tests/NativeWorldTextProbe/NativeJourneyPredictionChecks.cs',
        'tests/NativeWorldTextProbe/Program.cs','tests/NativeWorldTextProbe/NativeChecks.cs',
        'scripts/test-world-object-text.ps1','scripts/workload/AimStage.Development.ps1',
        'scripts/workload/Workload.Definitions.ps1','scripts/workload/Workload.Support.ps1',
        'docs/功能介绍/旅行菜单行为合同.md','docs/功能介绍/功能索引.md','docs/功能介绍/玩家使用指南.md',
        'docs/设计/旅行菜单实现设计.md','docs/设计/可重复构建与项目骨架.md','docs/README.md',
        'review-materials/journey-menu-20261018/evidence.md')
    $changed=@(Invoke-WorkloadGit $Root @('diff','--name-only','--no-renames',$base,$Identity.commit,'--') | Sort-Object)
    if(@($changed | Where-Object {$_ -cnotin $allowed}).Count -or
        (@($Record.revisions.path | Sort-Object) -join '|') -cne ($changed -join '|')){throw 'Journey menu revision scope mismatch.'}
    foreach($entry in $Record.revisions){if((Get-WorkloadFileHash -LiteralPath (Join-Path $Root $entry.path)).Hash -cne $entry.sha256){throw 'Journey menu revision bytes changed.'}}
    $required=@('build-structure','journey-state-input','native-journey-capabilities','native-journey-ui-coexistence','native-journey-save-read-kill','native-journey-prediction','journey-costs','package-entry-contract')
    if((@($Record.checks.name | Sort-Object) -join '|') -cne (@($required | Sort-Object) -join '|')){throw 'Journey menu necessary scopes missing/duplicated.'}
    foreach($check in $Record.checks){
        if($check.exit -ne 0 -or $check.disposition -cnotin @('CURRENT','REVIEWED_REUSE') -or -not $check.scope -or -not $check.applicability -or @($check.artifacts).Count -eq 0){throw 'Invalid journey menu scoped evidence.'}
        foreach($artifact in $check.artifacts){$null=Read-AimStageArtifact $Root $artifact}
    }
    if(@($Record.reviews).Count -eq 0 -or @($Record.limits).Count -eq 0 -or $null -eq $Record.PSObject.Properties['failureDispositions']){throw 'Journey menu independent review, failure dispositions or limits missing.'}
    foreach($failure in $Record.failureDispositions){
        if($failure.disposition -cnotin @('PRODUCT_FIXED','TEST_CORRECTED','ENVIRONMENT_LIMITATION') -or -not $failure.cause -or -not $failure.resolution -or @($failure.artifacts).Count -eq 0){throw 'Unresolved journey menu failure.'}
        foreach($artifact in $failure.artifacts){$null=Read-AimStageArtifact $Root $artifact}
    }
    foreach($review in $Record.reviews){
        if($review.result -cne 'APPROVED_FOR_STAGE' -or $review.sourceFingerprint -cne $Identity.fingerprint -or -not $review.coverage){throw 'Journey menu independent review identity mismatch.'}
        $null=Read-AimStageArtifact $Root $review.artifact
    }
    if($Record.admission.result -cne 'READY_FOR_PACKAGE' -or $Record.admission.sourceFingerprint -cne $Identity.fingerprint){throw 'Journey menu coordinator admission missing.'}
    $null=Read-AimStageArtifact $Root $Record.admission.artifact
    $debug=Read-WorkloadJson (Join-Path $Root 'artifacts/build/Debug/build-record.json')
    if(-not (Test-WorkloadBuildMatch $Root $debug $Identity $InputIdentity)){throw 'Journey menu Debug detection build changed.'}
    $consumers=@('artifacts/build/Debug/checks/bin/NativeWorldTextProbe/x86/Debug/net472/NativeWorldTextProbe.exe')
    foreach($project in @('Platform','Features','Infrastructure','TerrariaHost','ArchitectureTests')){$consumers+=('artifacts/build/Debug/work/bin/JueMingR.'+$project+'/x86/Debug/net472/JueMingR.'+$project+'.'+$(if($project -ceq 'ArchitectureTests'){'exe'}else{'dll'}))}
    if((@($Record.detectionFiles.path | Sort-Object) -join '|') -cne (@($consumers | Sort-Object) -join '|')){throw 'Journey menu detection consumer set mismatch.'}
    foreach($entry in $Record.detectionFiles){if((Get-WorkloadFileHash -LiteralPath (Join-Path $Root $entry.path)).Hash -cne $entry.sha256){throw 'Journey menu detection bytes changed.'}}
    return [ordered]@{status='STAGE_DEVELOPMENT';mode='ExplicitJourneyMenu20261018';fullQualification='INCOMPLETE';releaseEligible=$false;productBase=$base;recordPath=$Relative;recordSha256=(Get-WorkloadFileHash -LiteralPath $Path).Hash;necessaryScopes=$required;limits=@($Record.limits);checkCount=$required.Count}
}

# Only #124's fixed VIEW delta and reviewed evidence may enter the existing
# complete ContinuousProcessing development package. Default qualification,
# Release compilation, package members and installation admission stay owned
# by their existing callers; this reader never signs full qualification.
function Read-ViewUnlockDevelopment {
    param([string] $Root,[string] $Path,$Identity,$InputIdentity,$Record,[string] $Relative)
    $base='8bba8ffd2d24591e9efb1258ae0eac1e1d7d60df'
    if($Record.authorization -cne 'owner-issue124-view-unlock-20261018' -or $Record.productBase -cne $base -or
        $Record.candidateCommit -cne $Identity.commit -or $Record.sourceFingerprint -cne $Identity.fingerprint -or
        $Record.inputFingerprint -cne $InputIdentity.fingerprint -or $Record.fullQualification -cne 'INCOMPLETE' -or
        $Record.releaseEligible -ne $false -or $null -eq $Record.PSObject.Properties['openFailures'] -or @($Record.openFailures).Count){throw 'View unlock candidate/evidence lock mismatch or unresolved failure.'}
    $retained=if($null -ne $script:WorkloadRetainedAssets){@($script:WorkloadRetainedAssets.files.path)}else{@()}
    if(@((Get-WorkloadChanges $Root 'HEAD').paths | Where-Object {$_ -cnotin $retained}).Count){throw 'View unlock candidate has uncommitted inputs.'}
    & git -C $Root merge-base --is-ancestor $base $Identity.commit
    if($LASTEXITCODE -ne 0){throw 'View unlock candidate lost its accepted base.'}
    $allowed=@(
        'src/JueMingR.Features/View/ViewPreferenceCodec.cs',
        'src/JueMingR.Features/WorldObjectText/WorldObjectDiscovery.cs','src/JueMingR.Features/WorldTargets/WorldTargetFeature.cs',
        'src/JueMingR.TerrariaHost/Combat/CombatSelection.cs','src/JueMingR.TerrariaHost/Input/HostInputState.cs',
        'src/JueMingR.TerrariaHost/F5/F5Layout.cs','src/JueMingR.TerrariaHost/F5/F5Renderer.cs',
        'src/JueMingR.TerrariaHost/F5/F5Shell.cs','src/JueMingR.TerrariaHost/F5/ViewControls.cs',
        'src/JueMingR.TerrariaHost/View/HostView.cs','src/JueMingR.TerrariaHost/View/ViewHooks.cs',
        'src/JueMingR.TerrariaHost/View/ViewBackgroundScope.cs','src/JueMingR.TerrariaHost/View/ViewSectionRequests.cs',
        'src/JueMingR.TerrariaHost/View/ViewLegacyCapacity.cs','src/JueMingR.TerrariaHost/View/ViewNpcCulling.cs',
        'src/JueMingR.TerrariaHost/View/ViewSceneCapture.cs','src/JueMingR.TerrariaHost/View/ViewWaterResources.cs',
        'src/JueMingR.TerrariaHost/Phase0SLoadChainHost.cs',
        'tests/JueMingR.ArchitectureTests/CheckCatalog.cs','tests/JueMingR.ArchitectureTests/Phase0TArchitectureChecks.cs',
        'tests/JueMingR.ArchitectureTests/ViewPreferenceChecks.cs','tests/Phase0U/ViewControlChecks.cs',
        'tests/Phase0SFixtureTerraria/Phase0SFixtureTerraria.csproj','tests/Phase0SFixtureTerraria/Program.cs',
        'tests/NativeWorldTextProbe/NativeChecks.cs','tests/NativeWorldTextProbe/NativeViewCombatChecks.cs',
        'tests/NativeWorldTextProbe/NativeViewNetworkChecks.cs','tests/NativeWorldTextProbe/NativeViewOriginalChecks.cs',
        'tests/NativeWorldTextProbe/NativeViewNpcChecks.cs','tests/NativeWorldTextProbe/NativeViewFilterChecks.cs',
        'tests/NativeWorldTextProbe/NativeViewFailureChecks.cs','tests/NativeWorldTextProbe/NativeViewWaterChecks.cs',
        'scripts/test-world-object-text.ps1','scripts/workload/AimStage.Development.ps1',
        'scripts/workload/Workload.Definitions.ps1','scripts/workload/Workload.Support.ps1',
        'docs/README.md','docs/功能介绍/功能索引.md','docs/功能介绍/玩家使用指南.md',
        'docs/功能介绍/视距解锁行为合同.md','docs/设计/视距解锁实现设计.md',
        'docs/设计/可重复构建与项目骨架.md','review-materials/view-unlock-20261018/evidence.md')
    $changed=@(Invoke-WorkloadGit $Root @('diff','--name-only','--no-renames',$base,$Identity.commit,'--') | Sort-Object)
    if(@($changed | Where-Object {$_ -cnotin $allowed}).Count -or
        (@($Record.revisions.path | Sort-Object) -join '|') -cne ($changed -join '|')){throw 'View unlock revision scope mismatch.'}
    foreach($entry in $Record.revisions){if((Get-WorkloadFileHash -LiteralPath (Join-Path $Root $entry.path)).Hash -cne $entry.sha256){throw 'View unlock revision bytes changed.'}}
    $required=@('build-structure','view-ui-preferences','native-view-input-rendering','native-view-light-effects-lifecycle','view-combat-world-consumers','native-view-network','view-costs','package-entry-contract')
    if((@($Record.checks.name | Sort-Object) -join '|') -cne (@($required | Sort-Object) -join '|')){throw 'View unlock necessary scopes missing/duplicated.'}
    foreach($check in $Record.checks){
        if($check.exit -ne 0 -or $check.disposition -cnotin @('CURRENT','REVIEWED_REUSE') -or -not $check.scope -or -not $check.applicability -or @($check.artifacts).Count -eq 0){throw 'Invalid view unlock scoped evidence.'}
        foreach($artifact in $check.artifacts){$null=Read-AimStageArtifact $Root $artifact}
    }
    if(@($Record.reviews).Count -eq 0 -or @($Record.limits).Count -eq 0 -or $null -eq $Record.PSObject.Properties['failureDispositions']){throw 'View unlock independent review, failure dispositions or limits missing.'}
    foreach($failure in $Record.failureDispositions){
        if($failure.disposition -cnotin @('PRODUCT_FIXED','TEST_CORRECTED','ENVIRONMENT_LIMITATION') -or -not $failure.cause -or -not $failure.resolution -or @($failure.artifacts).Count -eq 0){throw 'Unresolved view unlock failure.'}
        foreach($artifact in $failure.artifacts){$null=Read-AimStageArtifact $Root $artifact}
    }
    foreach($review in $Record.reviews){
        if($review.result -cne 'APPROVED_FOR_STAGE' -or $review.sourceFingerprint -cne $Identity.fingerprint -or -not $review.coverage){throw 'View unlock independent review identity mismatch.'}
        $null=Read-AimStageArtifact $Root $review.artifact
    }
    if($Record.admission.result -cne 'READY_FOR_PACKAGE' -or $Record.admission.sourceFingerprint -cne $Identity.fingerprint){throw 'View unlock coordinator admission missing.'}
    $null=Read-AimStageArtifact $Root $Record.admission.artifact
    $debug=Read-WorkloadJson (Join-Path $Root 'artifacts/build/Debug/build-record.json')
    if(-not (Test-WorkloadBuildMatch $Root $debug $Identity $InputIdentity)){throw 'View unlock Debug detection build changed.'}
    $consumers=@('artifacts/build/Debug/checks/bin/NativeWorldTextProbe/x86/Debug/net472/NativeWorldTextProbe.exe')
    foreach($project in @('Platform','Features','Infrastructure','TerrariaHost','ArchitectureTests')){$consumers+=('artifacts/build/Debug/work/bin/JueMingR.'+$project+'/x86/Debug/net472/JueMingR.'+$project+'.'+$(if($project -ceq 'ArchitectureTests'){'exe'}else{'dll'}))}
    if((@($Record.detectionFiles.path | Sort-Object) -join '|') -cne (@($consumers | Sort-Object) -join '|')){throw 'View unlock detection consumer set mismatch.'}
    foreach($entry in $Record.detectionFiles){if((Get-WorkloadFileHash -LiteralPath (Join-Path $Root $entry.path)).Hash -cne $entry.sha256){throw 'View unlock detection bytes changed.'}}
    return [ordered]@{status='STAGE_DEVELOPMENT';mode='ExplicitViewUnlock20261018';fullQualification='INCOMPLETE';releaseEligible=$false;productBase=$base;recordPath=$Relative;recordSha256=(Get-WorkloadFileHash -LiteralPath $Path).Hash;necessaryScopes=$required;limits=@($Record.limits);checkCount=$required.Count}
}
