[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',
    [string] $TerrariaReferencesDirectory,
    [string] $OutputDirectory,
    [string] $ReadOnlyLegacyDirectory,
    [string] $ReproducibilityRoot,
    [switch] $SkipArchitectureTests,
    [switch] $RequireClean,
    [switch] $NoRestore
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$script:RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$script:BuildMarkerName = '.juemingr-phase0r-build-output'
$script:BuildMarkerValue = 'scripts/build.ps1 schema 1'
$script:Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$script:StrictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)
$script:DotnetPath = $null
$script:GitPath = $null
$script:GitMetadataRoot = $null

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
        [string] $Label,
        [switch] $RequireAbsolute
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "$Label must be a non-empty directory path."
    }
    if ($Path.StartsWith('\\?\', [StringComparison]::Ordinal) -or
        $Path.StartsWith('\\.\', [StringComparison]::Ordinal)) {
        throw "$Label may not use an extended or device path prefix."
    }
    if ($RequireAbsolute) {
        $isDriveAbsolute = $Path -match '^[A-Za-z]:[\\/]'
        $isUncAbsolute = $Path -match '^[\\/]{2}[^\\/]+[\\/]+[^\\/]+'
        if (-not $isDriveAbsolute -and -not $isUncAbsolute) {
            throw "$Label must be a fully qualified absolute directory path."
        }
    }

    try {
        $fullPath = if ($RequireAbsolute) {
            [System.IO.Path]::GetFullPath($Path)
        }
        else {
            Resolve-UnresolvedPath -Path $Path
        }
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
        if ([string]::IsNullOrWhiteSpace($leaf) -or
            [string]::IsNullOrWhiteSpace($parent) -or
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

$script:RepositoryRoot = Get-CanonicalDirectoryPath -Path $script:RepositoryRoot -Label 'repository root' -RequireAbsolute
$script:ProjectPaths = @(
    'src/JueMingR.Bootstrap/JueMingR.Bootstrap.csproj',
    'src/JueMingR.Platform/JueMingR.Platform.csproj',
    'src/JueMingR.Features/JueMingR.Features.csproj',
    'src/JueMingR.TerrariaHost/JueMingR.TerrariaHost.csproj',
    'src/JueMingR.Infrastructure/JueMingR.Infrastructure.csproj',
    'src/JueMingR.Setup/JueMingR.Setup.csproj',
    'tests/JueMingR.ArchitectureTests/JueMingR.ArchitectureTests.csproj'
)
$script:ExpectedProjectReferences = @{
    'src/JueMingR.Bootstrap/JueMingR.Bootstrap.csproj' = @()
    'src/JueMingR.Platform/JueMingR.Platform.csproj' = @()
    'src/JueMingR.Features/JueMingR.Features.csproj' = @('src/JueMingR.Platform/JueMingR.Platform.csproj')
    'src/JueMingR.TerrariaHost/JueMingR.TerrariaHost.csproj' = @(
        'src/JueMingR.Platform/JueMingR.Platform.csproj',
        'src/JueMingR.Features/JueMingR.Features.csproj',
        'src/JueMingR.Infrastructure/JueMingR.Infrastructure.csproj')
    'src/JueMingR.Infrastructure/JueMingR.Infrastructure.csproj' = @('src/JueMingR.Platform/JueMingR.Platform.csproj')
    'src/JueMingR.Setup/JueMingR.Setup.csproj' = @()
    'tests/JueMingR.ArchitectureTests/JueMingR.ArchitectureTests.csproj' = @('src/JueMingR.Platform/JueMingR.Platform.csproj')
}
$script:ExpectedHostReferences = @{
    Terraria = 'Terraria.exe'
    ReLogic = 'ReLogic.dll'
    'Microsoft.Xna.Framework.Game' = 'Microsoft.Xna.Framework.Game.dll'
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

function Get-ReadOnlyLegacyRoot {
    param(
        [string] $ExplicitDirectory,
        [string] $ExplicitReproducibilityRoot
    )

    $siblingCandidate = Join-Path ([System.IO.Path]::GetDirectoryName($script:RepositoryRoot)) 'JueMingZ'
    if ([System.IO.Directory]::Exists($siblingCandidate)) {
        $siblingRoot = Get-CanonicalDirectoryPath -Path $siblingCandidate -Label 'read-only sibling Legacy root' -RequireAbsolute
        if (-not [string]::IsNullOrWhiteSpace($ExplicitDirectory)) {
            $explicitRoot = Get-CanonicalDirectoryPath -Path $ExplicitDirectory -Label 'ReadOnlyLegacyDirectory' -RequireAbsolute
            if (-not [string]::Equals($explicitRoot, $siblingRoot, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'ReadOnlyLegacyDirectory may not replace the required sibling ../JueMingZ repository.'
            }
        }
        return $siblingRoot
    }

    if ([string]::IsNullOrWhiteSpace($ExplicitDirectory) -or
        -not [System.IO.Directory]::Exists($ExplicitDirectory)) {
        throw 'The required sibling Legacy repository ../JueMingZ is missing; an existing explicit read-only Legacy root is required for an isolated clean clone.'
    }
    if ([string]::IsNullOrWhiteSpace($ExplicitReproducibilityRoot) -or
        -not [System.IO.Directory]::Exists($ExplicitReproducibilityRoot)) {
        throw 'An explicit read-only Legacy root is accepted only for a verifier-owned reproducibility clone.'
    }
    $reproducibilityRoot = Get-CanonicalDirectoryPath -Path $ExplicitReproducibilityRoot -Label 'ReproducibilityRoot' -RequireAbsolute
    $repositoryParent = Get-CanonicalDirectoryPath `
        -Path ([System.IO.Path]::GetDirectoryName($script:RepositoryRoot)) `
        -Label 'repository parent' `
        -RequireAbsolute
    $systemTempRoot = Get-CanonicalDirectoryPath -Path ([System.IO.Path]::GetTempPath()) -Label 'system TEMP root' -RequireAbsolute
    $reproducibilityParent = Get-CanonicalDirectoryPath `
        -Path ([System.IO.Path]::GetDirectoryName($reproducibilityRoot)) `
        -Label 'reproducibility root parent' `
        -RequireAbsolute
    $repositoryLeaf = [System.IO.Path]::GetFileName($script:RepositoryRoot)
    if (-not [string]::Equals($repositoryParent, $reproducibilityRoot, [StringComparison]::OrdinalIgnoreCase) -or
        -not [string]::Equals($reproducibilityParent, $systemTempRoot, [StringComparison]::OrdinalIgnoreCase) -or
        [System.IO.Path]::GetFileName($reproducibilityRoot) -notmatch '^JueMingR-Repro-[0-9a-f]{32}$' -or
        ($repositoryLeaf -cne 'clone-a' -and $repositoryLeaf -cne 'clone-b-with-a-different-path')) {
        throw 'The explicit Legacy exception is limited to the two verifier-owned clones under a system TEMP reproducibility root.'
    }
    $explicitRoot = Get-CanonicalDirectoryPath -Path $ExplicitDirectory -Label 'ReadOnlyLegacyDirectory' -RequireAbsolute
    if (-not [string]::Equals([System.IO.Path]::GetFileName($explicitRoot), 'JueMingZ', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'An explicit read-only Legacy root must identify the JueMingZ directory.'
    }
    return $explicitRoot
}

$script:LegacyRoot = Get-ReadOnlyLegacyRoot `
    -ExplicitDirectory $ReadOnlyLegacyDirectory `
    -ExplicitReproducibilityRoot $ReproducibilityRoot
Assert-PathTreesDisjoint -Candidate $script:RepositoryRoot -CandidateLabel 'repository root' -Protected $script:LegacyRoot -ProtectedLabel 'read-only Legacy root'

function Get-RequiredMarkerDirectory {
    param(
        [object] $Source,
        [string] $PropertyName
    )

    $property = $Source.PSObject.Properties[$PropertyName]
    if ($null -eq $property -or -not ($property.Value -is [string]) -or
        [string]::IsNullOrWhiteSpace([string] $property.Value)) {
        throw "Prepared reference marker source.$PropertyName must be a non-empty absolute path."
    }

    return Get-CanonicalDirectoryPath -Path ([string] $property.Value) -Label ("reference marker source." + $PropertyName) -RequireAbsolute
}

function Get-ValidatedMarkerSourceDirectories {
    param(
        [string] $ReferencesRoot,
        [object] $Baseline
    )

    $markerPath = Join-Path $ReferencesRoot '.juemingr-reference-set.json'
    $marker = Read-StrictUtf8Json -Path $markerPath
    $sourceProperty = $marker.PSObject.Properties['source']
    $unchangedProperty = $marker.PSObject.Properties['sourceHashesUnchanged']
    if ($null -eq $sourceProperty -or $null -eq $sourceProperty.Value -or
        $null -eq $unchangedProperty -or
        -not ($unchangedProperty.Value -is [bool]) -or
        $unchangedProperty.Value -ne $true) {
        throw 'Prepared reference marker source metadata is incomplete or untrusted.'
    }

    $source = $sourceProperty.Value
    $terrariaDirectory = Get-RequiredMarkerDirectory -Source $source -PropertyName 'terrariaInstallDirectory'
    $xnaDirectory = Get-RequiredMarkerDirectory -Source $source -PropertyName 'xnaReferenceDirectory'
    $channelProperty = $source.PSObject.Properties['terrariaChannel']
    $channel = if ($null -eq $channelProperty) { '' } else { [string] $channelProperty.Value }
    if ($channel -ne 'Steam' -and $channel -ne 'Explicit legal local installation') {
        throw 'Prepared reference marker has an unsupported Terraria source channel.'
    }
    if ($channel -eq 'Steam') {
        $expected = $Baseline.terrariaChannelEvidence
        foreach ($item in @(
            @{ Name = 'steamAppId'; Expected = [string] $expected.appId },
            @{ Name = 'steamStateFlags'; Expected = [string] $expected.stateFlags },
            @{ Name = 'steamBuildId'; Expected = [string] $expected.buildId })) {
            $property = $source.PSObject.Properties[$item.Name]
            if ($null -eq $property -or
                -not [string]::Equals([string] $property.Value, $item.Expected, [StringComparison]::Ordinal)) {
                throw "Prepared reference marker Steam evidence mismatch: $($item.Name)."
            }
        }
    }

    return [ordered]@{ terraria = $terrariaDirectory; xna = $xnaDirectory }
}

function Assert-RecordedSourceFilesMatchBaseline {
    param(
        [object] $MarkerSources,
        [object] $Baseline
    )

    if (-not [System.IO.Directory]::Exists($MarkerSources.terraria) -or
        -not [System.IO.Directory]::Exists($MarkerSources.xna)) {
        throw 'Prepared reference marker source directories must still exist.'
    }

    $terrariaExpected = @($Baseline.files | Where-Object { $_.logicalName -eq 'Terraria.exe' })
    $xnaExpected = @($Baseline.files | Where-Object { $_.logicalName -eq 'Microsoft.Xna.Framework.Game.dll' })
    if ($terrariaExpected.Count -ne 1 -or $xnaExpected.Count -ne 1) {
        throw 'Reference baseline source identities are incomplete.'
    }

    foreach ($check in @(
        @{ Path = (Join-Path $MarkerSources.terraria 'Terraria.exe'); Expected = $terrariaExpected[0].sha256; Label = 'Terraria.exe' },
        @{ Path = (Join-Path $MarkerSources.xna 'Microsoft.Xna.Framework.Game.dll'); Expected = $xnaExpected[0].sha256; Label = 'Microsoft.Xna.Framework.Game.dll' })) {
        if (-not [System.IO.File]::Exists($check.Path)) {
            throw "Prepared reference marker source file is missing: $($check.Label)"
        }
        $actualHash = (Get-FileHash -LiteralPath $check.Path -Algorithm SHA256).Hash.ToUpperInvariant()
        if (-not [string]::Equals($actualHash, [string] $check.Expected, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Prepared reference marker source file no longer matches the baseline: $($check.Label)"
        }
    }
}

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

function Read-StrictUtf8Json {
    param([string] $Path)

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $offset = if ($bytes.Length -ge 3 -and
        $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) { 3 } else { 0 }
    $text = $script:StrictUtf8.GetString($bytes, $offset, $bytes.Length - $offset)
    return ConvertFrom-Json -InputObject $text
}

function Get-TextSha256 {
    param([string] $Text)

    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
        return ([System.BitConverter]::ToString($algorithm.ComputeHash($bytes))).Replace('-', '')
    }
    finally {
        $algorithm.Dispose()
    }
}

function Get-NormalizedTextFileSha256 {
    param([string] $Path)

    $text = [System.IO.File]::ReadAllText($Path)
    $normalized = $text.Replace("`r`n", "`n").Replace("`r", "`n")
    return Get-TextSha256 -Text $normalized
}

function Get-GitOutput {
    param([string[]] $Arguments)

    if ([string]::IsNullOrWhiteSpace($script:GitPath)) {
        throw 'The formal Git executable path has not been locked.'
    }
    if ([string]::IsNullOrWhiteSpace($script:GitMetadataRoot)) {
        throw 'The formal Git metadata directory has not been bound to the physical repository root.'
    }
    $output = & $script:GitPath `
        --no-pager `
        --no-optional-locks `
        --no-replace-objects `
        --literal-pathspecs `
        -C $script:RepositoryRoot `
        "--git-dir=$script:GitMetadataRoot" `
        "--work-tree=$script:RepositoryRoot" `
        -c core.safecrlf=false `
        @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git command failed: git $($Arguments -join ' ')"
    }

    return @($output)
}

function Get-UnboundGitDiscoveryLine {
    param(
        [string] $RepositoryRoot,
        [string[]] $Arguments,
        [string] $Label
    )

    if ([string]::IsNullOrWhiteSpace($script:GitPath)) {
        throw 'The formal Git executable path has not been locked.'
    }
    $output = @(& $script:GitPath --no-pager --no-optional-locks --no-replace-objects -C $RepositoryRoot @Arguments 2>&1)
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0 -or $output.Count -ne 1 -or [string]::IsNullOrWhiteSpace([string] $output[0])) {
        throw "Could not discover the physical Git $Label binding."
    }
    return ([string] $output[0]).TrimEnd([char[]] @("`r", "`n"))
}

function Get-PhysicalGitBinding {
    param([string] $ExpectedWorkTree)

    $insideWorkTree = Get-UnboundGitDiscoveryLine `
        -RepositoryRoot $ExpectedWorkTree `
        -Arguments @('rev-parse', '--is-inside-work-tree') `
        -Label 'work tree state'
    $isBare = Get-UnboundGitDiscoveryLine `
        -RepositoryRoot $ExpectedWorkTree `
        -Arguments @('rev-parse', '--is-bare-repository') `
        -Label 'bare repository state'
    if ($insideWorkTree -cne 'true' -or $isBare -cne 'false') {
        throw 'The repository root must be a non-bare Git work tree.'
    }

    $reportedWorkTree = Get-UnboundGitDiscoveryLine `
        -RepositoryRoot $ExpectedWorkTree `
        -Arguments @('rev-parse', '--path-format=absolute', '--show-toplevel') `
        -Label 'work tree root'
    $physicalWorkTree = Get-CanonicalDirectoryPath -Path $reportedWorkTree -Label 'Git reported work tree root' -RequireAbsolute
    if (-not [string]::Equals($physicalWorkTree, $ExpectedWorkTree, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Git is not bound to the physical repository root; local core.worktree or equivalent redirection is forbidden.'
    }

    $reportedGitDirectory = Get-UnboundGitDiscoveryLine `
        -RepositoryRoot $ExpectedWorkTree `
        -Arguments @('rev-parse', '--absolute-git-dir') `
        -Label 'per-worktree metadata directory'
    $reportedCommonDirectory = Get-UnboundGitDiscoveryLine `
        -RepositoryRoot $ExpectedWorkTree `
        -Arguments @('rev-parse', '--path-format=absolute', '--git-common-dir') `
        -Label 'common metadata directory'
    $gitDirectory = Get-CanonicalDirectoryPath -Path $reportedGitDirectory -Label 'Git per-worktree metadata directory' -RequireAbsolute
    $commonDirectory = Get-CanonicalDirectoryPath -Path $reportedCommonDirectory -Label 'Git common metadata directory' -RequireAbsolute

    $dotGitPath = Join-Path $ExpectedWorkTree '.git'
    $dotGitItem = Get-Item -LiteralPath $dotGitPath -Force -ErrorAction Stop
    if ((-not ($dotGitItem -is [System.IO.FileInfo]) -and -not ($dotGitItem -is [System.IO.DirectoryInfo])) -or
        ($dotGitItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'The repository .git entry must be a regular file or directory and may not be a reparse point.'
    }
    $dotGitKind = if ($dotGitItem -is [System.IO.FileInfo]) { 'file' } else { 'directory' }

    return [pscustomobject]@{
        workTree = $physicalWorkTree
        gitDirectory = $gitDirectory
        commonDirectory = $commonDirectory
        dotGitKind = $dotGitKind
    }
}

function Assert-PhysicalGitBindingUnchanged {
    param([object] $Expected)

    $actual = Get-PhysicalGitBinding -ExpectedWorkTree $script:RepositoryRoot
    foreach ($name in @('workTree', 'gitDirectory', 'commonDirectory', 'dotGitKind')) {
        if (-not [string]::Equals([string] $actual.$name, [string] $Expected.$name, [StringComparison]::OrdinalIgnoreCase)) {
            throw "The physical Git binding changed during the formal build: $name."
        }
    }
}

function Get-DirtyIdentity {
    param(
        [string] $Root,
        [string[]] $StatusLines,
        [string] $RecordedSourceContentSha256
    )

    $parts = New-Object System.Collections.Generic.List[string]
    $parts.Add("recorded-source-content|$RecordedSourceContentSha256")
    $parts.Add('status')
    foreach ($line in $StatusLines) { $parts.Add($line) }
    $parts.Add('working-diff')
    foreach ($line in Get-GitOutput -Arguments @('diff', '--no-ext-diff', '--no-textconv', '--binary', 'HEAD', '--')) { $parts.Add([string] $line) }
    $parts.Add('staged-diff')
    foreach ($line in Get-GitOutput -Arguments @('diff', '--cached', '--no-ext-diff', '--no-textconv', '--binary', 'HEAD', '--')) { $parts.Add([string] $line) }

    foreach ($relativePath in @(Get-GitOutput -Arguments @('-c', 'core.quotePath=false', 'ls-files', '--others', '--exclude-standard')) | Sort-Object) {
        $absolutePath = Join-Path $Root $relativePath
        if ([System.IO.File]::Exists($absolutePath)) {
            $parts.Add(('untracked|{0}|{1}' -f $relativePath.Replace('\', '/'), (Get-FileHash -LiteralPath $absolutePath -Algorithm SHA256).Hash))
        }
    }

    return Get-TextSha256 -Text ($parts -join "`n")
}

function Get-GitSourceIdentity {
    param(
        [string] $Root,
        [object] $ContentInventory
    )

    $commitLines = @(Get-GitOutput -Arguments @('rev-parse', 'HEAD'))
    $statusLines = @(Get-GitOutput -Arguments @('status', '--porcelain=v1', '--untracked-files=all'))
    if ($commitLines.Count -ne 1 -or [string] $commitLines[0] -cne [string] $ContentInventory.commit) {
        throw 'The recorded source content inventory is not bound to the current HEAD commit.'
    }
    $isClean = $statusLines.Count -eq 0 -and
        [bool] $ContentInventory.trackedBytesMatchIndexAndCommit -and
        -not [bool] $ContentInventory.hasUntrackedFiles
    $dirtyIdentity = if ($isClean) {
        ''
    }
    else {
        Get-DirtyIdentity `
            -Root $Root `
            -StatusLines $statusLines `
            -RecordedSourceContentSha256 ([string] $ContentInventory.contentSetSha256)
    }
    return [pscustomobject]@{
        commit = $commitLines[0].Trim()
        statusLines = @($statusLines)
        clean = $isClean
        dirtyIdentity = $dirtyIdentity
    }
}

function Get-GitRecordedSourcePaths {
    $paths = New-Object System.Collections.Generic.List[string]
    foreach ($relativePath in @(
        @(Get-GitOutput -Arguments @('-c', 'core.quotePath=false', 'ls-files')) +
        @(Get-GitOutput -Arguments @('-c', 'core.quotePath=false', 'ls-files', '--others', '--exclude-standard')) |
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

function Get-RawRepositoryContentInventory {
    param(
        [string] $Root,
        [string[]] $RecordedSourcePaths,
        [string[]] $TrackedSourcePaths,
        [string] $Commit
    )

    if ([string]::IsNullOrWhiteSpace($Commit) -or $Commit -notmatch '^(?:[0-9a-fA-F]{40}|[0-9a-fA-F]{64})$') {
        throw 'A fixed Git commit is required for the raw source content inventory.'
    }

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
    $entries = New-Object System.Collections.Generic.List[object]
    $identityParts = New-Object System.Collections.Generic.List[string]
    $trackedMismatchPaths = New-Object System.Collections.Generic.List[string]
    $trackedBytesMatch = $true
    $hasUntracked = $false
    foreach ($relativePath in $orderedPaths) {
        if ([string]::IsNullOrWhiteSpace($relativePath) -or
            [System.IO.Path]::IsPathRooted($relativePath) -or
            $relativePath -eq '..' -or
            $relativePath.StartsWith('../', [StringComparison]::Ordinal) -or
            $relativePath.Contains('/../')) {
            throw "The raw source content inventory contains an unsafe path: $relativePath"
        }

        $absolutePath = Join-Path $Root $relativePath.Replace('/', '\')
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
            throw "The raw source content inventory may not read a reparse file: $relativePath"
        }

        $mode = if ($isTracked) { '' } else { 'untracked' }
        $rawObjectId = ''
        if ($isTracked) {
            $indexLines = @(Get-GitOutput -Arguments @('-c', 'core.quotePath=false', 'ls-files', '--stage', '--', $relativePath))
            if ($indexLines.Count -ne 1 -or
                [string] $indexLines[0] -notmatch '^([0-9]{6}) ((?:[0-9a-fA-F]{40}|[0-9a-fA-F]{64})) 0\t') {
                throw "Tracked source must have exactly one ordinary stage-0 index entry: $relativePath"
            }
            $mode = $Matches[1]
            $indexObjectId = $Matches[2].ToLowerInvariant()
            if ($mode -cne '100644' -and $mode -cne '100755') {
                throw "Tracked source has a forbidden Git mode: $relativePath ($mode)"
            }

            $commitLines = @(Get-GitOutput -Arguments @('-c', 'core.quotePath=false', 'ls-tree', $Commit, '--', $relativePath))
            $commitObjectId = ''
            $commitMode = ''
            if ($commitLines.Count -eq 1 -and
                [string] $commitLines[0] -match '^([0-9]{6}) blob ((?:[0-9a-fA-F]{40}|[0-9a-fA-F]{64}))\t') {
                $commitMode = $Matches[1]
                $commitObjectId = $Matches[2].ToLowerInvariant()
            }
            elseif ($commitLines.Count -gt 1) {
                throw "The fixed commit returned an ambiguous source entry: $relativePath"
            }

            if ($present) {
                $rawObjectArguments = if ($contentMode -ceq 'raw') {
                    @('hash-object', '--no-filters', '--', $relativePath)
                }
                else {
                    @('-c', 'core.autocrlf=true', '-c', 'core.eol=lf', 'hash-object', '--', $relativePath)
                }
                $rawObjectLines = @(Get-GitOutput -Arguments $rawObjectArguments)
                if ($rawObjectLines.Count -ne 1 -or [string] $rawObjectLines[0] -notmatch '^(?:[0-9a-fA-F]{40}|[0-9a-fA-F]{64})$') {
                    throw "Could not hash the raw working-tree bytes for: $relativePath"
                }
                $rawObjectId = ([string] $rawObjectLines[0]).ToLowerInvariant()
            }
            if (-not $present -or
                [string]::IsNullOrEmpty($commitObjectId) -or
                $mode -cne $commitMode -or
                $rawObjectId -cne $indexObjectId -or
                $rawObjectId -cne $commitObjectId) {
                $trackedBytesMatch = $false
                $trackedMismatchPaths.Add(("{0}[{1};raw={2};index={3};commit={4};mode={5}/{6}]" -f `
                    $relativePath, $contentMode, $rawObjectId, $indexObjectId, $commitObjectId, $mode, $commitMode))
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
        $entry = [ordered]@{
            path = $relativePath
            tracked = $isTracked
            mode = $mode
            contentMode = $contentMode
            present = $present
            length = [int64] $length
            sha256 = $sha256
        }
        $entries.Add($entry)
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
        throw 'The recorded source inventory does not contain every tracked source path.'
    }
    $identityText = $identityParts -join "`n"
    return [pscustomobject]@{
        commit = $Commit.ToLowerInvariant()
        entries = @($entries.ToArray())
        identityText = $identityText
        contentSetSha256 = Get-TextSha256 -Text $identityText
        trackedBytesMatchIndexAndCommit = $trackedBytesMatch
        trackedMismatchPaths = @($trackedMismatchPaths.ToArray())
        hasUntrackedFiles = $hasUntracked
    }
}

function Assert-PhysicalContentInventoryMatches {
    param(
        [object[]] $Entries,
        [string] $ExpectedDigest
    )

    $identityParts = New-Object System.Collections.Generic.List[string]
    foreach ($entry in $Entries) {
        $relativePath = [string] $entry.path
        $absolutePath = Join-Path $script:RepositoryRoot $relativePath.Replace('/', '\')
        $present = [System.IO.File]::Exists($absolutePath)
        if ($present -and
            ([System.IO.File]::GetAttributes($absolutePath) -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "A recorded source became a reparse file after build cleanup: $relativePath"
        }
        $length = 0
        $sha256 = ''
        if ($present) {
            if ([string] $entry.contentMode -ceq 'normalizedTextLf') {
                $normalizedText = [System.IO.File]::ReadAllText($absolutePath).Replace("`r`n", "`n").Replace("`r", "`n")
                $length = $script:Utf8NoBom.GetByteCount($normalizedText)
                $sha256 = Get-TextSha256 -Text $normalizedText
            }
            elseif ([string] $entry.contentMode -ceq 'raw') {
                $length = (Get-Item -LiteralPath $absolutePath -Force).Length
                $sha256 = (Get-FileHash -LiteralPath $absolutePath -Algorithm SHA256).Hash.ToUpperInvariant()
            }
            else {
                throw "A recorded source has an unknown content mode after build cleanup: $relativePath"
            }
        }
        if ([bool] $entry.present -ne $present -or
            [int64] $entry.length -ne [int64] $length -or
            [string] $entry.sha256 -cne $sha256) {
            throw "A recorded source changed after build cleanup; no build record will be written: $relativePath"
        }
        $identityParts.Add(('{0}|{1}|{2}|{3}|{4}|{5}|{6}' -f `
            $relativePath,
            $(if ([bool] $entry.tracked) { 'tracked' } else { 'untracked' }),
            [string] $entry.mode,
            [string] $entry.contentMode,
            $(if ($present) { 'present' } else { 'missing' }),
            [int64] $length,
            $sha256))
    }
    $actualDigest = Get-TextSha256 -Text ($identityParts -join "`n")
    if ($actualDigest -cne $ExpectedDigest) {
        throw 'The recorded source content digest changed after build cleanup; no build record will be written.'
    }
}

function Assert-NoIgnoredFormalInputFiles {
    param([string[]] $RecordedSourcePaths)

    $known = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($relativePath in $RecordedSourcePaths) {
        if (-not $known.Add(([string] $relativePath).Replace('\', '/'))) {
            throw "The recorded source inventory contains a duplicate path: $relativePath"
        }
    }

    $physicalFiles = New-Object System.Collections.Generic.List[System.IO.FileInfo]
    foreach ($rootFileName in @(
        '.editorconfig', '.gitattributes', '.globalconfig', 'Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props',
        'global.json', 'JueMingR.sln', 'NuGet.config', 'MSBuild.rsp', 'Directory.Build.rsp')) {
        $candidate = Join-Path $script:RepositoryRoot $rootFileName
        if ([System.IO.File]::Exists($candidate)) {
            $physicalFiles.Add((Get-Item -LiteralPath $candidate -Force))
        }
    }

    foreach ($relativeRoot in @('eng', 'src', 'tests')) {
        $absoluteRoot = Join-Path $script:RepositoryRoot $relativeRoot
        if (-not [System.IO.Directory]::Exists($absoluteRoot)) {
            throw "Formal source root is missing: $relativeRoot"
        }
        $directories = New-Object System.Collections.Generic.Stack[string]
        $directories.Push($absoluteRoot)
        while ($directories.Count -ne 0) {
            $current = $directories.Pop()
            foreach ($entry in (New-Object System.IO.DirectoryInfo -ArgumentList $current).EnumerateFileSystemInfos()) {
                if (($entry.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                    throw "Formal source inventory may not contain a reparse point: $($entry.FullName)"
                }
                if ($entry -is [System.IO.DirectoryInfo]) {
                    if ($entry.Name -ine 'bin' -and $entry.Name -ine 'obj') {
                        $directories.Push($entry.FullName)
                    }
                }
                elseif ($entry -is [System.IO.FileInfo]) {
                    $physicalFiles.Add($entry)
                }
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

    foreach ($line in @(Get-GitOutput -Arguments @('-c', 'core.quotePath=false', 'ls-files', '-v'))) {
        if (-not ([string] $line).StartsWith('H ', [StringComparison]::Ordinal)) {
            throw "Git index assume-unchanged, skip-worktree, or other non-normal tracking mode is forbidden: $line"
        }
    }
    foreach ($relativePath in $RecordedSourcePaths) {
        $attributeLines = @(Get-GitOutput -Arguments @(
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

function Get-ApprovedEffectiveInputPaths {
    param(
        [object] $Evaluation,
        [string] $ItemName,
        [string] $ProjectPath,
        [string[]] $RecordedSourcePaths,
        [string] $BaseIntermediateRoot,
        [string[]] $IntermediateRoots,
        [string] $SdkRoot
    )

    $recorded = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($relativePath in $RecordedSourcePaths) { $null = $recorded.Add(([string] $relativePath).Replace('\', '/')) }
    $approved = New-Object System.Collections.Generic.List[object]
    $identities = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($item in @(Get-MsBuildItems -Evaluation $Evaluation -Name $ItemName)) {
        $fullPathValue = Get-MsBuildItemMetadata -Item $item -Name 'FullPath'
        $definingProjectValue = Get-MsBuildItemMetadata -Item $item -Name 'DefiningProjectFullPath'
        if ([string]::IsNullOrWhiteSpace($fullPathValue)) {
            throw "Effective $ItemName has no absolute FullPath in $ProjectPath."
        }
        if ([string]::IsNullOrWhiteSpace($definingProjectValue)) {
            throw "Effective $ItemName has no DefiningProjectFullPath in $ProjectPath."
        }
        $fullPath = [System.IO.Path]::GetFullPath($fullPathValue)
        $definingProject = [System.IO.Path]::GetFullPath($definingProjectValue)
        $definedByRepository = Test-RepositoryFilePath -Path $definingProject
        $definedBySdk = Test-PathWithinOrEqual -Candidate $definingProject -Parent $SdkRoot
        if (-not $definedByRepository -and -not $definedBySdk) {
            throw "Effective $ItemName was injected by an external build definition in $ProjectPath."
        }

        $origin = ''
        $recordedPath = ''
        $contentSha256 = ''
        $isGenerated = $false
        foreach ($intermediateRoot in $IntermediateRoots) {
            if (Test-PathWithinOrEqual -Candidate $fullPath -Parent $intermediateRoot) {
                $isGenerated = $true
                break
            }
        }
        if ($isGenerated) {
            if (-not $definedBySdk) {
                throw "Effective generated $ItemName was not defined by the locked SDK in $ProjectPath."
            }
            $relativeGeneratedPath = $fullPath.Substring($BaseIntermediateRoot.TrimEnd('\').Length).TrimStart('\').Replace('\', '/')
            $origin = 'generatedIntermediate'
            $recordedPath = "generated/$relativeGeneratedPath"
        }
        elseif (Test-RepositoryFilePath -Path $fullPath) {
            $relativePath = Get-RepositoryRelativeFilePath -Path $fullPath
            if (-not $recorded.Contains($relativePath) -or
                -not [System.IO.File]::Exists($fullPath) -or
                ([System.IO.File]::GetAttributes($fullPath) -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Effective $ItemName is not a recorded regular repository source file in ${ProjectPath}: $relativePath"
            }
            $origin = 'repositoryInput'
            $recordedPath = $relativePath
            $contentSha256 = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToUpperInvariant()
        }
        elseif (Test-PathWithinOrEqual -Candidate $fullPath -Parent $SdkRoot) {
            if (-not $definedBySdk -or
                -not [System.IO.File]::Exists($fullPath) -or
                ([System.IO.File]::GetAttributes($fullPath) -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Effective $ItemName points to a missing or reparse SDK file in $ProjectPath."
            }
            $relativeSdkPath = $fullPath.Substring($SdkRoot.TrimEnd('\').Length).TrimStart('\').Replace('\', '/')
            $origin = 'lockedSdkInput'
            $recordedPath = "sdk/$relativeSdkPath"
        }
        else {
            throw "Effective $ItemName escapes the recorded repository, exact guarded intermediate roots, and locked SDK in $ProjectPath."
        }

        $identity = "$origin|$recordedPath"
        if (-not $identities.Add($identity)) {
            throw "Effective $ItemName contains a duplicate approved input in ${ProjectPath}: $recordedPath"
        }
        $approved.Add([ordered]@{ path = $recordedPath; origin = $origin; sha256 = $contentSha256 })
    }
    return @($approved.ToArray() | Sort-Object @{ Expression = { $_.origin } }, @{ Expression = { $_.path } })
}

function Assert-RawBuildDefinitionsSafe {
    $solutionPath = Join-Path $script:RepositoryRoot 'JueMingR.sln'
    $solutionProjects = New-Object System.Collections.Generic.List[string]
    foreach ($line in [System.IO.File]::ReadAllLines($solutionPath)) {
        if (-not $line.StartsWith('Project(', [StringComparison]::Ordinal)) {
            continue
        }

        $match = [regex]::Match(
            $line,
            '^Project\("\{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC\}"\) = "[^"]+", "([^"]+\.csproj)", "\{[0-9A-Fa-f-]+\}"$')
        if (-not $match.Success) {
            throw "Only approved C# project entries are allowed in JueMingR.sln: $line"
        }

        if ($match.Success) {
            $absoluteProject = [System.IO.Path]::GetFullPath((Join-Path $script:RepositoryRoot $match.Groups[1].Value))
            $solutionProjects.Add((Get-RepositoryRelativeFilePath -Path $absoluteProject))
        }
    }
    Assert-ExactStringSet -Actual @($solutionProjects.ToArray()) -Expected $script:ProjectPaths -Label 'solution project set'

    $allowedProjectElements = @(
        'Project', 'PropertyGroup', 'ItemGroup',
        'TargetFramework', 'PlatformTarget', 'OutputType', 'AssemblyName', 'RootNamespace',
        'GenerateAssemblyInfo', 'AllowUnsafeBlocks', 'TreatWarningsAsErrors',
        'TerrariaReferencesDirectory', 'ProjectReference', 'Reference', 'HintPath', 'Private')
    foreach ($relativePath in $script:ProjectPaths) {
        $projectPath = Join-Path $script:RepositoryRoot $relativePath
        if (-not [System.IO.File]::Exists($projectPath)) {
            throw "Approved project file is missing: $relativePath"
        }

        $currentDirectory = [System.IO.Path]::GetDirectoryName($projectPath)
        while (-not [string]::Equals($currentDirectory, $script:RepositoryRoot, [StringComparison]::OrdinalIgnoreCase)) {
            foreach ($sharedName in @('Directory.Build.props', 'Directory.Build.targets')) {
                if ([System.IO.File]::Exists((Join-Path $currentDirectory $sharedName))) {
                    throw "Nested shared build file is forbidden before evaluation: $relativePath -> $sharedName"
                }
            }
            $currentDirectory = [System.IO.Path]::GetDirectoryName($currentDirectory)
            if ([string]::IsNullOrWhiteSpace($currentDirectory)) {
                throw "Project path escaped the repository while checking shared build files: $relativePath"
            }
        }

        [xml] $document = [System.IO.File]::ReadAllText($projectPath)
        $root = $document.DocumentElement
        if ($null -eq $root -or $root.LocalName -cne 'Project' -or
            -not [string]::IsNullOrEmpty($root.NamespaceURI) -or
            $root.Attributes.Count -ne 1 -or $root.GetAttribute('Sdk') -cne 'Microsoft.NET.Sdk') {
            throw "Project root/Sdk is not the approved pre-evaluation form: $relativePath"
        }

        foreach ($element in @($document.SelectNodes('//*'))) {
            if ($allowedProjectElements -cnotcontains $element.LocalName) {
                throw "Project contains an unapproved executable or dependency-bearing build element before evaluation: $relativePath -> $($element.LocalName)"
            }
            if ($element.InnerText.Contains('$([')) {
                throw "Project property functions are forbidden before evaluation: $relativePath -> $($element.LocalName)"
            }

            foreach ($attribute in @($element.Attributes)) {
                if ($attribute.Value.Contains('$([')) {
                    throw "Project attribute property functions are forbidden before evaluation: $relativePath -> $($element.LocalName)"
                }
                $allowedAttribute =
                    ($element.LocalName -ceq 'Project' -and $attribute.LocalName -ceq 'Sdk') -or
                    (($element.LocalName -ceq 'ProjectReference' -or $element.LocalName -ceq 'Reference') -and $attribute.LocalName -ceq 'Include') -or
                    ($relativePath -ceq 'src/JueMingR.TerrariaHost/JueMingR.TerrariaHost.csproj' -and
                        $element.LocalName -ceq 'TerrariaReferencesDirectory' -and
                        $attribute.LocalName -ceq 'Condition' -and
                        $attribute.Value.Trim() -ceq "'`$(TerrariaReferencesDirectory)' == ''")
                if (-not $allowedAttribute) {
                    throw "Project contains an unapproved attribute before evaluation: $relativePath -> $($element.LocalName).$($attribute.LocalName)"
                }
            }
        }

        $expectedName = [System.IO.Path]::GetFileNameWithoutExtension($relativePath)
        $expectedOutputType = if ($relativePath -eq 'tests/JueMingR.ArchitectureTests/JueMingR.ArchitectureTests.csproj') { 'Exe' } else { 'Library' }
        $requiredProperties = [ordered]@{
            TargetFramework = 'net472'
            PlatformTarget = 'x86'
            OutputType = $expectedOutputType
            AssemblyName = $expectedName
            RootNamespace = $expectedName
            GenerateAssemblyInfo = 'true'
            AllowUnsafeBlocks = 'false'
            TreatWarningsAsErrors = 'true'
        }
        foreach ($propertyName in $requiredProperties.Keys) {
            $nodes = @($document.SelectNodes("//$propertyName"))
            if ($nodes.Count -ne 1 -or $nodes[0].InnerText.Trim() -cne $requiredProperties[$propertyName]) {
                throw "Project property is not in the approved pre-evaluation form: $relativePath -> $propertyName"
            }
        }

        $effectiveProjectReferences = New-Object System.Collections.Generic.List[string]
        foreach ($reference in @($document.SelectNodes('//ProjectReference'))) {
            if ($reference.Attributes.Count -ne 1 -or $reference.ChildNodes.Count -ne 0) {
                throw "ProjectReference is not in the approved pre-evaluation form: $relativePath"
            }
            $referencedPath = [System.IO.Path]::GetFullPath((Join-Path ([System.IO.Path]::GetDirectoryName($projectPath)) $reference.GetAttribute('Include')))
            $effectiveProjectReferences.Add((Get-RepositoryRelativeFilePath -Path $referencedPath))
        }
        Assert-ExactStringSet -Actual @($effectiveProjectReferences.ToArray()) -Expected @($script:ExpectedProjectReferences[$relativePath]) -Label "$relativePath raw ProjectReference"

        $directReferences = @($document.SelectNodes('//Reference'))
        if ($relativePath -ne 'src/JueMingR.TerrariaHost/JueMingR.TerrariaHost.csproj') {
            if ($directReferences.Count -ne 0) {
                throw "Non-Host project has a direct Reference before evaluation: $relativePath"
            }
        }
        else {
            $identities = New-Object System.Collections.Generic.List[string]
            foreach ($reference in $directReferences) {
                $identity = $reference.GetAttribute('Include')
                $hint = @($reference.SelectNodes('./HintPath'))
                $private = @($reference.SelectNodes('./Private'))
                if ($reference.Attributes.Count -ne 1 -or $reference.ChildNodes.Count -ne 2 -or
                    $hint.Count -ne 1 -or $private.Count -ne 1 -or
                    -not $script:ExpectedHostReferences.ContainsKey($identity) -or
                    $hint[0].InnerText.Trim() -cne ('$(TerrariaReferencesDirectory)\' + $script:ExpectedHostReferences[$identity]) -or
                    $private[0].InnerText.Trim() -ine 'false') {
                    throw "TerrariaHost Reference is not in the approved pre-evaluation form: $identity"
                }
                $identities.Add($identity)
            }
            Assert-ExactStringSet -Actual @($identities.ToArray()) -Expected @($script:ExpectedHostReferences.Keys) -Label 'TerrariaHost raw direct Reference'

            $referenceRoot = @($document.SelectNodes('//TerrariaReferencesDirectory'))
            if ($referenceRoot.Count -ne 1 -or
                $referenceRoot[0].InnerText.Trim() -cne '$(MSBuildThisFileDirectory)..\..\external\TerrariaRefs') {
                throw 'TerrariaHost reference root is not in the approved pre-evaluation form.'
            }
        }
    }

    $propsPath = Join-Path $script:RepositoryRoot 'Directory.Build.props'
    $targetsPath = Join-Path $script:RepositoryRoot 'Directory.Build.targets'
    if (-not [System.IO.File]::Exists($propsPath) -or -not [System.IO.File]::Exists($targetsPath)) {
        throw 'Both root Directory.Build.props and the empty Directory.Build.targets import boundary are required.'
    }

    [xml] $targets = [System.IO.File]::ReadAllText($targetsPath)
    if ($null -eq $targets.DocumentElement -or $targets.DocumentElement.LocalName -cne 'Project' -or
        -not [string]::IsNullOrEmpty($targets.DocumentElement.NamespaceURI) -or
        $targets.DocumentElement.Attributes.Count -ne 0 -or $targets.DocumentElement.ChildNodes.Count -ne 0) {
        throw 'Directory.Build.targets must remain an empty unnamespaced Project import boundary.'
    }

    [xml] $props = [System.IO.File]::ReadAllText($propsPath)
    $allowedPropsElements = @(
        'Project', 'PropertyGroup', 'LangVersion', 'Deterministic', 'ContinuousIntegrationBuild',
        'DeterministicSourcePaths', 'DebugType', 'DebugSymbols', 'CodePage', 'PathMap',
        'SourceRevisionId', 'Version', 'AssemblyVersion', 'FileVersion', 'InformationalVersion',
        'IncludeSourceRevisionInInformationalVersion', 'TreatWarningsAsErrors',
        'EnableSourceControlManagerQueries', 'EnableSourceLink',
        'EmbedUntrackedSources', 'PublishRepositoryUrl', 'GenerateRepositoryUrlAttribute',
        'CopyLocalLockFileAssemblies', 'DefaultItemExcludes', 'BaseOutputPath', 'BaseIntermediateOutputPath')
    if ($null -eq $props.DocumentElement -or $props.DocumentElement.LocalName -cne 'Project' -or
        -not [string]::IsNullOrEmpty($props.DocumentElement.NamespaceURI) -or
        $props.DocumentElement.Attributes.Count -ne 0) {
        throw 'Directory.Build.props root is not in the approved pre-evaluation form.'
    }
    foreach ($element in @($props.SelectNodes('//*'))) {
        if ($allowedPropsElements -cnotcontains $element.LocalName -or $element.InnerText.Contains('$([')) {
            throw "Directory.Build.props contains an unapproved or executable element before evaluation: $($element.LocalName)"
        }
        foreach ($attribute in @($element.Attributes)) {
            $allowedCondition = $attribute.LocalName -ceq 'Condition' -and
                (($element.LocalName -ceq 'SourceRevisionId' -and $attribute.Value.Trim() -ceq "'`$(SourceRevisionId)' == ''") -or
                 ($element.LocalName -ceq 'PropertyGroup' -and $attribute.Value.Trim() -ceq "'`$(JueMingRBuildRoot)' != ''"))
            if (-not $allowedCondition -or $attribute.Value.Contains('$([')) {
                throw "Directory.Build.props contains an unapproved attribute: $($element.LocalName).$($attribute.LocalName)"
            }
        }
    }

    $propertyGroups = @($props.DocumentElement.SelectNodes('./PropertyGroup'))
    $unconditionalGroups = @($propertyGroups | Where-Object { $_.Attributes.Count -eq 0 })
    $buildRootGroups = @($propertyGroups | Where-Object {
        $_.Attributes.Count -eq 1 -and
        $_.Attributes[0].LocalName -ceq 'Condition' -and
        $_.Attributes[0].Value.Trim() -ceq "'`$(JueMingRBuildRoot)' != ''"
    })
    if ($propertyGroups.Count -ne 2 -or $unconditionalGroups.Count -ne 1 -or $buildRootGroups.Count -ne 1) {
        throw 'Directory.Build.props must contain exactly the approved unconditional and guarded property groups.'
    }

    $expectedUnconditionalProperties = [ordered]@{
        LangVersion = '7.3'
        Deterministic = 'true'
        ContinuousIntegrationBuild = 'true'
        DeterministicSourcePaths = 'false'
        DebugType = 'portable'
        DebugSymbols = 'true'
        CodePage = '65001'
        PathMap = '$(MSBuildProjectDirectory)=/_/$(MSBuildProjectName)'
        SourceRevisionId = 'unidentified'
        Version = '0.0.0-dev'
        AssemblyVersion = '0.0.0.0'
        FileVersion = '0.0.0.0'
        InformationalVersion = '0.0.0-dev+$(SourceRevisionId)'
        IncludeSourceRevisionInInformationalVersion = 'false'
        EnableSourceControlManagerQueries = 'false'
        EnableSourceLink = 'false'
        EmbedUntrackedSources = 'false'
        PublishRepositoryUrl = 'false'
        GenerateRepositoryUrlAttribute = 'false'
        TreatWarningsAsErrors = 'true'
        CopyLocalLockFileAssemblies = 'false'
        DefaultItemExcludes = '$(DefaultItemExcludes);$(MSBuildProjectDirectory)\bin\**;$(MSBuildProjectDirectory)\obj\**'
    }
    $expectedBuildRootProperties = [ordered]@{
        PathMap = '$(PathMap),$(JueMingRBuildRoot)=/_/build'
        BaseOutputPath = '$(JueMingRBuildRoot)\bin\$(MSBuildProjectName)\'
        BaseIntermediateOutputPath = '$(JueMingRBuildRoot)\obj\$(MSBuildProjectName)\'
    }
    foreach ($groupExpectation in @(
        @{ Group = $unconditionalGroups[0]; Properties = $expectedUnconditionalProperties; Label = 'unconditional' },
        @{ Group = $buildRootGroups[0]; Properties = $expectedBuildRootProperties; Label = 'build-root' })) {
        $childElements = @($groupExpectation.Group.SelectNodes('./*'))
        if ($childElements.Count -ne $groupExpectation.Properties.Count) {
            throw "Directory.Build.props $($groupExpectation.Label) property count is not approved."
        }
        foreach ($propertyName in $groupExpectation.Properties.Keys) {
            $nodes = @($groupExpectation.Group.SelectNodes("./$propertyName"))
            if ($nodes.Count -ne 1 -or $nodes[0].InnerText.Trim() -cne $groupExpectation.Properties[$propertyName]) {
                throw "Directory.Build.props property is not approved: $propertyName"
            }
            if ($propertyName -ceq 'SourceRevisionId') {
                if ($nodes[0].Attributes.Count -ne 1 -or
                    $nodes[0].Attributes[0].LocalName -cne 'Condition' -or
                    $nodes[0].Attributes[0].Value.Trim() -cne "'`$(SourceRevisionId)' == ''") {
                    throw 'Directory.Build.props SourceRevisionId condition is not approved.'
                }
            }
            elseif ($nodes[0].Attributes.Count -ne 0) {
                throw "Directory.Build.props property may not have attributes: $propertyName"
            }
        }
    }

    foreach ($expectedOutputProperty in @(
        @{ Name = 'BaseOutputPath'; Value = '$(JueMingRBuildRoot)\bin\$(MSBuildProjectName)\' },
        @{ Name = 'BaseIntermediateOutputPath'; Value = '$(JueMingRBuildRoot)\obj\$(MSBuildProjectName)\' })) {
        $nodes = @($props.SelectNodes("//$($expectedOutputProperty.Name)"))
        if ($nodes.Count -ne 1 -or $nodes[0].InnerText.Trim() -cne $expectedOutputProperty.Value) {
            throw "Directory.Build.props output routing is not approved: $($expectedOutputProperty.Name)"
        }
    }
}

function Assert-SafeBuildOutput {
    param(
        [string] $Root,
        [string] $OutputRoot,
        [string] $ConfigurationRoot,
        [string] $ReferencesRoot,
        [string] $TerrariaSourceRoot,
        [string] $XnaSourceRoot
    )

    $approvedArtifactsRoot = Get-CanonicalDirectoryPath -Path (Join-Path $Root 'artifacts') -Label 'approved artifacts root'
    if ([string]::Equals($OutputRoot, $approvedArtifactsRoot, [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-PathWithinOrEqual -Candidate $OutputRoot -Parent $approvedArtifactsRoot)) {
        throw 'OutputDirectory must be a dedicated child directory of the repository artifacts root.'
    }

    foreach ($candidate in @(
        @{ Path = $OutputRoot; Label = 'OutputDirectory' },
        @{ Path = $ConfigurationRoot; Label = 'configuration output directory' })) {
        if ([string]::Equals($candidate.Path, $Root, [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-PathWithinOrEqual -Candidate $candidate.Path -Parent $Root)) {
            throw "$($candidate.Label) must be a dedicated directory inside the repository root."
        }

        Assert-PathTreesDisjoint -Candidate $candidate.Path -CandidateLabel $candidate.Label -Protected $ReferencesRoot -ProtectedLabel 'prepared reference directory'
        Assert-PathTreesDisjoint -Candidate $candidate.Path -CandidateLabel $candidate.Label -Protected $TerrariaSourceRoot -ProtectedLabel 'Terraria source directory'
        Assert-PathTreesDisjoint -Candidate $candidate.Path -CandidateLabel $candidate.Label -Protected $XnaSourceRoot -ProtectedLabel 'XNA source directory'
    }

    if ($ConfigurationRoot.Length -le 3) {
        throw 'OutputDirectory is too broad.'
    }
}

function Assert-NoOutputReparsePoints {
    param([string] $Root)

    $directories = New-Object System.Collections.Generic.Stack[string]
    $directories.Push($Root)
    while ($directories.Count -ne 0) {
        $current = $directories.Pop()
        $directory = New-Object System.IO.DirectoryInfo -ArgumentList $current
        foreach ($entry in $directory.EnumerateFileSystemInfos()) {
            if (($entry.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Build output ownership cannot include a reparse point: $($entry.FullName)"
            }
            if (($entry.Attributes -band [System.IO.FileAttributes]::Directory) -ne 0) {
                $directories.Push($entry.FullName)
            }
        }
    }
}

function Assert-OwnedBuildOutput {
    param([string] $ConfigurationRoot)

    $marker = Join-Path $ConfigurationRoot $script:BuildMarkerName
    if (-not [System.IO.File]::Exists($marker) -or
        ([System.IO.File]::GetAttributes($marker) -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 -or
        [System.IO.File]::ReadAllText($marker).Trim() -ne $script:BuildMarkerValue) {
        throw "Refusing to replace an output directory not owned by scripts/build.ps1: $ConfigurationRoot"
    }

    $relativeRoot = Get-RepositoryRelativeFilePath -Path $ConfigurationRoot
    $tracked = @(Get-GitOutput -Arguments @('-c', 'core.quotePath=false', 'ls-files', '--', $relativeRoot))
    if ($tracked.Count -ne 0) {
        throw "Refusing to replace a build output directory that contains Git tracked files: $relativeRoot"
    }

    $allowedRootEntries = @($script:BuildMarkerName, 'build-record.json', 'work')
    $configurationDirectory = New-Object System.IO.DirectoryInfo -ArgumentList $ConfigurationRoot
    foreach ($entry in $configurationDirectory.EnumerateFileSystemInfos()) {
        $isOwnedRecordTemporaryFile = $entry.Name -cmatch '^\.build-record\.[0-9a-f]{32}\.tmp$'
        if ($allowedRootEntries -notcontains $entry.Name -and -not $isOwnedRecordTemporaryFile) {
            throw "Refusing to replace a build output directory with an unexpected root entry: $($entry.Name)"
        }
        if (($entry.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Refusing to replace a build output directory containing a reparse point: $($entry.Name)"
        }
        if ($entry.Name -eq 'work' -and ($entry.Attributes -band [System.IO.FileAttributes]::Directory) -eq 0) {
            throw 'Build output work entry must be a directory.'
        }
        if (($entry.Name -ne 'work' -or $isOwnedRecordTemporaryFile) -and
            ($entry.Attributes -band [System.IO.FileAttributes]::Directory) -ne 0) {
            throw "Build output file entry unexpectedly became a directory: $($entry.Name)"
        }
    }

    $work = Join-Path $ConfigurationRoot 'work'
    if ([System.IO.Directory]::Exists($work)) {
        $workDirectory = New-Object System.IO.DirectoryInfo -ArgumentList $work
        foreach ($entry in $workDirectory.EnumerateFileSystemInfos()) {
            if (($entry.Attributes -band [System.IO.FileAttributes]::Directory) -eq 0 -or
                ($entry.Name -ne 'bin' -and $entry.Name -ne 'obj')) {
                throw "Refusing to replace a build output with an unexpected work entry: $($entry.Name)"
            }
        }
        Assert-NoOutputReparsePoints -Root $work
    }
}

function Remove-OwnedBuildOutput {
    param([string] $ConfigurationRoot)

    if (-not [System.IO.Directory]::Exists($ConfigurationRoot)) {
        return
    }

    Assert-OwnedBuildOutput -ConfigurationRoot $ConfigurationRoot
    [System.IO.Directory]::Delete($ConfigurationRoot, $true)
}

function Test-ForbiddenAssemblySimpleName {
    param([string] $Name)

    return $Name -eq 'Terraria' -or
        $Name -eq 'ReLogic' -or
        $Name -like 'Microsoft.Xna.Framework*' -or
        $Name -eq '0Harmony' -or
        $Name -eq 'TerrariaHelper' -or
        $Name -like 'TerrariaHelper.*' -or
        $Name -eq 'JueMingZ' -or
        $Name -like 'JueMingZ.*'
}

function Assert-FileIsNotForbiddenBinary {
    param(
        [string] $Path,
        [string[]] $ForbiddenHashes,
        [string] $Context
    )

    $name = [System.IO.Path]::GetFileName($Path)
    if ($name -eq 'Terraria.exe' -or
        $name -eq 'ReLogic.dll' -or
        $name -like 'Microsoft.Xna.Framework*.dll' -or
        $name -eq '0Harmony.dll' -or
        $name -like 'TerrariaHelper*.dll' -or
        $name -like 'JueMingZ*.dll') {
        throw "$Context contains a forbidden game, Harmony, helper, or Legacy filename: $name"
    }

    $hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($ForbiddenHashes -contains $hash) {
        throw "$Context contains a forbidden legal-input binary by content hash: $name"
    }

    $assemblyName = $null
    try {
        $assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($Path)
    }
    catch {
        $exception = $_.Exception
        while ($null -ne $exception.InnerException) {
            $exception = $exception.InnerException
        }
        if ($exception -is [System.BadImageFormatException]) {
            return
        }

        throw "$Context managed assembly identity could not be inspected for $name."
    }

    if (Test-ForbiddenAssemblySimpleName -Name $assemblyName.Name) {
        throw "$Context contains a forbidden managed assembly identity: $($assemblyName.Name)"
    }
}

function Get-ForbiddenReferenceHashes {
    param([object] $Baseline)

    $hashes = @($Baseline.files | ForEach-Object { ([string] $_.sha256).ToUpperInvariant() })
    if ($hashes.Count -ne 3 -or @($hashes | Select-Object -Unique).Count -ne 3 -or
        @($hashes | Where-Object { $_ -notmatch '^[0-9A-F]{64}$' }).Count -ne 0) {
        throw 'Reference baseline must contain exactly three unique SHA-256 identities.'
    }

    return $hashes
}

function Assert-NoForbiddenOutput {
    param(
        [string] $ConfigurationRoot,
        [string[]] $ForbiddenHashes
    )

    foreach ($file in Get-ChildItem -LiteralPath $ConfigurationRoot -File -Recurse) {
        Assert-FileIsNotForbiddenBinary -Path $file.FullName -ForbiddenHashes $ForbiddenHashes -Context 'formal build output'
    }
}

function Test-RepositoryFilePath {
    param([string] $Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $false
    }
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $prefix = $script:RepositoryRoot.TrimEnd('\') + '\'
    return $fullPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)
}

function Get-RepositoryRelativeFilePath {
    param([string] $Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-RepositoryFilePath -Path $fullPath)) {
        throw "Effective MSBuild item points outside the repository: $Path"
    }

    return $fullPath.Substring($script:RepositoryRoot.TrimEnd('\').Length).TrimStart('\').Replace('\', '/')
}

function Assert-ExactStringSet {
    param(
        [object[]] $Actual,
        [object[]] $Expected,
        [string] $Label
    )

    $actualValues = @($Actual | Where-Object { $null -ne $_ -and -not [string]::IsNullOrEmpty([string] $_) } | ForEach-Object { [string] $_ })
    $expectedValues = @($Expected | Where-Object { $null -ne $_ -and -not [string]::IsNullOrEmpty([string] $_) } | ForEach-Object { [string] $_ })
    $actualSorted = @($actualValues | Sort-Object)
    $expectedSorted = @($expectedValues | Sort-Object)
    $uniqueActualCount = @($actualValues | Select-Object -Unique).Count
    if ($actualValues.Count -ne $uniqueActualCount -or
        $actualValues.Count -ne $expectedValues.Count -or
        ($actualSorted -join "`n") -cne ($expectedSorted -join "`n")) {
        throw ("{0} does not match the approved effective MSBuild item set; expected [{1}], actual [{2}]." -f `
            $Label,
            ($expectedSorted -join ', '),
            ($actualSorted -join ', '))
    }
}

function Get-MsBuildItemMetadata {
    param(
        [object] $Item,
        [string] $Name
    )

    $property = $Item.PSObject.Properties[$Name]
    return $(if ($null -eq $property) { '' } else { [string] $property.Value })
}

function Get-MsBuildItems {
    param(
        [object] $Evaluation,
        [string] $Name
    )

    $property = $Evaluation.Items.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) {
        return @()
    }
    return @($property.Value)
}

function Get-ValidatedCommandPath {
    param(
        [string] $Name,
        [string] $Label
    )

    $commands = @(Get-Command $Name -CommandType Application -ErrorAction Stop)
    if ($commands.Count -eq 0) {
        throw "$Label is unavailable."
    }
    $candidate = [System.IO.Path]::GetFullPath([string] $commands[0].Source)
    $item = Get-Item -LiteralPath $candidate -Force
    if (-not ($item -is [System.IO.FileInfo]) -or
        -not [string]::Equals($item.Name, $Name, [StringComparison]::OrdinalIgnoreCase) -or
        ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "$Label must resolve to a regular, non-reparse executable file."
    }
    $directory = Get-CanonicalDirectoryPath `
        -Path $item.DirectoryName `
        -Label "$Label directory" `
        -RequireAbsolute
    return Join-Path $directory $item.Name
}

function Assert-ExactProcessEnvironment {
    param([string[]] $AllowedNames)

    $allowed = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($name in $AllowedNames) {
        if (-not $allowed.Add($name)) {
            throw "The locked process environment allowlist contains a duplicate name: $name"
        }
    }
    $actualNames = @(Get-ChildItem Env: | ForEach-Object { $_.Name })
    foreach ($name in $actualNames) {
        if (-not $allowed.Contains($name)) {
            throw "The locked process environment contains an unapproved variable: $name"
        }
    }
    if ($actualNames.Count -ne $allowed.Count) {
        throw 'The locked process environment is missing one or more approved variables.'
    }
}

function Get-CompleteEnvironmentSnapshot {
    $snapshot = [ordered]@{}
    foreach ($item in @(Get-ChildItem Env:)) {
        $snapshot[$item.Name] = [ordered]@{ exists = $true; value = [string] $item.Value }
    }
    return $snapshot
}

function Enter-LockedBuildEnvironment {
    param(
        [object] $Snapshot,
        [string] $ScratchRoot,
        [string] $DotnetRoot,
        [string] $GitDirectory,
        [string] $WindowsRoot,
        [string] $PowerShellHome,
        [string] $ProgramFilesRoot,
        [string] $ProgramFilesX86Root,
        [string] $ProgramDataRoot
    )

    if ($null -eq $Snapshot) {
        throw 'A complete original environment snapshot is required.'
    }
    foreach ($name in @($Snapshot.Keys)) {
        Remove-Item -LiteralPath ("Env:$name") -ErrorAction SilentlyContinue
    }

    $system32 = Get-CanonicalDirectoryPath -Path (Join-Path $WindowsRoot 'System32') -Label 'Windows System32 root'
    $comSpec = Join-Path $system32 'cmd.exe'
    if (-not [System.IO.File]::Exists($comSpec)) {
        throw 'The locked Windows command processor is missing.'
    }
    $pathEntries = @(
        $GitDirectory,
        $DotnetRoot,
        $system32,
        $WindowsRoot,
        (Join-Path $system32 'Wbem'),
        (Join-Path $system32 'WindowsPowerShell\v1.0')) |
        Where-Object { [System.IO.Directory]::Exists($_) } |
        Select-Object -Unique
    $moduleEntries = @(
        (Join-Path $PowerShellHome 'Modules'),
        (Join-Path $system32 'WindowsPowerShell\v1.0\Modules')) |
        Where-Object { [System.IO.Directory]::Exists($_) } |
        Select-Object -Unique

    $values = [ordered]@{
        SystemRoot = $WindowsRoot
        WINDIR = $WindowsRoot
        ComSpec = $comSpec
        PATH = ($pathEntries -join ';')
        PATHEXT = '.COM;.EXE;.BAT;.CMD'
        PSModulePath = ($moduleEntries -join ';')
        ProgramFiles = $ProgramFilesRoot
        'ProgramFiles(x86)' = $ProgramFilesX86Root
        ProgramData = $ProgramDataRoot
        USERPROFILE = (Join-Path $ScratchRoot 'user-profile')
        HOME = (Join-Path $ScratchRoot 'user-profile')
        APPDATA = (Join-Path $ScratchRoot 'appdata-roaming')
        LOCALAPPDATA = (Join-Path $ScratchRoot 'appdata-local')
        TEMP = (Join-Path $ScratchRoot 'temp')
        TMP = (Join-Path $ScratchRoot 'temp')
        DOTNET_ROOT = $DotnetRoot
        DOTNET_MULTILEVEL_LOOKUP = '0'
        DOTNET_CLI_HOME = (Join-Path $ScratchRoot 'cli-home')
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_NOLOGO = '1'
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
        DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = '1'
        NUGET_PACKAGES = (Join-Path $ScratchRoot 'nuget-packages')
        NUGET_HTTP_CACHE_PATH = (Join-Path $ScratchRoot 'nuget-http-cache')
        NUGET_SCRATCH = (Join-Path $ScratchRoot 'nuget-scratch')
        NUGET_PLUGINS_CACHE_PATH = (Join-Path $ScratchRoot 'nuget-plugins-cache')
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
    foreach ($name in $values.Keys) {
        Set-Item -LiteralPath ("Env:$name") -Value ([string] $values[$name])
    }
    Assert-ExactProcessEnvironment -AllowedNames @($values.Keys)
}

function Add-LockedMsBuildEnvironment {
    param([string] $SdkRoot)

    $values = [ordered]@{
        MSBuildSDKsPath = (Join-Path $SdkRoot 'Sdks')
        MSBuildExtensionsPath = $SdkRoot
        MSBuildExtensionsPath32 = $SdkRoot
        MSBuildExtensionsPath64 = $SdkRoot
        MSBuildUserExtensionsPath = $SdkRoot
    }
    foreach ($name in $values.Keys) {
        Set-Item -LiteralPath ("Env:$name") -Value ([string] $values[$name])
    }
    $allowedNames = @(
        'SystemRoot', 'WINDIR', 'ComSpec', 'PATH', 'PATHEXT', 'PSModulePath',
        'ProgramFiles', 'ProgramFiles(x86)', 'ProgramData', 'USERPROFILE', 'HOME', 'APPDATA', 'LOCALAPPDATA',
        'TEMP', 'TMP',
        'DOTNET_ROOT', 'DOTNET_MULTILEVEL_LOOKUP', 'DOTNET_CLI_HOME', 'DOTNET_CLI_TELEMETRY_OPTOUT',
        'DOTNET_NOLOGO', 'DOTNET_SKIP_FIRST_TIME_EXPERIENCE', 'DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE',
        'NUGET_PACKAGES', 'NUGET_HTTP_CACHE_PATH', 'NUGET_SCRATCH', 'NUGET_PLUGINS_CACHE_PATH',
        'GIT_CONFIG_NOSYSTEM', 'GIT_CONFIG_GLOBAL', 'GIT_OPTIONAL_LOCKS', 'GIT_TERMINAL_PROMPT',
        'GIT_CONFIG_COUNT', 'GIT_CONFIG_KEY_0', 'GIT_CONFIG_VALUE_0', 'GIT_CONFIG_KEY_1', 'GIT_CONFIG_VALUE_1') +
        @($values.Keys)
    Assert-ExactProcessEnvironment -AllowedNames $allowedNames
}

function Get-FileSystemEntryInfo {
    param([string] $Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $parent = [System.IO.Path]::GetDirectoryName($fullPath)
    $leaf = [System.IO.Path]::GetFileName($fullPath)
    if ([string]::IsNullOrWhiteSpace($parent) -or
        [string]::IsNullOrWhiteSpace($leaf) -or
        -not [System.IO.Directory]::Exists($parent)) {
        return $null
    }
    $parentInfo = New-Object System.IO.DirectoryInfo -ArgumentList $parent
    foreach ($entry in $parentInfo.EnumerateFileSystemInfos()) {
        if ([string]::Equals($entry.Name, $leaf, [StringComparison]::OrdinalIgnoreCase)) {
            return $entry
        }
    }
    return $null
}

function Test-FileSystemEntryExists {
    param([string] $Path)

    return $null -ne (Get-FileSystemEntryInfo -Path $Path)
}

function Remove-OwnedDotnetStateRoot {
    param(
        [string] $ScratchRoot,
        [string] $SystemTempRoot
    )

    if ([string]::IsNullOrWhiteSpace($ScratchRoot)) {
        return
    }
    $originalScratch = [System.IO.Path]::GetFullPath($ScratchRoot)
    $scratchEntry = Get-FileSystemEntryInfo -Path $originalScratch
    if ($null -eq $scratchEntry) { return }
    if (($scratchEntry.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'The owned dotnet state root itself may not be a reparse point.'
    }
    if (($scratchEntry.Attributes -band [System.IO.FileAttributes]::Directory) -eq 0 -or
        -not [System.IO.Directory]::Exists($originalScratch) -or
        [System.IO.File]::Exists($originalScratch)) {
        throw 'The owned dotnet state root was replaced by a non-directory file-system entry.'
    }
    $canonicalScratch = Get-CanonicalDirectoryPath -Path $originalScratch -Label 'dotnet state scratch root' -RequireAbsolute
    $canonicalTemp = Get-CanonicalDirectoryPath -Path $SystemTempRoot -Label 'system TEMP root' -RequireAbsolute
    $scratchParent = Get-CanonicalDirectoryPath `
        -Path ([System.IO.Path]::GetDirectoryName($canonicalScratch)) `
        -Label 'dotnet state scratch parent' `
        -RequireAbsolute
    if (-not [string]::Equals($scratchParent, $canonicalTemp, [StringComparison]::OrdinalIgnoreCase) -or
        -not [System.IO.Path]::GetFileName($canonicalScratch).StartsWith('JueMingR-DotnetState-', [StringComparison]::Ordinal)) {
        throw 'Refusing to clean a dotnet state directory outside the owned system TEMP root.'
    }
    foreach ($entry in Get-ChildItem -LiteralPath $canonicalScratch -Force -Recurse) {
        if (($entry.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'Refusing to clean a dotnet state directory containing a reparse point.'
        }
    }
    Remove-Item -LiteralPath $canonicalScratch -Recurse -Force
    if (Test-FileSystemEntryExists -Path $originalScratch) {
        throw 'The owned dotnet state file-system entry still exists after cleanup.'
    }
    [System.Threading.Thread]::Sleep(250)
    if (Test-FileSystemEntryExists -Path $originalScratch) {
        throw 'The owned dotnet state file-system entry reappeared after cleanup; a build child process may still be writing.'
    }
}

function Restore-CompleteEnvironment {
    param([object] $Snapshot)

    if ($null -eq $Snapshot) {
        return
    }
    foreach ($item in @(Get-ChildItem Env:)) {
        Remove-Item -LiteralPath ("Env:$($item.Name)") -ErrorAction SilentlyContinue
    }
    foreach ($name in $Snapshot.Keys) {
        $originalValue = [string] $Snapshot[$name].value
        if ($originalValue.Length -eq 0) {
            if (-not [JueMingR.PhysicalPathNativeMethods]::SetEnvironmentVariable($name, '')) {
                throw "Could not restore an originally empty process environment value: $name."
            }
        }
        else {
            Set-Item -LiteralPath ("Env:$name") -Value $originalValue
        }
    }
    $restoredNames = @(Get-ChildItem Env: | ForEach-Object { $_.Name })
    if ($restoredNames.Count -ne $Snapshot.Count) {
        $expectedNames = @($Snapshot.Keys)
        $missingNames = @($expectedNames | Where-Object { $_ -notin $restoredNames })
        $unexpectedNames = @($restoredNames | Where-Object { $_ -notin $expectedNames })
        throw ("The original process environment was not restored exactly (expected count {0}, actual count {1}, missing [{2}], unexpected [{3}])." -f `
            $Snapshot.Count,
            $restoredNames.Count,
            ($missingNames -join ', '),
            ($unexpectedNames -join ', '))
    }
    foreach ($name in $Snapshot.Keys) {
        $restored = Get-Item -LiteralPath ("Env:$name") -ErrorAction SilentlyContinue
        if ($null -eq $restored -or [string] $restored.Value -cne [string] $Snapshot[$name].value) {
            throw "The original process environment value was not restored exactly: $name."
        }
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

function Get-MsBuildIsolationProperties {
    param(
        [string] $SdkRoot,
        [string] $TargetingPackRoot
    )

    $sdkAfterDirectoryBuildProps = Join-Path $SdkRoot 'Sdks\Microsoft.NET.Sdk\Sdk\UseArtifactsOutputPath.props'
    if (-not [System.IO.File]::Exists($sdkAfterDirectoryBuildProps)) {
        throw 'The locked SDK internal Directory.Build.props follow-up import is missing.'
    }

    $properties = @(
        ('-p:DirectoryBuildPropsPath={0}' -f (Join-Path $script:RepositoryRoot 'Directory.Build.props')),
        ('-p:DirectoryBuildTargetsPath={0}' -f (Join-Path $script:RepositoryRoot 'Directory.Build.targets')),
        '-p:ImportDirectoryBuildProps=true',
        '-p:ImportDirectoryBuildTargets=true',
        '-p:ImportDirectorySolutionProps=false',
        '-p:ImportDirectorySolutionTargets=false',
        '-p:DirectorySolutionPropsPath=',
        '-p:DirectorySolutionTargetsPath=',
        '-p:ImportDirectoryPackagesProps=false',
        '-p:DirectoryPackagesPropsPath=',
        '-p:ImportProjectExtensionProps=false',
        '-p:ImportProjectExtensionTargets=false',
        '-p:AlternateCommonProps=',
        '-p:CustomBeforeDirectoryBuildProps=',
        ('-p:CustomAfterDirectoryBuildProps={0}' -f $sdkAfterDirectoryBuildProps),
        '-p:CustomBeforeDirectoryBuildTargets=',
        '-p:CustomAfterDirectoryBuildTargets=',
        '-p:CustomBeforeMicrosoftCommonProps=',
        '-p:CustomAfterMicrosoftCommonProps=',
        '-p:CustomBeforeMicrosoftCommonTargets=',
        '-p:CustomAfterMicrosoftCommonTargets=',
        '-p:CustomBeforeMicrosoftCSharpTargets=',
        '-p:CustomAfterMicrosoftCSharpTargets=',
        '-p:CustomBeforeMicrosoftCommonCrossTargetingTargets=',
        '-p:CustomAfterMicrosoftCommonCrossTargetingTargets=',
        ('-p:MSBuildSDKsPath={0}' -f (Join-Path $SdkRoot 'Sdks')),
        ('-p:MSBuildExtensionsPath={0}' -f $SdkRoot),
        ('-p:MSBuildExtensionsPath32={0}' -f $SdkRoot),
        ('-p:MSBuildExtensionsPath64={0}' -f $SdkRoot),
        ('-p:MSBuildUserExtensionsPath={0}' -f $SdkRoot),
        ('-p:RoslynTargetsPath={0}' -f (Join-Path $SdkRoot 'Roslyn')),
        ('-p:CSharpCoreTargetsPath={0}' -f (Join-Path $SdkRoot 'Roslyn\Microsoft.CSharp.Core.targets')),
        '-p:CscToolPath=',
        '-p:CscToolExe=',
        '-p:UseSharedCompilation=false',
        '-p:EnableSourceControlManagerQueries=false',
        '-p:EnableSourceLink=false',
        '-p:EmbedUntrackedSources=false',
        '-p:PublishRepositoryUrl=false',
        '-p:GenerateRepositoryUrlAttribute=false',
        '-p:ErrorLog=',
        '-p:DocumentationFile=',
        '-p:GenerateDocumentationFile=false',
        '-p:EmitCompilerGeneratedFiles=false',
        '-p:CompilerGeneratedFilesOutputPath=',
        '-p:PdbFile=',
        '-p:PreBuildEvent=',
        '-p:PostBuildEvent=',
        '-p:RunPostBuildEvent=Never',
        '-p:GeneratePackageOnBuild=false',
        '-p:DeployOnBuild=false',
        '-p:RestoreGraphOutputPath=',
        ('-p:FrameworkPathOverride={0}' -f $TargetingPackRoot),
        '-p:RestoreSources=',
        '-p:RestoreAdditionalProjectSources=',
        '-p:RestoreFallbackFolders='
    )
    foreach ($propertyName in @(
        'ImportByWildcardBeforeMicrosoftCommonProps',
        'ImportByWildcardAfterMicrosoftCommonProps',
        'ImportUserLocationsByWildcardBeforeMicrosoftCommonProps',
        'ImportUserLocationsByWildcardAfterMicrosoftCommonProps',
        'ImportByWildcardBeforeMicrosoftCommonTargets',
        'ImportByWildcardAfterMicrosoftCommonTargets',
        'ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets',
        'ImportUserLocationsByWildcardAfterMicrosoftCommonTargets',
        'ImportByWildcardBeforeMicrosoftCSharpTargets',
        'ImportByWildcardAfterMicrosoftCSharpTargets',
        'ImportUserLocationsByWildcardBeforeMicrosoftCSharpTargets',
        'ImportUserLocationsByWildcardAfterMicrosoftCSharpTargets',
        'ImportByWildcardBeforeMicrosoftNetFrameworkProps',
        'ImportByWildcardAfterMicrosoftNetFrameworkProps',
        'ImportUserLocationsByWildcardBeforeMicrosoftNetFrameworkProps',
        'ImportUserLocationsByWildcardAfterMicrosoftNetFrameworkProps',
        'ImportByWildcardBeforeMicrosoftNetFrameworkTargets',
        'ImportByWildcardAfterMicrosoftNetFrameworkTargets',
        'ImportUserLocationsByWildcardBeforeMicrosoftNetFrameworkTargets',
        'ImportUserLocationsByWildcardAfterMicrosoftNetFrameworkTargets',
        'ImportByWildcardBeforeMicrosoftCommonCrossTargetingTargets',
        'ImportByWildcardAfterMicrosoftCommonCrossTargetingTargets',
        'ImportByWildcardBeforeMicrosoftVisualBasicTargets',
        'ImportByWildcardAfterMicrosoftVisualBasicTargets',
        'ImportUserLocationsByWildcardBeforeMicrosoftVisualBasicTargets',
        'ImportUserLocationsByWildcardAfterMicrosoftVisualBasicTargets')) {
        $properties += "-p:$propertyName=false"
    }
    return $properties
}

function Get-EvaluatedProjectBuildFacts {
    param(
        [string] $Configuration,
        [string] $ReferencesRoot,
        [string] $WorkRoot,
        [string] $SourceRevisionId,
        [string] $SdkRoot,
        [string] $TargetingPackRoot,
        [string[]] $RecordedSourcePaths
    )

    $propertyNames = @(
        'TargetFramework',
        'PlatformTarget',
        'LangVersion',
        'OutputType',
        'AssemblyName',
        'RootNamespace',
        'GenerateAssemblyInfo',
        'AllowUnsafeBlocks',
        'TreatWarningsAsErrors',
        'UsingMicrosoftNETSdk',
        'Deterministic',
        'ContinuousIntegrationBuild',
        'DeterministicSourcePaths',
        'DebugType',
        'DebugSymbols',
        'CodePage',
        'PathMap',
        'SourceRevisionId',
        'Version',
        'AssemblyVersion',
        'FileVersion',
        'InformationalVersion',
        'IncludeSourceRevisionInInformationalVersion',
        'EnableSourceControlManagerQueries',
        'EnableSourceLink',
        'EmbedUntrackedSources',
        'PublishRepositoryUrl',
        'GenerateRepositoryUrlAttribute',
        'RepositoryUrl',
        'PrivateRepositoryUrl',
        'ScmRepositoryUrl',
        'SourceLink',
        'CopyLocalLockFileAssemblies',
        'UseSharedCompilation',
        'ErrorLog',
        'DocumentationFile',
        'GenerateDocumentationFile',
        'EmitCompilerGeneratedFiles',
        'CompilerGeneratedFilesOutputPath',
        'PdbFile',
        'PreBuildEvent',
        'PostBuildEvent',
        'RunPostBuildEvent',
        'GeneratePackageOnBuild',
        'DeployOnBuild',
        'RestoreGraphOutputPath',
        'CscToolPath',
        'CscToolExe',
        'RoslynTargetsPath',
        'CSharpCoreTargetsPath',
        'FrameworkPathOverride',
        'MSBuildSDKsPath',
        'MSBuildExtensionsPath',
        'MSBuildExtensionsPath32',
        'MSBuildExtensionsPath64',
        'MSBuildUserExtensionsPath',
        'MSBuildToolsPath',
        'MSBuildBinPath',
        'MSBuildRuntimeType',
        'NETCoreSdkVersion',
        'ImportDirectoryPackagesProps',
        'DirectoryPackagesPropsPath',
        'BaseOutputPath',
        'OutputPath',
        'OutDir',
        'BaseIntermediateOutputPath',
        'IntermediateOutputPath',
        'MSBuildProjectExtensionsPath'
    )
    $wildcardPropertyNames = @(
        'ImportByWildcardBeforeMicrosoftCommonProps',
        'ImportByWildcardAfterMicrosoftCommonProps',
        'ImportUserLocationsByWildcardBeforeMicrosoftCommonProps',
        'ImportUserLocationsByWildcardAfterMicrosoftCommonProps',
        'ImportByWildcardBeforeMicrosoftCommonTargets',
        'ImportByWildcardAfterMicrosoftCommonTargets',
        'ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets',
        'ImportUserLocationsByWildcardAfterMicrosoftCommonTargets',
        'ImportByWildcardBeforeMicrosoftCSharpTargets',
        'ImportByWildcardAfterMicrosoftCSharpTargets',
        'ImportUserLocationsByWildcardBeforeMicrosoftCSharpTargets',
        'ImportUserLocationsByWildcardAfterMicrosoftCSharpTargets',
        'ImportByWildcardBeforeMicrosoftNetFrameworkProps',
        'ImportByWildcardAfterMicrosoftNetFrameworkProps',
        'ImportUserLocationsByWildcardBeforeMicrosoftNetFrameworkProps',
        'ImportUserLocationsByWildcardAfterMicrosoftNetFrameworkProps',
        'ImportByWildcardBeforeMicrosoftNetFrameworkTargets',
        'ImportByWildcardAfterMicrosoftNetFrameworkTargets',
        'ImportUserLocationsByWildcardBeforeMicrosoftNetFrameworkTargets',
        'ImportUserLocationsByWildcardAfterMicrosoftNetFrameworkTargets',
        'ImportByWildcardBeforeMicrosoftCommonCrossTargetingTargets',
        'ImportByWildcardAfterMicrosoftCommonCrossTargetingTargets',
        'ImportByWildcardBeforeMicrosoftVisualBasicTargets',
        'ImportByWildcardAfterMicrosoftVisualBasicTargets',
        'ImportUserLocationsByWildcardBeforeMicrosoftVisualBasicTargets',
        'ImportUserLocationsByWildcardAfterMicrosoftVisualBasicTargets')
    $propertyNames += $wildcardPropertyNames
    $isolationProperties = @(Get-MsBuildIsolationProperties -SdkRoot $SdkRoot -TargetingPackRoot $TargetingPackRoot)
    $result = New-Object System.Collections.Generic.List[object]
    foreach ($relativePath in $script:ProjectPaths) {
        $absolutePath = Join-Path $script:RepositoryRoot $relativePath
        $arguments = @(
            'msbuild',
            $absolutePath,
            '-noAutoResponse',
            '-nologo',
            '-nodeReuse:false',
            ("-p:Configuration=$Configuration"),
            '-p:Platform=x86',
            ("-p:TerrariaReferencesDirectory=$ReferencesRoot"),
            ("-p:JueMingRBuildRoot=$WorkRoot"),
            ("-p:SourceRevisionId=$SourceRevisionId")
        ) + $isolationProperties + @(
            ('-getProperty:' + ($propertyNames -join ',')),
            '-getItem:ProjectReference,Reference,PackageReference,Analyzer,COMReference,COMFileReference,NativeReference,Compile,EmbeddedResource,AdditionalFiles,EditorConfigFiles,AnalyzerConfigFiles'
        )
        $output = @(& $script:DotnetPath @arguments 2>&1)
        if ($LASTEXITCODE -ne 0) {
            throw "Could not evaluate effective MSBuild properties for $relativePath."
        }

        try {
            $evaluation = (($output -join [Environment]::NewLine) | ConvertFrom-Json)
            $properties = $evaluation.Properties
        }
        catch {
            throw "MSBuild property output was not valid JSON for $relativePath."
        }
        if ($null -eq $properties) {
            throw "MSBuild did not return effective properties for $relativePath."
        }

        $expectedName = [System.IO.Path]::GetFileNameWithoutExtension($relativePath)
        $expectedOutputType = if ($relativePath -eq 'tests/JueMingR.ArchitectureTests/JueMingR.ArchitectureTests.csproj') { 'Exe' } else { 'Library' }
        $expectedPathMap = ('{0}=/_/{1},{2}=/_/build' -f `
            [System.IO.Path]::GetDirectoryName($absolutePath),
            $expectedName,
            $WorkRoot)
        if ([string] $properties.TargetFramework -cne 'net472' -or
            [string] $properties.PlatformTarget -ine 'x86' -or
            [string] $properties.LangVersion -cne '7.3' -or
            [string] $properties.OutputType -ine $expectedOutputType -or
            [string] $properties.AssemblyName -cne $expectedName -or
            [string] $properties.RootNamespace -cne $expectedName -or
            [string] $properties.GenerateAssemblyInfo -ine 'true' -or
            [string] $properties.AllowUnsafeBlocks -ine 'false' -or
            [string] $properties.TreatWarningsAsErrors -ine 'true' -or
            [string] $properties.UsingMicrosoftNETSdk -ine 'true' -or
            [string] $properties.Deterministic -ine 'true' -or
            [string] $properties.ContinuousIntegrationBuild -ine 'true' -or
            [string] $properties.DeterministicSourcePaths -ine 'false' -or
            [string] $properties.DebugType -cne 'portable' -or
            [string] $properties.DebugSymbols -ine 'true' -or
            [string] $properties.CodePage -cne '65001' -or
            [string] $properties.PathMap -cne $expectedPathMap -or
            [string] $properties.SourceRevisionId -cne $SourceRevisionId -or
            [string] $properties.Version -cne '0.0.0-dev' -or
            [string] $properties.AssemblyVersion -cne '0.0.0.0' -or
            [string] $properties.FileVersion -cne '0.0.0.0' -or
            [string] $properties.InformationalVersion -cne ('0.0.0-dev+' + $SourceRevisionId) -or
            [string] $properties.IncludeSourceRevisionInInformationalVersion -ine 'false' -or
            [string] $properties.EnableSourceControlManagerQueries -ine 'false' -or
            [string] $properties.EnableSourceLink -ine 'false' -or
            [string] $properties.EmbedUntrackedSources -ine 'false' -or
            [string] $properties.PublishRepositoryUrl -ine 'false' -or
            [string] $properties.GenerateRepositoryUrlAttribute -ine 'false' -or
            -not [string]::IsNullOrEmpty([string] $properties.RepositoryUrl) -or
            -not [string]::IsNullOrEmpty([string] $properties.PrivateRepositoryUrl) -or
            -not [string]::IsNullOrEmpty([string] $properties.ScmRepositoryUrl) -or
            -not [string]::IsNullOrEmpty([string] $properties.SourceLink) -or
            [string] $properties.CopyLocalLockFileAssemblies -ine 'false' -or
            [string] $properties.UseSharedCompilation -ine 'false' -or
            -not [string]::IsNullOrEmpty([string] $properties.ErrorLog) -or
            -not [string]::IsNullOrEmpty([string] $properties.DocumentationFile) -or
            [string] $properties.GenerateDocumentationFile -ine 'false' -or
            [string] $properties.EmitCompilerGeneratedFiles -ine 'false' -or
            -not [string]::IsNullOrEmpty([string] $properties.CompilerGeneratedFilesOutputPath) -or
            -not [string]::IsNullOrEmpty([string] $properties.PdbFile) -or
            -not [string]::IsNullOrEmpty([string] $properties.PreBuildEvent) -or
            -not [string]::IsNullOrEmpty([string] $properties.PostBuildEvent) -or
            [string] $properties.RunPostBuildEvent -ine 'Never' -or
            [string] $properties.GeneratePackageOnBuild -ine 'false' -or
            [string] $properties.DeployOnBuild -ine 'false' -or
            -not [string]::IsNullOrEmpty([string] $properties.RestoreGraphOutputPath) -or
            -not [string]::IsNullOrEmpty([string] $properties.CscToolPath) -or
            -not [string]::IsNullOrEmpty([string] $properties.CscToolExe) -or
            [string] $properties.MSBuildRuntimeType -cne 'Core' -or
            [string] $properties.NETCoreSdkVersion -cne ([System.IO.Path]::GetFileName($SdkRoot)) -or
            [string] $properties.ImportDirectoryPackagesProps -ine 'false' -or
            -not [string]::IsNullOrEmpty([string] $properties.DirectoryPackagesPropsPath)) {
            throw "Effective MSBuild properties violate the Phase 0-R contract for $relativePath."
        }

        $expectedDirectoryProperties = [ordered]@{
            RoslynTargetsPath = Join-Path $SdkRoot 'Roslyn'
            FrameworkPathOverride = $TargetingPackRoot
            MSBuildSDKsPath = Join-Path $SdkRoot 'Sdks'
            MSBuildExtensionsPath = $SdkRoot
            MSBuildExtensionsPath32 = $SdkRoot
            MSBuildExtensionsPath64 = $SdkRoot
            MSBuildUserExtensionsPath = $SdkRoot
            MSBuildToolsPath = $SdkRoot
            MSBuildBinPath = $SdkRoot
        }
        foreach ($propertyName in $expectedDirectoryProperties.Keys) {
            $actualDirectory = Get-CanonicalDirectoryPath -Path ([string] $properties.$propertyName) -Label "$relativePath effective $propertyName"
            $expectedDirectory = Get-CanonicalDirectoryPath -Path $expectedDirectoryProperties[$propertyName] -Label "$relativePath expected $propertyName"
            if (-not [string]::Equals($actualDirectory, $expectedDirectory, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Effective MSBuild toolchain directory is not locked for $relativePath -> $propertyName."
            }
        }
        $actualCSharpCoreTargets = [System.IO.Path]::GetFullPath([string] $properties.CSharpCoreTargetsPath)
        $expectedCSharpCoreTargets = [System.IO.Path]::GetFullPath((Join-Path $SdkRoot 'Roslyn\Microsoft.CSharp.Core.targets'))
        if (-not [System.IO.File]::Exists($actualCSharpCoreTargets) -or
            -not [string]::Equals($actualCSharpCoreTargets, $expectedCSharpCoreTargets, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Effective C# compiler targets are not locked for $relativePath."
        }

        foreach ($propertyName in $wildcardPropertyNames) {
            $property = $properties.PSObject.Properties[$propertyName]
            if ($null -eq $property -or [string] $property.Value -ine 'false') {
                throw "Effective MSBuild wildcard import switch is not disabled for $relativePath -> $propertyName."
            }
        }

        $expectedPaths = [ordered]@{
            BaseOutputPath = Join-Path $WorkRoot ("bin\$expectedName")
            OutputPath = Join-Path $WorkRoot ("bin\$expectedName\x86\$Configuration\net472")
            BaseIntermediateOutputPath = Join-Path $WorkRoot ("obj\$expectedName")
            IntermediateOutputPath = Join-Path $WorkRoot ("obj\$expectedName\x86\$Configuration\net472")
            MSBuildProjectExtensionsPath = Join-Path $WorkRoot ("obj\$expectedName")
        }
        $actualOutDir = Get-CanonicalDirectoryPath -Path ([string] $properties.OutDir) -Label "$relativePath effective OutDir"
        $expectedOutDir = Get-CanonicalDirectoryPath `
            -Path (Join-Path $WorkRoot ("bin\$expectedName\x86\$Configuration\net472")) `
            -Label "$relativePath expected OutDir"
        if (-not [string]::Equals($actualOutDir, $expectedOutDir, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Effective MSBuild OutDir escapes the guarded work root for $relativePath."
        }
        $safeOutputPaths = [ordered]@{}
        foreach ($propertyName in $expectedPaths.Keys) {
            $actualPath = Get-CanonicalDirectoryPath -Path ([string] $properties.$propertyName) -Label "$relativePath effective $propertyName"
            $expectedPath = Get-CanonicalDirectoryPath -Path $expectedPaths[$propertyName] -Label "$relativePath expected $propertyName"
            if (-not [string]::Equals($actualPath, $expectedPath, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Effective MSBuild output routing escapes the guarded work root for $relativePath -> $propertyName."
            }
            $recordPropertyName = if ($propertyName -eq 'MSBuildProjectExtensionsPath') {
                'msbuildProjectExtensionsPath'
            }
            else {
                $propertyName.Substring(0, 1).ToLowerInvariant() + $propertyName.Substring(1)
            }
            $safeOutputPaths[$recordPropertyName] =
                $expectedPath.Substring($WorkRoot.TrimEnd('\').Length).TrimStart('\').Replace('\', '/')
        }

        $baseIntermediateRoot = Get-CanonicalDirectoryPath `
            -Path $expectedPaths.BaseIntermediateOutputPath `
            -Label "$relativePath approved base intermediate root"
        $intermediateRoots = @(
            $baseIntermediateRoot,
            (Get-CanonicalDirectoryPath -Path $expectedPaths.IntermediateOutputPath -Label "$relativePath approved intermediate root"),
            (Get-CanonicalDirectoryPath -Path $expectedPaths.MSBuildProjectExtensionsPath -Label "$relativePath approved project extensions root"))
        $effectiveInputs = [ordered]@{}
        $effectiveInputIdentityParts = New-Object System.Collections.Generic.List[string]
        foreach ($itemDefinition in @(
            @{ ItemName = 'Compile'; RecordName = 'compile' },
            @{ ItemName = 'EmbeddedResource'; RecordName = 'embeddedResource' },
            @{ ItemName = 'AdditionalFiles'; RecordName = 'additionalFiles' },
            @{ ItemName = 'EditorConfigFiles'; RecordName = 'editorConfigFiles' },
            @{ ItemName = 'AnalyzerConfigFiles'; RecordName = 'analyzerConfigFiles' })) {
            $approvedInputs = @(Get-ApprovedEffectiveInputPaths `
                -Evaluation $evaluation `
                -ItemName $itemDefinition.ItemName `
                -ProjectPath $relativePath `
                -RecordedSourcePaths $RecordedSourcePaths `
                -BaseIntermediateRoot $baseIntermediateRoot `
                -IntermediateRoots $intermediateRoots `
                -SdkRoot $SdkRoot)
            $effectiveInputs[$itemDefinition.RecordName] = $approvedInputs
            foreach ($input in $approvedInputs) {
                $effectiveInputIdentityParts.Add(("{0}|{1}|{2}|{3}|{4}" -f $relativePath, $itemDefinition.ItemName, $input.origin, $input.path, $input.sha256))
            }
        }
        $effectiveInputSetSha256 = Get-TextSha256 -Text ((@($effectiveInputIdentityParts.ToArray()) | Sort-Object) -join "`n")

        $effectiveProjectReferences = New-Object System.Collections.Generic.List[string]
        foreach ($item in @(Get-MsBuildItems -Evaluation $evaluation -Name 'ProjectReference')) {
            $effectiveProjectReferences.Add((Get-RepositoryRelativeFilePath -Path (Get-MsBuildItemMetadata -Item $item -Name 'FullPath')))
            $referenceOutputProperty = $item.PSObject.Properties['ReferenceOutputAssembly']
            if ($null -ne $referenceOutputProperty -and
                -not [string]::IsNullOrWhiteSpace([string] $referenceOutputProperty.Value) -and
                [string] $referenceOutputProperty.Value -ine 'true') {
                throw "ProjectReference disables its effective assembly edge in $relativePath."
            }
        }
        Assert-ExactStringSet -Actual @($effectiveProjectReferences.ToArray()) -Expected @($script:ExpectedProjectReferences[$relativePath]) -Label "$relativePath ProjectReference"

        $directAssemblyReferences = New-Object System.Collections.Generic.List[object]
        foreach ($item in @(Get-MsBuildItems -Evaluation $evaluation -Name 'Reference')) {
            $definingProjectFullPath = Get-MsBuildItemMetadata -Item $item -Name 'DefiningProjectFullPath'
            if (-not (Test-RepositoryFilePath -Path $definingProjectFullPath)) {
                if ([string]::IsNullOrWhiteSpace($definingProjectFullPath) -or
                    -not (Test-PathWithinOrEqual -Candidate ([System.IO.Path]::GetFullPath($definingProjectFullPath)) -Parent $SdkRoot)) {
                    throw "Effective Reference was injected by an external build definition in $relativePath."
                }
                continue
            }

            $identity = Get-MsBuildItemMetadata -Item $item -Name 'Identity'
            $definingProject = Get-RepositoryRelativeFilePath -Path $definingProjectFullPath
            $hintPath = Get-MsBuildItemMetadata -Item $item -Name 'HintPath'
            $privateValue = Get-MsBuildItemMetadata -Item $item -Name 'Private'
            $hintFullPath = if ([string]::IsNullOrWhiteSpace($hintPath)) {
                ''
            }
            elseif ([System.IO.Path]::IsPathRooted($hintPath)) {
                [System.IO.Path]::GetFullPath($hintPath)
            }
            else {
                [System.IO.Path]::GetFullPath((Join-Path ([System.IO.Path]::GetDirectoryName($absolutePath)) $hintPath))
            }
            $directAssemblyReferences.Add([ordered]@{
                identity = $identity
                hintFileName = [System.IO.Path]::GetFileName($hintFullPath)
                private = $privateValue
                definedBy = $definingProject
            })

            if ($relativePath -ne 'src/JueMingR.TerrariaHost/JueMingR.TerrariaHost.csproj' -or
                $definingProject -ne $relativePath -or
                -not $script:ExpectedHostReferences.ContainsKey($identity) -or
                $privateValue -ine 'false' -or
                -not [string]::Equals(
                    $hintFullPath,
                    (Join-Path $ReferencesRoot $script:ExpectedHostReferences[$identity]),
                    [StringComparison]::OrdinalIgnoreCase)) {
                throw "Effective direct assembly Reference violates the Phase 0-R contract in $relativePath."
            }
        }
        $expectedAssemblyReferenceNames = if ($relativePath -eq 'src/JueMingR.TerrariaHost/JueMingR.TerrariaHost.csproj') {
            @($script:ExpectedHostReferences.Keys)
        }
        else {
            @()
        }
        Assert-ExactStringSet -Actual @($directAssemblyReferences | ForEach-Object { $_.identity }) -Expected $expectedAssemblyReferenceNames -Label "$relativePath direct assembly Reference"

        $effectivePackageReferences = @(Get-MsBuildItems -Evaluation $evaluation -Name 'PackageReference')
        if ($effectivePackageReferences.Count -ne 0) {
            throw "Effective PackageReference is forbidden in $relativePath."
        }

        $approvedSdkAnalyzers = New-Object System.Collections.Generic.List[string]
        foreach ($item in @(Get-MsBuildItems -Evaluation $evaluation -Name 'Analyzer')) {
            $definingProjectFullPath = Get-MsBuildItemMetadata -Item $item -Name 'DefiningProjectFullPath'
            $analyzerFullPath = Get-MsBuildItemMetadata -Item $item -Name 'FullPath'
            if ([string]::IsNullOrWhiteSpace($definingProjectFullPath) -or
                [string]::IsNullOrWhiteSpace($analyzerFullPath) -or
                -not (Test-PathWithinOrEqual -Candidate ([System.IO.Path]::GetFullPath($definingProjectFullPath)) -Parent $SdkRoot) -or
                -not (Test-PathWithinOrEqual -Candidate ([System.IO.Path]::GetFullPath($analyzerFullPath)) -Parent $SdkRoot) -or
                -not [System.IO.File]::Exists($analyzerFullPath)) {
                throw "Repository or external Analyzer is forbidden in $relativePath."
            }
            $approvedSdkAnalyzers.Add([System.IO.Path]::GetFileName($analyzerFullPath))
        }
        $effectiveComReferences = @(Get-MsBuildItems -Evaluation $evaluation -Name 'COMReference')
        $effectiveComFileReferences = @(Get-MsBuildItems -Evaluation $evaluation -Name 'COMFileReference')
        $effectiveNativeReferences = @(Get-MsBuildItems -Evaluation $evaluation -Name 'NativeReference')
        if ($effectiveComReferences.Count -ne 0 -or
            $effectiveComFileReferences.Count -ne 0 -or
            $effectiveNativeReferences.Count -ne 0) {
            throw "COMReference, COMFileReference, and NativeReference are forbidden in $relativePath."
        }

        $result.Add([ordered]@{
            project = $relativePath
            targetFramework = [string] $properties.TargetFramework
            platformTarget = [string] $properties.PlatformTarget
            langVersion = [string] $properties.LangVersion
            outputType = [string] $properties.OutputType
            assemblyName = [string] $properties.AssemblyName
            rootNamespace = [string] $properties.RootNamespace
            generateAssemblyInfo = [string] $properties.GenerateAssemblyInfo
            allowUnsafeBlocks = [string] $properties.AllowUnsafeBlocks
            treatWarningsAsErrors = [string] $properties.TreatWarningsAsErrors
            usingMicrosoftNetSdk = [string] $properties.UsingMicrosoftNETSdk
            deterministic = [string] $properties.Deterministic
            continuousIntegrationBuild = [string] $properties.ContinuousIntegrationBuild
            deterministicSourcePaths = [string] $properties.DeterministicSourcePaths
            debugType = [string] $properties.DebugType
            debugSymbols = [string] $properties.DebugSymbols
            codePage = [string] $properties.CodePage
            pathMap = 'project directory -> /_/project; guarded build root -> /_/build'
            sourceRevisionId = [string] $properties.SourceRevisionId
            version = [string] $properties.Version
            assemblyVersion = [string] $properties.AssemblyVersion
            fileVersion = [string] $properties.FileVersion
            informationalVersion = [string] $properties.InformationalVersion
            includeSourceRevisionInInformationalVersion = [string] $properties.IncludeSourceRevisionInInformationalVersion
            enableSourceControlManagerQueries = [string] $properties.EnableSourceControlManagerQueries
            enableSourceLink = [string] $properties.EnableSourceLink
            embedUntrackedSources = [string] $properties.EmbedUntrackedSources
            publishRepositoryUrl = [string] $properties.PublishRepositoryUrl
            generateRepositoryUrlAttribute = [string] $properties.GenerateRepositoryUrlAttribute
            repositoryUrl = [string] $properties.RepositoryUrl
            privateRepositoryUrl = [string] $properties.PrivateRepositoryUrl
            scmRepositoryUrl = [string] $properties.ScmRepositoryUrl
            sourceLink = [string] $properties.SourceLink
            copyLocalLockFileAssemblies = [string] $properties.CopyLocalLockFileAssemblies
            useSharedCompilation = [string] $properties.UseSharedCompilation
            compilerSideOutputs = [ordered]@{
                errorLog = [string] $properties.ErrorLog
                documentationFile = [string] $properties.DocumentationFile
                generateDocumentationFile = [string] $properties.GenerateDocumentationFile
                emitCompilerGeneratedFiles = [string] $properties.EmitCompilerGeneratedFiles
                compilerGeneratedFilesOutputPath = [string] $properties.CompilerGeneratedFilesOutputPath
                pdbFile = [string] $properties.PdbFile
            }
            buildSideEffects = [ordered]@{
                preBuildEvent = [string] $properties.PreBuildEvent
                postBuildEvent = [string] $properties.PostBuildEvent
                runPostBuildEvent = [string] $properties.RunPostBuildEvent
                generatePackageOnBuild = [string] $properties.GeneratePackageOnBuild
                deployOnBuild = [string] $properties.DeployOnBuild
                restoreGraphOutputPath = [string] $properties.RestoreGraphOutputPath
            }
            compilerTargets = 'locked SDK Roslyn/Microsoft.CSharp.Core.targets'
            frameworkReferencePath = 'verified .NET Framework 4.7.2 Developer Pack'
            msbuildImportRoots = 'locked SDK only; user and wildcard imports disabled'
            outputPaths = $safeOutputPaths
            projectReferences = @($effectiveProjectReferences.ToArray())
            directAssemblyReferences = @($directAssemblyReferences.ToArray())
            packageReferences = @()
            approvedSdkAnalyzers = @($approvedSdkAnalyzers.ToArray())
            comReferences = @()
            comFileReferences = @()
            nativeReferences = @()
            preTargetEvaluatedInputs = $effectiveInputs
            preTargetEvaluatedInputSetSha256 = $effectiveInputSetSha256
            ignoredPreTargetEvaluatedInputCount = 0
        })
    }

    return @($result.ToArray())
}

function Get-ExpectedDeclaredOutputPaths {
    param([string] $Configuration)

    $layout = [ordered]@{
        'JueMingR.Bootstrap' = @('JueMingR.Bootstrap.dll', 'JueMingR.Bootstrap.pdb')
        'JueMingR.Platform' = @('JueMingR.Platform.dll', 'JueMingR.Platform.pdb')
        'JueMingR.Features' = @('JueMingR.Features.dll', 'JueMingR.Features.pdb', 'JueMingR.Platform.dll', 'JueMingR.Platform.pdb')
        'JueMingR.Infrastructure' = @('JueMingR.Infrastructure.dll', 'JueMingR.Infrastructure.pdb', 'JueMingR.Platform.dll', 'JueMingR.Platform.pdb')
        'JueMingR.TerrariaHost' = @(
            'JueMingR.TerrariaHost.dll', 'JueMingR.TerrariaHost.pdb',
            'JueMingR.Features.dll', 'JueMingR.Features.pdb',
            'JueMingR.Infrastructure.dll', 'JueMingR.Infrastructure.pdb',
            'JueMingR.Platform.dll', 'JueMingR.Platform.pdb')
        'JueMingR.Setup' = @('JueMingR.Setup.dll', 'JueMingR.Setup.pdb')
        'JueMingR.ArchitectureTests' = @(
            'JueMingR.ArchitectureTests.exe', 'JueMingR.ArchitectureTests.pdb',
            'JueMingR.Platform.dll', 'JueMingR.Platform.pdb')
    }
    $paths = New-Object System.Collections.Generic.List[string]
    foreach ($projectName in $layout.Keys) {
        foreach ($fileName in $layout[$projectName]) {
            $paths.Add("bin/$projectName/x86/$Configuration/net472/$fileName")
        }
    }
    return @($paths.ToArray())
}

Push-Location $script:RepositoryRoot
$buildStarted = [DateTime]::UtcNow
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$savedBuildEnvironment = $null
$dotnetStateRoot = $null
$systemTempRoot = $null
$completedRecord = $null
$completedRecordPath = $null
$completedArchitectureResult = $null
$completedDeclaredOutputCount = 0
$gitBinding = $null
try {
    if ([string]::IsNullOrWhiteSpace($TerrariaReferencesDirectory)) {
        $TerrariaReferencesDirectory = Join-Path $script:RepositoryRoot 'external\TerrariaRefs'
    }
    if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
        $OutputDirectory = Join-Path $script:RepositoryRoot 'artifacts\build'
    }
    if ($NoRestore) {
        throw 'NoRestore is not a trusted Phase 0-R build mode because cached restore graphs are not accepted as formal inputs. Run the normal build entry.'
    }
    Assert-FormalRepositoryEntriesAreRegular

    $referencesRoot = Get-CanonicalDirectoryPath -Path $TerrariaReferencesDirectory -Label 'TerrariaReferencesDirectory'
    $outputRoot = Get-CanonicalDirectoryPath -Path $OutputDirectory -Label 'OutputDirectory'
    $configurationRoot = Get-CanonicalDirectoryPath -Path (Join-Path $outputRoot $Configuration) -Label 'configuration output directory'
    $workRoot = Join-Path $configurationRoot 'work'
    Assert-PathTreesDisjoint -Candidate $referencesRoot -CandidateLabel 'TerrariaReferencesDirectory' -Protected $script:LegacyRoot -ProtectedLabel 'read-only Legacy root'
    Assert-PathTreesDisjoint -Candidate $outputRoot -CandidateLabel 'OutputDirectory' -Protected $script:LegacyRoot -ProtectedLabel 'read-only Legacy root'
    Assert-PathTreesDisjoint -Candidate $configurationRoot -CandidateLabel 'configuration output directory' -Protected $script:LegacyRoot -ProtectedLabel 'read-only Legacy root'

    $baselinePath = Join-Path $script:RepositoryRoot 'eng\TerrariaReferences.baseline.json'
    $baseline = Read-StrictUtf8Json -Path $baselinePath
    $baselineHash = Get-NormalizedTextFileSha256 -Path $baselinePath
    $forbiddenReferenceHashes = @(Get-ForbiddenReferenceHashes -Baseline $baseline)
    & (Join-Path $PSScriptRoot 'prepare-terraria-references.ps1') `
        -DestinationDirectory $referencesRoot `
        -ReadOnlyLegacyDirectory $script:LegacyRoot `
        -ReproducibilityRoot $ReproducibilityRoot `
        -VerifyOnly
    $markerSources = Get-ValidatedMarkerSourceDirectories -ReferencesRoot $referencesRoot -Baseline $baseline
    Assert-RecordedSourceFilesMatchBaseline -MarkerSources $markerSources -Baseline $baseline
    Assert-PathTreesDisjoint -Candidate $markerSources.terraria -CandidateLabel 'Terraria source directory' -Protected $script:RepositoryRoot -ProtectedLabel 'repository root'
    Assert-PathTreesDisjoint -Candidate $markerSources.xna -CandidateLabel 'XNA source directory' -Protected $script:RepositoryRoot -ProtectedLabel 'repository root'
    Assert-PathTreesDisjoint -Candidate $markerSources.terraria -CandidateLabel 'Terraria source directory' -Protected $script:LegacyRoot -ProtectedLabel 'read-only Legacy root'
    Assert-PathTreesDisjoint -Candidate $markerSources.xna -CandidateLabel 'XNA source directory' -Protected $script:LegacyRoot -ProtectedLabel 'read-only Legacy root'
    $systemTempRoot = Get-CanonicalDirectoryPath -Path ([System.IO.Path]::GetTempPath()) -Label 'system TEMP root'
    Assert-PathTreesDisjoint -Candidate $systemTempRoot -CandidateLabel 'system TEMP root' -Protected $markerSources.terraria -ProtectedLabel 'Terraria source directory'
    Assert-PathTreesDisjoint -Candidate $systemTempRoot -CandidateLabel 'system TEMP root' -Protected $markerSources.xna -ProtectedLabel 'XNA source directory'

    $script:DotnetPath = Get-ValidatedCommandPath -Name 'dotnet.exe' -Label 'dotnet executable'
    $script:GitPath = Get-ValidatedCommandPath -Name 'git.exe' -Label 'Git executable'
    $dotnetRoot = Get-CanonicalDirectoryPath `
        -Path ([System.IO.Path]::GetDirectoryName($script:DotnetPath)) `
        -Label 'dotnet root' `
        -RequireAbsolute
    $gitDirectory = Get-CanonicalDirectoryPath `
        -Path ([System.IO.Path]::GetDirectoryName($script:GitPath)) `
        -Label 'Git executable directory' `
        -RequireAbsolute
    $windowsRoot = Get-CanonicalDirectoryPath `
        -Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::Windows)) `
        -Label 'Windows root' `
        -RequireAbsolute
    $powerShellHome = Get-CanonicalDirectoryPath -Path $PSHOME -Label 'PowerShell home' -RequireAbsolute
    $globalJsonPath = Join-Path $script:RepositoryRoot 'global.json'
    $globalJson = Read-StrictUtf8Json -Path $globalJsonPath
    $expectedSdk = [string] $globalJson.sdk.version
    $sdkRoot = Get-CanonicalDirectoryPath `
        -Path (Join-Path $dotnetRoot ("sdk\$expectedSdk")) `
        -Label 'locked SDK root' `
        -RequireAbsolute
    if (-not [System.IO.File]::Exists((Join-Path $sdkRoot 'MSBuild.dll')) -or
        -not [System.IO.File]::Exists((Join-Path $sdkRoot 'Roslyn\Microsoft.CSharp.Core.targets'))) {
        throw "global.json requires .NET SDK $expectedSdk, but its locked MSBuild/Roslyn files are missing."
    }

    $programFilesRoot = Get-CanonicalDirectoryPath `
        -Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles)) `
        -Label 'machine Program Files root' `
        -RequireAbsolute
    $programFilesX86 = Get-CanonicalDirectoryPath `
        -Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFilesX86)) `
        -Label 'machine Program Files (x86) root' `
        -RequireAbsolute
    $programDataRoot = Get-CanonicalDirectoryPath `
        -Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::CommonApplicationData)) `
        -Label 'machine ProgramData root' `
        -RequireAbsolute
    $targetingPackRoot = Get-CanonicalDirectoryPath `
        -Path (Join-Path $programFilesX86 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2') `
        -Label '.NET Framework 4.7.2 Targeting Pack root'
    $targetingPackMscorlib = Join-Path $targetingPackRoot 'mscorlib.dll'
    if (-not [System.IO.File]::Exists($targetingPackMscorlib)) {
        throw '.NET Framework 4.7.2 Developer Pack / Targeting Pack is missing.'
    }
    $targetingPackVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($targetingPackMscorlib).FileVersion

    $dotnetStateRoot = Join-Path $systemTempRoot ('JueMingR-DotnetState-' + [Guid]::NewGuid().ToString('N'))
    $canonicalDotnetStateRoot = Get-CanonicalDirectoryPath -Path $dotnetStateRoot -Label 'dotnet state scratch root' -RequireAbsolute
    Assert-PathTreesDisjoint -Candidate $canonicalDotnetStateRoot -CandidateLabel 'dotnet state scratch root' -Protected $script:RepositoryRoot -ProtectedLabel 'repository root'
    Assert-PathTreesDisjoint -Candidate $canonicalDotnetStateRoot -CandidateLabel 'dotnet state scratch root' -Protected $referencesRoot -ProtectedLabel 'TerrariaReferencesDirectory'
    Assert-PathTreesDisjoint -Candidate $canonicalDotnetStateRoot -CandidateLabel 'dotnet state scratch root' -Protected $outputRoot -ProtectedLabel 'OutputDirectory'
    Assert-PathTreesDisjoint -Candidate $canonicalDotnetStateRoot -CandidateLabel 'dotnet state scratch root' -Protected $configurationRoot -ProtectedLabel 'configuration output directory'
    Assert-PathTreesDisjoint -Candidate $canonicalDotnetStateRoot -CandidateLabel 'dotnet state scratch root' -Protected $markerSources.terraria -ProtectedLabel 'Terraria source directory'
    Assert-PathTreesDisjoint -Candidate $canonicalDotnetStateRoot -CandidateLabel 'dotnet state scratch root' -Protected $markerSources.xna -ProtectedLabel 'XNA source directory'
    Assert-PathTreesDisjoint -Candidate $canonicalDotnetStateRoot -CandidateLabel 'dotnet state scratch root' -Protected $script:LegacyRoot -ProtectedLabel 'read-only Legacy root'
    if ([System.IO.Directory]::Exists($canonicalDotnetStateRoot) -or [System.IO.File]::Exists($canonicalDotnetStateRoot)) {
        throw 'The randomly selected dotnet state scratch root already exists.'
    }
    $dotnetStateRoot = $canonicalDotnetStateRoot
    $savedBuildEnvironment = Get-CompleteEnvironmentSnapshot
    Enter-LockedBuildEnvironment `
        -Snapshot $savedBuildEnvironment `
        -ScratchRoot $dotnetStateRoot `
        -DotnetRoot $dotnetRoot `
        -GitDirectory $gitDirectory `
        -WindowsRoot $windowsRoot `
        -PowerShellHome $powerShellHome `
        -ProgramFilesRoot $programFilesRoot `
        -ProgramFilesX86Root $programFilesX86 `
        -ProgramDataRoot $programDataRoot
    Add-LockedMsBuildEnvironment -SdkRoot $sdkRoot

    $gitBinding = Get-PhysicalGitBinding -ExpectedWorkTree $script:RepositoryRoot
    $script:GitMetadataRoot = $gitBinding.gitDirectory
    Assert-PathTreesDisjoint -Candidate $gitBinding.gitDirectory -CandidateLabel 'Git per-worktree metadata directory' -Protected $markerSources.terraria -ProtectedLabel 'Terraria source directory'
    Assert-PathTreesDisjoint -Candidate $gitBinding.gitDirectory -CandidateLabel 'Git per-worktree metadata directory' -Protected $markerSources.xna -ProtectedLabel 'XNA source directory'
    Assert-PathTreesDisjoint -Candidate $gitBinding.commonDirectory -CandidateLabel 'Git common metadata directory' -Protected $markerSources.terraria -ProtectedLabel 'Terraria source directory'
    Assert-PathTreesDisjoint -Candidate $gitBinding.commonDirectory -CandidateLabel 'Git common metadata directory' -Protected $markerSources.xna -ProtectedLabel 'XNA source directory'
    foreach ($gitMetadata in @(
        @{ Path = $gitBinding.gitDirectory; Label = 'Git per-worktree metadata directory' },
        @{ Path = $gitBinding.commonDirectory; Label = 'Git common metadata directory' })) {
        Assert-PathTreesDisjoint -Candidate $gitMetadata.Path -CandidateLabel $gitMetadata.Label -Protected $referencesRoot -ProtectedLabel 'TerrariaReferencesDirectory'
        Assert-PathTreesDisjoint -Candidate $gitMetadata.Path -CandidateLabel $gitMetadata.Label -Protected $outputRoot -ProtectedLabel 'OutputDirectory'
        Assert-PathTreesDisjoint -Candidate $gitMetadata.Path -CandidateLabel $gitMetadata.Label -Protected $configurationRoot -ProtectedLabel 'configuration output directory'
        Assert-PathTreesDisjoint -Candidate $gitMetadata.Path -CandidateLabel $gitMetadata.Label -Protected $dotnetStateRoot -ProtectedLabel 'dotnet state scratch root'
        Assert-PathTreesDisjoint -Candidate $gitMetadata.Path -CandidateLabel $gitMetadata.Label -Protected $script:LegacyRoot -ProtectedLabel 'read-only Legacy root'
    }
    [System.IO.Directory]::CreateDirectory($dotnetStateRoot) | Out-Null
    [System.IO.Directory]::CreateDirectory((Join-Path $dotnetStateRoot 'temp')) | Out-Null
    Assert-PhysicalGitBindingUnchanged -Expected $gitBinding
    $verifiedHead = @(Get-GitOutput -Arguments @('rev-parse', '--verify', 'HEAD^{commit}'))
    if ($verifiedHead.Count -ne 1 -or $verifiedHead[0] -notmatch '^[0-9a-fA-F]{40}$') {
        throw 'The physically bound Git work tree does not have one valid HEAD commit.'
    }

    Assert-SafeBuildOutput -Root $script:RepositoryRoot -OutputRoot $outputRoot -ConfigurationRoot $configurationRoot -ReferencesRoot $referencesRoot -TerrariaSourceRoot $markerSources.terraria -XnaSourceRoot $markerSources.xna

    $actualSdk = (& $script:DotnetPath --version).Trim()
    if ($LASTEXITCODE -ne 0 -or $actualSdk -ne $expectedSdk) {
        throw "global.json requires .NET SDK $expectedSdk, but dotnet selected '$actualSdk'."
    }

    $trackedFiles = @(Get-GitOutput -Arguments @('-c', 'core.quotePath=false', 'ls-files'))
    $recordedSourcePaths = @(Get-GitRecordedSourcePaths)
    Assert-NoIgnoredFormalInputFiles -RecordedSourcePaths $recordedSourcePaths
    Assert-GitIndexAndAttributesSafe -RecordedSourcePaths $recordedSourcePaths

    $sourceContentInventory = Get-RawRepositoryContentInventory `
        -Root $script:RepositoryRoot `
        -RecordedSourcePaths $recordedSourcePaths `
        -TrackedSourcePaths $trackedFiles `
        -Commit ([string] $verifiedHead[0])
    $sourceIdentity = Get-GitSourceIdentity -Root $script:RepositoryRoot -ContentInventory $sourceContentInventory
    $commit = $sourceIdentity.commit
    $statusLines = @($sourceIdentity.statusLines)
    $isClean = $sourceIdentity.clean
    if ($RequireClean -and -not $isClean) {
        throw ("RequireClean was specified, but source identity is not clean (status={0}, tracked-byte-mismatches=[{1}], untracked-inventory={2})." -f `
            $statusLines.Count,
            (@($sourceContentInventory.trackedMismatchPaths) -join ', '),
            [bool] $sourceContentInventory.hasUntrackedFiles)
    }
    Assert-RawBuildDefinitionsSafe
    foreach ($trackedFile in $trackedFiles) {
        $trackedPath = Join-Path $script:RepositoryRoot ([string] $trackedFile)
        if ([System.IO.File]::Exists($trackedPath)) {
            Assert-FileIsNotForbiddenBinary -Path $trackedPath -ForbiddenHashes $forbiddenReferenceHashes -Context 'Git tracked files'
        }
    }

    $dirtyIdentity = $sourceIdentity.dirtyIdentity
    $sourceRevisionId = if ($isClean) { $commit } else { $commit + '.dirty.' + $dirtyIdentity.Substring(0, 16).ToLowerInvariant() }
    $effectiveProjects = @(Get-EvaluatedProjectBuildFacts `
        -Configuration $Configuration `
        -ReferencesRoot $referencesRoot `
        -WorkRoot $workRoot `
        -SourceRevisionId $sourceRevisionId `
        -SdkRoot $sdkRoot `
        -TargetingPackRoot $targetingPackRoot `
        -RecordedSourcePaths $recordedSourcePaths)
    $targetFrameworks = @($effectiveProjects | ForEach-Object { $_.targetFramework } | Select-Object -Unique)
    $platformTargets = @($effectiveProjects | ForEach-Object { $_.platformTarget } | Select-Object -Unique)
    $langVersions = @($effectiveProjects | ForEach-Object { $_.langVersion } | Select-Object -Unique)
    if ($effectiveProjects.Count -ne 7 -or
        $targetFrameworks.Count -ne 1 -or
        $platformTargets.Count -ne 1 -or
        $langVersions.Count -ne 1) {
        throw 'Effective project build facts do not form one complete, consistent seven-project build identity.'
    }

    Remove-OwnedBuildOutput -ConfigurationRoot $configurationRoot
    [System.IO.Directory]::CreateDirectory($configurationRoot) | Out-Null
    [System.IO.File]::WriteAllText(
        (Join-Path $configurationRoot $script:BuildMarkerName),
        $script:BuildMarkerValue + [Environment]::NewLine,
        $script:Utf8NoBom)

    [System.IO.Directory]::CreateDirectory($workRoot) | Out-Null
    $commonProperties = @(
        '-p:Platform=x86',
        ('-p:JueMingRBuildRoot={0}' -f $workRoot),
        ('-p:TerrariaReferencesDirectory={0}' -f $referencesRoot),
        ('-p:SourceRevisionId={0}' -f $sourceRevisionId)
    ) + @(Get-MsBuildIsolationProperties -SdkRoot $sdkRoot -TargetingPackRoot $targetingPackRoot)
    Invoke-External `
        -FilePath $script:DotnetPath `
        -Arguments (@('msbuild', '.\JueMingR.sln', '-noAutoResponse', '-nologo', '-nodeReuse:false', '-t:Restore', ("-p:Configuration=$Configuration")) + $commonProperties) `
        -FailureMessage 'Solution restore failed'

    $buildArguments = @('msbuild', '.\JueMingR.sln', '-noAutoResponse', '-nologo', '-nodeReuse:false', '-t:Build', ("-p:Configuration=$Configuration")) + $commonProperties
    Invoke-External -FilePath $script:DotnetPath -Arguments $buildArguments -FailureMessage 'Solution build failed'

    $architectureResult = 'SKIPPED for diagnostics'
    if (-not $SkipArchitectureTests) {
        $architectureExecutables = @(
            Get-ChildItem -LiteralPath (Join-Path $workRoot 'bin\JueMingR.ArchitectureTests') -Filter 'JueMingR.ArchitectureTests.exe' -File -Recurse
        )
        if ($architectureExecutables.Count -ne 1) {
            throw 'Expected exactly one ArchitectureTests executable in the formal build output.'
        }

        Invoke-External `
            -FilePath $architectureExecutables[0].FullName `
            -Arguments @($script:RepositoryRoot, $Configuration, $sdkRoot, $targetingPackRoot, $dotnetStateRoot) `
            -FailureMessage 'Architecture tests failed'
        $architectureResult = 'PASS'
    }

    $sourceLinkJsonFiles = @(Get-ChildItem -LiteralPath $workRoot -Filter '*.sourcelink.json' -File -Recurse)
    $repositoryUrlMetadataMatches = @(Get-ChildItem -LiteralPath $workRoot -Filter '*.AssemblyInfo.cs' -File -Recurse |
        Select-String -SimpleMatch 'RepositoryUrl')
    if ($sourceLinkJsonFiles.Count -ne 0 -or $repositoryUrlMetadataMatches.Count -ne 0) {
        throw 'The formal build produced a forbidden SourceLink file or RepositoryUrl assembly metadata input.'
    }

    Assert-NoForbiddenOutput -ConfigurationRoot $configurationRoot -ForbiddenHashes $forbiddenReferenceHashes
    $declaredOutputs = New-Object System.Collections.Generic.List[object]
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $workRoot 'bin') -File -Recurse |
        Where-Object { $_.Extension -eq '.dll' -or $_.Extension -eq '.exe' -or $_.Extension -eq '.pdb' } |
        Sort-Object FullName) {
        $relativePath = $file.FullName.Substring($workRoot.Length).TrimStart('\').Replace('\', '/')
        $declaredOutputs.Add([ordered]@{
            path = $relativePath
            size = $file.Length
            sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
        })
    }
    if ($declaredOutputs.Count -eq 0) {
        throw 'The formal build produced no declared DLL, EXE, or PDB outputs.'
    }
    Assert-ExactStringSet `
        -Actual @($declaredOutputs | ForEach-Object { $_.path }) `
        -Expected @(Get-ExpectedDeclaredOutputPaths -Configuration $Configuration) `
        -Label 'formal declared output set'

    $msbuildVersion = ((& $script:DotnetPath msbuild -noAutoResponse -nologo -nodeReuse:false -version) | Select-Object -Last 1).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Could not read the locked MSBuild version.' }
    $compilerPath = Join-Path $sdkRoot 'Roslyn\bincore\csc.dll'
    $compilerVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($compilerPath).FileVersion
    $referenceHashes = @($baseline.files | ForEach-Object {
        [ordered]@{ logicalName = $_.logicalName; sha256 = $_.sha256 }
    })

    Assert-RecordedSourceFilesMatchBaseline -MarkerSources $markerSources -Baseline $baseline
    Assert-PhysicalGitBindingUnchanged -Expected $gitBinding
    $finalRecordedSourcePaths = @(Get-GitRecordedSourcePaths)
    Assert-ExactStringSet -Actual $finalRecordedSourcePaths -Expected $recordedSourcePaths -Label 'recorded Git source inventory after build'
    Assert-NoIgnoredFormalInputFiles -RecordedSourcePaths $finalRecordedSourcePaths
    Assert-GitIndexAndAttributesSafe -RecordedSourcePaths $finalRecordedSourcePaths
    $finalTrackedFiles = @(Get-GitOutput -Arguments @('-c', 'core.quotePath=false', 'ls-files'))
    $finalSourceContentInventory = Get-RawRepositoryContentInventory `
        -Root $script:RepositoryRoot `
        -RecordedSourcePaths $finalRecordedSourcePaths `
        -TrackedSourcePaths $finalTrackedFiles `
        -Commit $commit
    if ($finalSourceContentInventory.identityText -cne $sourceContentInventory.identityText -or
        $finalSourceContentInventory.contentSetSha256 -cne $sourceContentInventory.contentSetSha256) {
        throw 'Raw repository source bytes changed during the formal build; no build record will be written.'
    }
    $finalSourceIdentity = Get-GitSourceIdentity -Root $script:RepositoryRoot -ContentInventory $finalSourceContentInventory
    if ($finalSourceIdentity.commit -cne $sourceIdentity.commit -or
        [bool] $finalSourceIdentity.clean -ne [bool] $sourceIdentity.clean -or
        $finalSourceIdentity.dirtyIdentity -cne $sourceIdentity.dirtyIdentity -or
        (@($finalSourceIdentity.statusLines) -join "`n") -cne (@($sourceIdentity.statusLines) -join "`n")) {
        throw 'Repository source identity changed during the formal build; no build record will be written.'
    }

    $stopwatch.Stop()
    $buildEnded = [DateTime]::UtcNow
    $record = [ordered]@{
        schemaVersion = 2
        source = [ordered]@{
            commit = $commit
            clean = $isClean
            dirtyDiffIdentitySha256 = $dirtyIdentity
            sourceRevisionId = $sourceRevisionId
            gitBinding = 'physical work tree plus per-worktree and common Git metadata directories; paths omitted'
            recordedSourceContentSha256 = $sourceContentInventory.contentSetSha256
            formalInputContentInventory = @($sourceContentInventory.entries)
            trackedBytesMatchIndexAndCommit = [bool] $sourceContentInventory.trackedBytesMatchIndexAndCommit
            formalInputInventoryPolicy = 'recorded-git-content-modes-and-effective-msbuild-v2'
        }
        build = [ordered]@{
            configuration = $Configuration
            targetFramework = $targetFrameworks[0]
            platformTarget = $platformTargets[0]
            langVersion = $langVersions[0]
            effectiveProjects = $effectiveProjects
            sdk = $actualSdk
            msbuild = $msbuildVersion
            compiler = $compilerVersion
            powerShell = $PSVersionTable.PSVersion.ToString()
            developerPack = ".NET Framework 4.7.2 reference assemblies $targetingPackVersion"
            entry = 'scripts/build.ps1'
            environmentPolicy = 'closed-allowlist-v1'
            sourceControlSideEffects = [ordered]@{
                sourceLinkJsonCount = 0
                repositoryUrlAssemblyMetadataCount = 0
            }
            effectiveParameters = [ordered]@{
                configuration = $Configuration
                references = 'local directory verified against the tracked baseline; path omitted'
                output = 'ignored local artifacts directory; path omitted'
                readOnlyLegacyBoundary = 'existing physical JueMingZ root used only as a protected no-write path; path omitted'
                msbuildNodeReuse = $false
                skipArchitectureTests = [bool] $SkipArchitectureTests
                requireClean = [bool] $RequireClean
                restoreMode = 'fresh formal restore; NoRestore requests are rejected'
            }
            startedUtc = $buildStarted.ToString('o')
            endedUtc = $buildEnded.ToString('o')
            elapsedMilliseconds = $stopwatch.ElapsedMilliseconds
        }
        references = [ordered]@{
            profileId = $baseline.profileId
            baselineSha256 = $baselineHash
            files = $referenceHashes
        }
        architectureChecks = $architectureResult
        outputs = @($declaredOutputs.ToArray())
        unverifiedAxes = @(
            'Terraria loading',
            'hooks',
            'UI',
            'features',
            'multiplayer',
            'runtime performance',
            'installation and recovery',
            'project-owner game testing',
            'release'
        )
    }
    $completedRecord = $record
    $completedRecordPath = Join-Path $configurationRoot 'build-record.json'
    $completedArchitectureResult = $architectureResult
    $completedDeclaredOutputCount = $declaredOutputs.Count
}
finally {
    if ($stopwatch.IsRunning) {
        $stopwatch.Stop()
    }
    $environmentRestoreError = $null
    $scratchCleanupError = $null
    try {
        Restore-CompleteEnvironment -Snapshot $savedBuildEnvironment
    }
    catch {
        $environmentRestoreError = $_
    }
    try {
        Remove-OwnedDotnetStateRoot -ScratchRoot $dotnetStateRoot -SystemTempRoot $systemTempRoot
    }
    catch {
        $scratchCleanupError = $_
    }
    finally {
        Pop-Location
    }
    if ($null -ne $environmentRestoreError -or $null -ne $scratchCleanupError) {
        $messages = @()
        if ($null -ne $environmentRestoreError) { $messages += ('environment restore: ' + $environmentRestoreError.Exception.Message) }
        if ($null -ne $scratchCleanupError) { $messages += ('scratch cleanup: ' + $scratchCleanupError.Exception.Message) }
        throw ('Formal build cleanup did not complete; no build record will be written. ' + ($messages -join '; '))
    }
}

if ($null -eq $completedRecord -or [string]::IsNullOrWhiteSpace($completedRecordPath)) {
    throw 'The formal build did not produce a completed record after tool-state cleanup.'
}
$completedRecordBytes = $script:Utf8NoBom.GetBytes(
    (($completedRecord | ConvertTo-Json -Depth 10) + [Environment]::NewLine))
$recordTemporaryPath = Join-Path `
    ([System.IO.Path]::GetDirectoryName($completedRecordPath)) `
    ('.build-record.' + [Guid]::NewGuid().ToString('N') + '.tmp')
$closingGitEnvironment = Set-LockedGitEnvironment
$recordTemporaryCreated = $false
$recordStaged = $false
try {
    Assert-FormalRepositoryEntriesAreRegular
    Assert-PhysicalGitBindingUnchanged -Expected $gitBinding
    $closingRecordedSourcePaths = @(Get-GitRecordedSourcePaths)
    $closingRecordedSourcePaths = [string[]] @($closingRecordedSourcePaths)
    [Array]::Sort($closingRecordedSourcePaths, [StringComparer]::Ordinal)
    $recordedSourcePathsFromRecord = @($completedRecord.source.formalInputContentInventory | ForEach-Object { [string] $_.path })
    if (($closingRecordedSourcePaths -join "`n") -cne ($recordedSourcePathsFromRecord -join "`n")) {
        throw 'The recorded Git source inventory changed after build cleanup; no build record will be written.'
    }
    Assert-NoIgnoredFormalInputFiles -RecordedSourcePaths $closingRecordedSourcePaths
    Assert-GitIndexAndAttributesSafe -RecordedSourcePaths $closingRecordedSourcePaths
    $closingTrackedSourcePaths = @(Get-GitOutput -Arguments @('-c', 'core.quotePath=false', 'ls-files'))
    $closingSourceContent = Get-RawRepositoryContentInventory `
        -Root $script:RepositoryRoot `
        -RecordedSourcePaths $closingRecordedSourcePaths `
        -TrackedSourcePaths $closingTrackedSourcePaths `
        -Commit ([string] $completedRecord.source.commit)
    $recordedHasUntrackedFiles = @($completedRecord.source.formalInputContentInventory | Where-Object {
        $_.tracked -ne $true
    }).Count -ne 0
    if ([bool] $closingSourceContent.trackedBytesMatchIndexAndCommit -ne
            [bool] $completedRecord.source.trackedBytesMatchIndexAndCommit -or
        [bool] $closingSourceContent.hasUntrackedFiles -ne $recordedHasUntrackedFiles -or
        $closingSourceContent.contentSetSha256 -cne [string] $completedRecord.source.recordedSourceContentSha256) {
        throw 'The raw repository source identity changed after build cleanup; no build record will be written.'
    }
    Assert-PhysicalContentInventoryMatches `
        -Entries @($completedRecord.source.formalInputContentInventory) `
        -ExpectedDigest ([string] $completedRecord.source.recordedSourceContentSha256)
    $closingSourceIdentity = Get-GitSourceIdentity -Root $script:RepositoryRoot -ContentInventory $closingSourceContent
    if ($closingSourceIdentity.commit -cne [string] $completedRecord.source.commit -or
        [bool] $closingSourceIdentity.clean -ne [bool] $completedRecord.source.clean -or
        $closingSourceIdentity.dirtyIdentity -cne [string] $completedRecord.source.dirtyDiffIdentitySha256) {
        throw 'The repository source identity changed after build cleanup; no build record will be written.'
    }

    if ([System.IO.File]::Exists($completedRecordPath)) {
        throw 'The formal build-record path unexpectedly exists before staging.'
    }
    $recordStream = [System.IO.File]::Open(
        $recordTemporaryPath,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None)
    $recordTemporaryCreated = $true
    try {
        $recordStream.Write($completedRecordBytes, 0, $completedRecordBytes.Length)
        $recordStream.Flush($true)
    }
    finally {
        $recordStream.Dispose()
    }
    $recordStaged = $true
}
finally {
    $closingEnvironmentRestoreError = $null
    try {
        Restore-EnvironmentSnapshot -Snapshot $closingGitEnvironment
    }
    catch {
        $closingEnvironmentRestoreError = $_
    }
    if (($recordTemporaryCreated -and -not $recordStaged) -or
        $null -ne $closingEnvironmentRestoreError) {
        if ([System.IO.File]::Exists($recordTemporaryPath)) {
            Remove-Item -LiteralPath $recordTemporaryPath -Force -ErrorAction SilentlyContinue
            if ([System.IO.File]::Exists($recordTemporaryPath)) {
                throw 'The non-final staged build record could not be removed; the next owned-output cleanup will retry.'
            }
        }
    }
    if ($null -ne $closingEnvironmentRestoreError) {
        throw ('The closing Git environment was not restored; the staged non-final record was not published. ' +
            $closingEnvironmentRestoreError.Exception.Message)
    }
}
try {
    if (-not $recordStaged -or -not [System.IO.File]::Exists($recordTemporaryPath)) {
        throw 'The staged build record is missing before atomic publication.'
    }
    if ([System.IO.File]::Exists($completedRecordPath)) {
        throw 'The formal build-record path unexpectedly exists before atomic publication.'
    }
    [System.IO.File]::Move($recordTemporaryPath, $completedRecordPath)
    $recordTemporaryCreated = $false
}
catch {
    if ($recordTemporaryCreated -and [System.IO.File]::Exists($recordTemporaryPath)) {
        Remove-Item -LiteralPath $recordTemporaryPath -Force -ErrorAction SilentlyContinue
        if ([System.IO.File]::Exists($recordTemporaryPath)) {
            throw 'Atomic build-record publication failed and its non-final staged file could not be removed; the next owned-output cleanup will retry.'
        }
    }
    throw
}
Write-Output ("PASS: {0} build, architecture checks={1}, declared outputs={2}." -f $Configuration, $completedArchitectureResult, $completedDeclaredOutputCount)
Write-Output ("Build record: {0}" -f $completedRecordPath)
