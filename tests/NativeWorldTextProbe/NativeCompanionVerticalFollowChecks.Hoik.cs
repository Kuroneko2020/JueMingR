using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Achievements;
using Terraria.GameInput;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCompanionVerticalFollowChecks
    {
        private const int HoikFloor = 80;
        private const int HoikMouth = 80;
        private const int HoikTeeth = 2;
        private const int HoikModuleWidth = HoikTeeth * 2;

        private static void NativeHoikBaseline(object context, Player human)
        {
            Main.tile = new Tile[Width, Height];
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++) Main.tile[x, y] = new Tile();
            BuildHoikModule(HoikMouth, HoikFloor);
            for (int x = HoikMouth - 9; x <= HoikMouth + HoikModuleWidth + 10; x++)
                if (!Main.tile[x, HoikFloor].active()) PlaceFixtureTile(x, HoikFloor, TileID.Stone);

            human.Bottom = new Vector2((HoikMouth - 4) * 16 + 8, HoikFloor * 16);
            human.velocity = Vector2.Zero;
            var input = PlayerInput.Triggers.Current;
            var gameplayInput = Get(context, "Input");
            var noAchievements = IsolateNativeMovementAchievements();
            bool originalLeft = input.Left, originalRight = input.Right, originalUp = input.Up,
                originalDown = input.Down, originalJump = input.Jump;
            int contact = -1, passed = -1, impulses = 0;
            float largest = 0;
            try
            {
                for (int tick = 0; tick < 900 && passed < 0; tick++)
                {
                    NativeToolExecutionChecks.Sample(context, gameplayInput, human.Center, false, 1);
                    Vector2 before = human.position;
                    NativeQuickItemChecks.BeginWorldStep();
                    human.Update(human.whoAmI);
                    float delta = human.position.X - before.X;
                    largest = Math.Max(largest, delta);
                    if (delta > 7)
                    {
                        if (contact < 0) contact = tick;
                        impulses++;
                    }
                    if (human.Left.X > (HoikMouth + HoikModuleWidth + 1) * 16 && OnFloor(human, HoikFloor)) passed = tick;
                    if (tick == 0 || delta > 7)
                        Console.WriteLine($"HOIK native tick={tick} pose={human.position}/{human.velocity} dx={delta:F2} right={human.controlRight}");
                }
            }
            finally
            {
                noAchievements.UnpatchAll(noAchievements.Id);
                input.Left = originalLeft; input.Right = originalRight; input.Up = originalUp;
                input.Down = originalDown; input.Jump = originalJump;
            }
            Console.WriteLine($"HOIK native started=external width={human.width} height={human.height} contact={contact} impulses={impulses} exit={passed} largest={largest:F2} bottom={human.Bottom}");
            Require(contact >= 0 && impulses >= 2 && passed > contact && !human.dead,
                "a same-size ordinary native player enters from outside, is displaced by the original hoik and steps onto clear support after the exit");
        }

        private static Harmony IsolateNativeMovementAchievements()
        {
            // These sinks depend on the graphics client's achievement
            // service. Physics, controls and Player.Update stay native.
            var patch = new Harmony("JueMingR.Tests.CompanionMovementAchievements");
            foreach (string method in new[] { "HandleSpecialEvent", "HandleMining", "HandleRunning" })
                patch.Patch(AccessTools.Method(typeof(AchievementsHelper), method),
                    prefix: new HarmonyMethod(typeof(NativeCombatCadenceChecks), "SkipAchievement"));
            return patch;
        }

        private static void HoikFollow(object context, object host, object profiles, Player human)
        {
            const int lower = HoikFloor + 6, lip = HoikMouth + 7;
            float start = HoikMouth * 16 - 2;
            float owner = (HoikMouth - 6) * 16;
            using (var run = new NativeFollowWorld(context, host, profiles, human, "hoik-native-follow",
                new Vector2(start, HoikFloor * 16), new Vector2(owner, lower * 16), () =>
                {
                    for (int x = HoikMouth - 9; x < lip; x++)
                        PlaceFixtureTile(x, HoikFloor, TileID.Stone);
                    for (int y = HoikFloor - 9; y < lower; y++)
                        PlaceFixtureTile(HoikMouth - 10, y, TileID.Stone);
                    for (int x = HoikMouth - 9; x <= lip - 2; x++)
                        for (int y = HoikFloor - 9; y < HoikFloor - 3; y++) PlaceFixtureTile(x, y, TileID.Stone);
                    PlaceFixtureStrip(HoikMouth - 9, lip + 8, lower, TileID.Stone);
                    BuildHoikModule(HoikMouth, HoikFloor);
                }))
            {
                int observedAction = -1, impulseCount = 0, exit = -1, lowerContact = -1, followed = -1, stable = 0;
                int projectedThreat = -1;
                bool keptNativeExit = false;
                object navigation = Navigator(run.Actor);
                const BindingFlags staticMethod = BindingFlags.Static | BindingFlags.NonPublic;
                var engaged = navigation.GetType().GetMethod("HoikEngaged", staticMethod);
                Vector2 firstOwnerPose = human.position;
                for (int tick = 0; tick < 1600; tick++)
                {
                    float oldX = run.Body.position.X;
                    int ownerInput = lowerContact >= 0 ? NativeAxisTo(human, owner + 52) : 0;
                    if (engaged != null && projectedThreat < 0 && ObservedActionKind(navigation) == "Hoik" &&
                        run.Body.Center.X == (int)run.Body.Center.X &&
                        (bool)engaged.Invoke(null, new[] { run.Body, Get(Get(run.Actor, "Mobility"), "Observed"), Get(Get(navigation, "actions"), "Current") }))
                    {
                        int slot = Array.FindIndex(Main.projectile, item => item == null || !item.active);
                        Require(slot >= 0, "the isolated Hoik scene has space for a genuine hostile observation");
                        var previous = Main.projectile[slot];
                        int health = run.Body.statLife;
                        // The observed sweep covers the escape poses. Its
                        // initial hitbox remains high above this body.
                        Main.projectile[slot] = new Projectile { whoAmI = slot, active = true, hostile = true, damage = 10,
                            width = 168, height = 12, position = run.Body.Top - new Vector2(84, 64), velocity = new Vector2(0, 12) };
                        try
                        {
                            run.Step(ownerInput);
                            var field = navigation.GetType().Assembly.GetType(navigation.GetType().Namespace + ".CompanionThreatField", true);
                            var forecast = field.GetMethod("Observe", staticMethod);
                            object[] observation = { run.Body, new int[Main.projectile.Length + Main.npc.Length], 1, 0, false };
                            projectedThreat = tick;
                            keptNativeExit = (bool)forecast.Invoke(null, observation) && !(bool)observation[4] &&
                                run.Body.controlRight && run.Body.position.X > oldX && run.Body.statLife == health &&
                                ObservedActionKind(navigation) == "Hoik";
                        }
                        finally { Main.projectile[slot] = previous; }
                    }
                    else run.Step(ownerInput);
                    string kind = ObservedActionKind(navigation);
                    if (kind == "Hoik" && observedAction < 0) observedAction = tick;
                    if (run.Body.position.X - oldX > 7 && run.Body.velocity.X < 1) impulseCount++;
                    if (exit < 0 && impulseCount >= 2 && run.Body.Left.X > (HoikMouth + HoikModuleWidth) * 16 && OnFloor(run.Body, HoikFloor))
                        exit = tick;
                    if (lowerContact < 0 && exit >= 0 && OnFloor(run.Body, lower)) lowerContact = tick;
                    if (lowerContact >= 0 && human.position.X - firstOwnerPose.X > 40 && run.Body.Center.X < firstOwnerPose.X + 110)
                    {
                        if (followed < 0) followed = tick;
                        stable = OnFloor(run.Body, lower) && Math.Abs(run.Body.Center.X - human.Center.X) <= 32 ? stable + 1 : 0;
                        if (stable >= 20) break;
                    }
                }
                Console.WriteLine($"HOIK follow action={observedAction} impulses={impulseCount} exit={exit} lower={lowerContact} followed={followed} stable={stable} projectedThreat={projectedThreat} protectedExit={keptNativeExit} actor={run.Body.Bottom}/{run.Body.velocity} owner={human.Bottom} status={Get(run.Actor, "Status")}");
                Require(observedAction >= 0 && impulseCount >= 2 && exit > observedAction && lowerContact > exit && followed > lowerContact && stable >= 20,
                    "ordinary Follow takes the directional Hoik away from the lower goal, observes the native displacements, changes layers and resumes following the native moving owner");
                Require(engaged == null || projectedThreat >= observedAction && projectedThreat < exit && keptNativeExit,
                    "the native hostile observation does not discard the only validated exit while the body is between live teeth");
            }
        }

        private static string ObservedActionKind(object navigation)
        {
            object executor = GetOptional(navigation, "actions") ?? navigation;
            var route = GetOptional(executor, "route") as IList;
            var routeIndex = GetOptional(executor, "routeIndex");
            if (route == null || !(routeIndex is int index) || index < 0 || index >= route.Count) return "none";
            return Get(route[index], "Kind").ToString();
        }

        private sealed class NativeFollowWorld : IDisposable
        {
            private readonly object context, host, input, profiles;
            private readonly Player human;
            private readonly Guid id;
            private readonly Harmony noAchievements;
            private readonly Terraria.Map.WorldMap previousMap;
            private bool recalled;
            internal readonly Player Body;
            internal readonly object Actor;

            internal NativeFollowWorld(object context, object host, object profiles, Player human, string name,
                Vector2 bodyStart, Vector2 ownerStart, Action terrain)
            {
                this.context = context; this.host = host; this.profiles = profiles; this.human = human;
                input = Get(context, "Input");
                previousMap = Main.Map;
                Main.Map = new Terraria.Map.WorldMap(Width, Height);
                Main.tile = new Tile[Width, Height];
                for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++) Main.tile[x, y] = new Tile();
                try
                {
                    terrain();
                    Main.spawnTileX = (int)(ownerStart.X / 16); Main.spawnTileY = (int)(ownerStart.Y / 16) - 2;
                    human.Bottom = ownerStart; human.velocity = Vector2.Zero;
                    Require((bool)Call(host, "Create", name), "create a native continuous Follow profile: " + Get(host, "Feedback"));
                    id = (Guid)Get(host, "LastCreatedId");
                    NativeQuickItemChecks.Until(() => { Call(profiles, "Pump"); return (bool)Call(profiles, "IsSaved", id); });
                    Require((bool)Call(host, "Join", id), "join the native continuous Follow profile: " + Get(host, "Feedback"));
                    Body = (Player)Call(host, "GetBody", id);
                    Actor = Call(host, "Owned", Body);
                    Body.Bottom = bodyStart;
                    Body.velocity = Vector2.Zero;
                    Require((bool)Call(host, "SetBehavior", id, new CompanionBehavior
                    { Follow = true, AttackEnabled = false, HealthRecoveryEnabled = false }), "configure ordinary Follow");
                    noAchievements = IsolateNativeMovementAchievements();
                }
                catch { Main.Map = previousMap; throw; }
            }

            internal long Step(int direction = 0, bool jump = false, bool down = false, bool up = false)
            {
                NativeToolExecutionChecks.Sample(context, input, human.Center, false, direction);
                var controls = PlayerInput.Triggers.Current;
                controls.Jump = jump; controls.Down = down; controls.Up = up;
                NativeQuickItemChecks.BeginWorldStep();
                long started = Stopwatch.GetTimestamp();
                human.Update(human.whoAmI);
                if (!recalled) Body.Update(Body.whoAmI);
                Call(context, "UpdateRuntime");
                long elapsed = Stopwatch.GetTimestamp() - started;
                Require(recalled || !(bool)Get(Actor, "Faulted"), "normal continuous Player.Update faulted: " + Get(Actor, "Status"));
                return elapsed;
            }

            internal void Recall()
            {
                if (recalled) return;
                Call(host, "Recall", id);
                NativeQuickItemChecks.Until(() => { Call(host, "Update", 0UL); Call(profiles, "Pump"); return !((IDictionary)Get(host, "actors")).Contains(id); });
                recalled = true;
            }

            public void Dispose()
            {
                try
                {
                    noAchievements.UnpatchAll(noAchievements.Id);
                    Recall();
                }
                finally { Main.Map = previousMap; }
            }
        }

        private static void BuildHoikModule(int mouth, int floor)
        {
            int roof = floor - 3;
            for (int x = mouth; x < mouth + HoikModuleWidth; x += 2)
            {
                PlaceFixtureTile(x, roof, TileID.Stone);
                Require(WorldGen.SlopeTile(x, roof, 4, noEffects: true), "shape the native Hoik mouth");
                Require(WorldGen.PlaceActuator(x, roof), "place the native Hoik mouth actuator");
                Wiring.ActuateForced(x, roof);
                Require(Main.tile[x, roof].inActive() && Main.tile[x, roof].slope() == 4,
                    "the placed entrance keeps the inactive directional slope");
                PlaceFixtureTile(x + 1, roof, TileID.Stone);
                Require(WorldGen.SlopeTile(x + 1, roof, 3, noEffects: true) && Main.tile[x + 1, roof].slope() == 3,
                    "the original hammer phase leaves the intended active tooth");
            }
        }

        private static void PlaceFixtureTile(int x, int y, int type)
        {
            try { WorldGen.PlaceTile(x, y, type, mute: true, forced: true); }
            catch (IndexOutOfRangeException exception)
            { throw new InvalidOperationException($"Original tile placement at ({x},{y}) with type {type} failed in the isolated test world", exception); }
            Require(Main.tile[x, y].active() && Main.tile[x, y].type == type, $"the original placement consumer made tile {type} at ({x},{y})");
        }
    }
}
