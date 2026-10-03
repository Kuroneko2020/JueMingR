[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string] $ZipPath,
    [Parameter(Mandatory=$true)][string] $BuildRecordPath,
    [Parameter(Mandatory=$true)][ValidatePattern('^[0-9a-f]{40}$')][string] $ExpectedSourceCommit,
    [Parameter(Mandatory=$true)][ValidatePattern('^[0-9A-Fa-f]{64}$')][string] $ExpectedZipSha256
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2.0
$watch=[Diagnostics.Stopwatch]::StartNew()
$temporary=$null
try {
    . (Join-Path $PSScriptRoot 'phase0s/PackageVerification.Support.ps1')
    # Use repository validation code, never dot-source code supplied by a ZIP.
    . (Join-Path $PSScriptRoot 'phase0s/Phase0S.ScriptSupport.ps1')
    $zip=[IO.Path]::GetFullPath($ZipPath); $recordPath=[IO.Path]::GetFullPath($BuildRecordPath)
    $zipHash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
    if ($zipHash -cne $ExpectedZipSha256.ToUpperInvariant()) {throw 'ZIP_IDENTITY_MISMATCH'}
    $recordHash=(Get-FileHash -LiteralPath $recordPath -Algorithm SHA256).Hash
    $record=Get-Content -LiteralPath $recordPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($record.schemaVersion -ne 1 -or $record.clean -ne $true -or $record.sourceCommit -cne $ExpectedSourceCommit -or
        $record.configuration -cne 'Release' -or $record.targetFramework -cne 'net472' -or $record.platformTarget -cne 'x86' -or
        $record.archive.sha256 -cne $zipHash -or $record.archive.length -ne (Get-Item -LiteralPath $zip).Length) {throw 'BUILD_RECORD_IDENTITY_MISMATCH'}
    $count=Test-RecordedPackageArchive $zip $record
    $temporary=Join-Path ([IO.Path]::GetTempPath()) ('JueMingR-verify-package-'+[Guid]::NewGuid().ToString('N'))
    [IO.Compression.ZipFile]::ExtractToDirectory($zip,$temporary)
    $packageRoot=Join-Path $temporary $record.packageDirectory.name
    $package=Read-Phase0SPackage -PackageRoot $packageRoot
    if ($package.sourceCommit -cne $ExpectedSourceCommit -or $package.packageId -cne $record.packageId) {throw 'PACKAGE_IDENTITY_MISMATCH'}
    # Guard the evidence window; metadata-only checks never turn this into a
    # current-HEAD build, a synthetic load PASS or an owner gameplay result.
    if ((Get-FileHash -LiteralPath $zip).Hash -cne $zipHash -or (Get-FileHash -LiteralPath $recordPath).Hash -cne $recordHash) {throw 'INPUT_CHANGED_DURING_VERIFICATION'}
    $tools=@('verify-existing-package.ps1','phase0s/PackageVerification.Support.ps1','phase0s/Phase0S.ScriptSupport.ps1') | ForEach-Object {
        [ordered]@{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $_)).Hash}
    }
    [ordered]@{status='PASS';scope='archive-and-package-identity';sourceCommit=$package.sourceCommit;packageId=$package.packageId;
        zipSha256=$zipHash;zipBytes=(Get-Item -LiteralPath $zip).Length;buildRecordSha256=$recordHash;fileCount=$count;
        payloadCount=@($package.payload).Count;tools=@($tools);milliseconds=$watch.ElapsedMilliseconds;
        realGameLoad='NOT_EXECUTED';productChecks='NOT_RERUN'} | ConvertTo-Json -Depth 5
} catch {
    [Console]::Out.WriteLine(([ordered]@{status='FAIL';scope='archive-and-package-identity';error=$_.Exception.Message;milliseconds=$watch.ElapsedMilliseconds}|ConvertTo-Json -Compress))
    exit 1
} finally {
    if ($temporary -and [IO.Directory]::Exists($temporary)) {
        $absolute=[IO.Path]::GetFullPath($temporary);$temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
        if (-not $absolute.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($absolute).StartsWith('JueMingR-verify-package-')) {throw 'Unsafe verification cleanup.'}
        Remove-Item -LiteralPath $absolute -Recurse -Force
    }
}
