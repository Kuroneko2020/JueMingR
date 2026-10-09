# Standalone archive checks; no build, installer, game or package script execution.
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Assert-PackageMemberPath([string] $Path) {
    if ([string]::IsNullOrWhiteSpace($Path) -or $Path -match '[\\:]|^/|//|(^|/)\.\.?(/|$)' -or $Path.EndsWith('/')) {
        throw 'Unsafe package member path.'
    }
}
function Test-RecordedPackageArchive {
    param([string] $ZipPath, $Record)
    $root=[string]$Record.packageDirectory.name
    Assert-PackageMemberPath $root
    if ($root.Contains('/')) {throw 'Package must have one root.'}
    $expected=New-Object 'System.Collections.Generic.Dictionary[string,object]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $Record.packageDirectory.files) {
        Assert-PackageMemberPath ([string]$file.path)
        if ($file.sha256 -cnotmatch '^[0-9A-F]{64}$' -or $file.length -lt 0 -or $expected.ContainsKey($root+'/'+$file.path)) {throw 'Invalid or duplicate recorded member.'}
        $expected.Add($root+'/'+$file.path,$file)
    }
    if ($expected.Count -eq 0) {throw 'Empty package record.'}
    $seen=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    $archive=[IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        foreach ($entry in $archive.Entries) {
            # The existing builder writes files only; accepting directory/link
            # entries would widen its exact recorded archive contract.
            Assert-PackageMemberPath $entry.FullName
            if (-not $seen.Add($entry.FullName) -or -not $expected.ContainsKey($entry.FullName) -or
                (($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000) {throw 'Unknown, duplicate or symbolic package member.'}
            $file=$expected[$entry.FullName]
            if ($entry.FullName -cne ($root+'/'+$file.path) -or $entry.Length -ne $file.length) {throw 'Package member identity mismatch.'}
            $stream=$entry.Open(); $sha=[Security.Cryptography.SHA256]::Create()
            try { $hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') }
            finally {$sha.Dispose();$stream.Dispose()}
            if ($hash -cne $file.sha256) {throw 'Package member hash mismatch.'}
        }
        if ($seen.Count -ne $expected.Count) {throw 'Missing recorded package members.'}
        return $seen.Count
    } finally {$archive.Dispose()}
}
