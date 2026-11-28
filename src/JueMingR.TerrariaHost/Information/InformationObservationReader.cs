using System;
using System.Threading;
using JueMingR.Platform.Information;
using Terraria;
using Terraria.ID;
using Terraria.Localization;

namespace JueMingR.TerrariaHost.Information
{
    internal enum QuestTextStatus { NotReady, Ready, MissingKey, UnsupportedFormat, ReadFailed }
    internal sealed class InformationObservationReader
    {
        private readonly InformationReadiness readiness;
        private readonly Npcs.NativeNpcObservation nativeNpcs;
        private int localizedItem = -1;
        private object culture;
        private string questName, questLocation;
        private string parsedText, parsedLocation;
        private bool hasParsedText, retryRead, attached;
        private int resourceGeneration, attemptedGeneration = -1, readFailures;
        internal QuestTextStatus NameStatus { get; private set; }
        internal QuestTextStatus LocationStatus { get; private set; }
        private long epoch = -1;
#if DEBUG
        internal int NpcQueries { get; private set; }
        internal int NpcVisits { get; private set; }
        internal int ScalarSamples { get; private set; }
        internal int LocalizationReads { get; private set; }
        internal int LocationParses { get; private set; }
#endif
        internal InformationObservationReader(InformationReadiness readiness, Npcs.NativeNpcObservation nativeNpcs = null) { this.readiness = readiness; this.nativeNpcs = nativeNpcs; }
        internal long NativeEpoch { get { return readiness.Snapshot().Epoch; } }
        internal void Attach() { if (!attached) { LanguageManager.Instance.OnLanguageChanged += ResourcesChanged; attached = true; } }
        internal void Detach() { if (attached) { LanguageManager.Instance.OnLanguageChanged -= ResourcesChanged; attached = false; } Clear(); }
        // T8 raises this after native, pack and copied texts finish reloading,
        // including same-culture UseSources/HotReload. No resource reads here.
        private void ResourcesChanged(LanguageManager manager) { Interlocked.Increment(ref resourceGeneration); }
        internal void Clear()
        {
            localizedItem = -1; culture = null; questName = questLocation = null; epoch = -1;
            parsedText = parsedLocation = null; hasParsedText = retryRead = false;
            attemptedGeneration = -1; readFailures = 0; NameStatus = LocationStatus = QuestTextStatus.NotReady;
        }
        internal void Update(HostInformation host)
        {
            var settings = host.Preferences.Value;
            if (!settings.AnySummaryEnabled) return;
            var state = readiness.Snapshot();
            if (epoch != state.Epoch) { Clear(); epoch = state.Epoch; host.Adjustment.Cancel(); host.Pointer.Invalidate(); host.ClearContent(); }
            bool wantInfection = settings.Enabled(InformationKind.Infection), wantLuck = settings.Enabled(InformationKind.Luck), wantAngler = settings.Enabled(InformationKind.Angler);
            var player = Main.LocalPlayer;
            if (Main.gameMenu || Main.dedServ || Main.netMode != 0 && Main.netMode != 1 || player == null || !player.active)
            { host.ClearContent(); return; }
            bool dryad = false, wizard = wantLuck && NPC.savedWizard, angler = wantAngler && NPC.savedAngler;
            bool qualificationReadable = true;
            // One demand-limited table traversal. No savedDryad fiction, NPC
            // name scan, history cache or dependency on entity-label settings.
            if (wantInfection || wantLuck && !wizard || wantAngler && !angler)
            {
#if DEBUG
                NpcQueries++;
#endif
                NPC[] npcs = Main.npc;
                // A missing/incomplete native table is not proof of absence.
                // Positive saved/current facts remain independently sufficient.
                qualificationReadable = npcs != null && npcs.Length >= 200;
                if (npcs != null)
                    for (int i = 0; i < Math.Min(200, npcs.Length); i++)
                    {
#if DEBUG
                        NpcVisits++;
#endif
                        int type;
                        if (nativeNpcs == null) { var npc = npcs[i]; if (npc == null || !npc.active) continue; type = npc.type; }
                        else { Platform.Guidance.GuidanceNpc basic; if (!nativeNpcs.TryRead(i, Platform.Guidance.NpcDemand.Basic, out basic) || !basic.Active) continue; type = basic.Type; }
                        if (wantInfection && type == NPCID.Dryad) dryad = true;
                        if (wantLuck && type == NPCID.Wizard) wizard = true;
                        if (wantAngler && type == NPCID.Angler) angler = true;
                        if ((!wantInfection || dryad) && (!wantLuck || wizard) && (!wantAngler || angler)) break;
                    }
            }
            if (wantInfection)
            {
                // Infection requires a currently active Dryad, never saved
                // history. A readable absence has the same meaning on clients.
                var availability = !dryad ? MissingQualification(qualificationReadable, false) : !state.Available ? InformationAvailability.Unavailable : !state.Infection ? InformationAvailability.Waiting : InformationAvailability.Ready;
                host.Infection.Update(new InfectionObservation { Availability = availability,
                    Hallow = availability == InformationAvailability.Ready ? (int?)WorldGen.tGood : null,
                    Corruption = availability == InformationAvailability.Ready ? (int?)WorldGen.tEvil : null,
                    Crimson = availability == InformationAvailability.Ready ? (int?)WorldGen.tBlood : null });
            }
            if (wantLuck)
            {
                var sample = new LuckObservation { Availability = wizard ? InformationAvailability.Ready : MissingQualification(qualificationReadable) };
                if (wizard)
                {
#if DEBUG
                    ScalarSamples++;
#endif
                    sample.Total = player.luck; sample.LadybugTime = player.ladyBugLuckTimeLeft; sample.Torch = player.torchLuck;
                    sample.Potion = player.luckPotion; sample.Kite = player.kiteLuckLevel; sample.Pearl = player.usedGalaxyPearl;
                    sample.Lantern = Terraria.GameContent.Events.LanternNight.LanternsUp; sample.Gnome = player.HasGardenGnomeNearby;
                    sample.Stinky = player.stinky; sample.Equipment = player.equipmentBasedLuckBonus; sample.Coin = player.coinLuck; sample.Mirror = player.brokenMirrorBadLuck;
                }
                host.Luck.Update(sample);
            }
            if (wantAngler)
            {
                var sample = new AnglerObservation { Availability = !angler ? MissingQualification(qualificationReadable) : !state.Available ? InformationAvailability.Unavailable : !state.Quest || !state.Today ? InformationAvailability.Waiting : InformationAvailability.Ready,
                    Completed = player.anglerQuestsFinished >= 0 ? (int?)player.anglerQuestsFinished : null };
                if (angler)
                {
#if DEBUG
                    ScalarSamples++;
#endif
                    sample.SubmittedToday = state.Today ? (bool?)Main.anglerQuestFinished : null;
                    int index = Main.anglerQuest; var mapping = Main.anglerQuestItemNetIDs;
                    if (state.Quest && mapping != null && index >= 0 && index < mapping.Length && mapping[index] > 0 && mapping[index] < ItemID.Count)
                    {
                        sample.ItemType = mapping[index]; Localize(mapping[index]); sample.Name = questName; sample.Location = questLocation;
                    }
                }
                host.Angler.Update(sample);
            }
        }
        private static InformationAvailability MissingQualification(bool readable, bool needsHistory = true)
        { return !readable ? InformationAvailability.Unavailable : needsHistory && Main.netMode == 1 ? InformationAvailability.Waiting : InformationAvailability.ConditionUnmet; }
        private void Localize(int item)
        {
            object currentCulture = Language.ActiveCulture;
            int generation = Volatile.Read(ref resourceGeneration);
            bool identityChanged = localizedItem != item || !ReferenceEquals(culture, currentCulture);
            bool newInput = identityChanged || attemptedGeneration != generation;
            if (!newInput && !retryRead) return;
            if (identityChanged) hasParsedText = false;
            if (newInput) readFailures = 0;
            localizedItem = item; culture = currentCulture; attemptedGeneration = generation; retryRead = false;
            questName = questLocation = null; NameStatus = LocationStatus = QuestTextStatus.NotReady;
#if DEBUG
            LocalizationReads++;
#endif
            try
            {
                string internalName = ItemID.Search.GetName(item);
                if (String.IsNullOrEmpty(internalName)) return;
                string nameKey = "ItemName." + internalName;
                string name = Language.GetText(nameKey).UnformattedValue;
                NameStatus = TextStatus(name, nameKey);
                if (NameStatus == QuestTextStatus.Ready) questName = name;
                string key = "AnglerQuestText.Quest_" + internalName;
                // Only the terminal location annotation is needed. Value would expand
                // narrative NPC-name variables and perform unrelated searches.
                string value = Language.GetText(key).UnformattedValue;
                LocationStatus = TextStatus(value, key);
                if (LocationStatus != QuestTextStatus.Ready) return;
                // LocalizedText may be reused and SetValue may change its raw
                // string. Compare content only after the real completion event.
                if (!hasParsedText || !String.Equals(parsedText, value, StringComparison.Ordinal))
                {
#if DEBUG
                    LocationParses++;
#endif
                    parsedLocation = AnglerQuestLocationText.Parse(value);
                    parsedText = value; hasParsedText = true;
                }
                questLocation = parsedLocation;
                if (questLocation == null) LocationStatus = QuestTextStatus.UnsupportedFormat;
            }
            catch
            {
                if (questName == null) NameStatus = QuestTextStatus.ReadFailed;
                LocationStatus = QuestTextStatus.ReadFailed; questLocation = null;
                // One isolated read failure gets one subsequent demand attempt.
                // Persistent failure waits for a real resource/key change, with
                // no timer, repeated exceptions or per-frame localization.
                retryRead = ++readFailures == 1;
            }
        }
        private static QuestTextStatus TextStatus(string value, string key)
        { return value == key ? QuestTextStatus.MissingKey : String.IsNullOrWhiteSpace(value) ? QuestTextStatus.NotReady : QuestTextStatus.Ready; }
    }
}
