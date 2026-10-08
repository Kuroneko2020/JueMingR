[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $PackageRoot,
    [string] $TerrariaDirectory,
    [ValidateSet('startup', 'first-world-refocus')][string] $Window = 'startup',
    [switch] $CandidateOnly,
    [switch] $Launch,
    [Parameter(DontShow=$true)][switch] $MetadataOnly
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
. (Join-Path $PSScriptRoot 'Phase0S.ScriptSupport.ps1')
if ($Launch -and ($CandidateOnly -or $MetadataOnly)) { throw 'Candidate-only validation cannot launch.' }
$package = Read-Phase0SPackage -PackageRoot $PackageRoot
$descriptorPath = Join-Path $package.root 'input-diagnostic-candidate.json'
$descriptor = Get-Content -LiteralPath $descriptorPath -Raw -Encoding UTF8 | ConvertFrom-Json
Assert-Phase0SExactProperties -Object $descriptor -ExpectedNames @('schema', 'sourceCommit', 'packageManifestSha256', 'hostSha256', 'hostMvid', 'windows')
if ($descriptor.schema -cne 'input-diagnostic-candidate-1' -or $descriptor.sourceCommit -cne $package.sourceCommit -or
    $descriptor.packageManifestSha256 -cne (Get-FileHash -LiteralPath $package.manifestPath).Hash -or
    ($descriptor.windows -join '|') -cne 'startup|first-world-refocus') { throw 'Diagnostic candidate identity differs.' }
$hostPath = Resolve-Phase0SContainedPath -Root $package.payloadRoot -RelativePath 'JueMingR.Validation/JueMingR.TerrariaHost.dll'
if ((Get-FileHash -LiteralPath $hostPath).Hash -cne $descriptor.hostSha256) { throw 'Diagnostic Host differs.' }
# Metadata inspection runs in the existing x86 Framework runtime; it does not
# initialize the recorder or load/operate Terraria. No parent environment edit.
if ($MetadataOnly) {
    $assembly = [Reflection.Assembly]::ReflectionOnlyLoadFrom($hostPath)
    if ($null -eq $assembly.GetType('JueMingR.TerrariaHost.Input.InputDiagnosticTrace',$false) -or
        $assembly.ManifestModule.ModuleVersionId.ToString('D') -cne $descriptor.hostMvid) { throw 'Diagnostic recorder/type identity differs.' }
    Write-Output $assembly.ManifestModule.ModuleVersionId.ToString('D')
    return
}
$runtime = Join-Path $env:SystemRoot 'SysWOW64/WindowsPowerShell/v1.0/powershell.exe'
$priorPreference = $ErrorActionPreference
try {
    $ErrorActionPreference = 'Continue'
    $metadata = @(& $runtime -NoProfile -NonInteractive -File $PSCommandPath -PackageRoot $package.root -CandidateOnly -MetadataOnly 2>&1)
    $code = $LASTEXITCODE
} finally { $ErrorActionPreference = $priorPreference }
$mvid = ($metadata | Out-String).Trim()
if ($code -ne 0 -or $mvid -cne $descriptor.hostMvid) { throw ('Diagnostic type/MVID check failed: exit=' + $code + '; metadata=' + $mvid) }
if (-not $CandidateOnly) {
    $target = Get-Phase0SValidatedTerrariaDirectory -TerrariaDirectory $TerrariaDirectory
    if (-not (Test-Phase0STerrariaIdentity -Path (Join-Path $target 'Terraria.exe'))) { throw 'Terraria identity differs.' }
    if (Get-Process -Name Terraria, TerrariaServer, JueMingR.PredictionWorker -ErrorAction SilentlyContinue) { throw 'Exit the game before this one-use diagnostic start.' }
    $ownership = Test-Phase0SRestoreOwnership -TerrariaDirectory $target -Package $package
    if (-not $ownership.valid -or $ownership.noop) { throw 'The exact diagnostic candidate is not installed with valid ownership.' }
    # Restore also accepts safely recoverable partial installs. Launch requires
    # every final payload and the exact receipt, rather than that weaker state.
    foreach ($entry in $package.payload) {
        $installed = Resolve-Phase0SContainedPath -Root $target -RelativePath $entry.installRelativePath
        if (-not (Test-Phase0SFileMatchesPayloadEntry -Path $installed -Entry $entry)) { throw 'Diagnostic installation is incomplete or changed.' }
    }
}
if ($Launch) {
    # Only the owner invokes -Launch after separately approving installation.
    # These switches belong to this child, never system/user/Steam settings.
    $game = New-Object Diagnostics.ProcessStartInfo
    $game.FileName = Join-Path $target 'Terraria.exe'; $game.WorkingDirectory = $target
    $game.UseShellExecute = $false
    $game.EnvironmentVariables['JMR_INPUT_DIAGNOSTIC'] = '1'
    $game.EnvironmentVariables['JMR_INPUT_DIAGNOSTIC_WINDOW'] = $Window
    $launched = [Diagnostics.Process]::Start($game)
    try { Write-Output ('Owner diagnostic process started: ' + $launched.Id) } finally { $launched.Dispose() }
} else {
    [ordered]@{status='PREFLIGHT_PASS';candidateOnly=[bool]$CandidateOnly;sourceCommit=$package.sourceCommit;hostSha256=$descriptor.hostSha256;hostMvid=$mvid;window=$Window;launched=$false} | ConvertTo-Json
}
