param([string]$Root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))
$ErrorActionPreference='Stop'
# This joins the fixed ContentSamples census to explicit source routes. It
# does not run AI or turn a predicate/representative into per-species PASS.
$census=Import-Csv (Join-Path $PSScriptRoot 'npc-route-inventory.csv')
$fallback=@{};foreach($r in (Import-Csv (Join-Path $PSScriptRoot 'npc-receiving-fallback-routes.csv'))){$fallback[$r.netId]=$r}
$nonreceiving=@{};foreach($r in (Import-Csv (Join-Path $PSScriptRoot 'npc-default-nonreceiving-routes.csv'))){$nonreceiving[$r.netId]=$r}
$nativeFiles=@(Get-ChildItem -LiteralPath (Join-Path $Root 'tests/NativeWorldTextProbe') -Filter '*Checks*.cs' -File)
$architectureFiles=@(Get-ChildItem -LiteralPath (Join-Path $Root 'tests/JueMingR.ArchitectureTests') -Filter '*Checks*.cs' -File)
$testText=@{};foreach($file in @($nativeFiles)+@($architectureFiles)){$testText[$file.FullName.Substring($Root.Length+1).Replace('\','/')]=[IO.File]::ReadAllText($file.FullName)}
$rows=foreach($n in $census){
    $known=[string]$n.knownMotorFamilies
    $route='NpcMotion.Step ordered gates: '+$known
    $scope='Current full sample -> Rolling.Prepare/Step -> immutable Cache publication -> current selected consumer'
    $boundary='Known predicate is static dispatch only; finite state/phase/target/terrain/player assumptions and native stop boundaries still apply'
    $evidence=New-Object 'System.Collections.Generic.List[string]'
    if($fallback.ContainsKey($n.netId)){
        $f=$fallback[$n.netId];$route=$f.dispatch;$evidence.Add($f.directEvidence);$boundary=$f.claimBoundary
    } elseif(-not $known){
        $route=switch([int]$n.style){
            1 {'NpcMotion.Slime; NpcWindySlimePrelude before ordinary jumps'}
            6 {'NpcMotion Style6 -> NpcWormMotion head/link and existing terrain-qualified approximate seek'}
            37 {'NpcMotion Style37 -> NpcWormMotion Destroyer head/link/anchor'}
            17 {'NpcMotion.Vulture type61/301 phase0 gravity -> flight'}
            39 {'NpcRollingMotion.TortoiseType -> Tortoise'}
            40 {'NpcWallMotion.Wall; unsupported structural types retain explicit stop'}
            41 {'NpcRollingMotion.Hopper -> Derpling'}
            69 {'NpcMotion.Duke finite phases and random transition boundary'}
            70 {'NpcPositionObservation/PositionRelation -> NpcPositionMotion'}
            71 {'NpcPositionObservation/PositionRelation -> NpcPositionMotion'}
            92 {'NpcPositionObservation/PositionRelation -> NpcPositionMotion'}
            default {'NpcMotion ordered gate; if no known structural gate, existing Rolling.Trend with native gravity/collision/health'}
        }
        $evidence.Add('NativeCombatPredictionChecks; BasicNpcMotionChecks; NativeCombatFoundationChecks (representative applicable branches, not every netId)')
        if($nonreceiving.ContainsKey($n.netId)){$boundary=$nonreceiving[$n.netId].claimBoundary+'; retain native current receiving filter and reclassify actual changed phase before use'}
    }
    foreach($family in ($known -split ';' | Where-Object {$_})){
        foreach($path in $testText.Keys){if($testText[$path].Contains($family)){$evidence.Add($path)}}
    }
    [pscustomobject][ordered]@{netId=$n.netId;type=$n.type;style=$n.style;defaultReceives=$n.defaultReceives;structuredPredicate=$n.structured;actualRoute=$route;sourceAndConsumer=$scope;testPointers=(@($evidence | Sort-Object -Unique)-join ';');claimBoundary=$boundary;runtimeVerdict='MECHANISM_EVIDENCE_REQUIRED_NOT_A_PER_NETID_PASS'}
}
if(@($rows).Count -ne 762 -or @($rows.netId | Sort-Object -Unique).Count -ne 762){throw 'NPC census membership changed'}
if($fallback.Count -ne 277 -or $nonreceiving.Count -ne 67){throw 'Explicit fallback/default-filter partitions changed'}
$rows | Export-Csv -LiteralPath (Join-Path $PSScriptRoot 'npc-mechanism-map.csv') -NoTypeInformation -Encoding UTF8
[pscustomobject]@{netIds=@($rows).Count;receivingUnstructured=$fallback.Count;defaultNonreceivingUnstructured=$nonreceiving.Count;structured=@($census | Where-Object {$_.structured -eq 'True'}).Count}
