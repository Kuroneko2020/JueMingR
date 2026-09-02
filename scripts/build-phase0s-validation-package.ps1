[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\')
. (Join-Path $PSScriptRoot 'phase0s\Phase0S.ScriptSupport.ps1')

function Invoke-Phase0SGit {
    param([Parameter(Mandatory = $true)][string[]] $Arguments)

    $output = @(& git -C $repositoryRoot @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw 'A required Git query failed.'
    }
    return $output
}

function Copy-Phase0SBuilderFileCreateNew {
    param(
        [Parameter(Mandatory = $true)][string] $SourcePath,
        [Parameter(Mandatory = $true)][string] $DestinationPath
    )

    if (-not (Test-Phase0SOrdinaryFile -Path $SourcePath)) {
        throw 'A fixed package source is not an ordinary file.'
    }
    $sourceLength = (Get-Item -LiteralPath $SourcePath -Force -ErrorAction Stop).Length
    $sourceHash = Get-Phase0SFileSha256 -Path $SourcePath
    $source = [System.IO.File]::Open($SourcePath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
    try {
        $destination = [System.IO.File]::Open($DestinationPath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        try {
            $source.CopyTo($destination)
            $destination.Flush($true)
        }
        finally {
            $destination.Dispose()
        }
    }
    finally {
        $source.Dispose()
    }
    $destinationItem = Get-Item -LiteralPath $DestinationPath -Force -ErrorAction Stop
    if ($destinationItem.Length -ne $sourceLength -or (Get-Phase0SFileSha256 -Path $DestinationPath) -cne $sourceHash) {
        throw 'A fixed package source changed while being copied.'
    }
}

function Write-Phase0SBuilderTextCreateNew {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string] $Text
    )

    $bytes = (New-Object System.Text.UTF8Encoding($false, $true)).GetBytes($Text)
    Copy-Phase0SBytesCreateNew -Bytes $bytes -DestinationPath $Path
}

function Get-Phase0SBuildOutputPath {
    param(
        [Parameter(Mandatory = $true)][string] $ProjectName,
        [Parameter(Mandatory = $true)][string] $FileName
    )

    $path = Join-Path $repositoryRoot ('artifacts\build\Release\work\bin\' + $ProjectName + '\x86\Release\net472\' + $FileName)
    if (-not (Test-Phase0SOrdinaryFile -Path $path)) {
        throw 'A required Release build output is missing.'
    }
    return $path
}

function Get-Phase0SRelativeForwardPath {
    param(
        [Parameter(Mandatory = $true)][string] $Root,
        [Parameter(Mandatory = $true)][string] $Path
    )

    $rootUri = New-Object System.Uri(([System.IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'))
    $pathUri = New-Object System.Uri([System.IO.Path]::GetFullPath($Path))
    return [System.Uri]::UnescapeDataString($rootUri.MakeRelativeUri($pathUri).ToString())
}

function Get-Phase0SPackageTreeRecords {
    param([Parameter(Mandatory = $true)][string] $PackageRoot)

    $records = New-Object System.Collections.Generic.List[object]
    $queue = New-Object 'System.Collections.Generic.Queue[string]'
    $queue.Enqueue($PackageRoot)
    while ($queue.Count -gt 0) {
        $directory = $queue.Dequeue()
        foreach ($item in @(Get-ChildItem -LiteralPath $directory -Force -ErrorAction Stop | Sort-Object Name)) {
            if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw 'The package tree contains a reparse point.'
            }
            if ($item.PSIsContainer) {
                $queue.Enqueue($item.FullName)
            }
            else {
                $records.Add([pscustomobject][ordered]@{
                    path = Get-Phase0SRelativeForwardPath -Root $PackageRoot -Path $item.FullName
                    length = [int64] $item.Length
                    sha256 = Get-Phase0SFileSha256 -Path $item.FullName
                })
            }
        }
    }
    $recordsByPath = New-Object 'System.Collections.Generic.Dictionary[string,object]' ([System.StringComparer]::Ordinal)
    $paths = New-Object System.Collections.Generic.List[string]
    foreach ($record in $records.ToArray()) {
        $recordsByPath.Add([string] $record.path, $record)
        $paths.Add([string] $record.path)
    }
    $sortedPaths = $paths.ToArray()
    [Array]::Sort($sortedPaths, [System.StringComparer]::Ordinal)
    return @($sortedPaths | ForEach-Object { $recordsByPath[$_] })
}

function Assert-Phase0SFixedPackageTree {
    param([Parameter(Mandatory = $true)][string] $PackageRoot)

    $expectedFiles = @(
        'Harmony-2.4.2-LICENSE.txt',
        'Install-Phase0S.ps1',
        'Phase0S-Owner-Test-Card.zh-CN.md',
        'Phase0S.ScriptSupport.ps1',
        'Restore-Phase0S.ps1',
        'THIRD-PARTY-NOTICES.md',
        'payload/JueMingR.Bootstrap.dll',
        'payload/JueMingR.Validation/0Harmony.dll',
        'payload/JueMingR.Validation/JueMingR.Features.dll',
        'payload/JueMingR.Validation/JueMingR.Infrastructure.dll',
        'payload/JueMingR.Validation/JueMingR.Platform.dll',
        'payload/JueMingR.Validation/JueMingR.TerrariaHost.dll',
        'payload/JueMingR.Validation/phase-0-s-runtime.manifest',
        'payload/Terraria.exe.config',
        'phase-0-s-package.manifest.json'
    )
    $records = @(Get-Phase0SPackageTreeRecords -PackageRoot $PackageRoot)
    $actualFiles = @($records | ForEach-Object { $_.path })
    if (($actualFiles -join '|') -cne ($expectedFiles -join '|')) {
        throw 'The final package tree has missing or unexpected files.'
    }
    $expectedRootNames = @(
        'Harmony-2.4.2-LICENSE.txt', 'Install-Phase0S.ps1', 'Phase0S-Owner-Test-Card.zh-CN.md',
        'Phase0S.ScriptSupport.ps1', 'Restore-Phase0S.ps1', 'THIRD-PARTY-NOTICES.md',
        'payload', 'phase-0-s-package.manifest.json'
    ) | Sort-Object
    $actualRootNames = @(Get-ChildItem -LiteralPath $PackageRoot -Force -ErrorAction Stop | Sort-Object Name | ForEach-Object { $_.Name })
    if (($actualRootNames -join '|') -cne ($expectedRootNames -join '|') -or
        -not (Test-Phase0SOrdinaryDirectory -Path (Join-Path $PackageRoot 'payload')) -or
        -not (Test-Phase0SOrdinaryDirectory -Path (Join-Path $PackageRoot 'payload\JueMingR.Validation'))) {
        throw 'The final package tree has missing or unexpected root objects.'
    }
    return $records
}

function Assert-Phase0SPackageHasNoPrivatePath {
    param([Parameter(Mandatory = $true)][string] $PackageRoot)

    $sensitiveValues = New-Object System.Collections.Generic.List[string]
    foreach ($value in @($repositoryRoot, $repositoryRoot.Replace('\', '/'), [Environment]::GetFolderPath('UserProfile'))) {
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            $sensitiveValues.Add($value)
        }
    }
    $textExtensions = @('.ps1', '.md', '.txt', '.json', '.config', '.manifest')
    foreach ($record in @(Get-Phase0SPackageTreeRecords -PackageRoot $PackageRoot)) {
        $filePath = Resolve-Phase0SContainedPath -Root $PackageRoot -RelativePath $record.path
        $file = Get-Item -LiteralPath $filePath -Force -ErrorAction Stop
        if ($textExtensions -notcontains $file.Extension) {
            continue
        }
        $text = Get-Phase0SStrictUtf8Text -Path $file.FullName -MaximumLength 1048576
        foreach ($value in $sensitiveValues) {
            if ($text.IndexOf($value, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw 'A package text file contains a private absolute path.'
            }
        }
        if ($text -match '(?i)[A-Z]:[\\/](?:Users|Documents)[\\/]' -or $text -match '(?i)/home/[^/]+/') {
            throw 'A package text file contains a user-specific absolute path.'
        }
    }
}

function New-Phase0SDeterministicZip {
    param(
        [Parameter(Mandatory = $true)][string] $PackageRoot,
        [Parameter(Mandatory = $true)][string] $PackageDirectoryName,
        [Parameter(Mandatory = $true)][string] $ZipPath
    )

    Add-Type -AssemblyName System.IO.Compression -ErrorAction Stop
    $stream = [System.IO.File]::Open($ZipPath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
    try {
        $archive = New-Object System.IO.Compression.ZipArchive($stream, [System.IO.Compression.ZipArchiveMode]::Create, $true, [System.Text.Encoding]::UTF8)
        try {
            $timestamp = New-Object System.DateTimeOffset(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
            foreach ($record in @(Get-Phase0SPackageTreeRecords -PackageRoot $PackageRoot)) {
                $entryName = $PackageDirectoryName + '/' + $record.path
                $entry = $archive.CreateEntry($entryName, [System.IO.Compression.CompressionLevel]::Optimal)
                $entry.LastWriteTime = $timestamp
                $entryStream = $entry.Open()
                try {
                    $sourcePath = Resolve-Phase0SContainedPath -Root $PackageRoot -RelativePath $record.path
                    $source = [System.IO.File]::Open($sourcePath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
                    try {
                        $source.CopyTo($entryStream)
                    }
                    finally {
                        $source.Dispose()
                    }
                }
                finally {
                    $entryStream.Dispose()
                }
            }
        }
        finally {
            $archive.Dispose()
        }
        $stream.Flush($true)
    }
    finally {
        $stream.Dispose()
    }
}

function Assert-Phase0SZipMatchesPackage {
    param(
        [Parameter(Mandatory = $true)][string] $PackageRoot,
        [Parameter(Mandatory = $true)][string] $PackageDirectoryName,
        [Parameter(Mandatory = $true)][string] $ZipPath
    )

    $records = @(Get-Phase0SPackageTreeRecords -PackageRoot $PackageRoot)
    Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction Stop
    $archive = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        $entries = @($archive.Entries)
        if ($entries.Count -ne $records.Count) {
            throw 'ZIP entry count differs from the package tree.'
        }
        for ($index = 0; $index -lt $records.Count; $index++) {
            $entry = $entries[$index]
            $record = $records[$index]
            $expectedName = $PackageDirectoryName + '/' + $record.path
            if ($entry.FullName -cne $expectedName -or [int64] $entry.Length -ne [int64] $record.length) {
                throw 'ZIP entry identity differs from the package tree.'
            }
            $entryStream = $entry.Open()
            try {
                $sha = [System.Security.Cryptography.SHA256]::Create()
                try {
                    $hashBytes = $sha.ComputeHash($entryStream)
                    $hash = ($hashBytes | ForEach-Object { $_.ToString('X2') }) -join ''
                }
                finally {
                    $sha.Dispose()
                }
            }
            finally {
                $entryStream.Dispose()
            }
            if ($hash -cne [string] $record.sha256) {
                throw 'ZIP entry hash differs from the package tree.'
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

$gitRoot = [string] (Invoke-Phase0SGit -Arguments @('rev-parse', '--show-toplevel') | Select-Object -First 1)
if ([System.IO.Path]::GetFullPath($gitRoot.Trim()).TrimEnd('\') -cne $repositoryRoot) {
    throw 'The script directory is not the active Git repository root.'
}
$status = @(Invoke-Phase0SGit -Arguments @('status', '--porcelain=v1', '--untracked-files=all'))
if ($status.Count -ne 0) {
    throw 'Phase 0-S owner package requires a clean commit.'
}
$sourceCommit = ([string] (Invoke-Phase0SGit -Arguments @('rev-parse', 'HEAD') | Select-Object -First 1)).Trim()
if ($sourceCommit -notmatch '^[0-9a-f]{40}$') {
    throw 'The source commit identity is invalid.'
}
$packageId = 'phase0s-' + $sourceCommit

$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\')
if ([string]::IsNullOrWhiteSpace($outputRoot) -or (Get-Phase0SPathState -Path $outputRoot).exists) {
    throw 'OutputDirectory must name a new directory.'
}
$outputParent = Split-Path -Parent $outputRoot
if (-not (Test-Phase0SOrdinaryDirectory -Path $outputParent)) {
    throw 'OutputDirectory parent must be an existing ordinary directory.'
}
$repositoryPrefix = $repositoryRoot + '\'
if ($outputRoot.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    $outputsPrefix = (Join-Path $repositoryRoot 'outputs').TrimEnd('\') + '\'
    $artifactsPrefix = (Join-Path $repositoryRoot 'artifacts').TrimEnd('\') + '\'
    if (-not $outputRoot.StartsWith($outputsPrefix, [System.StringComparison]::OrdinalIgnoreCase) -and
        -not $outputRoot.StartsWith($artifactsPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'Repository-local output must be beneath outputs or artifacts.'
    }
}

$buildOutput = @(& (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release -RequireClean 2>&1)
if (-not $?) {
    throw 'The locked Release build failed.'
}
$statusAfterBuild = @(Invoke-Phase0SGit -Arguments @('status', '--porcelain=v1', '--untracked-files=all'))
$headAfterBuild = ([string] (Invoke-Phase0SGit -Arguments @('rev-parse', 'HEAD') | Select-Object -First 1)).Trim()
if ($statusAfterBuild.Count -ne 0 -or $headAfterBuild -cne $sourceCommit) {
    throw 'The source tree changed during the Release build.'
}

$buildRecordPath = Join-Path $repositoryRoot 'artifacts\build\Release\build-record.json'
$buildRecord = (Get-Phase0SStrictUtf8Text -Path $buildRecordPath -MaximumLength 1048576) | ConvertFrom-Json
if ([int] $buildRecord.schemaVersion -ne 2 -or [string] $buildRecord.commit -cne $sourceCommit -or
    -not [bool] $buildRecord.clean -or [string] $buildRecord.sdk -cne '10.0.203' -or
    [string] $buildRecord.configuration -cne 'Release') {
    throw 'The Release build record does not describe the clean source commit.'
}
if (@($buildRecord.outputs | Where-Object { [string] $_.path -match '(?i)(^|\\)0Harmony\.dll$' }).Count -ne 0) {
    throw 'Ordinary Release output unexpectedly contains Harmony.'
}

$bootstrapOutput = Get-Phase0SBuildOutputPath -ProjectName 'JueMingR.Bootstrap' -FileName 'JueMingR.Bootstrap.dll'
$hostOutput = Get-Phase0SBuildOutputPath -ProjectName 'JueMingR.TerrariaHost' -FileName 'JueMingR.TerrariaHost.dll'
$platformOutput = Get-Phase0SBuildOutputPath -ProjectName 'JueMingR.Platform' -FileName 'JueMingR.Platform.dll'
$featuresOutput = Get-Phase0SBuildOutputPath -ProjectName 'JueMingR.Features' -FileName 'JueMingR.Features.dll'
$infrastructureOutput = Get-Phase0SBuildOutputPath -ProjectName 'JueMingR.Infrastructure' -FileName 'JueMingR.Infrastructure.dll'
$hostIdentity = Get-Phase0SAssemblyFileIdentity -Path $hostOutput
if ($hostIdentity.simpleName -cne 'JueMingR.TerrariaHost' -or $hostIdentity.version -cne '0.0.0.0') {
    throw 'The Host Release output identity is invalid.'
}

$harmonyPath = Join-Path $repositoryRoot 'external\Harmony\0Harmony.dll'
$harmonyLicensePath = Join-Path $repositoryRoot 'external\Harmony\LICENSE'
$harmonyIdentity = Get-Phase0SAssemblyFileIdentity -Path $harmonyPath
if ($harmonyIdentity.fullName -cne '0Harmony, Version=2.4.2.0, Culture=neutral, PublicKeyToken=null' -or
    $harmonyIdentity.mvid -cne '024a0e6e-c8c2-437e-ad04-7b6279389c23' -or
    $harmonyIdentity.sha256 -cne '7B9E756306FA3D7620E02A857C8927A6AB04973F9BD8A77D3866700A6DEAC55C' -or
    (Get-Phase0SFileSha256 -Path $harmonyLicensePath) -cne '407DDD98200C9F17F49942B5A7791E288A2BD553B59BF2F462DB87DBB21C50BC') {
    throw 'Prepared Harmony identity or license is invalid.'
}

$packageDirectoryName = 'JueMingR-Phase0S-' + $sourceCommit
$zipFileName = $packageDirectoryName + '.zip'
$stagingToken = [Guid]::NewGuid().ToString('N')
$stagingRoot = Join-Path $outputParent ((Split-Path -Leaf $outputRoot) + '.phase0s-stage-' + $stagingToken)
$markerPath = Join-Path $stagingRoot '.phase0s-builder-stage'
$stagingCreated = $false
$markerRemoved = $false
try {
    if ((Get-Phase0SPathState -Path $stagingRoot).exists) {
        throw 'Builder staging path already exists.'
    }
    [System.IO.Directory]::CreateDirectory($stagingRoot) | Out-Null
    $stagingCreated = $true
    Write-Phase0SBuilderTextCreateNew -Path $markerPath -Text ($stagingToken + [Environment]::NewLine)

    $packageRoot = Join-Path $stagingRoot $packageDirectoryName
    $payloadRoot = Join-Path $packageRoot 'payload'
    $sidecarPayloadRoot = Join-Path $payloadRoot 'JueMingR.Validation'
    [System.IO.Directory]::CreateDirectory($sidecarPayloadRoot) | Out-Null

    $configText = @(
        '<?xml version="1.0" encoding="utf-8"?>',
        '<configuration>',
        '  <runtime>',
        '    <appDomainManagerAssembly value="JueMingR.Bootstrap, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null" />',
        '    <appDomainManagerType value="JueMingR.Bootstrap.Phase0SAppDomainManager" />',
        '    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">',
        '      <probing privatePath="JueMingR.Validation" />',
        '    </assemblyBinding>',
        '  </runtime>',
        '</configuration>'
    ) -join [Environment]::NewLine
    $configText += [Environment]::NewLine
    Write-Phase0SBuilderTextCreateNew -Path (Join-Path $payloadRoot 'Terraria.exe.config') -Text $configText

    Copy-Phase0SBuilderFileCreateNew -SourcePath $bootstrapOutput -DestinationPath (Join-Path $payloadRoot 'JueMingR.Bootstrap.dll')
    Copy-Phase0SBuilderFileCreateNew -SourcePath $hostOutput -DestinationPath (Join-Path $sidecarPayloadRoot 'JueMingR.TerrariaHost.dll')
    Copy-Phase0SBuilderFileCreateNew -SourcePath $platformOutput -DestinationPath (Join-Path $sidecarPayloadRoot 'JueMingR.Platform.dll')
    Copy-Phase0SBuilderFileCreateNew -SourcePath $featuresOutput -DestinationPath (Join-Path $sidecarPayloadRoot 'JueMingR.Features.dll')
    Copy-Phase0SBuilderFileCreateNew -SourcePath $infrastructureOutput -DestinationPath (Join-Path $sidecarPayloadRoot 'JueMingR.Infrastructure.dll')
    Copy-Phase0SBuilderFileCreateNew -SourcePath $harmonyPath -DestinationPath (Join-Path $sidecarPayloadRoot '0Harmony.dll')

    $runtimeLines = @(
        'schemaVersion=1',
        ('packageId=' + $packageId),
        ('sourceCommit=' + $sourceCommit),
        'targetAssemblySimpleName=Terraria',
        'targetAssemblyVersion=1.4.5.8',
        'targetAssemblyMvid=2c29f6c3-4bd9-4add-9c58-da159804e083',
        'targetAssemblySha256=960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3',
        'targetTypeName=Terraria.Main',
        'targetMethodName=Initialize',
        'targetMethodMetadataToken=0x06000CDE',
        'targetMethodIsStatic=false',
        'targetMethodReturnType=System.Void',
        'targetMethodParameterCount=0',
        'hostAssemblySimpleName=JueMingR.TerrariaHost',
        'hostAssemblyVersion=0.0.0.0',
        ('hostAssemblyMvid=' + $hostIdentity.mvid),
        ('hostAssemblySha256=' + $hostIdentity.sha256),
        'harmonyAssemblySimpleName=0Harmony',
        'harmonyAssemblyVersion=2.4.2.0',
        'harmonyAssemblyMvid=024a0e6e-c8c2-437e-ad04-7b6279389c23',
        'harmonyAssemblySha256=7B9E756306FA3D7620E02A857C8927A6AB04973F9BD8A77D3866700A6DEAC55C',
        'patchOwner=JueMingR.Phase0S.MainInitialize',
        'evidenceFileName=phase-0-s-evidence.log'
    )
    if ($runtimeLines.Count -ne 23) {
        throw 'Runtime manifest construction did not produce 23 lines.'
    }
    $runtimeText = ($runtimeLines -join [Environment]::NewLine) + [Environment]::NewLine
    $runtimePath = Join-Path $sidecarPayloadRoot 'phase-0-s-runtime.manifest'
    Write-Phase0SBuilderTextCreateNew -Path $runtimePath -Text $runtimeText
    $runtimeCheck = Read-Phase0SRuntimeManifest -Path $runtimePath
    if ($runtimeCheck.packageId -cne $packageId -or $runtimeCheck.sourceCommit -cne $sourceCommit -or
        $runtimeCheck.targetMethodMetadataToken -cne '0x06000CDE' -or
        $runtimeCheck.hostAssemblyMvid -cne $hostIdentity.mvid -or
        $runtimeCheck.hostAssemblySha256 -cne $hostIdentity.sha256) {
        throw 'Runtime manifest round-trip verification failed.'
    }

    $payloadEntries = New-Object System.Collections.Generic.List[object]
    foreach ($relativePath in $script:Phase0SExpectedPayloadPaths) {
        $path = Resolve-Phase0SContainedPath -Root $payloadRoot -RelativePath $relativePath
        $item = Get-Item -LiteralPath $path -Force -ErrorAction Stop
        $payloadEntries.Add([pscustomobject][ordered]@{
            installRelativePath = $relativePath
            length = [int64] $item.Length
            sha256 = Get-Phase0SFileSha256 -Path $path
        })
    }
    $manifest = [pscustomobject][ordered]@{
        schemaVersion = 1
        packageId = $packageId
        sourceCommit = $sourceCommit
        target = [pscustomobject][ordered]@{
            simpleName = 'Terraria'
            version = '1.4.5.8'
            mvid = '2c29f6c3-4bd9-4add-9c58-da159804e083'
            sha256 = '960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3'
        }
        payload = $payloadEntries.ToArray()
    }
    $manifestPath = Join-Path $packageRoot 'phase-0-s-package.manifest.json'
    Write-Phase0SBuilderTextCreateNew -Path $manifestPath -Text (ConvertTo-Phase0SCanonicalPackageManifestText -Manifest $manifest)

    foreach ($sourceAndName in @(
        @((Join-Path $repositoryRoot 'scripts\phase0s\Install-Phase0S.ps1'), 'Install-Phase0S.ps1'),
        @((Join-Path $repositoryRoot 'scripts\phase0s\Restore-Phase0S.ps1'), 'Restore-Phase0S.ps1'),
        @((Join-Path $repositoryRoot 'scripts\phase0s\Phase0S.ScriptSupport.ps1'), 'Phase0S.ScriptSupport.ps1'),
        @((Join-Path $repositoryRoot 'scripts\phase0s\Phase0S-Owner-Test-Card.zh-CN.md'), 'Phase0S-Owner-Test-Card.zh-CN.md'),
        @((Join-Path $repositoryRoot 'THIRD-PARTY-NOTICES.md'), 'THIRD-PARTY-NOTICES.md'),
        @($harmonyLicensePath, 'Harmony-2.4.2-LICENSE.txt')
    )) {
        Copy-Phase0SBuilderFileCreateNew -SourcePath $sourceAndName[0] -DestinationPath (Join-Path $packageRoot $sourceAndName[1])
    }

    $validatedPackage = Read-Phase0SPackage -PackageRoot $packageRoot
    if ($validatedPackage.packageId -cne $packageId -or $validatedPackage.sourceCommit -cne $sourceCommit) {
        throw 'Final package identity verification failed.'
    }
    $packageFiles = @(Assert-Phase0SFixedPackageTree -PackageRoot $packageRoot)
    Assert-Phase0SPackageHasNoPrivatePath -PackageRoot $packageRoot

    $zipPath = Join-Path $stagingRoot $zipFileName
    New-Phase0SDeterministicZip -PackageRoot $packageRoot -PackageDirectoryName $packageDirectoryName -ZipPath $zipPath
    Assert-Phase0SZipMatchesPackage -PackageRoot $packageRoot -PackageDirectoryName $packageDirectoryName -ZipPath $zipPath
    $zipItem = Get-Item -LiteralPath $zipPath -Force -ErrorAction Stop

    $externalBuildRecord = [ordered]@{
        schemaVersion = 1
        sourceCommit = $sourceCommit
        clean = $true
        packageId = $packageId
        sdk = '10.0.203'
        configuration = 'Release'
        targetFramework = 'net472'
        platformTarget = 'x86'
        buildEntry = 'scripts/build.ps1'
        solution = 'JueMingR.sln'
        sourceBuildRecordSha256 = Get-Phase0SFileSha256 -Path $buildRecordPath
        target = [ordered]@{
            simpleName = 'Terraria'
            version = '1.4.5.8'
            mvid = '2c29f6c3-4bd9-4add-9c58-da159804e083'
            sha256 = '960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3'
        }
        harmony = [ordered]@{
            package = 'Lib.Harmony'
            version = '2.4.2'
            assemblyMvid = $harmonyIdentity.mvid
            assemblySha256 = $harmonyIdentity.sha256
            licenseSha256 = Get-Phase0SFileSha256 -Path $harmonyLicensePath
        }
        packageDirectory = [ordered]@{
            name = $packageDirectoryName
            files = $packageFiles
        }
        archive = [ordered]@{
            fileName = $zipFileName
            length = [int64] $zipItem.Length
            sha256 = Get-Phase0SFileSha256 -Path $zipPath
        }
        generatedAtUtc = [DateTime]::UtcNow.ToString('O', [System.Globalization.CultureInfo]::InvariantCulture)
    }
    $externalBuildRecordPath = Join-Path $stagingRoot 'phase-0-s-build-record.json'
    Write-Phase0SBuilderTextCreateNew -Path $externalBuildRecordPath -Text (($externalBuildRecord | ConvertTo-Json -Depth 10) + [Environment]::NewLine)
    $recordText = Get-Phase0SStrictUtf8Text -Path $externalBuildRecordPath -MaximumLength 1048576
    if ($recordText.IndexOf($repositoryRoot, [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        $recordText -match '(?i)[A-Z]:[\\/](?:Users|Documents)[\\/]') {
        throw 'External build record contains a private absolute path.'
    }

    $rootNames = @(Get-ChildItem -LiteralPath $stagingRoot -Force -ErrorAction Stop | Sort-Object Name | ForEach-Object { $_.Name })
    $expectedRootNames = @('.phase0s-builder-stage', $packageDirectoryName, 'phase-0-s-build-record.json', $zipFileName) | Sort-Object
    if (($rootNames -join '|') -cne ($expectedRootNames -join '|')) {
        throw 'Builder staging has an unexpected final object.'
    }
    if ([System.IO.File]::ReadAllText($markerPath).Trim() -cne $stagingToken) {
        throw 'Builder staging marker changed.'
    }
    [System.IO.File]::Delete($markerPath)
    $markerRemoved = $true
    if ((Get-Phase0SPathState -Path $outputRoot).exists) {
        throw 'OutputDirectory appeared before final promotion.'
    }
    [System.IO.Directory]::Move($stagingRoot, $outputRoot)
    $stagingCreated = $false

    $summary = [ordered]@{
        schemaVersion = 1
        status = 'success'
        packageId = $packageId
        directoryName = $packageDirectoryName
        archiveFileName = $zipFileName
        archiveSha256 = [string] $externalBuildRecord.archive.sha256
        buildRecordFileName = 'phase-0-s-build-record.json'
    }
    Write-Output ($summary | ConvertTo-Json -Compress)
}
finally {
    if ($stagingCreated -and -not $markerRemoved -and [System.IO.Directory]::Exists($stagingRoot) -and
        [System.IO.File]::Exists($markerPath)) {
        $marker = [System.IO.File]::ReadAllText($markerPath).Trim()
        $parentPrefix = [System.IO.Path]::GetFullPath($outputParent).TrimEnd('\') + '\'
        $stagingFullPath = [System.IO.Path]::GetFullPath($stagingRoot)
        if ($marker -ceq $stagingToken -and
            $stagingFullPath.StartsWith($parentPrefix, [System.StringComparison]::OrdinalIgnoreCase) -and
            (Split-Path -Leaf $stagingFullPath).EndsWith('.phase0s-stage-' + $stagingToken, [System.StringComparison]::Ordinal)) {
            Remove-Item -LiteralPath $stagingRoot -Recurse -Force
        }
    }
}
