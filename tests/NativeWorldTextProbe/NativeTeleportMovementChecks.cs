using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;

namespace NativeWorldTextProbe
{
    // Version-bound original consumers in a disposable native fixture. Cosmetic
    // effects/camera are sinks; Teleport, item time, buffs and message65 entry
    // remain real. This is not a live multiplayer or owner-experience claim.
    internal static class NativeTeleportMovementChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private delegate bool Correct(Player player, ref Vector2 point, out int enumerated);
        private static int teleports, messages, permissions, limits;
        private static bool permit = true;
        private static Vector2 teleported;

        internal static void Run(object context)
        {
            Assembly assembly = context.GetType().Assembly;
            Type hook = assembly.GetType("JueMingR.TerrariaHost.Movement.TeleportHooks", true);
            var permission = (Func<Player, bool>)hook.GetField("canCorrect", Flags).GetValue(null);
            var report = (Action<string>)hook.GetField("reportFailure", Flags).GetValue(null);
            try { Run(assembly); }
            finally
            {
                if (permission != null)
                    Require((bool)hook.GetMethod("TryInstall", Flags).Invoke(null, new object[] { permission, report }),
                        "native leaf restores the exact composed Session permission and reporting owners");
            }
        }
        internal static void Run(Assembly assembly)
        {
            Type hook = assembly.GetType("JueMingR.TerrariaHost.Movement.TeleportHooks", true);
            Type correction = assembly.GetType("JueMingR.TerrariaHost.Movement.TeleportTargetCorrection", true);
            var correct = (Correct)Delegate.CreateDelegate(typeof(Correct), correction.GetMethod("TryCorrect", Flags));
            var rod = typeof(Player).GetMethod("ItemCheck_UseTeleportRod", Flags);
            var h = new Harmony("JueMingR.Tests.TeleportConsumers");
            Action<string> failure = message => { throw new InvalidOperationException(message); };
            // The caller owns any previously composed permission delegate. This
            // leaf temporarily installs a controlled gate; integration restores
            // the genuine Session gate after this method returns.
            hook.GetMethod("Uninstall", Flags).Invoke(null, null);
            try
            {
                CheckTranspiler(hook, rod);
                h.Patch(typeof(Main).GetMethod("TeleportEffect", Flags), prefix: Patch(nameof(SkipCosmetic)));
                h.Patch(typeof(Main).GetMethod("StartCameraTransitionForPlayerTeleport", Flags), prefix: Patch(nameof(SkipCosmetic)));
                h.Patch(typeof(Player).GetMethod("Teleport", Flags), prefix: Patch(nameof(ObserveTeleport)));
                h.Patch(typeof(NetMessage).GetMethod("SendData", Flags), prefix: Patch(nameof(ObserveMessage)));
                h.Patch(typeof(Player).GetMethod("LimitPointToPlayerReachableArea", Flags), prefix: Patch(nameof(ObserveLimit)));
                Require((bool)hook.GetMethod("TryInstall", Flags).Invoke(null, new object[] { (Func<Player, bool>)Permission, failure }), "exact local-target transpiler installs");
                Require(Harmony.GetPatchInfo(rod).Owners.Contains("JueMingR.Movement.Teleport"), "real rod method owns the exact patch");
                Player p = Main.LocalPlayer;
                p.width = 20; p.height = 42;
                Clear(); Prepare(p); Vector2 original = new Vector2(1000, 680), point = original; int n;
                Require(!correct(p, ref point, out n) && point == original && n == 0, "legal original queries zero offsets");
                Measure(correct, p, original, "legal", 0);
                Main.tileSolid[1] = true; Main.tile[63, 43].active(true); Main.tile[63, 43].type = 1;
                point = original;
                Require(correct(p, ref point, out n) && point == new Vector2(984, 680) && n > 0, "full 20x42 native box selects nearest genuinely clear space");
                Console.WriteLine("TELEPORT native nearby candidates=" + n + " selected=" + point);
                Measure(correct, p, original, "nearby", n);
                NativeUse(rod, p, 5335, original, new Vector2(984, 680));
                Require(p.itemTime == 20 && permissions == 1 && limits > 1 && teleports == 1 && messages == 1 && p.FindBuffIndex(88) < 0,
                    "harmony rod native ApplyItemTime precedes gate, real Teleport and message65 once, no chaos buff");
                Require(p.teleportStyle == 1 && p.teleportTime == 1 && p.environmentBuffImmunityTimer == 4, "native Teleport completes beyond position write");
                // Ordinary failure and denied owner both retain native use time.
                Prepare(p); permit = false;
                NativeUse(rod, p, 5335, original, p.position);
                Require(teleports == 0 && messages == 0 && limits == 1 && p.itemTime == 20, "denied input gate performs no candidate limiter query");
                permit = true;
                foreach (int gate in new[] { 0, 1, 2, 3 })
                {
                    teleports = messages = permissions = limits = 0; Prepare(p); p.itemAnimation = gate == 1 ? 0 : 20; p.itemTime = gate == 2 ? 10 : 0;
                    int who = p.whoAmI; if (gate == 3) p.whoAmI = who + 1;
                    try { rod.Invoke(p, new object[] { new Item { type = gate == 0 ? 1 : 5335, useTime = 20 } }); }
                    finally { p.whoAmI = who; }
                    Require(permissions == 0 && limits == 0 && teleports == 0 && messages == 0,
                        "native nonrod/unused/occupied-time/remote guard performs no helper work gate=" + gate);
                }
                Clear(); Prepare(p);
                NativeUse(rod, p, 1326, original, original);
                Require(p.FindBuffIndex(88) >= 0 && p.statLife == 700 && teleports == 1 && messages == 1, "chaos rod native buff preserved on first use");
                Prepare(p); p.chaosState = true;
                NativeUse(rod, p, 1326, original, original);
                Require(p.statLife == 600 && p.FindBuffIndex(88) >= 0 && p.lifeRegenCount == 0 && p.lifeRegenTime == 0, "reused chaos rod retains native one-seventh damage and regeneration reset");
                Clear(); Prepare(p);
                // A continuous wall350 separates every sample in the search
                // circle. Tile350 or a harmless different wall cannot stand in.
                for (int y = 0; y < Main.maxTilesY; y++) Main.tile[48, y].wall = 350;
                point = original;
                Require(!correct(p, ref point, out n) && point == original && n == CountCircle(), "wall350 line independently blocks all candidates");
                for (int y = 0; y < Main.maxTilesY; y++) Main.tile[48, y].wall = 349;
                point = original; Require(!correct(p, ref point, out n) && n == 0, "another non-solid wall is not wall350");
                Clear(); Prepare(p); Main.worldSurface = 20; NPC.downedPlantBoss = false;
                for (int x = 48; x <= 77; x++) for (int y = 27; y <= 58; y++) Main.tile[x, y].wall = 87;
                point = original; Require(!correct(p, ref point, out n) && n == CountCircle(), "undefeated underground wall87 independently blocks destination");
                NPC.downedPlantBoss = true; point = original; Require(!correct(p, ref point, out n) && n == 0, "progression unlock makes original legal without search");
                NPC.downedPlantBoss = false; Main.worldSurface = 100; point = original;
                Require(!correct(p, ref point, out n) && n == 0, "non-remix surface wall87 remains native legal");
                Main.remixWorld = true; point = original;
                Require(!correct(p, ref point, out n) && n == CountCircle(), "remix retains wall87 restriction above surface");
                Main.remixWorld = false;
                Clear(); Prepare(p);
                // Dense solid world proves finite worst case without extending
                // radius or retaining a map cache across the following Clear.
                for (int x = 0; x < Main.maxTilesX; x++) for (int y = 0; y < Main.maxTilesY; y++) { Main.tile[x, y].active(true); Main.tile[x, y].type = 1; }
                point = original; var timer = Stopwatch.StartNew();
                Require(!correct(p, ref point, out n) && n == CountCircle() && point == original, "dense world exhausts exact finite circle and preserves origin");
                timer.Stop(); Console.WriteLine("TELEPORT native exhausted candidates=" + n + " elapsed-ms=" + timer.Elapsed.TotalMilliseconds.ToString("R"));
                Measure(correct, p, original, "exhausted", n);
                Clear(); Prepare(p); point = new Vector2(50, 640);
                // The tiny map is not a generated world's 640-pixel inset.
                // Expose the independent 50-pixel rod boundary with explicit
                // test play-area bounds, retaining the real native limiter.
                float left = Main.leftWorld, top = Main.topWorld, right = Main.rightWorld, bottom = Main.bottomWorld;
                try
                {
                    Main.leftWorld = Main.topWorld = -640; Main.rightWorld = Main.maxTilesX * 16 + 640; Main.bottomWorld = Main.maxTilesY * 16 + 640;
                    Require(correct(p, ref point, out n) && point.X > 50, "strict native world50 boundary is corrected inward");
                }
                finally { Main.leftWorld = left; Main.topWorld = top; Main.rightWorld = right; Main.bottomWorld = bottom; }
                point = new Vector2(1600, 680); p.LimitPointToPlayerReachableArea(ref point); original = point;
                // Every accepted correction is itself fixed by native limiting;
                // this also covers the clamped small fixture's reachable edge.
                Main.tile[(int)(point.X / 16), (int)(point.Y / 16)].active(true); Main.tile[(int)(point.X / 16), (int)(point.Y / 16)].type = 1;
                if (correct(p, ref point, out n)) { Vector2 verify = point; p.LimitPointToPlayerReachableArea(ref verify); Require(verify == point, "chosen sample is still the same reachable point"); }
                Clear(); ZoomAndGravity(rod, p, correct);
                // Failure inside the helper returns the untouched local. The
                // real native method continues; ordinary native exceptions are
                // outside this containment and are never intercepted by it.
                hook.GetMethod("Uninstall", Flags).Invoke(null, null);
                string reported = null;
                Require((bool)hook.GetMethod("TryInstall", Flags).Invoke(null, new object[] { (Func<Player, bool>)(_ => { throw new InvalidOperationException("injected-permission"); }), (Action<string>)(message => reported = message) }), "failure-containment installation");
                Prepare(p); NativeUse(rod, p, 5335, new Vector2(1000, 680), new Vector2(1000, 680));
                Require(reported != null && !(bool)hook.GetProperty("Available", Flags).GetValue(null) && teleports == 1 && messages == 1,
                    "helper failure disables only correction and lets original local complete once");
                Console.WriteLine("PASS teleport accurate native local-use, timers, complete box,50/reach,wall350/wall87,chaos costs,normal/inverse-gravity,actual RenderZoom mouse mapping and helper containment; network=message65-entry-only cosmetics=sinks");
            }
            finally
            {
                permit = true; hook.GetMethod("Uninstall", Flags).Invoke(null, null);
                foreach (var method in h.GetPatchedMethods().ToArray()) h.Unpatch(method, HarmonyPatchType.All, h.Id);
                Main.remixWorld = false; NPC.downedPlantBoss = false;
            }
        }

        private static void CheckTranspiler(Type hook, MethodInfo rod)
        {
            var native = PatchProcessor.GetCurrentInstructions(rod);
            var transpiler = hook.GetMethod("Target", Flags);
            var result = ((System.Collections.Generic.IEnumerable<CodeInstruction>)transpiler.Invoke(null, new object[] { native, rod })).ToArray();
            Require(result.Length == native.Count + 4 && native.All(instruction => result.Contains(instruction)),
                "exact transpiler keeps every original instruction, label and exception marker");
            var limit = typeof(Player).GetMethod("LimitPointToPlayerReachableArea", Flags);
            var duplicate = native.ToList(); duplicate.Add(new CodeInstruction(System.Reflection.Emit.OpCodes.Call, limit));
            bool rejected = false;
            try { transpiler.Invoke(null, new object[] { duplicate, rod }); }
            catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { rejected = true; }
            Require(rejected, "ambiguous second native limiter fails closed rather than selecting the first");
            int at = native.FindIndex(instruction => instruction.Calls(limit));
            var changed = native.Select(instruction => new CodeInstruction(instruction.opcode, instruction.operand)).ToList();
            changed[at + 2] = new CodeInstruction(System.Reflection.Emit.OpCodes.Ldfld, typeof(Vector2).GetField(nameof(Vector2.Y)));
            rejected = false;
            try { transpiler.Invoke(null, new object[] { changed, rod }); }
            catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { rejected = true; }
            Require(rejected, "changed downstream native target consumer is an ABI failure");
        }

        private static void ZoomAndGravity(MethodInfo rod, Player p, Correct correct)
        {
            Main.screenPosition = new Vector2(200, 200); Main.screenWidth = 960; Main.screenHeight = 640;
            foreach (float zoom in new[] { .3f, 1f, 5f }) foreach (float gravity in new[] { 1f, -1f })
            {
                Clear(); Prepare(p); p.gravDir = gravity;
                Main.GameViewMatrix.Zoom = new Vector2(zoom);
                typeof(PlayerInput).GetField("_originalMouseX", Flags).SetValue(null, 600);
                typeof(PlayerInput).GetField("_originalMouseY", Flags).SetValue(null, 340);
                PlayerInput.SetZoom_MouseInWorld();
                Vector2 native = new Vector2(Main.mouseX + Main.screenPosition.X - p.width / 2,
                    gravity == 1 ? Main.mouseY + Main.screenPosition.Y - p.height : Main.screenPosition.Y + Main.screenHeight - Main.mouseY);
                p.LimitPointToPlayerReachableArea(ref native);
                int mx = Main.mouseX, my = Main.mouseY;
                NativeUseMapped(rod, p, 5335, native);
                Require(Main.mouseX == mx && Main.mouseY == my && teleported == native && p.position == native, "actual native mapped target is unchanged by local helper at actual zoom/gravity");
                Prepare(p); p.gravDir = gravity;
                Main.tile[(int)((native.X + p.width / 2) / 16), (int)((native.Y + p.height / 2) / 16)].active(true);
                Main.tile[(int)((native.X + p.width / 2) / 16), (int)((native.Y + p.height / 2) / 16)].type = 1;
                Vector2 expected = native; int count;
                Require(correct(p, ref expected, out count) && expected != native && Vector2.DistanceSquared(expected, native) <= 208 * 208,
                    "blocked real mapped destination obtains a bounded distinct candidate");
                NativeUseMapped(rod, p, 5335, expected);
                Require(Main.mouseX == mx && Main.mouseY == my && teleported == expected && !Collision.SolidCollision(p.position, p.width, p.height),
                    "blocked mapped target reaches actual native clear destination without global mouse writes");
                Console.WriteLine("TELEPORT mapping targetZoom=" + zoom + " RenderZoom=" + Main.GameViewMatrix.RenderZoom + " grav=" + gravity + " mouse=" + mx + "," + my + " target=" + native);
            }
            Main.GameViewMatrix.Zoom = Vector2.One; Main.screenPosition = Vector2.Zero;
        }
        private static int CountCircle() { int count = 0; for (int y = -26; y <= 26; y++) for (int x = -26; x <= 26; x++) if ((x != 0 || y != 0) && x * x + y * y <= 676) count++; return count; }
        private static void Measure(Correct correct, Player p, Vector2 origin, string label, int expected)
        {
            AppDomain.MonitoringIsEnabled = true;
            int count;
            for (int i = 0; i < 20; i++) { Vector2 point = origin; correct(p, ref point, out count); }
            var samples = new double[40];
            long allocation = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
            int gc0 = GC.CollectionCount(0);
            for (int i = 0; i < samples.Length; i++)
            {
                Vector2 point = origin; long start = Stopwatch.GetTimestamp(); correct(p, ref point, out count);
                samples[i] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                if (count != expected) throw new InvalidOperationException("native work count changed across identical uses");
            }
            long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - allocation;
            Require(allocated == 0 && GC.CollectionCount(0) == gc0, "warmed native destination query performs no managed allocation/GC: " + label);
            Array.Sort(samples);
            Console.WriteLine("TELEPORT cost label=" + label + " uses=40 candidates-per-use=" + expected + " median-ms=" + samples[20].ToString("R") + " p95-ms=" + samples[38].ToString("R") + " max-ms=" + samples[39].ToString("R") + " allocated=" + allocated + " gc0=0");
        }
        private static void Prepare(Player p)
        {
            p.position = new Vector2(640, 640); p.width = 20; p.height = 42; p.active = true; p.dead = false;
            p.gravDir = 1; p.chaosState = false; p.statLife = p.statLifeMax2 = 700; p.lifeRegenCount = 10; p.lifeRegenTime = 10;
            p.teleportTime = 0; p.teleportStyle = 0; p.environmentBuffImmunityTimer = 0; p.ClearBuff(88);
            Main.netMode = 0; Main.myPlayer = p.whoAmI; Main.player[Main.myPlayer] = p; Main.worldSurface = 20;
        }
        private static void Clear()
        { for (int x = 0; x < Main.maxTilesX; x++) for (int y = 0; y < Main.maxTilesY; y++) { Main.tile[x, y].active(false); Main.tile[x, y].wall = 0; Main.tile[x, y].liquid = 0; } }
        private static void NativeUse(MethodInfo rod, Player p, int type, Vector2 target, Vector2 expected)
        {
            Main.mouseX = (int)(target.X + p.width / 2 - Main.screenPosition.X);
            Main.mouseY = (int)(p.gravDir == 1 ? target.Y + p.height - Main.screenPosition.Y : Main.screenPosition.Y + Main.screenHeight - target.Y);
            NativeUseMapped(rod, p, type, expected);
        }
        private static void NativeUseMapped(MethodInfo rod, Player p, int type, Vector2 expected)
        {
            teleports = messages = permissions = limits = 0; teleported = new Vector2(float.NaN);
            p.itemAnimation = 20; p.itemTime = 0;
            // Only native branch inputs are fabricated; the original use method
            // consumes time and runs its own Teleport/buff/network branches.
            rod.Invoke(p, new object[] { new Item { type = type, useTime = 20 } });
            Require(p.position == expected, "real Teleport result=" + p.position + " expected=" + expected);
        }
        private static bool Permission(Player p) { permissions++; Require(p.itemTime == 20, "permission runs after native ApplyItemTime"); return permit; }
        private static bool SkipCosmetic() { return false; }
        private static void ObserveTeleport(Vector2 newPos) { teleports++; teleported = newPos; }
        private static void ObserveMessage(int msgType) { if (msgType == 65) messages++; }
        private static void ObserveLimit() { limits++; }
        private static HarmonyMethod Patch(string name) => new HarmonyMethod(typeof(NativeTeleportMovementChecks), name);
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
