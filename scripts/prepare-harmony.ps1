[CmdletBinding()]
param(
    [switch] $VerifyOnly,
    [string] $HarmonyPackagePath,
    [switch] $DownloadOfficial
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$baselinePath = Join-Path $repositoryRoot 'eng\Harmony.baseline.json'
$externalDirectory = Join-Path $repositoryRoot 'external'
$preparedDirectory = Join-Path $externalDirectory 'Harmony'
$receiptFileName = 'preparation-receipt.json'

function Get-FileHashUpper {
    param([string] $Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Assert-Equal {
    param([string] $Actual, [string] $Expected, [string] $Description)

    if ($Actual -cne $Expected) {
        throw ("{0} mismatch. Expected {1}; actual {2}." -f $Description, $Expected, $Actual)
    }
}

function Get-SingleBaselineEntry {
    param([object] $Baseline, [string] $Role)

    $matches = @($Baseline.entries | Where-Object { [string] $_.role -ceq $Role })
    if ($matches.Count -ne 1) {
        throw "Harmony baseline must contain exactly one $Role entry."
    }

    return $matches[0]
}

function Get-PublicKeyTokenText {
    param([System.Reflection.AssemblyName] $AssemblyName)

    $token = $AssemblyName.GetPublicKeyToken()
    if ($null -eq $token -or $token.Length -eq 0) {
        return ''
    }

    return (($token | ForEach-Object { $_.ToString('x2') }) -join '')
}

function Get-AssemblyMvid {
    param([string] $Path)

    if ($null -ne ('System.Reflection.PortableExecutable.PEReader' -as [type])) {
        $stream = [System.IO.File]::OpenRead($Path)
        try {
            $reader = [System.Reflection.PortableExecutable.PEReader]::new($stream)
            try {
                $metadata = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($reader)
                return $metadata.GetGuid($metadata.GetModuleDefinition().Mvid).ToString('D')
            }
            finally {
                $reader.Dispose()
            }
        }
        finally {
            $stream.Dispose()
        }
    }

    $powershellPath = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    if (-not [System.IO.File]::Exists($powershellPath)) {
        throw 'Windows PowerShell 5.1 is unavailable for isolated MVID inspection.'
    }

    $environmentName = 'JUEMINGR_PHASE0S_MVID_PATH'
    $previousEnvironmentValue = [Environment]::GetEnvironmentVariable($environmentName, 'Process')
    $previousErrorActionPreference = $ErrorActionPreference
    $inspectionCommand = '[Console]::Out.WriteLine([Reflection.Assembly]::ReflectionOnlyLoadFrom($env:JUEMINGR_PHASE0S_MVID_PATH).ManifestModule.ModuleVersionId.ToString("D"))'
    $encodedInspectionCommand = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($inspectionCommand))
    $output = @()
    $exitCode = -1
    try {
        [Environment]::SetEnvironmentVariable($environmentName, $Path, 'Process')
        $ErrorActionPreference = 'Continue'
        $output = @(& $powershellPath -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
            -EncodedCommand $encodedInspectionCommand 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
        [Environment]::SetEnvironmentVariable($environmentName, $previousEnvironmentValue, 'Process')
    }

    $mvid = if ($output.Count -eq 1) { ([string] $output[0]).Trim() } else { '' }
    $parsedMvid = [Guid]::Empty
    if ($exitCode -ne 0 -or
        -not [Guid]::TryParseExact($mvid, 'D', [ref] $parsedMvid) -or
        $parsedMvid.ToString('D') -cne $mvid) {
        throw 'Isolated Harmony MVID inspection failed.'
    }
    return $mvid
}

function Test-HarmonyAssembly {
    param([string] $Path, [object] $AssemblyEntry)

    $assemblyName = [Reflection.AssemblyName]::GetAssemblyName($Path)
    Assert-Equal -Actual $assemblyName.Name -Expected ([string] $AssemblyEntry.assemblySimpleName) -Description 'Harmony assembly simple name'
    Assert-Equal -Actual $assemblyName.Version.ToString() -Expected ([string] $AssemblyEntry.assemblyVersion) -Description 'Harmony assembly version'
    Assert-Equal -Actual ([System.Diagnostics.FileVersionInfo]::GetVersionInfo($Path)).FileVersion `
        -Expected ([string] $AssemblyEntry.fileVersion) -Description 'Harmony file version'
    Assert-Equal -Actual (Get-PublicKeyTokenText -AssemblyName $assemblyName) `
        -Expected ([string] $AssemblyEntry.publicKeyToken) -Description 'Harmony assembly public key token'
    Assert-Equal -Actual $assemblyName.FullName `
        -Expected ([string] $AssemblyEntry.assemblyFullName) -Description 'Harmony assembly full name'
    Assert-Equal -Actual (Get-AssemblyMvid -Path $Path) `
        -Expected ([string] $AssemblyEntry.mvid) -Description 'Harmony assembly MVID'
}

function Assert-PreparedSet {
    param(
        [string] $Directory,
        [object] $PackageEntry,
        [object] $AssemblyEntry,
        [object] $LicenseEntry,
        [object] $Baseline,
        [bool] $RequireReceipt
    )

    if (-not [System.IO.Directory]::Exists($Directory)) {
        throw "Prepared Harmony directory is missing: $Directory"
    }

    $expectedNames = @([string] $AssemblyEntry.preparedFileName, [string] $LicenseEntry.preparedFileName)
    if ($RequireReceipt) {
        $expectedNames += $receiptFileName
    }

    $items = @(Get-ChildItem -LiteralPath $Directory -Force)
    $unexpectedItems = @($items | Where-Object {
        $_ -is [System.IO.DirectoryInfo] -or $expectedNames -notcontains $_.Name
    })
    if ($items.Count -ne $expectedNames.Count -or $unexpectedItems.Count -ne 0) {
        throw 'Prepared Harmony set has missing, unexpected, or directory entries.'
    }

    $assemblyPath = Join-Path $Directory ([string] $AssemblyEntry.preparedFileName)
    $licensePath = Join-Path $Directory ([string] $LicenseEntry.preparedFileName)
    $assemblyLength = (Get-Item -LiteralPath $assemblyPath).Length
    $licenseLength = (Get-Item -LiteralPath $licensePath).Length
    if ($assemblyLength -ne [int64] $AssemblyEntry.length) {
        throw ("Prepared Harmony DLL length mismatch. Expected {0}; actual {1}." -f $AssemblyEntry.length, $assemblyLength)
    }
    if ($licenseLength -ne [int64] $LicenseEntry.length) {
        throw ("Prepared Harmony license length mismatch. Expected {0}; actual {1}." -f $LicenseEntry.length, $licenseLength)
    }

    Assert-Equal -Actual (Get-FileHashUpper -Path $assemblyPath) -Expected ([string] $AssemblyEntry.sha256) -Description 'Prepared Harmony DLL SHA-256'
    Assert-Equal -Actual (Get-FileHashUpper -Path $licensePath) -Expected ([string] $LicenseEntry.sha256) -Description 'Prepared Harmony license SHA-256'
    Test-HarmonyAssembly -Path $assemblyPath -AssemblyEntry $AssemblyEntry

    if ($RequireReceipt) {
        $receiptPath = Join-Path $Directory $receiptFileName
        $receiptText = [System.IO.File]::ReadAllText($receiptPath)
        if ($receiptText -match '(?i)(?:[A-Z]:\\|\\\\)') {
            throw 'Harmony preparation receipt must not contain a filesystem path.'
        }

        $receipt = $receiptText | ConvertFrom-Json
        Assert-Equal -Actual ([string] $receipt.schemaVersion) -Expected '1' -Description 'Harmony preparation receipt schemaVersion'
        Assert-Equal -Actual ([string] $receipt.packageId) -Expected ([string] $Baseline.packageId) -Description 'Harmony preparation receipt packageId'
        Assert-Equal -Actual ([string] $receipt.packageVersion) -Expected ([string] $Baseline.packageVersion) -Description 'Harmony preparation receipt packageVersion'
        Assert-Equal -Actual ([string] $receipt.packageSha256) -Expected ([string] $PackageEntry.sha256) -Description 'Harmony preparation receipt package SHA-256'
        Assert-Equal -Actual ([string] $receipt.assemblySha256) -Expected ([string] $AssemblyEntry.sha256) -Description 'Harmony preparation receipt assembly SHA-256'
        Assert-Equal -Actual ([string] $receipt.licenseSha256) -Expected ([string] $LicenseEntry.sha256) -Description 'Harmony preparation receipt license SHA-256'
        Assert-Equal -Actual ([string] $receipt.assemblyFullName) -Expected ([string] $AssemblyEntry.assemblyFullName) -Description 'Harmony preparation receipt assembly full name'
        Assert-Equal -Actual ([string] $receipt.mvid) -Expected ([string] $AssemblyEntry.mvid) -Description 'Harmony preparation receipt MVID'
        Assert-Equal -Actual ([string] $receipt.upstreamCommit) -Expected ([string] $Baseline.upstreamCommit) -Description 'Harmony preparation receipt upstream commit'
    }
}

function Copy-ZipEntry {
    param([object] $Archive, [string] $EntryName, [string] $DestinationPath)

    $entries = @($Archive.Entries | Where-Object { $_.FullName -ceq $EntryName })
    if ($entries.Count -ne 1) {
        throw "Harmony package must contain exactly one $EntryName entry."
    }

    $input = $entries[0].Open()
    try {
        $output = [System.IO.File]::Open($DestinationPath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        try {
            $input.CopyTo($output)
        }
        finally {
            $output.Dispose()
        }
    }
    finally {
        $input.Dispose()
    }
}

if ($VerifyOnly -and ($HarmonyPackagePath -or $DownloadOfficial)) {
    throw 'VerifyOnly cannot be combined with a Harmony preparation source.'
}
if (-not $VerifyOnly -and (($HarmonyPackagePath -and $DownloadOfficial) -or (-not $HarmonyPackagePath -and -not $DownloadOfficial))) {
    throw 'Preparation requires exactly one of HarmonyPackagePath or DownloadOfficial.'
}
if (-not [System.IO.File]::Exists($baselinePath)) {
    throw 'Harmony baseline is missing.'
}

$baseline = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
if ([int] $baseline.schemaVersion -ne 1 -or [string] $baseline.packageId -cne 'Lib.Harmony' -or [string] $baseline.packageVersion -cne '2.4.2') {
    throw 'Harmony baseline schema or package identity is invalid.'
}
$packageEntry = Get-SingleBaselineEntry -Baseline $baseline -Role 'package'
$assemblyEntry = Get-SingleBaselineEntry -Baseline $baseline -Role 'assembly'
$licenseEntry = Get-SingleBaselineEntry -Baseline $baseline -Role 'license'

if ([System.IO.Directory]::Exists($preparedDirectory)) {
    Assert-PreparedSet -Directory $preparedDirectory -PackageEntry $packageEntry -AssemblyEntry $assemblyEntry -LicenseEntry $licenseEntry -Baseline $baseline -RequireReceipt $true
    Write-Output 'PASS: existing prepared Harmony input is valid.'
    exit 0
}
if ($VerifyOnly) {
    throw 'Prepared Harmony input is missing.'
}

$downloadDirectory = $null
$stagingDirectory = $null
try {
    if ($DownloadOfficial) {
        $downloadDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ('JueMingR-Harmony-' + [Guid]::NewGuid().ToString('N'))
        [System.IO.Directory]::CreateDirectory($downloadDirectory) | Out-Null
        $packagePath = Join-Path $downloadDirectory ([string] $packageEntry.packagePath)
        Invoke-WebRequest -UseBasicParsing -Uri ([string] $packageEntry.url) -OutFile $packagePath
    }
    else {
        $packagePath = [System.IO.Path]::GetFullPath($HarmonyPackagePath)
    }

    if (-not [System.IO.File]::Exists($packagePath)) {
        throw 'The explicit Harmony package path does not exist.'
    }
    if ((Get-Item -LiteralPath $packagePath).Length -ne [int64] $packageEntry.length) {
        throw 'Harmony package length mismatch.'
    }
    Assert-Equal -Actual (Get-FileHashUpper -Path $packagePath) -Expected ([string] $packageEntry.sha256) -Description 'Harmony package SHA-256'

    [System.IO.Directory]::CreateDirectory($externalDirectory) | Out-Null
    $stagingDirectory = Join-Path $externalDirectory ('Harmony.phase0s-stage-' + [Guid]::NewGuid().ToString('N'))
    $externalFullPath = [System.IO.Path]::GetFullPath($externalDirectory).TrimEnd(@(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)) + [System.IO.Path]::DirectorySeparatorChar
    $stagingFullPath = [System.IO.Path]::GetFullPath($stagingDirectory)
    if (-not $stagingFullPath.StartsWith($externalFullPath, [System.StringComparison]::OrdinalIgnoreCase) -or [System.IO.Directory]::Exists($stagingDirectory)) {
        throw 'Harmony staging directory is not a new directory beneath repository external.'
    }
    [System.IO.Directory]::CreateDirectory($stagingDirectory) | Out-Null

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
    try {
        Copy-ZipEntry -Archive $archive -EntryName ([string] $assemblyEntry.packagePath) -DestinationPath (Join-Path $stagingDirectory ([string] $assemblyEntry.preparedFileName))
        Copy-ZipEntry -Archive $archive -EntryName ([string] $licenseEntry.packagePath) -DestinationPath (Join-Path $stagingDirectory ([string] $licenseEntry.preparedFileName))
    }
    finally {
        $archive.Dispose()
    }

    # Verify the fixed assembly and license before publishing a receipt for the prepared set.
    Assert-PreparedSet -Directory $stagingDirectory -PackageEntry $packageEntry -AssemblyEntry $assemblyEntry -LicenseEntry $licenseEntry -Baseline $baseline -RequireReceipt $false
    $receipt = [ordered]@{
        schemaVersion = 1
        packageId = [string] $baseline.packageId
        packageVersion = [string] $baseline.packageVersion
        packageSha256 = [string] $packageEntry.sha256
        assemblySha256 = [string] $assemblyEntry.sha256
        licenseSha256 = [string] $licenseEntry.sha256
        assemblyFullName = [string] $assemblyEntry.assemblyFullName
        mvid = [string] $assemblyEntry.mvid
        upstreamCommit = [string] $baseline.upstreamCommit
    }
    $receiptPath = Join-Path $stagingDirectory $receiptFileName
    [System.IO.File]::WriteAllText($receiptPath, (($receipt | ConvertTo-Json) + [Environment]::NewLine), (New-Object System.Text.UTF8Encoding($false)))
    Assert-PreparedSet -Directory $stagingDirectory -PackageEntry $packageEntry -AssemblyEntry $assemblyEntry -LicenseEntry $licenseEntry -Baseline $baseline -RequireReceipt $true

    if ([System.IO.Directory]::Exists($preparedDirectory)) {
        throw 'Prepared Harmony directory appeared during staging; it was not overwritten.'
    }
    [System.IO.Directory]::Move($stagingDirectory, $preparedDirectory)
    $stagingDirectory = $null
    Write-Output 'PASS: Harmony 2.4.2 prepared from the verified official package.'
}
finally {
    if ($stagingDirectory -and [System.IO.Directory]::Exists($stagingDirectory)) {
        $externalFullPath = [System.IO.Path]::GetFullPath($externalDirectory).TrimEnd(@(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar)) + [System.IO.Path]::DirectorySeparatorChar
        $stagingFullPath = [System.IO.Path]::GetFullPath($stagingDirectory)
        if ($stagingFullPath.StartsWith($externalFullPath, [System.StringComparison]::OrdinalIgnoreCase) -and
            [System.IO.Path]::GetFileName($stagingFullPath).StartsWith('Harmony.phase0s-stage-', [System.StringComparison]::Ordinal)) {
            Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
        }
    }
    if ($downloadDirectory -and [System.IO.Directory]::Exists($downloadDirectory)) {
        $tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd(@(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar)) + [System.IO.Path]::DirectorySeparatorChar
        $downloadFullPath = [System.IO.Path]::GetFullPath($downloadDirectory)
        if ($downloadFullPath.StartsWith($tempRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
            [System.IO.Path]::GetFileName($downloadFullPath).StartsWith('JueMingR-Harmony-', [System.StringComparison]::Ordinal)) {
            Remove-Item -LiteralPath $downloadDirectory -Recurse -Force
        }
    }
}
