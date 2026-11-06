function Read-WorkloadNativeDispatch {
    $path=Join-Path $PSScriptRoot '../../eng/Workload.NativeDispatch.tsv'
    $selectors=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    $leaves=@{}
    foreach($line in [IO.File]::ReadAllLines($path)){
        if(!$line -or $line.StartsWith('#')){continue};$parts=$line.Split("`t")
        if($parts.Count -eq 2 -and $parts[0] -ceq 'selector' -and $parts[1] -cmatch '^JUEMINGR_[A-Z0-9_]+$'){
            if(!$selectors.Add($parts[1])){throw 'Duplicate native selector contract.'}
        }elseif($parts.Count -eq 3 -and $parts[0] -ceq 'leaf' -and $parts[1] -cmatch '^Npc[A-Za-z0-9]+$' -and $parts[2] -cmatch '^[a-z0-9-]+$'){
            if($leaves.ContainsKey($parts[1])){throw 'Duplicate native scope contract.'};$leaves[$parts[1]]=$parts[2]
        }else{throw 'Malformed native dispatch contract.'}
    }
    if(!$selectors.Count -or !$leaves.Count){throw 'Empty native dispatch contract.'}
    return @{selectors=$selectors;leaves=$leaves}
}
function Get-WorkloadExpectedNativeLeaf {
    param([string]$Name)
    if(!$Name.StartsWith('native-',[StringComparison]::Ordinal)){return $null}
    return (Read-WorkloadNativeDispatch).leaves[$Name.Substring(7)]
}
