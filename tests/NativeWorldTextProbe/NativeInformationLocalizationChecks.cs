using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Information;
using JueMingR.Platform.Information;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeInformationLocalizationChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        // Independently read from the fixed T8 embedded Town JSON and native
        // quest mapping; this oracle is test data, never a product location table.
        private static readonly string[] Expected = {
            "Batfish|Underground & Caverns|地下和洞穴", "BumblebeeTuna|Honey|蜂蜜", "Catfish|Jungle Surface|丛林地表",
            "Cloudfish|Sky Lakes|天湖", "Cursedfish|Corruption|腐化之地", "Dirtfish|Surface & Underground|地表和地下",
            "DynamiteFish|Surface|地表", "EaterofPlankton|Corruption|腐化之地", "FallenStarfish|Sky Lakes & Surface|天湖和地表",
            "TheFishofCthulu|Sky Lakes & Surface|天湖和地表", "Fishotron|Caverns|洞穴", "Harpyfish|Sky Lakes & Surface|天湖和地表",
            "Hungerfish|Caverns|洞穴", "Ichorfish|Crimson|猩红之地", "Jewelfish|Underground & Caverns|地下和洞穴",
            "MirageFish|Underground Hallow|地下神圣之地", "MutantFlinxfin|Underground Tundra|地下苔原", "Pengfish|Surface Tundra|地表苔原",
            "Pixiefish|Surface Hallow|地表神圣之地", "Spiderfish|Underground & Caverns|地下和洞穴", "TundraTrout|Surface Tundra|地表苔原",
            "UnicornFish|Hallow|神圣之地", "GuideVoodooFish|Caverns|洞穴", "Wyverntail|Sky Lakes|天湖", "ZombieFish|Surface|地表",
            "AmanitaFungifin|Glowing Mushroom Fields|发光蘑菇地", "Angelfish|Sky Lakes|天湖", "BloodyManowar|Crimson|猩红之地",
            "Bonefish|Underground & Caverns|地下和洞穴", "Bunnyfish|Surface|地表", "CapnTunabeard|Ocean|海洋", "Clownfish|Ocean|海洋",
            "DemonicHellfish|Caverns|洞穴", "Derpfish|Jungle Surface|丛林地表", "Fishron|Underground Tundra|地下苔原",
            "InfectedScabbardfish|Corruption|腐化之地", "Mudfish|Jungle|丛林", "Slimefish|Surface Forest|地表森林",
            "TropicalBarracuda|Jungle Surface|丛林地表", "ScarabFish|Desert|沙漠", "ScorpioFish|Desert|沙漠" };
        private const string QuestKey = "AnglerQuestText.Quest_Angelfish", NameKey = "ItemName.Angelfish";
        private static string replacement, replacementName;
        private static int failures;
        // Substitute only this resource input after the real native loader;
        // ReloadLanguage and its completion event are the actual T8 methods.
        private static void SupplyResource()
        {
            if (replacement != null) Call(Language.GetText(QuestKey), "SetValue", replacement);
            if (replacementName != null) Call(Language.GetText(NameKey), "SetValue", replacementName);
        }
        private static void ReadFailure(string key)
        { if (key == QuestKey && failures > 0) { failures--; throw new InvalidOperationException("isolated first resource read failure"); } }
        private static void Reload(string text, string name = "天使鱼")
        {
            replacement = text; replacementName = name;
            Call(LanguageManager.Instance, "ReloadLanguage", Language.ActiveCulture);
        }
        internal static void Run(object context, object host)
        {
            object reader = Get(host, "source");
            Require(Main.anglerQuestItemNetIDs.Length == Expected.Length, "fixed native quest mapping length");
            for (int language = 0; language < 2; language++)
            {
                LanguageManager.Instance.SetLanguage(language == 0 ? "en-US" : "zh-Hans"); Lang.InitializeLegacyLocalization();
                for (int i = 0; i < Expected.Length; i++)
                {
                    string[] expected = Expected[i].Split('|'); int item = Main.anglerQuestItemNetIDs[i];
                    Require(ItemID.Search.GetName(item) == expected[0], "native oracle item order " + i);
                    Call(reader, "Localize", item);
                    Require((string)GetOptional(reader, "questLocation") == expected[language + 1] && !String.IsNullOrEmpty((string)GetOptional(reader, "questName")),
                        "actual native exact location " + expected[0] + " / " + language);
                }
            }
            var patch = new Harmony("JueMingR.Information.ResourceRevisionTests");
            MethodInfo load = typeof(LanguageManager).GetMethod("LoadFromContentSources", Flags), read = typeof(Language).GetMethod("GetText", Flags);
            Require(load != null && read != null, "fixed native resource completion/read targets");
            patch.Patch(load, postfix: new HarmonyMethod(typeof(NativeInformationLocalizationChecks).GetMethod(nameof(SupplyResource), Flags)));
            patch.Patch(read, prefix: new HarmonyMethod(typeof(NativeInformationLocalizationChecks).GetMethod(nameof(ReadFailure), Flags)));
            Action observe = () => Call(reader, "Localize", ItemID.Angelfish);
            try
            {
                // Exact terminal line on disk in the owner's enabled translation
                // pack, not a claim that the screenshot's live memory was read.
                Reload("(天使鱼，捕获位置：太空，纯净地形)"); observe();
                Require((string)GetOptional(reader, "questLocation") == "太空，纯净地形", "enabled-pack Angelfish terminal annotation must recover location");
                object sameText = Language.GetText(QuestKey);
                Reload("\r\n （ 天使鱼 , 捕获位置 : 天空湖泊 ） \r\n"); observe();
                Require(ReferenceEquals(sameText, Language.GetText(QuestKey)) && (string)Get(reader, "questLocation") == "天空湖泊", "same culture and LocalizedText object may change its raw value");
                Reload("（抓捕位置：更新地点）", "更新鱼名"); observe();
                Require((string)Get(reader, "questName") == "更新鱼名" && (string)Get(reader, "questLocation") == "更新地点", "resource completion refreshes name and location together");
                int reads = (int)Get(reader, "LocalizationReads"), parses = (int)Get(reader, "LocationParses");
                Reload("（抓捕位置：更新地点）", "更新鱼名"); observe();
                Require((int)Get(reader, "LocalizationReads") == reads + 1 && (int)Get(reader, "LocationParses") == parses, "unchanged resource reload does not reparse");
                foreach (string text in new[] { "故事（天湖）", "(别的鱼，捕获位置：海洋)", "(Caught in Sky Lakes) trailing", "（抓捕位置：）" })
                {
                    Reload(text); observe();
                    Require(GetOptional(reader, "questLocation") == null && Get(reader, "LocationStatus").ToString() == "UnsupportedFormat", "unsupported story/foreign fish/invalid terminal stays unknown");
                    reads = (int)Get(reader, "LocalizationReads"); parses = (int)Get(reader, "LocationParses");
                    for (int i = 0; i < 500; i++) observe();
                    Require((int)Get(reader, "LocalizationReads") == reads && (int)Get(reader, "LocationParses") == parses, "stable unsupported resource never retries each update");
                }
                foreach (string text in new[] { QuestKey, "" })
                {
                    Reload(text); observe();
                    Require(GetOptional(reader, "questLocation") == null && Get(reader, "LocationStatus").ToString() == (text == "" ? "NotReady" : "MissingKey"), "missing key and not-ready reasons remain distinct");
                    Reload("（抓捕位置：天湖）"); observe(); Require((string)Get(reader, "questLocation") == "天湖", "native completion recovers initially absent text");
                }
                Reload("（抓捕位置：天湖）"); failures = 1; observe();
                Require(Get(reader, "LocationStatus").ToString() == "ReadFailed", "one read exception has its own internal reason");
                observe(); Require((string)Get(reader, "questLocation") == "天湖", "one transient read exception recovers on the bounded next demand");
                Reload("（抓捕位置：天湖）"); failures = 100; observe(); observe();
                reads = (int)Get(reader, "LocalizationReads"); for (int i = 0; i < 500; i++) observe();
                Require((int)Get(reader, "LocalizationReads") == reads, "persistent exception has at most one immediate retry per resource generation");
                failures = 0; Reload("(天使鱼，捕获位置：太空，纯净地形)"); observe();
                Main.anglerQuest = Array.IndexOf(Main.anglerQuestItemNetIDs, ItemID.Angelfish);
                var buffer = NetMessage.buffer[256]; buffer.readBuffer[0] = 74; buffer.readBuffer[1] = (byte)Main.anglerQuest; buffer.readBuffer[2] = 0;
                int messageType; buffer.GetData(0, 3, out messageType);
                Call(context, "UpdateRuntime"); reads = (int)Get(reader, "LocalizationReads");
                Main.anglerQuestFinished = true; Main.LocalPlayer.anglerQuestsFinished++;
                Call(context, "UpdateRuntime");
                string summary = ((AnglerSummary)Get(host, "Angler")).Content.Text;
                Require(summary.Contains("已提交") && summary.Contains("太空，纯净地形") && (int)Get(reader, "LocalizationReads") == reads, "today/count refresh leaves reliable task location cache intact");
                buffer.GetData(0, 3, out messageType); Call(context, "UpdateRuntime");
                Require(((AnglerSummary)Get(host, "Angler")).Content.Text.Contains("未提交") && (int)Get(reader, "LocalizationReads") == reads, "same fish new day updates today without localization");
                CheckHud(host);
            }
            finally
            {
                failures = 0; replacement = replacementName = null;
                patch.Unpatch(load, HarmonyPatchType.All, patch.Id); patch.Unpatch(read, HarmonyPatchType.All, patch.Id);
                LanguageManager.Instance.SetLanguage("en-US"); Lang.InitializeLegacyLocalization();
            }
            Console.WriteLine("PASS: 82 native exact locations, enabled-pack Angelfish input, real reload event, bounded failure recovery and Summary-to-HUD two-line layout.");
        }
        private static void CheckHud(object host)
        {
            object hud = Get(host, "Hud"); var font = Terraria.GameContent.FontAssets.MouseText.Value;
            Call(host, "ResetStyle", InformationKind.Angler);
            Call(hud, "Prepare", font, 960f, 640f, false, null);
            object block = ((Array)Get(hud, "blocks")).GetValue(3); var lines = (IList)Get(block, "Lines");
            Require(lines.Count == 2 && ((string)Get(lines[0], "Text")).Contains("天使鱼；地点：太空，纯净地形") && ((string)Get(lines[1], "Text")).Contains("累计完成：") && ((string)Get(lines[1], "Text")).Contains("今日："), "default 0.82 actual HUD keeps the two logical rows");
            ((AnglerSummary)Get(host, "Angler")).Update(new AnglerObservation { Availability = InformationAvailability.Ready, ItemType = ItemID.Angelfish, Name = "天使鱼", Location = new string('湖', 200), Completed = 13, SubmittedToday = true });
            for (int i = 0; i < 10; i++) Call(host, "StepSize", InformationKind.Angler, 1);
            Call(hud, "Prepare", font, 240f, 300f, false, null);
            lines = (IList)Get(block, "Lines"); Require(lines.Count > 2, "long/narrow/large-font content safely wraps without forced shrink");
            foreach (object line in lines) Require((float)Get(Get(line, "Size"), "Width") * (float)Get(block, "Scale") <= 216.01f, "measured lines stay within viewport and drag bounds");
            int builds = (int)Get(hud, "LayoutBuilds");
            var previousAsset = Terraria.GameContent.FontAssets.MouseText;
            FiniteCostChecks.SetCpuFont(14);
            try { Call(hud, "Prepare", Terraria.GameContent.FontAssets.MouseText.Value, 240f, 300f, false, null); }
            finally { Terraria.GameContent.FontAssets.MouseText = previousAsset; }
            Require((int)Get(hud, "LayoutBuilds") > builds, "actual font identity replacement invalidates HUD layout");
            Call(host, "ResetStyle", InformationKind.Angler); Call(host, "PrepareHud");
        }
    }
}
