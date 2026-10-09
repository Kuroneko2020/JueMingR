# One maintained definition supplies both selection and evidence projection.
# Unknown/shared inputs stay conservative; compile identity is separate.
function Get-WorkloadLeafChecks {
    $leaves = @{
        'scripts/verify-existing-package.ps1'=@('workload-PackageVerification')
        'scripts/phase0s/PackageVerification.Support.ps1'=@('workload-PackageVerification')
        'tests/Phase0S/Invoke-PackageVerificationChecks.ps1'=@('workload-PackageVerification')
        'tests/NativeWorldTextProbe/NativePageCompositionChecks.cs'=@('native-PageCompositionCpu')
        'tests/NativeWorldTextProbe/NativeBackgroundAutomationChecks.cs'=@('native-BackgroundCpu','native-F5AutomationCpu')
        'tests/NativeWorldTextProbe/NativeToolCadenceChecks.cs'=@('native-ToolsCadence')
        'tests/NativeWorldTextProbe/NativeToolsWorkloadChecks.cs'=@('native-ToolsWorkload')
        'tests/NativeWorldTextProbe/NativeYoyoAdoptionChecks.cs'=@('native-CombatCpu','native-CombatYoyoCausal')
        'tests/NativeWorldTextProbe/NativeCombatFoundationChecks.cs'=@('native-NpcFoundationRules','native-NpcFoundationContinuous','native-NpcStrategyContinuous')
        'tests/NativeWorldTextProbe/NativeCombatFoundationContinuousChecks.cs'=@('native-NpcFoundationContinuous','native-NpcStrategyContinuous','native-NpcFiniteFlight')
        'tests/NativeWorldTextProbe/NativeCombatStrategyChecks.cs'=@('native-NpcStrategy','native-NpcStrategy-rollchoice')
        'tests/NativeWorldTextProbe/NativeCombatFamilyControlChecks.cs'=@('native-NpcStrategy')
        'tests/NativeWorldTextProbe/NativeCombatFiniteControlChecks.cs'=@('native-NpcStrategy')
        'tests/NativeWorldTextProbe/NativeCombatRetargetChecks.cs'=@('native-NpcStrategy')
        'tests/NativeWorldTextProbe/NativeCombatRollingControlChecks.cs'=@('native-NpcStrategy','native-NpcStrategy-rollchoice')
        'tests/NativeWorldTextProbe/NativeNpcLifetimeChecks.cs'=@('native-NpcStrategy','native-NpcStrategy-rollchoice')
        'tests/NativeWorldTextProbe/NativeOuterInputBoundaryChecks.cs'=@('native-InputBoundary')
        'tests/NativeWorldTextProbe/NativeCombatRunningControlChecks.cs'=@('native-NpcStrategy')
        'tests/NativeWorldTextProbe/NativeCombatStructuralControlChecks.cs'=@('native-NpcStrategy')
        'tests/NativeWorldTextProbe/NativeCombatFighterControlChecks.cs'=@('native-NpcStrategy')
        'tests/NativeWorldTextProbe/NativeCombatPositionControlChecks.cs'=@('native-NpcStrategy')
        'tests/NativeWorldTextProbe/NativeCombatFlyingTailChecks.cs'=@('native-NpcStrategy')
        'tests/NativeWorldTextProbe/NativeCombatEventRetirementChecks.cs'=@('native-NpcEventRetirementCpu','native-NpcEventRetirement')
        'tests/NativeWorldTextProbe/NativeDisplayResponsibilityChecks.cs'=@('native-NpcDisplayIsolation')
        'tests/NativeWorldTextProbe/NativeCombatCloseoutTerrainChecks.cs'=@('native-NpcCloseoutTerrain')
        'tests/Workload/Invoke-WorkloadRoutingChecks.ps1'=@('workload-Routing')
        'tests/Workload/Invoke-WorkloadEvidenceChecks.ps1'=@('workload-Evidence')
    }
    return $leaves
}
function Get-WorkloadNativeScopes {
    $scopes = [ordered]@{
        'NpcRollingCpu'=@('combat-host');
        'NpcRollingSelectionNegative'=@('combat-host');
        'NpcBasicMotion'=@('combat-host');
        'NpcFoundationRules'=@('combat-host'); 'NpcFoundationContinuous'=@('combat-host'); 'NpcPlayerPolicy'=@('combat-host'); 'NpcStrategy'=@('combat-host'); 'NpcStrategyContinuous'=@('combat-host'); 'NpcEventRetirementCpu'=@('combat-host');
        # Ordinary delivery retains the real shared selection/terrain and
        # marker consumer seams; detailed phase matrices stay bounded probes.
        'NpcSync'=@('combat-host'); 'NpcLocalFailure'=@('combat-host'); 'NpcDisplayIsolation'=@('combat-host'); 'NpcCloseoutTerrain'=@('combat-host');
        'NpcSharedGeometry'=@('combat-host'); 'NpcTargetMarker'=@('combat-host');
        'NpcSamplePresentation'=@('combat-host'); 'NpcFiniteFlight'=@('combat-host'); 'CombatYoyoCausal'=@('combat-host');
        'NpcWorkerIntegration'=@('legacy-worker'); 'NpcSnapshot'=@('legacy-worker'); 'NpcWorkerPreparation'=@('legacy-worker');
        'NpcDiagnosticsOff'=@('legacy-worker'); 'NpcPostDelivery'=@('legacy-worker'); 'NpcGuardianQuery'=@('legacy-worker'); 'NpcModeledImpact'=@('legacy-worker');
        'NpcLegalCoverage'=@('legacy-worker'); 'NpcWorkerTransport'=@('legacy-worker'); 'NpcMenuPreparation'=@('legacy-worker'); 'NpcSessionCapacity'=@('legacy-worker'); 'NpcProduction'=@('legacy-worker'); 'NpcLongCoverage'=@('legacy-worker');
        'WorkloadCpu'=@('world-host','shared-host'); 'InformationCpu'=@('information','shared-host'); 'GuidanceCpu'=@('guidance','shared-host');
        'ShortFeedbackCpu'=@('shared-host','storage-host','quick-items-host','coin-deposit-host','recovery-host','processing-host','about-host','tools-host','fishing-host','combat-host');
        'CombatCpu'=@('combat-host'); 'CombatFacingCpu'=@('combat-host'); 'CombatHitsCpu'=@('combat-host'); 'CombatReportCpu'=@('combat-host'); 'CombatUiCpu'=@('combat-host'); 'CombatObservationCpu'=@('combat-host'); 'CombatCosts'=@('combat-host');
        'InputBoundary'=@('input-boundary','shared-host');
        'ToolsCpu'=@('tools-host'); 'ToolsCadence'=@('tools-host'); 'ToolsExecutionCpu'=@('tools-host'); 'ToolsWorkload'=@('tools-host');
        'PageCompositionCpu'=@('pages-host'); 'FishingCpu'=@('fishing-host'); 'BackgroundCpu'=@('fishing-host','shared-host'); 'F5AutomationCpu'=@('fishing-host','shared-host');
        'AboutCpu'=@('about-host'); 'BrowserCpu'=@('browser-host'); 'QuickItemsCpu'=@('quick-items-host'); 'CoinDepositCpu'=@('coin-deposit-host');
        'RecoveryCpu'=@('recovery-host'); 'ProcessingCpu'=@('processing-host'); 'DeathCpu'=@('death-host'); 'FootprintsCpu'=@('footprints-host'); 'ExplorationCpu'=@('map-host')
    }
    return $scopes
}
function Get-WorkloadCheckGroups {
    param([string] $Name)
    if ($Name.StartsWith('native-')) {
        $scope=$Name.Substring(7)
        if ($scope -ceq 'NpcStrategy-rollchoice') {return @('combat-host')}
        if ($scope -ceq 'NpcPrivateSafety') {return @('legacy-worker')}
        $scopes=Get-WorkloadNativeScopes
        if ($scopes.Contains($scope)) {return $scopes[$scope]}
    }
    if ($script:WorkloadCheckGroups -and $script:WorkloadCheckGroups.ContainsKey($Name)) {return $script:WorkloadCheckGroups[$Name]}
    return @('*')
}
function Get-WorkloadPathChecks {
    param([string] $Path)
    $leaves=Get-WorkloadLeafChecks
    if ($leaves.ContainsKey($Path)) {return $leaves[$Path]}
    return @()
}
$script:WorkloadCheckGroups=@{}
function Get-WorkloadAdditionalCheck {
    param([string] $Root,[string] $Name)
    # Existing explicit strategy sub-scope remains available for a task's
    # reviewed delta proof; its registration is not a universal obligation.
    if ($Name -cne 'native-NpcStrategy-rollchoice') {throw ('Unknown additional check: '+$Name)}
    $checks=Join-Path $Root 'artifacts/build/Debug/checks'
    return [pscustomobject]@{name=$Name;executable=(Join-Path $checks 'bin/NativeWorldTextProbe/x86/Debug/net472/NativeWorldTextProbe.exe');arguments=@($Root,'--cpu',(Join-Path $checks 'NpcStrategy-rollchoice'),'NpcStrategy:rollchoice');project='NativeWorldTextProbe'}
}
function Get-WorkloadQualificationPolicy {
    param([string] $Profile,[string] $Name,[string[]] $Differences)
    # Reviewed task delta proof is explicit. It cannot become a universal
    # strategy obligation, or authorize unrelated changed-input conclusions.
    if($Profile -ceq 'issue112-lifetime-delta-20261008' -and $Name -ceq 'native-NpcStrategy'){
        # These exact assertion revisions were covered by the reviewed lifetime
        # sub-scope. A pathname/profile label alone cannot excuse new assertions
        # or changed production/dispatch/compile/environment inputs.
        $allowed=@(
            'tests/NativeWorldTextProbe/NativeCombatRollingControlChecks.cs|85DEEE19205C8243CE39DDE9E187468820BFB98E537719B38A77E9BE8D90C828|16ECAAD59329232199E93D157D1E63473F4455331F72C614AF4EC0C51C130EE0',
            'tests/NativeWorldTextProbe/NativeNpcLifetimeChecks.cs|<absent>|5E15817C5FD2E9FC1ADB3BFC6587CE6BE0FF173FE59C84A81B54283111DE069E')
        if(@($Differences).Count -eq 0 -or @($Differences|Where-Object {$allowed -cnotcontains $_}).Count){return $null}
        return [pscustomobject]@{requiresCurrent=@('native-NpcStrategy-rollchoice','native-NpcStrategyContinuous','native-NpcRollingCpu','native-NpcRollingSelectionNegative')}
    }
    return $null
}
function Get-WorkloadLegacyCheckFingerprint {
    param($InputIdentity, [string] $Name)
    # Only reviewed leaf assertion files may be omitted from another CPU check.
    # NativeChecks/CheckCatalog, shared helpers, projects, all production and all
    # recipes remain common inputs. New consumers must update this map in the
    # same change (the dispatcher change itself invalidates all old evidence).
    # Page composition is also used by ToolsVisual, outside this CPU cache.
    $leaves=Get-WorkloadLeafChecks
    # Immutable pre-DT projection compatibility: this helper used to omit the
    # vertical assertion shared by the continuous scopes. This does not grant
    # reuse across that input changing; current projection still binds it.
    $leaves['tests/NativeWorldTextProbe/NativeCombatFoundationChecks.cs']=@('native-NpcFoundationRules')
    $leaves['tests/NativeWorldTextProbe/NativeCombatFoundationContinuousChecks.cs']=@('native-NpcFoundationContinuous','native-NpcStrategyContinuous')
    $rows = @($InputIdentity.inputs | Where-Object {
        $path = ($_ -split ':',2)[0]
        -not $leaves.ContainsKey($path) -or $leaves[$path] -contains $Name
    })
    return Get-WorkloadHash $rows
}
