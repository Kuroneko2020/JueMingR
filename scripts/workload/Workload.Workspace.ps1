# Explicit owner-approved, non-executable assets only. Never means clean.
function Read-WorkloadRetainedAssets {
    param([string] $Root,[string] $Path,[switch] $RequireCommitted)
    if (-not $Path) {return $null}
    $value=Read-WorkloadJson $Path
    if ($null -eq $value -or $value.schema -cne 'retained-workspace-assets-1' -or -not $value.authorization -or @($value.files).Count -eq 0) {throw 'Invalid retained workspace declaration.'}
    $base=[IO.Path]::GetFullPath($Root).TrimEnd('\')+'\'
    $seen=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    $files=@(foreach($entry in $value.files){
        $relative=[string]$entry.path
        # Excluding production/tests/recipes/dependencies would hide actual
        # inputs. This facility accepts documents and root reference notes.
        if (-not $seen.Add($relative) -or $relative -match '(^|/)\.\.(/|$)|[\\:]|^/' -or
            $relative -notmatch '^(docs/.*\.md|\.workbuddy/memory/[^/]+\.md|[^/]+\.(txt|json))$' -or $relative -ceq 'global.json') {throw ('Unsafe retained asset: '+$relative)}
        $full=[IO.Path]::GetFullPath((Join-Path $Root $relative))
        if(-not $full.StartsWith($base,[StringComparison]::OrdinalIgnoreCase)){throw 'Retained asset escaped root.'}
        $item=Get-Item -LiteralPath $full -Force -ErrorAction Stop
        for($ancestor=$item;$null -ne $ancestor -and $ancestor.FullName.Length -ge $base.Length-1;$ancestor=$(if($ancestor -is [IO.FileInfo]){$ancestor.Directory}else{$ancestor.Parent})){
            if($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Reparse retained asset.'}
        }
        $tracked=@(Invoke-WorkloadGit $Root @('ls-files','--',$relative)).Count -gt 0
        if($item.PSIsContainer -or $tracked -ne $entry.tracked -or $item.Length -ne $entry.length -or (Get-WorkloadFileHash -LiteralPath $full).Hash -cne $entry.sha256){throw ('Retained asset changed: '+$relative)}
        [pscustomobject]@{path=$relative;tracked=$tracked;length=$item.Length;sha256=$entry.sha256}
    })
    $changes=Get-WorkloadChanges $Root 'HEAD'
    if ($RequireCommitted -and @($changes.paths | Where-Object {-not $seen.Contains($_)}).Count) {throw 'Restricted candidate has undeclared workspace changes.'}
    return [pscustomobject]@{kind='restricted-development';clean=$false;workspaceDirty=$true;authorization=$value.authorization;declaration=[IO.Path]::GetFullPath($Path);declarationSha256=(Get-WorkloadFileHash -LiteralPath $Path).Hash;files=$files;releaseEligible=$false}
}
function Initialize-WorkloadWorkspace {
    param([string] $Root,[string] $Path,[switch] $RequireCommitted)
    $script:WorkloadRetainedAssets=Read-WorkloadRetainedAssets $Root $Path -RequireCommitted:$RequireCommitted
    return $script:WorkloadRetainedAssets
}
$script:WorkloadRetainedAssets=$null
function Test-WorkloadHistoricalPackageWorkspace {
    param($Record,$Retained)
    # Consume the existing authorized four-asset receipt without changing its
    # source/recipe identity or treating it as a clean/release candidate.
    try {
        if($Record.clean -ne $false -or $Record.workspaceDirty -ne $true -or $Record.committedProductSourceClean -ne $true -or
            $Record.admission.kind -cne 'issue112-four-retained-assets-once' -or $Record.admission.sourceCommit -cne $Record.sourceCommit){return $false}
        $original=@($Record.admission.retainedAssets | Sort-Object path)
        $current=@($Retained.files | Sort-Object path)
        if($original.Count -ne $current.Count){return $false}
        for($i=0;$i -lt $original.Count;$i++){
            if($original[$i].path -cne $current[$i].path -or $original[$i].sha256 -cne $current[$i].sha256 -or
                $original[$i].bytes -ne $current[$i].length -or $original[$i].tracked -ne $current[$i].tracked -or $original[$i].regular -ne $true){return $false}
        }
        return $true
    }catch{return $false}
}

function Test-WorkloadRestrictedRecord {
    param($Record,$Retained)
    try {
        return $null -ne $Retained -and $Record.clean -eq $false -and $Record.workspace.kind -ceq 'restricted-development' -and
            $Record.workspace.releaseEligible -eq $false -and
            ($Record.workspace | ConvertTo-Json -Depth 8 -Compress) -ceq ($Retained | ConvertTo-Json -Depth 8 -Compress)
    }catch{return $false}
}
