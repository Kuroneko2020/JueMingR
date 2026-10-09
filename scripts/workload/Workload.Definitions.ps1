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
$script:WorkloadBehaviorRecipeCache=@{}
function Assert-WorkloadRecipeWrites {
    param($Ast,[string[]] $Owners)
    # This finite owner model supports direct variable assignments only. PS
    # variable identity ignores case; scoped/index/member/indirect mutation is
    # not a proof of equivalent argv and must require current validation.
    foreach($node in $Ast.FindAll({param($child) $child -is [Management.Automation.Language.AssignmentStatementAst] -or $child -is [Management.Automation.Language.UnaryExpressionAst] -or $child -is [Management.Automation.Language.InvokeMemberExpressionAst]},$true)){
        $target=if($node -is [Management.Automation.Language.AssignmentStatementAst]){$node.Left}elseif($node -is [Management.Automation.Language.InvokeMemberExpressionAst]){$node.Expression}else{$node}
        $variables=@($target.FindAll({param($child) $child -is [Management.Automation.Language.VariableExpressionAst]},$true))
        foreach($variable in $variables){
            $identity=$variable.VariablePath.UserPath
            if($Owners -inotcontains ($identity -replace '^.*:','')){continue}
            if($node -is [Management.Automation.Language.AssignmentStatementAst] -and $node.Left -is [Management.Automation.Language.VariableExpressionAst] -and $Owners -icontains $identity){continue}
            if($node -is [Management.Automation.Language.InvokeMemberExpressionAst] -and $node.Member.Extent.Text -iin @('Trim','TrimEnd','StartsWith')){continue}
            if($node -is [Management.Automation.Language.UnaryExpressionAst] -and $node.TokenKind -notin @('PlusPlus','MinusMinus','PostfixPlusPlus','PostfixMinusMinus')){continue}
            throw 'Unsupported behavior parameter mutation requires current validation.'
        }
    }
    foreach($call in $Ast.FindAll({param($child) $child -is [Management.Automation.Language.CommandAst]},$true)){
        if($call.GetCommandName() -iin @('Set-Variable','New-Variable','Remove-Variable','Clear-Variable','sv','nv','rv','cv','Invoke-Expression','iex')){throw 'Indirect behavior parameter mutation requires current validation.'}
    }
}
function Get-WorkloadBehaviorRecipeProjection {
    param([string] $Path,[string] $Text)
    # This is the existing three command owners, not an MSBuild analyser.
    # Keep parameter-producing assignments and their known fixed path sources;
    # unknown argument sources are not an equivalence proof.
    $tokens=$null;$errors=$null
    $ast=[Management.Automation.Language.Parser]::ParseInput($Text,[ref]$tokens,[ref]$errors)
    if($errors.Count){throw ('Unparseable behavior recipe: '+$Path)}
    $selected=New-Object 'System.Collections.Generic.List[object]'
    if($Path -ieq 'scripts/build.ps1') {
        $owners=@('buildArguments','solutionPath','repositoryRoot','referencesDirectory','harmonyReferencesDirectory','buildRoot','workRoot','dotnetCommand','debugRoot','debugWork')
        Assert-WorkloadRecipeWrites $ast ($owners+@('Configuration'))
        foreach($name in $owners){
            $assignments=@($ast.FindAll({param($node) $node -is [Management.Automation.Language.AssignmentStatementAst] -and $node.Left -is [Management.Automation.Language.VariableExpressionAst] -and $node.Left.VariablePath.UserPath -ieq $name},$true))
            if($assignments.Count -ne 1){throw ('Unknown compile parameter owner: '+$name)}
            foreach($call in $assignments[0].Right.FindAll({param($node) $node -is [Management.Automation.Language.CommandAst]},$true)){
                $allowedCall=if($name -ieq 'dotnetCommand'){'Get-Command'}else{'Join-Path'}
                if($call.GetCommandName() -ine $allowedCall){throw 'Unknown compile parameter helper requires current validation.'}
            }
            if($name -ine 'buildArguments'){foreach($variable in $assignments[0].Right.FindAll({param($node) $node -is [Management.Automation.Language.VariableExpressionAst]},$true)){
                if(@('PSScriptRoot','repositoryRoot','Configuration','buildRoot','debugRoot') -inotcontains $variable.VariablePath.UserPath){throw 'Unknown compile path/tool source.'}
            }}
            $selected.Add($assignments[0])
        }
        $allowed=@('solutionPath','Configuration','workRoot','referencesDirectory','harmonyReferencesDirectory','commit')
        $arguments=$selected[0]
        foreach($variable in $arguments.Right.FindAll({param($node) $node -is [Management.Automation.Language.VariableExpressionAst]},$true)){
            if($allowed -inotcontains $variable.VariablePath.UserPath){throw 'Unknown compile argument source requires current validation.'}
        }
        foreach($assignment in $ast.FindAll({param($node) $node -is [Management.Automation.Language.AssignmentStatementAst] -and $node.Left -is [Management.Automation.Language.VariableExpressionAst] -and $node.Left.VariablePath.UserPath -ieq 'Configuration'},$true)){$selected.Add($assignment)}
        $calls=@($ast.FindAll({param($node) $node -is [Management.Automation.Language.CommandAst] -and ($node.CommandElements[0].Extent.Text -ieq '$dotnetCommand.Source' -or $node.GetCommandName() -iin @('dotnet','dotnet.exe'))},$true))
        $main=0;$detection=0;$version=0
        foreach($call in $calls){
            if($call.CommandElements[0].Extent.Text -ine '$dotnetCommand.Source'){throw 'Unregistered solution compile invocation.'}
            if($call.CommandElements.Count -eq 2 -and $call.CommandElements[1].Extent.Text -ieq '@buildArguments'){$main++}
            elseif($call.CommandElements[1].Extent.Text -ieq 'build'){
                $detection++
                foreach($variable in $call.FindAll({param($node) $node -is [Management.Automation.Language.VariableExpressionAst]},$true)){
                    if(($allowed+@('dotnetCommand','debugWork')) -inotcontains $variable.VariablePath.UserPath){throw 'Unknown detection compile argument source.'}
                }
                if($call.FindAll({param($node) $node -is [Management.Automation.Language.CommandAst]},$true).Count -ne 1){throw 'Unknown detection compile argument helper.'}
            }elseif($call.CommandElements.Count -eq 2 -and $call.CommandElements[1].Extent.Text -ieq '--version'){$version++;continue}
            else{throw 'Unregistered solution compile invocation.'}
            $selected.Add($call)
        }
        if($main -ne 1 -or $detection -ne 1 -or $version -ne 1){throw 'Unknown solution/detection compile inventory.'}
    } elseif($Path -ieq 'scripts/workload/Workload.Evidence.ps1') {
        foreach($name in @('Get-WorkloadClearedEnvironment','Get-WorkloadFixtureInputFingerprint')){
            $functions=@($ast.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ieq $name},$true))
            if($functions.Count -ne 1){throw ('Unknown execution/input owner: '+$name)};$selected.Add($functions[0])
        }
        $fixture=@($ast.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ieq 'Ensure-WorkloadFixture'},$true))
        $process=@($ast.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ieq 'Invoke-WorkloadProcess'},$true))
        if($fixture.Count -ne 1 -or $process.Count -ne 1){throw 'Unknown fixture/process owner.'}
        Assert-WorkloadRecipeWrites $fixture[0] @('Root','Project','checks')
        foreach($assignment in $fixture[0].FindAll({param($node) $node -is [Management.Automation.Language.AssignmentStatementAst] -and $node.Left -is [Management.Automation.Language.VariableExpressionAst] -and $node.Left.VariablePath.UserPath -iin @('Root','Project')},$true)){
            if($assignment.Right.FindAll({param($node) $node -is [Management.Automation.Language.CommandAst] -or $node -is [Management.Automation.Language.VariableExpressionAst]},$true).Count){throw 'Unknown fixture parameter source.'}
            $selected.Add($assignment)
        }
        $calls=@($fixture[0].FindAll({param($node) $node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -ieq 'dotnet.exe'},$true))
        if($calls.Count -ne 1){throw 'Unknown fixture compile invocation.'}
        foreach($variable in $calls[0].FindAll({param($node) $node -is [Management.Automation.Language.VariableExpressionAst]},$true)){
            if(@('Root','Project','checks') -inotcontains $variable.VariablePath.UserPath){throw 'Unknown fixture compile argument source.'}
        }
        $selected.Add($calls[0])
        foreach($call in $calls[0].FindAll({param($node) $node -is [Management.Automation.Language.CommandAst]},$true)){
            if($call.GetCommandName() -inotin @('dotnet.exe','Join-Path')){throw 'Unknown fixture compile parameter helper.'}
        }
        $paths=@($fixture[0].FindAll({param($node) $node -is [Management.Automation.Language.AssignmentStatementAst] -and $node.Left.Extent.Text -ieq '$checks'},$true))
        if($paths.Count -ne 1){throw 'Unknown fixture output/path owner.'}
        foreach($call in $paths[0].Right.FindAll({param($node) $node -is [Management.Automation.Language.CommandAst]},$true)){if($call.GetCommandName() -ine 'Join-Path'){throw 'Unknown fixture path helper.'}}
        foreach($variable in $paths[0].Right.FindAll({param($node) $node -is [Management.Automation.Language.VariableExpressionAst]},$true)){if($variable.VariablePath.UserPath -ine 'Root'){throw 'Unknown fixture path source.'}}
        $selected.Add($paths[0])
        # Receipt/log writes do not change what the native process consumes.
        # The actual executable/argv, environment removal/restoration and any
        # reassignment of those inputs remain bound to the old conclusion.
        Assert-WorkloadRecipeWrites $process[0] @('Executable','Arguments','onlyVariables')
        $calls=@($process[0].FindAll({param($node) $node -is [Management.Automation.Language.CommandAst] -and $node.CommandElements[0].Extent.Text -ieq '$Executable'},$true))
        if($calls.Count -ne 1){throw 'Unknown native process invocation.'}
        foreach($variable in $calls[0].FindAll({param($node) $node -is [Management.Automation.Language.VariableExpressionAst]},$true)){
            if(@('Executable','Arguments') -inotcontains $variable.VariablePath.UserPath){throw 'Unknown native execution argument source.'}
        }
        $selected.Add($calls[0])
        foreach($node in $process[0].FindAll({param($node)
            ($node -is [Management.Automation.Language.AssignmentStatementAst] -and $node.Left.Extent.Text -imatch '^\$(Executable|Arguments|onlyVariables)(\W|$)') -or
            ($node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -iin @('Remove-Item','Set-Item'))
        },$true)){$selected.Add($node)}
        foreach($assignment in $process[0].FindAll({param($node) $node -is [Management.Automation.Language.AssignmentStatementAst] -and $node.Left.Extent.Text -imatch '^\$(Executable|Arguments|onlyVariables)(\W|$)'},$true)){
            foreach($call in $assignment.Right.FindAll({param($node) $node -is [Management.Automation.Language.CommandAst]},$true)){if($call.GetCommandName() -ine 'Get-WorkloadClearedEnvironment'){throw 'Unknown process parameter helper.'}}
            foreach($variable in $assignment.Right.FindAll({param($node) $node -is [Management.Automation.Language.VariableExpressionAst]},$true)){if(@('Executable','Arguments') -inotcontains $variable.VariablePath.UserPath){throw 'Unknown process parameter source.'}}
        }
    } elseif($Path -ieq 'scripts/test-workload-regressions.ps1') {
        $calls=@($ast.FindAll({param($node) $node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -ieq 'Invoke-WorkloadProcess'},$true))
        if($calls.Count -ne 1){throw 'Unknown workload process dispatcher.'};$selected.Add($calls[0])
        foreach($variable in $calls[0].FindAll({param($node) $node -is [Management.Automation.Language.VariableExpressionAst]},$true)){
            if(@('check','attempt') -inotcontains $variable.VariablePath.UserPath){throw 'Unknown dispatch argument source.'}
        }
        $functions=@($ast.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ieq 'Ensure-Fixture'},$true))
        if($functions.Count -ne 1){throw 'Unknown fixture dispatcher.'};$selected.Add($functions[0])
        foreach($node in $ast.FindAll({param($node) $node -is [Management.Automation.Language.AssignmentStatementAst] -and $node.Left.Extent.Text -imatch '^\$check(\.(arguments|executable|project))?(\W|$)'},$true)){
            if($node.Right.FindAll({param($child) $child -is [Management.Automation.Language.CommandAst]},$true).Count){throw 'Unknown dispatch parameter helper.'}
            foreach($variable in $node.Right.FindAll({param($child) $child -is [Management.Automation.Language.VariableExpressionAst]},$true)){if($variable.VariablePath.UserPath -ine 'check'){throw 'Unknown dispatch parameter source.'}}
            $selected.Add($node)
        }
    } else {throw 'Unregistered behavior recipe.'}
    # Explicit environment writes anywhere in these scripts are consumers too.
    foreach($node in $ast.FindAll({param($node)
        ($node -is [Management.Automation.Language.AssignmentStatementAst] -and $node.Left.Extent.Text -match '^\$env:') -or
        ($node -is [Management.Automation.Language.InvokeMemberExpressionAst] -and $node.Member.Extent.Text -ieq 'SetEnvironmentVariable')
    },$true)){$selected.Add($node)}
    $rows=@($selected|ForEach-Object {
        $start=$_.Extent.StartOffset;$end=$_.Extent.EndOffset
        @($tokens|Where-Object {$_.Extent.StartOffset -ge $start -and $_.Extent.EndOffset -le $end -and $_.Kind -notin @('Comment','NewLine','LineContinuation','EndOfInput')}|ForEach-Object {$_.Kind.ToString()+':'+$_.Text}) -join '|'
    })
    return Get-WorkloadHash $rows
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
