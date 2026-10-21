using System;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Movement;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Exact native DashMovement/DoCommonDashHandle in the silent isolated
    // fixture. Full mapped Main.Update evidence belongs to MovementOriginal.
    internal static class NativeMovementDashChecks
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private static long sample;
        private static bool throwConsumer;
        private static Player watched;
        private static object watchedMovement, watchedInput;
        internal static void Run(object context)
        {
            object movement = Get(context, "Movement"), input = Get(context, "Input");
            Require(movement != null && (bool)Call(movement, "Available", 1), "ordinary full profile installed dash hooks");
            NativeQuickItemChecks.Until(() => (bool)Get(movement, "ControlsEnabled"));
            Require((ContinuousDashMode)Get(movement, "DashMode") == ContinuousDashMode.DoubleTap && !(bool)Call(movement, "IsEnabled", 1), "ordinary full profile defaults off/double tap");
            Require((bool)Call(movement, "SetDashMode", ContinuousDashMode.Hold), "disabled dash changes mode");
            Require((bool)Call(movement, "SetEnabled", 1, true), "enable current hold mode");
            NativeCombatLiveContextChecks.InitializeMount(); NativeCombatWorkerAssetChecks.Initialize();
            Terraria.DataStructures.ArmorSetBonuses.Initialize(); Terraria.DataStructures.ArmorSetBonuses.BuildLookup();
            for (int i = 0; i < Main.gore.Length; i++) if (Main.gore[i] == null) Main.gore[i] = new Gore();
            Player original = Main.LocalPlayer; int cases = 0;
            try
            {
                foreach (int type in new[] { 1, 2, 3, 5 })
                {
                    var p = Local(); p.dashType = type; p.solarShields = 2;
                    Stage(movement, input, p, false, true);
                    Require(p.velocity.X > 0 && p.dashDelay < 0 && p.dash == type, "native dash launch type=" + type);
                    Require(!p.controlDash && !p.releaseDash, "consumed borrowed edge is not rearmed type=" + type);
                    if (type == 3) Require(p.solarShields == 2 && p.solarDashing && !p.solarDashConsumedFlare, "native solar launch arms collision; flare is consumed only on first contact");
                    cases++;
                }
                // Vanilla final capability, rather than an armor whitelist,
                // supplies functional-slot/order/loadout and set constraints.
                foreach (int item in new[] { 977, 984, 3097 })
                {
                    var p = Local(); p.armor[3].SetDefaults(item); p.UpdateEquips(p.whoAmI); p.UpdateArmorSets(p.whoAmI);
                    Require(p.dashType == (item == 3097 ? 2 : 1), "native functional accessory settles capability " + item); cases++;
                }
                var shared = Local(); shared.Loadouts[1].Armor[3].SetDefaults(977); shared.Loadouts[1].Armor[3].favorited = true; shared.UpdateEquips(shared.whoAmI);
                Require(shared.dashType == 1 && shared.armor[3].IsAir, "native effective armor shares favorited other loadout"); cases++;
                var ordered = Local(); ordered.armor[3].SetDefaults(977); ordered.armor[4].SetDefaults(3097); ordered.UpdateEquips(ordered.whoAmI);
                Require(ordered.dashType == 2, "final original slot order overrides first accessory"); cases++;
                foreach (int start in new[] { 2763, 4982 })
                {
                    var p = Local(); for (int slot = 0; slot < 3; slot++) p.armor[slot].SetDefaults(start + slot); p.UpdateEquips(p.whoAmI); p.UpdateArmorSets(p.whoAmI);
                    Require(p.dashType == (start == 4982 ? 5 : 0), "native set eligibility includes solar without shield negative " + start); cases++;
                }
                for (int mount = 56; mount <= 65; mount++)
                {
                    foreach (bool equipment in new[] { false, true })
                    {
                        var p = Local(); Mount(p, mount); p.dashType = equipment ? 2 : 0;
                        Stage(movement, input, p, false, true);
                        bool expected = equipment || mount >= 62;
                        Require((p.velocity.X > 0 && p.dashDelay < 0) == expected, "native mount/equipment qualification " + mount + "/" + equipment);
                        if (mount >= 62) Require(p.dashType == 6 && p.dash == 6, "first native mount entry establishes type6 " + mount);
                        cases++;
                    }
                }
                var blocked = Local(); Mount(blocked, 5); blocked.dashType = 2; Stage(movement, input, blocked, false, true); Require(blocked.dashDelay == 0 && !blocked.controlDash, "forbidden mount retains native restriction");
                foreach (int delay in new[] { -1, 4 })
                { var p = Local(); p.dashType = 2; p.dashDelay = delay; Stage(movement, input, p, false, true); Require(!p.controlDash && p.releaseDash, "active/cooldown does not borrow a new edge"); }
                var native = Local(); native.dashType = 2; native.controlDash = native.releaseDash = true; Stage(movement, input, native, true, false);
                Require(native.velocity.X < 0 && !native.releaseDash && native.controlDash, "native dash key owns and consumes its own direction/edge");
                // Facing right, holding right, then pressing left while both
                // remain held must reach the real direction consumer as left.
                Call(movement, "SetEnabled", 1, false); Call(movement, "SetEnabled", 1, true);
                var dual = Local(); dual.dashType = 2; Stage(movement, input, dual, false, true); dual.dashDelay = 0; dual.velocity = Vector2.Zero; dual.direction = 1;
                Stage(movement, input, dual, true, true);
                Require(dual.velocity.X < 0 && !dual.controlLeft && !dual.controlRight, "recent-left dual key reaches original dash and ordinary zero controls are restored; vx=" + dual.velocity.X + " direction=" + dual.direction); cases++;
                var errorScope = new Harmony("JueMingR.Tests.Movement.DashFailure");
                try
                {
                    errorScope.Patch(typeof(Player).GetMethod("DoCommonDashHandle", F), postfix: new HarmonyMethod(typeof(NativeMovementDashChecks), nameof(ConsumerFailure)));
                    var p = Local(); p.dashType = 2; throwConsumer = true; bool threw = false;
                    try { Stage(movement, input, p, true, false); } catch (InvalidOperationException e) { threw = e.Message == "owned-dash-test"; }
                    Require(threw && !p.controlDash && !p.releaseDash && p.controlLeft && !p.controlRight, "original consumer exception survives and own inputs close without rearming consumed dash");
                }
                finally { throwConsumer = false; errorScope.UnpatchAll(errorScope.Id); }
                var standalone = Local(); standalone.dashType = 2; standalone.DashMovement(); Require(standalone.velocity.X == 0, "isolated consumer without an admitted scope is not injected");
                Lifecycle(context, movement, input);
                RemoteAdmission(context, movement, input);
                NativeMappedDeath(context, movement, input);
                Console.WriteLine("PASS MOVEMENT DASH native capability/input/consumer cases=" + cases + "; native DashMovement/DoCommonDashHandle; full Main mapping separate");
            }
            finally { Call(movement, "SetEnabled", 1, false); Main.player[Main.myPlayer] = original; }
        }
        private static void ConsumerFailure() { if (throwConsumer) throw new InvalidOperationException("owned-dash-test"); }
        private static int Selected(object movement) { return (int)Get(Get(movement, "DashHost"), "direction"); }
        private static void EmptyOuter(object context, object movement, object input, Player p)
        {
            long mapping = (long)Get(input, "Mapping"), tick = (long)Get(movement, "tick"), version = (long)Get(movement, "intentVersion");
            int selected = Selected(movement), delay = p.dashDelay; Vector2 velocity = p.velocity;
            Call(input, "BeginUpdate"); Call(movement, "ObserveInput"); Call(context, "UpdateRuntime");
            Require(!(bool)Get(input, "CanStartActions") && !(bool)Call(movement, "CanAct", p), "empty outer epoch never admits stale native action");
            Require((long)Get(input, "Mapping") == mapping && (long)Get(movement, "tick") == tick && (long)Get(movement, "intentVersion") == version && Selected(movement) == selected && p.dashDelay == delay && p.velocity == velocity,
                "production Begin/Observe/Runtime empty outer preserves gesture/version without mapping, world tick or native action");
        }
        private static void Lifecycle(object context, object movement, object input)
        {
            Call(movement, "SetDashMode", ContinuousDashMode.DoubleTap);
            foreach (int type in new[] { 1, 2, 3, 5, 6 })
            {
                var p = Local(); p.dashType = type; p.solarShields = 2;
                if (type == 6) Mount(p, 62);
                Call(context, "UpdateRuntime");
                Stage(movement, input, p, false, true);
                Require(Selected(movement) == 0, "first held press does not arm type=" + type);
                EmptyOuter(context, movement, input, p);
                Stage(movement, input, p, false, false); EmptyOuter(context, movement, input, p);
                Stage(movement, input, p, false, true);
                Require(Selected(movement) == 1 && p.dashDelay < 0, "second press immediately arms/launches through native type=" + type);
                // Native mounted active-dash dust needs the real loaded assets.
                // Its natural repeat is exercised in MappedDoubleTap below;
                // this CPU fixture proves the independent type6 first edge.
                if (type == 6) { Console.WriteLine("MOVEMENT DOUBLE TAP type=6 native-first-launch=true natural-repeat=NOT_RUN-requires-loaded-assets"); continue; }
                int launches = 1, cooldown = 0;
                for (int k = 0; k < 240 && launches < 3; k++)
                {
                    int before = p.dashDelay;
                    if (before > 0) cooldown++;
                    if (k % 3 == 0) EmptyOuter(context, movement, input, p);
                    Stage(movement, input, p, false, true);
                    if (before >= 0 && p.dashDelay < 0) launches++;
                }
                Require(launches >= 3 && cooldown > 0, "native active/cooldown alone permits second/third launch type=" + type + " launches=" + launches + " cooldown=" + cooldown);
                Stage(movement, input, p, false, false); Stage(movement, input, p, false, false); Stage(movement, input, p, false, false);
                Require(Selected(movement) == 0, "true sampled neutral retires armed after grace");
                Console.WriteLine("MOVEMENT DOUBLE TAP type=" + type + " native-launches=" + launches + " native-cooldown-ticks=" + cooldown + " empty-outer=true no-manual-cooldown-reset=true");
            }
            var first = Local(); first.dashType = 2; Call(context, "UpdateRuntime");
            for (int k = 0; k < 24; k++) { Stage(movement, input, first, false, true); EmptyOuter(context, movement, input, first); }
            Require(Selected(movement) == 0, "holding only first press across empty outers never arms");
            Arm(movement, input, first);
            Stage(movement, input, first, true, true);
            Require(Selected(movement) == 0, "newer left while both held retires old right doubletap arm");
            Stage(movement, input, first, false, false); Stage(movement, input, first, true, false);
            Require(Selected(movement) == -1, "second newer-left press arms opposite direction by existing doubletap rules");
            Arm(movement, input, first);
            long jumpVersion = (long)Get(movement, "intentVersion");
            Call(movement, "SetDashMode", ContinuousDashMode.Hold); Call(movement, "SetDashMode", ContinuousDashMode.DoubleTap);
            Require(Selected(movement) == 0 && (long)Get(movement, "intentVersion") == jumpVersion, "dash mode cancellation does not alter jump intent version");
            Arm(movement, input, first); Call(movement, "SetEnabled", 1, false); Call(movement, "SetEnabled", 1, true);
            Stage(movement, input, first, false, true); Require(Selected(movement) == 0, "disable/re-enable does not inherit double tap");
            foreach (string barrier in new[] { "text", "capture", "focus" })
            {
                Arm(movement, input, first);
                if (barrier == "text") Main.drawingPlayerChat = true;
                else if (barrier == "capture") Set(input, "HotkeyCapture", true);
                else Set(input, "foregroundWindow", (Func<IntPtr>)(() => IntPtr.Zero));
                Call(input, "BeginUpdate"); Call(movement, "ObserveInput");
                long version = (long)Get(movement, "intentVersion");
                Call(movement, "ObserveInput"); Call(movement, "Update", (ulong)0);
                Require(Selected(movement) == 0 && (long)Get(movement, "intentVersion") == version, "real barrier cancellation is idempotent " + barrier);
                Main.drawingPlayerChat = false; Set(input, "HotkeyCapture", false); Set(input, "foregroundWindow", (Func<IntPtr>)(() => new IntPtr(1)));
                NativeQuickItemChecks.Sample(input, new Keys[0]); NativeQuickItemChecks.Sample(input, new Keys[0]);
                Stage(movement, input, first, false, true); Require(Selected(movement) == 0, "barrier recovery requires new double tap " + barrier);
            }
            Arm(movement, input, first); first.dead = true;
            Require(!(bool)Call(movement, "BeginUpdate", first) && Selected(movement) == 0, "dead Player Begin early return cancels retained gesture");
            first.dead = false; Stage(movement, input, first, false, true); Require(Selected(movement) == 0, "same instance alive again cannot inherit arm");
            Arm(movement, input, first); var replacement = Local(); replacement.dashType = 2;
            Stage(movement, input, replacement, false, true); Require(Selected(movement) == 0, "new local Player cannot inherit earlier gesture before runtime identity refresh");
            Arm(movement, input, replacement); Call(movement, "OnSessionEnded"); Call(movement, "OnSessionStarted");
            Stage(movement, input, replacement, false, true); Require(Selected(movement) == 0, "session edge requires fresh double tap");
            // Finish the fixture's deliberate player replacement before D8.
            // A real Runtime identity transition is a cancellation, not an
            // empty-epoch preservation case.
            Call(context, "UpdateRuntime");
            Call(movement, "SetEnabled", 0, true);
            var jumpHost = Get(movement, "JumpHost");
            foreach (bool down in new[] { false, true })
            {
                NativeQuickItemChecks.Sample(input, new[] { Keys.Space });
                Terraria.GameInput.PlayerInput.Triggers.Current.Jump = true;
                replacement.controlJump = true; replacement.controlDown = false; replacement.velocity.Y = 0; replacement.jump = 0;
                Call(movement, "ObserveInput"); Require((bool)Call(movement, "BeginUpdate", replacement), "fresh jump scope");
                try { Call(movement, "CaptureInput", replacement); Call(movement, "BeforeJump", replacement); replacement.JumpMovement(); Call(movement, "AfterJump", replacement); }
                finally { Call(movement, "EndUpdate", replacement); }
                Require((bool)Get(jumpHost, "intent"), "real scoped JumpMovement published intent");
                long published = (long)Get(jumpHost, "version"), inputVersion = (long)Get(movement, "intentVersion");
                Call(movement, "SetDashMode", ContinuousDashMode.Hold); Call(movement, "SetDashMode", ContinuousDashMode.DoubleTap);
                Call(movement, "SetEnabled", 1, false); Call(movement, "SetEnabled", 1, true);
                Require((bool)Get(jumpHost, "intent") && (long)Get(jumpHost, "version") == published && (long)Get(movement, "intentVersion") == inputVersion,
                    "dash mode and switch changes cannot cancel an actually published jump premise");
                Call(movement, "ObserveInput"); Call(movement, "ObserveInput"); EmptyOuter(context, movement, input, replacement);
                Require((bool)Get(jumpHost, "intent") && (long)Get(jumpHost, "version") == published && (long)Get(movement, "intentVersion") == inputVersion,
                    "duplicate observation and empty outer preserve published jump premise/version");
                NativeQuickItemChecks.Sample(input, new Keys[0]);
                Terraria.GameInput.PlayerInput.Triggers.Current.Jump = down; Terraria.GameInput.PlayerInput.Triggers.Current.Down = down;
                Call(movement, "ObserveInput");
                long cancelled = (long)Get(jumpHost, "version"), cancelInput = (long)Get(movement, "intentVersion");
                Call(movement, "ObserveInput"); Call(movement, "Update", (ulong)0);
                Require(!(bool)Get(jumpHost, "intent") && cancelled > published && cancelInput == inputVersion + 1 && (long)Get(jumpHost, "version") == cancelled && (long)Get(movement, "intentVersion") == cancelInput,
                    "one fresh release/Down revokes published intent once, repeated cancellation is idempotent Down=" + down);
            }
            Call(movement, "SetEnabled", 0, false);
            Console.WriteLine("PASS MOVEMENT lifecycle D1-D6/D8 producer seams; exact native mapped/death sequence follows separately");
        }
        private static object[] AdmissionState(object movement, object input, Player local, Player other)
        {
            var dash = Get(movement, "DashHost"); var jump = Get(movement, "JumpHost");
            return new[] { Get(dash, "gesture"), Get(dash, "direction"), Get(dash, "sampledTick"),
                GetOptional(movement, "player"), Get(movement, "frame"), Get(movement, "tick"), Get(movement, "intentVersion"),
                Get(movement, "intentPlayer"), Get(movement, "intentGeneration"), Get(movement, "observedMapping"),
                Get(jump, "intent"), Get(jump, "phase"), Get(jump, "version"), Get(jump, "active"), GetOptional(jump, "player"),
                Get(input, "Frame"), Get(input, "Mapping"), Get(input, "CanStartActions"),
                local.controlJump, local.controlDown, local.controlLeft, local.controlRight, local.releaseJump,
                other == null ? null : (object)other.controlJump, other == null ? null : (object)other.controlDash };
        }
        private static void UnownedAdmission(object movement, object input, Player local, Player other, string label)
        {
            // Compare immediately around the production admission, without a
            // local sample/update in between; native local transitions may advance.
            var before = AdmissionState(movement, input, local, other);
            Require(!(bool)Call(movement, "BeginUpdate", other), "unowned admission refused " + label);
            var after = AdmissionState(movement, input, local, other);
            for (int i = 0; i < before.Length; i++) Require(Equals(before[i], after[i]), "unowned admission leaves local state/input unchanged " + label + " field=" + i);
            Require(!(bool)Call(movement, "CanAct", other), "unowned admission lends no action " + label);
            Console.WriteLine("PASS MOVEMENT remote admission " + label + "; immediate state/version/input unchanged; no scope/action");
        }
        private static void RemoteAdmission(object context, object movement, object input)
        {
            Call(movement, "SetDashMode", ContinuousDashMode.DoubleTap);
            var p = Local(); p.dashType = 2; Call(context, "UpdateRuntime");
            var remote = new Player { whoAmI = (Main.myPlayer + 1) % Main.maxPlayers, active = true, controlJump = true };
            var sameNumber = new Player { whoAmI = Main.myPlayer, active = true, controlJump = true };
            Stage(movement, input, p, false, true);
            Require((int)Get(Get(Get(movement, "DashHost"), "gesture"), "firstDirection") == 1, "real first press retained after normal End");
            UnownedAdmission(movement, input, p, remote, "dash-first/remote");
            UnownedAdmission(movement, input, p, sameNumber, "dash-first/same-whoAmI-different-reference");
            Stage(movement, input, p, false, false); Stage(movement, input, p, false, true);
            Require(Selected(movement) == 1 && p.dashDelay < 0, "first press survives remote admission and second press launches natively");
            Require((int)Get(Get(Get(movement, "DashHost"), "gesture"), "armed") == 1, "real double tap produced armed state");
            UnownedAdmission(movement, input, p, remote, "dash-armed/remote");
            UnownedAdmission(movement, input, p, sameNumber, "dash-armed/same-whoAmI-different-reference");
            int me = Main.myPlayer; Player slot = Main.player[me];
            try
            {
                Main.myPlayer = -1; UnownedAdmission(movement, input, p, remote, "invalid-negative-index");
                Main.myPlayer = Main.player.Length; UnownedAdmission(movement, input, p, remote, "index-at-array-length");
                Main.myPlayer = me; Main.player[me] = null; UnownedAdmission(movement, input, p, remote, "empty-local-slot");
                UnownedAdmission(movement, input, p, null, "null-value-and-slot");
            }
            finally { Main.myPlayer = me; Main.player[me] = slot; }
            Stage(movement, input, p, false, true); Require(Selected(movement) == 1, "armed local continues after unowned/unknown-local admissions");
            Call(movement, "SetEnabled", 0, true);
            var jump = Get(movement, "JumpHost");
            float speed = Player.jumpSpeed; int height = Player.jumpHeight;
            try
            {
                Player.jumpSpeed = 5.01f; Player.jumpHeight = 15;
                p = Local(); Call(context, "UpdateRuntime");
                JumpStage(movement, input, p);
                Require((bool)Get(jump, "intent") && Get(jump, "phase").ToString() == "Held" && p.jump > 0, "real scoped JumpMovement produces intent/Held");
                UnownedAdmission(movement, input, p, remote, "jump-intent-Held/remote");
                UnownedAdmission(movement, input, p, sameNumber, "jump-intent-Held/same-whoAmI-different-reference");
                int held = p.jump;
                for (int k = 0; k < held; k++) JumpStage(movement, input, p);
                Require(p.jump == 0 && p.velocity.Y < 0 && Get(jump, "phase").ToString() == "Coast", "native held countdown produces Coast without assigning phase");
                UnownedAdmission(movement, input, p, remote, "jump-Coast/remote");
                UnownedAdmission(movement, input, p, sameNumber, "jump-Coast/same-whoAmI-different-reference");
                JumpStage(movement, input, p);
                Require((bool)Get(jump, "intent") && Get(jump, "phase").ToString() == "Coast", "local Coast continues after remote admission");
                p.ghost = true;
                Require(!(bool)Call(movement, "BeginUpdate", p) && !(bool)Get(jump, "intent") && Get(jump, "phase").ToString() == "None", "current local ghost still cancels actual published state");
                p.ghost = false; JumpStage(movement, input, p);
                Require(Get(jump, "phase").ToString() == "None", "same local recovery cannot inherit old Coast");
                p.velocity.Y = 0; p.jump = 0; JumpStage(movement, input, p);
                Require(Get(jump, "phase").ToString() == "Held", "fresh native jump establishes state before Runtime edge");
                var runtime = Get(movement, "runtime"); long generation = (long)Get(runtime, "Generation");
                Call(runtime, "InvalidateSession"); Call(context, "UpdateRuntime");
                Require((long)Get(runtime, "Generation") > generation && !(bool)Get(jump, "intent") && Get(jump, "phase").ToString() == "None",
                    "actual Runtime generation transition clears same-player published intent/phase");
            }
            finally { Player.jumpSpeed = speed; Player.jumpHeight = height; Call(movement, "SetEnabled", 0, false); }
        }
        private static void JumpStage(object movement, object input, Player p)
        {
            NativeQuickItemChecks.Sample(input, new[] { Keys.Space });
            Terraria.GameInput.PlayerInput.Triggers.Current.Jump = true;
            p.controlJump = true; p.controlDown = false;
            Require((bool)Call(movement, "BeginUpdate", p), "real local jump scope admitted");
            try { Call(movement, "CaptureInput", p); Call(movement, "BeforeJump", p); p.JumpMovement(); Call(movement, "AfterJump", p); }
            finally { Call(movement, "EndUpdate", p); }
        }
        private static void Arm(object movement, object input, Player p)
        {
            Call(movement, "SetDashMode", ContinuousDashMode.Hold); Call(movement, "SetDashMode", ContinuousDashMode.DoubleTap);
            Stage(movement, input, p, false, false); Stage(movement, input, p, false, true);
            Stage(movement, input, p, false, false); Stage(movement, input, p, false, true);
            Require(Selected(movement) == 1, "fresh double tap arm setup");
        }
        private static bool CosmeticSink() { return false; }
        private static void NativeMappedDeath(object context, object movement, object input)
        {
            int mode = Main.GameMode, spawnX = Main.spawnTileX, spawnY = Main.spawnTileY;
            var chroma = Main.ChromaPainter;
            var sinks = new Harmony("JueMingR.Tests.Movement.DeathCosmetics");
            var spawn = typeof(Player).GetMethod("Spawn", F, null, new[] { typeof(PlayerSpawnContext) }, null);
            var recall = typeof(NativeQuickItemChecks).GetMethod("Recall", F);
            var fixture = new Harmony("JueMingR.Tests.QuickItemOutlets");
            fixture.Unpatch(spawn, recall);
            try
            {
                // Preserve original death/state/spawn bodies. These exact
                // unrelated outlets need assets/UI or write a character file;
                // the existing isolated fixture owns no such acceptance claim.
                foreach (var target in new[] {
                    typeof(Player).GetMethod("KillMe_DustExplosion", F), typeof(Player).GetMethod("PlayDeathSound", F),
                    typeof(Player).GetMethod("DropTombstone", F), typeof(WorldGen).GetMethod("saveToonWhilePlaying", F),
                    typeof(Terraria.Chat.ChatHelper).GetMethod("BroadcastChatMessage", F) })
                { Require(target != null, "exact native death cosmetic/save outlet exists"); sinks.Patch(target, prefix: new HarmonyMethod(typeof(NativeMovementDashChecks), nameof(CosmeticSink))); }
                Main.ChromaPainter = new Terraria.GameContent.ChromaHotkeyPainter(); Main.GameMode = 1; Main.spawnTileX = Main.spawnTileY = 60;
                var p = Local(); p.name = "MOVEMENT isolated death"; p.statLife = p.statLifeMax = p.statLifeMax2 = 400;
                p.armor[3].SetDefaults(3097); p.UpdateEquips(p.whoAmI); p.UpdateArmorSets(p.whoAmI);
                Require(p.dashType == 2, "real expert Cthulhu functional accessory supplies mapped test capability");
                Call(movement, "SetDashMode", ContinuousDashMode.Hold); Call(movement, "SetDashMode", ContinuousDashMode.DoubleTap);
                var none = new Keys[0]; var right = new[] { Keys.D };
                MappedStage(context, movement, input, p, none); MappedStage(context, movement, input, p, right);
                Require(Selected(movement) == 0, "native KeyboardInput first mapped press is not armed");
                MappedStage(context, movement, input, p, none); EmptyOuter(context, movement, input, p); MappedStage(context, movement, input, p, right);
                Require(Selected(movement) == 1 && p.dashDelay < 0, "native KeyboardInput/final sampling/CopyInto second press launches actual equipped shield");
                int launches = 1, cooldown = 0;
                for (int k = 0; k < 240 && launches < 3; k++)
                {
                    int delay = p.dashDelay; if (delay > 0) cooldown++;
                    if (k % 3 == 0) EmptyOuter(context, movement, input, p);
                    MappedStage(context, movement, input, p, right); if (delay >= 0 && p.dashDelay < 0) launches++;
                }
                Require(launches >= 3 && cooldown > 0 && GetOptional(movement, "player") == null, "three mapped native launches/natural cooldown finish normal End before death");
                long generation = (long)Get(Get(movement, "runtime"), "Generation");
                p.KillMe(Terraria.DataStructures.PlayerDeathReason.ByCustomReason("MOVEMENT isolated lifecycle"), 1000, 0);
                Require(p.dead && Main.LocalPlayer == p && !Main.dedServ, "actual KillMe marks same local client dead after closed movement scope");
                // Keep direction physically held. The explicit Begin is the
                // same production gate reached before native dead early-return.
                Require(!(bool)Call(movement, "BeginUpdate", p) && Selected(movement) == 0, "dead Begin cancels without Capture or global Session exit");
                NativeMappedSample(context, movement, input, right); NativeMappedSample(context, movement, input, right);
                int deadTime = p.deadTime; p.Update(Main.myPlayer);
                Require(p.dead && p.deadTime > deadTime, "original Player.Update executes original UpdateDead early return");
                p.Spawn(PlayerSpawnContext.ReviveFromDeath);
                Require(ReferenceEquals(Main.LocalPlayer, p) && !p.dead && (long)Get(Get(movement, "runtime"), "Generation") == generation, "original Spawn revives identical Player without a Runtime generation edge");
                int replay = 0;
                for (int k = 0; k < 120; k++) { int delay = p.dashDelay; MappedStage(context, movement, input, p, right); if (delay >= 0 && p.dashDelay < 0) replay++; }
                Require(Selected(movement) == 0 && replay == 0, "held mapped direction after same-instance native revive never inherits old doubletap");
                MappedStage(context, movement, input, p, none); MappedStage(context, movement, input, p, right); MappedStage(context, movement, input, p, none); MappedStage(context, movement, input, p, right);
                Require(Selected(movement) == 1 && p.dashDelay < 0, "new native mapped doubletap works after revival");
                Console.WriteLine("PASS MOVEMENT MAPPED CPU original KeyboardInput -> final sample -> CopyInto -> shield capability -> native Dash launches=" + launches + " cooldown=" + cooldown + "; normal End -> KillMe -> Player.Update/UpdateDead -> identical Spawn -> held no replay -> new doubletap; cosmetics/chat/save=sinks; fullMain/physicalKeyboard=NOT_EVALUATED");
            }
            finally { sinks.UnpatchAll(sinks.Id); fixture.Patch(spawn, prefix: new HarmonyMethod(recall)); Main.GameMode = mode; Main.spawnTileX = spawnX; Main.spawnTileY = spawnY; Main.ChromaPainter = chroma; }
        }
        private static void NativeMappedSample(object context, object movement, object input, Keys[] keys)
        {
            Call(input, "BeginUpdate");
            Terraria.GameInput.PlayerInput.MouseInfo = new MouseState(200, 200, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            Terraria.GameInput.PlayerInput.MouseKeys.Clear(); Terraria.GameInput.PlayerInput.Triggers.Reset();
            typeof(Terraria.GameInput.PlayerInput).GetMethod("KeyboardInput", F).Invoke(null, null);
            Terraria.GameInput.PlayerInput.Triggers.Update(); Call(input, "AfterMapping");
            Main.oldKeyState = Main.keyState; Main.keyState = new KeyboardState(keys); Call(input, "AfterKeyboardRefresh");
            Call(movement, "ObserveInput"); Call(context, "UpdateRuntime");
        }
        private static void MappedStage(object context, object movement, object input, Player p, Keys[] keys)
        {
            NativeMappedSample(context, movement, input, keys); NativeMappedSample(context, movement, input, keys);
            Require((bool)Call(movement, "BeginUpdate", p), "native mapped local scope admitted");
            try { typeof(Player).GetMethod("ResetControls", F).Invoke(p, null); Terraria.GameInput.PlayerInput.Triggers.Current.CopyInto(p); Call(movement, "CaptureInput", p); p.DashMovement(); CloseDirectionSample(p); }
            finally { Call(movement, "EndUpdate", p); }
        }
        internal static void Mapped(object context, Action<Keys[]> input, Action world)
        {
            var movement = Get(context, "Movement");
            Call(movement, "SetEnabled", 0, false); Call(movement, "SetEnabled", 1, false); Call(movement, "SetDashMode", ContinuousDashMode.Hold); Call(movement, "SetEnabled", 1, true);
            Player saved = Main.LocalPlayer;
            var witness = new Harmony("JueMingR.Tests.Movement.MappedDashWitness");
            try
            {
                watchedMovement = movement; watchedInput = Get(context, "Input");
                MappedDoubleTap(context, movement, watchedInput, input, world);
                Call(movement, "SetDashMode", ContinuousDashMode.Hold);
                witness.Patch(typeof(Player).GetMethod("DashMovement", F), prefix: new HarmonyMethod(typeof(NativeMovementDashChecks), nameof(DashWitness)));
                witness.Patch(movement.GetType().Assembly.GetType("JueMingR.TerrariaHost.Movement.MovementHooks").GetMethod("Capture", F), prefix: new HarmonyMethod(typeof(NativeMovementDashChecks), nameof(CaptureWitness)));
                foreach (int direction in new[] { -1, 1 })
                {
                    var p = Local(); watched = p; p.position = new Vector2(6400, 225 * 16 - p.height); p.fallStart = p.fallStart2 = (int)(p.position.Y / 16); p.statLife = p.statLifeMax = p.statLifeMax2 = 400; p.armor[3].SetDefaults(977);
                    if (Main.ActivePlayerFileData != null) Main.ActivePlayerFileData.Player = p;
                    input(new Keys[0]); input(new Keys[0]); world();
                    Keys first = direction == -1 ? Keys.D : Keys.A, next = direction == -1 ? Keys.A : Keys.D;
                    // The native input phase maps the previous finalized
                    // Main.keyState. Supply both real opportunities, as the
                    // established View original harness does for mapped keys.
                    input(new[] { first }); input(new[] { first }); world(); p.dashDelay = 0; p.velocity = Vector2.Zero; p.direction = -direction;
                    input(new[] { first, next }); input(new[] { first, next }); world();
                    Require(p.dashType == 1 && p.dashDelay < 0 && Math.Sign(p.velocity.X) == direction && !p.controlLeft && !p.controlRight, "real mapped dual key latest direction reaches actual native launch " + direction + "; vx=" + p.velocity.X + " delay=" + p.dashDelay + " controls=" + p.controlLeft + "/" + p.controlRight + " active/dead/ghost=" + p.active + "/" + p.dead + "/" + p.ghost + " life=" + p.statLife + " who=" + p.whoAmI + " my=" + Main.myPlayer + " film=" + p.isControlledByFilm);
                }
                watched = null;
                foreach (int type in new[] { 2, 3 })
                {
                    var p = Local(); p.position = new Vector2(6400, 225 * 16 - p.height); p.fallStart = p.fallStart2 = (int)(p.position.Y / 16); p.statLife = p.statLifeMax = p.statLifeMax2 = 400; p.immune = true;
                    // This second fixture intentionally covers the expert-only
                    // shield under its real native equipment qualification.
                    Main.GameMode = 1;
                    if (Main.ActivePlayerFileData != null) Main.ActivePlayerFileData.Player = p;
                    if (type == 2) p.armor[3].SetDefaults(3097);
                    else { for (int slot = 0; slot < 3; slot++) p.armor[slot].SetDefaults(2763 + slot); }
                    input(new Keys[0]); input(new Keys[0]); world();
                    // A new player has no previous setSolar fact. Vanilla
                    // deletes a pre-seeded shield buff before the first armor
                    // pass, so earn the first flare through the real set timer.
                    if (type == 3)
                    {
                        for (int warm = 0; warm < 182; warm++) { input(new Keys[0]); world(); }
                        Require(p.setSolar && p.solarShields > 0, "original equipped solar set earns its first flare before mapped launch");
                    }
                    input(new[] { Keys.D }); input(new[] { Keys.D }); world();
                    Require(p.dashDelay < 0 && p.dash == type, "mapped collision scenario actually launches original type=" + type);
                    int shields = p.solarShields;
                    var npc = Main.npc[0]; npc.SetDefaults(1); npc.active = true; npc.friendly = false; npc.dontTakeDamage = false; npc.noTileCollide = true; npc.life = npc.lifeMax = 9999; npc.position = p.position + new Vector2(12, 0); npc.immune[p.whoAmI] = 0;
                    int life = npc.life;
                    input(new[] { Keys.D }); world();
                    Require(npc.life < life, "original dash collision damages real NPC type=" + type);
                    if (type == 2) Require(p.eocHit == 0 && p.dashDelay > 0, "original shield collision sets native hit and cooldown");
                    else
                    {
                        Require(p.solarDashConsumedFlare && p.solarShields == shields - 1, "original first solar collision consumes exactly one flare");
                        int remaining = p.solarShields; npc.immune[p.whoAmI] = 0; npc.position = p.position + new Vector2(12, 0);
                        input(new[] { Keys.D }); world(); Require(p.solarShields == remaining, "later collision in same dash does not consume a second flare");
                    }
                    npc.active = false;
                }
                Console.WriteLine("PASS MOVEMENT DASH real native mapping -> Main world -> Player.Update -> dual-key capture -> DoCommonDashHandle -> launch both directions");
            }
            finally { watched = null; watchedMovement = watchedInput = null; witness.UnpatchAll(witness.Id); Call(movement, "SetEnabled", 1, false); Main.player[Main.myPlayer] = saved; if (Main.ActivePlayerFileData != null) Main.ActivePlayerFileData.Player = saved; }
        }
        private static void CaptureWitness(Player player)
        { if (ReferenceEquals(player, watched)) Console.WriteLine("MOVEMENT DASH witness=capture controls=" + player.controlLeft + "/" + player.controlRight + " map=" + Get(watchedInput, "Mapping")); }
        private static void DashWitness(Player __instance)
        { if (ReferenceEquals(__instance, watched)) Console.WriteLine("MOVEMENT DASH witness=native dashType=" + __instance.dashType + " controls=" + __instance.controlLeft + "/" + __instance.controlRight + " dash=" + __instance.controlDash + "/" + __instance.releaseDash + " selected=" + Get(Get(watchedMovement, "DashHost"), "direction") + " permission=" + Call(watchedMovement, "CanAct", __instance) + " input=" + Get(watchedInput, "CanStartActions")); }
        private static void MappedDoubleTap(object context, object movement, object hostInput, Action<Keys[]> input, Action world)
        {
            Call(movement, "SetDashMode", ContinuousDashMode.DoubleTap);
            Main.GameMode = 1;
            var p = Local(); p.position = new Vector2(6400, 225 * 16 - p.height); p.fallStart = p.fallStart2 = (int)(p.position.Y / 16);
            p.statLife = p.statLifeMax = p.statLifeMax2 = 400; p.immune = true; p.immuneTime = 100000; p.armor[3].SetDefaults(3097);
            if (Main.ActivePlayerFileData != null) Main.ActivePlayerFileData.Player = p;
            Action<Keys[]> mapped = keys => { input(keys); input(keys); world(); };
            var none = new Keys[0]; var right = new[] { Keys.D };
            mapped(none); mapped(right); Require(Selected(movement) == 0, "mapped first press alone does not arm");
            mapped(none); EmptyOuter(context, movement, hostInput, p); mapped(right);
            Require(p.dashType == 2 && p.dashDelay < 0 && Selected(movement) == 1, "real expert Cthulhu equipment second press starts armed native dash");
            int launches = 1, cooldown = 0;
            for (int k = 0; k < 280 && launches < 3; k++)
            {
                int delay = p.dashDelay; if (delay > 0) cooldown++;
                if (k % 3 == 0) EmptyOuter(context, movement, hostInput, p);
                mapped(right); if (delay >= 0 && p.dashDelay < 0) launches++;
            }
            Require(launches >= 3 && cooldown > 0 && !p.dead, "mapped held second press survives natural world cooldown for three launches; count=" + launches + " cooldown=" + cooldown);
            Require(GetOptional(movement, "player") == null, "normal Player EndUpdate has closed before later world death");
            // The native world orders NPC/projectile damage after Player.Update.
            // Reproduce that exact post-End ordering without a synthetic killer.
            p.creativeGodMode = false; p.KillMe(Terraria.DataStructures.PlayerDeathReason.ByCustomReason("MOVEMENT owned death lifecycle"), 1000, 0);
            Require(p.dead, "real KillMe reached dead state after normal EndUpdate");
            mapped(right); Require(Selected(movement) == 0, "native dead early-return cannot retain armed gesture");
            p.Spawn(PlayerSpawnContext.ReviveFromDeath);
            Require(ReferenceEquals(Main.LocalPlayer, p) && !p.dead, "native Spawn revives the exact same Player instance");
            for (int k = 0; k < 8; k++) mapped(right);
            Require(Selected(movement) == 0 && p.dashDelay >= 0, "same-instance native revive while direction held requires a new double tap");
            mapped(none); mapped(right); mapped(none); mapped(right);
            Require(Selected(movement) == 1 && p.dashDelay < 0, "new real double tap after native revive works");
            Console.WriteLine("PASS MOVEMENT mapped DoubleTap Cthulhu native-launches=" + launches + " native-cooldown-ticks=" + cooldown + " empty-outer=true; post-End KillMe -> UpdateDead -> same-object Spawn -> held no replay -> new doubletap PASS");
            p = Local(); p.position = new Vector2(6400, 225 * 16 - p.height); p.fallStart = p.fallStart2 = (int)(p.position.Y / 16);
            p.statLife = p.statLifeMax = p.statLifeMax2 = 400; p.immune = true; p.immuneTime = 100000;
            if (Main.ActivePlayerFileData != null) Main.ActivePlayerFileData.Player = p;
            p.mount.SetMount(62, p);
            p.position.Y = 225 * 16 - p.height; p.fallStart = p.fallStart2 = (int)(p.position.Y / 16);
            mapped(none); mapped(right); mapped(none); EmptyOuter(context, movement, hostInput, p); mapped(right);
            Require(p.dash == 6 && p.dashDelay < 0 && Selected(movement) == 1, "native type6 mount second press arms without inventing equipment capability");
            launches = 1; cooldown = 0;
            for (int k = 0; k < 280 && launches < 3; k++)
            {
                int delay = p.dashDelay; if (delay > 0) cooldown++;
                if (k % 3 == 0) EmptyOuter(context, movement, hostInput, p);
                mapped(right); if (delay >= 0 && p.dashDelay < 0) launches++;
            }
            Require(launches >= 3 && cooldown > 0, "loaded native type6 mount repeats through natural active/cooldown; launches=" + launches);
            Console.WriteLine("PASS MOVEMENT mapped type6 mount native-launches=" + launches + " native-cooldown-ticks=" + cooldown + " loaded-assets=true no-manual-cooldown-reset=true");
        }
        private static Player Local()
        { var p = new Player { whoAmI = Main.myPlayer, active = true, position = new Vector2(700, 900), releaseDash = true, direction = 1 }; Main.player[Main.myPlayer] = p; return p; }
        private static void Mount(Player p, int type)
        { int mode = Main.netMode, me = Main.myPlayer; bool dedicated = Main.dedServ; try { Main.netMode = 2; Main.dedServ = true; Main.myPlayer = 1; p.mount.SetMount(type, p); } finally { Main.netMode = mode; Main.myPlayer = me; Main.dedServ = dedicated; } }
        private static void Stage(object movement, object input, Player p, bool left, bool right)
        {
            NativeQuickItemChecks.Sample(input, new Keys[0]);
            p.controlLeft = left; p.controlRight = right;
            Require((bool)Call(movement, "BeginUpdate", p), "stage real-local scope admitted " + ++sample);
            try { Call(movement, "CaptureInput", p); if (left && right) p.controlLeft = p.controlRight = false; p.DashMovement(); CloseDirectionSample(p); }
            finally { Call(movement, "EndUpdate", p); }
        }
        private static void CloseDirectionSample(Player p)
        {
            // Exact final release latches of the omitted Player.Update tail.
            // Without this, a held direction in a stage fixture could masquerade
            // as a new native double-tap edge on every DashMovement call.
            p.releaseLeft = !p.controlLeft; p.releaseRight = !p.controlRight;
        }
    }
}
