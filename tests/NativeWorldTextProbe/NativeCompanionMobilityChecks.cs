using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCompanionMobilityChecks
    {
        private const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static Player receiver;
        private static object intent;

        internal static void Run(object host, Player body, Player human)
        {
            const string patchId = "JueMingR.Tests.CompanionMobilityIntent";
            var behavior = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionBehaviorHost", true);
            var decide = AccessTools.Method(behavior, "DecideMobility");
            var actor = Call(host, "Owned", body);
            var mobility = Get(actor, "Mobility");
            var harmony = new Harmony(patchId);
            var update = AccessTools.Method(typeof(Player), nameof(Player.Update), new[] { typeof(int) });
            var oldPosition = body.position;
            var oldVelocity = body.velocity;
            var oldHumanPosition = human.position;
            var armor = (Item[])body.armor.Clone();
            var inventory = (Item[])body.inventory.Clone();
            var misc = (Item[])body.miscEquips.Clone();
            var wingStats = ArmorIDs.Wing.Sets.Stats;
            int selection = body.selectedItem;
            int mouseX = Main.mouseX, mouseY = Main.mouseY, humanIndex = Main.myPlayer;
            bool shared = PlayerInput.GrappleAndInteractAreShared;
            receiver = body;
            harmony.Patch(decide, transpiler: new HarmonyMethod(typeof(NativeCompanionMobilityChecks), nameof(KeepNativeConsumer)));
            harmony.Patch(update, finalizer: new HarmonyMethod(typeof(NativeCompanionMobilityChecks), nameof(ReportBodyFault)) { priority = Priority.First });
            try
            {
                // The CPU runner doesn't execute Main's wing data bootstrap.
                // Load the locked Terraria data rather than fixing test fields.
                Terraria.Initializers.WingStatsInitializer.Load();
                human.position = new Vector2(120 * 16, 73 * 16 - human.height);
                Call(mobility, "Reset", Get(actor, "Generation"));
                BodyJumpsAndDash(host, body, decide.ReturnType, mobility);
                WingsAndGravity(host, body, decide.ReturnType, mobility);
                Console.WriteLine("PASS companion mobility inputs through native updates: extra jumps, dash, finite wings, rocket boots, carpet and isolated gravity toggle.");
                HookAndRelease(host, body, decide.ReturnType, mobility);
                ReleaseHookInShaft(host, body, human, decide.ReturnType, mobility);
                Console.WriteLine("PASS companion native grapple: own birth, observed attachment and native detach.");
                ContinuousRope(host, body, decide.ReturnType);
                OpenDoor(host, body, decide.ReturnType, mobility);
                RideExistingTrack(host, body, decide.ReturnType, mobility);
                CompactMount(host, body, decide.ReturnType, mobility);
                Console.WriteLine("PASS companion native travel: rope, named door, cart and compact mount.");
                DedicatedSwitch(host, body, human, decide.ReturnType, mobility);
                ExistingPortal(host, body, human, decide.ReturnType, mobility);
                PylonLoopback(host, body, human, decide.ReturnType, mobility);
                Console.WriteLine("PASS companion native facilities: dedicated wire pulse, existing portal, actor pylon loopback.");
                Rod(host, body, decide.ReturnType, mobility);
                Require(!(bool)Get(actor, "Faulted"), "native mobility fixture body remains active: " + Get(actor, "Status"));
                Require(Main.myPlayer == humanIndex && Main.mouseX == 23 && Main.mouseY == 41,
                    "mobility consumers return the exact human player and pointer");
            }
            finally
            {
                harmony.Unpatch(decide, HarmonyPatchType.Transpiler, patchId);
                harmony.Unpatch(update, HarmonyPatchType.Finalizer, patchId);
                receiver = null; intent = null;
                Array.Copy(armor, body.armor, armor.Length);
                Array.Copy(inventory, body.inventory, inventory.Length);
                Array.Copy(misc, body.miscEquips, misc.Length);
                ArmorIDs.Wing.Sets.Stats = wingStats;
                body.RemoveAllGrapplingHooks();
                body.position = oldPosition; body.velocity = oldVelocity;
                body.selectedItemState.Select(selection);
                human.position = oldHumanPosition;
                Main.mouseX = mouseX; Main.mouseY = mouseY;
                PlayerInput.GrappleAndInteractAreShared = shared;
            }
            Console.WriteLine("PASS companion native mobility: grapple, rope, door, cart, compact mount, existing teleport facilities and rod through real Player.Update.");
        }

        private static void BodyJumpsAndDash(object host, Player body, Type frame, object state)
        {
            Ground(body, 101);
            body.armor[3] = Gear(ItemID.CloudinaBottle);
            body.armor[4] = Gear(ItemID.SandstorminaBottle);
            body.armor[5] = Gear(ItemID.Tabi);
            Set(frame);
            Step(host, body);
            Step(host, body);
            var snapshot = Get(state, "Observed");
            Require((int)Get(snapshot, "ExtraJumps") != 0 && (int)Get(snapshot, "DashType") > 0 && body.releaseJump,
                "effective accessory results are sampled from the actual body update");

            Set(frame, jump: true);
            Step(host, body);
            Require(body.velocity.Y < 0 && body.canJumpAgain_Sandstorm && body.canJumpAgain_Cloud, "ground jump spends neither extra charge");
            Set(frame);
            Step(host, body);
            Set(frame, jump: true);
            Step(host, body);
            Require(!body.canJumpAgain_Sandstorm && body.canJumpAgain_Cloud, "native release/push spends Sandstorm before Cloud");
            Set(frame);
            Step(host, body);
            Set(frame, jump: true);
            Step(host, body);
            Require(!body.canJumpAgain_Cloud, "a second real edge spends Cloud");

            Ground(body, 101);
            var prior = Player.Settings.DashControl;
            try
            {
                Player.Settings.DashControl = Player.Settings.DashPreference.OnlyThroughHotkeys;
                Set(frame, dash: 1);
                Step(host, body);
                Require(body.dashDelay != 0 && body.velocity.X > 3, "actor dash uses native movement and enters cooldown");
            }
            finally { Player.Settings.DashControl = prior; }
            body.armor[3] = new Item(); body.armor[4] = new Item(); body.armor[5] = new Item();
        }

        private static void WingsAndGravity(object host, Player body, Type frame, object state)
        {
            Ground(body, 101);
            body.armor[3] = Gear(ItemID.FrozenWings);
            Set(frame);
            Step(host, body);
            Step(host, body);
            float full = body.wingTime;
            Require(body.wingsLogic > 0 && full > 1f, "the grounded body obtains finite wing time from its effective equipped accessory");
            Set(frame, jump: true);
            for (int i = 0; i < 35 && body.wingTime >= full; i++) Step(host, body);
            Require(body.Bottom.Y < 73 * 16 - 20 && body.wingTime < full, "continued native jump reaches wing flight and spends its finite time");
            var actor = Call(host, "Owned", body);
            var profile = (CompanionProfile)Get(actor, "Profile");
            int life = body.statLife;
            body.statLife = Math.Max(1, body.statLifeMax2 - 65);
            int injured = body.statLife;
            body.inventory[31] = Gear(ItemID.LesserHealingPotion);
            float remainingFlight = body.wingTime;
            try
            {
                Require((bool)Call(host, "UseItemOnce", profile.Id, CompanionInventoryArea.Inventory, 31),
                    "the existing explicit use command accepts the actor's healing item in flight");
                Step(host, body);
                Require(body.inventory[31].IsAir && body.statLife > injured && body.wingTime < remainingFlight && body.controlJump && body.velocity.Y < 0,
                    "actual potion payment and native ascent continue in the same real body update");
            }
            finally { body.statLife = life; }

            Ground(body, 101);
            body.armor[3] = Gear(ItemID.RocketBoots);
            Set(frame);
            Step(host, body);
            Step(host, body);
            int fuel = body.rocketTime;
            Require(body.rocketBoots > 0 && fuel > 0, "effective rocket boots replenish from real ground support");
            Set(frame, jump: true);
            Step(host, body);
            Require(body.velocity.Y < 0 && !body.rocketRelease, "normal takeoff disarms the native rocket edge");
            Set(frame);
            Step(host, body);
            Require(body.rocketRelease, "a real air release rearms the boot consumer");
            Set(frame, jump: true);
            for (int i = 0; i < 40 && body.rocketTime >= fuel; i++) Step(host, body);
            Require(body.rocketTime < fuel && body.rocketDelay > 0, "released then held Jump reaches the native rocket burst and consumes one unit");

            Ground(body, 101);
            body.armor[3] = Gear(ItemID.FlyingCarpet);
            Set(frame);
            Step(host, body);
            Step(host, body);
            Set(frame, jump: true, move: 1);
            for (int i = 0; i < 40 && body.carpetFrame < 0; i++) Step(host, body);
            Require(body.carpetFrame >= 0 && body.carpetTime > 0, "held input moves onto the native carpet only after ground jump ends");
            int carpetTime = body.carpetTime;
            Step(host, body);
            Require(body.carpetTime < carpetTime, "the continuing carpet action spends real remaining time");
            body.armor[3] = new Item();

            Ground(body, 101);
            body.AddBuff(BuffID.Gravitation, 300);
            try
            {
                Set(frame, vertical: -1);
                Step(host, body);
                Require(body.gravDir == 1f, "ordinary Up must not also trigger the equipped gravity toggle");
                Set(frame);
                Step(host, body);
                Set(frame, "Gravity", 2020, new Vector2(0, -1));
                Step(host, body);
                Require(body.gravDir == -1f && Get(state, "Result").ToString() == "Completed", "explicit reverse-gravity action reaches the native toggle");
                Set(frame);
                Step(host, body);
                Set(frame, "Gravity", 2021, new Vector2(0, 1));
                Step(host, body);
                Require(body.gravDir == 1f, "a genuinely released Up edge restores the regular gravity direction");
            }
            finally
            {
                int buff = body.FindBuffIndex(BuffID.Gravitation);
                if (buff >= 0) body.DelBuff(buff);
            }
        }

        private static void HookAndRelease(object host, Player body, Type frame, object state)
        {
            Ground(body, 102);
            const int x = 110, y = 68;
            Tile previous = Main.tile[x, y];
            var stone = new Tile(); stone.active(true); stone.type = TileID.Stone;
            Main.tile[x, y] = stone;
            var point = new Vector2(x * 16 + 8, y * 16 + 8);
            var item = Gear(ItemID.GrapplingHook);
            bool priorHook = Main.projHook[item.shoot];
            var actor = Call(host, "Owned", body);
            var previousPreferences = (CompanionBehavior)Get(actor, "Preferences");
            const int buffSlot = 32;
            var previousBuffItem = body.inventory[buffSlot];
            try
            {
                // Vanilla Main marks hook projectile types during content
                // initialization. The cheap CPU fixture loads the same
                // projectile defaults but omits that full Main registration.
                Main.projHook[item.shoot] = ContentSamples.ProjectilesByType[item.shoot].aiStyle == 7;
                body.miscEquips[4] = item;
                Main.mouseX = 23; Main.mouseY = 41;
                PlayerInput.GrappleAndInteractAreShared = true;
                Set(frame);
                Step(host, body);
                var observed = Get(state, "Observed");
                var geometry = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionMobilityGeometry", true);
                var anchor = AccessTools.Method(geometry, "HookAnchor");
                var nativeSource = body.QuickGrapple_GetItemToUse();
                var hookRules = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Movement.SmartFallGrapple", true);
                float range = (float)AccessTools.Method(hookRules, "Range").Invoke(null, new object[] { item.shoot });
                bool latch = (bool)anchor.Invoke(null, new object[] { body, point, item.shoot, range, false });
                bool swept = (bool)anchor.Invoke(null, new object[] { body, point, item.shoot, range, true });
                var recovery = previousPreferences.Clone();
                recovery.BuffEnabled = true;
                recovery.AllowedBuffTypes = new int[] { ItemID.IronskinPotion };
                NativeInformationChecks.Set(actor, "preferences", recovery);
                body.inventory[buffSlot] = Gear(ItemID.IronskinPotion);
                Require((bool)Call(host, "CompanionBuffPending", actor), "an eligible automatic buff is genuinely waiting for the hand");
                Set(frame, "Hook", 2001, point, emergency: true);
                Step(host, body);
                Require(Get(state, "Result").ToString() == "Started" && body.grapCount == 0,
                    "real hook birth is distinct from attachment with shared human input: " + Get(state, "Result") +
                    ", hook=" + (GetOptional(state, "Hook") != null) + ", release=" + body.releaseUseItem + ", grapCount=" + body.grapCount +
                    ", selected=" + nativeSource?.type + ", projectile=" + item.shoot + ", observed=" + Get(observed, "HookType") + "/" + Get(observed, "HookRange") +
                    ", range=" + range + ", latch=" + latch + ", swept=" + swept + ", body=" + body.position + "/" + body.Size +
                    ", origin solid=" + Collision.SolidCollision(body.position, body.width, body.height) +
                    ", bound=" + (GetOptional(state, "Source") != null) + ", native shots=" + Array.FindAll(Main.projectile,
                        shot => shot != null && shot.active && shot.type == item.shoot && shot.owner == body.whoAmI).Length +
                    ", prohibited=" + body.noItems + "/" + body.cursed + "/" + body.CCed);
                Require(!body.inventory[buffSlot].IsAir && body.FindBuffIndex(BuffID.Ironskin) < 0,
                    "the emergency hook really launches before an unstarted automatic buff takes the item edge");
                var hook = (Projectile)Get(state, "Hook");
                Require(hook.owner == body.whoAmI && hook.velocity.X > 0 && hook.velocity.Y < 0,
                    "actor's target supplies independent native hook aim");
                for (int tick = 0; tick < 60 && body.grapCount == 0; tick++)
                {
                    Step(host, body);
                    AdvanceHook(hook);
                }
                Step(host, body);
                Require(hook.active && hook.ai[0] == 2 && Get(state, "Result").ToString() == "Completed", "native projectile truly attaches to the chosen tile");
                AdvanceHook(hook);
                Set(frame, "ReleaseHook", 2002);
                Step(host, body);
                AdvanceHook(hook);
                Step(host, body);
                Require(!hook.active && body.grapCount == 0, "real released Jump detaches and retires the same projectile");
            }
            finally
            {
                Main.tile[x, y] = previous;
                Main.projHook[item.shoot] = priorHook;
                NativeInformationChecks.Set(actor, "preferences", previousPreferences);
                body.inventory[buffSlot] = previousBuffItem;
                int buff = body.FindBuffIndex(BuffID.Ironskin);
                if (buff >= 0) body.DelBuff(buff);
            }
        }

        private static void Rod(object host, Player body, Type frame, object state)
        {
            Ground(body, 95);
            int prior = body.selectedItem;
            body.inventory[20] = Gear(ItemID.RodofDiscord);
            Set(frame);
            Step(host, body);
            var actor = Call(host, "Owned", body);
            var profile = (CompanionProfile)Get(actor, "Profile");
            var manual = new object[] { profile.Id, CompanionInventoryArea.Inventory, 20, null };
            Require(!(bool)AccessTools.Method(host.GetType(), "CanUseItemOnce").Invoke(host, manual) &&
                manual[3].ToString().Contains("地点"), "manual UseOnce still rejects a rod with no world destination");
            Vector2 first = new Vector2(115 * 16 + 8, 73 * 16 - 1);
            Set(frame, "Rod", 2003, first, slot: 20);
            Step(host, body);
            Require(Vector2.DistanceSquared(body.Bottom, first) < 16 && body.FindBuffIndex(BuffID.ChaosState) >= 0 &&
                Get(state, "Result").ToString() == "Completed", "native rod ItemCheck places the actual actor and applies chaos state");
            int life = body.statLife;
            Set(frame, "Rod", 2004, new Vector2(98 * 16, 73 * 16 - 1), slot: 20);
            for (int tick = 0; tick < 40; tick++) Step(host, body);
            Require(Math.Abs(body.Center.X - first.X) < 4 && body.statLife >= life && body.selectedItem == prior,
                "chaos state blocks repeat paid use and the previous selection returns after the real item tail");
        }

        private static Item Gear(int id) { var item = new Item(); item.SetDefaults(id); return item; }
        private static void Ground(Player body, int x)
        {
            body.position = new Vector2(x * 16, 73 * 16 - body.height);
            body.velocity = Vector2.Zero;
        }

        private static void ReleaseHookInShaft(object host, Player body, Player human, Type frame, object state)
        {
            const int leftWall = 108, rightWall = 111, anchorRow = 66;
            var tiles = new Dictionary<Point, Tile>();
            Vector2 humanPosition = human.position;
            var item = Gear(ItemID.GrapplingHook);
            Item originalItem = body.miscEquips[4];
            bool priorHook = Main.projHook[item.shoot];
            try
            {
                for (int y = 65; y < 73; y++)
                {
                    FixtureTile(tiles, leftWall, y, TileID.Stone);
                    FixtureTile(tiles, rightWall, y, TileID.Stone);
                }
                FixtureTile(tiles, 110, anchorRow, TileID.Stone);
                Ground(body, 110);
                body.position.X -= body.width * .5f;
                Main.projHook[item.shoot] = ContentSamples.ProjectilesByType[item.shoot].aiStyle == 7;
                body.miscEquips[4] = item;
                Set(frame);
                Step(host, body);
                var target = new Vector2(110 * 16 + 1, anchorRow * 16 + 8);
                Set(frame, "Hook", 2031, target);
                Step(host, body);
                var hook = (Projectile)Get(state, "Hook");
                for (int tick = 0; tick < 60 && (Get(state, "Result").ToString() != "Completed" || body.position.Y > 1100); tick++)
                { AdvanceHook(hook); Step(host, body); }
                Require(hook.active && Get(state, "Result").ToString() == "Completed" && body.position.Y <= 1100,
                    "real hook attaches above the body in the two-tile shaft");
                foreach (int side in new[] { -1, 1 })
                    Require(Collision.TileCollision(body.position, new Vector2(side * 16, 0), body.width, body.height).X != side * 16,
                        "neither side of the shaft admits a full release step");

                // Only the initial shot was injected. From the actual attached
                // pose the production route chooses the vertical release.
                human.Center = new Vector2(body.Center.X, 73 * 16 - human.height * .5f);
                float startY = body.position.Y;
                float startX = body.position.X;
                bool releaseJump = false, sidewaysInput = false, detached = false, keptShaft = true;
                int deliveredReleases = 0;
                float farthestSideways = 0;
                intent = null;
                for (int tick = 0; tick < 60 && (hook.active || Math.Abs(body.Bottom.Y - 73 * 16) > 1); tick++)
                {
                    // In each world step the previous projectile update feeds
                    // the next Player.Update's real grapple list.
                    AdvanceHook(hook);
                    Step(host, body);
                    farthestSideways = Math.Max(farthestSideways, Math.Abs(body.position.X - startX));
                    keptShaft &= body.Left.X >= (leftWall + 1) * 16 - .25f && body.Right.X <= rightWall * 16 + .25f;
                    if (HookReleaseStartedThisUpdate(state) && (int)Get(state, "Sequence") != 2031)
                    {
                        deliveredReleases++;
                        releaseJump |= body.controlJump;
                        sidewaysInput |= body.controlLeft || body.controlRight;
                    }
                    if (Get(state, "Action").ToString() != "ReleaseHook" || (int)Get(state, "Sequence") == 2031) continue;
                    detached |= !(bool)Get(Get(state, "Observed"), "GrappleAttached") && Get(state, "Result").ToString() == "Completed";
                }
                Require(deliveredReleases > 0 && releaseJump && !sidewaysInput && detached && !hook.active && body.position.Y > startY + 8 &&
                    Math.Abs(body.Bottom.Y - 73 * 16) <= 1 && keptShaft,
                    "the production route leaves the real overhead hook along the safe vertical shaft: " + body.position + " from " + startY +
                    ", arrivals=" + deliveredReleases + ", jump=" + releaseJump + ", sidewaysInput=" + sidewaysInput + ", sidewaysTravel=" + farthestSideways +
                    ", detached=" + detached + ", hook=" + hook.active +
                    ", action=" + Get(state, "Action") + "/" + Get(state, "Result"));
                Console.WriteLine($"PASS companion vertical shaft: actual attachment, route-selected vertical release and real descent. " +
                    $"releaseUpdates={deliveredReleases} jump={releaseJump} sidewaysInput={sidewaysInput} travel={farthestSideways:F3} feet={body.Bottom}");
            }
            finally
            {
                body.RemoveAllGrapplingHooks();
                body.miscEquips[4] = originalItem;
                Main.projHook[item.shoot] = priorHook;
                human.position = humanPosition;
                RestoreTiles(tiles);
            }
        }

        internal static bool HookReleaseStartedThisUpdate(object mobility)
        {
            // Action and Sequence identify the most recent tool after it has
            // completed. Started identifies the update that sent the native key.
            return Get(mobility, "Action").ToString() == "ReleaseHook" &&
                (uint)Get(mobility, "Started") == Main.GameUpdateCount;
        }

        private static void AdvanceHook(Projectile hook)
        {
            if (!hook.active) return;
            int previous = Main.ProjectileUpdateLoopIndex;
            try { Main.ProjectileUpdateLoopIndex = hook.whoAmI; hook.Update(hook.whoAmI); }
            finally { Main.ProjectileUpdateLoopIndex = previous; }
        }

        private static void Step(object host, Player body)
        {
            NativeQuickItemChecks.BeginWorldStep();
            body.Update(body.whoAmI);
            Call(host, "Update", (ulong)Main.GameUpdateCount);
        }

        private static void Set(Type frame, string action = "None", int sequence = 0, Vector2 point = default(Vector2),
            bool jump = false, int dash = 0, int slot = -1, int move = 0, int vertical = 0, int mountType = -1, bool emergency = false)
        {
            intent = Activator.CreateInstance(frame);
            Write("Jump", jump); Write("Dash", dash); Write("Sequence", sequence); Write("ItemSlot", slot);
            Write("Move", move); Write("Vertical", vertical);
            Write("MountTypePlusOne", mountType + 1);
            Write("EmergencyHand", emergency);
            Write("WorldX", point.X); Write("WorldY", point.Y); Write("Status", "测试移动");
            var activation = frame.GetField("Activation", Fields);
            activation.SetValue(intent, Enum.Parse(activation.FieldType, action));
        }

        private static void Write(string name, object value) => intent.GetType().GetField(name, Fields).SetValue(intent, value);

        private static object TestIntent(object original, Player body) => ReferenceEquals(body, receiver) && intent != null ? intent : original;

        private static Exception ReportBodyFault(Player __instance, Exception __exception)
        {
            if (__exception != null && ReferenceEquals(__instance, receiver)) Console.WriteLine("NAV_NATIVE_BODY_FAULT " + __exception);
            return __exception;
        }

        private static IEnumerable<CodeInstruction> KeepNativeConsumer(IEnumerable<CodeInstruction> source, MethodBase __originalMethod)
        {
            var frame = ((MethodInfo)__originalMethod).ReturnType;
            foreach (var instruction in source)
            {
                if (instruction.opcode != OpCodes.Ret) { yield return instruction; continue; }
                // The fixture chooses precise input intents. It leaves host
                // hand arbitration, the actual body and native consumers intact.
                var box = new CodeInstruction(OpCodes.Box, frame);
                box.labels.AddRange(instruction.labels);
                box.blocks.AddRange(instruction.blocks);
                yield return box;
                yield return new CodeInstruction(OpCodes.Ldarg_1);
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NativeCompanionMobilityChecks), nameof(TestIntent)));
                yield return new CodeInstruction(OpCodes.Unbox_Any, frame);
                yield return new CodeInstruction(OpCodes.Ret);
            }
        }
    }
}
