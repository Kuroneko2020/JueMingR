[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $ContentDirectory,
    [Parameter(Mandatory = $true)][string] $OutputDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (Test-Path -LiteralPath $OutputDirectory) { throw 'OutputDirectory must be new; preserve previous evidence.' }
if (-not (Test-Path -LiteralPath (Join-Path $ContentDirectory 'Fonts\Mouse_Text.xnb') -PathType Leaf)) { throw 'Actual matching Terraria Content directory is required.' }
# A neutral executable loads fixed EXE metadata and original XNB resources. It
# never constructs/initializes/runs Main, starts a server or reads player saves.
& (Join-Path $PSScriptRoot 'build.ps1') -Configuration Debug
if ($LASTEXITCODE -ne 0) { throw 'Debug build failed.' }
& dotnet.exe build (Join-Path $repositoryRoot 'tests\NativeWorldTextProbe\NativeWorldTextProbe.csproj') --configuration Debug --nologo -p:Platform=x86
if ($LASTEXITCODE -ne 0) { throw 'Native probe build failed.' }
& (Join-Path $repositoryRoot 'tests\NativeWorldTextProbe\bin\x86\Debug\net472\NativeWorldTextProbe.exe') $repositoryRoot $ContentDirectory $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw 'Native world text check failed. A draw/layout failure is not an environment deferral.' }
$inputs = @(& git -C $repositoryRoot ls-files -- src tests/NativeWorldTextProbe | ForEach-Object {
    $file = Join-Path $repositoryRoot $_
    [ordered]@{ path = $_; sha256 = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash }
})
if ($LASTEXITCODE -ne 0) { throw 'Source identity read failed.' }
$inputs | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'source-inputs.json') -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'artifacts\build\Debug\build-record.json') -Destination (Join-Path $OutputDirectory 'source-build-record.json')
