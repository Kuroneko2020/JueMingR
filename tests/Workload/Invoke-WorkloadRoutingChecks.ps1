[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $repositoryRoot 'scripts/workload/Workload.Support.ps1')
# A few text files in an isolated Git fixture exercise real diff shapes; this is
# neither another project checkout nor a replacement runtime/production model.
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('JueMingR-routing-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixtureRoot) | Out-Null
function Write-Fixture {
    param([string] $Relative, [string] $Value)
    $path = Join-Path $fixtureRoot $Relative
    [IO.Directory]::CreateDirectory((Split-Path -Parent $path)) | Out-Null
    [IO.File]::WriteAllText($path, $Value, (New-Object Text.UTF8Encoding($false)))
}
function Assert-Route {
    param([bool] $Pass, [string] $Reason)
    if (-not $Pass) { throw ('Workload route: ' + $Reason) }
}
try {
    Invoke-WorkloadGit $fixtureRoot @('init', '--quiet') | Out-Null
    Invoke-WorkloadGit $fixtureRoot @('config', 'core.autocrlf', 'false') | Out-Null
    $cases = [ordered]@{
        'src/JueMingR.TerrariaHost/About/AboutPage.cs' = 'shared-host';
        'src/JueMingR.Features/Onboarding/OnboardingState.cs' = 'storage-host';
        'src/JueMingR.TerrariaHost/CoinDeposit/CoinTransfer.cs' = 'coin-deposit-host';
        'src/JueMingR.Features/CoinDeposit/CoinIntent.cs' = 'coin-deposit-host';
        'src/JueMingR.Platform/Items/ItemOperationOwnership.cs' = 'coin-deposit-host';
        'src/JueMingR.TerrariaHost/Items/ItemPendingGuards.cs' = 'coin-deposit-host';
        'src/JueMingR.TerrariaHost/Items/ItemSourceHooks.cs' = 'coin-deposit-host';
        'src/JueMingR.TerrariaHost/Input/HostInputState.cs' = 'coin-deposit-host';
        'src/JueMingR.TerrariaHost/QuickItems/QuickItemUse.cs' = 'quick-items-host';
        'src/JueMingR.TerrariaHost/KeepFavorited/FavoriteHooks.cs' = 'quick-items-host';
        'src/JueMingR.TerrariaHost/Notes/NotesCards.cs' = 'notes-host';
        'src/JueMingR.TerrariaHost/EntityLabels/StyleEditor.cs' = 'style-host';
        'src/JueMingR.Features/WorldObjectText/OpenedPositionStore.cs' = 'records';
        'src/JueMingR.Platform/Persistence/DocumentWorker.cs' = 'storage-host';
        'src/JueMingR.TerrariaHost/F5/F5Layout.cs' = 'shared-host';
        'src/JueMingR.Features/Biomes/BiomeDisplayFeature.cs' = 'shared-host';
        'src/JueMingR.TerrariaHost/Hotkeys/HostHotkeys.cs' = 'shared-host';
        'src/JueMingR.TerrariaHost/Phase0TBiomeRuntime.cs' = 'shared-host';
        'src/JueMingR.TerrariaHost/Information/InformationHud.cs' = 'shared-host';
        'src/JueMingR.Features/Information/InformationPreferenceCodec.cs' = 'storage-host';
        'src/JueMingR.Platform/Information/InformationObservation.cs' = 'shared-host';
        'src/JueMingR.Features/DeathHistory/DeathArchive.cs' = 'death-host';
        'src/JueMingR.TerrariaHost/WorldTime/WorldTimeSourceHooks.cs' = 'death-host';
        'src/JueMingR.Features/MapMarkers/MarkerLibrary.cs' = 'map-host';
        'src/JueMingR.Features/Exploration/ExplorationCounter.cs' = 'map-host';
        'src/JueMingR.Features/Footprints/FootprintRecorder.cs' = 'footprints-host';
        'src/JueMingR.TerrariaHost/ItemBrowser/NativeItemCatalog.cs' = 'browser-host';
        'src/JueMingR.Platform/ItemCatalog/CatalogItem.cs' = 'browser-host';
        'src/JueMingR.Features/ChestLocator/ChestKnowledge.cs' = 'browser-host';
        'src/JueMingR.Features/Announcements/SafeChatText.cs' = 'browser-host';
        'src/JueMingR.Features/Text/TextEditBuffer.cs' = 'notes-host';
        'src/JueMingR.TerrariaHost/Notes/NotesClipboard.cs' = 'notes-host';
        'src/JueMingR.TerrariaHost/Notes/NotesInput.cs' = 'notes-host';
        'src/JueMingR.Features/WorldObjectText/WorldObjectResolver.cs' = 'world-host';
        'src/JueMingR.TerrariaHost/World/WorldTileObservation.cs' = 'world-host';
        'src/JueMingR.TerrariaHost/Map/FullscreenMapDrawing.cs' = 'map-host';
        'docs/guide.md' = 'core'
    }
    $exactGroups = @{
        'src/JueMingR.TerrariaHost/CoinDeposit/CoinTransfer.cs' = @('core','coin-deposit-host');
        'src/JueMingR.Features/CoinDeposit/CoinIntent.cs' = @('core','coin-deposit-host');
        'src/JueMingR.TerrariaHost/QuickItems/QuickItemUse.cs' = @('core','quick-items-host','coin-deposit-host');
        'src/JueMingR.TerrariaHost/KeepFavorited/FavoriteHooks.cs' = @('core','quick-items-host','coin-deposit-host');
        'src/JueMingR.Features/Text/TextEditBuffer.cs' = @('core','notes-host','map-host','footprints-host','browser-host');
        'src/JueMingR.TerrariaHost/Notes/NotesClipboard.cs' = @('core','notes-host','browser-host');
        'src/JueMingR.TerrariaHost/Notes/NotesInput.cs' = @('core','notes-host','browser-host');
        'src/JueMingR.Features/WorldObjectText/WorldObjectResolver.cs' = @('core','world-host','browser-host');
        'src/JueMingR.TerrariaHost/World/WorldTileObservation.cs' = @('core','world-host','browser-host');
        'src/JueMingR.TerrariaHost/ItemBrowser/NativeItemCatalog.cs' = @('core','browser-host');
        'src/JueMingR.TerrariaHost/Hotkeys/HostHotkeys.cs' = @('core','shared-host','death-host','map-host','footprints-host','browser-host','quick-items-host','coin-deposit-host');
        'src/JueMingR.TerrariaHost/F5/F5Layout.cs' = @('core','shared-host','death-host','map-host','footprints-host','browser-host','quick-items-host','coin-deposit-host');
        'src/JueMingR.TerrariaHost/Notes/NotesCards.cs' = @('core','notes-host');
        'src/JueMingR.Features/WorldObjectText/OpenedPositionStore.cs' = @('core','records');
        'src/JueMingR.TerrariaHost/EntityLabels/StyleEditor.cs' = @('core','style-host');
        'src/JueMingR.Features/DeathHistory/DeathArchive.cs' = @('core','death-host');
        'docs/guide.md' = @('core')
    }
    foreach ($path in $cases.Keys) { Write-Fixture $path "baseline`n" }
    Invoke-WorkloadGit $fixtureRoot @('add', '.') | Out-Null
    Invoke-WorkloadGit $fixtureRoot @('-c', 'user.name=Workload fixture', '-c', 'user.email=fixture@example.invalid', '-c', 'commit.gpgSign=false', 'commit', '--quiet', '-m', 'fixture baseline') | Out-Null
    $baseline = [string](Invoke-WorkloadGit $fixtureRoot @('rev-parse', 'HEAD'))
    foreach ($path in $cases.Keys) {
        Write-Fixture $path "changed`n"
        $changes = Get-WorkloadChanges $fixtureRoot $baseline; $route = Get-WorkloadRoute $changes.paths
        Assert-Route ($changes.paths.Count -eq 1 -and $changes.paths[0] -ceq $path -and $route.groups -contains $cases[$path]) ('actual unstaged route ' + $path)
        Assert-Route (-not $route.slowGraphics -and $route.unknown.Count -eq 0) 'known local/document edits never automatically launch slow graphics'
        if ($exactGroups.ContainsKey($path)) {
            Assert-Route ((@($route.groups | Sort-Object) -join ',') -ceq (@($exactGroups[$path] | Sort-Object) -join ',')) ('exact consumers, without all-group fallback: ' + $path)
        }
        if ($path -ceq 'docs/guide.md') { Assert-Route ($route.groups.Count -eq 1) 'behaviorless document selects core only' }
        Write-Output ('PASS route: ' + $path + ' -> ' + ($route.groups -join ', '))
        Write-Fixture $path "baseline`n"
    }
    $notesPath = [string]@($cases.Keys)[0]
    Write-Fixture $notesPath "staged`n"; Invoke-WorkloadGit $fixtureRoot @('add', $notesPath) | Out-Null
    Write-Fixture $notesPath "baseline`n"
    Assert-Route ((Get-WorkloadChanges $fixtureRoot $baseline).paths -contains $notesPath) 'index and worktree cancellation must remain in routing input'
    Invoke-WorkloadGit $fixtureRoot @('add', $notesPath) | Out-Null
    $old = 'src/JueMingR.TerrariaHost/EntityLabels/StyleEditor.cs'; $new = 'src/JueMingR.TerrariaHost/EntityLabels/StyleDraft.cs'
    Invoke-WorkloadGit $fixtureRoot @('mv', $old, $new) | Out-Null
    $changes = Get-WorkloadChanges $fixtureRoot $baseline
    Assert-Route ($changes.paths -contains $old -and $changes.paths -contains $new) 'rename must cover deleted and added paths'
    Invoke-WorkloadGit $fixtureRoot @('-c', 'user.name=Workload fixture', '-c', 'user.email=fixture@example.invalid', '-c', 'commit.gpgSign=false', 'commit', '--quiet', '-m', 'rename') | Out-Null
    Write-Fixture 'docs/guide.md' "second commit`n"; Invoke-WorkloadGit $fixtureRoot @('add', '.') | Out-Null
    Invoke-WorkloadGit $fixtureRoot @('-c', 'user.name=Workload fixture', '-c', 'user.email=fixture@example.invalid', '-c', 'commit.gpgSign=false', 'commit', '--quiet', '-m', 'docs') | Out-Null
    Assert-Route ((Get-WorkloadChanges $fixtureRoot $baseline).paths -contains $old) 'full baseline delta includes earlier commits'
    $identity = Get-WorkloadIdentity $fixtureRoot; Write-Fixture 'unclassified/new.cs' "new`n"
    $changes = Get-WorkloadChanges $fixtureRoot $baseline; $route = Get-WorkloadRoute $changes.paths
    Assert-Route ($route.groups -contains 'core' -and $route.unknown -contains 'unclassified/new.cs') 'unknown untracked input keeps core and blocks unclassified delivery'
    Assert-Route ($identity.fingerprint -cne (Get-WorkloadIdentity $fixtureRoot).fingerprint) 'untracked bytes belong to build identity'
    Assert-Route ($null -ne (Get-WorkloadChanges $fixtureRoot 'missing-baseline').reason) 'missing baseline is an explicit unresolved risk'
    Write-Output 'PASS: actual single-file diff routes, staged cancellation, rename/deletion, cumulative commits, untracked identity, and missing baseline.'
} finally {
    $resolved = [IO.Path]::GetFullPath($fixtureRoot); $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($temp, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($resolved).StartsWith('JueMingR-routing-', [StringComparison]::Ordinal)) { throw 'Unsafe route fixture cleanup.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
