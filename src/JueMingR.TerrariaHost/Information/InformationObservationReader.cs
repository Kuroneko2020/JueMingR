using System;
using JueMingR.Platform.Information;
using Terraria;
using Terraria.ID;
using Terraria.Localization;

namespace JueMingR.TerrariaHost.Information
{
    internal sealed class InformationObservationReader
    {
        private readonly InformationReadiness readiness;
        private int localizedItem = -1;
        private object culture;
        private string questName, questLocation;
        private long epoch = -1;
#if DEBUG
        internal int NpcQueries { get; private set; }
        internal int NpcVisits { get; private set; }
        internal int ScalarSamples { get; private set; }
        internal int LocalizationReads { get; private set; }
#endif
        internal InformationObservationReader(InformationReadiness readiness) { this.readiness = readiness; }
        internal void Clear() { localizedItem = -1; culture = null; questName = questLocation = null; epoch = -1; }
        internal void Update(HostInformation host)
        {
            var settings = host.Preferences.Value;
            if (!settings.AnySummaryEnabled) return;
            var state = readiness.Snapshot();
            if (epoch != state.Epoch) { Clear(); epoch = state.Epoch; host.ClearContent(); }
            bool wantInfection = settings.Enabled(InformationKind.Infection), wantLuck = settings.Enabled(InformationKind.Luck), wantAngler = settings.Enabled(InformationKind.Angler);
            var player = Main.LocalPlayer;
            if (Main.gameMenu || Main.dedServ || Main.netMode != 0 && Main.netMode != 1 || player == null || !player.active)
            { host.ClearContent(); return; }
            bool dryad = false, wizard = wantLuck && NPC.savedWizard, angler = wantAngler && NPC.savedAngler;
            // One demand-limited table traversal. No savedDryad fiction, NPC
            // name scan, history cache or dependency on entity-label settings.
            if (wantInfection || wantLuck && !wizard || wantAngler && !angler)
            {
#if DEBUG
                NpcQueries++;
#endif
                NPC[] npcs = Main.npc;
                if (npcs != null)
                    for (int i = 0; i < Math.Min(200, npcs.Length); i++)
                    {
#if DEBUG
                        NpcVisits++;
#endif
                        var npc = npcs[i]; if (npc == null || !npc.active) continue;
                        if (wantInfection && npc.type == NPCID.Dryad) dryad = true;
                        if (wantLuck && npc.type == NPCID.Wizard) wizard = true;
                        if (wantAngler && npc.type == NPCID.Angler) angler = true;
                        if ((!wantInfection || dryad) && (!wantLuck || wizard) && (!wantAngler || angler)) break;
                    }
            }
            if (wantInfection)
            {
                var availability = !dryad ? MissingQualification() : !state.Available ? InformationAvailability.Unavailable : !state.Infection ? InformationAvailability.Waiting : InformationAvailability.Ready;
                host.Infection.Update(new InfectionObservation { Availability = availability,
                    Hallow = availability == InformationAvailability.Ready ? (int?)WorldGen.tGood : null,
                    Corruption = availability == InformationAvailability.Ready ? (int?)WorldGen.tEvil : null,
                    Crimson = availability == InformationAvailability.Ready ? (int?)WorldGen.tBlood : null });
            }
            if (wantLuck)
            {
                var sample = new LuckObservation { Availability = wizard ? InformationAvailability.Ready : MissingQualification() };
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
                var sample = new AnglerObservation { Availability = !angler ? MissingQualification() : !state.Available ? InformationAvailability.Unavailable : InformationAvailability.Ready };
                if (angler)
                {
#if DEBUG
                    ScalarSamples++;
#endif
                    sample.Completed = player.anglerQuestsFinished >= 0 ? (int?)player.anglerQuestsFinished : null;
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
        private static InformationAvailability MissingQualification()
        { return Main.netMode == 1 ? InformationAvailability.Waiting : InformationAvailability.ConditionUnmet; }
        private void Localize(int item)
        {
            object currentCulture = Language.ActiveCulture;
            if (localizedItem == item && ReferenceEquals(culture, currentCulture)) return;
            localizedItem = item; culture = currentCulture; questName = questLocation = null;
#if DEBUG
            LocalizationReads++;
#endif
            try
            {
                questName = Lang.GetItemNameValue(item);
                string internalName = ItemID.Search.GetName(item);
                if (String.IsNullOrEmpty(internalName)) return;
                string key = "AnglerQuestText.Quest_" + internalName;
                string value = Language.GetTextValue(key);
                if (value == key) return;
                questLocation = ParseLocation(value);
            }
            catch { questLocation = null; }
        }
        internal static string ParseLocation(string value)
        {
            if (String.IsNullOrEmpty(value)) return null;
            string last = value.TrimEnd(); int newline = last.LastIndexOf('\n'); if (newline >= 0) last = last.Substring(newline + 1).Trim();
            string prefix = last.StartsWith("(Caught in ", StringComparison.Ordinal) ? "(Caught in " :
                last.StartsWith("（抓捕位置：", StringComparison.Ordinal) ? "（抓捕位置：" : null;
            char closing = prefix == "(Caught in " ? ')' : '）';
            if (prefix == null || last.Length <= prefix.Length + 1 || last[last.Length - 1] != closing) return null;
            string location = last.Substring(prefix.Length, last.Length - prefix.Length - 1).Trim();
            return location.Length > 0 && location.Length <= 512 && location.IndexOfAny(new[] { '(', ')', '（', '）' }) < 0 ? location : null;
        }
    }
}
