using System;
using System.Collections.Generic;
using System.Reflection;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Real collision/entity primitives in the already isolated native CPU world.
    // Run observes local geometry/work only. RunJumpReachability separately
    // executes one isolated native Player.Update route; neither is game/FPS or
    // complete navigation acceptance.
    internal static class NativeCompanionBehaviorChecks
    {
        private static Player jumpBody;
        private static bool jumpHeld, moveRight;

        internal static object DecideObserved(object behavior, Player body, Player owner, CompanionBehavior preferences, bool planning)
        {
            var native = Activator.CreateInstance(behavior.GetType().Assembly.GetType(
                "JueMingR.TerrariaHost.Companions.CompanionMobilityState", true), true);
            Call(native, "ObserveBody", body, false);
            var decide = AccessTools.Method(behavior.GetType(), "DecideMobility");
            object[] args = { body, owner, preferences, planning, null, false, false, null, native, false, null };
            return decide.Invoke(behavior, args.Take(decide.GetParameters().Length).ToArray());
        }

        internal static void RunJumpReachability(object host)
        {
            Require(Main.netMode == 0, "jump A/B uses the existing isolated single-player native fixture");
            var actors = (System.Collections.IDictionary)Get(host, "actors");
            object actor = actors.Values.Cast<object>().FirstOrDefault(a => (bool)Get(a, "Ready") && !(bool)Get(a, "Closing"));
            Require(actor != null, "jump A/B requires the actual already-joined native companion");
            var originalBody = (Player)Get(actor, "Body"); int slot = originalBody.whoAmI;
            var originalSlot = Main.player[slot]; uint tick = Main.GameUpdateCount;
            var clock = typeof(Main).GetField("_gameUpdateCount", BindingFlags.Static | BindingFlags.NonPublic);
            var actorFields = actor.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(f => !f.IsInitOnly).ToArray();
            var actorValues = actorFields.Select(f => f.GetValue(actor)).ToArray();
            int myPlayer = Main.myPlayer, mouseX = Main.mouseX, mouseY = Main.mouseY, tileX = Player.tileTargetX, tileY = Player.tileTargetY;
            var mouseItem = Main.mouseItem; bool map = Main.mapFullscreen;
            float jumpSpeed = Player.jumpSpeed; int jumpHeight = Player.jumpHeight;
            double surface=Main.worldSurface;
            var collisionFields = typeof(Collision).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => !f.IsInitOnly && !f.IsLiteral && f.FieldType.IsValueType).ToArray();
            var collisionValues = collisionFields.Select(f => f.GetValue(null)).ToArray();
            var saved = new List<Tuple<int, int, Tile>>(); bool stone = Main.tileSolid[TileID.Stone];
            var npcActive = Main.npc.Select(n => n != null && n.active).ToArray();
            var originalItems=Main.item;
            var control = host.GetType().GetMethod("Control", BindingFlags.Instance | BindingFlags.NonPublic);
            var isolation = new Harmony("JueMingR.Tests.CompanionJumpReachability");
            try
            {
                Require(Main.maxTilesX > 134 && Main.maxTilesY > 95, "jump A/B fixture contains its exact isolated pit area");
                // The original Y60 bank was above the native normal-gravity
                // threshold. Use a deeper owned fixture bank so the positive
                // case completes under actual default gravity within 90 steps.
                Main.worldSurface=40;
                for (int x = 80; x <= 132; x++) for (int y = 50; y <= 94; y++)
                {
                    saved.Add(Tuple.Create(x, y, Main.tile[x, y])); var tile = new Tile();
                    // Six empty columns, equal-height banks, and a real lower
                    // floor make a failed crossing observable as a pit landing.
                    if (y >= 80 || y >= 72 && (x < 100 || x >= 106)) { tile.active(true); tile.type = TileID.Stone; }
                    Main.tile[x, y] = tile;
                }
                Main.tileSolid[TileID.Stone] = true;
                for (int i = 0; i < Main.npc.Length; i++) if (Main.npc[i] != null) Main.npc[i].active = false;
                Main.item=new WorldItem[originalItems.Length];for(int i=0;i<Main.item.Length;i++)Main.item[i]=new WorldItem{whoAmI=i};
                // Replace only this fixture actor's decision output. The normal
                // production Player.Update hooks, native input consumers and
                // collision/gravity/jump/cooldown code all remain in operation.
                // No whole-update Main.myPlayer swap or position writes occur
                // during either measured trajectory.
                isolation.Patch(control, prefix: new HarmonyMethod(typeof(NativeCompanionBehaviorChecks), nameof(JumpControl)));
                var stationary = JumpTrial(host, actor, slot, false);
                var running = JumpTrial(host, actor, slot, true);
                Require(stationary.TookOff && running.TookOff, "both A/B bodies actually leave the native bank under the same finite jump hold");
                Require(running.LandedRight, "same six-column geometry has a native successful crossing with existing legal horizontal velocity");
                Require(!stationary.Planned || stationary.LandedRight, "navigation-approved stationary launch must land on its claimed far bank; see NAV_JUMP_AB");
                Require(!running.Planned || running.LandedRight, "navigation-approved running launch must reach its far bank");
            }
            finally
            {
                jumpBody = null; isolation.Unpatch(control, HarmonyPatchType.All, isolation.Id);
                var indexed=(System.Collections.IDictionary)Get(host,"bodies");
                foreach(var key in indexed.Keys.Cast<Player>().Where(p=>ReferenceEquals(indexed[p],actor)).ToArray())indexed.Remove(key);
                indexed[originalBody]=actor;
                for (int i = 0; i < actorFields.Length; i++) actorFields[i].SetValue(actor, actorValues[i]);
                Main.player[slot] = originalSlot;
                foreach (var tile in saved) Main.tile[tile.Item1, tile.Item2] = tile.Item3;
                for (int i = 0; i < npcActive.Length; i++) if (Main.npc[i] != null) Main.npc[i].active = npcActive[i];
                Main.item=originalItems;
                Main.tileSolid[TileID.Stone] = stone; clock.SetValue(null, tick);
                Main.worldSurface=surface;
                Main.myPlayer = myPlayer; Main.mouseX = mouseX; Main.mouseY = mouseY; Main.mouseItem = mouseItem; Main.mapFullscreen = map;
                Player.tileTargetX = tileX; Player.tileTargetY = tileY; Player.jumpSpeed = jumpSpeed; Player.jumpHeight = jumpHeight;
                for (int i = 0; i < collisionFields.Length; i++) collisionFields[i].SetValue(null, collisionValues[i]);
            }
        }
        private sealed class JumpResult
        { internal bool Planned, TookOff, LandedRight; }
        private static JumpResult JumpTrial(object host, object actor, int slot, bool running)
        {
            var profile = Get(actor, "Profile"); var profiles = Get(host, "profiles");
            var body = (Player)Call(profiles, "LoadBody", profile);
            var indexed=(System.Collections.IDictionary)Get(host,"bodies");
            foreach(var key in indexed.Keys.Cast<Player>().Where(p=>ReferenceEquals(indexed[p],actor)).ToArray())indexed.Remove(key);
            indexed[body]=actor;
            body.whoAmI = slot; body.active = true; Set(actor, "Body", body); Main.player[slot] = body;
            Set(actor, "Faulted", false); Set(actor, "LastUpdate", UInt32.MaxValue);
            jumpBody = body; jumpHeld = moveRight = false;
            Call(host, "Spawn", actor, PlayerSpawnContext.SpawningIntoWorld);
            // The only placement is fixture setup, identical in both trials.
            body.position = new Vector2(100 * 16 - body.width - 4, 72 * 16 - body.height); body.velocity = Vector2.Zero;
            for (int i = 0; i < 6; i++) { NativeQuickItemChecks.BeginWorldStep(); body.Update(slot); }
            Require(!(bool)Get(actor, "Faulted") && body.velocity.Y == 0 && Supported(host, body), "native body settles on the actual left bank before A/B launch: position="+body.position+" velocity="+body.velocity+" gravity="+body.gravity+" status="+Get(actor,"Status"));
            if (running) body.velocity = new Vector2(body.maxRunSpeed, 0); // legal pre-existing speed; never changed during flight
            body.releaseJump = true;
            Assembly assembly = host.GetType().Assembly;
            const string navNamespace = "JueMingR.TerrariaHost.Companions.";
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            object New(string name, params object[] args) => Activator.CreateInstance(
                assembly.GetType(navNamespace + name, true), flags, null, args, null);
            var mobility = New("CompanionMobilityState");
            Call(mobility, "ObserveBody", body, false);
            var observed = Get(mobility, "Observed");
            var target = new Vector2(106 * 16 + 14, body.Bottom.Y);
            var jump = Enum.Parse(assembly.GetType(navNamespace + "NavigationAction", true), "Jump");
            var detail = Enum.Parse(assembly.GetType(navNamespace + "NavigationTraversalDetail", true), "None");
            var edge = New("NavigationTransition", jump, body.Bottom, target, Point.Zero, Point.Zero, 0, false, detail, Vector2.Zero);
            var refinement = New("CompanionActionRefinement");
            Call(refinement, "Begin", body, edge, true);
            string proof = "Pending";
            for (int slice = 0; slice < 16 && proof == "Pending"; slice++)
                proof = Call(refinement, "Advance", body, observed, New("NavigationBudget", 1024, 32, 16)).ToString();
            Require(proof != "Pending", "the native jump proof reaches a result within finite component slices");
            var result = new JumpResult { Planned = proof == "Ready" };
            int hold = (int)Get(observed, "JumpHeight");
            var predicted = result.Planned ? target - new Vector2(body.width * .5f, body.height) : Vector2.Zero;
            var start = body.position; float startSpeed = body.velocity.X, acceleration = body.runAcceleration;
            bool crossed = false, fell = false, landed = false; Vector2 landing = Vector2.Zero; int landingFrame = -1;
            var trace = new List<string>();
            for (int frame = 0; frame < 90; frame++)
            {
                moveRight = true; jumpHeld = frame < hold;
                NativeQuickItemChecks.BeginWorldStep(); body.Update(slot);
                Require(!(bool)Get(actor, "Faulted"), "native jump trajectory must not hide a production body-update exception: " + Get(actor, "Status"));
                bool supported = Supported(host, body);
                result.TookOff |= body.position.Y < start.Y - 1 && body.velocity.Y < 0;
                crossed |= body.Center.X >= 106 * 16;
                fell |= body.Center.X >= 100 * 16 && body.Center.X < 106 * 16 && body.position.Y > start.Y + 16;
                trace.Add(frame + ":" + Number(body.position.X) + "," + Number(body.position.Y) + "," + Number(body.velocity.X) + "," + Number(body.velocity.Y) + "," + (supported ? "1" : "0"));
                if (result.TookOff && frame > 1 && supported && Math.Abs(body.velocity.Y) < .01f && body.position.Y >= start.Y - 2)
                {
                    landed = true; landing = body.position; landingFrame = frame;
                    result.LandedRight = body.Center.X >= 106 * 16 + 8 && Math.Abs(body.position.Y - start.Y) < 2;
                    break;
                }
            }
            Console.WriteLine("NAV_JUMP_AB case=" + (running ? "running" : "stationary") + " gapTiles=6 startX=" + Number(start.X) + " startY=" + Number(start.Y) +
                " initialVx=" + Number(startSpeed) + " acceleration=" + Number(acceleration) + " hold=" + hold + " planned=" + result.Planned + " predictedX=" + Number(predicted.X) + " predictedY=" + Number(predicted.Y) +
                " tookOff=" + result.TookOff + " crossed=" + crossed + " fellBelowBank=" + fell + " landed=" + landed + " landedRight=" + result.LandedRight + " landingFrame=" + landingFrame + " landingX=" + Number(landing.X) + " landingY=" + Number(landing.Y) + " trace=" + String.Join(";", trace));
            return result;
        }
        private static bool Supported(object host, Player body)
        {
            var method = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionNativeContact", true).GetMethod("Supported", BindingFlags.Static | BindingFlags.NonPublic);
            return (bool)method.Invoke(null, new object[] { body, body.position, true });
        }
        private static string Number(float value) { return value.ToString("R", CultureInfo.InvariantCulture); }
        private static bool JumpControl(object a)
        {
            if (jumpBody == null || !ReferenceEquals(Get(a, "Body"), jumpBody)) return true;
            jumpBody.controlLeft = jumpBody.controlDown = jumpBody.controlUp = jumpBody.controlUseItem = jumpBody.controlUseTile = jumpBody.controlHook = jumpBody.controlThrow = false;
            jumpBody.controlRight = moveRight; jumpBody.controlJump = jumpHeld; jumpBody.channel = false;
            return false;
        }
        internal static void Run(object host)
        {
            var assembly = host.GetType().Assembly;
            var brainType = assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionBehaviorHost", true);
            var threats = assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionThreatField", true);
            var builds = threats.GetProperty("SnapshotBuilds", BindingFlags.Static | BindingFlags.NonPublic);
            var reset = threats.GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic);
            var clock = typeof(Main).GetField("_gameUpdateCount", BindingFlags.Static | BindingFlags.NonPublic);
            var saved = new List<Tuple<int, int, Tile>>();
            uint tick = Main.GameUpdateCount; var oldProjectile = Main.projectile[0];
            bool solid = Main.tileSolid[TileID.Dirt];
            try
            {
                Require(Main.maxTilesX > 124 && Main.maxTilesY > 70, "native companion behavior fixture has a real isolated tile area");
                for (int x = 80; x <= 120; x++) for (int y = 42; y <= 64; y++)
                {
                    saved.Add(Tuple.Create(x, y, Main.tile[x, y])); var tile = new Tile();
                    if (y == 60) { tile.active(true); tile.type = TileID.Dirt; }
                    Main.tile[x, y] = tile;
                }
                Main.tileSolid[TileID.Dirt] = true;
                var owner = new Player { active = true, position = new Vector2(1720, 918) };
                var first = new Player { active = true, position = new Vector2(1600, 918), direction = 1, statLife = 100, statLifeMax2 = 100, maxRunSpeed = 3 };
                var second = new Player { active = true, position = new Vector2(1640, 918), direction = 1, statLife = 100, statLifeMax2 = 100, maxRunSpeed = 3 };
                object brainA = Activator.CreateInstance(brainType, true), brainB = Activator.CreateInstance(brainType, true);
                reset.Invoke(null, null); clock.SetValue(null, tick + 1);
                long before = (long)builds.GetValue(null);
                var a = DecideObserved(brainA, first, owner, new CompanionBehavior(), true);
                var b = DecideObserved(brainB, second, owner, new CompanionBehavior(), false);
                Require((long)builds.GetValue(null) == before + 1, "two bodies share one native threat scan in the same tick");
                Require((int)Get(a,"Move") == 1 && (int)Get(b,"Move") == 1, "budget-deferred body still follows on a safe existing ground prefix");
                Main.projectile[0] = new Projectile { active = true, hostile = true, damage = 10, width = 16, height = 16, position = first.position + new Vector2(10, 8), velocity = Vector2.Zero };
                clock.SetValue(null, tick + 2);
                var avoid = DecideObserved(brainA, first, owner, new CompanionBehavior { AttackEnabled = false }, true);
                Require((int)Get(avoid,"Move") < 0 || (bool)Get(avoid,"Jump"), "ceasefire still emits local evasion intent for a real hostile projectile hitbox");
            }
            finally
            {
                Main.projectile[0] = oldProjectile; clock.SetValue(null, tick); Main.tileSolid[TileID.Dirt] = solid;
                foreach (var tile in saved) Main.tile[tile.Item1, tile.Item2] = tile.Item3;
                reset.Invoke(null, null);
            }
        }
    }
}
