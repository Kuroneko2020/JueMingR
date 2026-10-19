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
                Console.WriteLine("PASS MOVEMENT DASH native capability/input/consumer cases=" + cases + "; native DashMovement/DoCommonDashHandle; full Main mapping separate");
            }
            finally { Call(movement, "SetEnabled", 1, false); Main.player[Main.myPlayer] = original; }
        }
        private static void ConsumerFailure() { if (throwConsumer) throw new InvalidOperationException("owned-dash-test"); }
        internal static void Mapped(object context, Action<Keys[]> input, Action world)
        {
            var movement = Get(context, "Movement");
            Call(movement, "SetEnabled", 0, false); Call(movement, "SetEnabled", 1, false); Call(movement, "SetDashMode", ContinuousDashMode.Hold); Call(movement, "SetEnabled", 1, true);
            Player saved = Main.LocalPlayer;
            var witness = new Harmony("JueMingR.Tests.Movement.MappedDashWitness");
            try
            {
                watchedMovement = movement; watchedInput = Get(context, "Input");
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
        private static Player Local()
        { var p = new Player { whoAmI = Main.myPlayer, active = true, position = new Vector2(700, 900), releaseDash = true, direction = 1 }; Main.player[Main.myPlayer] = p; return p; }
        private static void Mount(Player p, int type)
        { int mode = Main.netMode, me = Main.myPlayer; bool dedicated = Main.dedServ; try { Main.netMode = 2; Main.dedServ = true; Main.myPlayer = 1; p.mount.SetMount(type, p); } finally { Main.netMode = mode; Main.myPlayer = me; Main.dedServ = dedicated; } }
        private static void Stage(object movement, object input, Player p, bool left, bool right)
        {
            NativeQuickItemChecks.Sample(input, new Keys[0]);
            p.controlLeft = left; p.controlRight = right;
            Require((bool)Call(movement, "BeginUpdate", p), "stage real-local scope admitted " + ++sample);
            try { Call(movement, "CaptureInput", p); if (left && right) p.controlLeft = p.controlRight = false; p.DashMovement(); }
            finally { Call(movement, "EndUpdate", p); }
        }
    }
}
