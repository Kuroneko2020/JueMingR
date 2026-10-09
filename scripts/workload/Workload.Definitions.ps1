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
        'tests/NativeWorldTextProbe/NativeCombatAimChecks.cs'=@('native-CombatAimCpu','native-CombatControlCpu','native-CombatNavigationCpu','native-CombatMechanicsCpu','native-CombatEffectsCpu','native-CombatIntegrationCpu','native-CombatCosts','native-CombatAttackCosts','native-CombatImpactVisual','native-CombatAimUi','native-CombatVisual')
        'tests/NativeWorldTextProbe/NativeCombatAimUiChecks.cs'=@('native-CombatAimUi','native-CombatVisual')
        'tests/NativeWorldTextProbe/NativeCombatImpactVisualChecks.cs'=@('native-CombatImpactVisual','native-CombatVisual')
        'tests/NativeWorldTextProbe/NativeCombatIntegrationChecks.cs'=@('native-CombatIntegrationCpu')
        'tests/NativeWorldTextProbe/NativeCombatCadenceAimChecks.cs'=@('native-CombatIntegrationCpu')
        'tests/NativeWorldTextProbe/NativeCombatResourceChecks.cs'=@('native-CombatIntegrationCpu')
        'tests/NativeWorldTextProbe/NativeCombatCandidateDemandChecks.cs'=@('native-CombatIntegrationCpu')
        'tests/NativeWorldTextProbe/NativeCombatNeighborChecks.cs'=@('native-CombatIntegrationCpu')
        'tests/NativeWorldTextProbe/NativeCombatAttackCostChecks.cs'=@('native-CombatCosts','native-CombatAttackCosts','native-CombatImpactVisual','native-CombatVisual')
        'tests/NativeWorldTextProbe/NativeCombatEffectChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatProjectileEnvironmentChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatObstacleChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatAmmoChecks.cs'=@('native-CombatAimCpu')
        'tests/NativeWorldTextProbe/NativeCombatSkyChecks.cs'=@('native-CombatAimCpu')
        'tests/NativeWorldTextProbe/NativeCombatScatterChecks.cs'=@('native-CombatAimCpu')
        'tests/NativeWorldTextProbe/NativeCombatHeldSequenceChecks.cs'=@('native-CombatControlCpu')
        'tests/NativeWorldTextProbe/NativeCombatAttackFailureChecks.cs'=@('native-CombatControlCpu')
        'tests/NativeWorldTextProbe/NativeCombatControlChecks.cs'=@('native-CombatControlCpu')
        'tests/NativeWorldTextProbe/NativeCombatBeamChecks.cs'=@('native-CombatControlCpu')
        'tests/NativeWorldTextProbe/NativeCombatYoyoNavigationChecks.cs'=@('native-CombatControlCpu','native-CombatNavigationCpu')
        'tests/NativeWorldTextProbe/NativeCombatYoyoChecks.cs'=@('native-CombatControlCpu')
        'tests/NativeWorldTextProbe/NativeCombatReceiveChecks.cs'=@('native-CombatControlCpu')
        'tests/NativeWorldTextProbe/NativeCombatMeleeChecks.cs'=@('native-CombatMechanicsCpu')
        'tests/NativeWorldTextProbe/NativeCombatPhaseChecks.cs'=@('native-CombatMechanicsCpu','native-CombatControlCpu')
        'tests/NativeWorldTextProbe/NativeCombatSwingChecks.cs'=@('native-CombatMechanicsCpu')
        'tests/NativeWorldTextProbe/NativeCombatWhipChecks.cs'=@('native-CombatMechanicsCpu')
        'tests/NativeWorldTextProbe/NativeCombatReturnChecks.cs'=@('native-CombatMechanicsCpu')
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
    # CPU cumulative entries own Navigation through Control and AttackCosts
    # through CombatCosts. Their independent specialist flags remain available
    # in test-world-object-text. Impact/AimUi/Visual need matching real Content
    # and GPU: they are explicit graphical obligations, never --cpu children.
    $scopes = [ordered]@{
        'NpcRollingCpu'=@('combat-host');
        'CombatAimCpu'=@('combat-host');
        'CombatControlCpu'=@('combat-host');
        'CombatMechanicsCpu'=@('combat-host');
        'CombatEffectsCpu'=@('combat-host');
        'CombatIntegrationCpu'=@('combat-host');
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
$script:WorkloadBehaviorRecipeCache=@{}
function Get-WorkloadBehaviorRecipeProjection {
    param([string] $Path,[string] $Text)
    # Bind the complete three compile/launch owners, including control flow,
    # helpers and executable bookkeeping. Unknown executable changes require
    # current validation; this is a token boundary, not a script interpreter.
    if($Path -inotin @('scripts/build.ps1','scripts/workload/Workload.Evidence.ps1','scripts/test-workload-regressions.ps1')){throw 'Unregistered behavior recipe.'}
    $tokens=$null;$errors=$null
    $ast=[Management.Automation.Language.Parser]::ParseInput($Text,[ref]$tokens,[ref]$errors)
    if($errors.Count){throw ('Unparseable behavior recipe: '+$Path)}
    $ignored=New-Object 'System.Collections.Generic.List[object]'
    # Even literal Write-Host resets $?. Only build's appended, top-level
    # reports after its existing final successful record output are exempt;
    # there is no later executable statement to consume their status. Other
    # reporting stays bound, without trying to interpret status data flow.
    $statements=@(if($Path -ieq 'scripts/build.ps1' -and $null -ne $ast.EndBlock){$ast.EndBlock.Statements})
    for($index=$statements.Count-1;$index -ge 0;$index--){
        $pipeline=$statements[$index]
        if($pipeline -isnot [Management.Automation.Language.PipelineAst] -or $pipeline.PipelineElements.Count -ne 1){break}
        $call=$pipeline.PipelineElements[0]
        if($call -isnot [Management.Automation.Language.CommandAst] -or $call.InvocationOperator -ne [Management.Automation.Language.TokenKind]::Unknown -or $call.Redirections.Count -ne 0 -or $call.GetCommandName() -ine 'Write-Host'){break}
        $constants=@($call.CommandElements | Select-Object -Skip 1)
        if($constants.Count -eq 0 -or @($constants | Where-Object {$_ -isnot [Management.Automation.Language.StringConstantExpressionAst] -and $_ -isnot [Management.Automation.Language.ConstantExpressionAst]}).Count){break}
        $ignored.Add($pipeline.Extent)
    }
    if($index -lt 0 -or $statements[$index].Extent.Text -cne 'Write-Output ("Build record: {0}" -f $recordPath)'){$ignored.Clear()}
    $events=New-Object 'System.Collections.Generic.List[object]'
    foreach($token in $tokens){
        if($token.Kind -in @('NewLine','LineContinuation','Semi','EndOfInput')){continue}
        # #requires is lexed as a comment but constrains script execution.
        if($token.Kind -eq 'Comment' -and $token.Text -inotmatch '^#requires\b'){continue}
        $skip=$false
        foreach($range in $ignored){if($token.Extent.StartOffset -ge $range.StartOffset -and $token.Extent.EndOffset -le $range.EndOffset){$skip=$true;break}}
        if($skip){continue}
        $events.Add([pscustomobject]@{offset=$token.Extent.StartOffset;edge=1;value=$token.Kind.ToString()+':'+$token.Text})
    }
    # Newlines/semicolons can change statements without changing other tokens.
    # Keep parser statement/command boundaries while permitting mere layout.
    foreach($node in $ast.FindAll({param($child) $child -is [Management.Automation.Language.StatementAst] -or $child -is [Management.Automation.Language.CommandAst]},$true)){
        $skip=$false
        foreach($range in $ignored){if($node.Extent.StartOffset -ge $range.StartOffset -and $node.Extent.EndOffset -le $range.EndOffset){$skip=$true;break}}
        if($skip){continue}
        $kind=$node.GetType().Name
        $events.Add([pscustomobject]@{offset=$node.Extent.StartOffset;edge=0;value='begin:'+ $kind})
        $events.Add([pscustomobject]@{offset=$node.Extent.EndOffset;edge=2;value='end:'+ $kind})
    }
    return Get-WorkloadHash @($events | Sort-Object offset,edge,value | ForEach-Object {$_.value})
}
function Read-WorkloadOriginalRecipe {
    param([string] $Root,$Evidence,[string] $Path,[string] $Hash)
    $current=Join-Path $Root $Path
    if([IO.File]::Exists($current) -and (Get-WorkloadFileHash $current).Hash -ceq $Hash){return [IO.File]::ReadAllText($current)}
    if($null -eq $Evidence.PSObject.Properties['sourceCommit'] -or $Evidence.sourceCommit -cnotmatch '^[0-9a-f]{40}$'){throw 'Original recipe bytes unavailable.'}
    # Git blobs and Git's actual checkout filter are candidate byte sources.
    # Accept only the recorded SHA, never newline-normalised semantic guessing.
    foreach($mode in @('blob','--filters')) {
        $start=New-Object Diagnostics.ProcessStartInfo
        $start.FileName=(Get-Command git.exe).Source;$start.Arguments='-C "'+$Root+'" cat-file '+$mode+' '+$Evidence.sourceCommit+':'+$Path
        $start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
        $child=New-Object Diagnostics.Process;$child.StartInfo=$start;$bytes=New-Object IO.MemoryStream
        try {
            [void]$child.Start();$stderr=$child.StandardError.ReadToEndAsync();$child.StandardOutput.BaseStream.CopyTo($bytes);$child.WaitForExit();$null=$stderr.GetAwaiter().GetResult()
            if($child.ExitCode -ne 0){continue}
            $sha=[Security.Cryptography.SHA256]::Create()
            try{$actual=[BitConverter]::ToString($sha.ComputeHash($bytes.ToArray())).Replace('-','')}finally{$sha.Dispose()}
            if($actual -ceq $Hash){$bytes.Position=0;$reader=New-Object IO.StreamReader($bytes,[Text.Encoding]::UTF8,$true);try{return $reader.ReadToEnd()}finally{$reader.Dispose()}}
        } finally {$bytes.Dispose();$child.Dispose()}
    }
    throw 'Original recipe bytes do not match recorded SHA; current validation required.'
}
function Test-WorkloadBehaviorRecipes {
    param([string] $Root,$Evidence,$InputIdentity,[string] $Name)
    if($Name.StartsWith('workload-')){return $true}
    try {
        foreach($path in @('scripts/build.ps1','scripts/workload/Workload.Evidence.ps1','scripts/test-workload-regressions.ps1')){
            $prefix=$path+':'
            $old=@($Evidence.inputs|Where-Object {$_.StartsWith($prefix,[StringComparison]::Ordinal)})
            $current=@($InputIdentity.inputs|Where-Object {$_.StartsWith($prefix,[StringComparison]::Ordinal)})
            if($old.Count -eq 0 -and $current.Count -eq 0){continue}
            if($old.Count -ne 1 -or $current.Count -ne 1){return $false}
            $oldHash=$old[0].Substring($prefix.Length);$currentHash=$current[0].Substring($prefix.Length)
            # Cache only projections of exact, already verified immutable bytes.
            # Live/source identity is still re-read at the existing boundaries.
            $oldKey=$path+'|'+$oldHash;$currentKey=$path+'|'+$currentHash
            if(-not $script:WorkloadBehaviorRecipeCache.ContainsKey($oldKey)){$script:WorkloadBehaviorRecipeCache[$oldKey]=Get-WorkloadBehaviorRecipeProjection $path (Read-WorkloadOriginalRecipe $Root $Evidence $path $oldHash)}
            if(-not $script:WorkloadBehaviorRecipeCache.ContainsKey($currentKey)){$script:WorkloadBehaviorRecipeCache[$currentKey]=Get-WorkloadBehaviorRecipeProjection $path (Read-WorkloadOriginalRecipe $Root $InputIdentity $path $currentHash)}
            $before=$script:WorkloadBehaviorRecipeCache[$oldKey];$after=$script:WorkloadBehaviorRecipeCache[$currentKey]
            if($before -cne $after){return $false}
        }
        return $true
    } catch {return $false}
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
