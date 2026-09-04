[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',
    [switch] $RequireClean
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$solutionPath = Join-Path $repositoryRoot 'JueMingR.sln'
$baselinePath = Join-Path $repositoryRoot 'eng\TerrariaReferences.baseline.json'
$harmonyBaselinePath = Join-Path $repositoryRoot 'eng\Harmony.baseline.json'
$referencesDirectory = Join-Path $repositoryRoot 'external\TerrariaRefs'
$harmonyReferencesDirectory = Join-Path $repositoryRoot 'external\Harmony'
$buildRoot = Join-Path $repositoryRoot ("artifacts\build\$Configuration")
$workRoot = Join-Path $buildRoot 'work'
$recordPath = Join-Path $buildRoot 'build-record.json'

function Invoke-Git {
    param([string[]] $Arguments)

    $output = @(& git -C $repositoryRoot @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw ('Git command failed: ' + ($output -join [Environment]::NewLine))
    }
    return $output
}

function Get-RelativePath {
    param([string] $BasePath, [string] $Path)

    $baseUri = New-Object System.Uri(($BasePath.TrimEnd('\') + '\'))
    $pathUri = New-Object System.Uri($Path)
    return [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($pathUri).ToString()).Replace('/', '\')
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

    return [Reflection.Assembly]::ReflectionOnlyLoadFrom($Path).ManifestModule.ModuleVersionId.ToString('D')
}

function Get-CompileInputRecord {
    param([string] $LogicalName, [string] $Path)

    $assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($Path)
    return [ordered]@{
        logicalName = $LogicalName
        assemblySimpleName = $assemblyName.Name
        assemblyVersion = $assemblyName.Version.ToString()
        assemblyFullName = $assemblyName.FullName
        mvid = Get-AssemblyMvid -Path $Path
        sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
    }
}

if (-not [System.IO.File]::Exists($solutionPath)) {
    throw 'JueMingR.sln is missing.'
}

$dotnetCommand = Get-Command dotnet.exe -ErrorAction Stop
$sdkVersionOutput = @(& $dotnetCommand.Source --version)
$sdkCommandSucceeded = $?
$sdkVersion = [string] ($sdkVersionOutput | Select-Object -First 1)
if (-not $sdkCommandSucceeded -or $sdkVersion.Trim() -cne '10.0.203') {
    throw "The locked .NET SDK 10.0.203 is unavailable; actual: $sdkVersion."
}

& (Join-Path $PSScriptRoot 'prepare-terraria-references.ps1') -VerifyOnly
if (-not $?) {
    throw 'The fixed local Terraria reference set is not valid.'
}
& (Join-Path $PSScriptRoot 'prepare-harmony.ps1') -VerifyOnly
if (-not $?) {
    throw 'The fixed local Harmony input is not valid.'
}

$statusLines = @(Invoke-Git -Arguments @('status', '--porcelain=v1', '--untracked-files=all'))
$isClean = $statusLines.Count -eq 0
if ($RequireClean -and -not $isClean) {
    throw 'RequireClean was specified, but the repository has tracked or untracked changes.'
}

$commit = [string] (Invoke-Git -Arguments @('rev-parse', 'HEAD') | Select-Object -First 1)
$commit = $commit.Trim()

if ([System.IO.Directory]::Exists($buildRoot)) {
    Remove-Item -LiteralPath $buildRoot -Recurse -Force
}
[System.IO.Directory]::CreateDirectory($workRoot) | Out-Null

$buildArguments = @(
    'build',
    $solutionPath,
    '--configuration', $Configuration,
    '--no-incremental',
    '--nologo',
    '-p:Platform=x86',
    "-p:JueMingRBuildRoot=$workRoot",
    "-p:TerrariaReferencesDirectory=$referencesDirectory",
    "-p:HarmonyReferencesDirectory=$harmonyReferencesDirectory",
    "-p:SourceRevisionId=$commit"
)
& $dotnetCommand.Source @buildArguments
if ($LASTEXITCODE -ne 0) {
    throw "The $Configuration solution build failed."
}

$architectureTests = Join-Path $workRoot "bin\JueMingR.ArchitectureTests\x86\$Configuration\net472\JueMingR.ArchitectureTests.exe"
if (-not [System.IO.File]::Exists($architectureTests)) {
    throw 'The ArchitectureTests executable was not produced.'
}

& $architectureTests $repositoryRoot
if ($LASTEXITCODE -ne 0) {
    throw 'ArchitectureTests failed.'
}

$declaredFiles = @(Get-ChildItem -LiteralPath (Join-Path $workRoot 'bin') -Recurse -File | Where-Object {
    $_.Extension -in @('.dll', '.exe', '.pdb')
} | Sort-Object FullName)
if ($declaredFiles.Count -eq 0) {
    throw 'The build produced no declared DLL, EXE, or PDB outputs.'
}

$forbiddenNames = @(
    'Terraria.exe',
    'ReLogic.dll',
    'Microsoft.Xna.Framework.Game.dll',
    '0Harmony.dll'
)
foreach ($file in $declaredFiles) {
    if ($forbiddenNames -icontains $file.Name -or
        $file.Name.StartsWith('JueMingZ', [StringComparison]::OrdinalIgnoreCase) -or
        $file.Name.StartsWith('TerrariaHelper', [StringComparison]::OrdinalIgnoreCase)) {
        throw "A forbidden file entered the declared build outputs: $($file.Name)."
    }
}

$baseline = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
$harmonyBaseline = Get-Content -LiteralPath $harmonyBaselinePath -Raw | ConvertFrom-Json
$harmonyAssembly = @($harmonyBaseline.entries | Where-Object { [string] $_.role -ceq 'assembly' })
if ($harmonyAssembly.Count -ne 1) {
    throw 'Harmony baseline must contain exactly one assembly entry.'
}
$referenceRecords = @($baseline.files | ForEach-Object {
    $referencePath = Join-Path $referencesDirectory ([string] $_.logicalName)
    Get-CompileInputRecord -LogicalName ([string] $_.logicalName) -Path $referencePath
})
$harmonyReferencePath = Join-Path $harmonyReferencesDirectory ([string] $harmonyAssembly[0].preparedFileName)
$referenceRecords += Get-CompileInputRecord -LogicalName ([string] $harmonyAssembly[0].preparedFileName) -Path $harmonyReferencePath
$outputRecords = @($declaredFiles | ForEach-Object {
    [ordered]@{
        path = Get-RelativePath -BasePath $workRoot -Path $_.FullName
        length = $_.Length
        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
    }
})
$record = [ordered]@{
    schemaVersion = 2
    commit = $commit
    clean = $isClean
    sdk = $sdkVersion.Trim()
    configuration = $Configuration
    baselineSha256 = (Get-FileHash -LiteralPath $baselinePath -Algorithm SHA256).Hash.ToUpperInvariant()
    harmonyBaselineSha256 = (Get-FileHash -LiteralPath $harmonyBaselinePath -Algorithm SHA256).Hash.ToUpperInvariant()
    references = $referenceRecords
    outputs = $outputRecords
}
$json = ($record | ConvertTo-Json -Depth 6) + [Environment]::NewLine
[System.IO.File]::WriteAllText($recordPath, $json, (New-Object System.Text.UTF8Encoding($false)))

Write-Output ("PASS: {0} build, ArchitectureTests PASS, declared outputs={1}." -f $Configuration, $declaredFiles.Count)
Write-Output ("Build record: {0}" -f $recordPath)
