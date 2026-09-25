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
        'src/JueMingR.TerrariaHost/Recovery/HostRecovery.cs' = 'recovery-host';
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
        'src/JueMingR.TerrariaHost/Recovery/HostRecovery.cs' = @('core','recovery-host');
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
        'src/JueMingR.TerrariaHost/QuickItems/QuickItemUse.cs' = @('core','quick-items-host','coin-deposit-host','recovery-host');
        'src/JueMingR.TerrariaHost/KeepFavorited/FavoriteHooks.cs' = @('core','quick-items-host','coin-deposit-host','recovery-host');
        'src/JueMingR.Features/Text/TextEditBuffer.cs' = @('core','notes-host','map-host','footprints-host','browser-host');
        'src/JueMingR.TerrariaHost/Notes/NotesClipboard.cs' = @('core','notes-host','browser-host','about-host');
        'src/JueMingR.TerrariaHost/Notes/NotesInput.cs' = @('core','notes-host','browser-host');
        'src/JueMingR.TerrariaHost/Notes/NotesPresentation.cs' = @('core','notes-host','about-host');
        'src/JueMingR.Features/WorldObjectText/WorldObjectResolver.cs' = @('core','world-host','browser-host');
        'src/JueMingR.TerrariaHost/World/WorldTileObservation.cs' = @('core','world-host','browser-host','recovery-host');
        'src/JueMingR.Platform/WorldTargets/WorldTargetObservation.cs' = @('core','world-host','browser-host','recovery-host');
        'src/JueMingR.TerrariaHost/ItemBrowser/NativeItemCatalog.cs' = @('core','browser-host');
        'src/JueMingR.TerrariaHost/Hotkeys/HostHotkeys.cs' = @('core','shared-host','death-host','map-host','footprints-host','browser-host','quick-items-host','coin-deposit-host','about-host','recovery-host');
        'src/JueMingR.TerrariaHost/F5/F5Layout.cs' = @('core','shared-host','death-host','map-host','footprints-host','browser-host','quick-items-host','coin-deposit-host','about-host','recovery-host');
        'src/JueMingR.TerrariaHost/Input/HostInputState.cs' = @('core','shared-host','death-host','map-host','footprints-host','browser-host','quick-items-host','coin-deposit-host','about-host','recovery-host');
        'src/JueMingR.Platform/Persistence/DocumentWorker.cs' = @('core','storage-host','death-host','map-host','footprints-host','browser-host','quick-items-host','coin-deposit-host','about-host','recovery-host');
        'src/JueMingR.Infrastructure/Storage/AtomicFileDocument.cs' = @('core','storage-host','death-host','map-host','footprints-host','browser-host','quick-items-host','coin-deposit-host','about-host','recovery-host');
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
    # A local feature edit plus a shared edit must retain the shared consumers.
    $aboutPath = 'src/JueMingR.TerrariaHost/About/AboutPage.cs'; $sharedPath = 'src/JueMingR.TerrariaHost/F5/F5Layout.cs'
    Write-Fixture $aboutPath "mixed`n"; Write-Fixture $sharedPath "mixed`n"
    $changes = Get-WorkloadChanges $fixtureRoot $baseline; $route = Get-WorkloadRoute $changes.paths
    Assert-Route ($changes.paths.Count -eq 2 -and (($route.groups | Sort-Object) -join ',') -ceq (($exactGroups[$sharedPath] | Sort-Object) -join ',')) 'mixed About/F5 diff retains exact shared consumers'
    Write-Fixture $aboutPath "baseline`n"; Write-Fixture $sharedPath "baseline`n"
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
    # Execute the runner's actual feature dispatcher with a recording process
    # boundary. This proves wiring only; real assertions run in the normal build.
    $runnerPath = Join-Path $repositoryRoot 'scripts/test-workload-regressions.ps1'
    $tokens = $null; $parseErrors = $null
    $runnerAst = [Management.Automation.Language.Parser]::ParseFile($runnerPath, [ref]$tokens, [ref]$parseErrors)
    Assert-Route ($parseErrors.Count -eq 0) 'runner parses'
    $dispatch = $runnerAst.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq 'Invoke-AboutWorkloadChecks' }, $false)
    $check = $runnerAst.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq 'Invoke-WorkloadCheck' }, $false)
    Assert-Route ($null -ne $dispatch -and $null -ne $check) 'actual runner functions available'
    & {
        . ([scriptblock]::Create($dispatch.Extent.Text))
        $calls = New-Object 'System.Collections.Generic.List[object]'
        $checksRoot = Join-Path $fixtureRoot 'checks'
        function Invoke-WorkloadCheck {
            param([string] $Name, [string] $Executable, [string[]] $Arguments)
            $calls.Add(@{ name = $Name; executable = $Executable; arguments = $Arguments })
        }
        foreach ($groups in @(@('core'), @('core','about-host'), @('core','about-host','shared-host','storage-host'))) {
            $calls.Clear()
            Invoke-AboutWorkloadChecks $groups 'architecture.exe' 'fixture.exe' 'native.exe'
            if ($groups -notcontains 'about-host') { Assert-Route ($calls.Count -eq 0) 'no feature group means no feature checks'; continue }
            Assert-Route ($calls.Count -eq 3) 'local and shared routes execute feature checks exactly once'
            Assert-Route ($calls[0].name -ceq 'onboarding-markers' -and $calls[0].executable -ceq 'architecture.exe' -and ($calls[0].arguments -join '|') -ceq '--onboarding') 'onboarding entry arguments'
            Assert-Route ($calls[1].name -ceq 'about-native-composition' -and $calls[1].executable -ceq 'native.exe' -and ($calls[1].arguments -join '|') -ceq (@($repositoryRoot,'--cpu',(Join-Path $checksRoot 'about-cpu'),'AboutCpu') -join '|')) 'native About entry arguments'
            Assert-Route ($calls[2].name -ceq 'f5-cpu' -and $calls[2].executable -ceq 'fixture.exe' -and ($calls[2].arguments -join '|') -ceq 'f5-cpu') 'existing F5 input/layout entry arguments'
        }
    }
    # Run the real process wrapper in a child PowerShell: failed checks must
    $recoveryDispatch = $runnerAst.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq 'Invoke-RecoveryWorkloadChecks' }, $false)
    Assert-Route ($null -ne $recoveryDispatch) 'actual recovery dispatcher exists'
    & {
        . ([scriptblock]::Create($recoveryDispatch.Extent.Text))
        $calls = New-Object 'System.Collections.Generic.List[object]'
        $checksRoot = Join-Path $fixtureRoot 'checks'
        function Invoke-WorkloadCheck {
            param([string] $Name, [string] $Executable, [string[]] $Arguments)
            $calls.Add(@{ name = $Name; executable = $Executable; arguments = $Arguments })
        }
        foreach ($groups in @(@('core'), @('core','recovery-host'), @('core','recovery-host','shared-host'))) {
            $calls.Clear(); Invoke-RecoveryWorkloadChecks $groups 'architecture.exe' 'native.exe'
            if ($groups -notcontains 'recovery-host') { Assert-Route ($calls.Count -eq 0) 'no recovery group has no recovery dispatch'; continue }
            Assert-Route ($calls.Count -eq 2) 'recovery actual dispatcher executes both checks exactly once'
            Assert-Route ($calls[0].name -ceq 'recovery-rules-storage' -and $calls[0].executable -ceq 'architecture.exe' -and ($calls[0].arguments -join '|') -ceq '--recovery') 'actual recovery core arguments'
            Assert-Route ($calls[1].name -ceq 'recovery-native-execution' -and $calls[1].executable -ceq 'native.exe' -and ($calls[1].arguments -join '|') -ceq (@($repositoryRoot,'--cpu',(Join-Path $checksRoot 'recovery-cpu'),'RecoveryCpu') -join '|')) 'actual recovery native arguments'
        }
    }
    # Run the real process wrapper in a child PowerShell: failed checks must
    # escape as a nonzero process exit and must never append a PASS result.
    $failurePath = Join-Path $fixtureRoot 'failure-exit.ps1'
    $failureBody = @'
$ErrorActionPreference = 'Stop'
$results = New-Object 'System.Collections.Generic.List[object]'
try { Invoke-WorkloadCheck 'expected-failure' (Get-Command powershell.exe).Source @('-NoProfile','-Command','exit 7') }
finally { if ($results.Count -ne 0) { throw 'Failed check was recorded as PASS.' } }
'@
    [IO.File]::WriteAllText($failurePath, ($check.Extent.Text + "`n" + $failureBody), (New-Object Text.UTF8Encoding($false)))
    # Capture the expected child error without turning it into a parent native error.
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = (Get-Command powershell.exe).Source
    $start.Arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + $failurePath + '"'
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $start.RedirectStandardError = $true
    $process = New-Object Diagnostics.Process; $process.StartInfo = $start
    try {
        [void]$process.Start(); $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit(); $failureOutput = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
        Assert-Route ($process.ExitCode -ne 0 -and $failureOutput.Contains('expected-failure failed with exit 7') -and -not $failureOutput.Contains('recorded as PASS')) 'real check failure exits nonzero without PASS'
    } finally { $process.Dispose() }
    Write-Output 'PASS: actual diff routes, exact consumers, mixed edits, staged cancellation, rename/deletion, cumulative commits, untracked identity, missing baseline, runner dispatch, and failure exit.'
} finally {
    $resolved = [IO.Path]::GetFullPath($fixtureRoot); $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($temp, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($resolved).StartsWith('JueMingR-routing-', [StringComparison]::Ordinal)) { throw 'Unsafe route fixture cleanup.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
