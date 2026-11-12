# One maintained definition supplies both selection and evidence projection.
# Unknown/shared inputs stay conservative; compile identity is separate.
function Test-WorkloadNonExecutableInput {
    param([string] $Path)
    # Exact archived evidence and existing document classes only. Unknown
    # executables beside reports remain unclassified and cannot gain a waiver.
    if($Path -match '\.(exe|dll|ps1|cmd|bat|cs|csproj|props|targets)$'){return $false}
    return $Path.Replace('\','/') -match '^(docs/|AGENTS\.md$|README(?:\.[^/]+)?$|LICENSE$|THIRD-PARTY-NOTICES\.md$|scripts/phase0s/[^/]*Owner-Test-Card[^/]*\.md$|review-materials/(movement-assists-20261019|smart-fall-protection-20261021)/evidence\.md$|review-materials/aim-midpoint-20261011-035938-HKT/.+\.(md|txt|json|csv)$|review-materials/aim-dedup-repair-20261014/(evidence\.md|(validation-map|failure-dispositions)\.json|(A-birth-directions|A-time-summary|B-model-equivalence|B-natural-births|B-natural-time-summary|B-supplement-summary|F-natural-births|F-natural-consumer-summary|F-natural-time-summary)\.csv)$)'
}
function Get-WorkloadLeafChecks {
    # Hand-opening/navigation owners are consumed by the attack preparation,
    # G11 yoyo and presentation chains, not by NPC motion models. Parent CPU
    # entries still own Navigation/AttackCosts; the children remain specialists.
    $attackConsumers=@('native-CombatGuidedNavigationCpu','native-CombatAimCpu','native-CombatControlCpu','native-CombatNavigationCpu','native-CombatMechanicsCpu','native-CombatEffectsCpu','native-CombatIntegrationCpu','native-CombatInputGeometryCpu','native-CombatSkyActionCpu','native-CombatCosts','native-CombatAttackCosts','native-CombatCpu','native-CombatYoyoCausal','native-CombatAimUi','native-CombatImpactVisual','native-CombatVisual')
    # This asset initializer is also called by the narrow combat fixture.
    # Preserve its worker consumers when adding the explicit combat leaf;
    # the combat scope itself must not inherit the legacy-worker group.
    $workerAssetConsumers=@('native-CombatInputGeometryCpu','native-CombatSkyActionCpu','native-CombatEffectsCpu','native-CombatObservationCpu','native-NpcPrivateSafety','native-NpcWorkerCatalogue','native-NpcWorkerLinked','native-NpcWorkerLifetime','native-NpcWorkerAssets','native-NpcWorkerRandom','native-NpcWorkerPlayer','native-NpcWorkerEntity','native-NpcWorkerBirth','native-NpcWorkerContext','native-NpcWorkerImmunity','native-NpcWorkerLifecycle','native-NpcWorkerFields','native-JourneySinglePlayerExit')
    foreach($scope in (Get-WorkloadNativeScopes).GetEnumerator()){
        if($scope.Value -contains 'legacy-worker'){$workerAssetConsumers+='native-'+$scope.Key}
    }
    $leaves = @{
        'tests/JueMingR.ArchitectureTests/BlueprintHoikSceneChecks.cs'=@('BlueprintHoikSceneChecks')
        'tests/NativeWorldTextProbe/NativeBlueprintHoikChecks.cs'=@('native-BlueprintHoikCpu','native-BlueprintConstructionCpu','native-BlueprintAppearanceCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintHoikMixedChecks.cs'=@('native-BlueprintHoikCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintHoikPersistenceChecks.cs'=@('native-BlueprintHoikCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintHoikRecoveryChecks.cs'=@('native-BlueprintHoikCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintHoikSubstitutionChecks.cs'=@('native-BlueprintHoikSubstitutionCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintHelperImportChecks.cs'=@('native-BlueprintPayloadCpu','native-BlueprintExchangeCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintImportedMaterialsChecks.cs'=@('native-BlueprintImportedMaterialsCpu')
        'tests/NativeWorldTextProbe/NativeViewZoomControlChecks.cs'=@('native-ViewZoomControlCpu')
        'tests/JueMingR.ArchitectureTests/RuntimeFailureChecks.cs'=@('RuntimeFailureChecks')
        'tests/JueMingR.ArchitectureTests/PreferenceDeadlineChecks.cs'=@('PreferenceDeadlineChecks')
        'tests/JueMingR.ArchitectureTests/WorldObjectText/OpenedDeadlineChecks.cs'=@('OpenedDeadlineChecks')
        'tests/NativeWorldTextProbe/NativeRuntimeFailureChecks.cs'=@('native-RuntimeFailureCpu','native-RuntimeFailureActiveCpu')
        'tests/NativeWorldTextProbe/NativeJourneyCloseAdmissionChecks.cs'=@('native-JourneyCloseCpu','native-RuntimeFailureCpu','native-RuntimeFailureActiveCpu','native-RuntimeFailureToolsCpu','native-RuntimeFailureQuickCpu','native-RuntimeFailureLoansCpu','native-RuntimeFailureWorldCpu')
        'tests/NativeWorldTextProbe/NativeRuntimeFailureReturnChecks.cs'=@('native-RuntimeFailureToolsCpu','native-RuntimeFailureQuickCpu','native-RuntimeFailureLoansCpu','native-RuntimeFailureWorldCpu')
        'tests/NativeWorldTextProbe/NativeRuntimeFailureBuildChecks.cs'=@('native-RuntimeFailureBuildCpu','native-RuntimeFailureBuildManualCpu','native-RuntimeFailureBuildWorldCpu','native-RuntimeFailureBuildUsingSelectionCpu','native-RuntimeFailureBuildUsingScrollCpu','native-RuntimeFailureBuildSelectionCpu','native-RuntimeFailureBuildScrollCpu')
        'tests/NativeWorldTextProbe/NativeRuntimeFailureBuildReceiptChecks.cs'=@('native-RuntimeFailureBuildReceiptCpu')
        'tests/NativeWorldTextProbe/NativeCombatTerrainKnowledgeChecks.cs'=@('native-CombatTerrainKnowledgeCpu')
        'tests/NativeWorldTextProbe/NativeCombatShotBirthChecks.cs'=@('native-CombatShotFacingCpu','native-CombatShotBirthOrderCpu')
        'tests/NativeWorldTextProbe/NativeCombatNailDetachChecks.cs'=@('native-CombatNailDetachCpu')
        'tests/NativeWorldTextProbe/NativeCombatReviewedNativeChecks.cs'=@('native-NpcReviewedStool','native-NpcReviewedParticle')
        'tests/NativeWorldTextProbe/NativeCombatSnapshotChecks.cs'=@('native-NpcSnapshot')
        'tests/NativeWorldTextProbe/NativeWorldGenPreviewChecks.cs'=@('native-WorldGenPreviewCpu')
        'tests/NativeWorldTextProbe/NativeCombatWorkerAssetChecks.cs'=$workerAssetConsumers
        'tests/NativeWorldTextProbe/NativeBlueprintSubstitutionChecks.cs'=@('native-BlueprintSubstitutionCpu','native-BlueprintSupplyContinuationCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintSubstitutionNativeChecks.cs'=@('native-BlueprintSubstitutionCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintSubstitutionLifecycleChecks.cs'=@('native-BlueprintSubstitutionCpu','native-BlueprintSubstitutionLifecycleCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintSubstitutionUiChecks.cs'=@('native-BlueprintCpu','native-BlueprintInput','native-BlueprintVisual','native-BlueprintSubstitutionCpu','native-BlueprintSubstitutionVisual')
        'tests/NativeWorldTextProbe/NativeBlueprintChecks.cs'=@('native-BlueprintCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintPlacedListChecks.cs'=@('native-BlueprintCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintExternalModalChecks.cs'=@('native-BlueprintCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintExternalModalPreparationChecks.cs'=@('native-BlueprintExchangeCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintPreviewCooperationChecks.cs'=@('native-BlueprintExchangeCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintPlacedMaterialsChecks.cs'=@('native-BlueprintExchangeCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintExchangeLifecycleChecks.cs'=@('native-BlueprintExchangeCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintExchangeFollowupChecks.cs'=@('native-BlueprintExchangeFollowupCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintMirrorProfileChecks.cs'=@('native-BlueprintCpu','native-BlueprintMirrorTrimCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintMirrorTrimUiChecks.cs'=@('native-BlueprintCpu','native-BlueprintMirrorTrimCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintMirrorTrimChecks.cs'=@('native-BlueprintCpu','native-BlueprintMirrorTrimCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintMirrorConsumerChecks.cs'=@('native-BlueprintCpu','native-BlueprintMirrorTrimCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintTrimAccumulationChecks.cs'=@('native-BlueprintCpu','native-BlueprintMirrorTrimCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintTrimInputChecks.cs'=@('native-BlueprintCpu','native-BlueprintMirrorTrimCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintMemberBatchChecks.cs'=@('native-BlueprintCpu','native-BlueprintMirrorTrimCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintMirrorWaitChecks.cs'=@('native-BlueprintCpu','native-BlueprintMirrorTrimCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintMirrorTrimVisualChecks.cs'=@('native-BlueprintInput')
        'tests/NativeWorldTextProbe/NativeBlueprintUiChecks.cs'=@('native-BlueprintCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintLibraryUiChecks.cs'=@('native-BlueprintCpu','native-BlueprintInput')
        'tests/NativeWorldTextProbe/NativeBlueprintInputRecoveryChecks.cs'=@('native-BlueprintCpu','native-BlueprintInput')
        'tests/NativeWorldTextProbe/NativeBlueprintInputContinuityChecks.cs'=@('native-BlueprintInput')
        'tests/NativeWorldTextProbe/NativeBlueprintLibraryVisualChecks.cs'=@('native-BlueprintCpu','native-BlueprintInput')
        'tests/NativeWorldTextProbe/NativeBlueprintCaptureContentChecks.cs'=@('native-BlueprintCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintCaptureBoundaryChecks.cs'=@('native-BlueprintCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintRepairRecoveryChecks.cs'=@('native-BlueprintCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintCompletionV2Checks.cs'=@('native-BlueprintSubstitutionCpu','native-BlueprintCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintPlacementChecks.cs'=@('native-BlueprintCpu','native-BlueprintInput')
        'tests/NativeWorldTextProbe/NativeBlueprintProjectionChecks.cs'=@('native-BlueprintCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintWorldMatchSemanticChecks.cs'=@('native-BlueprintCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintWorldChangeChecks.cs'=@('native-BlueprintCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintWorldMatchChecks.cs'=@('native-BlueprintSubstitutionCpu','native-BlueprintSubstitutionLifecycleCpu','native-BlueprintCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintLibraryWait.cs'=@('native-BlueprintCpu','native-BlueprintInput')
        'tests/NativeWorldTextProbe/NativeBlueprintBuildChecks.cs'=@('native-BlueprintHoikCpu','native-BlueprintBuildCadenceCpu','native-BlueprintSubstitutionCpu','native-BlueprintCpu','native-BlueprintConstructionCpu','native-BlueprintAppearanceCpu','native-BlueprintWiringCpu','native-BlueprintSpecialCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintBuildCadenceChecks.cs'=@('native-BlueprintBuildCadenceCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintPostProcessingContinuityChecks.cs'=@('native-BlueprintBuildCadenceCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintBusinessContinuityChecks.cs'=@('native-BlueprintBuildCadenceCpu','native-BlueprintSupplyContinuationCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintSchedulerRunnableChecks.cs'=@('native-BlueprintBuildCadenceCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintSchedulerFreshnessChecks.cs'=@('native-BlueprintBuildCadenceCpu','native-BlueprintSupplyContinuationCpu','native-BlueprintNearQueueCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintSchedulerOpportunityChecks.cs'=@('native-BlueprintBuildCadenceCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintSchedulerColdChecks.cs'=@('native-BlueprintBuildCadenceCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintFocusDiscoveryChecks.cs'=@('native-BlueprintBuildCadenceCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintOperationGroupChecks.cs'=@('native-BlueprintBuildCadenceCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintPostProcessingCadenceChecks.cs'=@('native-BlueprintBuildCadenceCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintStructureChecks.cs'=@('native-BlueprintStructureCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintEntityNetworkChecks.cs'=@('native-BlueprintPayloadCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintPayloadChecks.cs'=@('native-BlueprintPayloadCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintContainerRepairChecks.cs'=@('native-BlueprintPayloadCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintConstructionChecks.cs'=@('native-BlueprintStructureCpu','native-BlueprintPayloadCpu','native-BlueprintHoikCpu','native-BlueprintSubstitutionCpu','native-BlueprintConstructionCpu','native-BlueprintPreparationCpu','native-BlueprintAppearanceCpu','native-BlueprintWiringCpu','native-BlueprintSpecialCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintConstructionAppearanceChecks.cs'=@('native-BlueprintHoikCpu','native-BlueprintSubstitutionCpu','native-BlueprintConstructionCpu','native-BlueprintAppearanceCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintAppearanceRestorationChecks.cs'=@('native-BlueprintConstructionCpu','native-BlueprintAppearanceCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintSmartPlacementRepairChecks.cs'=@('native-BlueprintConstructionCpu','native-BlueprintAppearanceCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintExactChoiceRepairChecks.cs'=@('native-BlueprintConstructionCpu','native-BlueprintSpecialCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintSupplyMapReturnChecks.cs'=@('native-BlueprintConstructionCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintConstructionDoorChecks.cs'=@('native-BlueprintSubstitutionCpu','native-BlueprintConstructionCpu','native-BlueprintAppearanceCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintConstructionWiringSpecialChecks.cs'=@('native-BlueprintConstructionCpu','native-BlueprintWiringCpu','native-BlueprintSpecialCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintLiquidChecks.cs'=@('native-BlueprintCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintBuildProgressChecks.cs'=@('native-BlueprintCpu','native-BlueprintBuildCadenceCpu','native-BlueprintConstructionCpu','native-BlueprintAppearanceCpu','native-BlueprintWiringCpu','native-BlueprintSubstitutionCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintBuildContinuationChecks.cs'=@('native-BlueprintBuildCadenceCpu','native-BlueprintCpu','native-BlueprintConstructionCpu','native-BlueprintAppearanceCpu','native-BlueprintSubstitutionCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintBuildThroughputChecks.cs'=@('native-BlueprintCpu','native-BlueprintConstructionCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintBuildWallContextChecks.cs'=@('native-BlueprintConstructionCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintBuildNativeTargetChecks.cs'=@('native-BlueprintCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintPersistenceChecks.cs'=@('native-BlueprintCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintQualificationChecks.cs'=@('native-BlueprintCpu','native-BlueprintQualificationCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintBuildMixedSourceChecks.cs'=@('native-BlueprintCpu','native-BlueprintConstructionCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintBuildPreflightChecks.cs'=@('native-BlueprintSubstitutionCpu','native-BlueprintCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintBuildReviewPreflightChecks.cs'=@('native-BlueprintCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintBuildReviewUnknownChecks.cs'=@('native-BlueprintCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintBuildLegacyChecks.cs'=@('native-BlueprintCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintBuildChoiceChecks.cs'=@('native-BlueprintCpu','native-BlueprintConstructionCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintBuildEligibilityChecks.cs'=@('native-BlueprintCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintBuildNetworkChecks.cs'=@('native-BlueprintCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintBuildLifecycleChecks.cs'=@('native-BlueprintCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintMaterialChecks.cs'=@('native-BlueprintCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintMaterialSeamChecks.cs'=@('native-BlueprintCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintMaterialsUiChecks.cs'=@('native-BlueprintCpu','native-BlueprintInput')
        'tests/NativeWorldTextProbe/NativeBlueprintDraftMaterialsChecks.cs'=@('native-BlueprintCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintMaterialGeometryChecks.cs'=@('native-BlueprintCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintMaterialNameChecks.cs'=@('native-BlueprintInput')
        'tests/NativeWorldTextProbe/NativeBlueprintMaterialsDragChecks.cs'=@('native-BlueprintInput')
        'tests/NativeWorldTextProbe/NativeBrowserCompositionChecks.cs'=@('native-BrowserCpu')
        'tests/NativeWorldTextProbe/NativeTargetPlacementBudgetChecks.cs'=@('native-BrowserCpu')
        'tests/NativeWorldTextProbe/NativeTargetDiagnosticRequestChecks.cs'=@('native-TargetGestureDiagnostic')
        'tests/NativeWorldTextProbe/NativeTargetDiagnosticChecks.cs'=@('native-TargetGestureDiagnostic')
        'tests/NativeWorldTextProbe/NativeTargetCancellationChecks.cs'=@('native-BrowserCpu','native-PageCompositionCpu')
        'tests/NativeWorldTextProbe/NativeTargetGestureChecks.cs'=@('native-BrowserCpu')
        'tests/NativeWorldTextProbe/NativeTargetMotionInputChecks.cs'=@('native-BrowserCpu')
        'tests/NativeWorldTextProbe/NativeTargetOwnerChecks.cs'=@('native-BrowserCpu','native-PageCompositionCpu')
        'tests/NativeWorldTextProbe/NativeNaturalNameChecks.cs'=@('native-BrowserCpu')
        'tests/NativeWorldTextProbe/NativePalmTargetChecks.cs'=@('native-BrowserCpu')
        'tests/NativeWorldTextProbe/NativeActorGestureChecks.cs'=@('native-BrowserCpu','native-PageCompositionCpu')
        'tests/NativeWorldTextProbe/NativeActorResourceProofChecks.cs'=@('native-BrowserCpu')
        'tests/NativeWorldTextProbe/NativeAnnouncementOutcomeChecks.cs'=@('native-BrowserCpu')
        'tests/NativeWorldTextProbe/NativeAnnouncementObjectChecks.cs'=@('native-BrowserCpu')
        'tests/NativeWorldTextProbe/NativeAnnouncementTargetChecks.cs'=@('native-BrowserCpu')
        'tests/NativeWorldTextProbe/NativePlacementIdentityChecks.cs'=@('native-BrowserCpu')
        'tests/NativeWorldTextProbe/NativeSwitchIdentityChecks.cs'=@('native-BrowserCpu')
        'tests/NativeWorldTextProbe/NativeLegacyPlacementIdentityChecks.cs'=@('native-BrowserCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintQueueRecoveryChecks.cs'=@('native-BlueprintNearQueueCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintReachAdmissionChecks.cs'=@('native-BlueprintNearQueueCpu')
        # This owner authenticates only actual Content consumers; CPU fixtures
        # keep their original argument and proof projection.
        'scripts/workload/Workload.NativeContent.ps1'=@('native-BlueprintInput','native-NpcInvasionFlight','native-NpcInvasionFlightLoss','native-NpcBetsy','native-NpcDarkMage','native-NpcBloodNautilus','native-NpcTreeBoss','native-NpcReviewedParticle','workload-Evidence','workload-Routing','workload-DevelopmentCandidate','workload-PackageVerification')
        'tests/Workload/Invoke-WorkloadContentChecks.ps1'=@('workload-Evidence')
        'tests/NativeWorldTextProbe/NativeMovementJumpChecks.cs'=@('native-MovementJumpCpu')
        'tests/NativeWorldTextProbe/NativeMovementJumpHandoffChecks.cs'=@('native-MovementJumpCpu')
        'tests/NativeWorldTextProbe/NativeSmartFallRiskActionsChecks.cs'=@('native-MovementSmartFallCpu')
        'tests/NativeWorldTextProbe/NativeSmartFallActionsChecks.cs'=@('native-MovementSmartFallCpu')
        'tests/NativeWorldTextProbe/NativeSmartFallCobwebChecks.cs'=@('native-MovementSmartFallCpu')
        'tests/NativeWorldTextProbe/NativeSmartFallBorrowChecks.cs'=@('native-MovementSmartFallCpu')
        'tests/NativeWorldTextProbe/NativeSmartFallScopeChecks.cs'=@('native-MovementSmartFallCpu')
        'tests/Phase0U/MovementConfigurationChecks.cs'=@('fixture-movement-layout')
        'tests/NativeWorldTextProbe/NativeMovementJumpWireChecks.cs'=@('native-MovementJumpCpu')
        'tests/NativeWorldTextProbe/NativeMovementDashChecks.cs'=@('native-MovementDashCpu')
        'tests/NativeWorldTextProbe/NativeTeleportMovementChecks.cs'=@('native-MovementTeleportCpu')
        'tests/NativeWorldTextProbe/NativeMovementUiChecks.cs'=@('native-MovementOriginal')
        'tests/NativeWorldTextProbe/NativeMovementOriginalChecks.cs'=@('native-MovementOriginal')
        'tests/NativeWorldTextProbe/NativeViewCombatChecks.cs'=@('native-ViewCombatCpu')
        'tests/NativeWorldTextProbe/NativeViewRangeConsumerChecks.cs'=@('native-ViewRangeConsumerCpu')
        'tests/NativeWorldTextProbe/NativeViewZoomSpeedChecks.cs'=@('native-ViewZoomSpeedCpu')
        'tests/NativeWorldTextProbe/NativeJourneyChecks.cs'=@('native-JourneyCpu','native-Journey','native-JourneyCloseCpu','native-RuntimeFailureCpu','native-RuntimeFailureActiveCpu','native-RuntimeFailureToolsCpu','native-RuntimeFailureQuickCpu','native-RuntimeFailureLoansCpu','native-RuntimeFailureWorldCpu','native-JourneySinglePlayerExit')
        'tests/NativeWorldTextProbe/NativeJourneySinglePlayerExitChecks.cs'=@('native-JourneySinglePlayerExit')
        'tests/NativeWorldTextProbe/NativeRenameMapChecks.cs'=@('native-FishingAnglerContinuousCpu','native-FishingCpu','native-JourneySinglePlayerExit')
        'tests/NativeWorldTextProbe/NativeJourneyUiChecks.cs'=@('native-Journey','native-JourneySinglePlayerExit')
        'tests/NativeWorldTextProbe/NativeJourneyPredictionChecks.cs'=@('native-JourneyPredictionCpu')
        'src/JueMingR.TerrariaHost/Combat/HostAttackControl.cs'=$attackConsumers
        'src/JueMingR.TerrariaHost/Combat/HostAttackWindow.cs'=$attackConsumers
        'src/JueMingR.TerrariaHost/Combat/HostYoyoNavigation.cs'=$attackConsumers
        'scripts/verify-existing-package.ps1'=@('workload-PackageVerification')
        'scripts/phase0s/PackageVerification.Support.ps1'=@('workload-PackageVerification')
        'tests/Phase0S/Invoke-PackageVerificationChecks.ps1'=@('workload-PackageVerification')
        'tests/NativeWorldTextProbe/NativePageCompositionChecks.cs'=@('native-PageCompositionCpu')
        'tests/NativeWorldTextProbe/NativeDeathPreferenceFeedbackChecks.cs'=@('native-ShortFeedbackCpu')
        'tests/NativeWorldTextProbe/NativeMiscPolishUiChecks.cs'=@('native-ProcessingCpu','native-ProcessingVisual','native-ToolsVisual')
        'tests/NativeWorldTextProbe/NativeHerbMovementChecks.cs'=@('native-ToolsExecutionCpu','native-ToolsExecution','native-ToolsExecutionRelease')
        'tests/NativeWorldTextProbe/NativeJourneyMultiplayerChecks.cs'=@('native-JourneyCpu','native-Journey')
        'tests/NativeWorldTextProbe/NativeJourneyDisconnectSaveChecks.cs'=@('native-Journey')
        # Merchant expectations execute only in RunCpu. Other scopes borrow
        # unchanged Flags/Npc/Until/CheckLocked helpers; widen this leaf if those
        # shared helpers change rather than silently dropping their consumers.
        'tests/NativeWorldTextProbe/NativeGuidanceChecks.cs'=@('native-GuidanceCpu')
        'tests/NativeWorldTextProbe/NativeBackgroundAutomationChecks.cs'=@('native-BackgroundCpu','native-F5AutomationCpu')
        'tests/NativeWorldTextProbe/NativeToolCadenceChecks.cs'=@('native-ToolsCadence')
        'tests/NativeWorldTextProbe/NativeToolsWorkloadChecks.cs'=@('native-ToolsWorkload')
        'tests/NativeWorldTextProbe/NativeYoyoAdoptionChecks.cs'=@('native-CombatCpu','native-CombatYoyoCausal')
        'tests/NativeWorldTextProbe/NativeCombatAimChecks.cs'=@('native-CombatAimCpu')
        # These private Run bodies are reached only by their named formal
        # branches. Shared NativeChecks/fixtures and attack-cost owners stay shared.
        'tests/NativeWorldTextProbe/NativeCombatCostChecks.cs'=@('native-CombatCosts')
        'tests/NativeWorldTextProbe/NativeCombatSyncChecks.cs'=@('native-NpcSync')
        # Initialize also serves native Journey and graphical footprint/path
        # coexistence. The latter remains a separate graphics-risk entry; the
        # ordinary footprints CPU group cannot claim it executed real Draw.
        'tests/NativeWorldTextProbe/NativeCombatAimFixture.cs'=@('native-CombatAimCpu','native-CombatControlCpu','native-CombatNavigationCpu','native-CombatMechanicsCpu','native-CombatEffectsCpu','native-CombatIntegrationCpu','native-CombatInputGeometryCpu','native-CombatSkyActionCpu','native-CombatCosts','native-CombatAttackCosts','native-CombatImpactVisual','native-CombatAimUi','native-CombatVisual','native-CombatObservationCpu','native-CombatTerrainCpu','native-CombatShotFacingCpu','native-CombatShotBirthOrderCpu','native-CombatNailDetachCpu','native-NpcPlayerPolicy','native-Journey','native-JourneyCpu','native-JourneyPredictionCpu','native-FootprintsCpu','native-JourneySinglePlayerExit')
        'tests/NativeWorldTextProbe/NativeCombatVisualChecks.cs'=@('native-CombatVisual')
        'tests/NativeWorldTextProbe/NativeCombatAimQualificationChecks.cs'=@('native-CombatIntegrationCpu')
        'tests/NativeWorldTextProbe/NativeCombatAimUiChecks.cs'=@('native-CombatAimUi','native-CombatVisual')
        'tests/NativeWorldTextProbe/NativeCombatImpactVisualChecks.cs'=@('native-CombatImpactVisual','native-CombatVisual')
        'tests/NativeWorldTextProbe/NativeCombatImpactRetirementChecks.cs'=@('native-CombatImpactVisual','native-CombatVisual')
        'tests/NativeWorldTextProbe/NativeCombatImpactVisualChecks.Geometry.cs'=@('native-CombatImpactVisual','native-CombatVisual')
        'tests/NativeWorldTextProbe/NativeCombatIntegrationChecks.cs'=@('native-CombatIntegrationCpu')
        'tests/NativeWorldTextProbe/NativeCombatExperimentChecks.cs'=@('native-CombatExperimentsCpu')
        'tests/NativeWorldTextProbe/NativeCombatExperimentHotkeyChecks.cs'=@('native-CombatExperimentsCpu')
        'tests/NativeWorldTextProbe/NativeDeathInputChecks.cs'=@('native-DeathCpu')
        'tests/NativeWorldTextProbe/NativeFootprintChecks.cs'=@('native-FootprintsCpu')
        'tests/NativeWorldTextProbe/NativeMapMarkerChecks.cs'=@('native-ExplorationCpu')
        'tests/NativeWorldTextProbe/NativeCombatBalancedAimChecks.cs'=@('native-CombatIntegrationCpu','native-CombatInputGeometryCpu','native-CombatSkyActionCpu')
        'tests/NativeWorldTextProbe/NativeCombatInputGeometryChecks.cs'=@('native-CombatInputGeometryCpu')
        'tests/NativeWorldTextProbe/NativeCombatSkyActionChecks.cs'=@('native-CombatSkyActionCpu')
        'tests/NativeWorldTextProbe/NativeCombatSkySemanticsChecks.cs'=@('native-CombatSkyActionCpu')
        'tests/NativeWorldTextProbe/NativeCombatSkyNextPoseChecks.cs'=@('native-CombatSkyActionCpu')
        'tests/NativeWorldTextProbe/NativeCombatLunarChecks.cs'=@('native-CombatSkyActionCpu')
        'tests/NativeWorldTextProbe/NativeCombatReflectionSeamsChecks.cs'=@('native-CombatSkyActionCpu')
        'tests/NativeWorldTextProbe/NativeCombatBalancedPhaseChecks.cs'=@('native-CombatIntegrationCpu')
        'tests/NativeWorldTextProbe/NativeCombatBalancedCandidateChecks.cs'=@('native-CombatIntegrationCpu')
        # Step is a shared native update/purity fixture. Bind every actual
        # caller even when this revision changes only the private ranking tests.
        'tests/NativeWorldTextProbe/NativeCombatSelectionPreviewChecks.cs'=@('native-CombatAimCpu','native-CombatControlCpu','native-CombatMechanicsCpu','native-CombatEffectsCpu','native-CombatIntegrationCpu','native-CombatCosts','native-CombatAttackCosts','native-CombatImpactVisual','native-CombatVisual')
        'tests/NativeWorldTextProbe/NativeCombatControlPreviewChecks.cs'=@('native-CombatIntegrationCpu','native-CombatCosts','native-CombatAttackCosts','native-CombatSkyActionCpu','native-CombatShotFacingCpu','native-CombatShotBirthOrderCpu','native-CombatNailDetachCpu')
        'tests/NativeWorldTextProbe/NativeCombatUsageChecks.cs'=@('native-CombatIntegrationCpu','native-CombatImpactVisual')
        'tests/NativeWorldTextProbe/NativeCombatOriginalFeedbackChecks.cs'=@('native-CombatIntegrationCpu','native-CombatImpactVisual')
        'tests/NativeWorldTextProbe/NativeCombatOriginalRangedChecks.cs'=@('native-CombatIntegrationCpu')
        'tests/NativeWorldTextProbe/NativeCombatCelebrationChecks.cs'=@('native-CombatIntegrationCpu')
        'tests/NativeWorldTextProbe/NativeCombatBasicAimChecks.cs'=@('native-CombatAimCpu','native-CombatIntegrationCpu','native-CombatImpactVisual')
        'tests/NativeWorldTextProbe/NativeCombatMagicStringAimChecks.cs'=@('native-CombatIntegrationCpu')
        'tests/NativeWorldTextProbe/NativeCombatBallisticCandidateChecks.cs'=@('native-CombatEffectsCpu','native-CombatSkyActionCpu','native-CombatShotFacingCpu','native-CombatShotBirthOrderCpu','native-CombatNailDetachCpu')
        'tests/NativeWorldTextProbe/NativeCombatBallisticPreparationChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatBallisticSceneChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatBallisticMotionChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatBallisticSeedChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatBallisticFreeFlightChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatBallisticCloudChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatBallisticRapierChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatBallisticTyphoonChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatHomingEffectChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatTerrainLineChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatYoyoFamilyChecks.cs'=@('native-CombatControlCpu','native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatYoyoFamilyConsumerChecks.cs'=@('native-CombatControlCpu','native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatPhantasmEchoChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatSpearShardChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatGuitarCurveChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatWeatherPainChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatSolarEruptionChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatNailChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatInfluxChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatNebulaArcanumChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatMedusaScanChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatSuperStarChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatMonkStaffChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatGrassCurveChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatAmmoImpactChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatNorthPoleChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatLightsBaneChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatOnyxBlastChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatBeeHandChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatMeleeHitChildChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatMeleeHitFamilyChecks.cs'=@('native-CombatEffectsCpu','native-CombatIntegrationCpu')
        'tests/NativeWorldTextProbe/NativeCombatHandFixture.cs'=@('native-CombatEffectsCpu','native-CombatAimCpu')
        'tests/NativeWorldTextProbe/NativeCombatAmmoSteeringChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatHeldAmmoFamilyChecks.cs'=@('native-CombatEffectsCpu','native-CombatControlCpu')
        'tests/NativeWorldTextProbe/NativeCombatSharpTearsChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatPiranhaChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatBubbleFlailFamilyChecks.cs'=@('native-CombatEffectsCpu','native-CombatControlCpu')
        'tests/NativeWorldTextProbe/NativeCombatHarpyArrowFamilyChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatClingerWallChecks.cs'=@('native-CombatEffectsCpu','native-CombatSkyActionCpu')
        'tests/NativeWorldTextProbe/NativeCombatArrowFamilyChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatJoustingLanceChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatWhipStarChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatFixedSkyChecks.cs'=@('native-CombatEffectsCpu','native-CombatSkyActionCpu')
        'tests/NativeWorldTextProbe/NativeCombatSeedlerChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatRainbowChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatImpactBurstChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatBallisticSpiritChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatPlacedPulseChecks.cs'=@('native-CombatEffectsCpu','native-CombatSkyActionCpu')
        'tests/NativeWorldTextProbe/NativeCombatMagnetSphereChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatDirectionalControlChecks.cs'=@('native-CombatControlCpu')
        'tests/NativeWorldTextProbe/NativeCombatCostNavigationChecks.cs'=@('native-CombatControlCpu','native-CombatNavigationCpu','native-CombatIntegrationCpu')
        'tests/NativeWorldTextProbe/Fixtures/MagicReleaseBeforeNpc.bits'=@('native-CombatControlCpu','native-CombatIntegrationCpu')
        'tests/NativeWorldTextProbe/NativeCombatPathContinuityChecks.cs'=@('native-CombatVisual')
        'tests/NativeWorldTextProbe/NativeCombatPathDisplayChecks.cs'=@('native-CombatVisual')
        'tests/NativeWorldTextProbe/NativeCombatPlacementChecks.cs'=@('native-CombatIntegrationCpu')
        'tests/NativeWorldTextProbe/NativeCombatCadenceAimChecks.cs'=@('native-CombatIntegrationCpu')
        'tests/NativeWorldTextProbe/NativeCombatResourceChecks.cs'=@('native-CombatIntegrationCpu')
        'tests/NativeWorldTextProbe/NativeCombatCandidateDemandChecks.cs'=@('native-CombatIntegrationCpu')
        'tests/NativeWorldTextProbe/NativeCombatCurrentDemandChecks.cs'=@('native-CombatIntegrationCpu')
        'tests/NativeWorldTextProbe/NativeCombatCandidateFailureChecks.cs'=@('native-CombatIntegrationCpu')
        'tests/NativeWorldTextProbe/NativeCombatCandidateReuseChecks.cs'=@('native-CombatIntegrationCpu')
        'tests/NativeWorldTextProbe/NativeCombatNeighborChecks.cs'=@('native-CombatIntegrationCpu')
        'tests/NativeWorldTextProbe/NativeCombatAttackCostChecks.cs'=@('native-CombatCosts','native-CombatAttackCosts','native-CombatImpactVisual','native-CombatVisual')
        'tests/NativeWorldTextProbe/NativeCombatAttackCostChecks.Optional.cs'=@('native-CombatCosts','native-CombatAttackCosts')
        # CombatAttackCosts is a specialist selector; its normal workload
        # dispatcher is CombatCosts, which actually calls the new leaf once.
        'tests/NativeWorldTextProbe/NativeCombatDedupChecks.cs'=@('native-CombatCosts','native-CombatSkyActionCpu')
        'tests/NativeWorldTextProbe/NativeCombatDedupPremiseChecks.cs'=@('native-CombatCosts')
        'tests/NativeWorldTextProbe/NativeCombatDedupReasons.cs'=@('native-CombatCosts','native-CombatSkyActionCpu')
        'tests/NativeWorldTextProbe/NativeCombatDedupPrefixChecks.cs'=@('native-CombatCosts')
        'tests/NativeWorldTextProbe/NativeCombatDedupModelChecks.cs'=@('native-CombatCosts','native-CombatSkyActionCpu')
        'tests/NativeWorldTextProbe/NativeCombatDedupSourceChecks.cs'=@('native-CombatCosts')
        'tests/NativeWorldTextProbe/NativeCombatPreviewLeaseChecks.cs'=@('native-CombatCosts','native-CombatAttackCosts','native-CombatControlCpu','native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatAttackCostChecks.Natural.cs'=@('native-CombatCosts','native-CombatAttackCosts')
        'tests/NativeWorldTextProbe/NativeCombatAttackCostChecks.CandidateState.cs'=@('native-CombatCosts','native-CombatAttackCosts')
        'tests/NativeWorldTextProbe/NativeBlueprintSupplyHandoffChecks.cs'=@('native-BlueprintSupplyContinuationCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintSupplyReadyChecks.cs'=@('native-BlueprintSupplyContinuationCpu')
        'tests/NativeWorldTextProbe/NativeCombatObservationSuite.cs'=@('native-CombatObservationCpu','native-CombatObservationBoundaryCpu')
        'tests/NativeWorldTextProbe/NativeObservationBoundaryChecks.cs'=@('native-CombatObservationBoundaryCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintSchedulerColdHandoffChecks.cs'=@('native-BlueprintBuildCadenceCpu')
        'tests/NativeWorldTextProbe/NativeBlueprintSupplyConversionChecks.cs'=@('native-BlueprintPreparationCpu','native-BlueprintConstructionCpu')
        'tests/NativeWorldTextProbe/NativeCombatEffectChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatProjectileEnvironmentChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatObstacleChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatAmmoChecks.cs'=@('native-CombatAimCpu','native-CombatSkyActionCpu')
        'tests/NativeWorldTextProbe/NativeCombatSkyChecks.cs'=@('native-CombatAimCpu','native-CombatSkyActionCpu')
        'tests/NativeWorldTextProbe/NativeCombatScatterChecks.cs'=@('native-CombatAimCpu')
        'tests/NativeWorldTextProbe/NativeCombatHeldSequenceChecks.cs'=@('native-CombatControlCpu')
        'tests/NativeWorldTextProbe/NativeCombatAttackFailureChecks.cs'=@('native-CombatControlCpu')
        'tests/NativeWorldTextProbe/NativeCombatGuidedNavigationChecks.cs'=@('native-CombatGuidedNavigationCpu')
        'tests/NativeWorldTextProbe/NativeCombatGuidedBudgetChecks.cs'=@('native-CombatGuidedNavigationCpu')
        'tests/NativeWorldTextProbe/NativeCombatGuidedLiquidChecks.cs'=@('native-CombatGuidedNavigationCpu')
        'tests/NativeWorldTextProbe/NativeCombatControlChecks.cs'=@('native-CombatControlCpu')
        'tests/NativeWorldTextProbe/NativeCombatBubbleChecks.cs'=@('native-CombatControlCpu')
        'tests/NativeWorldTextProbe/NativeCombatFlyingKnifeChecks.cs'=@('native-CombatControlCpu')
        'tests/NativeWorldTextProbe/NativeCombatLaserChecks.cs'=@('native-CombatControlCpu')
        'tests/NativeWorldTextProbe/NativeCombatHarpyChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatFiniteThrowChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatFiniteShotChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatMixedSwingChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatBeeGunChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatWaspBatChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatDartChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatLaserPaintChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatJavelinChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatPhoenixChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatThornChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatStreamChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatBottleChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatNoteChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatNebulaBlazeChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatSkyFractureChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatIceRodChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatAi29SpellChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatFlareChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatBlizzardChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatRaySpellChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatPaperAirplaneChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatInfernoChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatOrnamentChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatSonicBoomChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatBookStaffChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatSkullChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatArcSwingChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatAttachedShotChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatBetsyFlameChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatElectrosphereChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatBarrelChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatLineChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatBeeChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatReflectionChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatColdBoltChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatScytheChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatScourgeChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatPhasebladeChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatClusterChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatBoomerangVariantChecks.cs'=@('native-CombatMechanicsCpu')
        'tests/NativeWorldTextProbe/NativeCombatBoomerangCommonChecks.cs'=@('native-CombatMechanicsCpu')
        'tests/NativeWorldTextProbe/NativeCombatGemStaffChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatPoisonCloudChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatFlameChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatSpecialArrowChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatExplosiveChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatHarpoonChecks.cs'=@('native-CombatEffectsCpu')
        'tests/NativeWorldTextProbe/NativeCombatBeamChecks.cs'=@('native-CombatControlCpu')
        'tests/NativeWorldTextProbe/NativeCombatYoyoNavigationChecks.cs'=@('native-CombatControlCpu','native-CombatNavigationCpu')
        'tests/NativeWorldTextProbe/NativeCombatYoyoGapChecks.cs'=@('native-CombatControlCpu','native-CombatNavigationCpu')
        'tests/NativeWorldTextProbe/NativeCombatYoyoChecks.cs'=@('native-CombatControlCpu')
        'tests/NativeWorldTextProbe/NativeCombatReceiveChecks.cs'=@('native-CombatControlCpu')
        'tests/NativeWorldTextProbe/NativeCombatMeleeChecks.cs'=@('native-CombatMechanicsCpu')
        'tests/NativeWorldTextProbe/NativeCombatPhaseChecks.cs'=@('native-CombatMechanicsCpu','native-CombatControlCpu')
        'tests/NativeWorldTextProbe/NativeCombatSwingChecks.cs'=@('native-CombatMechanicsCpu')
        'tests/NativeWorldTextProbe/NativeCombatSwingSpecialChecks.cs'=@('native-CombatMechanicsCpu')
        'tests/NativeWorldTextProbe/NativeCombatWhipChecks.cs'=@('native-CombatMechanicsCpu')
        'tests/NativeWorldTextProbe/NativeCombatReturnChecks.cs'=@('native-CombatMechanicsCpu')
        'tests/NativeWorldTextProbe/NativeCombatFoundationChecks.cs'=@('native-NpcFoundationRules','native-NpcFoundationContinuous','native-NpcStrategyContinuous')
        'tests/NativeWorldTextProbe/NativeCombatPhasedFlightChecks.cs'=@('native-NpcPhasedFlight')
        'tests/NativeWorldTextProbe/NativeCombatAmbientFlightChecks.cs'=@('native-NpcAmbientFlight')
        'tests/NativeWorldTextProbe/NativeCombatRelatedChecks.cs'=@('native-NpcRelatedMotion')
        'tests/NativeWorldTextProbe/NativeCombatFreeFlightBoundsChecks.cs'=@('native-NpcRelatedMotion')
        'tests/NativeWorldTextProbe/NativeCombatPlanteraChecks.cs'=@('native-NpcPlantera')
        'tests/NativeWorldTextProbe/NativeCombatInvasionFlightChecks.cs'=@('native-NpcInvasionFlightCpu','native-NpcInvasionFlightLoss')
        'tests/NativeWorldTextProbe/NativeCombatInvasionGroundChecks.cs'=@('native-NpcInvasionGround')
        'tests/NativeWorldTextProbe/NativeCombatEyeBossChecks.cs'=@('native-NpcEyeBoss')
        'tests/NativeWorldTextProbe/NativeCombatSnowmanChecks.cs'=@('native-NpcSnowman')
        'tests/NativeWorldTextProbe/NativeCombatSantaTankChecks.cs'=@('native-NpcSantaTank')
        'tests/NativeWorldTextProbe/NativeCombatPlayerBorderChecks.cs'=@('native-NpcPlayerPolicy')
        'tests/NativeWorldTextProbe/NativeCombatKingSlimeChecks.cs'=@('native-NpcKingSlime')
        'tests/NativeWorldTextProbe/NativeCombatSkeletonBossChecks.cs'=@('native-NpcSkeletonBoss')
        'tests/NativeWorldTextProbe/NativeCombatBetsyChecks.cs'=@('native-NpcBetsy')
        'tests/NativeWorldTextProbe/NativeCombatTownCritterChecks.cs'=@('native-NpcTownCritter')
        'tests/NativeWorldTextProbe/NativeCombatInvasionLightningChecks.cs'=@('native-NpcInvasionLightning')
        'tests/NativeWorldTextProbe/NativeCombatDiveFlightChecks.cs'=@('native-NpcDiveFlight')
        'tests/NativeWorldTextProbe/NativeCombatAttachedWeaponChecks.cs'=@('native-NpcAttachedWeapon')
        'tests/NativeWorldTextProbe/NativeCombatDarkMageChecks.cs'=@('native-NpcDarkMage')
        'tests/NativeWorldTextProbe/NativeCombatBloodNautilusChecks.cs'=@('native-NpcBloodNautilus')
        'tests/NativeWorldTextProbe/NativeCombatGolemChecks.cs'=@('native-NpcGolem')
        'tests/NativeWorldTextProbe/NativeCombatQueenBeeChecks.cs'=@('native-NpcQueenBee')
        'tests/NativeWorldTextProbe/NativeCombatBrainSwarmChecks.cs'=@('native-NpcBrainSwarm')
        'tests/NativeWorldTextProbe/NativeCombatWallBossChecks.cs'=@('native-NpcWallBoss')
        'tests/NativeWorldTextProbe/NativeCombatTreeBossChecks.cs'=@('native-NpcTreeBoss')
        'tests/NativeWorldTextProbe/NativeCombatCelestialTowerChecks.cs'=@('native-NpcCelestialTower')
        'tests/NativeWorldTextProbe/NativeCombatPumpkingChecks.cs'=@('native-NpcPumpking')
        'tests/NativeWorldTextProbe/NativeCombatIceQueenChecks.cs'=@('native-NpcIceQueen')
        'tests/NativeWorldTextProbe/NativeCombatCultistBossChecks.cs'=@('native-NpcCultistBoss')
        'tests/NativeWorldTextProbe/NativeCombatCultistDoomOwnerChecks.cs'=@('native-NpcCultistBoss')
        'tests/NativeWorldTextProbe/NativeCombatCultistRitualChecks.cs'=@('native-NpcCultistRitual')
        'tests/NativeWorldTextProbe/NativeCombatMoonLordChecks.cs'=@('native-NpcMoonLord')
        'tests/NativeWorldTextProbe/NativeCombatMoonLeechChecks.cs'=@('native-NpcMoonLeech')
        'tests/NativeWorldTextProbe/NativeCombatQueenSlimeChecks.cs'=@('native-NpcQueenSlime')
        'tests/NativeWorldTextProbe/NativeCombatWindyBalloonChecks.cs'=@('native-NpcWindyBalloon')
        'tests/NativeWorldTextProbe/NativeCombatBirdChecks.cs'=@('native-NpcBird')
        'tests/NativeWorldTextProbe/NativeCombatBirdTownChecks.cs'=@('native-NpcBird')
        'tests/NativeWorldTextProbe/NativeCombatCasterChecks.cs'=@('native-NpcCaster')
        'tests/NativeWorldTextProbe/NativeCombatMartianTurretChecks.cs'=@('native-NpcMartianTurret')
        'tests/NativeWorldTextProbe/NativeCombatSegmentedWormChecks.cs'=@('native-NpcSegmentedWorm')
        'tests/NativeWorldTextProbe/NativeCombatSolitaryWormChecks.cs'=@('native-NpcSolitaryWorm')
        'tests/NativeWorldTextProbe/NativeCombatSolarWormChecks.cs'=@('native-NpcSolarWorm')
        'tests/NativeWorldTextProbe/NativeCombatEmpressChecks.cs'=@('native-NpcEmpress')
        'tests/NativeWorldTextProbe/NativeCombatDeerclopsChecks.cs'=@('native-NpcDeerclops')
        'tests/NativeWorldTextProbe/NativeCombatFishronChecks.cs'=@('native-NpcFishron')
        'tests/NativeWorldTextProbe/NativeCombatFishronChildChecks.cs'=@('native-NpcFishron')
        'tests/NativeWorldTextProbe/NativeCombatBigMimicChecks.cs'=@('native-NpcBigMimic')
        'tests/NativeWorldTextProbe/NativeCombatAquaticActionChecks.cs'=@('native-NpcAquaticActions')
        'tests/NativeWorldTextProbe/NativeCombatTerrainFlightChecks.cs'=@('native-NpcTerrainFlight','native-NpcUmbrella')
        'tests/NativeWorldTextProbe/NativeCombatFoundationContinuousChecks.cs'=@('native-NpcFoundationContinuous','native-NpcStrategyContinuous','native-NpcFiniteFlight')
        'tests/NativeWorldTextProbe/NativeCombatStrategyChecks.cs'=@('native-NpcStrategy','native-NpcStrategy-rollchoice')
        'tests/NativeWorldTextProbe/NativeCombatFamilyControlChecks.cs'=@('native-NpcStrategy')
        'tests/NativeWorldTextProbe/NativeCombatFiniteControlChecks.cs'=@('native-NpcStrategy')
        'tests/NativeWorldTextProbe/NativeCombatRetargetChecks.cs'=@('native-NpcStrategy')
        'tests/NativeWorldTextProbe/NativeCombatRollingControlChecks.cs'=@('native-NpcStrategy','native-NpcStrategy-rollchoice')
        'tests/NativeWorldTextProbe/NativeNpcLifetimeChecks.cs'=@('native-NpcStrategy','native-NpcStrategy-rollchoice')
        'tests/NativeWorldTextProbe/NativeOuterInputBoundaryChecks.cs'=@('native-InputBoundary')
        'tests/NativeWorldTextProbe/NativeCombatRunningControlChecks.cs'=@('native-NpcStrategy')
        'tests/NativeWorldTextProbe/NativeCombatStructuralControlChecks.cs'=@('native-NpcStrategy')
        'tests/NativeWorldTextProbe/NativeCombatFighterControlChecks.cs'=@('native-NpcStrategy')
        'tests/NativeWorldTextProbe/NativeCombatPositionControlChecks.cs'=@('native-NpcStrategy')
        'tests/NativeWorldTextProbe/NativeCombatFlyingTailChecks.cs'=@('native-NpcStrategy')
        'tests/NativeWorldTextProbe/NativeCombatEventRetirementChecks.cs'=@('native-NpcEventRetirementCpu','native-NpcEventRetirement')
        'tests/NativeWorldTextProbe/NativeDisplayResponsibilityChecks.cs'=@('native-NpcDisplayIsolation')
        'tests/NativeWorldTextProbe/NativeCombatCloseoutTerrainChecks.cs'=@('native-NpcCloseoutTerrain')
        'tests/Workload/Invoke-WorkloadRoutingChecks.ps1'=@('workload-Routing')
        'tests/Workload/Invoke-WorkloadEvidenceChecks.ps1'=@('workload-Evidence')
    }
    # The exchange owner-test slice reuses real BlueprintCpu/world-match and
    # construction helpers. Conservatively bind their existing leaf inputs;
    # this expands fingerprint responsibility without executing sibling scopes.
    foreach($path in @($leaves.Keys)){
        if(@($leaves[$path] | Where-Object {$_ -in @('native-BlueprintCpu','native-BlueprintConstructionCpu')}).Count){
            $leaves[$path]=@($leaves[$path])+@('native-BlueprintExchangeCpu')
        }
    }
    foreach($path in @('tests/NativeWorldTextProbe/NativeBlueprintExchangeLifecycleChecks.cs','tests/NativeWorldTextProbe/NativeBlueprintPlacedListChecks.cs')){
        $leaves[$path]=@($leaves[$path])+@('native-BlueprintExchangeFollowupCpu')
    }
    # The failure-flight leaf reuses these real construction and input helpers.
    # Keep their assertion bytes bound without claiming sibling execution.
    $failureBuild=@('native-RuntimeFailureBuildUsingSelectionCpu','native-RuntimeFailureBuildUsingScrollCpu','native-RuntimeFailureBuildSelectionCpu','native-RuntimeFailureBuildScrollCpu','native-RuntimeFailureBuildCpu','native-RuntimeFailureBuildManualCpu','native-RuntimeFailureBuildWorldCpu','native-RuntimeFailureBuildReceiptCpu')
    foreach($path in @($leaves.Keys)){
        if(@($leaves[$path]|Where-Object {$_ -in @('native-BlueprintCpu','native-BlueprintConstructionCpu')}).Count -or $path -cin @('tests/NativeWorldTextProbe/NativeToolExecutionChecks.cs','tests/NativeWorldTextProbe/NativeRuntimeFailureReturnChecks.cs')){
            $leaves[$path]=@($leaves[$path])+@($failureBuild)
        }
    }
    # EmptyWorld/metadata are called by both actual Build and Construction
    # runners, including their narrow families. Never discard that shared setup.
    $leaves['tests/NativeWorldTextProbe/NativeBlueprintWorldMatchChecks.cs']+=@('native-BlueprintBuildCadenceCpu','native-BlueprintConstructionCpu','native-BlueprintPreparationCpu','native-BlueprintAppearanceCpu','native-BlueprintWiringCpu','native-BlueprintSpecialCpu')
    # SupplyRepair's actual preparation/conversion/handoff/Ready/native-tail
    # call graph. PostProcessingProbe, BusinessScope and SchedulerTimeline are
    # shared helpers despite their files' private cadence assertion bodies.
    # Appearance/Continuation/Network/Hammer files also initialize this partial
    # type; retain them until those initializers have a separate shared owner.
    $supplyHelpers=@('Construction','SupplyConversion','SupplyHandoff','SupplyReady',
        'Build','PostProcessingContinuity','BusinessContinuity','SchedulerFreshness',
        'Substitution','SupplyLifecycle','WorldMatch','ConstructionAppearance',
        'BuildContinuation','BuildNetwork','HammerEligibility','ConstructionWiringSpecial','CompletionV2')
    foreach($helper in $supplyHelpers){
        $path='tests/NativeWorldTextProbe/NativeBlueprint'+$helper+'Checks.cs'
        $leaves[$path]=@($leaves[$path])+@('native-BlueprintSupplyContinuationCpu')
    }
    # NativeToolExecution/NativeTools stay shared through their ordinary group
    # route; making an exclusive leaf map here would omit their other consumers.
    $leaves['tests/NativeWorldTextProbe/NativeBlueprintWiringZeroDebitChecks.cs']=@('native-BlueprintConstructionCpu','native-BlueprintWiringCpu')
    # The narrow queue scene shares the actual build and business fixture.
    # Restock uses SupplyHandoff; exact native gap replay uses Opportunity's
    # HandoffRun and Freshness's SchedulerTimeline (not ColdHandoff's body).
    # CaptureCompletionAsset lives in CompletionV2; raw Save/Place consume
    # LibraryWait. Keep their helper/partial-initialization bytes bound too.
    foreach($helper in @('Build','BusinessContinuity','WorldMatch','Construction',
        'BuildContinuation','BuildNetwork','HammerEligibility','ConstructionWiringSpecial','ConstructionAppearance',
        'SupplyHandoff','SchedulerOpportunity','SchedulerFreshness','CompletionV2','PostProcessingContinuity','WiringZeroDebit','BuildMixedSource','BuildProgress')){
        $path='tests/NativeWorldTextProbe/NativeBlueprint'+$helper+'Checks.cs'
        $leaves[$path]=@($leaves[$path])+@('native-BlueprintNearQueueCpu')
    }
    # Follow-up Hoik substitution calls these existing native preparation,
    # actual source-capture, Save/Place and diagnostic helpers. Bind their
    # bytes to this new leaf without executing their sibling families.
    foreach($helper in @('Build','Construction','ConstructionAppearance','WorldMatch','Substitution','Hoik')){
        $path='tests/NativeWorldTextProbe/NativeBlueprint'+$helper+'Checks.cs'
        $leaves[$path]=@($leaves[$path])+@('native-BlueprintHoikSubstitutionCpu')
    }
    $leaves['tests/NativeWorldTextProbe/NativeBlueprintCompletionV2Checks.cs']+=@($leaves['tests/NativeWorldTextProbe/NativeBlueprintWorldMatchChecks.cs'])
    $leaves['tests/NativeWorldTextProbe/NativeBlueprintLibraryWait.cs']+=@('native-BlueprintNearQueueCpu','native-BlueprintSupplyContinuationCpu','native-BlueprintBuildCadenceCpu','native-BlueprintConstructionCpu','native-BlueprintAppearanceCpu','native-BlueprintWiringCpu','native-BlueprintSpecialCpu','native-BlueprintSubstitutionCpu','native-BlueprintSubstitutionLifecycleCpu')
    $leaves['tests/NativeWorldTextProbe/NativeBlueprintLibraryWait.cs']+=@('native-BlueprintHoikSubstitutionCpu')
    foreach($helper in @('WorldMatch','HelperImport','Build')){
        $path='tests/NativeWorldTextProbe/NativeBlueprint'+$helper+'Checks.cs'
        $leaves[$path]=@($leaves[$path])+@('native-BlueprintImportedMaterialsCpu')
    }
    $leaves['tests/NativeWorldTextProbe/NativeBlueprintLibraryWait.cs']+=@('native-BlueprintImportedMaterialsCpu')
    $leaves['tests/NativeWorldTextProbe/NativeLegacyPlacementIdentityChecks.cs']+=@('native-BlueprintImportedMaterialsCpu')
    $leaves['tests/NativeWorldTextProbe/NativeBlueprintExpectationChecks.cs']=@('native-BlueprintNearQueueCpu')
    # A family enters EffectChecks.RunCore and returns from its exact branch.
    # Its own body and called shared scene/Save/Step/solution helpers matter;
    # sibling private assertions (for example Bee) do not execute on that path.
    # NativeChecks/ScopeDispatch/QuickItem/Information remain common inputs.
    $familyHelpers=@{
        'Nail'=@('BallisticCandidate','Observation','Cadence','ControlPreview')
        'Piranha'=@('BallisticCandidate','Observation','Cadence','ControlPreview')
        'Phoenix'=@('Cadence','ControlPreview','Observation','SelectionPreview')
        'SpecialArrow'=@('Observation')
        'AttachedShot'=@('Cadence','ControlPreview','Observation','SelectionPreview')
        'PhantasmEcho'=@('Cadence','Observation')
        'BetsyFlame'=@('Cadence','ControlPreview','Observation','SelectionPreview')
        'ArcSwing'=@('Cadence','ControlPreview','Observation','SelectionPreview')
        'HomingEffect'=@('Cadence','Observation')
        'JoustingLance'=@('Cadence','Observation')
        'WhipStar'=@('Cadence','Observation')
        'Thorn'=@('Cadence','ControlPreview','Observation','SelectionPreview')
    }
    # Initialize and the shared solution helpers physically live in AimFixture;
    # ordinary AimChecks has no family callee or static initialization.
    # The aggregate invokes every family and therefore owns their helper union.
    $familyNames=@($familyHelpers.Keys|ForEach-Object {'native-CombatEffects'+$_+'Cpu'})
    foreach($path in @($leaves.Keys)){if($path.StartsWith('src/') -and $leaves[$path] -contains 'native-CombatEffectsCpu'){$leaves[$path]=@($leaves[$path])+@($familyNames)}}
    foreach($family in $familyHelpers.Keys){
        $name='native-CombatEffects'+$family+'Cpu'
        foreach($helper in @('Effect',$family,'WorkerAsset')+@($familyHelpers[$family])){
            $path='tests/NativeWorldTextProbe/NativeCombat'+$helper+'Checks.cs'
            # A previously shared path keeps its conservative existing route;
            # adding a family must not silently remove its other consumers.
            if($leaves.ContainsKey($path)){$leaves[$path]=@((@($leaves[$path])+@($name,'native-CombatEffectsCpu'))|Sort-Object -Unique)}
        }
        foreach($path in @('tests/NativeWorldTextProbe/NativeCombatAimFixture.cs','tests/NativeWorldTextProbe/NativeToolExecutionChecks.cs','tests/NativeWorldTextProbe/NativeToolsChecks.cs')){
            if($leaves.ContainsKey($path)){$leaves[$path]=@((@($leaves[$path])+@($name,'native-CombatEffectsCpu'))|Sort-Object -Unique)}
        }
    }
    # These existing narrow bodies share the real aim/control/effects fixture.
    # Bind its existing assertions conservatively without executing siblings.
    foreach($path in @($leaves.Keys)){
        if(@($leaves[$path]|Where-Object {$_ -in @('native-CombatAimCpu','native-CombatControlCpu','native-CombatEffectsCpu')}).Count){$leaves[$path]=@($leaves[$path])+@('native-DirectionalControl','native-NpcSelectedContinuation','native-TerrainLine')}
    }
    # These private cases are reached only by the full construction and
    # appearance bodies. They export no shared fixture and have no static
    # initializers; the older shared-partial expansions above do not apply.
    $leaves['tests/NativeWorldTextProbe/NativeBlueprintEmptyReceiptChecks.cs']=@('native-BlueprintConstructionCpu','native-BlueprintAppearanceCpu')
    $leaves['tests/NativeWorldTextProbe/NativeBlueprintSettledPaintAdmissionChecks.cs']=@('native-BlueprintConstructionCpu','native-BlueprintAppearanceCpu')
    $afkInputs=@('src/JueMingR.Features/AfkMode/AfkPreferences.cs','src/JueMingR.TerrariaHost/AfkMode/HostAfkMode.cs','src/JueMingR.TerrariaHost/AfkMode/HostAfkMode.Operation.cs','src/JueMingR.TerrariaHost/AfkMode/AfkNativeHooks.cs','tests/NativeWorldTextProbe/NativeAfkModeChecks.cs')
    foreach($path in $afkInputs){$leaves[$path]=@('native-AfkModeCompletionCpu','native-AfkModeBorrowCpu','native-AfkModeVisual')}
    $leaves['tests/NativeWorldTextProbe/NativeAfkModeChecks.cs']=@('native-AfkModeCompletionCpu','native-AfkModeBorrowCpu')
    $leaves['tests/NativeWorldTextProbe/NativeAfkUiChecks.cs']=@('native-AfkModeVisual')
    # Actual shared native cadence helpers and composition remain inputs to
    # the new leaf; sibling assertion bodies do not become AFK obligations.
    foreach($path in @('tests/NativeWorldTextProbe/NativeCombatCadenceChecks.cs','tests/NativeWorldTextProbe/NativeCombatSelectionPreviewChecks.cs','tests/NativeWorldTextProbe/NativeCombatObservationChecks.cs','tests/NativeWorldTextProbe/NativeCombatBeamChecks.cs','tests/NativeWorldTextProbe/NativeToolExecutionChecks.cs','tests/NativeWorldTextProbe/NativeCombatAimFixture.cs','tests/NativeWorldTextProbe/NativeQuickItemChecks.cs','tests/NativeWorldTextProbe/NativeToolsChecks.cs')){
        $leaves[$path]=@($leaves[$path])+@('native-AfkModeCompletionCpu','native-AfkModeBorrowCpu')
    }
    return $leaves
}
function Get-WorkloadNativeScopes {
    # CPU cumulative entries own Navigation through Control and AttackCosts
    # through CombatCosts. Their independent specialist flags remain available
    # in test-world-object-text. Impact/AimUi/Visual need matching real Content
    # and GPU: they are explicit graphical obligations, never --cpu children.
    $scopes = [ordered]@{
        'AfkModeCompletionCpu'=@('afk-host','combat-host','tools-host','quick-items-host','shared-host');
        'AfkModeBorrowCpu'=@('afk-host','combat-host','tools-host','quick-items-host','shared-host');
        'RuntimeFailureCpu'=@('shared-host','journey-host','quick-items-host');
        'RuntimeFailureActiveCpu'=@('shared-host','journey-host','quick-items-host');
        'RuntimeFailureToolsCpu'=@('shared-host','journey-host','quick-items-host','tools-host');
        'RuntimeFailureQuickCpu'=@('shared-host','journey-host','quick-items-host');
        'RuntimeFailureLoansCpu'=@('shared-host','journey-host','quick-items-host','fishing-host','movement-host');
        'RuntimeFailureWorldCpu'=@('shared-host','journey-host','quick-items-host','fishing-host','movement-host');
        'RuntimeFailureBuildCpu'=@('shared-host','blueprint-host','tools-host');
        'RuntimeFailureBuildManualCpu'=@('shared-host','blueprint-host','tools-host');
        'RuntimeFailureBuildWorldCpu'=@('shared-host','blueprint-host','tools-host');
        'RuntimeFailureBuildReceiptCpu'=@('shared-host','blueprint-host','tools-host');
        'BlueprintNearQueueCpu'=@('blueprint-host','tools-host');
        'RuntimeFailureBuildUsingSelectionCpu'=@('shared-host','blueprint-host','tools-host');
        'RuntimeFailureBuildUsingScrollCpu'=@('shared-host','blueprint-host','tools-host');
        'RuntimeFailureBuildSelectionCpu'=@('shared-host','blueprint-host','tools-host');
        'RuntimeFailureBuildScrollCpu'=@('shared-host','blueprint-host','tools-host');
        'JourneyCloseCpu'=@('journey-host','quick-items-host');
        'TargetGestureDiagnostic'=@('browser-host');
        'WorldGenPreviewCpu'=@('worldgen-preview','shared-host');
        'MovementJumpCpu'=@('movement-host');
        # The rescue leaf directly consumes the fishing overlap owner and its
        # existing native tool/fishing fixture initialization. Bind those inputs
        # for reuse; this adds no execution of either parent feature matrix.
        'MovementSmartFallCpu'=@('movement-host','fishing-host','tools-host');
        'MovementDashCpu'=@('movement-host');
        'MovementTeleportCpu'=@('movement-host');
        'ViewZoomSpeedCpu'=@('view-host');
        'ViewCombatCpu'=@('view-host');
        'ViewRangeConsumerCpu'=@('view-host');
        'JourneyCpu'=@('journey-host'); 'JourneySinglePlayerExit'=@('journey-host');
        'JourneyPredictionCpu'=@('journey-host');
        'NpcRollingCpu'=@('combat-host');
        'CombatAimCpu'=@('combat-host');
        'CombatGuidedNavigationCpu'=@('combat-host','quick-items-host','tools-host');
        'CombatControlCpu'=@('combat-host');
        # Reviewed regression leaves consume this existing full native fixture
        # initialization, without executing its quick-item feature matrix.
        'CombatTerrainKnowledgeCpu'=@('combat-host','quick-items-host');
        'CombatShotFacingCpu'=@('combat-host','quick-items-host','tools-host');
        'CombatShotBirthOrderCpu'=@('combat-host','quick-items-host','tools-host');
        'CombatNailDetachCpu'=@('combat-host','quick-items-host','tools-host');
        'CombatMechanicsCpu'=@('combat-host');
        'CombatEffectsCpu'=@('combat-host');
        'CombatEffectsNailCpu'=@('combat-host','quick-items-host','tools-host');
        'CombatEffectsPiranhaCpu'=@('combat-host','quick-items-host','tools-host');
        'CombatEffectsPhoenixCpu'=@('combat-host','quick-items-host','tools-host');
        'CombatEffectsSpecialArrowCpu'=@('combat-host','quick-items-host','tools-host');
        'CombatEffectsAttachedShotCpu'=@('combat-host','quick-items-host','tools-host');
        'CombatEffectsPhantasmEchoCpu'=@('combat-host','quick-items-host','tools-host');
        'CombatEffectsBetsyFlameCpu'=@('combat-host','quick-items-host','tools-host'); 'CombatEffectsArcSwingCpu'=@('combat-host','quick-items-host','tools-host');
        'CombatEffectsHomingEffectCpu'=@('combat-host','quick-items-host','tools-host'); 'CombatEffectsJoustingLanceCpu'=@('combat-host','quick-items-host','tools-host');
        'CombatEffectsWhipStarCpu'=@('combat-host','quick-items-host','tools-host'); 'CombatEffectsThornCpu'=@('combat-host','quick-items-host','tools-host');
        'DirectionalControl'=@('combat-host','quick-items-host','tools-host'); 'NpcSelectedContinuation'=@('combat-host','quick-items-host','tools-host'); 'TerrainLine'=@('combat-host','quick-items-host','tools-host');
        'CombatIntegrationCpu'=@('combat-host');
        'CombatExperimentsCpu'=@('combat-host','quick-items-host');
        # The narrow bodies use NativeQuickItemChecks' real initialization;
        # bind that fixture owner without executing its feature matrices.
        'CombatInputGeometryCpu'=@('combat-host','quick-items-host');
        'CombatSkyActionCpu'=@('combat-host','quick-items-host');
        'NpcRollingSelectionNegative'=@('combat-host');
        'NpcBasicMotion'=@('combat-host');
        'NpcPhasedFlight'=@('combat-host'); 'NpcAmbientFlight'=@('combat-host'); 'NpcRelatedMotion'=@('combat-host'); 'NpcTerrainFlight'=@('combat-host'); 'NpcUmbrella'=@('combat-host');
        'NpcPlantera'=@('combat-host'); 'NpcBigMimic'=@('combat-host'); 'NpcAquaticActions'=@('combat-host');
        'NpcInvasionFlight'=@('combat-host'); 'NpcInvasionGround'=@('combat-host'); 'NpcEyeBoss'=@('combat-host'); 'NpcSnowman'=@('combat-host');
        'NpcSantaTank'=@('combat-host'); 'NpcKingSlime'=@('combat-host'); 'NpcSkeletonBoss'=@('combat-host');
        'NpcInvasionFlightCpu'=@('combat-host'); 'NpcInvasionFlightLoss'=@('combat-host'); 'NpcBetsy'=@('combat-host'); 'NpcTownCritter'=@('combat-host');
        'NpcInvasionLightning'=@('combat-host'); 'NpcDiveFlight'=@('combat-host'); 'NpcAttachedWeapon'=@('combat-host'); 'NpcDarkMage'=@('combat-host'); 'CombatTerrainCpu'=@('combat-host');
        'NpcBloodNautilus'=@('combat-host'); 'NpcGolem'=@('combat-host');
        'NpcQueenBee'=@('combat-host'); 'NpcBrainSwarm'=@('combat-host');
        'NpcWallBoss'=@('combat-host');
        'NpcTreeBoss'=@('combat-host'); 'NpcCelestialTower'=@('combat-host');
        'NpcPumpking'=@('combat-host'); 'NpcIceQueen'=@('combat-host');
        'NpcCultistBoss'=@('combat-host');
        'NpcCultistRitual'=@('combat-host');
        'NpcMoonLord'=@('combat-host');
        'NpcMoonLeech'=@('combat-host');
        'NpcQueenSlime'=@('combat-host');
        'NpcWindyBalloon'=@('combat-host');
        'NpcBird'=@('combat-host');
        'NpcCaster'=@('combat-host');
        'NpcMartianTurret'=@('combat-host');
        'NpcSegmentedWorm'=@('combat-host');
        'NpcSolitaryWorm'=@('combat-host');
        'NpcSolarWorm'=@('combat-host');
        'NpcEmpress'=@('combat-host');
        'NpcDeerclops'=@('combat-host');
        'NpcFishron'=@('combat-host');
        'NpcFoundationRules'=@('combat-host'); 'NpcFoundationContinuous'=@('combat-host'); 'NpcPlayerPolicy'=@('combat-host'); 'NpcStrategy'=@('combat-host'); 'NpcStrategyContinuous'=@('combat-host'); 'NpcEventRetirementCpu'=@('combat-host');
        # Ordinary delivery retains the real shared selection/terrain and
        # marker consumer seams; detailed phase matrices stay bounded probes.
        'NpcSync'=@('combat-host'); 'NpcLocalFailure'=@('combat-host'); 'NpcDisplayIsolation'=@('combat-host'); 'NpcCloseoutTerrain'=@('combat-host');
        'NpcSharedGeometry'=@('combat-host'); 'NpcTargetMarker'=@('combat-host');
        'NpcSamplePresentation'=@('combat-host'); 'NpcFiniteFlight'=@('combat-host'); 'CombatYoyoCausal'=@('combat-host');
        'NpcWorkerIntegration'=@('legacy-worker'); 'NpcSnapshot'=@('legacy-worker'); 'NpcWorkerPreparation'=@('legacy-worker');
        'NpcReviewedStool'=@('legacy-worker'); 'NpcReviewedParticle'=@('legacy-worker');
        # These existing native worker bodies are task-specific consumers,
        # with their original dispatch/expectations retained as named checks.
        'NpcWorkerContext'=@('legacy-worker'); 'NpcWorkerAssets'=@('legacy-worker'); 'NpcWorkerLifecycle'=@('legacy-worker');
        'NpcDiagnosticsOff'=@('legacy-worker'); 'NpcPostDelivery'=@('legacy-worker'); 'NpcGuardianQuery'=@('legacy-worker'); 'NpcModeledImpact'=@('legacy-worker');
        'NpcLegalCoverage'=@('legacy-worker'); 'NpcWorkerTransport'=@('legacy-worker'); 'NpcMenuPreparation'=@('legacy-worker'); 'NpcSessionCapacity'=@('legacy-worker'); 'NpcProduction'=@('legacy-worker'); 'NpcLongCoverage'=@('legacy-worker');
        'WorkloadCpu'=@('world-host','shared-host'); 'InformationCpu'=@('information','shared-host'); 'GuidanceCpu'=@('guidance','shared-host');
        'ShortFeedbackCpu'=@('shared-host','storage-host','quick-items-host','coin-deposit-host','recovery-host','processing-host','about-host','tools-host','fishing-host','combat-host','blueprint-host');
        'CombatCpu'=@('combat-host'); 'CombatFacingCpu'=@('combat-host'); 'CombatHitsCpu'=@('combat-host'); 'CombatReportCpu'=@('combat-host'); 'CombatUiCpu'=@('combat-host'); 'CombatObservationCpu'=@('combat-host'); 'CombatObservationBoundaryCpu'=@('combat-host'); 'CombatCosts'=@('combat-host');
        'InputBoundary'=@('input-boundary','shared-host');
        'ToolsCpu'=@('tools-host'); 'ToolsCadence'=@('tools-host'); 'ToolsExecutionCpu'=@('tools-host'); 'ToolsWorkload'=@('tools-host');
        'BlueprintSubstitutionCpu'=@('blueprint-host','tools-host','browser-host');
        'BlueprintSubstitutionLifecycleCpu'=@('blueprint-host');
        'BlueprintCpu'=@('blueprint-host','tools-host');
        'BlueprintExchangeCpu'=@('blueprint-host','tools-host','shared-host');
        'BlueprintExchangeFollowupCpu'=@('blueprint-host','shared-host','hotkeys');
        'BlueprintMirrorTrimCpu'=@('blueprint-host','hotkeys','tools-host');
        'BlueprintQualificationCpu'=@('blueprint-host');
        'BlueprintSupplyContinuationCpu'=@('blueprint-host','tools-host');
        'BlueprintConstructionCpu'=@('blueprint-host','tools-host');
        'BlueprintBuildCadenceCpu'=@('blueprint-host','tools-host');
        'BlueprintPreparationCpu'=@('blueprint-host','tools-host');
        'BlueprintStructureCpu'=@('blueprint-host','tools-host');
        'BlueprintPayloadCpu'=@('blueprint-host','tools-host');
        'BlueprintAppearanceCpu'=@('blueprint-host','tools-host');
        'BlueprintHoikCpu'=@('blueprint-host','tools-host');
        'BlueprintHoikSubstitutionCpu'=@('blueprint-host','tools-host');
        'BlueprintImportedMaterialsCpu'=@('blueprint-host','tools-host');
        'ViewZoomControlCpu'=@('view-host','shared-host','hotkeys');
        'BlueprintWiringCpu'=@('blueprint-host','tools-host');
        'BlueprintSpecialCpu'=@('blueprint-host','tools-host');
        'BlueprintInput'=@('blueprint-host','shared-host','storage-host','notes-host','preferences','hotkeys');
        'PageCompositionCpu'=@('pages-host'); 'FishingCpu'=@('fishing-host'); 'FishingAnglerContinuousCpu'=@('fishing-host'); 'CombatNativeInputCpu'=@('combat-host'); 'BackgroundCpu'=@('fishing-host','shared-host'); 'F5AutomationCpu'=@('fishing-host','shared-host');
        'AboutCpu'=@('about-host'); 'BrowserCpu'=@('browser-host'); 'QuickItemsCpu'=@('quick-items-host'); 'CoinDepositCpu'=@('coin-deposit-host');
        'RecoveryCpu'=@('recovery-host'); 'ProcessingCpu'=@('processing-host'); 'DeathCpu'=@('death-host'); 'FootprintsCpu'=@('footprints-host'); 'ExplorationCpu'=@('map-host')
    }
    return $scopes
}
function Get-WorkloadCheckGroups {
    param([string] $Name)
    if ($Name.StartsWith('native-')) {
        $scope=$Name.Substring(7)
        if ($scope -ceq 'NpcStrategy-rollchoice') {return @('combat-host')}
        if ($scope -ceq 'NpcPrivateSafety') {return @('legacy-worker')}
        $scopes=Get-WorkloadNativeScopes
        if ($scopes.Contains($scope)) {return $scopes[$scope]}
    }
    if ($script:WorkloadCheckGroups -and $script:WorkloadCheckGroups.ContainsKey($Name)) {return $script:WorkloadCheckGroups[$Name]}
    return @('*')
}
function Get-WorkloadPathChecks {
    param([string] $Path)
    # The definition is immutable for this loaded script. Fingerprinting asks
    # for each path/check pair; rebuilding the full map there adds no evidence.
    if($null -eq $script:WorkloadLeafLookup){$script:WorkloadLeafLookup=Get-WorkloadLeafChecks}
    $leaves=$script:WorkloadLeafLookup
    if ($leaves.ContainsKey($Path)) {return $leaves[$Path]}
    return @()
}
$script:WorkloadLeafLookup=$null
function Get-WorkloadCheckDependencies {
    param([string] $Name)
    # The private-image safety child consumes the authenticated layout produced
    # by Integration. Other registered checks run in fresh processes/outputs.
    if ($Name -ceq 'native-NpcPrivateSafety') { return @('native-NpcWorkerIntegration') }
    return @()
}
$script:WorkloadCheckGroups=@{}
function Get-WorkloadAdditionalCheck {
    param([string] $Root,[string] $Name)
    # Existing explicit strategy sub-scope remains available for a task's
    # reviewed delta proof; its registration is not a universal obligation.
    if ($Name -cne 'native-NpcStrategy-rollchoice') {throw ('Unknown additional check: '+$Name)}
    $checks=Join-Path $Root 'artifacts/build/Debug/checks'
    return [pscustomobject]@{name=$Name;executable=(Join-Path $checks 'bin/NativeWorldTextProbe/x86/Debug/net472/NativeWorldTextProbe.exe');arguments=@($Root,'--cpu',(Join-Path $checks 'NpcStrategy-rollchoice'),'NpcStrategy:rollchoice');project='NativeWorldTextProbe'}
}
function Get-WorkloadQualificationPolicy {
    param([string] $Profile,[string] $Name,[string[]] $Differences)
    # Reviewed task delta proof is explicit. It cannot become a universal
    # strategy obligation, or authorize unrelated changed-input conclusions.
    if($Profile -ceq 'issue112-lifetime-delta-20261008' -and $Name -ceq 'native-NpcStrategy'){
        # These exact assertion revisions were covered by the reviewed lifetime
        # sub-scope. A pathname/profile label alone cannot excuse new assertions
        # or changed production/dispatch/compile/environment inputs.
        $allowed=@(
            'tests/NativeWorldTextProbe/NativeCombatRollingControlChecks.cs|85DEEE19205C8243CE39DDE9E187468820BFB98E537719B38A77E9BE8D90C828|16ECAAD59329232199E93D157D1E63473F4455331F72C614AF4EC0C51C130EE0',
            'tests/NativeWorldTextProbe/NativeNpcLifetimeChecks.cs|<absent>|5E15817C5FD2E9FC1ADB3BFC6587CE6BE0FF173FE59C84A81B54283111DE069E')
        if(@($Differences).Count -eq 0 -or @($Differences|Where-Object {$allowed -cnotcontains $_}).Count){return $null}
        return [pscustomobject]@{requiresCurrent=@('native-NpcStrategy-rollchoice','native-NpcStrategyContinuous','native-NpcRollingCpu','native-NpcRollingSelectionNegative')}
    }
    return $null
}
$script:WorkloadBehaviorRecipeCache=@{}
function Get-WorkloadBehaviorRecipeProjection {
    param([string] $Path,[string] $Text)
    # Bind the complete three compile/launch owners, including control flow,
    # helpers and executable bookkeeping. Unknown executable changes require
    # current validation; this is a token boundary, not a script interpreter.
    if($Path -inotin @('scripts/build.ps1','scripts/workload/Workload.Evidence.ps1','scripts/test-workload-regressions.ps1')){throw 'Unregistered behavior recipe.'}
    $tokens=$null;$errors=$null
    $ast=[Management.Automation.Language.Parser]::ParseInput($Text,[ref]$tokens,[ref]$errors)
    if($errors.Count){throw ('Unparseable behavior recipe: '+$Path)}
    $ignored=New-Object 'System.Collections.Generic.List[object]'
    # These functions classify/select immutable inputs or explain a plan. They
    # do not prepare/launch/qualify a child. Original receipts still have to
    # satisfy their original projection before the current dependency check.
    # Every other function/control-flow token remains execution-contract input.
    if($Path -ieq 'scripts/workload/Workload.Evidence.ps1'){
        $selectionOnly=@('Get-WorkloadEvidenceInput','Get-WorkloadCheckInputs','Get-WorkloadCheckFingerprint','Get-WorkloadPlan')
        foreach($function in $ast.FindAll({param($node)$node -is [Management.Automation.Language.FunctionDefinitionAst]},$true)){
            if($selectionOnly -ccontains $function.Name){$ignored.Add($function.Extent)}
        }
    }
    # Even literal Write-Host resets $?. Only build's appended, top-level
    # reports after its existing final successful record output are exempt;
    # there is no later executable statement to consume their status. Other
    # reporting stays bound, without trying to interpret status data flow.
    $statements=@(if($Path -ieq 'scripts/build.ps1' -and $null -ne $ast.EndBlock){$ast.EndBlock.Statements})
    for($index=$statements.Count-1;$index -ge 0;$index--){
        $pipeline=$statements[$index]
        if($pipeline -isnot [Management.Automation.Language.PipelineAst] -or $pipeline.PipelineElements.Count -ne 1){break}
        $call=$pipeline.PipelineElements[0]
        if($call -isnot [Management.Automation.Language.CommandAst] -or $call.InvocationOperator -ne [Management.Automation.Language.TokenKind]::Unknown -or $call.Redirections.Count -ne 0 -or $call.GetCommandName() -ine 'Write-Host'){break}
        $constants=@($call.CommandElements | Select-Object -Skip 1)
        if($constants.Count -eq 0 -or @($constants | Where-Object {$_ -isnot [Management.Automation.Language.StringConstantExpressionAst] -and $_ -isnot [Management.Automation.Language.ConstantExpressionAst]}).Count){break}
        $ignored.Add($pipeline.Extent)
    }
    if($Path -ieq 'scripts/build.ps1' -and ($index -lt 0 -or $statements[$index].Extent.Text -cne 'Write-Output ("Build record: {0}" -f $recordPath)')){$ignored.Clear()}
    $events=New-Object 'System.Collections.Generic.List[object]'
    foreach($token in $tokens){
        if($token.Kind -in @('NewLine','LineContinuation','Semi','EndOfInput')){continue}
        # #requires is lexed as a comment but constrains script execution.
        if($token.Kind -eq 'Comment' -and $token.Text -inotmatch '^#requires\b'){continue}
        $skip=$false
        foreach($range in $ignored){if($token.Extent.StartOffset -ge $range.StartOffset -and $token.Extent.EndOffset -le $range.EndOffset){$skip=$true;break}}
        if($skip){continue}
        $events.Add([pscustomobject]@{offset=$token.Extent.StartOffset;edge=1;value=$token.Kind.ToString()+':'+$token.Text})
    }
    # Newlines/semicolons can change statements without changing other tokens.
    # Keep parser statement/command boundaries while permitting mere layout.
    foreach($node in $ast.FindAll({param($child) $child -is [Management.Automation.Language.StatementAst] -or $child -is [Management.Automation.Language.CommandAst]},$true)){
        $skip=$false
        foreach($range in $ignored){if($node.Extent.StartOffset -ge $range.StartOffset -and $node.Extent.EndOffset -le $range.EndOffset){$skip=$true;break}}
        if($skip){continue}
        $kind=$node.GetType().Name
        $events.Add([pscustomobject]@{offset=$node.Extent.StartOffset;edge=0;value='begin:'+ $kind})
        $events.Add([pscustomobject]@{offset=$node.Extent.EndOffset;edge=2;value='end:'+ $kind})
    }
    return Get-WorkloadHash @($events | Sort-Object offset,edge,value | ForEach-Object {$_.value})
}
function Read-WorkloadOriginalRecipe {
    param([string] $Root,$Evidence,[string] $Path,[string] $Hash)
    $current=Join-Path $Root $Path
    if([IO.File]::Exists($current) -and (Get-WorkloadFileHash $current).Hash -ceq $Hash){return [IO.File]::ReadAllText($current)}
    if($null -eq $Evidence.PSObject.Properties['sourceCommit'] -or $Evidence.sourceCommit -cnotmatch '^[0-9a-f]{40}$'){throw 'Original recipe bytes unavailable.'}
    # Git blobs and Git's actual checkout filter are candidate byte sources.
    # Accept only the recorded SHA, never newline-normalised semantic guessing.
    foreach($mode in @('blob','--filters')) {
        $start=New-Object Diagnostics.ProcessStartInfo
        $start.FileName=(Get-Command git.exe).Source;$start.Arguments='-C "'+$Root+'" cat-file '+$mode+' '+$Evidence.sourceCommit+':'+$Path
        $start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
        $child=New-Object Diagnostics.Process;$child.StartInfo=$start;$bytes=New-Object IO.MemoryStream
        try {
            [void]$child.Start();$stderr=$child.StandardError.ReadToEndAsync();$child.StandardOutput.BaseStream.CopyTo($bytes);$child.WaitForExit();$null=$stderr.GetAwaiter().GetResult()
            if($child.ExitCode -ne 0){continue}
            $sha=[Security.Cryptography.SHA256]::Create()
            try{$actual=[BitConverter]::ToString($sha.ComputeHash($bytes.ToArray())).Replace('-','')}finally{$sha.Dispose()}
            if($actual -ceq $Hash){$bytes.Position=0;$reader=New-Object IO.StreamReader($bytes,[Text.Encoding]::UTF8,$true);try{return $reader.ReadToEnd()}finally{$reader.Dispose()}}
        } finally {$bytes.Dispose();$child.Dispose()}
    }
    throw 'Original recipe bytes do not match recorded SHA; current validation required.'
}
function Test-WorkloadBehaviorRecipes {
    param([string] $Root,$Evidence,$InputIdentity,[string] $Name)
    if($Name.StartsWith('workload-')){return $true}
    try {
        foreach($path in @('scripts/build.ps1','scripts/workload/Workload.Evidence.ps1','scripts/test-workload-regressions.ps1')){
            $prefix=$path+':'
            $old=@($Evidence.inputs|Where-Object {$_.StartsWith($prefix,[StringComparison]::Ordinal)})
            $current=@($InputIdentity.inputs|Where-Object {$_.StartsWith($prefix,[StringComparison]::Ordinal)})
            if($old.Count -eq 0 -and $current.Count -eq 0){continue}
            if($old.Count -ne 1 -or $current.Count -ne 1){return $false}
            $oldHash=$old[0].Substring($prefix.Length);$currentHash=$current[0].Substring($prefix.Length)
            # Cache only projections of exact, already verified immutable bytes.
            # Live/source identity is still re-read at the existing boundaries.
            $oldKey=$path+'|'+$oldHash;$currentKey=$path+'|'+$currentHash
            if(-not $script:WorkloadBehaviorRecipeCache.ContainsKey($oldKey)){$script:WorkloadBehaviorRecipeCache[$oldKey]=Get-WorkloadBehaviorRecipeProjection $path (Read-WorkloadOriginalRecipe $Root $Evidence $path $oldHash)}
            if(-not $script:WorkloadBehaviorRecipeCache.ContainsKey($currentKey)){$script:WorkloadBehaviorRecipeCache[$currentKey]=Get-WorkloadBehaviorRecipeProjection $path (Read-WorkloadOriginalRecipe $Root $InputIdentity $path $currentHash)}
            $before=$script:WorkloadBehaviorRecipeCache[$oldKey];$after=$script:WorkloadBehaviorRecipeCache[$currentKey]
            if($before -cne $after){return $false}
        }
        return $true
    } catch {return $false}
}
function Get-WorkloadLegacyCheckFingerprint {
    param($InputIdentity, [string] $Name)
    # Before the two private leaves and shared Quick fixture were corrected,
    # their old combat-host/Quick shadow routes defined the original projection.
    # Validate that exact
    # recorded fingerprint in original input order; current reuse still compares
    # the precise map and never exempts an actual consumer's changed input.
    if($null -ne $InputIdentity.PSObject.Properties['inputFingerprint']){
        $groups=@(Get-WorkloadCheckGroups $Name)
        $precise=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
        foreach($row in @(Get-WorkloadCheckInputs $InputIdentity $Name)){[void]$precise.Add([string]$row)}
        $beforePrivateLeaves=@($InputIdentity.inputs | Where-Object {
            $path=($_ -split ':',2)[0]
            if($path -cin @('tests/NativeWorldTextProbe/NativeCombatSyncChecks.cs','tests/NativeWorldTextProbe/NativeCombatCostChecks.cs')){$groups -contains '*' -or $groups -contains 'combat-host'}
            elseif($path -ceq 'tests/NativeWorldTextProbe/NativeQuickItemChecks.cs'){$groups -contains '*' -or @($groups|Where-Object {$_ -cin @('quick-items-host','recovery-host','coin-deposit-host','hotkeys')}).Count -gt 0}
            else{$precise.Contains([string]$_)}
        })
        $beforePrivateFingerprint=Get-WorkloadHash $beforePrivateLeaves
        if($InputIdentity.inputFingerprint -ceq $beforePrivateFingerprint){return $beforePrivateFingerprint}
    }
    $legacyAimConsumers=@('native-CombatAimCpu','native-CombatControlCpu','native-CombatNavigationCpu','native-CombatMechanicsCpu','native-CombatEffectsCpu','native-CombatIntegrationCpu','native-CombatCosts','native-CombatAttackCosts','native-CombatImpactVisual','native-CombatAimUi','native-CombatVisual')
    # Before the three attack owners became leaves they were combat-host
    # grouped inputs. Verify that immutable original projection only against
    # its recorded fingerprint; current candidate comparison always uses the
    # precise map in Get-WorkloadCheckFingerprint. Never rewrite old receipts
    # or use this compatibility projection to exempt another changed input.
    if($null -ne $InputIdentity.PSObject.Properties['inputFingerprint']){
        $groups=@(Get-WorkloadCheckGroups $Name)
        $groupedLeaves=Get-WorkloadLeafChecks
        $groupedLeaves['tests/NativeWorldTextProbe/NativeCombatAimChecks.cs']=$legacyAimConsumers
        foreach($path in @('src/JueMingR.TerrariaHost/Combat/HostAttackControl.cs','src/JueMingR.TerrariaHost/Combat/HostAttackWindow.cs','src/JueMingR.TerrariaHost/Combat/HostYoyoNavigation.cs')){$groupedLeaves.Remove($path)}
        $groupedRows=@($InputIdentity.inputs | Where-Object {
            $path=($_ -split ':',2)[0]
            if($groupedLeaves.ContainsKey($path)){$groupedLeaves[$path] -contains $Name}
            elseif($path -in @('src/JueMingR.TerrariaHost/Combat/HostAttackControl.cs','src/JueMingR.TerrariaHost/Combat/HostAttackWindow.cs','src/JueMingR.TerrariaHost/Combat/HostYoyoNavigation.cs')){$groups -contains '*' -or $groups -contains 'combat-host'}
            elseif($path -match '^scripts/workload/|^scripts/(build|test-workload-regressions|test-world-object-text)\.ps1$|^tests/Workload/'){$Name.StartsWith('workload-')}
            elseif($groups -contains '*' -or $path -notmatch '^(src/|tests/|scripts/)'){$true}
            else{
                $route=Get-WorkloadRoute @($path)
                $route.unknown.Count -gt 0 -or @($route.groups | Where-Object {$_ -ne 'core' -and $groups -contains $_}).Count -gt 0
            }
        })
        $groupedFingerprint=Get-WorkloadHash $groupedRows
        if($InputIdentity.inputFingerprint -ceq $groupedFingerprint){return $groupedFingerprint}
    }
    # Immutable pre-DT common projection included all production, including
    # the three owners now mapped above. Keep those original bytes bound here.
    # Only reviewed leaf assertion files may be omitted from another CPU check.
    # NativeChecks/CheckCatalog, shared helpers, projects, all production and all
    # recipes remain common inputs. New consumers must update this map in the
    # same change (the dispatcher change itself invalidates all old evidence).
    # Page composition is also used by ToolsVisual, outside this CPU cache.
    $leaves=Get-WorkloadLeafChecks
    $leaves['tests/NativeWorldTextProbe/NativeCombatAimChecks.cs']=$legacyAimConsumers
    foreach($path in @('src/JueMingR.TerrariaHost/Combat/HostAttackControl.cs','src/JueMingR.TerrariaHost/Combat/HostAttackWindow.cs','src/JueMingR.TerrariaHost/Combat/HostYoyoNavigation.cs')){$leaves.Remove($path)}
    # Immutable pre-DT projection compatibility: this helper used to omit the
    # vertical assertion shared by the continuous scopes. This does not grant
    # reuse across that input changing; current projection still binds it.
    $leaves['tests/NativeWorldTextProbe/NativeCombatFoundationChecks.cs']=@('native-NpcFoundationRules')
    $leaves['tests/NativeWorldTextProbe/NativeCombatFoundationContinuousChecks.cs']=@('native-NpcFoundationContinuous','native-NpcStrategyContinuous')
    $rows = @($InputIdentity.inputs | Where-Object {
        $path = ($_ -split ':',2)[0]
        -not $leaves.ContainsKey($path) -or $leaves[$path] -contains $Name
    })
    return Get-WorkloadHash $rows
}
