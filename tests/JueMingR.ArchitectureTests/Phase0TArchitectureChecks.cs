using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace JueMingR.ArchitectureTests
{
    internal static class Phase0TArchitectureChecks
    {
        internal static void Check(IList<string> failures)
        {
            Assembly features = Assembly.Load("JueMingR.Features");
            var expected = new HashSet<string>(StringComparer.Ordinal)
            {
                "JueMingR.Features.CoinDeposit.CoinIntent", "JueMingR.Features.CoinDeposit.CoinRules",
                "JueMingR.Features.CoinDeposit.CoinOutcome", "JueMingR.Features.CoinDeposit.CoinPreferenceCodec", "JueMingR.Features.CoinDeposit.CoinSettings",
                "JueMingR.Features.Onboarding.OnboardingState", "JueMingR.Features.Onboarding.OnboardingMarkerCodec",
                "JueMingR.Features.KeepFavorited.FavoriteIntent", "JueMingR.Features.KeepFavorited.FavoriteClaim",
                "JueMingR.Features.QuickItems.QuickItemMode", "JueMingR.Features.QuickItems.QuickItemEntry",
                "JueMingR.Features.QuickItems.QuickItemCandidate", "JueMingR.Features.QuickItems.QuickItemChoice",
                "JueMingR.Features.QuickItems.QuickItemRules", "JueMingR.Features.QuickItems.QuickItemDocument",
                "JueMingR.Features.QuickItems.QuickItemSettings",
                "JueMingR.Features.ItemBrowser.BrowserCatalog", "JueMingR.Features.ItemBrowser.RelationIndex", "JueMingR.Features.ItemBrowser.BrowserWorkspace",
                "JueMingR.Features.ChestLocator.ChestKnowledge",
                "JueMingR.Features.Announcements.AnnouncementCooldown", "JueMingR.Features.Announcements.AnnouncementSettings",
                "JueMingR.Features.Announcements.AnnouncementCodec", "JueMingR.Features.Announcements.SafeChatText",
                "JueMingR.Features.Footprints.FootprintArchive", "JueMingR.Features.Footprints.FootprintRecorder",
                "JueMingR.Features.Footprints.FootprintPlayback", "JueMingR.Features.Footprints.FootprintClearConfirmation",
                "JueMingR.Features.Footprints.FootprintStore", "JueMingR.Features.Footprints.FootprintStoreState",
                "JueMingR.Features.Footprints.FootprintPreferences", "JueMingR.Features.Footprints.FootprintPreferenceCodec",
                "JueMingR.Features.DeathHistory.DeathArchive", "JueMingR.Features.DeathHistory.DeathHistory",
                "JueMingR.Features.DeathHistory.DeathDisplayPreferences", "JueMingR.Features.DeathHistory.DeathDisplayCodec",
                "JueMingR.Features.WorldTime.WorldTimeHistory",
                "JueMingR.Features.Text.TextElements", "JueMingR.Features.Text.TextEditBuffer",
                "JueMingR.Features.MapMarkers.MarkerName", "JueMingR.Features.MapMarkers.MarkerRecord", "JueMingR.Features.MapMarkers.MarkerDocument",
                "JueMingR.Features.MapMarkers.MarkerCodec", "JueMingR.Features.MapMarkers.MarkerLibrary", "JueMingR.Features.MapMarkers.MarkerPreferenceCodec",
                "JueMingR.Features.MapMarkers.MarkerWorkspace",
                "JueMingR.Features.Exploration.ExplorationCounter", "JueMingR.Features.Exploration.ExplorationSummary", "JueMingR.Features.Exploration.ExplorationHistory", "JueMingR.Features.Exploration.ExplorationPreferenceCodec",
                "JueMingR.Features.Information.InformationPreferences", "JueMingR.Features.Information.InformationStyle",
                "JueMingR.Features.Information.InformationPreferenceCodec", "JueMingR.Features.Information.InformationText",
                "JueMingR.Features.Guidance.RareCreatureDirection", "JueMingR.Features.Guidance.TravellingMerchantDirection", "JueMingR.Features.Guidance.MerchantLocation",
                "JueMingR.Features.Guidance.EquipmentRules", "JueMingR.Features.Guidance.EquipmentWarning", "JueMingR.Features.Guidance.MerchantTestFeature",
                "JueMingR.Features.Guidance.GuidanceKind", "JueMingR.Features.Guidance.GuidancePreferences", "JueMingR.Features.Guidance.GuidancePreferenceCodec", "JueMingR.Features.Guidance.GuidanceStyle",
                "JueMingR.Features.Guidance.DirectionPose", "JueMingR.Features.Guidance.DirectionProjection",
                "JueMingR.Features.Information.InfectionSummary", "JueMingR.Features.Information.AnglerSummary", "JueMingR.Features.Information.LuckSummary",
                "JueMingR.Features.WorldObjectText.WorldObjectResolver", "JueMingR.Features.WorldObjectText.WorldObjectSelection",
                "JueMingR.Features.WorldObjectText.WorldObjectSettings", "JueMingR.Features.WorldObjectText.WorldObjectStyle", "JueMingR.Features.WorldObjectText.WorldObjectCodec",
                "JueMingR.Features.Text.VisibleTextBoundary", "JueMingR.Features.WorldObjectText.WorldTextCursor",
                "JueMingR.Features.WorldObjectText.WorldTextPart", "JueMingR.Features.WorldObjectText.WorldTextElement", "JueMingR.Features.WorldObjectText.WorldTextStep",
                "JueMingR.Features.WorldObjectText.OpenedPositionIndex", "JueMingR.Features.WorldObjectText.OpenedPositionCodec",
                "JueMingR.Features.WorldObjectText.OpenedPositionHistory",
                "JueMingR.Features.WorldObjectText.OpenedPositionQuery", "JueMingR.Features.WorldObjectText.OpenedQueryStep",
                "JueMingR.Features.WorldObjectText.WorldObjectDiscovery", "JueMingR.Features.WorldObjectText.WorldObjectTextCandidate",
                "JueMingR.Features.WorldTargets.WorldTargetSettings", "JueMingR.Features.WorldTargets.WorldTargetCodec",
                "JueMingR.Features.WorldTargets.WorldTargetFeature", "JueMingR.Features.WorldTargets.WorldTarget",
                "JueMingR.Features.WorldTargets.WorldTargetArrows", "JueMingR.Features.WorldTargets.WorldTargetAnimation", "JueMingR.Features.WorldTargets.ArrowPose",
                "JueMingR.Features.Biomes.BiomeDisplayFeature",
                "JueMingR.Features.Biomes.BiomeDisplayViewModel",
                "JueMingR.Features.Biomes.BiomePreferenceCodec",
                "JueMingR.Features.Items.ItemAutomationSettings", "JueMingR.Features.Items.ItemAutomationCodec",
                "JueMingR.Features.Items.ItemActionKind", "JueMingR.Features.Items.ItemListKind",
                "JueMingR.Features.Items.ItemAutomationFeature",
                "JueMingR.Features.EntityLabels.EntityLabelKind", "JueMingR.Features.EntityLabels.NpcLabelMode",
                "JueMingR.Features.EntityLabels.EntityLabelStyle", "JueMingR.Features.EntityLabels.EntityLabelSettings",
                "JueMingR.Features.EntityLabels.EntityLabelCodec",
                "JueMingR.Features.EntityLabels.EntityLabelFeature", "JueMingR.Features.EntityLabels.EntityLabel",
                "JueMingR.Features.Notes.Note", "JueMingR.Features.Notes.NoteReading", "JueMingR.Features.Notes.Notebook",
                "JueMingR.Features.Notes.NotebookCodec", "JueMingR.Features.Notes.TextElements",
                "JueMingR.Features.Notes.NoteEditor", "JueMingR.Features.Notes.NotesTextLayout",
                "JueMingR.Features.Notes.NotesTextLine", "JueMingR.Features.Notes.NotesFeature",
                "JueMingR.Features.Notes.NotesWorkspace", "JueMingR.Features.Notes.NotesAction",
                "JueMingR.Features.Notes.NotesActionKind"
            };
            if (!expected.SetEquals(features.GetExportedTypes().Select(type => type.FullName)))
            {
                failures.Add("Features must export exactly the registered feature contracts.");
            }

            string[] forbiddenAssemblyPrefixes =
            {
                "Terraria",
                "ReLogic",
                "Microsoft.Xna.Framework",
                "0Harmony"
            };
            foreach (Assembly assembly in new[] { typeof(JueMingR.Platform.Operations.IGameOperationRequest).Assembly, features })
            {
                if (assembly.GetReferencedAssemblies().Any(reference =>
                    forbiddenAssemblyPrefixes.Any(prefix =>
                        reference.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))))
                {
                    failures.Add(assembly.GetName().Name + " must remain host-neutral.");
                }
            }
        }
    }
}
