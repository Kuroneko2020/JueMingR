# Process-local, explicitly bounded verification phase. Read leases prevent
# replacement while parsed/hash results are shared; no timestamp trust/cache.
$script:WorkloadReadWindow=$null
$script:WorkloadProjectionCache=@{}
$script:WorkloadPathGroupCache=@{}
function Start-WorkloadReadWindow {
    Stop-WorkloadReadWindow
    $script:WorkloadReadWindow=@{}
}
function Stop-WorkloadReadWindow {
    if($null -ne $script:WorkloadReadWindow){foreach($entry in $script:WorkloadReadWindow.Values){$entry.stream.Dispose()}}
    $script:WorkloadReadWindow=$null
}
function Release-WorkloadReadPath {
    param([string] $Path)
    $full=[IO.Path]::GetFullPath($Path)
    if($null -ne $script:WorkloadReadWindow -and $script:WorkloadReadWindow.ContainsKey($full)){
        $script:WorkloadReadWindow[$full].stream.Dispose();$script:WorkloadReadWindow.Remove($full)
    }
}
function Get-WorkloadReadEntry {
    param([string] $Path)
    $full=[IO.Path]::GetFullPath($Path)
    if($null -eq $script:WorkloadReadWindow){throw 'Read entry requires an active verification window.'}
    if(-not $script:WorkloadReadWindow.ContainsKey($full)){
        $stream=[IO.File]::Open($full,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
        $sha=[Security.Cryptography.SHA256]::Create()
        try{$hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}catch{$stream.Dispose();throw}finally{$sha.Dispose()}
        $script:WorkloadReadWindow[$full]=@{stream=$stream;hash=$hash;parsed=$false;json=$null}
    }
    return $script:WorkloadReadWindow[$full]
}
function Get-WorkloadFileHash {
    param([string] $LiteralPath,[string] $Algorithm='SHA256')
    if($null -eq $script:WorkloadReadWindow){return Get-FileHash -LiteralPath $LiteralPath -Algorithm $Algorithm}
    if($Algorithm -cne 'SHA256'){throw 'Unsupported verification digest.'}
    return [pscustomobject]@{Hash=(Get-WorkloadReadEntry $LiteralPath).hash}
}
