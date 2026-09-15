using System;
using System.Reflection;
using HarmonyLib;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.Utilities;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeDeathCauseChecks
    {
        private const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
        internal static void Run(Type safe)
        {
            var capture = safe.GetMethod("Capture", Flags);
            Action<NetworkText, string> check = (text, expected) =>
            {
                string before = text.ToString(); object[] args = { text, null };
                string frozen = (string)capture.Invoke(null, args);
                Require(frozen == before && (string)args[1] == expected && text.ToString() == before, "direct cause and unchanged original: " + expected);
            };
            var oldRandom = Main.rand;
            try
            {
                Main.rand = new UnifiedRandom(173);
                int[] other = { 0, 1, 2, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23 };
                string[] expected = { "死于摔落", "死于溺水", "死于岩浆", "死于石化", "死于刺伤", "死于窒息", "死于燃烧", "死于中毒", "死于触电", "死于逃离血肉墙", "死于血肉墙", "死于混沌状态", "死于混沌状态", "死于混沌状态", "死于地狱火", "死于黑暗", "死于饥饿", "死于越过世界顶部", "死于替队友承受伤害", "死于坠出世界底部", "死于阳光灼烧", "死于叶绿孢子" };
                for (int i = 0; i < other.Length; i++) check(PlayerDeathReason.ByOther(other[i]).GetDeathText("测试玩家"), expected[i]);
                check(NetworkText.FromKey("DeathSource.NPC", "完整原句", "飞鱼"), "死于飞鱼");
                check(NetworkText.FromKey("DeathSource.Projectile", "完整原句", "火球"), "死于火球");
                check(NetworkText.FromKey("DeathSource.Player", "完整原句", "[i:1] 对手", "剑"), "死于玩家 [i:1] 对手");
                check(NetworkText.FromKey("DeathText.Fell_1", "玩家"), "死于摔落");
                check(NetworkText.FromKey("DeathText.Fell_9", "玩家"), "死于摔落");
                check(NetworkText.FromLiteral("DeathText.Fell_1 死于飞鱼"), null);
                check(NetworkText.FromFormattable("{0}", "死于摔落"), null);
                check(NetworkText.FromKey("DeathText.Fell_10", "玩家"), null);
                check(NetworkText.FromKey("DeathText.FutureCause", "玩家"), null);
                check(NetworkText.FromKey("DeathText.Fell_1", "玩家", "多余参数"), null);
                check(NetworkText.FromKey("DeathSource.NPC", "原句", new string('名', 1024)), null);
                var npc = new NPC { GivenName = "冻结名字" }; var oldNpc = Main.npc[0]; Main.npc[0] = npc;
                try
                {
                    var text = PlayerDeathReason.ByNPC(0).GetDeathText("玩家"); object[] args = { text, null };
                    string original = (string)capture.Invoke(null, args); Main.npc[0] = new NPC { GivenName = "换槽后的名字" };
                    Require((string)args[1] == "死于冻结名字" && original.Contains("冻结名字") && !original.Contains("换槽后的名字"), "captured source name survives slot replacement");
                }
                finally { Main.npc[0] = oldNpc; }
                var harmony = new Harmony("JueMingR.Tests.DeathCauseFailure"); var classifier = safe.GetMethod("DirectCause", Flags);
                try
                {
                    harmony.Patch(classifier, prefix: new HarmonyMethod(typeof(NativeDeathCauseChecks).GetMethod(nameof(FailClassification), Flags)));
                    check(NetworkText.FromKey("DeathText.Fell_1", "玩家"), null);
                }
                finally { harmony.Unpatch(classifier, HarmonyPatchType.All, harmony.Id); }
            }
            finally { Main.rand = oldRandom; }
            Console.WriteLine("PASS: native direct causes, three chaos variants, preserved originals, slot replacement and classification-failure fallback.");
        }
        private static void FailClassification() { throw new InvalidOperationException("isolated-classification-failure"); }
    }
}
