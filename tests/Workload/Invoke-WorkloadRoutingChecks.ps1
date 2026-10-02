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
        'src/JueMingR.TerrariaHost/Feedback/NativePopupText.cs' = 'shared-host';
        'src/JueMingR.TerrariaHost/Feedback/LocalShortFeedback.cs' = 'shared-host';
        'tests/NativeWorldTextProbe/NativeShortFeedbackChecks.cs' = 'shared-host';
        'src/JueMingR.TerrariaHost/Recovery/HostRecovery.cs' = 'recovery-host';
        'src/JueMingR.TerrariaHost/Tools/AutoCapture.cs' = 'tools-host';
        'tests/NativeWorldTextProbe/NativeFishingBorrowChecks.cs' = 'tools-host';
        'tests/NativeWorldTextProbe/NativeToolCadenceChecks.cs' = 'tools-host';
        'tests/NativeWorldTextProbe/NativeToolExecutionChecks.cs' = 'tools-host';
        'tests/NativeWorldTextProbe/NativeToolWaitChecks.cs' = 'tools-host';
        'tests/NativeWorldTextProbe/NativeSeedDiscoveryChecks.cs' = 'tools-host';
        'tests/NativeWorldTextProbe/NativePageCompositionChecks.cs' = 'tools-host';
        'tests/NativeWorldTextProbe/NativeRecoveryChecks.cs' = 'recovery-host';
        'src/JueMingR.TerrariaHost/About/AboutPage.cs' = 'about-host';
        'src/JueMingR.TerrariaHost/Onboarding/HostOnboarding.cs' = 'about-host';
        'src/JueMingR.Features/Onboarding/OnboardingState.cs' = 'about-host';
        'src/JueMingR.TerrariaHost/About/Assets/wechat.png' = 'about-host';
        'tests/Phase0U/AboutChecks.cs' = 'about-host';
        'tests/JueMingR.ArchitectureTests/OnboardingFailureChecks.cs' = 'about-host';
        'tests/NativeWorldTextProbe/NativeAboutVisualChecks.cs' = 'about-host';
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
        'src/JueMingR.Infrastructure/Storage/AtomicFileDocument.cs' = 'storage-host';
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
        'src/JueMingR.TerrariaHost/Notes/NotesPresentation.cs' = 'notes-host';
        'src/JueMingR.Features/WorldObjectText/WorldObjectResolver.cs' = 'world-host';
        'src/JueMingR.TerrariaHost/World/WorldTileObservation.cs' = 'world-host';
        'src/JueMingR.Platform/WorldTargets/WorldTargetObservation.cs' = 'world-host';
        'src/JueMingR.TerrariaHost/Map/FullscreenMapDrawing.cs' = 'map-host';
        'docs/guide.md' = 'core'
    }
    $exactGroups = @{
        'src/JueMingR.TerrariaHost/Tools/AutoCapture.cs' = @('core','tools-host','quick-items-host','coin-deposit-host','recovery-host','processing-host');
        'tests/NativeWorldTextProbe/NativeFishingBorrowChecks.cs' = @('core','tools-host','quick-items-host','coin-deposit-host','recovery-host','processing-host');
        'tests/NativeWorldTextProbe/NativeToolCadenceChecks.cs' = @('core','tools-host','quick-items-host','coin-deposit-host','recovery-host','processing-host');
        'tests/NativeWorldTextProbe/NativeToolExecutionChecks.cs' = @('core','tools-host','quick-items-host','coin-deposit-host','recovery-host','processing-host');
        'tests/NativeWorldTextProbe/NativeToolWaitChecks.cs' = @('core','tools-host','quick-items-host','coin-deposit-host','recovery-host','processing-host');
        'tests/NativeWorldTextProbe/NativeSeedDiscoveryChecks.cs' = @('core','tools-host','quick-items-host','coin-deposit-host','recovery-host','processing-host');
        'src/JueMingR.TerrariaHost/Processing/HostProcessing.cs' = @('core','tools-host','quick-items-host','coin-deposit-host','recovery-host','processing-host');
        'src/JueMingR.Features/Processing/ProcessingSettings.cs' = @('core','tools-host','quick-items-host','coin-deposit-host','recovery-host','processing-host');
        'tests/JueMingR.ArchitectureTests/Processing/ProcessingChecks.cs' = @('core','processing-host');
        'tests/NativeWorldTextProbe/NativeProcessingUiChecks.cs' = @('core','processing-host');
        'tests/NativeWorldTextProbe/NativeExtractionChecks.cs' = @('core','processing-host');
        'tests/NativeWorldTextProbe/NativeReforgeChecks.cs' = @('core','processing-host');
        'src/JueMingR.TerrariaHost/Recovery/RecoveryHooks.cs' = @('core','tools-host','quick-items-host','coin-deposit-host','recovery-host','processing-host');
        'src/JueMingR.TerrariaHost/Recovery/RecoveryBankGuards.cs' = @('core','tools-host','quick-items-host','coin-deposit-host','recovery-host','processing-host');
        'src/JueMingR.TerrariaHost/Recovery/RecoverySource.cs' = @('core','tools-host','quick-items-host','coin-deposit-host','recovery-host','processing-host');
        'src/JueMingR.TerrariaHost/Input/TextEditInput.cs' = @('core','shared-host','death-host','map-host','footprints-host','browser-host','quick-items-host','coin-deposit-host','about-host','recovery-host','processing-host','tools-host');
        'src/JueMingR.TerrariaHost/Input/SingleLineEditView.cs' = @('core','shared-host','death-host','map-host','footprints-host','browser-host','quick-items-host','coin-deposit-host','about-host','recovery-host','processing-host','tools-host');
        'src/JueMingR.Platform/Items/ItemOperationOwnership.cs' = @('core','shared-host','death-host','map-host','footprints-host','browser-host','quick-items-host','coin-deposit-host','about-host','recovery-host','processing-host','tools-host');

        'src/JueMingR.TerrariaHost/Recovery/HostRecovery.cs' = @('core','tools-host','quick-items-host','coin-deposit-host','recovery-host','processing-host');
        'tests/NativeWorldTextProbe/NativeRecoveryChecks.cs' = @('core','recovery-host');
        'src/JueMingR.TerrariaHost/About/AboutPage.cs' = @('core','about-host');
        'src/JueMingR.TerrariaHost/Onboarding/HostOnboarding.cs' = @('core','about-host');
        'src/JueMingR.Features/Onboarding/OnboardingState.cs' = @('core','about-host');
        'src/JueMingR.TerrariaHost/About/Assets/wechat.png' = @('core','about-host');
        'tests/Phase0U/AboutChecks.cs' = @('core','about-host');
        'tests/JueMingR.ArchitectureTests/OnboardingFailureChecks.cs' = @('core','about-host');
        'tests/NativeWorldTextProbe/NativeAboutVisualChecks.cs' = @('core','about-host');
        'src/JueMingR.TerrariaHost/CoinDeposit/CoinTransfer.cs' = @('core','coin-deposit-host','recovery-host');
        'src/JueMingR.Features/CoinDeposit/CoinIntent.cs' = @('core','coin-deposit-host','recovery-host');
        'src/JueMingR.TerrariaHost/QuickItems/QuickItemUse.cs' = @('core','tools-host','quick-items-host','coin-deposit-host','recovery-host','processing-host');
        'src/JueMingR.TerrariaHost/KeepFavorited/FavoriteHooks.cs' = @('core','tools-host','quick-items-host','coin-deposit-host','recovery-host','processing-host');
        'src/JueMingR.Features/Text/TextEditBuffer.cs' = @('core','notes-host','map-host','footprints-host','browser-host','processing-host');
        'src/JueMingR.TerrariaHost/Notes/NotesClipboard.cs' = @('core','notes-host','browser-host','about-host','processing-host');
        'src/JueMingR.TerrariaHost/Notes/NotesInput.cs' = @('core','notes-host','browser-host','processing-host');
        'src/JueMingR.TerrariaHost/Notes/NotesPresentation.cs' = @('core','notes-host','about-host');
        'src/JueMingR.Features/WorldObjectText/WorldObjectResolver.cs' = @('core','world-host','browser-host');
        'src/JueMingR.TerrariaHost/World/WorldTileObservation.cs' = @('core','world-host','browser-host','tools-host','quick-items-host','coin-deposit-host','recovery-host','processing-host');
        'src/JueMingR.Platform/WorldTargets/WorldTargetObservation.cs' = @('core','world-host','browser-host','tools-host','quick-items-host','coin-deposit-host','recovery-host','processing-host');
        'src/JueMingR.TerrariaHost/ItemBrowser/NativeItemCatalog.cs' = @('core','browser-host');
        'src/JueMingR.TerrariaHost/Hotkeys/HostHotkeys.cs' = @('core','shared-host','death-host','map-host','footprints-host','browser-host','quick-items-host','coin-deposit-host','about-host','recovery-host','processing-host','tools-host');
        'src/JueMingR.TerrariaHost/F5/F5Layout.cs' = @('core','shared-host','death-host','map-host','footprints-host','browser-host','quick-items-host','coin-deposit-host','about-host','recovery-host','processing-host','tools-host');
        'src/JueMingR.TerrariaHost/Input/HostInputState.cs' = @('core','shared-host','death-host','map-host','footprints-host','browser-host','quick-items-host','coin-deposit-host','about-host','recovery-host','processing-host','tools-host');
        'src/JueMingR.Platform/Persistence/DocumentWorker.cs' = @('core','storage-host','death-host','map-host','footprints-host','browser-host','quick-items-host','coin-deposit-host','about-host','recovery-host','processing-host','tools-host');
        'src/JueMingR.Infrastructure/Storage/AtomicFileDocument.cs' = @('core','storage-host','death-host','map-host','footprints-host','browser-host','quick-items-host','coin-deposit-host','about-host','recovery-host','processing-host','tools-host');
        'src/JueMingR.TerrariaHost/Notes/NotesCards.cs' = @('core','notes-host');
        'src/JueMingR.Features/WorldObjectText/OpenedPositionStore.cs' = @('core','records');
        'src/JueMingR.TerrariaHost/EntityLabels/StyleEditor.cs' = @('core','style-host');
        'src/JueMingR.Features/DeathHistory/DeathArchive.cs' = @('core','death-host');
        'docs/guide.md' = @('core')
    }
    # Exercise production-only regressions too: a test-file edit must not be
    # required to select these assertions. Shared facts retain all consumers.
    foreach ($name in @('NetGeometry','AutoMining','MiningOverlay','MiningEligibility','HerbHarvest','HerbField','FishingBorrow','ToolHooks')) {
        $path = 'src/JueMingR.TerrariaHost/Tools/' + $name + '.cs'
        $cases[$path] = 'tools-host'; $exactGroups[$path] = $exactGroups['src/JueMingR.TerrariaHost/Tools/AutoCapture.cs']
    }
    foreach ($name in @('NativeCaptureWorkloadChecks','NativeToolsCacheChecks','NativeToolsWorkloadChecks','NativeCaptureChecks')) {
        $path = 'tests/NativeWorldTextProbe/' + $name + '.cs'
        $cases[$path] = 'tools-host'; $exactGroups[$path] = $exactGroups['src/JueMingR.TerrariaHost/Tools/AutoCapture.cs']
    }
    foreach ($path in @('src/JueMingR.TerrariaHost/Fishing/FishingEquipment.cs','src/JueMingR.Features/Fishing/FishFilter.cs','tests/JueMingR.ArchitectureTests/Fishing/FishingChecks.cs','tests/NativeWorldTextProbe/NativeFishingChecks.cs','tests/NativeWorldTextProbe/NativeFishingUiChecks.cs','tests/NativeWorldTextProbe/NativeFishingNetworkChecks.cs','tests/NativeWorldTextProbe/NativePlayerRenameChecks.cs','tests/NativeWorldTextProbe/NativeBackgroundAutomationChecks.cs','tests/NativeWorldTextProbe/NativeF5AutomationChecks.cs')) {
        $cases[$path] = 'fishing-host'; $exactGroups[$path] = @('core','fishing-host')
    }
    foreach ($path in @($exactGroups.Keys)) {
        if ($exactGroups[$path] -contains 'tools-host' -or $path -in @('src/JueMingR.Features/Text/TextEditBuffer.cs','src/JueMingR.TerrariaHost/Notes/NotesClipboard.cs')) {
            $exactGroups[$path] = @($exactGroups[$path] + 'fishing-host' | Sort-Object -Unique)
        }
    }
    foreach ($path in @($exactGroups.Keys)) {
        $expected = @($exactGroups[$path])
        if ($expected -contains 'shared-host' -or $expected -contains 'storage-host' -or $expected -contains 'tools-host') { $expected += 'combat-host' }
        if ($expected -contains 'shared-host' -or $expected -contains 'storage-host') {
            $expected += @('notes-host','records','style-host','world-host','hotkeys','preferences','information','guidance','entity','items','biomes','pages-host')
        }
        if ($expected -contains 'fishing-host') { $expected += 'information' }
        if ($expected -contains 'quick-items-host' -or $expected -contains 'notes-host') { $expected += 'hotkeys' }
        if ($expected -contains 'processing-host') { $expected += 'items' }
        if ($expected -contains 'world-host') { $expected += @('records','entity') }
        if ($expected -contains 'tools-host') { $expected += 'pages-host' }
        $exactGroups[$path] = @($expected | Sort-Object -Unique)
    }
    $cases['tests/NativeWorldTextProbe/NativePageCompositionChecks.cs'] = 'pages-host'
    $exactGroups['tests/NativeWorldTextProbe/NativePageCompositionChecks.cs'] = @('core','hotkeys','pages-host')
    $cases['src/JueMingR.TerrariaHost/Npcs/NativeNpcObservation.cs'] = 'tools-host'
    $exactGroups['src/JueMingR.TerrariaHost/Npcs/NativeNpcObservation.cs'] = @($exactGroups['src/JueMingR.TerrariaHost/F5/F5Layout.cs'] + 'storage-host' | Sort-Object -Unique)
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
    # A local feature edit plus a shared edit must retain the shared consumers.
    $aboutPath = 'src/JueMingR.TerrariaHost/About/AboutPage.cs'; $sharedPath = 'src/JueMingR.TerrariaHost/F5/F5Layout.cs'
    Write-Fixture $aboutPath "mixed`n"; Write-Fixture $sharedPath "mixed`n"
    $changes = Get-WorkloadChanges $fixtureRoot $baseline; $route = Get-WorkloadRoute $changes.paths
    Assert-Route ($changes.paths.Count -eq 2 -and (($route.groups | Sort-Object) -join ',') -ceq (($exactGroups[$sharedPath] | Sort-Object) -join ',')) 'mixed About/F5 diff retains exact shared consumers'
    Write-Fixture $aboutPath "baseline`n"; Write-Fixture $sharedPath "baseline`n"
    $fishingPath = 'src/JueMingR.TerrariaHost/Fishing/FishingEquipment.cs'
    Write-Fixture $fishingPath "mixed`n"; Write-Fixture $sharedPath "mixed`n"
    $changes = Get-WorkloadChanges $fixtureRoot $baseline; $route = Get-WorkloadRoute $changes.paths
    Assert-Route ($changes.paths.Count -eq 2 -and ($route.groups -join ',') -ceq (($exactGroups[$sharedPath] | Sort-Object) -join ',')) 'mixed Fishing/F5 retains all shared consumers'
    Write-Fixture $fishingPath "baseline`n"; Write-Fixture $sharedPath "baseline`n"
    Invoke-WorkloadGit $fixtureRoot @('rm','--quiet',$fishingPath) | Out-Null
    $route = Get-WorkloadRoute (Get-WorkloadChanges $fixtureRoot $baseline).paths
    Assert-Route (($route.groups -join ',') -ceq 'core,fishing-host,information') 'Fishing deletion keeps its own checks'
    Write-Fixture $fishingPath "baseline`n"; Invoke-WorkloadGit $fixtureRoot @('add',$fishingPath) | Out-Null
    $fishingRename = 'src/JueMingR.TerrariaHost/Fishing/FormerInput.cs'
    Invoke-WorkloadGit $fixtureRoot @('mv',$sharedPath,$fishingRename) | Out-Null
    $changes = Get-WorkloadChanges $fixtureRoot $baseline; $route = Get-WorkloadRoute $changes.paths
    Assert-Route ($changes.paths.Count -eq 2 -and ($route.groups -join ',') -ceq (($exactGroups[$sharedPath] | Sort-Object) -join ',')) 'rename from shared to Fishing retains deleted provider consumers'
    Invoke-WorkloadGit $fixtureRoot @('mv',$fishingRename,$sharedPath) | Out-Null
    Invoke-WorkloadGit $fixtureRoot @('rm', '--quiet', $aboutPath) | Out-Null
    $changes = Get-WorkloadChanges $fixtureRoot $baseline; $route = Get-WorkloadRoute $changes.paths
    Assert-Route ($changes.paths.Count -eq 1 -and ($route.groups -join ',') -ceq 'about-host,core') 'About deletion selects only core and feature checks'
    Write-Fixture $aboutPath "baseline`n"; Invoke-WorkloadGit $fixtureRoot @('add', $aboutPath) | Out-Null
    $renamedPath = 'src/JueMingR.TerrariaHost/About/FormerF5Layout.cs'
    Invoke-WorkloadGit $fixtureRoot @('mv', $sharedPath, $renamedPath) | Out-Null
    $changes = Get-WorkloadChanges $fixtureRoot $baseline; $route = Get-WorkloadRoute $changes.paths
    Assert-Route ($changes.paths.Count -eq 2 -and $changes.paths -contains $sharedPath -and $changes.paths -contains $renamedPath -and (($route.groups | Sort-Object) -join ',') -ceq (($exactGroups[$sharedPath] | Sort-Object) -join ',')) 'rename into About retains consumers of deleted shared path'
    Invoke-WorkloadGit $fixtureRoot @('mv', $renamedPath, $sharedPath) | Out-Null
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
    # Exercise the actual executable plan, not script text or a duplicate dispatcher.
    $catalog = @('FishingChecks|fishing-host','ProcessingChecks|processing-host','HotkeyCoreChecks|hotkeys','OnboardingChecks|about-host','CombatChecks|combat-host')
    foreach($path in @('src/JueMingR.Features/Combat/CombatSettings.cs','src/JueMingR.TerrariaHost/Combat/CombatUse.cs','src/JueMingR.TerrariaHost/F5/CombatIntervalDrag.cs','tests/NativeWorldTextProbe/CombatNetworkFixture.cs')) {
        $local = Get-WorkloadRoute @($path)
        Assert-Route ((@($local.groups) -join ',') -ceq 'combat-host,core') ('combat leaf classification: '+$path)
    }
    $combatPlan=@(Get-WorkloadPlan $repositoryRoot (Join-Path $fixtureRoot 'checks') 'architecture.exe' $catalog @('core','combat-host'))
    $capacityPlan=@($combatPlan | Where-Object {$_.name -ceq 'native-NpcSessionCapacity'})
    Assert-Route ($capacityPlan.Count -eq 1 -and $capacityPlan[0].arguments[-1] -ceq 'NpcSessionCapacity') 'independent capacity consumer is present once with its actual probe scope'
    $scopeValidation=@((Get-Command (Join-Path $repositoryRoot 'scripts/test-world-object-text.ps1')).Parameters['Scope'].Attributes | Where-Object {$_ -is [System.Management.Automation.ValidateSetAttribute]})
    foreach($scope in @('NpcSessionCapacity','NpcFailureRecovery')) {Assert-Route ($scopeValidation.Count -eq 1 -and $scopeValidation[0].ValidValues -contains $scope) ('native entry accepts '+$scope)}
    foreach($expected in @('CombatChecks','native-CombatCpu','native-CombatFacingCpu','native-CombatHitsCpu','native-CombatReportCpu','native-CombatUiCpu','native-ShortFeedbackCpu')) {Assert-Route (@($combatPlan.name) -contains $expected) ('combat consumer '+$expected)}
    $plan = @(Get-WorkloadPlan $repositoryRoot (Join-Path $fixtureRoot 'checks') 'architecture.exe' $catalog @('core','pages-host','hotkeys'))
    $names = @($plan | ForEach-Object {$_.name})
    foreach ($expected in @('native-PageCompositionCpu','fixture-focus-input','fixture-hotkeys-popup','HotkeyCoreChecks')) { Assert-Route ($names -contains $expected) ('page actual consumer ' + $expected) }
    foreach ($excluded in @('native-FishingCpu','native-ToolsCpu','native-ProcessingCpu','FishingChecks')) { Assert-Route ($names -notcontains $excluded) ('page excludes unrelated execution ' + $excluded) }
    $all = @(Get-WorkloadPlan $repositoryRoot (Join-Path $fixtureRoot 'checks') 'architecture.exe' $catalog (Get-WorkloadRoute @('scripts/build.ps1')).groups)
    $packageRoute = Get-WorkloadRoute @('scripts/verify-existing-package.ps1')
    $packagePlan = @(Get-WorkloadPlan $repositoryRoot (Join-Path $fixtureRoot 'checks') 'architecture.exe' $catalog $packageRoute.groups)
    Assert-Route ($packagePlan.Count -eq 1 -and $packagePlan[0].name -ceq 'workload-PackageVerification' -and $packagePlan[0].project -ceq '' -and $packagePlan[0].arguments[-1] -ceq (Join-Path $repositoryRoot 'tests/Phase0S/Invoke-PackageVerificationChecks.ps1')) 'standalone package tool has exactly its actual PowerShell check'
    Assert-Route ($all.name -contains 'workload-PackageVerification') 'Full retains package verifier coverage'
    $mixed = Get-WorkloadRoute @('scripts/verify-existing-package.ps1','src/JueMingR.TerrariaHost/Input/HostInputState.cs')
    Assert-Route ($mixed.groups -contains 'package-tools' -and $mixed.groups -contains 'combat-host' -and $mixed.groups -contains 'shared-host' -and $mixed.groups -contains 'fishing-host') 'package plus shared provider retains all propagation'
    Assert-Route ((Get-WorkloadRoute @('scripts/phase0s/UnknownHelper.ps1')).groups -contains 'shared-host') 'new helper cannot inherit an unproven leaf exemption'
    Assert-Route (@($all.name | Sort-Object -Unique).Count -eq $all.Count) 'shared/domain overlaps dispatch each check only once'
    foreach ($expected in @('native-FishingCpu','native-BackgroundCpu','native-F5AutomationCpu','native-ToolsCpu','native-ToolsCadence','native-ToolsWorkload','native-ToolsExecutionCpu','native-RecoveryCpu','native-ProcessingCpu','native-AboutCpu','native-CoinDepositCpu')) {
        Assert-Route ($all.name -contains $expected) ('full entry retains ' + $expected)
    }
    Write-Output 'PASS: actual Git diff routes, local exclusions, shared consumers, mixed/index/worktree/rename/deletion/cumulative/untracked/missing baseline and executable plan.'
} finally {
    $resolved = [IO.Path]::GetFullPath($fixtureRoot); $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($temp, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($resolved).StartsWith('JueMingR-routing-', [StringComparison]::Ordinal)) { throw 'Unsafe route fixture cleanup.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
