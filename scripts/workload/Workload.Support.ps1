# Shared by the existing build and its thin CPU runner. No build/test recursion.
function Invoke-WorkloadGit {
    param([string] $Root, [string[]] $Arguments)
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = (Get-Command git.exe -ErrorAction Stop).Source
    # Direct Windows argv quoting; stderr warnings are not filenames, and UTF-8
    # paths must not depend on the Windows PowerShell console code page.
    $quoted = foreach ($argument in (@('-C', $Root, '-c', 'core.quotepath=false') + $Arguments)) {
        '"' + [regex]::Replace([regex]::Replace($argument, '(\\*)"', '$1$1\"'), '(\\+)$', '$1$1') + '"'
    }
    $start.Arguments = $quoted -join ' '; $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $start.RedirectStandardError = $true
    $start.StandardOutputEncoding = $start.StandardErrorEncoding = [Text.Encoding]::UTF8
    $process = New-Object Diagnostics.Process; $process.StartInfo = $start
    try {
        [void]$process.Start(); $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit(); $result = $stdout.GetAwaiter().GetResult(); $errorText = $stderr.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) { throw ('Git input query failed: ' + $errorText) }
        # Explicit string[] keeps Framework and modern .NET Split overload binding identical.
        return $result.Split([string[]]@("`r`n", "`n"), [StringSplitOptions]::RemoveEmptyEntries)
    } finally { $process.Dispose() }
}
function Get-WorkloadIdentity {
    param([string] $Root)
    $paths = @(Invoke-WorkloadGit $Root @('ls-files', '--cached', '--others', '--exclude-standard') | Sort-Object -Unique)
    if ($paths.Count -eq 0) { throw 'No source inputs.' }
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $rows = foreach ($path in $paths) {
            $file = Join-Path $Root $path
            $hash = if ([IO.File]::Exists($file)) { (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash } else { 'MISSING' }
            [string]$path + ':' + $hash
        }
        $bytes = [Text.Encoding]::UTF8.GetBytes(($rows -join "`n"))
        return [ordered]@{ commit = [string](Invoke-WorkloadGit $Root @('rev-parse', 'HEAD'));
            fingerprint = [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', ''); inputCount = $paths.Count }
    } finally { $sha.Dispose() }
}
function Get-WorkloadChanges {
    param([string] $Root, [string] $Baseline)
    $resolved = $null; $reason = $null
    try {
        if ([string]::IsNullOrWhiteSpace($Baseline)) { $Baseline = [string](Invoke-WorkloadGit $Root @('merge-base', 'HEAD', 'origin/main')) }
        $resolved = [string](Invoke-WorkloadGit $Root @('rev-parse', '--verify', ($Baseline + '^{commit}')))
    } catch { $reason = 'No usable comparison baseline; classify task risks before delivery.' }
    $paths = @()
    if ($resolved) { $paths += @(Invoke-WorkloadGit $Root @('diff', '--name-only', '--no-renames', $resolved, 'HEAD', '--')) }
    # Separate index/worktree diffs retain changes that cancel each other in net HEAD diff.
    $paths += @(Invoke-WorkloadGit $Root @('diff', '--name-only', '--no-renames', '--cached', '--'))
    $paths += @(Invoke-WorkloadGit $Root @('diff', '--name-only', '--no-renames', '--'))
    $paths += @(Invoke-WorkloadGit $Root @('ls-files', '--others', '--exclude-standard'))
    return [ordered]@{ baseline = $resolved; paths = @($paths | Sort-Object -Unique); reason = $reason }
}
function Get-WorkloadRoute {
    param([string[]] $Paths)
    $groups = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    [void]$groups.Add('core')
    $unknown = @()
    foreach ($path in $Paths) {
        switch -Regex ($path.Replace('\', '/')) {
            '^docs/|^AGENTS\.md$|^README(?:\.[^/]+)?$|^LICENSE$|^THIRD-PARTY-NOTICES\.md$' { continue }
            '^src/[^/]+/(QuickItems|KeepFavorited)/|^tests/JueMingR.ArchitectureTests/(QuickItems|KeepFavorited)/|^tests/NativeWorldTextProbe/Native(Quick|Favorite)' { [void]$groups.Add('quick-items-host'); continue }
            '^src/[^/]+/(ItemCatalog|ItemBrowser|ChestLocator|Announcements)/|^tests/(ItemBrowser|ChestLocator|Announcements)/' { [void]$groups.Add('browser-host'); continue }
            # BrowserPresentation directly shares these input adapters, but not
            # the rest of Notes UI. Preserve the narrow ordinary Notes route.
            '^src/JueMingR.TerrariaHost/Notes/Notes(Clipboard|Input)\.cs$' { [void]$groups.Add('notes-host'); [void]$groups.Add('browser-host'); continue }
            '^src/[^/]+/Notes/|^tests/Notes/' { [void]$groups.Add('notes-host'); continue }
            '^src/[^/]+/Text/' { [void]$groups.Add('notes-host'); [void]$groups.Add('map-host'); [void]$groups.Add('browser-host'); continue }
            '^src/[^/]+/Footprints/|^tests/Footprints/' { [void]$groups.Add('footprints-host'); [void]$groups.Add('map-host'); [void]$groups.Add('storage-host'); continue }
            '^src/[^/]+/(MapMarkers|Exploration)/|^src/JueMingR.TerrariaHost/Map/|^tests/MapMarkers/' { [void]$groups.Add('map-host'); [void]$groups.Add('death-host'); continue }
            '^src/JueMingR.TerrariaHost/EntityLabels/(Style|Hex)|^tests/EntityLabels/EntityStyle|^tests/WorldTargets/WorldTargetStyle' { [void]$groups.Add('style-host'); continue }
            '^src/[^/]+/WorldObjectText/Opened|^tests/JueMingR.ArchitectureTests/WorldObjectText/Opened' { [void]$groups.Add('records'); continue }
            '^src/JueMingR.Infrastructure/Storage/|^src/JueMingR.Platform/(Persistence|Settings)/' { [void]$groups.Add('storage-host'); continue }
            '^src/[^/]+/(DeathHistory|WorldTime)/|^tests/DeathHistory/' { [void]$groups.Add('death-host'); continue }
            '^src/[^/]+/Information/' { [void]$groups.Add('shared-host'); [void]$groups.Add('style-host'); [void]$groups.Add('storage-host'); continue }
            '^src/[^/]+/(Guidance|Npcs)/' { [void]$groups.Add('shared-host'); [void]$groups.Add('storage-host'); continue }
            '^src/JueMingR.TerrariaHost/(F5|Input)/|^src/JueMingR.TerrariaHost/Phase0|^src/JueMingR.Platform/Runtime/' { [void]$groups.Add('shared-host'); continue }
            '^src/[^/]+/(WorldObjectText|WorldTargets|World|Rendering)/|^tests/(WorldObjectText|WorldTargets)/' { [void]$groups.Add('world-host'); continue }
            '^src/[^/]+/(EntityLabels|Hotkeys|Items|Settings|Biomes)/|^tests/(EntityLabels|Hotkeys|Items|Phase0U|Phase0V)/' { [void]$groups.Add('shared-host'); continue }
            '^scripts/|^tests/|^eng/|^src/.*\.(csproj|props|targets)$|^Directory\.Build\.|^global\.json$|^JueMingR\.sln$|^NuGet\.Config$|^\.github/' { [void]$groups.Add('shared-host'); [void]$groups.Add('storage-host'); continue }
            default { $unknown += $path }
        }
    }
    if ($groups.Contains('shared-host') -or $groups.Contains('storage-host')) { [void]$groups.Add('death-host'); [void]$groups.Add('map-host') }
    if ($groups.Contains('shared-host') -or $groups.Contains('storage-host')) { [void]$groups.Add('quick-items-host') }
    if ($groups.Contains('map-host') -or $groups.Contains('shared-host') -or $groups.Contains('storage-host')) { [void]$groups.Add('footprints-host') }
    # Browser text editing and chest/target resolution consume these shared
    # paths even when no ItemBrowser file itself changed in the current diff.
    if ($groups.Contains('shared-host') -or $groups.Contains('storage-host') -or $groups.Contains('world-host')) { [void]$groups.Add('browser-host') }
    return [ordered]@{ groups = @($groups | Sort-Object); unknown = $unknown; slowGraphics = $false }
}
function Test-WorkloadBuildMatch {
    param([string] $Root, $Record, $Identity)
    if ($null -eq $Record -or $null -eq $Record.PSObject.Properties['sourceFingerprint'] -or
        $Record.commit -cne $Identity.commit -or $Record.sourceFingerprint -cne $Identity.fingerprint -or
        $Record.configuration -cne 'Debug' -or $Record.sdk -cne '10.0.203') { return $false }
    foreach ($output in $Record.outputs) {
        $path = Join-Path (Join-Path $Root 'artifacts/build/Debug/work') $output.path
        if (-not [IO.File]::Exists($path) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $output.sha256) { return $false }
    }
    return @($Record.outputs).Count -gt 0
}
