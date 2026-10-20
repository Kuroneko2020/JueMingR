# Read-only explanations use the same route/projection/reuse decisions as the
# runner. This module neither launches a check nor changes qualification.
function Get-WorkloadAggregateStatus {
    param([string] $Mode,$Results,[string[]] $Required)
    if(@($Results | Where-Object {$_.result -in @('FAIL','BLOCKED')}).Count){return 'FAILED'}
    if(@($Results | Where-Object {$_.result -cne 'PASS'}).Count -or (@($Results | ForEach-Object {$_.name} | Sort-Object) -join '|') -cne (@($Required | Sort-Object) -join '|')){return 'INCOMPLETE'}
    if($Mode -ceq 'Feedback'){return 'FEEDBACK'}
    return 'PASS'
}
function Get-WorkloadRouteExplanation {
    param([string[]] $Paths)
    foreach($path in $Paths){
        $route=Get-WorkloadRoute @($path)
        [pscustomobject]@{path=$path;classification=$(if(Test-WorkloadNonExecutableInput $path){'NonExecutable'}elseif($route.unknown.Count){'Unknown'}else{'ExecutionInput'});groups=@($route.groups | Where-Object {$_ -cne 'core'});checks=@(Get-WorkloadPathChecks $path)}
    }
}
$script:WorkloadExplanationDifferenceCache=@{}
function Get-WorkloadCheckExplanation {
    param([string] $Root,$Check,$InputIdentity,$Cache)
    $old=@(if($null -ne $Cache -and $null -ne $Cache.PSObject.Properties['results']){$Cache.results | Where-Object {$_.name -ceq $Check.name}})
    $signature=Get-WorkloadHash (@($Check.executable)+@($Check.arguments))
    $reason='No original evidence';$differences=@();$reusable=$false;$duration=$null
    if($old.Count -eq 1){
        $item=$old[0];$duration=$item.milliseconds
        $differenceKey=(Get-WorkloadHash @($item.inputs))+'|'+(Get-WorkloadHash @($InputIdentity.inputs))
        if(-not $script:WorkloadExplanationDifferenceCache.ContainsKey($differenceKey)){
            $script:WorkloadExplanationDifferenceCache[$differenceKey]=@(Get-WorkloadInputDifferences $item.inputs $InputIdentity.inputs)
        }
        $differences=@($script:WorkloadExplanationDifferenceCache[$differenceKey] | Where-Object {
            $path=($_ -split '\|',2)[0]
            # A projection includes/excludes paths independently of their hash.
            # Ask the same projection only for actual deltas, not every unchanged
            # source row once per check. This has no qualification side effects.
            @(Get-WorkloadCheckInputs ([pscustomobject]@{inputs=@($path+':changed')}) $Check.name).Count -gt 0
        })
        $reusable=Test-WorkloadReusable $Root $item $InputIdentity $Check.name $signature
        $reason=if($reusable){'Applicable original evidence'}elseif($item.status -cne 'PASS'){'Original was not PASS'}elseif($item.signature -cne $signature){'Executable/argv changed'}elseif($differences.Count){'Relevant execution inputs changed'}elseif(-not (Test-WorkloadBehaviorRecipes $Root $item $InputIdentity $Check.name)){'Actual compile/launch/qualification recipe changed'}else{'Original receipt, latest attempt, outputs or live identity not applicable'}
    }
    [pscustomobject]@{name=$Check.name;reusable=$reusable;reason=$reason;changedInputs=$differences;originalMilliseconds=$duration;dependencies=@(Get-WorkloadCheckDependencies $Check.name);executable=[IO.Path]::GetFileName($Check.executable);arguments=@($Check.arguments)}
}
