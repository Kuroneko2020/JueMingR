using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JueMingR.Platform.DeathHistory;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Creative;
using Terraria.Utilities;

namespace NativeWorldTextProbe
{
    // Keep real native guards, field writes, reason creation and time addition.
    // Only unrelated death side effects/world-event blocks are jumped over in
    // this isolated process. Production instrumentation must remain in the IL.
    internal static class NativeDeathExecutionChecks
    {
        private const BindingFlags Flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string Owner = "JueMingR.Tests.DeathIsolation";
        private static bool cancel, recurse, nested, rejectNested, throwAfter, crossing;
        private static int texts;
        private static readonly Exception expected = new InvalidOperationException("isolated-after-death");
        private static readonly object token = new object();
        internal static void Run(Assembly assembly)
        {
            var facts = new List<DeathFact>(); var contributions = new List<double>();
            var deaths = assembly.GetType("JueMingR.TerrariaHost.DeathHistory.DeathSourceHooks", true);
            var times = assembly.GetType("JueMingR.TerrariaHost.WorldTime.WorldTimeSourceHooks", true);
            var player = new Player { active = true, name = "固定 [i:1] 玩家", position = new Vector2(320, 640) };
            var kill = typeof(Player).GetMethod("KillMe", new[] { typeof(PlayerDeathReason), typeof(double), typeof(int), typeof(bool) });
            var update = typeof(Main).GetMethod("UpdateTime", Flags);
            var night = typeof(Main).GetMethod("UpdateTime_StartNight", Flags);
            var reason = typeof(PlayerDeathReason).GetMethod("GetDeathText");
            var isolated = new Harmony(Owner);
            object deathHooks = Activator.CreateInstance(deaths, Flags, null, new object[] { new Func<Player, object>(p => ReferenceEquals(p, player) && !(nested && rejectNested) ? token : null), new Action<object, DeathFact, DateTime>((owner, fact, stamp) => { Require(ReferenceEquals(owner, token), "correct capture owner"); facts.Add(fact); }) }, null);
            object timeHooks = Activator.CreateInstance(times, Flags, null, new object[] { new Action<double>(contributions.Add) }, null);
            int oldWidth = Main.maxTilesX, oldHeight = Main.maxTilesY, oldMode = Main.netMode; bool oldMenu = Main.gameMenu, oldDay = Main.dayTime, oldFast = Main.fastForwardTimeToDusk, oldDawn = Main.fastForwardTimeToDawn;
            double oldTime = Main.time; int oldRate = Main.dayRate; var oldRandom = Main.rand;
            CreativePowerManager.Initialize();
            var freeze = CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>(); bool oldFreeze = freeze.Enabled;
            var speed = CreativePowerManager.Instance.GetPower<CreativePowers.ModifyTimeRate>(); int oldSpeed = speed.TargetTimeRate;
            int oldActive = Main.CurrentFrameFlags.ActivePlayersCount, oldSleeping = Main.CurrentFrameFlags.SleepingPlayersCount;
            try
            {
                NativeInformationChecks.Call(deathHooks, "Install"); NativeInformationChecks.Call(timeHooks, "Install");
                Require((bool)NativeInformationChecks.Get(deathHooks, "Ready") && (bool)NativeInformationChecks.Get(timeHooks, "Ready"), "production native hooks installed");
                PatchIsolation(isolated);
                Main.maxTilesX = 4200; Main.maxTilesY = 1200; Main.netMode = 0; Main.gameMenu = false;
                var cause = PlayerDeathReason.ByCustomReason("固定 [i:1] 理由");
                var referenceRandom = new UnifiedRandom(17); Main.rand = new UnifiedRandom(17);
                player.KillMe(cause, 1, 0);
                Require(player.dead && facts.Count == 1 && texts == 1 && facts[0].Reason == "固定 [i:1] 理由" && facts[0].X == player.lastDeathPostion.X && facts[0].Y == player.lastDeathPostion.Y, "actual transition freezes native location and one existing reason");
                Require(Main.rand.Next() == referenceRandom.Next(), "original reason observation never consumes an extra random value");
                player.KillMe(cause, 1, 0); Require(facts.Count == 1, "persistent dead / repeated call adds no event");
                player.dead = false; player.creativeGodMode = true; player.KillMe(cause, 1, 0); player.creativeGodMode = false; Require(facts.Count == 1 && !player.dead, "native invulnerability early return");
                cancel = true; player.KillMe(cause, 1, 0); cancel = false; Require(facts.Count == 1 && !player.dead, "cancelled native original adds no event");
                player.KillMe(cause, 1, 0); Require(facts.Count == 2 && facts[0].EventId != facts[1].EventId, "revive and die at same location/reason keeps two identities");
                player.dead = false; recurse = true; player.KillMe(cause, 1, 0); Require(facts.Count == 3, "nested call actual transition counted once");
                player.dead = false; rejectNested = recurse = true; player.KillMe(cause, 1, 0); rejectNested = false; Require(facts.Count == 3, "untracked nested scope shadows outer token");
                player.dead = false; throwAfter = true;
                try { player.KillMe(cause, 1, 0); throw new Exception("expected native exception"); } catch (InvalidOperationException e) { Require(ReferenceEquals(e, expected), "original exception returned unchanged"); }
                throwAfter = false; Require(facts.Count == 4, "already completed transition survives later exception exactly once");
                Main.dayTime = true; Main.fastForwardTimeToDawn = false; Main.fastForwardTimeToDusk = true; Main.dayRate = 7; Main.time = 100;
                update.Invoke(null, null); Require(Main.time == 160 && contributions.SequenceEqual(new[] { 60.0 }), "actual inner rate 60, not entry rate 7");
                Main.time = 53990; crossing = true; update.Invoke(null, null); crossing = false;
                Require(Main.time == 0 && !Main.dayTime && !Main.fastForwardTimeToDusk && contributions.SequenceEqual(new[] { 60.0, 60.0 }), "native dusk reset retains actual increment before new rate");
                Main.SkipToTime(1234, Main.dayTime); Require(Main.time == 1234 && contributions.Count == 2, "actual direct set has no observed contribution");
                freeze.SetPowerInfo(true); update.Invoke(null, null);
                Require(Main.time == 1234 && contributions.Count == 2 && Main.dayRate == 0, "native freeze produces zero contribution");
                freeze.SetPowerInfo(false); typeof(CreativePowers.ModifyTimeRate).GetProperty("TargetTimeRate", Flags).SetValue(speed, 24);
                Main.CurrentFrameFlags.ActivePlayersCount = Main.CurrentFrameFlags.SleepingPlayersCount = 1; update.Invoke(null, null);
                Require(contributions.Last() == 120, "native sleep multiplies current travel time rate");
                Main.CurrentFrameFlags.SleepingPlayersCount = 0; update.Invoke(null, null); Require(contributions.Last() == 24, "native travel rate alone contributes its actual operand");
                Console.WriteLine("PASS: controlled real KillMe and UpdateTime execution; transition, cancellation, nesting, reason/RNG, freeze, direct set and dusk boundary.");
            }
            finally
            {
                cancel = recurse = nested = rejectNested = throwAfter = crossing = false;
                foreach (var method in new[] { kill, update, night, reason }) isolated.Unpatch(method, HarmonyPatchType.All, Owner);
                ((IDisposable)deathHooks).Dispose(); ((IDisposable)timeHooks).Dispose(); freeze.SetPowerInfo(oldFreeze);
                typeof(CreativePowers.ModifyTimeRate).GetProperty("TargetTimeRate", Flags).SetValue(speed, oldSpeed); Main.CurrentFrameFlags.ActivePlayersCount = oldActive; Main.CurrentFrameFlags.SleepingPlayersCount = oldSleeping;
                Main.maxTilesX = oldWidth; Main.maxTilesY = oldHeight; Main.netMode = oldMode; Main.gameMenu = oldMenu; Main.dayTime = oldDay; Main.time = oldTime; Main.dayRate = oldRate; Main.fastForwardTimeToDusk = oldFast; Main.fastForwardTimeToDawn = oldDawn; Main.rand = oldRandom;
            }
        }
        internal static void WithIsolation(Action check)
        {
            var isolated = new Harmony(Owner);
            try { PatchIsolation(isolated); check(); }
            finally { isolated.UnpatchAll(Owner); }
        }
        private static void PatchIsolation(Harmony isolated)
        {
            var killIsolation = Hook(nameof(IsolateDeath)); killIsolation.after = new[] { "JueMingR.DeathHistory.Source" };
            var timeIsolation = Hook(nameof(IsolateTime)); timeIsolation.after = new[] { "JueMingR.WorldTime.Source" };
            isolated.Patch(typeof(Player).GetMethod("KillMe", new[] { typeof(PlayerDeathReason), typeof(double), typeof(int), typeof(bool) }), prefix: Hook(nameof(Cancel)), transpiler: killIsolation);
            isolated.Patch(typeof(Main).GetMethod("UpdateTime", Flags), transpiler: timeIsolation);
            isolated.Patch(typeof(Main).GetMethod("UpdateTime_StartNight", Flags), transpiler: Hook(nameof(IsolateNight)));
            isolated.Patch(typeof(PlayerDeathReason).GetMethod("GetDeathText"), prefix: Hook(nameof(CountReason)));
        }
        private static HarmonyMethod Hook(string name) { return new HarmonyMethod(typeof(NativeDeathExecutionChecks).GetMethod(name, Flags)); }
        private static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        private static bool Cancel() { return !cancel; }
        private static void CountReason() { texts++; }
        private static void Reenter(Player player, PlayerDeathReason reason)
        { if (!recurse) return; recurse = false; nested = true; try { player.KillMe(reason, 1, 0); } finally { nested = false; } }
        private static void FinishText() { if (throwAfter) throw expected; }
        private static int Unique(List<CodeInstruction> code, Func<CodeInstruction, bool> predicate)
        { int[] matches = Enumerable.Range(0, code.Count).Where(i => predicate(code[i])).ToArray(); Require(matches.Length == 1, "unique isolation anchor"); return matches[0]; }
        private static bool Call(CodeInstruction instruction, string type, string name)
        { var method = instruction.operand as MethodInfo; return (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) && method?.DeclaringType?.Name == type && method.Name == name; }
        private static void Insert(List<CodeInstruction> code, int index, params CodeInstruction[] inserted)
        { Require(code[index].blocks.Count == 0, "empty exception boundary at insertion"); inserted[0].labels.AddRange(code[index].labels); code[index].labels.Clear(); code.InsertRange(index, inserted); }
        private static Label Target(List<CodeInstruction> code, int index, ILGenerator generator)
        { Require(code[index].blocks.Count == 0, "target outside exception boundaries"); var label = generator.DefineLabel(); code[index].labels.Add(label); return label; }
        private static IEnumerable<CodeInstruction> IsolateDeath(IEnumerable<CodeInstruction> input, ILGenerator generator)
        {
            var code = input.ToList();
            int before = Unique(code, c => Call(c, "DeathSourceHooks", "BeforeWrite"));
            int after = Unique(code, c => Call(c, "DeathSourceHooks", "AfterWrite"));
            int created = Unique(code, c => Call(c, "DeathSourceHooks", "CreatedText"));
            int nativeText = Unique(code, c => Call(c, "PlayerDeathReason", "GetDeathText"));
            int position = Unique(code, c => c.opcode == OpCodes.Stfld && Equals(c.operand, typeof(Player).GetField("lastDeathPostion")));
            int shown = Unique(code, c => c.opcode == OpCodes.Stfld && Equals(c.operand, typeof(Player).GetField("showLastDeath")));
            int guard = code.FindIndex(c => c.opcode == OpCodes.Ret) + 1;
            Require(guard > 0 && guard < position && code[position - 3].opcode == OpCodes.Ldarg_0 && code[position - 2].opcode == OpCodes.Ldarg_0 && code[before - 1].opcode == OpCodes.Ldarg_0 && code[nativeText - 3].opcode == OpCodes.Ldarg_1 && code[nativeText - 2].opcode == OpCodes.Ldarg_0, "native death expression boundaries");
            var toPosition = Target(code, position - 3, generator); var toWrite = Target(code, before - 1, generator); var toText = Target(code, nativeText - 3, generator);
            Insert(code, created + 1, new CodeInstruction(OpCodes.Pop), new CodeInstruction(OpCodes.Call, typeof(NativeDeathExecutionChecks).GetMethod(nameof(FinishText), Flags)), new CodeInstruction(OpCodes.Ret));
            Insert(code, after + 1, new CodeInstruction(OpCodes.Br, toText));
            Insert(code, shown + 1, new CodeInstruction(OpCodes.Br, toWrite));
            Insert(code, guard, new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldarg_1), new CodeInstruction(OpCodes.Call, typeof(NativeDeathExecutionChecks).GetMethod(nameof(Reenter), Flags)), new CodeInstruction(OpCodes.Br, toPosition));
            return code;
        }
        private static IEnumerable<CodeInstruction> IsolateTime(IEnumerable<CodeInstruction> input, ILGenerator generator)
        {
            var code = input.ToList(); int rate = Unique(code, c => Call(c, "Main", "UpdateTimeRate")), applied = Unique(code, c => Call(c, "WorldTimeSourceHooks", "Applied"));
            int night = Unique(code, c => Call(c, "Main", "UpdateTime_StartNight"));
            int threshold = Unique(code, c => c.opcode == OpCodes.Ldc_R8 && Equals(c.operand, 54000.0));
            Require(code[threshold - 1].opcode == OpCodes.Ldsfld && Equals(code[threshold - 1].operand, typeof(Main).GetField("time")), "actual dusk predicate");
            var toRate = Target(code, rate, generator); var toThreshold = Target(code, threshold - 1, generator);
            Insert(code, night + 1, new CodeInstruction(OpCodes.Ret));
            Insert(code, applied + 1, new CodeInstruction(OpCodes.Ldsfld, typeof(NativeDeathExecutionChecks).GetField(nameof(crossing), Flags)), new CodeInstruction(OpCodes.Brtrue, toThreshold), new CodeInstruction(OpCodes.Ret));
            Insert(code, 0, new CodeInstruction(OpCodes.Br, toRate)); return code;
        }
        private static IEnumerable<CodeInstruction> IsolateNight(IEnumerable<CodeInstruction> input, ILGenerator generator)
        {
            var code = input.ToList(); int rate = Unique(code, c => Call(c, "Main", "UpdateTimeRate")); int cooldown = rate + 1;
            Require(code[cooldown].opcode == OpCodes.Ldsfld && Equals(code[cooldown].operand, typeof(Main).GetField("moondialCooldown")), "native event boundary after fast-forward rate update");
            int reset = Unique(code, c => c.opcode == OpCodes.Stsfld && Equals(c.operand, typeof(Main).GetField("time")));
            Require(code[reset - 1].opcode == OpCodes.Ldc_R8 && Equals(code[reset - 1].operand, 0.0), "actual native reset to zero");
            var target = Target(code, reset - 1, generator); Insert(code, cooldown, new CodeInstruction(OpCodes.Br, target)); return code;
        }
    }
}
