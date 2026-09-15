using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JueMingR.Platform.DeathHistory;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;

namespace JueMingR.TerrariaHost.DeathHistory
{
    internal sealed class DeathSourceHooks : IDisposable
    {
        private const string Owner = "JueMingR.DeathHistory.Source";
        private const BindingFlags Flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static DeathSourceHooks current;
        [ThreadStatic] private static Scope top;
        private readonly Func<Player, object> token;
        private readonly Action<object, DeathFact, DateTime> accept;
        private readonly Harmony harmony = new Harmony(Owner);
        private MethodInfo target;
        private sealed class Scope
        {
            internal DeathSourceHooks Owner;
            internal Scope Parent;
            internal Player Player;
            internal object Token;
            internal string Id, Reason;
            internal TimeSpan Offset;
            internal DateTime NativeStamp;
            internal float X, Y;
            internal bool Position, Transition, Completed;
        }
        internal DeathSourceHooks(Func<Player, object> token, Action<object, DeathFact, DateTime> accept)
        { this.token = token ?? throw new ArgumentNullException(nameof(token)); this.accept = accept ?? throw new ArgumentNullException(nameof(accept)); }
        internal bool Ready { get; private set; }
        internal string Failure { get; private set; }
        internal void Install()
        {
            try
            {
                if (current != null) throw new InvalidOperationException("death-source-already-installed");
                if (typeof(Main).Module.ModuleVersionId != new Guid("2c29f6c3-4bd9-4add-9c58-da159804e083")) throw new InvalidOperationException("death-source-version");
                NativeDeathText.Validate();
                target = typeof(Player).GetMethod("KillMe", Flags, null, new[] { typeof(PlayerDeathReason), typeof(double), typeof(int), typeof(bool) }, null);
                if (target == null || target.IsStatic || target.ReturnType != typeof(void) || target.ContainsGenericParameters || target.GetMethodBody() == null) throw new MissingMethodException("Player.KillMe");
                current = this; harmony.Patch(target, Hook(nameof(Before)), Hook(nameof(After)), Hook(nameof(Rewrite)), Hook(nameof(FinalizeCall)));
                if (!Harmony.GetPatchInfo(target).Owners.Contains(Owner)) throw new InvalidOperationException("death-source-not-installed");
                Ready = true;
            }
            catch (Exception e) { Failure = e.GetType().Name + ": " + e.Message; Dispose(); }
        }
        private static HarmonyMethod Hook(string name) { return new HarmonyMethod(typeof(DeathSourceHooks).GetMethod(name, Flags)); }
        private static void Before(Player __instance, out Scope __state)
        {
            __state = null; var owner = current; if (owner == null || !owner.Ready) return;
            __state = new Scope { Owner = owner, Parent = top, Player = __instance }; top = __state;
            try
            {
                // An untracked nested invocation must shadow its parent too;
                // otherwise its write could be attributed to the outer call.
                __state.Token = owner.token(__instance);
            }
            catch (Exception e) { owner.Failure = "death-scope: " + e.GetType().Name; }
        }
        private static void BeforeWrite(Player player)
        { var scope = top; if (scope != null && scope.Token != null && ReferenceEquals(scope.Player, player)) scope.Transition = !player.dead && scope.Id == null; }
        private static void AfterWrite(Player player)
        {
            var scope = top; if (scope == null || !ReferenceEquals(scope.Player, player) || !scope.Transition || !player.dead) return;
            scope.Transition = false;
            try
            {
                var time = DateTimeOffset.Now; scope.Id = DeathEventId.Create(time, Guid.NewGuid()); scope.Offset = time.Offset;
                scope.NativeStamp = player.lastDeathTime; scope.X = player.lastDeathPostion.X; scope.Y = player.lastDeathPostion.Y;
                scope.Position = !Single.IsNaN(scope.X) && !Single.IsInfinity(scope.X) && !Single.IsNaN(scope.Y) && !Single.IsInfinity(scope.Y) &&
                    Main.maxTilesX > 0 && Main.maxTilesY > 0 && scope.X >= 0 && scope.Y >= 0 && scope.X < Main.maxTilesX * 16f && scope.Y < Main.maxTilesY * 16f;
            }
            catch (Exception e) { scope.Owner.Failure = "death-fact: " + e.GetType().Name; }
        }
        private static void CreatedText(NetworkText text, Player player)
        {
            var scope = top; if (scope == null || scope.Id == null || !ReferenceEquals(scope.Player, player)) return;
            try { scope.Reason = NativeDeathText.Freeze(text); } catch (Exception e) { scope.Owner.Failure = "death-text: " + e.GetType().Name; }
        }
        private static void After(Scope __state) { Complete(__state); }
        private static Exception FinalizeCall(Exception __exception, Scope __state) { Complete(__state); return __exception; }
        private static void Complete(Scope scope)
        {
            if (scope == null || scope.Completed) return; scope.Completed = true;
            if (ReferenceEquals(top, scope)) top = scope.Parent;
            // A later native exception does not undo an already observed dead
            // transition. Postfix/finalizer and nested calls share this token.
            if (scope.Id == null) return;
            try { scope.Owner.accept(scope.Token, new DeathFact(scope.Id, scope.Offset, scope.Position, scope.X, scope.Y, scope.Reason), scope.NativeStamp); }
            catch (Exception e) { scope.Owner.Failure = "death-admission: " + e.GetType().Name; }
        }
        private static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList(); var dead = typeof(Player).GetField("dead");
            var text = typeof(PlayerDeathReason).GetMethod("GetDeathText", new[] { typeof(string) });
            int[] stores = Enumerable.Range(0, code.Count).Where(i => code[i].opcode == OpCodes.Stfld && Equals(code[i].operand, dead)).ToArray();
            int[] texts = Enumerable.Range(0, code.Count).Where(i => (code[i].opcode == OpCodes.Call || code[i].opcode == OpCodes.Callvirt) && Equals(code[i].operand, text)).ToArray();
            if (stores.Length != 1 || texts.Length != 1 || stores[0] < 2 || code[stores[0] - 2].opcode != OpCodes.Ldarg_0 || code[stores[0] - 1].opcode != OpCodes.Ldc_I4_1 || texts[0] <= stores[0]) throw new InvalidOperationException("death-source-transition-text-shape");
            int store = stores[0], created = texts[0];
            if (code[store - 1].labels.Count != 0 || code[store].labels.Count != 0 || code.Skip(store - 2).Take(3).Any(c => c.blocks.Count != 0) || code[created].blocks.Count != 0) throw new InvalidOperationException("death-source-control-flow-shape");
            var output = new List<CodeInstruction>(code.Count + 8);
            for (int i = 0; i < code.Count; i++)
            {
                if (i == store - 2)
                {
                    var first = new CodeInstruction(OpCodes.Ldarg_0); first.labels.AddRange(code[i].labels); code[i].labels.Clear(); output.Add(first);
                    output.Add(new CodeInstruction(OpCodes.Call, typeof(DeathSourceHooks).GetMethod(nameof(BeforeWrite), Flags)));
                }
                output.Add(code[i]);
                if (i == store) { output.Add(new CodeInstruction(OpCodes.Ldarg_0)); output.Add(new CodeInstruction(OpCodes.Call, typeof(DeathSourceHooks).GetMethod(nameof(AfterWrite), Flags))); }
                if (i == created) { output.Add(new CodeInstruction(OpCodes.Dup)); output.Add(new CodeInstruction(OpCodes.Ldarg_0)); output.Add(new CodeInstruction(OpCodes.Call, typeof(DeathSourceHooks).GetMethod(nameof(CreatedText), Flags))); }
            }
            return output;
        }
        public void Dispose()
        {
            Ready = false; if (ReferenceEquals(current, this)) current = null;
            if (target != null) { try { harmony.Unpatch(target, HarmonyPatchType.All, Owner); } catch (Exception e) { Failure = "death-unpatch: " + e.GetType().Name; } target = null; }
        }
    }
}
