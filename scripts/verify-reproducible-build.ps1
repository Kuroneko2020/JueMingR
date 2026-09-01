[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [string] $TerrariaInstallDirectory,
    [string] $XnaReferenceDirectory,
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$script:RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$script:Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$script:StrictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)
$script:GitPath = $null
$script:SourceGitMetadataRoot = $null

if ($null -eq ('JueMingR.PhysicalPathNativeMethods' -as [type])) {
    $assemblyName = New-Object System.Reflection.AssemblyName('JueMingR.PhysicalPath.Dynamic')
    if ($PSVersionTable.PSEdition -eq 'Core') {
        $assemblyBuilder = [System.Reflection.Emit.AssemblyBuilder]::DefineDynamicAssembly(
            $assemblyName,
            [System.Reflection.Emit.AssemblyBuilderAccess]::Run)
    }
    else {
        $assemblyBuilder = [AppDomain]::CurrentDomain.DefineDynamicAssembly(
            $assemblyName,
            [System.Reflection.Emit.AssemblyBuilderAccess]::Run)
    }
    $moduleBuilder = $assemblyBuilder.DefineDynamicModule('JueMingR.PhysicalPath.Dynamic')
    $typeBuilder = $moduleBuilder.DefineType(
        'JueMingR.PhysicalPathNativeMethods',
        [System.Reflection.TypeAttributes] 'Public, Abstract, Sealed')
    $methodAttributes = [System.Reflection.MethodAttributes] 'Public, Static, PinvokeImpl'
    $createFileMethod = $typeBuilder.DefinePInvokeMethod(
        'CreateFile',
        'kernel32.dll',
        'CreateFileW',
        $methodAttributes,
        [System.Reflection.CallingConventions]::Standard,
        [Microsoft.Win32.SafeHandles.SafeFileHandle],
        [Type[]] @([string], [uint32], [uint32], [IntPtr], [uint32], [uint32], [IntPtr]),
        [System.Runtime.InteropServices.CallingConvention]::Winapi,
        [System.Runtime.InteropServices.CharSet]::Unicode)
    $createFileMethod.SetImplementationFlags(
        $createFileMethod.GetMethodImplementationFlags() -bor [System.Reflection.MethodImplAttributes]::PreserveSig)
    $getFinalPathMethod = $typeBuilder.DefinePInvokeMethod(
        'GetFinalPathNameByHandle',
        'kernel32.dll',
        'GetFinalPathNameByHandleW',
        $methodAttributes,
        [System.Reflection.CallingConventions]::Standard,
        [uint32],
        [Type[]] @([Microsoft.Win32.SafeHandles.SafeFileHandle], [System.Text.StringBuilder], [uint32], [uint32]),
        [System.Runtime.InteropServices.CallingConvention]::Winapi,
        [System.Runtime.InteropServices.CharSet]::Unicode)
    $getFinalPathMethod.SetImplementationFlags(
        $getFinalPathMethod.GetMethodImplementationFlags() -bor [System.Reflection.MethodImplAttributes]::PreserveSig)
    $setEnvironmentVariableMethod = $typeBuilder.DefinePInvokeMethod(
        'SetEnvironmentVariable',
        'kernel32.dll',
        'SetEnvironmentVariableW',
        $methodAttributes,
        [System.Reflection.CallingConventions]::Standard,
        [bool],
        [Type[]] @([string], [string]),
        [System.Runtime.InteropServices.CallingConvention]::Winapi,
        [System.Runtime.InteropServices.CharSet]::Unicode)
    $setEnvironmentVariableMethod.SetImplementationFlags(
        $setEnvironmentVariableMethod.GetMethodImplementationFlags() -bor [System.Reflection.MethodImplAttributes]::PreserveSig)
    $null = $typeBuilder.CreateType()
}

function Get-FinalDirectoryPath {
    param([string] $Path)

    $handle = [JueMingR.PhysicalPathNativeMethods]::CreateFile(
        $Path,
        0,
        0x00000007,
        [IntPtr]::Zero,
        3,
        0x02000000,
        [IntPtr]::Zero)
    if ($null -eq $handle -or $handle.IsInvalid) {
        if ($null -ne $handle) { $handle.Dispose() }
        throw 'Could not open a directory handle for physical path resolution.'
    }

    try {
        $buffer = New-Object System.Text.StringBuilder(512)
        $length = [JueMingR.PhysicalPathNativeMethods]::GetFinalPathNameByHandle(
            $handle,
            $buffer,
            [uint32] $buffer.Capacity,
            0)
        if ($length -eq 0) {
            throw 'Could not resolve a directory handle to its physical path.'
        }
        if ($length -ge $buffer.Capacity) {
            $buffer = New-Object System.Text.StringBuilder([int] $length + 1)
            $length = [JueMingR.PhysicalPathNativeMethods]::GetFinalPathNameByHandle(
                $handle,
                $buffer,
                [uint32] $buffer.Capacity,
                0)
            if ($length -eq 0 -or $length -ge $buffer.Capacity) {
                throw 'Could not resolve a directory handle to its complete physical path.'
            }
        }
        return $buffer.ToString()
    }
    finally {
        $handle.Dispose()
    }
}

function Resolve-UnresolvedPath {
    param([string] $Path)

    return [System.IO.Path]::GetFullPath(
        $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path))
}

function Get-CanonicalDirectoryPath {
    param(
        [string] $Path,
        [string] $Label
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "$Label must be a non-empty directory path."
    }
    if ($Path.StartsWith('\\?\', [StringComparison]::Ordinal) -or
        $Path.StartsWith('\\.\', [StringComparison]::Ordinal)) {
        throw "$Label may not use an extended or device path prefix."
    }

    try {
        $fullPath = Resolve-UnresolvedPath -Path $Path
    }
    catch {
        throw "$Label could not be canonicalized as a directory path."
    }
    $current = $fullPath
    $missingSegments = New-Object System.Collections.Generic.List[string]
    while (-not [System.IO.Directory]::Exists($current)) {
        if ([System.IO.File]::Exists($current)) {
            throw "$Label resolves to a file, not a directory."
        }
        $trimmedCurrent = $current.TrimEnd([char[]] @(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar))
        $leaf = [System.IO.Path]::GetFileName($trimmedCurrent)
        $parent = [System.IO.Path]::GetDirectoryName($trimmedCurrent)
        if ([string]::IsNullOrWhiteSpace($leaf) -or [string]::IsNullOrWhiteSpace($parent) -or
            [string]::Equals($parent, $current, [StringComparison]::OrdinalIgnoreCase)) {
            throw "$Label has no resolvable existing directory ancestor."
        }
        $missingSegments.Insert(0, $leaf)
        $current = $parent
    }

    try {
        $physicalPath = Get-FinalDirectoryPath -Path $current
    }
    catch {
        throw "$Label could not be resolved to a physical directory path."
    }
    if ($physicalPath.StartsWith('\\?\UNC\', [StringComparison]::OrdinalIgnoreCase)) {
        $physicalPath = '\\' + $physicalPath.Substring(8)
    }
    elseif ($physicalPath.StartsWith('\\?\', [StringComparison]::OrdinalIgnoreCase)) {
        $physicalPath = $physicalPath.Substring(4)
    }
    else {
        throw "$Label resolved to an unsupported physical path form."
    }
    foreach ($segment in $missingSegments) {
        $physicalPath = [System.IO.Path]::Combine($physicalPath, $segment)
    }
    $physicalPath = [System.IO.Path]::GetFullPath($physicalPath)
    $root = [System.IO.Path]::GetPathRoot($physicalPath)
    if ([string]::Equals($physicalPath, $root, [StringComparison]::OrdinalIgnoreCase)) {
        return $physicalPath
    }
    return $physicalPath.TrimEnd([char[]] @(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar))
}

function Test-PathWithinOrEqual {
    param(
        [string] $Candidate,
        [string] $Parent
    )

    if ([string]::Equals($Candidate, $Parent, [StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }
    $parentPrefix = $Parent.TrimEnd([char[]] @(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)) + [System.IO.Path]::DirectorySeparatorChar
    return $Candidate.StartsWith($parentPrefix, [StringComparison]::OrdinalIgnoreCase)
}

function Assert-PathTreesDisjoint {
    param(
        [string] $Candidate,
        [string] $CandidateLabel,
        [string] $Protected,
        [string] $ProtectedLabel
    )

    if ((Test-PathWithinOrEqual -Candidate $Candidate -Parent $Protected) -or
        (Test-PathWithinOrEqual -Candidate $Protected -Parent $Candidate)) {
        throw "$CandidateLabel and $ProtectedLabel must be disjoint directory trees."
    }
}

function Read-StrictUtf8Json {
    param([string] $Path)

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $offset = if ($bytes.Length -ge 3 -and
        $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) { 3 } else { 0 }
    $text = $script:StrictUtf8.GetString($bytes, $offset, $bytes.Length - $offset)
    return ConvertFrom-Json -InputObject $text
}

function Get-NormalizedTextFileSha256 {
    param([string] $Path)

    $strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)
    $text = [System.IO.File]::ReadAllText($Path, $strictUtf8)
    $normalized = $text.Replace("`r`n", "`n").Replace("`r", "`n")
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($normalized)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha256.ComputeHash($bytes))).Replace('-', '')
    }
    finally {
        $sha256.Dispose()
    }
}

function Get-TextSha256 {
    param([string] $Text)

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
        return ([BitConverter]::ToString($sha256.ComputeHash($bytes))).Replace('-', '')
    }
    finally {
        $sha256.Dispose()
    }
}

function Get-BaselineEntry {
    param(
        [object] $Baseline,
        [string] $LogicalName
    )

    $matches = @($Baseline.files | Where-Object { $_.logicalName -eq $LogicalName })
    if ($matches.Count -ne 1) {
        throw "Reference baseline must contain exactly one $LogicalName entry."
    }
    return $matches[0]
}

function Assert-SourceFileMatchesBaseline {
    param(
        [string] $Path,
        [object] $Expected,
        [string] $Label
    )

    if (-not [System.IO.File]::Exists($Path)) {
        throw "$Label is missing from the resolved legal source."
    }
    $actualHash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($actualHash -ne [string] $Expected.sha256) {
        throw "$Label does not match the approved reference baseline."
    }
}

function Get-ReproducibilitySources {
    param(
        [string] $ExplicitTerrariaDirectory,
        [string] $ExplicitXnaDirectory,
        [object] $Baseline,
        [string] $BaselineHash
    )

    $hasTerrariaParameter = -not [string]::IsNullOrWhiteSpace($ExplicitTerrariaDirectory)
    $hasXnaParameter = -not [string]::IsNullOrWhiteSpace($ExplicitXnaDirectory)
    if ($hasTerrariaParameter -ne $hasXnaParameter) {
        throw 'Specify both TerrariaInstallDirectory and XnaReferenceDirectory, or omit both and use the verified local reference marker.'
    }

    if ($hasTerrariaParameter) {
        $terrariaSource = Get-CanonicalDirectoryPath -Path $ExplicitTerrariaDirectory -Label 'TerrariaInstallDirectory'
        $xnaSource = Get-CanonicalDirectoryPath -Path $ExplicitXnaDirectory -Label 'XnaReferenceDirectory'
    }
    else {
        $markerPath = Join-Path $script:RepositoryRoot 'external\TerrariaRefs\.juemingr-reference-set.json'
        if (-not [System.IO.File]::Exists($markerPath)) {
            throw 'Reproducibility verification requires both explicit legal source directories or an existing verified local reference marker.'
        }
        try {
            $marker = Read-StrictUtf8Json -Path $markerPath
        }
        catch {
            throw 'The local reference marker is not valid JSON.'
        }
        if ($marker.schemaVersion -ne 1 -or
            $marker.generator -ne 'scripts/prepare-terraria-references.ps1' -or
            $marker.profileId -ne $Baseline.profileId -or
            $marker.baselineSha256 -ne $BaselineHash -or
            $marker.sourceHashesUnchanged -ne $true -or
            $null -eq $marker.source) {
            throw 'The local reference marker cannot establish the approved reproducibility source identity.'
        }
        $terrariaSource = Get-CanonicalDirectoryPath `
            -Path ([string] $marker.source.terrariaInstallDirectory) `
            -Label 'marker Terraria source directory'
        $xnaSource = Get-CanonicalDirectoryPath `
            -Path ([string] $marker.source.xnaReferenceDirectory) `
            -Label 'marker XNA source directory'
    }

    return [ordered]@{
        terraria = $terrariaSource
        xna = $xnaSource
    }
}

function Get-ValidatedGitPath {
    $commands = @(Get-Command 'git.exe' -CommandType Application -ErrorAction Stop)
    if ($commands.Count -eq 0) {
        throw 'Git executable is unavailable.'
    }
    $candidate = [System.IO.Path]::GetFullPath([string] $commands[0].Source)
    $item = Get-Item -LiteralPath $candidate -Force
    if (-not ($item -is [System.IO.FileInfo]) -or
        -not [string]::Equals($item.Name, 'git.exe', [StringComparison]::OrdinalIgnoreCase) -or
        ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'Git must resolve to a regular, non-reparse git.exe file.'
    }
    $directory = Get-CanonicalDirectoryPath -Path $item.DirectoryName -Label 'Git executable directory'
    return Join-Path $directory $item.Name
}

function Assert-FormalRepositoryEntriesAreRegular {
    foreach ($fileName in @(
        '.editorconfig', '.gitattributes', '.globalconfig', 'Directory.Build.props', 'Directory.Build.targets',
        'Directory.Packages.props', 'global.json', 'JueMingR.sln', 'NuGet.config', 'MSBuild.rsp', 'Directory.Build.rsp')) {
        $candidate = Join-Path $script:RepositoryRoot $fileName
        $item = Get-Item -LiteralPath $candidate -Force -ErrorAction SilentlyContinue
        if ($null -ne $item -and
            (-not ($item -is [System.IO.FileInfo]) -or
             ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0)) {
            throw "Formal root input must be a regular non-reparse file: $fileName"
        }
    }
    foreach ($relativeRoot in @('eng', 'scripts', 'src', 'tests')) {
        $absoluteRoot = Join-Path $script:RepositoryRoot $relativeRoot
        $rootItem = Get-Item -LiteralPath $absoluteRoot -Force -ErrorAction Stop
        if (-not ($rootItem -is [System.IO.DirectoryInfo]) -or
            ($rootItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Formal repository root must be a regular non-reparse directory: $relativeRoot"
        }
        $directories = New-Object System.Collections.Generic.Stack[string]
        $directories.Push($absoluteRoot)
        while ($directories.Count -ne 0) {
            $current = $directories.Pop()
            foreach ($entry in (New-Object System.IO.DirectoryInfo -ArgumentList $current).EnumerateFileSystemInfos()) {
                if (($entry.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                    throw "Formal repository input may not be a reparse point: $($entry.FullName)"
                }
                if ($entry -is [System.IO.DirectoryInfo] -and
                    $entry.Name -ine 'bin' -and $entry.Name -ine 'obj') {
                    $directories.Push($entry.FullName)
                }
            }
        }
    }
}

$script:RepositoryRoot = Get-CanonicalDirectoryPath -Path $script:RepositoryRoot -Label 'repository root'
Assert-FormalRepositoryEntriesAreRegular
$legacyCandidate = Join-Path ([System.IO.Path]::GetDirectoryName($script:RepositoryRoot)) 'JueMingZ'
if (-not [System.IO.Directory]::Exists($legacyCandidate)) {
    throw 'The required sibling Legacy repository ../JueMingZ is missing; reproducibility verification cannot continue.'
}
$script:LegacyRoot = Get-CanonicalDirectoryPath -Path $legacyCandidate -Label 'read-only Legacy root'
Assert-PathTreesDisjoint -Candidate $script:RepositoryRoot -CandidateLabel 'repository root' -Protected $script:LegacyRoot -ProtectedLabel 'read-only Legacy root'
$script:GitPath = Get-ValidatedGitPath
$baselinePath = Join-Path $script:RepositoryRoot 'eng\TerrariaReferences.baseline.json'
$baseline = Read-StrictUtf8Json -Path $baselinePath
$baselineHash = Get-NormalizedTextFileSha256 -Path $baselinePath
$resolvedSources = Get-ReproducibilitySources `
    -ExplicitTerrariaDirectory $TerrariaInstallDirectory `
    -ExplicitXnaDirectory $XnaReferenceDirectory `
    -Baseline $baseline `
    -BaselineHash $baselineHash
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $script:RepositoryRoot 'artifacts\reproducibility'
}
$approvedSummaryRoot = Get-CanonicalDirectoryPath `
    -Path (Join-Path $script:RepositoryRoot 'artifacts\reproducibility') `
    -Label 'approved reproducibility summary root'
$outputRoot = Get-CanonicalDirectoryPath -Path $OutputDirectory -Label 'OutputDirectory'
if (-not (Test-PathWithinOrEqual -Candidate $outputRoot -Parent $approvedSummaryRoot)) {
    throw 'OutputDirectory must remain inside the repository artifacts/reproducibility root.'
}
$summaryRoot = Get-CanonicalDirectoryPath -Path (Join-Path $outputRoot $Configuration) -Label 'reproducibility summary directory'
$systemTempRoot = Get-CanonicalDirectoryPath -Path ([System.IO.Path]::GetTempPath()) -Label 'system TEMP root'
Assert-PathTreesDisjoint -Candidate $outputRoot -CandidateLabel 'OutputDirectory' -Protected $script:LegacyRoot -ProtectedLabel 'read-only Legacy root'
Assert-PathTreesDisjoint -Candidate $summaryRoot -CandidateLabel 'reproducibility summary directory' -Protected $script:LegacyRoot -ProtectedLabel 'read-only Legacy root'
Assert-PathTreesDisjoint -Candidate $outputRoot -CandidateLabel 'OutputDirectory' -Protected $resolvedSources.terraria -ProtectedLabel 'Terraria source directory'
Assert-PathTreesDisjoint -Candidate $script:RepositoryRoot -CandidateLabel 'repository root' -Protected $resolvedSources.terraria -ProtectedLabel 'Terraria source directory'
Assert-PathTreesDisjoint -Candidate $systemTempRoot -CandidateLabel 'system TEMP root' -Protected $resolvedSources.terraria -ProtectedLabel 'Terraria source directory'
Assert-PathTreesDisjoint -Candidate $outputRoot -CandidateLabel 'OutputDirectory' -Protected $resolvedSources.xna -ProtectedLabel 'XNA source directory'
Assert-PathTreesDisjoint -Candidate $script:RepositoryRoot -CandidateLabel 'repository root' -Protected $resolvedSources.xna -ProtectedLabel 'XNA source directory'
Assert-PathTreesDisjoint -Candidate $systemTempRoot -CandidateLabel 'system TEMP root' -Protected $resolvedSources.xna -ProtectedLabel 'XNA source directory'
Assert-PathTreesDisjoint -Candidate $resolvedSources.terraria -CandidateLabel 'Terraria source directory' -Protected $script:LegacyRoot -ProtectedLabel 'read-only Legacy root'
Assert-PathTreesDisjoint -Candidate $resolvedSources.xna -CandidateLabel 'XNA source directory' -Protected $script:LegacyRoot -ProtectedLabel 'read-only Legacy root'
Assert-SourceFileMatchesBaseline `
    -Path (Join-Path $resolvedSources.terraria 'Terraria.exe') `
    -Expected (Get-BaselineEntry -Baseline $baseline -LogicalName 'Terraria.exe') `
    -Label 'Terraria.exe'
Assert-SourceFileMatchesBaseline `
    -Path (Join-Path $resolvedSources.xna 'Microsoft.Xna.Framework.Game.dll') `
    -Expected (Get-BaselineEntry -Baseline $baseline -LogicalName 'Microsoft.Xna.Framework.Game.dll') `
    -Label 'Microsoft.Xna.Framework.Game.dll'

function Invoke-External {
    param(
        [string] $FilePath,
        [string[]] $Arguments,
        [string] $FailureMessage
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage (exit $LASTEXITCODE)."
    }
}

function Get-UnboundGitDiscoveryLine {
    param(
        [string] $RepositoryRoot,
        [string[]] $Arguments,
        [string] $Label
    )

    $output = @(& $script:GitPath --no-pager --no-optional-locks --no-replace-objects -C $RepositoryRoot @Arguments 2>&1)
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0 -or $output.Count -ne 1 -or [string]::IsNullOrWhiteSpace([string] $output[0])) {
        throw "Could not discover the physical Git $Label binding."
    }
    return ([string] $output[0]).TrimEnd([char[]] @("`r", "`n"))
}

function Get-PhysicalGitBinding {
    param([string] $ExpectedWorkTree)

    $insideWorkTree = Get-UnboundGitDiscoveryLine -RepositoryRoot $ExpectedWorkTree -Arguments @('rev-parse', '--is-inside-work-tree') -Label 'work tree state'
    $isBare = Get-UnboundGitDiscoveryLine -RepositoryRoot $ExpectedWorkTree -Arguments @('rev-parse', '--is-bare-repository') -Label 'bare repository state'
    if ($insideWorkTree -cne 'true' -or $isBare -cne 'false') {
        throw 'The reproducibility source must be a non-bare Git work tree.'
    }

    $reportedWorkTree = Get-UnboundGitDiscoveryLine -RepositoryRoot $ExpectedWorkTree -Arguments @('rev-parse', '--path-format=absolute', '--show-toplevel') -Label 'work tree root'
    $physicalWorkTree = Get-CanonicalDirectoryPath -Path $reportedWorkTree -Label 'Git reported work tree root'
    if (-not [string]::Equals($physicalWorkTree, $ExpectedWorkTree, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Git is not bound to the physical reproducibility source root; local core.worktree or equivalent redirection is forbidden.'
    }

    $reportedGitDirectory = Get-UnboundGitDiscoveryLine -RepositoryRoot $ExpectedWorkTree -Arguments @('rev-parse', '--absolute-git-dir') -Label 'per-worktree metadata directory'
    $reportedCommonDirectory = Get-UnboundGitDiscoveryLine -RepositoryRoot $ExpectedWorkTree -Arguments @('rev-parse', '--path-format=absolute', '--git-common-dir') -Label 'common metadata directory'
    $gitDirectory = Get-CanonicalDirectoryPath -Path $reportedGitDirectory -Label 'Git per-worktree metadata directory'
    $commonDirectory = Get-CanonicalDirectoryPath -Path $reportedCommonDirectory -Label 'Git common metadata directory'

    $dotGitItem = Get-Item -LiteralPath (Join-Path $ExpectedWorkTree '.git') -Force -ErrorAction Stop
    if ((-not ($dotGitItem -is [System.IO.FileInfo]) -and -not ($dotGitItem -is [System.IO.DirectoryInfo])) -or
        ($dotGitItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'The reproducibility source .git entry must be a regular file or directory and may not be a reparse point.'
    }

    return [pscustomobject]@{
        workTree = $physicalWorkTree
        gitDirectory = $gitDirectory
        commonDirectory = $commonDirectory
        dotGitKind = $(if ($dotGitItem -is [System.IO.FileInfo]) { 'file' } else { 'directory' })
    }
}

function Assert-PhysicalGitBindingUnchanged {
    param([object] $Expected)

    $actual = Get-PhysicalGitBinding -ExpectedWorkTree $script:RepositoryRoot
    foreach ($name in @('workTree', 'gitDirectory', 'commonDirectory', 'dotGitKind')) {
        if (-not [string]::Equals([string] $actual.$name, [string] $Expected.$name, [StringComparison]::OrdinalIgnoreCase)) {
            throw "The reproducibility source Git binding changed during verification: $name."
        }
    }
}

function Get-BoundSourceGitLines {
    param([string[]] $Arguments)

    if ([string]::IsNullOrWhiteSpace($script:SourceGitMetadataRoot)) {
        throw 'The reproducibility source Git metadata directory has not been bound.'
    }
    $output = & $script:GitPath `
        --no-pager `
        --no-optional-locks `
        --no-replace-objects `
        --literal-pathspecs `
        -C $script:RepositoryRoot `
        "--git-dir=$script:SourceGitMetadataRoot" `
        "--work-tree=$script:RepositoryRoot" `
        -c core.safecrlf=false `
        @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git command failed: git $($Arguments -join ' ')"
    }
    return @($output)
}

function Get-GitRecordedSourcePaths {
    $paths = New-Object System.Collections.Generic.List[string]
    foreach ($relativePath in @(
        @(Get-BoundSourceGitLines -Arguments @('-c', 'core.quotePath=false', 'ls-files')) +
        @(Get-BoundSourceGitLines -Arguments @('-c', 'core.quotePath=false', 'ls-files', '--others', '--exclude-standard')) |
        Sort-Object -Unique)) {
        $normalized = ([string] $relativePath).Replace('\', '/')
        if ([string]::IsNullOrWhiteSpace($normalized) -or
            [System.IO.Path]::IsPathRooted($normalized) -or
            $normalized -eq '..' -or
            $normalized.StartsWith('../', [StringComparison]::Ordinal) -or
            $normalized.Contains('/../')) {
            throw "Git returned an unsafe recorded source path: $relativePath"
        }
        $paths.Add($normalized)
    }
    return @($paths.ToArray())
}

function Get-RepositoryRelativeFilePath {
    param([string] $Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $prefix = $script:RepositoryRoot.TrimEnd('\') + '\'
    if (-not $fullPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Formal source inventory path escaped the repository: $Path"
    }
    return $fullPath.Substring($prefix.Length).Replace('\', '/')
}

function Assert-NoIgnoredFormalInputFiles {
    param([string[]] $RecordedSourcePaths)

    $known = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($relativePath in $RecordedSourcePaths) { $null = $known.Add(([string] $relativePath).Replace('\', '/')) }
    $physicalFiles = New-Object System.Collections.Generic.List[System.IO.FileInfo]
    foreach ($rootFileName in @(
        '.editorconfig', '.gitattributes', '.globalconfig', 'Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props',
        'global.json', 'JueMingR.sln', 'NuGet.config', 'MSBuild.rsp', 'Directory.Build.rsp')) {
        $candidate = Join-Path $script:RepositoryRoot $rootFileName
        if ([System.IO.File]::Exists($candidate)) { $physicalFiles.Add((Get-Item -LiteralPath $candidate -Force)) }
    }
    foreach ($relativeRoot in @('eng', 'src', 'tests')) {
        $absoluteRoot = Join-Path $script:RepositoryRoot $relativeRoot
        if (-not [System.IO.Directory]::Exists($absoluteRoot)) { throw "Formal source root is missing: $relativeRoot" }
        $directories = New-Object System.Collections.Generic.Stack[string]
        $directories.Push($absoluteRoot)
        while ($directories.Count -ne 0) {
            $current = $directories.Pop()
            foreach ($entry in (New-Object System.IO.DirectoryInfo -ArgumentList $current).EnumerateFileSystemInfos()) {
                if (($entry.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                    throw "Formal source inventory may not contain a reparse point: $($entry.FullName)"
                }
                if ($entry -is [System.IO.DirectoryInfo]) {
                    if ($entry.Name -ine 'bin' -and $entry.Name -ine 'obj') { $directories.Push($entry.FullName) }
                }
                elseif ($entry -is [System.IO.FileInfo]) { $physicalFiles.Add($entry) }
            }
        }
    }
    foreach ($file in $physicalFiles) {
        if (($file.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Formal source inventory may not contain a reparse file: $($file.FullName)"
        }
        $relativePath = Get-RepositoryRelativeFilePath -Path $file.FullName
        if (-not $known.Contains($relativePath)) {
            throw "Formal source file is ignored or otherwise absent from the recorded Git source inventory: $relativePath"
        }
    }
}

function Assert-GitIndexAndAttributesSafe {
    param([string[]] $RecordedSourcePaths)

    foreach ($line in @(Get-BoundSourceGitLines -Arguments @('-c', 'core.quotePath=false', 'ls-files', '-v'))) {
        if (-not ([string] $line).StartsWith('H ', [StringComparison]::Ordinal)) {
            throw "Git index assume-unchanged, skip-worktree, or other non-normal tracking mode is forbidden: $line"
        }
    }
    foreach ($relativePath in $RecordedSourcePaths) {
        $attributeLines = @(Get-BoundSourceGitLines -Arguments @(
            '-c', 'core.quotePath=false',
            'check-attr', 'filter', 'ident', 'working-tree-encoding', '--', $relativePath))
        if ($attributeLines.Count -ne 3) {
            throw "Could not establish the Git content-transform attributes for: $relativePath"
        }
        foreach ($attributeName in @('filter', 'ident', 'working-tree-encoding')) {
            $expected = "${relativePath}: ${attributeName}: unspecified"
            if (@($attributeLines | Where-Object { [string] $_ -ceq $expected }).Count -ne 1) {
                throw "Git content-transform attribute '$attributeName' is forbidden for recorded source: $relativePath"
            }
        }
    }
}

function Get-BoundRawSourceContentInventory {
    param(
        [string[]] $RecordedSourcePaths,
        [string[]] $TrackedSourcePaths,
        [string] $Commit
    )

    $formalRootFiles = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($fileName in @(
        '.editorconfig', '.gitattributes', '.globalconfig', 'Directory.Build.props', 'Directory.Build.targets',
        'Directory.Packages.props', 'global.json', 'JueMingR.sln', 'NuGet.config', 'MSBuild.rsp', 'Directory.Build.rsp')) {
        $null = $formalRootFiles.Add($fileName)
    }
    $isRawBuildInputPath = {
        param([string] $RelativePath)
        $normalized = $RelativePath.Replace('\', '/')
        return $formalRootFiles.Contains($normalized) -or
            $normalized.StartsWith('eng/', [StringComparison]::OrdinalIgnoreCase) -or
            $normalized.StartsWith('src/', [StringComparison]::OrdinalIgnoreCase) -or
            $normalized.StartsWith('tests/', [StringComparison]::OrdinalIgnoreCase)
    }

    $tracked = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    foreach ($relativePath in $TrackedSourcePaths) {
        if (-not $tracked.Add(([string] $relativePath).Replace('\', '/'))) {
            throw "The tracked source inventory contains a duplicate path: $relativePath"
        }
    }
    $orderedPaths = [string[]] @($RecordedSourcePaths | ForEach-Object { ([string] $_).Replace('\', '/') })
    [Array]::Sort($orderedPaths, [StringComparer]::Ordinal)
    $identityParts = New-Object System.Collections.Generic.List[string]
    $trackedBytesMatch = $true
    $hasUntracked = $false
    foreach ($relativePath in $orderedPaths) {
        $absolutePath = Join-Path $script:RepositoryRoot $relativePath.Replace('/', '\')
        $isTracked = $tracked.Contains($relativePath)
        $contentMode = if (& $isRawBuildInputPath $relativePath) {
            'raw'
        }
        elseif ($relativePath.EndsWith('.md', [StringComparison]::OrdinalIgnoreCase) -or
            $relativePath.EndsWith('.ps1', [StringComparison]::OrdinalIgnoreCase) -or
            $relativePath -ieq '.gitignore') {
            'normalizedTextLf'
        }
        else {
            'raw'
        }
        $present = [System.IO.File]::Exists($absolutePath)
        if ($present -and
            ([System.IO.File]::GetAttributes($absolutePath) -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "The raw reproducibility source inventory may not read a reparse file: $relativePath"
        }

        $mode = if ($isTracked) { '' } else { 'untracked' }
        if ($isTracked) {
            $indexLines = @(Get-BoundSourceGitLines -Arguments @('-c', 'core.quotePath=false', 'ls-files', '--stage', '--', $relativePath))
            if ($indexLines.Count -ne 1 -or
                [string] $indexLines[0] -notmatch '^([0-9]{6}) ((?:[0-9a-fA-F]{40}|[0-9a-fA-F]{64})) 0\t') {
                throw "Tracked reproducibility source must have exactly one ordinary stage-0 index entry: $relativePath"
            }
            $mode = $Matches[1]
            $indexObjectId = $Matches[2].ToLowerInvariant()
            if ($mode -cne '100644' -and $mode -cne '100755') {
                throw "Tracked reproducibility source has a forbidden Git mode: $relativePath ($mode)"
            }
            $commitLines = @(Get-BoundSourceGitLines -Arguments @('-c', 'core.quotePath=false', 'ls-tree', $Commit, '--', $relativePath))
            $commitObjectId = ''
            $commitMode = ''
            if ($commitLines.Count -eq 1 -and
                [string] $commitLines[0] -match '^([0-9]{6}) blob ((?:[0-9a-fA-F]{40}|[0-9a-fA-F]{64}))\t') {
                $commitMode = $Matches[1]
                $commitObjectId = $Matches[2].ToLowerInvariant()
            }
            $rawObjectId = ''
            if ($present) {
                $rawObjectArguments = if ($contentMode -ceq 'raw') {
                    @('hash-object', '--no-filters', '--', $relativePath)
                }
                else {
                    @('-c', 'core.autocrlf=true', '-c', 'core.eol=lf', 'hash-object', '--', $relativePath)
                }
                $rawObjectLines = @(Get-BoundSourceGitLines -Arguments $rawObjectArguments)
                if ($rawObjectLines.Count -ne 1 -or [string] $rawObjectLines[0] -notmatch '^(?:[0-9a-fA-F]{40}|[0-9a-fA-F]{64})$') {
                    throw "Could not hash the raw reproducibility source bytes for: $relativePath"
                }
                $rawObjectId = ([string] $rawObjectLines[0]).ToLowerInvariant()
            }
            if (-not $present -or
                [string]::IsNullOrEmpty($commitObjectId) -or
                $mode -cne $commitMode -or
                $rawObjectId -cne $indexObjectId -or
                $rawObjectId -cne $commitObjectId) {
                $trackedBytesMatch = $false
            }
        }
        else {
            $hasUntracked = $true
        }

        $length = 0
        $sha256 = ''
        if ($present) {
            if ($contentMode -ceq 'normalizedTextLf') {
                $normalizedText = [System.IO.File]::ReadAllText($absolutePath).Replace("`r`n", "`n").Replace("`r", "`n")
                $length = $script:Utf8NoBom.GetByteCount($normalizedText)
                $sha256 = Get-TextSha256 -Text $normalizedText
            }
            else {
                $length = (Get-Item -LiteralPath $absolutePath -Force).Length
                $sha256 = (Get-FileHash -LiteralPath $absolutePath -Algorithm SHA256).Hash.ToUpperInvariant()
            }
        }
        $identityParts.Add(('{0}|{1}|{2}|{3}|{4}|{5}|{6}' -f `
            $relativePath,
            $(if ($isTracked) { 'tracked' } else { 'untracked' }),
            $mode,
            $contentMode,
            $(if ($present) { 'present' } else { 'missing' }),
            [int64] $length,
            $sha256))
    }
    if ($tracked.Count -ne @($orderedPaths | Where-Object { $tracked.Contains($_) }).Count) {
        throw 'The reproducibility source inventory does not contain every tracked path.'
    }
    $identityText = $identityParts -join "`n"
    return [pscustomobject]@{
        identityText = $identityText
        contentSetSha256 = Get-TextSha256 -Text $identityText
        trackedBytesMatchIndexAndCommit = $trackedBytesMatch
        hasUntrackedFiles = $hasUntracked
    }
}

function Set-LockedGitEnvironment {
    $fixedNames = @(
        'GIT_CONFIG_NOSYSTEM',
        'GIT_CONFIG_GLOBAL',
        'GIT_OPTIONAL_LOCKS',
        'GIT_TERMINAL_PROMPT',
        'GIT_CONFIG_COUNT',
        'GIT_CONFIG_KEY_0',
        'GIT_CONFIG_VALUE_0',
        'GIT_CONFIG_KEY_1',
        'GIT_CONFIG_VALUE_1')
    $inheritedNames = @(Get-ChildItem Env: | Where-Object {
        $_.Name.StartsWith('GIT_', [StringComparison]::OrdinalIgnoreCase)
    } | ForEach-Object { $_.Name })
    $names = @($fixedNames + $inheritedNames | Sort-Object -Unique)
    $snapshot = [ordered]@{}
    foreach ($name in $names) {
        $item = Get-Item -LiteralPath ("Env:$name") -ErrorAction SilentlyContinue
        $snapshot[$name] = [ordered]@{
            exists = $null -ne $item
            value = $(if ($null -eq $item) { '' } else { [string] $item.Value })
        }
    }

    try {
        foreach ($name in $names) {
            Remove-Item -LiteralPath ("Env:$name") -ErrorAction SilentlyContinue
        }
        Set-Item -LiteralPath 'Env:GIT_CONFIG_NOSYSTEM' -Value '1'
        Set-Item -LiteralPath 'Env:GIT_CONFIG_GLOBAL' -Value 'NUL'
        Set-Item -LiteralPath 'Env:GIT_OPTIONAL_LOCKS' -Value '0'
        Set-Item -LiteralPath 'Env:GIT_TERMINAL_PROMPT' -Value '0'
        Set-Item -LiteralPath 'Env:GIT_CONFIG_COUNT' -Value '2'
        Set-Item -LiteralPath 'Env:GIT_CONFIG_KEY_0' -Value 'core.hooksPath'
        Set-Item -LiteralPath 'Env:GIT_CONFIG_VALUE_0' -Value 'NUL'
        Set-Item -LiteralPath 'Env:GIT_CONFIG_KEY_1' -Value 'core.fsmonitor'
        Set-Item -LiteralPath 'Env:GIT_CONFIG_VALUE_1' -Value 'false'
        $expectedValues = [ordered]@{
            GIT_CONFIG_NOSYSTEM = '1'
            GIT_CONFIG_GLOBAL = 'NUL'
            GIT_OPTIONAL_LOCKS = '0'
            GIT_TERMINAL_PROMPT = '0'
            GIT_CONFIG_COUNT = '2'
            GIT_CONFIG_KEY_0 = 'core.hooksPath'
            GIT_CONFIG_VALUE_0 = 'NUL'
            GIT_CONFIG_KEY_1 = 'core.fsmonitor'
            GIT_CONFIG_VALUE_1 = 'false'
        }
        $actualNames = [string[]] @(Get-ChildItem Env: | Where-Object {
            $_.Name.StartsWith('GIT_', [StringComparison]::OrdinalIgnoreCase)
        } | ForEach-Object { $_.Name })
        $expectedNames = [string[]] @($expectedValues.Keys)
        [Array]::Sort($actualNames, [StringComparer]::OrdinalIgnoreCase)
        [Array]::Sort($expectedNames, [StringComparer]::OrdinalIgnoreCase)
        if (($actualNames -join "`n") -ine ($expectedNames -join "`n")) {
            throw 'The locked Git environment contains an unapproved or missing variable.'
        }
        foreach ($name in $expectedValues.Keys) {
            $actual = Get-Item -LiteralPath ("Env:$name") -ErrorAction Stop
            if ([string] $actual.Value -cne [string] $expectedValues[$name]) {
                throw "The locked Git environment value is incorrect: $name."
            }
        }
    }
    catch {
        $lockError = $_
        try {
            Restore-EnvironmentSnapshot -Snapshot $snapshot
        }
        catch {
            throw ('Could not establish or roll back the locked Git environment. lock: ' +
                $lockError.Exception.Message + '; rollback: ' + $_.Exception.Message)
        }
        throw $lockError
    }
    return $snapshot
}

function Restore-EnvironmentSnapshot {
    param([object] $Snapshot)

    if ($null -eq $Snapshot) {
        return
    }
    $snapshotNames = [string[]] @($Snapshot.Keys)
    $currentGitNames = [string[]] @(Get-ChildItem Env: | Where-Object {
        $_.Name.StartsWith('GIT_', [StringComparison]::OrdinalIgnoreCase)
    } | ForEach-Object { $_.Name })
    foreach ($name in $currentGitNames) {
        if (-not ($snapshotNames -icontains $name)) {
            Remove-Item -LiteralPath ("Env:$name") -ErrorAction SilentlyContinue
        }
    }
    foreach ($name in $Snapshot.Keys) {
        if ($Snapshot[$name].exists) {
            $originalValue = [string] $Snapshot[$name].value
            if ($originalValue.Length -eq 0) {
                if (-not [JueMingR.PhysicalPathNativeMethods]::SetEnvironmentVariable($name, '')) {
                    throw "Could not restore an originally empty Git environment value: $name."
                }
            }
            else {
                Set-Item -LiteralPath ("Env:$name") -Value $originalValue
            }
        }
        else {
            Remove-Item -LiteralPath ("Env:$name") -ErrorAction SilentlyContinue
        }
    }
    $expectedRestoredNames = [string[]] @($Snapshot.Keys | Where-Object { $Snapshot[$_].exists })
    $actualRestoredNames = [string[]] @(Get-ChildItem Env: | Where-Object {
        $_.Name.StartsWith('GIT_', [StringComparison]::OrdinalIgnoreCase)
    } | ForEach-Object { $_.Name })
    [Array]::Sort($expectedRestoredNames, [StringComparer]::OrdinalIgnoreCase)
    [Array]::Sort($actualRestoredNames, [StringComparer]::OrdinalIgnoreCase)
    if (($actualRestoredNames -join "`n") -ine ($expectedRestoredNames -join "`n")) {
        throw 'The original Git environment name set was not restored exactly.'
    }
    foreach ($name in $Snapshot.Keys) {
        $restored = Get-Item -LiteralPath ("Env:$name") -ErrorAction SilentlyContinue
        if ($Snapshot[$name].exists) {
            if ($null -eq $restored -or [string] $restored.Value -cne [string] $Snapshot[$name].value) {
                throw "The original Git environment value was not restored exactly: $name."
            }
        }
        elseif ($null -ne $restored) {
            throw "An originally absent Git environment value remained after restore: $name."
        }
    }
}

function Remove-ValidatedTemporaryRoot {
    param([string] $Path)

    if (-not [System.IO.Directory]::Exists($Path)) {
        return
    }

    $systemTemp = (Get-CanonicalDirectoryPath -Path ([System.IO.Path]::GetTempPath()) -Label 'cleanup system TEMP root').TrimEnd('\') + '\'
    $resolved = Get-CanonicalDirectoryPath -Path $Path -Label 'temporary reproducibility root cleanup target'
    $leaf = [System.IO.Path]::GetFileName($resolved)
    if (-not $resolved.StartsWith($systemTemp, [StringComparison]::OrdinalIgnoreCase) -or
        -not $leaf.StartsWith('JueMingR-Repro-', [StringComparison]::Ordinal)) {
        throw "Refusing to delete an unexpected temporary path: $resolved"
    }

    foreach ($item in Get-ChildItem -LiteralPath $resolved -Force -File -Recurse) {
        if ($item.IsReadOnly) {
            $item.IsReadOnly = $false
        }
    }
    [System.IO.Directory]::Delete($resolved, $true)
}

function Get-ValidatedEffectiveInputSignatures {
    param([object] $Record)

    if ([int] $Record.schemaVersion -ne 2 -or
        [string] $Record.source.formalInputInventoryPolicy -cne 'recorded-git-content-modes-and-effective-msbuild-v2') {
        throw 'A clean clone build record does not use the Phase 0-R content-mode schema.'
    }
    if ([string] $Record.build.environmentPolicy -cne 'closed-allowlist-v1') {
        throw 'A clean clone build record does not use the closed Phase 0-R environment policy.'
    }
    if ($null -eq $Record.build.sourceControlSideEffects -or
        $null -eq $Record.build.sourceControlSideEffects.PSObject.Properties['sourceLinkJsonCount'] -or
        $null -eq $Record.build.sourceControlSideEffects.PSObject.Properties['repositoryUrlAssemblyMetadataCount'] -or
        [int] $Record.build.sourceControlSideEffects.sourceLinkJsonCount -ne 0 -or
        [int] $Record.build.sourceControlSideEffects.repositoryUrlAssemblyMetadataCount -ne 0) {
        throw 'A clean clone build record reports a forbidden SourceLink or RepositoryUrl side effect.'
    }
    $projects = @($Record.build.effectiveProjects)
    if ($projects.Count -ne 7) {
        throw 'A clean clone build record must contain seven effective project input records.'
    }

    $signatures = @{}
    foreach ($project in $projects) {
        $projectPath = [string] $project.project
        if ([string]::IsNullOrWhiteSpace($projectPath) -or $signatures.ContainsKey($projectPath)) {
            throw 'A clean clone build record contains a missing or duplicate effective project path.'
        }
        $ignoredCountProperty = $project.PSObject.Properties['ignoredPreTargetEvaluatedInputCount']
        if ($null -eq $ignoredCountProperty -or
            -not ($ignoredCountProperty.Value -is [int]) -or
            [int] $ignoredCountProperty.Value -ne 0) {
            throw "A clean clone build record reports an ignored effective input: $projectPath"
        }
        if ([string] $project.preTargetEvaluatedInputSetSha256 -notmatch '^[0-9A-F]{64}$') {
            throw "A clean clone build record has an invalid effective input digest: $projectPath"
        }
        if ([string] $project.enableSourceControlManagerQueries -ine 'false' -or
            [string] $project.enableSourceLink -ine 'false' -or
            [string] $project.embedUntrackedSources -ine 'false' -or
            [string] $project.publishRepositoryUrl -ine 'false' -or
            [string] $project.generateRepositoryUrlAttribute -ine 'false' -or
            -not [string]::IsNullOrEmpty([string] $project.repositoryUrl) -or
            -not [string]::IsNullOrEmpty([string] $project.privateRepositoryUrl) -or
            -not [string]::IsNullOrEmpty([string] $project.scmRepositoryUrl) -or
            -not [string]::IsNullOrEmpty([string] $project.sourceLink)) {
            throw "A clean clone build record does not close Git/SourceLink metadata inputs: $projectPath"
        }
        if ([string] $project.deterministic -ine 'true' -or
            [string] $project.deterministicSourcePaths -ine 'false' -or
            [string] $project.pathMap -cne 'project directory -> /_/project; guarded build root -> /_/build' -or
            [string] $project.sourceRevisionId -cne [string] $Record.source.sourceRevisionId) {
            throw "A clean clone build record does not preserve the approved deterministic path/source identity: $projectPath"
        }

        $identityParts = New-Object System.Collections.Generic.List[string]
        foreach ($itemDefinition in @(
            @{ ItemName = 'Compile'; RecordName = 'compile' },
            @{ ItemName = 'EmbeddedResource'; RecordName = 'embeddedResource' },
            @{ ItemName = 'AdditionalFiles'; RecordName = 'additionalFiles' },
            @{ ItemName = 'EditorConfigFiles'; RecordName = 'editorConfigFiles' },
            @{ ItemName = 'AnalyzerConfigFiles'; RecordName = 'analyzerConfigFiles' })) {
            $property = $project.preTargetEvaluatedInputs.PSObject.Properties[$itemDefinition.RecordName]
            if ($null -eq $property) {
                throw "A clean clone build record is missing effective $($itemDefinition.ItemName) inputs: $projectPath"
            }
            foreach ($entry in @($property.Value)) {
                $path = [string] $entry.path
                $origin = [string] $entry.origin
                $sha256 = [string] $entry.sha256
                if ([string]::IsNullOrWhiteSpace($path) -or
                    $path.Contains('\') -or
                    [System.IO.Path]::IsPathRooted($path) -or
                    $path -eq '..' -or
                    $path.StartsWith('../', [StringComparison]::Ordinal) -or
                    $path.Contains('/../')) {
                    throw "A clean clone build record contains an unsafe effective input path: $projectPath"
                }
                if (($origin -cne 'repositoryInput' -or $path.StartsWith('generated/', [StringComparison]::Ordinal) -or $path.StartsWith('sdk/', [StringComparison]::Ordinal)) -and
                    ($origin -cne 'generatedIntermediate' -or -not $path.StartsWith('generated/', [StringComparison]::Ordinal)) -and
                    ($origin -cne 'lockedSdkInput' -or -not $path.StartsWith('sdk/', [StringComparison]::Ordinal))) {
                    throw "A clean clone build record contains an invalid effective input origin/path pair: $projectPath"
                }
                if (($origin -ceq 'repositoryInput' -and $sha256 -notmatch '^[0-9A-F]{64}$') -or
                    ($origin -cne 'repositoryInput' -and -not [string]::IsNullOrEmpty($sha256))) {
                    throw "A clean clone build record contains an invalid effective input content hash: $projectPath"
                }
                $identityParts.Add(("{0}|{1}|{2}|{3}|{4}" -f $projectPath, $itemDefinition.ItemName, $origin, $path, $sha256))
            }
        }
        $normalizedIdentity = (@($identityParts.ToArray()) | Sort-Object) -join "`n"
        $actualDigest = Get-TextSha256 -Text $normalizedIdentity
        if ($actualDigest -cne [string] $project.preTargetEvaluatedInputSetSha256) {
            throw "A clean clone build record effective input digest does not match its arrays: $projectPath"
        }
        $signatures[$projectPath] = ([string] $project.preTargetEvaluatedInputSetSha256) + "`n" + $normalizedIdentity
    }
    return $signatures
}

function Get-ValidatedRawSourceContentDigest {
    param(
        [object] $Record,
        [string] $CloneRoot
    )

    $expectedDigest = [string] $Record.source.recordedSourceContentSha256
    if ($expectedDigest -notmatch '^[0-9A-F]{64}$' -or
        $Record.source.clean -ne $true -or
        $Record.source.trackedBytesMatchIndexAndCommit -ne $true) {
        throw 'A clean clone build record is missing a valid clean recorded-source content identity.'
    }
    $entries = @($Record.source.formalInputContentInventory)
    if ($entries.Count -eq 0) {
        throw 'A clean clone build record contains no recorded-source content inventory.'
    }

    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    $identityParts = New-Object System.Collections.Generic.List[string]
    foreach ($entry in $entries) {
        $path = [string] $entry.path
        $mode = [string] $entry.mode
        $contentMode = [string] $entry.contentMode
        $sha256 = [string] $entry.sha256
        if ([string]::IsNullOrWhiteSpace($path) -or
            -not $seen.Add($path) -or
            $path.Contains('\') -or
            [System.IO.Path]::IsPathRooted($path) -or
            $path -eq '..' -or
            $path.StartsWith('../', [StringComparison]::Ordinal) -or
            $path.Contains('/../') -or
            $entry.tracked -ne $true -or
            ($mode -cne '100644' -and $mode -cne '100755') -or
            ($contentMode -cne 'raw' -and $contentMode -cne 'normalizedTextLf') -or
            $entry.present -ne $true -or
            $sha256 -notmatch '^[0-9A-F]{64}$') {
            throw 'A clean clone build record contains an invalid recorded-source content entry.'
        }
        $physicalPath = Join-Path $CloneRoot $path.Replace('/', '\')
        if (-not [System.IO.File]::Exists($physicalPath) -or
            ([System.IO.File]::GetAttributes($physicalPath) -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "A recorded clean-clone source file is missing or reparse: $path"
        }
        if ($contentMode -ceq 'normalizedTextLf') {
            $normalizedText = [System.IO.File]::ReadAllText($physicalPath).Replace("`r`n", "`n").Replace("`r", "`n")
            $actualLength = $script:Utf8NoBom.GetByteCount($normalizedText)
            $actualSha256 = Get-TextSha256 -Text $normalizedText
        }
        else {
            $actualLength = (Get-Item -LiteralPath $physicalPath -Force).Length
            $actualSha256 = (Get-FileHash -LiteralPath $physicalPath -Algorithm SHA256).Hash.ToUpperInvariant()
        }
        if ([int64] $entry.length -ne [int64] $actualLength -or $sha256 -cne $actualSha256) {
            throw "A clean clone build record source content hash does not match the physical file: $path"
        }
        $identityParts.Add(('{0}|tracked|{1}|{2}|present|{3}|{4}' -f $path, $mode, $contentMode, [int64] $actualLength, $actualSha256))
    }
    $orderedPaths = [string[]] @($entries | ForEach-Object { [string] $_.path })
    $sortedPaths = [string[]] @($orderedPaths)
    [Array]::Sort($sortedPaths, [StringComparer]::Ordinal)
    if (($orderedPaths -join "`n") -cne ($sortedPaths -join "`n")) {
        throw 'A clean clone build record source content inventory is not ordinally sorted.'
    }
    $actualDigest = Get-TextSha256 -Text ($identityParts -join "`n")
    if ($actualDigest -cne $expectedDigest) {
        throw 'A clean clone build record source content digest does not match its physical inventory.'
    }
    return $actualDigest
}

function Invoke-CleanCloneBuild {
    param(
        [string] $CloneRoot,
        [string] $Commit,
        [string] $ConfigurationName,
        [string] $TerrariaSource,
        [string] $XnaSource,
        [string] $LegacyRoot,
        [string] $VerifierRoot,
        [string] $RemoteSentinelUrl
    )

    Invoke-External -FilePath $script:GitPath -Arguments @(
        '--no-pager',
        '--no-optional-locks',
        '--no-replace-objects',
        'clone',
        '--config',
        'core.autocrlf=false',
        '--config',
        'core.eol=lf',
        '--no-local',
        '--no-hardlinks',
        '--quiet',
        '--no-checkout',
        $script:RepositoryRoot,
        $CloneRoot
    ) -FailureMessage 'Temporary clean clone creation failed'
    Invoke-External -FilePath $script:GitPath -Arguments @(
        '--no-pager', '--no-optional-locks', '--no-replace-objects', '-C', $CloneRoot,
        '-c', 'core.autocrlf=false', '-c', 'core.eol=lf',
        'checkout', '--detach', '--quiet', $Commit) -FailureMessage 'Temporary clone checkout failed'
    Invoke-External -FilePath $script:GitPath -Arguments @(
        '--no-pager', '--no-optional-locks', '--no-replace-objects', '-C', $CloneRoot,
        'remote', 'set-url', 'origin', $RemoteSentinelUrl) -FailureMessage 'Temporary clone remote sentinel setup failed'

    $references = Join-Path $CloneRoot 'external\TerrariaRefs'
    if ([System.IO.Directory]::Exists($references)) {
        throw 'A clean clone unexpectedly inherited external/TerrariaRefs.'
    }

    $powerShellPath = [System.IO.Path]::GetFullPath((Join-Path $PSHOME 'powershell.exe'))
    if (-not [System.IO.File]::Exists($powerShellPath) -or
        ([System.IO.File]::GetAttributes($powerShellPath) -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'The independent clean-clone Windows PowerShell executable is unavailable or unsafe.'
    }
    $prepareArguments = @(
        '-NoLogo', '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass',
        '-File', (Join-Path $CloneRoot 'scripts\prepare-terraria-references.ps1'),
        '-DestinationDirectory', $references,
        '-ReadOnlyLegacyDirectory', $LegacyRoot,
        '-ReproducibilityRoot', $VerifierRoot)
    if (-not [string]::IsNullOrWhiteSpace($TerrariaSource)) {
        $prepareArguments += @('-TerrariaInstallDirectory', $TerrariaSource)
    }
    if (-not [string]::IsNullOrWhiteSpace($XnaSource)) {
        $prepareArguments += @('-XnaReferenceDirectory', $XnaSource)
    }
    Invoke-External `
        -FilePath $powerShellPath `
        -Arguments $prepareArguments `
        -FailureMessage 'Independent clean-clone reference preparation failed' | Out-Host

    $buildOutput = Join-Path $CloneRoot 'artifacts\build'
    Invoke-External `
        -FilePath $powerShellPath `
        -Arguments @(
            '-NoLogo', '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass',
            '-File', (Join-Path $CloneRoot 'scripts\build.ps1'),
            '-Configuration', $ConfigurationName,
            '-TerrariaReferencesDirectory', $references,
            '-OutputDirectory', $buildOutput,
            '-ReadOnlyLegacyDirectory', $LegacyRoot,
            '-ReproducibilityRoot', $VerifierRoot,
            '-RequireClean') `
        -FailureMessage 'Independent clean-clone formal build failed' | Out-Host

    $recordPath = Join-Path $buildOutput ("$ConfigurationName\build-record.json")
    if (-not [System.IO.File]::Exists($recordPath)) {
        throw 'A clean clone build did not produce its build record.'
    }

    $record = Read-StrictUtf8Json -Path $recordPath
    if ($record.source.commit -ne $Commit -or $record.source.clean -ne $true) {
        throw 'A clean clone build record does not identify the requested clean commit.'
    }
    $rawSourceContentDigest = Get-ValidatedRawSourceContentDigest -Record $record -CloneRoot $CloneRoot
    $effectiveInputSignatures = Get-ValidatedEffectiveInputSignatures -Record $record

    $workRoot = Join-Path $buildOutput "$ConfigurationName\work"
    $outputs = New-Object System.Collections.Generic.List[object]
    foreach ($entry in $record.outputs) {
        $relativePath = [string] $entry.path
        $physicalPath = Join-Path $workRoot $relativePath.Replace('/', '\')
        if (-not [System.IO.File]::Exists($physicalPath)) {
            throw "Declared output is missing from a clean clone: $relativePath"
        }

        $actualHash = (Get-FileHash -LiteralPath $physicalPath -Algorithm SHA256).Hash.ToUpperInvariant()
        if ($actualHash -ne $entry.sha256) {
            throw "Build record output hash does not match the physical file: $relativePath"
        }

        $extension = [System.IO.Path]::GetExtension($physicalPath)
        if ($extension -ne '.dll' -and $extension -ne '.exe' -and $extension -ne '.pdb') {
            throw "Build record declares an unsupported reproducibility output: $relativePath"
        }

        $outputs.Add([ordered]@{
            path = $relativePath
            size = [int64] $entry.size
            sha256 = $actualHash
        })
    }

    foreach ($file in Get-ChildItem -LiteralPath $workRoot -File -Recurse) {
        if ($file.Name -eq 'Terraria.exe' -or
            $file.Name -eq 'ReLogic.dll' -or
            $file.Name -like 'Microsoft.Xna.Framework*.dll' -or
            $file.Name -eq '0Harmony.dll') {
            throw "A clean clone output contains a forbidden game/runtime binary: $($file.Name)"
        }
    }

    return [ordered]@{
        record = $record
        outputs = @($outputs.ToArray())
        effectiveInputSignatures = $effectiveInputSignatures
        recordedSourceContentDigest = $rawSourceContentDigest
    }
}

Push-Location $script:RepositoryRoot
$temporaryRoot = $null
$result = $null
$savedGitEnvironment = Set-LockedGitEnvironment
$sourceGitBinding = $null
$initialRecordedSourcePaths = @()
$initialRawSourceContent = $null
$commit = $null
$started = [DateTime]::UtcNow
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $sourceGitBinding = Get-PhysicalGitBinding -ExpectedWorkTree $script:RepositoryRoot
    $script:SourceGitMetadataRoot = $sourceGitBinding.gitDirectory
    Assert-PathTreesDisjoint -Candidate $sourceGitBinding.gitDirectory -CandidateLabel 'Git per-worktree metadata directory' -Protected $resolvedSources.terraria -ProtectedLabel 'Terraria source directory'
    Assert-PathTreesDisjoint -Candidate $sourceGitBinding.gitDirectory -CandidateLabel 'Git per-worktree metadata directory' -Protected $resolvedSources.xna -ProtectedLabel 'XNA source directory'
    Assert-PathTreesDisjoint -Candidate $sourceGitBinding.commonDirectory -CandidateLabel 'Git common metadata directory' -Protected $resolvedSources.terraria -ProtectedLabel 'Terraria source directory'
    Assert-PathTreesDisjoint -Candidate $sourceGitBinding.commonDirectory -CandidateLabel 'Git common metadata directory' -Protected $resolvedSources.xna -ProtectedLabel 'XNA source directory'
    foreach ($gitMetadata in @(
        @{ Path = $sourceGitBinding.gitDirectory; Label = 'Git per-worktree metadata directory' },
        @{ Path = $sourceGitBinding.commonDirectory; Label = 'Git common metadata directory' })) {
        Assert-PathTreesDisjoint -Candidate $gitMetadata.Path -CandidateLabel $gitMetadata.Label -Protected $outputRoot -ProtectedLabel 'OutputDirectory'
        Assert-PathTreesDisjoint -Candidate $gitMetadata.Path -CandidateLabel $gitMetadata.Label -Protected $summaryRoot -ProtectedLabel 'reproducibility summary directory'
        Assert-PathTreesDisjoint -Candidate $gitMetadata.Path -CandidateLabel $gitMetadata.Label -Protected $script:LegacyRoot -ProtectedLabel 'read-only Legacy root'
    }
    $initialRecordedSourcePaths = @(Get-GitRecordedSourcePaths)
    Assert-NoIgnoredFormalInputFiles -RecordedSourcePaths $initialRecordedSourcePaths
    Assert-GitIndexAndAttributesSafe -RecordedSourcePaths $initialRecordedSourcePaths

    $status = @(Get-BoundSourceGitLines -Arguments @('status', '--porcelain=v1', '--untracked-files=all'))
    if ($status.Count -ne 0) {
        throw 'Reproducibility verification requires a clean committed source tree.'
    }

    $commitLines = @(Get-BoundSourceGitLines -Arguments @('rev-parse', '--verify', 'HEAD^{commit}'))
    $commit = $commitLines[0].Trim()
    if ($commitLines.Count -ne 1 -or $commit -notmatch '^[0-9a-fA-F]{40}$') {
        throw 'Reproducibility verification requires an existing source commit.'
    }
    $initialTrackedSourcePaths = @(Get-BoundSourceGitLines -Arguments @('-c', 'core.quotePath=false', 'ls-files'))
    $initialRawSourceContent = Get-BoundRawSourceContentInventory `
        -RecordedSourcePaths $initialRecordedSourcePaths `
        -TrackedSourcePaths $initialTrackedSourcePaths `
        -Commit $commit
    if (-not [bool] $initialRawSourceContent.trackedBytesMatchIndexAndCommit -or
        [bool] $initialRawSourceContent.hasUntrackedFiles) {
        throw 'Reproducibility verification requires raw tracked source bytes to match the fixed commit and index.'
    }

    $temporaryRoot = Get-CanonicalDirectoryPath `
        -Path (Join-Path $systemTempRoot ('JueMingR-Repro-' + [Guid]::NewGuid().ToString('N'))) `
        -Label 'temporary reproducibility root'
    Assert-PathTreesDisjoint -Candidate $temporaryRoot -CandidateLabel 'temporary reproducibility root' -Protected $script:RepositoryRoot -ProtectedLabel 'repository root'
    Assert-PathTreesDisjoint -Candidate $temporaryRoot -CandidateLabel 'temporary reproducibility root' -Protected $resolvedSources.terraria -ProtectedLabel 'Terraria source directory'
    Assert-PathTreesDisjoint -Candidate $temporaryRoot -CandidateLabel 'temporary reproducibility root' -Protected $resolvedSources.xna -ProtectedLabel 'XNA source directory'
    Assert-PathTreesDisjoint -Candidate $temporaryRoot -CandidateLabel 'temporary reproducibility root' -Protected $sourceGitBinding.gitDirectory -ProtectedLabel 'Git per-worktree metadata directory'
    Assert-PathTreesDisjoint -Candidate $temporaryRoot -CandidateLabel 'temporary reproducibility root' -Protected $sourceGitBinding.commonDirectory -ProtectedLabel 'Git common metadata directory'
    Assert-PathTreesDisjoint -Candidate $temporaryRoot -CandidateLabel 'temporary reproducibility root' -Protected $script:LegacyRoot -ProtectedLabel 'read-only Legacy root'
    [System.IO.Directory]::CreateDirectory($temporaryRoot) | Out-Null
    $createdTemporaryRoot = Get-CanonicalDirectoryPath -Path $temporaryRoot -Label 'created temporary reproducibility root'
    if (-not [string]::Equals($createdTemporaryRoot, $temporaryRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Temporary reproducibility root changed physical identity while it was being created.'
    }
    $cloneA = Join-Path $temporaryRoot 'clone-a'
    $cloneB = Join-Path $temporaryRoot 'clone-b-with-a-different-path'

    $first = Invoke-CleanCloneBuild -CloneRoot $cloneA -Commit $commit -ConfigurationName $Configuration -TerrariaSource $resolvedSources.terraria -XnaSource $resolvedSources.xna -LegacyRoot $script:LegacyRoot -VerifierRoot $temporaryRoot -RemoteSentinelUrl 'https://clone-a.example.invalid/Kuroneko2020/JueMingR.git'
    $second = Invoke-CleanCloneBuild -CloneRoot $cloneB -Commit $commit -ConfigurationName $Configuration -TerrariaSource $resolvedSources.terraria -XnaSource $resolvedSources.xna -LegacyRoot $script:LegacyRoot -VerifierRoot $temporaryRoot -RemoteSentinelUrl 'https://clone-b.example.invalid/different/JueMingR.git'

    if ($first.recordedSourceContentDigest -cne $second.recordedSourceContentDigest -or
        $first.recordedSourceContentDigest -cne $initialRawSourceContent.contentSetSha256) {
        throw 'The caller and two clean clones do not have identical recorded-source content identities.'
    }

    if ($first.record.build.sdk -ne $second.record.build.sdk -or
        $first.record.build.msbuild -ne $second.record.build.msbuild -or
        $first.record.build.compiler -ne $second.record.build.compiler -or
        $first.record.build.developerPack -ne $second.record.build.developerPack -or
        $first.record.references.baselineSha256 -ne $second.record.references.baselineSha256) {
        throw 'The two clean builds did not use identical SDK, targeting pack, and reference baseline identities.'
    }
    $firstInputProjects = @($first.effectiveInputSignatures.Keys | Sort-Object)
    $secondInputProjects = @($second.effectiveInputSignatures.Keys | Sort-Object)
    if (($firstInputProjects -join "`n") -cne ($secondInputProjects -join "`n")) {
        throw 'The two clean builds did not evaluate the same project input set.'
    }
    foreach ($projectPath in $firstInputProjects) {
        if ([string] $first.effectiveInputSignatures[$projectPath] -cne [string] $second.effectiveInputSignatures[$projectPath]) {
            throw "The two clean builds did not evaluate identical effective inputs: $projectPath"
        }
    }

    $firstByPath = @{}
    foreach ($entry in $first.outputs) { $firstByPath[[string] $entry.path] = $entry }
    $secondByPath = @{}
    foreach ($entry in $second.outputs) { $secondByPath[[string] $entry.path] = $entry }
    $allPaths = @($firstByPath.Keys + $secondByPath.Keys | Sort-Object -Unique)
    $comparisons = New-Object System.Collections.Generic.List[object]
    $differenceCount = 0
    foreach ($path in $allPaths) {
        $firstEntry = $firstByPath[$path]
        $secondEntry = $secondByPath[$path]
        $matches = $null -ne $firstEntry -and
            $null -ne $secondEntry -and
            $firstEntry.size -eq $secondEntry.size -and
            $firstEntry.sha256 -eq $secondEntry.sha256
        if (-not $matches) {
            $differenceCount++
        }

        $comparisons.Add([ordered]@{
            path = $path
            firstSha256 = if ($null -eq $firstEntry) { '' } else { $firstEntry.sha256 }
            secondSha256 = if ($null -eq $secondEntry) { '' } else { $secondEntry.sha256 }
            matches = $matches
        })
        Write-Output ("{0}: {1}" -f $(if ($matches) { 'MATCH' } else { 'DIFFERENT' }), $path)
    }

    if ($differenceCount -ne 0) {
        throw "Reproducibility verification found $differenceCount differing declared output(s)."
    }

    $stopwatch.Stop()
    $result = [ordered]@{
        schemaVersion = 2
        sourceCommit = $commit
        recordedSourceContentSha256 = $initialRawSourceContent.contentSetSha256
        configuration = $Configuration
        sdk = $first.record.build.sdk
        msbuild = $first.record.build.msbuild
        developerPack = $first.record.build.developerPack
        referenceProfileId = $first.record.references.profileId
        referenceBaselineSha256 = $first.record.references.baselineSha256
        independentCloneCount = 2
        inheritedReferenceDirectory = $false
        preTargetEvaluatedInputSetsMatched = $true
        recordedSourceContentMatched = $true
        distinctGitRemoteSentinels = $true
        sourceControlMetadataInputsClosed = $true
        declaredOutputCount = $allPaths.Count
        differenceCount = $differenceCount
        comparisons = @($comparisons.ToArray())
        forbiddenGameFilesInOutputs = 0
        startedUtc = $started.ToString('o')
        endedUtc = [DateTime]::UtcNow.ToString('o')
        elapsedMilliseconds = $stopwatch.ElapsedMilliseconds
        temporaryClones = 'cleaned before summary was written'
    }
}
finally {
    if ($stopwatch.IsRunning) {
        $stopwatch.Stop()
    }
    try {
        if ($null -ne $temporaryRoot) {
            Remove-ValidatedTemporaryRoot -Path $temporaryRoot
        }
    }
    finally {
        Restore-EnvironmentSnapshot -Snapshot $savedGitEnvironment
        Pop-Location
    }
}

if ($null -eq $result) {
    throw 'Reproducibility verification did not produce a result.'
}

$finalGitEnvironment = Set-LockedGitEnvironment
try {
    Assert-PhysicalGitBindingUnchanged -Expected $sourceGitBinding
    $finalRecordedSourcePaths = @(Get-GitRecordedSourcePaths)
    if (($finalRecordedSourcePaths -join "`n") -cne ($initialRecordedSourcePaths -join "`n")) {
        throw 'The recorded Git source inventory changed during reproducibility verification.'
    }
    Assert-NoIgnoredFormalInputFiles -RecordedSourcePaths $finalRecordedSourcePaths
    Assert-GitIndexAndAttributesSafe -RecordedSourcePaths $finalRecordedSourcePaths
    $finalStatus = @(Get-BoundSourceGitLines -Arguments @('status', '--porcelain=v1', '--untracked-files=all'))
    $finalCommitLines = @(Get-BoundSourceGitLines -Arguments @('rev-parse', '--verify', 'HEAD^{commit}'))
    if ($finalStatus.Count -ne 0 -or
        $finalCommitLines.Count -ne 1 -or
        ([string] $finalCommitLines[0]).Trim() -cne $commit) {
        throw 'The reproducibility source identity changed during verification.'
    }
    $finalTrackedSourcePaths = @(Get-BoundSourceGitLines -Arguments @('-c', 'core.quotePath=false', 'ls-files'))
    $finalRawSourceContent = Get-BoundRawSourceContentInventory `
        -RecordedSourcePaths $finalRecordedSourcePaths `
        -TrackedSourcePaths $finalTrackedSourcePaths `
        -Commit $commit
    if (-not [bool] $finalRawSourceContent.trackedBytesMatchIndexAndCommit -or
        [bool] $finalRawSourceContent.hasUntrackedFiles -or
        $finalRawSourceContent.identityText -cne $initialRawSourceContent.identityText -or
        $finalRawSourceContent.contentSetSha256 -cne $initialRawSourceContent.contentSetSha256) {
        throw 'The raw reproducibility source bytes changed during verification.'
    }
}
finally {
    Restore-EnvironmentSnapshot -Snapshot $finalGitEnvironment
}

$recheckedSummaryRoot = Get-CanonicalDirectoryPath -Path (Join-Path $outputRoot $Configuration) -Label 'rechecked reproducibility summary directory'
if (-not [string]::Equals($recheckedSummaryRoot, $summaryRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Reproducibility summary directory changed physical identity during verification.'
}
Assert-PathTreesDisjoint -Candidate $summaryRoot -CandidateLabel 'reproducibility summary directory' -Protected $resolvedSources.terraria -ProtectedLabel 'Terraria source directory'
Assert-PathTreesDisjoint -Candidate $summaryRoot -CandidateLabel 'reproducibility summary directory' -Protected $resolvedSources.xna -ProtectedLabel 'XNA source directory'
Assert-SourceFileMatchesBaseline `
    -Path (Join-Path $resolvedSources.terraria 'Terraria.exe') `
    -Expected (Get-BaselineEntry -Baseline $baseline -LogicalName 'Terraria.exe') `
    -Label 'Terraria.exe after reproducibility verification'
Assert-SourceFileMatchesBaseline `
    -Path (Join-Path $resolvedSources.xna 'Microsoft.Xna.Framework.Game.dll') `
    -Expected (Get-BaselineEntry -Baseline $baseline -LogicalName 'Microsoft.Xna.Framework.Game.dll') `
    -Label 'Microsoft.Xna.Framework.Game.dll after reproducibility verification'
$closingGitEnvironment = Set-LockedGitEnvironment
try {
    Assert-PhysicalGitBindingUnchanged -Expected $sourceGitBinding
    $closingRecordedSourcePaths = @(Get-GitRecordedSourcePaths)
    if (($closingRecordedSourcePaths -join "`n") -cne ($initialRecordedSourcePaths -join "`n")) {
        throw 'The recorded Git source inventory changed before reproducibility summary publication.'
    }
    Assert-NoIgnoredFormalInputFiles -RecordedSourcePaths $closingRecordedSourcePaths
    Assert-GitIndexAndAttributesSafe -RecordedSourcePaths $closingRecordedSourcePaths
    $closingStatus = @(Get-BoundSourceGitLines -Arguments @('status', '--porcelain=v1', '--untracked-files=all'))
    $closingCommitLines = @(Get-BoundSourceGitLines -Arguments @('rev-parse', '--verify', 'HEAD^{commit}'))
    $closingTrackedSourcePaths = @(Get-BoundSourceGitLines -Arguments @('-c', 'core.quotePath=false', 'ls-files'))
    $closingSourceContent = Get-BoundRawSourceContentInventory `
        -RecordedSourcePaths $closingRecordedSourcePaths `
        -TrackedSourcePaths $closingTrackedSourcePaths `
        -Commit $commit
    if ($closingStatus.Count -ne 0 -or
        $closingCommitLines.Count -ne 1 -or
        ([string] $closingCommitLines[0]).Trim() -cne $commit -or
        -not [bool] $closingSourceContent.trackedBytesMatchIndexAndCommit -or
        [bool] $closingSourceContent.hasUntrackedFiles -or
        $closingSourceContent.identityText -cne $initialRawSourceContent.identityText -or
        $closingSourceContent.contentSetSha256 -cne $initialRawSourceContent.contentSetSha256) {
        throw 'The reproducibility source identity changed before summary publication.'
    }
}
finally {
    Restore-EnvironmentSnapshot -Snapshot $closingGitEnvironment
}
[System.IO.Directory]::CreateDirectory($summaryRoot) | Out-Null
$summaryPath = Join-Path $summaryRoot 'reproducibility-summary.json'
$summaryTemporaryPath = Join-Path $summaryRoot ('.juemingr-reproducibility-summary-' + [Guid]::NewGuid().ToString('N') + '.tmp')
$summaryBytes = $script:Utf8NoBom.GetBytes(($result | ConvertTo-Json -Depth 10) + [Environment]::NewLine)
$summaryStream = $null
try {
    $summaryStream = New-Object System.IO.FileStream -ArgumentList @(
        $summaryTemporaryPath,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None,
        4096,
        [System.IO.FileOptions]::WriteThrough)
    $summaryStream.Write($summaryBytes, 0, $summaryBytes.Length)
    $summaryStream.Flush($true)
    $summaryStream.Dispose()
    $summaryStream = $null

    $temporaryItem = Get-Item -LiteralPath $summaryTemporaryPath -Force
    if (-not ($temporaryItem -is [System.IO.FileInfo]) -or
        ($temporaryItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'The owned reproducibility summary temporary file is not a regular non-reparse file.'
    }
    $existingSummaryItem = Get-Item -LiteralPath $summaryPath -Force -ErrorAction SilentlyContinue
    if ($null -ne $existingSummaryItem) {
        if (-not ($existingSummaryItem -is [System.IO.FileInfo])) {
            throw 'The reproducibility summary destination is not a file directory entry.'
        }
        [System.IO.File]::Delete($summaryPath)
    }
    [System.IO.File]::Move($summaryTemporaryPath, $summaryPath)
}
finally {
    if ($null -ne $summaryStream) { $summaryStream.Dispose() }
    if ([System.IO.File]::Exists($summaryTemporaryPath)) {
        [System.IO.File]::Delete($summaryTemporaryPath)
    }
}

Write-Output ("PASS: two independent clean clones produced {0} byte-identical declared DLL/EXE/PDB outputs; differences=0." -f $result.declaredOutputCount)
Write-Output ("Reproducibility summary: {0}" -f $summaryPath)
