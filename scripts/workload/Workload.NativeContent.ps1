# These ordinary mixed scopes really construct ProbeGraphics. Keep resource
# admission in this separately bound owner, not in a pure plan projection.
function Get-WorkloadNativeContentScopes {
    return @('NpcInvasionFlight','NpcInvasionFlightLoss','NpcBetsy','NpcDarkMage','NpcBloodNautilus','NpcTreeBoss')
}
function Get-WorkloadNativeContentFiles {
    param([string]$Scope)
    if($Scope -cnotin @(Get-WorkloadNativeContentScopes)){return @()}
    $files=@('Fonts/Mouse_Text.xnb','Fonts/Item_Stack.xnb','Fonts/Combat_Text.xnb','Fonts/Combat_Crit.xnb','Images/Item_8.xnb','Images/Inventory_Back.xnb','Images/MagicPixel.xnb')
    if($Scope -cin @('NpcInvasionFlight','NpcInvasionFlightLoss','NpcBetsy','NpcDarkMage')){$files+=@('ScreenShader.xnb','Images/Misc/noise.xnb')}
    return $files
}
function Read-WorkloadNativeContent {
    param([string]$Root,[string]$RequiredScope='')
    $state=[ordered]@{directory='';gameHash='unavailable';files=@{};reason=''}
    try {
        $baselinePath=Join-Path $Root 'eng/TerrariaReferences.baseline.json'
        $receiptPath=Join-Path $Root 'external/TerrariaRefs/.juemingr-reference-set.json'
        $baseline=Read-WorkloadJson $baselinePath;$receipt=Read-WorkloadJson $receiptPath
        if($null -eq $baseline -or $null -eq $receipt -or !$receipt.sourceHashesUnchanged -or
            $receipt.profileId -cne $baseline.profileId){throw 'Matching original reference preparation receipt required for native Content.'}
        $game=@($baseline.files | Where-Object {$_.logicalName -ceq 'Terraria.exe'})
        $preparedGame=@($receipt.files | Where-Object {$_.logicalName -ceq 'Terraria.exe'})
        # Preparation may predate a baseline report/checkout-byte edit. The
        # actual source EXE, fixed reference and prepared EXE identity must all
        # match the current baseline; an old whole-JSON hash is not that proof.
        if($game.Count -ne 1 -or $preparedGame.Count -ne 1 -or $preparedGame[0].sha256 -cne $game[0].sha256 -or [string]::IsNullOrWhiteSpace($receipt.source.terrariaInstallDirectory)){throw 'Original Terraria source identity unavailable.'}
        $source=[IO.Path]::GetFullPath($receipt.source.terrariaInstallDirectory)
        $sourceGame=Join-Path $source 'Terraria.exe'
        if((Get-WorkloadFileHash $sourceGame).Hash -cne $game[0].sha256 -or
            (Get-WorkloadFileHash (Join-Path $Root 'external/TerrariaRefs/Terraria.exe')).Hash -cne $game[0].sha256){throw 'Native Content source game does not match the fixed original Terraria identity.'}
        $state.directory=[IO.Path]::GetFullPath((Join-Path $source 'Content'))
        $state.gameHash=$game[0].sha256
        foreach($relative in @(Get-WorkloadNativeContentScopes | ForEach-Object {Get-WorkloadNativeContentFiles $_} | Sort-Object -Unique)){
            $path=Join-Path $state.directory $relative
            $state.files[$relative]=if([IO.File]::Exists($path)){(Get-WorkloadFileHash $path).Hash}else{'missing'}
        }
    } catch {$state.reason=$_.Exception.Message}
    if($RequiredScope){
        if($RequiredScope -cnotin @(Get-WorkloadNativeContentScopes)){throw 'Unregistered native Content consumer.'}
        if($state.reason){throw $state.reason}
        foreach($relative in @(Get-WorkloadNativeContentFiles $RequiredScope)){
            if(!$state.files.ContainsKey($relative) -or $state.files[$relative] -ceq 'missing'){throw ('Required original native Content asset missing: '+$relative)}
        }
    }
    return $state
}
function Get-WorkloadNativeContentRows {
    param([string]$Root)
    # Absence does not block an unrelated CPU-only project. A selected actual
    # consumer must pass RequiredScope admission before its plan can launch.
    $state=Read-WorkloadNativeContent $Root
    $rows=@(('native-content/root:'+(Get-WorkloadHash @($state.directory))), ('native-content/game:'+$state.gameHash))
    if($state.reason){$rows+='native-content/unavailable:'+(Get-WorkloadHash @($state.reason))}
    foreach($relative in @($state.files.Keys | Sort-Object)){$rows+='native-content/file/'+$relative+':'+$state.files[$relative]}
    return $rows
}
function Test-WorkloadNativeContentRow {
    param([string]$Path,[string]$Name)
    if(!$Name.StartsWith('native-',[StringComparison]::Ordinal)){return $false}
    $scope=$Name.Substring(7)
    if($scope -cnotin @(Get-WorkloadNativeContentScopes)){return $false}
    if($Path.StartsWith('native-content/file/',[StringComparison]::Ordinal)){
        return $Path.Substring(20) -cin @(Get-WorkloadNativeContentFiles $scope)
    }
    # Root, source game, unavailable and unknown future resource metadata stay
    # bound to every actual Content consumer. Unknown executable helpers never
    # acquire an exemption through this data-row classification.
    return $true
}
function Get-WorkloadNativeContentArgument {
    param([string]$Root,[string]$Scope)
    if($Scope -cnotin @(Get-WorkloadNativeContentScopes)){return '--cpu'}
    return (Read-WorkloadNativeContent $Root $Scope).directory
}
