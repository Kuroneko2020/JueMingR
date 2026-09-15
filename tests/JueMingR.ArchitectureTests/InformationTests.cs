using System;
using System.Collections.Generic;
using System.Text;
using JueMingR.Features.Information;
using JueMingR.Platform.Information;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class InformationTests
    {
        internal static void Check(List<string> failures)
        {
            foreach (Action check in new Action[] { Preferences, Content, Luck })
                try { check(); } catch (Exception e) { failures.Add("Information behavior: " + e.Message); }
        }
        private static void Preferences()
        {
            var codec = new InformationPreferenceCodec();
            var value = InformationPreferences.Default.WithEnabled(InformationKind.Luck, true)
                .WithStyle(InformationKind.Biome, new InformationStyle(0x123456, 82).Step(1));
            var loaded = codec.Decode(codec.Encode(value));
            Require(loaded.Enabled(InformationKind.Luck) && !loaded.Enabled(InformationKind.Infection) && !loaded.Enabled(InformationKind.Angler), "new toggles must roundtrip independently");
            Require(loaded.Style(InformationKind.Biome).Rgb == 0x123456 && loaded.Style(InformationKind.Biome).Size == 92 && loaded.Style(InformationKind.Luck).Size == 82, "stepping biome must preserve other 0.82 styles");
            loaded = codec.Decode(codec.Encode(loaded.ResetStyle(InformationKind.Biome)));
            Require(loaded.Style(InformationKind.Biome).Size == 82 && loaded.Style(InformationKind.Biome).Rgb == 0x90EE90 && loaded.Enabled(InformationKind.Luck), "reset restores 0.82 without resetting toggles");
            Require(new InformationStyle(1, 52).Step(-1).Size == 50 && new InformationStyle(1, 172).Step(1).Size == 180, "size steps clamp on both boundaries");
            string unknown = Encoding.UTF8.GetString(codec.Encode(value)).Replace("\"size\":92", "\"future\":true,\"size\":92");
            try { codec.Decode(Encoding.UTF8.GetBytes(unknown)); throw new Exception("unknown nested style was overwritten"); }
            catch (PreferenceFormatException e) { Require(e.Status == PreferenceStatus.UnknownFields, "unknown style is protected"); }
        }
        private static void Content()
        {
            var infection = new InfectionSummary();
            infection.Update(new InfectionObservation { Availability = InformationAvailability.Waiting });
            Require(!infection.Content.Text.Contains("0%"), "unready infection must not publish normal zero");
            var clean = new InfectionObservation { Availability = InformationAvailability.Ready, Hallow = 0, Corruption = 0, Crimson = 0 };
            infection.Update(clean); long version = infection.Content.Version;
            for (int i = 0; i < 100; i++) infection.Update(clean);
            Require(infection.Content.Text.Contains("神圣 0%") && infection.Content.Version == version, "published zero remains valid and stable");
            Require(infection.Content.Text.StartsWith("世界感染：", StringComparison.Ordinal) && !infection.Content.Text.Contains("最近统计"), "normal infection text leaves publication explanation to existing help");
#if DEBUG
            Require(infection.TextBuilds == 2, "stable infection must not regenerate equal strings");
#endif
            clean.Crimson = null; infection.Update(clean);
            Require(infection.Content.Text.Contains("猩红 未知") && infection.Content.Text.Contains("腐化 0%"), "partial infection retains known zero");
            var angler = new AnglerSummary();
            var quest = new AnglerObservation { Availability = InformationAvailability.Ready, ItemType = 2450, Name = "蝙蝠鱼", Location = "地下和洞穴", Completed = 12, SubmittedToday = true };
            angler.Update(quest);
            Require(angler.Content.Text == "渔夫任务：蝙蝠鱼；地点：地下和洞穴\n累计完成：12；今日：已提交", "name/location share the first logical line and personal facts the second");
            version = angler.Content.Version; quest.SubmittedToday = false; angler.Update(quest);
            Require(angler.Content.Version > version && angler.Content.Text.Contains("未提交"), "same fish in a new cycle must refresh today");
            quest.Completed = 13; angler.Update(quest); Require(angler.Content.Text.Contains("13"), "personal count changes independently");
            quest = new AnglerObservation { Availability = InformationAvailability.Unavailable, Completed = 12 };
            angler.Update(quest); Require(angler.Content.Text.Contains("累计完成：12") && angler.Content.Text.Contains("不可用"), "unavailable quest must preserve reliable personal count");
        }
        internal static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Luck()
        {
            var summary = new LuckSummary();
            var value = new LuckObservation { Availability = InformationAvailability.Ready, Total = 0, LadybugTime = 0, Torch = 0, Equipment = 0, Coin = 0,
                Potion = 0, Kite = 0, Pearl = false, Lantern = false, Gnome = false, Stinky = false, Mirror = false };
            summary.Update(value); Require(summary.Content.Text.EndsWith("来源：无", StringComparison.Ordinal), "known all-zero sources may say none");
#if DEBUG
            value.LadybugTime = 61; summary.Update(value); int hiddenBuilds = summary.TextBuilds;
            value.LadybugTime = 60; summary.Update(value);
            Require(summary.TextBuilds == hiddenBuilds, "hidden ladybug timer must not regenerate unchanged text");
            value.LadybugTime = 0; value.Torch = .001f; value.Total = .0002f; summary.Update(value); hiddenBuilds = summary.TextBuilds;
            value.Torch = .002f; value.Total = .0004f; summary.Update(value);
            Require(summary.TextBuilds == hiddenBuilds, "hidden torch explanation must not regenerate unchanged text");
            value.Torch = 0; value.Total = 0;
#endif
            value.Equipment = null; summary.Update(value); Require(summary.Content.Text.Contains("部分明细不可用") && !summary.Content.Text.Contains("来源：无"), "unreadable is not none");
            value.Equipment = 0.0004f; value.Torch = 0.002f; value.Total = 0.0008f; summary.Update(value);
            Require(!summary.Content.Text.Contains("不一致"), "valid hidden small sources must count before mismatch");
            value.Equipment = 0; value.Torch = 1; value.Total = 0.2f; summary.Update(value); long version = summary.Content.Version;
            value.Torch = 1.001f; summary.Update(value);
            Require(summary.Content.Version == version, "hidden raw value changes do not rebuild unchanged player contributions");
            value.Torch = 0; value.LadybugTime = 43200; value.Total = 0.2f; summary.Update(value); version = summary.Content.Version;
            value.LadybugTime = 43140; summary.Update(value); Require(summary.Content.Version > version && summary.Content.Text.Contains("11分59秒"), "visible game-clock remaining time updates");
#if DEBUG
            int builds = summary.TextBuilds;
            for (int i = 0; i < 100; i++) summary.Update(value);
            Require(builds == summary.TextBuilds, "stable luck must reuse text rather than repeatedly format equal strings");
#endif
            value.LadybugTime = 43200; value.Torch = 1; value.Potion = 3; value.Kite = 3; value.Pearl = true; value.Lantern = true;
            value.Gnome = true; value.Stinky = true; value.Equipment = 0.08f; value.Coin = 250000; value.Mirror = true; value.Total = 1.11f;
            summary.Update(value);
            foreach (string source in new[] { "瓢虫", "火把", "幸运药水", "风筝", "银河珍珠", "灯笼夜", "花园侏儒", "臭味", "装备", "钱币", "破镜坏运" })
                Require(summary.Content.Text.Contains(source), "all meaningful vanilla sources: " + source);
            Require(!summary.Content.Text.Contains("不一致"), "float accumulation should not create a twelfth source");
            value.Total = Single.NaN; summary.Update(value); Require(summary.Content.Text.Contains("暂不可用"), "NaN cannot become a normal luck value");
            Require(Math.Abs(LuckSummary.Ladybug(-10800) + 0.2) < 1e-9, "negative ladybug sign and duration");
            float[] samples = { 0, 0.1f, 0.25f, 3, 25, 250, 2500, 25000, 250000 };
            double[] expected = { 0, 0.025, 0.05, 0.075, 0.1, 0.125, 0.15, 0.175, 0.2 };
            for (int i = 0; i < samples.Length; i++) Require(LuckSummary.Coin(samples[i]) == expected[i], "coin contribution tier " + i);
            Require(LuckSummary.Coin(24.9f) == .075 && LuckSummary.Coin(2.49f) == .075 && LuckSummary.Coin(.249f) == .025, "native float-to-double decimal tier boundaries");
        }
    }
}
