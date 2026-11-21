using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Events;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // One finite native leaf: value-query invariants plus actual Player.Update
    // takeoff/landing/next-direction consumption. No Steam/game/FPS claim.
    internal static class NativeCompanionNavigationChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static Type navigation;
        private static Player basicRangedBody;
        private static bool UseBasicGeometry(Player player) => !ReferenceEquals(player, basicRangedBody);
        private static bool Query(string name,Player body,params object[] tail)
        { return (bool)navigation.GetMethod(name,Flags).Invoke(null,new object[]{body}.Concat(tail).ToArray()); }
        private static bool Support(Player body,bool platforms=true)
        { return Query("Supported",body,body.position,platforms); }
        private static void TileAt(int x,int y,int type,int slope=0,bool half=false)
        { var tile=new Tile();tile.active(true);tile.type=(ushort)type;tile.slope((byte)slope);tile.halfBrick(half);Main.tile[x,y]=tile; }
        internal static void Run(object context, bool comparison = false)
        {
            NativeCombatAimChecks.Initialize();
            new Harmony("JueMingR.Tests.QuickItemOutlets").Unpatch(AccessTools.Method(typeof(Player),"Spawn"),HarmonyPatchType.Prefix,"JueMingR.Tests.QuickItemOutlets");
            Main.spawnTileX=60;Main.spawnTileY=58;Main.leftWorld=Main.topWorld=0;Main.rightWorld=Main.maxTilesX*16;Main.bottomWorld=Main.maxTilesY*16;
            for(int x=2;x<118;x++)TileAt(x,60,TileID.Stone);
            NativeCompanionCoreLifecycleChecks.PrepareHuman();
            var host=Get(context,"Companions");var profiles=Get(host,"profiles");
            Require((bool)Get(host, "Enabled"), "companion native hooks: " + Get(host, "Feedback"));
            NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");Call(host,"Update",0UL);return (bool)Get(profiles,"Ready");});
            Require((bool)Call(host,"Create","斜平台原生代表"),"create native navigation profile");
            try { NativeQuickItemChecks.Until(()=>{Call(profiles,"Pump");return ((IEnumerable)Get(host,"Profiles")).Cast<CompanionProfile>().Any(p=>p.Name=="斜平台原生代表");}); }
            catch (Exception failure) { throw new InvalidOperationException("navigation character save: " + Get(host, "Feedback"), failure); }
            Guid id=((IEnumerable)Get(host,"Profiles")).Cast<CompanionProfile>().Single(p=>p.Name=="斜平台原生代表").Id;
            Require((bool)Call(host,"Join",id),"join native navigation profile: "+Get(host,"Feedback"));
            var body=(Player)Call(host,"GetBody",id);var actor=Call(host,"Owned",body);var human=Main.LocalPlayer;
            navigation=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionNavigation",true);
            var oldTiles=Main.tile;int oldX=Main.maxTilesX,oldY=Main.maxTilesY;double surface=Main.worldSurface;float bottom=Main.bottomWorld,right=Main.rightWorld;
            var humanPosition=human.position;var originalPosition=body.position;var originalVelocity=body.velocity;
            var scratch=typeof(Collision).GetFields(Flags).Where(f=>new[]{"up","down","stair","stairFall","sloping","honey","shimmer","landMine"}.Contains(f.Name)).ToArray();
            var scratchValues=scratch.Select(f=>f.GetValue(null)).ToArray();
            try
            {
                Require(Main.netMode==0,"native navigation leaf uses isolated single-player physics");
                Main.maxTilesX=160;Main.maxTilesY=160;Main.tile=new Tile[160,160];
                for(int x=0;x<160;x++)for(int y=0;y<160;y++)Main.tile[x,y]=new Tile();
                Main.worldSurface=40;Main.bottomWorld=160*16;Main.rightWorld=160*16;
                CommonMotionSample(host, body, human);
                if (comparison)
                {
                    NativeCompanionPlannedMovementChecks.SampleFastMotion(host, actor, body, human);
                    return;
                }
                var probe=new Player{width=20,height=42,active=true,position=new Vector2(1608,1118),gravity=.4f,gravDir=1,maxRunSpeed=3,runAcceleration=.08f,runSlowdown=.2f,maxFallSpeed=10};
                TileAt(100,72,TileID.Platforms,1);
                var references=new List<Tuple<int,int,Tile>>();for(int x=94;x<108;x++)for(int y=66;y<77;y++)references.Add(Tuple.Create(x,y,Main.tile[x,y]));
                for(int i=0;i<scratch.Length;i++)scratch[i].SetValue(null,i%2==0);
                var beforeScratch=scratch.Select(f=>f.GetValue(null)).ToArray();var beforePosition=probe.position;var beforeVelocity=probe.velocity;
                Require(Support(probe) && !Support(probe,false),"one-pixel advanced contact detects slope1 platform; fall-through excludes same platform");
                Require(Query("WalkSafe",probe,-1),"continuous ascending platform steps retain corrected Y");
                Require(probe.position==beforePosition && probe.velocity==beforeVelocity,"support and continuous walking queries never move the actual body");
                Main.tile[100,72]=new Tile();TileAt(100,72,TileID.Platforms,2);probe.position=new Vector2(1592,1114);
                Require(Support(probe) && !Support(probe,false),"mirror platform support uses same native geometry");
                TileAt(100,72,TileID.Platforms);probe.position=new Vector2(1600,1110);Require(Support(probe),"ordinary platform support is preserved");
                TileAt(100,72,TileID.Stone,0,true);probe.position=new Vector2(1600,1118);Require(Support(probe),"half-brick support is preserved");
                Require(scratch.Select(f=>f.GetValue(null)).SequenceEqual(beforeScratch),"all eight native scratch flags restored across every query");
                foreach(var entry in references.Where(t=>t.Item1!=100 || t.Item2!=72))Require(ReferenceEquals(Main.tile[entry.Item1,entry.Item2],entry.Item3),"query preserved loaded tile instance");
                Main.tile[98,70]=null;
                Require(!Support(probe) && Main.tile[98,70]==null && navigation.GetMethod("InspectWalk",Flags).Invoke(null,new object[]{probe,1}).ToString()=="Unknown",
                    "null native footprint remains distinct unknown and is never allocated by prediction");
                var brain=Activator.CreateInstance(host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionBehaviorHost"),true);
                var unknownOwner=new Player{active=true,position=probe.position+new Vector2(100,0)};
                var intent=(CompanionControlFrame)Call(brain,"Decide",probe,unknownOwner,new CompanionBehavior(),true);
                Require(intent.Move==0 && !intent.Jump && !intent.Drop,"unknown body geometry cannot be mistaken for an airborne horizontal route grant");
                Main.tile[98,70]=new Tile();
                for(int x=80;x<125;x++)TileAt(x,72,TileID.Stone);
                TileAt(101,71,TileID.Stone);probe.position=new Vector2(1592,1110);
                Require(Query("WalkSafe",probe,1) && !Query("Wall",probe,1),"one normal native step remains walkable without a false tall-wall decision");
                Main.tile[101,71]=new Tile();
                probe.position=new Vector2(1600,1110);probe.velocity=Vector2.Zero;
                var plan=Activator.CreateInstance(navigation,true);Call(plan,"ObserveCapabilities",probe);var target=probe.Center+new Vector2(100,-60);
                bool accepted=(bool)Call(plan,"JumpSafe",probe,target,1,true);int builds=(int)Get(plan,"TrajectoryBuilds");
                Require(accepted && (bool)Call(plan,"JumpSafe",probe,target,1,false) && (int)Get(plan,"TrajectoryBuilds")==builds,"unchanged native trajectory reuses bounded success without a second simulation");
                probe.velocity=new Vector2(-8,0);
                Require(!(bool)Call(plan,"JumpSafe",probe,target,1,true),"remaining opposite launch momentum cannot approve a landing behind the requested direction");
                probe.velocity=Vector2.Zero;
                Require((bool)Call(plan,"JumpSafe",probe,target,1,true),"restoring actual launch state restores the finite valid trajectory");builds=(int)Get(plan,"TrajectoryBuilds");
                TileAt(104,67,TileID.Stone);
                Call(plan,"JumpSafe",probe,target,1,true);Require((int)Get(plan,"TrajectoryBuilds")==builds+1,"changed intermediate headroom invalidates successful path dependency");
                Main.tile[104,67]=new Tile();
                for(int x=80;x<125;x++){Main.tile[x,72]=new Tile();TileAt(x,75,TileID.Stone);}
                TileAt(100,72,TileID.Platforms,1);probe.position=new Vector2(1608,1118);
                Require(Query("DropSafe",probe),"drop-through requires actual platform support and bounded lower landing");
                for(int x=80;x<125;x++)Main.tile[x,75]=new Tile();
                Require(!Query("WalkSafe",probe,1),"real cliff has no fabricated support route");
                GroundQueryWork(host,probe);
                for(int x=80;x<125;x++)TileAt(x,73,TileID.Stone);
                NativeLanding(host,actor,body,human);
                NativeWallCrossing(host,actor,body,human);
                NativeDetour(host,actor,body,human);
                NativeTeamSlices(host,actor,body,human,profiles);
                NativeCompanionMobilityChecks.Run(host,body,human);
                NativeAttackChoice(host,actor,body,human);
                NativeAirEscape(host,actor,body,human,id);
                NativePetAndEventItems(host,body);
                // Keep the real arrows and thrown items from the shape checks
                // out of the preceding native combat simulation.
                NativeCompanionPlannedMovementChecks.Run(host, actor, body, human);
                NativeCompanionShapeHandChecks.Run(host, actor, body, human);
                NativePaladinShare(host,body,human);
                Console.WriteLine("PASS companion navigation: native slope/halfbrick/platform contact, continuous uphill, cliff/drop, scratch/tile identity, bounded dependency cache and actual update landing-direction retirement. No historical exact terrain/Steam/FPS claim.");
            }
            finally
            {
                Main.tile=oldTiles;Main.maxTilesX=oldX;Main.maxTilesY=oldY;Main.worldSurface=surface;Main.bottomWorld=bottom;Main.rightWorld=right;
                body.position=originalPosition;body.velocity=originalVelocity;human.position=humanPosition;
                for(int i=0;i<scratch.Length;i++)scratch[i].SetValue(null,scratchValues[i]);Call(host,"Recall",id);
            }
        }

        private static void CommonMotionSample(object host, Player body, Player human)
        {
            for (int x = 61; x < 110; x++) TileAt(x, 82, TileID.Stone);
            body.position = new Vector2(72 * 16, 82 * 16 - body.height);
            body.velocity = Vector2.Zero;
            human.position = body.position;
            for (int i = 0; i < 18; i++) NativeStep(host, body);
            long idle = 0, idleMaximum = 0;
            for (int i = 0; i < 120; i++)
            {
                long step = TimedNativeStep(host, body);
                idle += step; idleMaximum = Math.Max(idleMaximum, step);
            }

            human.position += new Vector2(500, 0);
            int firstMove = -1, walking = 0, lost = 0;
            long moving = 0, movingMaximum = 0;
            for (int i = 0; i < 65; i++)
            {
                long step = TimedNativeStep(host, body);
                moving += step; movingMaximum = Math.Max(movingMaximum, step);
                if (body.controlRight) { if (firstMove < 0) firstMove = i; walking++; }
                else if (firstMove >= 0) lost++;
            }

            int wall = (int)(body.Right.X + 8) / 16 + 1;
            for (int y = 71; y < 82; y++) TileAt(wall, y, TileID.Stone);
            int braking = -1;
            for (int i = 0; i < 12; i++)
            {
                NativeStep(host, body);
                if (!body.controlRight) { braking = i; break; }
            }
            for (int y = 71; y < 82; y++) Main.tile[wall, y] = new Tile();
            int recovering = -1;
            for (int i = 0; i < 12; i++)
            {
                NativeStep(host, body);
                if (body.controlRight) { recovering = i; break; }
            }
            Console.WriteLine(FormattableString.Invariant($"NAV_COMMON idle120Ms={Milliseconds(idle):F2} idleMaxMs={Milliseconds(idleMaximum):F2} moving65Ms={Milliseconds(moving):F2} movingMaxMs={Milliseconds(movingMaximum):F2} firstControl={firstMove} held={walking} releasedDuringOrdinaryMotion={lost} brakeUpdate={braking} recoveryUpdate={recovering}"));
            Require(firstMove >= 0 && walking > 0 && braking >= 0 && recovering >= 0, "real route responds to moving owner, wall and restored terrain");
        }

        private static void NativeDetour(object host, object actor, Player body, Player human)
        {
            // The wall immediately toward the owner exceeds the ordinary jump.
            // The reachable stair starts left of the body, behind the target.
            var area = new Dictionary<Point, Tile>();
            const int origin = 43; // Beyond Player.BordersMovement's 40-tile inset.
            for (int x = 14 + origin; x <= 55 + origin; x++)
                for (int y = 35; y <= 66; y++)
                { area[new Point(x, y)] = Main.tile[x, y]; Main.tile[x, y] = new Tile(); }
            try
            {
                for (int x = 14 + origin; x <= 55 + origin; x++) TileAt(x, 62, TileID.Stone);
                for (int x = 34 + origin; x <= 53 + origin; x++)
                    for (int y = 47; y < 62; y++) TileAt(x, y, TileID.Stone);
                for (int x = 18 + origin; x <= 22 + origin; x++) TileAt(x, 58, TileID.Platforms);
                for (int x = 20 + origin; x <= 25 + origin; x++) TileAt(x, 54, TileID.Platforms);
                for (int x = 24 + origin; x <= 29 + origin; x++) TileAt(x, 50, TileID.Platforms);
                for (int x = 27 + origin; x <= 34 + origin; x++) TileAt(x, 47, TileID.Platforms);
                body.position = new Vector2((30 + origin) * 16, 62 * 16 - body.height);
                body.velocity = Vector2.Zero;
                human.position = new Vector2((39 + origin) * 16, 47 * 16 - human.height);
                var initial = body.position;
                long elapsed = 0, maximum = 0;
                int ticks = 0, firstAway = -1, largestSlice = 0;
                object movement = Get(Get(actor, "Behavior"), "navigation");
                int work = (int)Get(movement, "RouteWork"), previousWork = work;
                for (; ticks < 1200 && Vector2.DistanceSquared(body.Center, human.Center) > 55 * 55; ticks++)
                {
                    long step = TimedNativeStep(host, body);
                    elapsed += step; maximum = Math.Max(maximum, step);
                    int nextWork = (int)Get(movement, "RouteWork");
                    largestSlice = Math.Max(largestSlice, nextWork - previousWork);
                    previousWork = nextWork;
                    if (firstAway < 0 && body.position.X < initial.X - 12) firstAway = ticks;
                }
                Console.WriteLine("NAV_ROUTE detourTicks=" + ticks + " firstAway=" + firstAway + " totalWork=" + (previousWork - work) +
                    " maxSlice=" + largestSlice + FormattableString.Invariant($" nativeStepMs={Milliseconds(elapsed):F2} nativeMaxMs={Milliseconds(maximum):F2}") +
                    " actual=" + body.position + " goal=" + human.position + " status=" + Get(actor, "Status"));
                Require(firstAway >= 0 && ticks < 1200 && !(bool)Get(actor, "Faulted"),
                    "real body backtracks to the platforms, reaches the upper owner region, and stays valid");
                Require(largestSlice <= 160 && previousWork > work, "detour consumes incremental work inside its selected world-step bound");
            }
            finally
            {
                foreach (var cell in area) Main.tile[cell.Key.X, cell.Key.Y] = cell.Value;
            }
        }

        private static void NativeTeamSlices(object host, object actor, Player body, Player human, object profiles)
        {
            const string name = "规划轮转第二人";
            Require((bool)Call(host, "Create", name), "create second profile through the regular outlet");
            var id = (Guid)Get(host, "LastCreatedId");
            NativeQuickItemChecks.Until(() => { Call(profiles, "Pump"); return (bool)Call(profiles, "IsSaved", id); });
            Require((bool)Call(host, "Join", id), "activate second real body: " + Get(host, "Feedback"));
            var other = (Player)Call(host, "GetBody", id);
            object firstPlan = Get(Get(actor, "Behavior"), "navigation");
            object secondPlan = Get(Get(Call(host, "Owned", other), "Behavior"), "navigation");
            const int origin = 43;
            for (int x = 15 + origin; x <= 60 + origin; x++) TileAt(x, 89, TileID.Stone);
            for (int y = 73; y < 89; y++) TileAt(30 + origin, y, TileID.Stone);
            body.position = new Vector2((30 + origin) * 16 - body.width - 2, 89 * 16 - body.height);
            other.position = new Vector2((30 + origin) * 16 - other.width - 2, 89 * 16 - other.height);
            human.position = new Vector2((56 + origin) * 16, 89 * 16 - human.height);
            body.velocity = other.velocity = Vector2.Zero;
            body.fallStart = body.fallStart2 = (int)(body.position.Y / 16);
            other.fallStart = other.fallStart2 = (int)(other.position.Y / 16);
            int firstGrant = -1, secondGrant = -1, lastFirst = -1, lastSecond = -1, peak = 0;
            int withoutBudget = 0, expectedMotion = 0;
            try
            {
                for (int tick = 0; tick < 40; tick++)
                {
                    int previousFirst = (int)Get(firstPlan, "RouteWork"), previousSecond = (int)Get(secondPlan, "RouteWork");
                    NativeQuickItemChecks.BeginWorldStep(); Call(host, "Update", (ulong)Main.GameUpdateCount);
                    body.Update(body.whoAmI); other.Update(other.whoAmI);
                    int a = (int)Get(firstPlan, "RouteWork") - previousFirst, b = (int)Get(secondPlan, "RouteWork") - previousSecond;
                    if (a > 0) { if (firstGrant == -1) firstGrant = tick; lastFirst = tick; }
                    if (b > 0) { if (secondGrant == -1) secondGrant = tick; lastSecond = tick; }
                    peak = Math.Max(peak, a + b);
                    Require(a == 0 || b == 0, "only one real body expands a new frontier on any world step");
                }
                other.position = new Vector2((33 + origin) * 16, 89 * 16 - other.height);
                other.velocity = Vector2.Zero;
                for (int tick = 0; tick < 24; tick++)
                {
                    NativeQuickItemChecks.BeginWorldStep(); Call(host, "Update", (ulong)Main.GameUpdateCount);
                    body.Update(body.whoAmI); other.Update(other.whoAmI);
                    if (ReferenceEquals(Get(host, "planningActor"), actor))
                    { expectedMotion++; if (other.controlRight) withoutBudget++; }
                }
                Console.WriteLine($"NAV_TEAM first={firstGrant} second={secondGrant} lastFirst={lastFirst} lastSecond={lastSecond} peakTeamSlice={peak} movingWithoutSlice={withoutBudget}/{expectedMotion}");
                Require(firstGrant >= 0 && firstGrant <= 3 && secondGrant >= 0 && secondGrant <= 3 &&
                    lastFirst > firstGrant && lastSecond > secondGrant && peak <= 160 && !body.dead && !other.dead,
                    "two blocked real bodies both retain bounded search service: " + body.dead + "/" + other.dead);
                Require(expectedMotion >= 8 && withoutBudget >= expectedMotion - 1, "ordinary safe controls continue while the other body searches");
            }
            finally
            {
                Call(host, "Recall", id);
                for (int x = 15 + origin; x <= 60 + origin; x++) Main.tile[x, 89] = new Tile();
                for (int y = 73; y < 89; y++) Main.tile[30 + origin, y] = new Tile();
            }
        }

        private static void NativeWallCrossing(object host,object actor,Player body,Player human)
        {
            for(int y=71;y<73;y++)TileAt(106,y,TileID.Stone);
            try
            {
                body.position=new Vector2(106*16-body.width-1,73*16-body.height);
                body.velocity=Vector2.Zero;body.releaseJump=false;
                human.position=body.position+new Vector2(130,0);
                NativeStep(host,body);
                Require(!body.controlJump && body.releaseJump,"real obstacle demand allows the initializing jump release");
                bool rose=false;
                for(int tick=0;tick<120 && body.position.X<107*16;tick++)
                {
                    NativeStep(host,body);
                    rose|=body.velocity.Y<0 && body.Bottom.Y<73*16-4;
                }
                Require(rose && body.position.X>=107*16 && !(bool)Get(actor,"Faulted"),
                    "a legal native vertical ascent can clip the initial wall face and still physically clear its far side: "+body.position+" "+body.velocity+" "+Get(actor,"Status"));
            }
            finally { for(int y=71;y<73;y++)Main.tile[106,y]=new Tile(); }
        }
        private static void GroundQueryWork(object host,Player probe)
        {
            var brainType=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionBehaviorHost",true);
            var behavior=Activator.CreateInstance(brainType,true);var owner=new Player{active=true,position=probe.position};
            Call(behavior,"Decide",probe,owner,new CompanionBehavior(),true);
            var plan=Get(behavior,"navigation");
            Require((int)Get(plan,"DropBuilds")==0,"same-height target never requests the lower-landing search");
            owner.position=probe.position+new Vector2(0,100);
            Call(behavior,"Decide",probe,owner,new CompanionBehavior(),true);
            int dropBuilds=(int)Get(plan,"DropBuilds"),dropMoves=(int)Get(plan,"DropGeometryMoves");
            Require(dropBuilds==1 && dropMoves>100,"ordinary known no-landing case really enters the long native drop query once");
            for(int tick=0;tick<20;tick++)
            { NativeQuickItemChecks.BeginWorldStep();Call(behavior,"Decide",probe,owner,new CompanionBehavior(),tick%2==0); }
            Require((int)Get(plan,"DropBuilds")==dropBuilds && (int)Get(plan,"DropGeometryMoves")==dropMoves,
                "unchanged ordinary failure across real world-step clocks validates its finite reads without rebuilding the drop trajectory");
            TileAt(100,78,TileID.Stone);
            Require(!(bool)Call(plan,"PlanDrop",probe,false) && (int)Get(plan,"DropGeometryMoves")==dropMoves,
                "changed landing facts cannot rebuild outside the granted team planning slice");
            Require((bool)Call(plan,"PlanDrop",probe,true) && (int)Get(plan,"DropBuilds")==dropBuilds+1,
                "changed lower terrain invalidates failure and the next granted slice finds its actual new landing");
            int changedDropMoves=(int)Get(plan,"DropGeometryMoves");
            Require((bool)Call(plan,"PlanDrop",probe,false) && (int)Get(plan,"DropGeometryMoves")==changedDropMoves,
                "known successful drop stays usable when another body owns the planning slice");
            probe.controlDown=true;probe.SlopingCollision(true,true);probe.controlDown=false;
            Require(probe.stairFall,"native sloping consumer retains accepted fall-through after the input edge");
            var falling=Activator.CreateInstance(brainType,true);
            var fallingIntent=(CompanionControlFrame)Call(falling,"Decide",probe,owner,new CompanionBehavior(),true);
            Require((int)Get(Get(falling,"navigation"),"DropBuilds")==0 && !fallingIntent.Drop &&
                !(bool)Call(plan,"PlanDrop",probe,true) && (int)Get(plan,"DropGeometryMoves")==changedDropMoves,
                "native in-progress fall-through is not reclassified as platform support or a fresh drop plan");
            probe.stairFall=false;
            Main.tile[100,78]=new Tile();

            var originalPosition=probe.position;probe.position=new Vector2(1615,1125);probe.velocity=Vector2.Zero;
            var walk=Activator.CreateInstance(navigation,true);
            Require(Call(walk,"PlanWalk",probe,1,true).ToString()=="Unsafe","real platform edge enters a known finite no-ground search");
            int walkBuilds=(int)Get(walk,"WalkBuilds"),searches=(int)Get(walk,"WalkSearchSteps"),walkMoves=(int)Get(walk,"WalkGeometryMoves");
            Require(searches>0,"known ordinary edge actually exercises the vertical ground-search work");
            for(int tick=0;tick<20;tick++)
            { NativeQuickItemChecks.BeginWorldStep();Require(Call(walk,"PlanWalk",probe,1,false).ToString()=="Unsafe","known failed walk remains a failed walk"); }
            Require((int)Get(walk,"WalkBuilds")==walkBuilds && (int)Get(walk,"WalkSearchSteps")==searches && (int)Get(walk,"WalkGeometryMoves")==walkMoves,
                "stable known edge does not rebuild its multi-step search on every ordinary decision");
            var delayed=Activator.CreateInstance(navigation,true);
            for(int tick=0;tick<4;tick++)
            {
                probe.position+=new Vector2(1,0);probe.velocity=new Vector2(.5f,0);
                Require(Call(delayed,"PlanWalk",probe,1,false).ToString()=="Deferred" && !(bool)Call(delayed,"PlanDrop",probe,false),
                    "moving cache misses defer new vertical/drop planning when the team slice belongs elsewhere");
            }
            Require((int)Get(delayed,"WalkSearchSteps")==0 && (int)Get(delayed,"DropGeometryMoves")==0,
                "moving second body cannot bypass the team slice with one 48/160-step query per frame");
            probe.position=new Vector2(1615,1125);probe.velocity=Vector2.Zero;
            TileAt(101,74,TileID.Stone);TileAt(102,74,TileID.Stone);
            Require(Call(walk,"PlanWalk",probe,1,false).ToString()=="Deferred" && (int)Get(walk,"WalkSearchSteps")==searches,
                "changed path support invalidates the old answer without an ungranted expensive rebuild");
            Require(Call(walk,"PlanWalk",probe,1,true).ToString()=="Clear" && (int)Get(walk,"WalkSearchSteps")>searches,
                "next granted slice recovers a native continuous safe walking route from changed terrain");
            Console.WriteLine("NAV_GROUND_WORK dropBuilds="+dropBuilds+" dropMoves="+dropMoves+" stableDropMoves="+dropMoves+
                " changedDropMoves="+changedDropMoves+" walkBuilds="+walkBuilds+" walkMoves="+walkMoves+" verticalSearchSteps="+searches+
                " deferredMovingSearchSteps="+Get(delayed,"WalkSearchSteps")+" deferredMovingDropMoves="+Get(delayed,"DropGeometryMoves"));
            Main.tile[101,74]=new Tile();Main.tile[102,74]=new Tile();probe.position=originalPosition;probe.velocity=Vector2.Zero;
        }
        private static void NativeLanding(object host,object actor,Player body,Player human)
        {
            body.position=new Vector2(1608,72*16+8-body.height);body.velocity=Vector2.Zero;human.position=body.position;
            for(int tick=0;tick<8;tick++)NativeStep(host,body);
            Require(!(bool)Get(actor,"Faulted") && Support(body),"real native update settles on slope platform: "+body.position+" "+body.velocity+" "+Get(actor,"Status"));
            for (int x = 95; x <= 102; x++) TileAt(x, 67, TileID.Platforms);
            var start=body.position;
            human.position=new Vector2(body.position.X, 67*16-human.height);
            bool airborne=false,landed=false;
            // Model first-activation input without the old fixture's manual
            // releaseJump=true. The destination remains above throughout.
            body.releaseJump=false;
            NativeStep(host,body);
            Require(!body.controlJump && body.releaseJump,"native JumpMovement really consumes a neutral frame at first takeoff");
            for(int tick=0;tick<150;tick++)
            {
                NativeStep(host,body);
                Require(!(bool)Get(actor,"Faulted"),"actual native navigation must not hide update exception: "+Get(actor,"Status"));
                airborne|=body.position.Y<start.Y-2 && body.velocity.Y<0;
                if(airborne && body.velocity.Y>=0 && Support(body)){landed=true;break;}
            }
            Require(airborne && landed,"actual follow input takes off and lands on slope platform");
            // Set only the fixture's incoming pose. A millimetre of geometric
            // support while falling does not supply native takeoff admission.
            for (int x = 93; x <= 103; x++) TileAt(x, 63, TileID.Platforms);
            human.position=new Vector2(body.position.X, 63*16-human.height);
            body.position-=new Vector2(0,.5f);body.velocity=new Vector2(0,.5f);body.releaseJump=false;
            start=body.position;
            NativeStep(host,body);
            Require(!body.controlJump && body.releaseJump,"native update consumes release from a pre-landing support pose");
            airborne=landed=false;
            for(int tick=0;tick<150;tick++)
            {
                NativeStep(host,body);
                airborne|=body.position.Y<start.Y-2 && body.velocity.Y<0;
                if(airborne && body.velocity.Y>=0 && Support(body)){landed=true;break;}
            }
            Require(airborne && landed,"continuously elevated goal permits a second real takeoff and landing after pre-landing release");
            human.position=body.position-new Vector2(100,0);
            NativeStep(host,body);
            Require(body.controlLeft && !body.controlRight && !(bool)Get(actor,"Faulted"),"after genuine slope landing old jump direction retires and new owner direction enters Player.Update");
            var at=body.position;body.Update(body.whoAmI);Require(body.position==at,"same world-step duplicate native body update remains suppressed");
            for (int x = 93; x <= 103; x++) { Main.tile[x, 63] = new Tile(); Main.tile[x, 67] = new Tile(); }
        }

        private static void NativeAttackChoice(object host,object actor,Player body,Player human)
        {
            var id=((CompanionProfile)Get(actor,"Profile")).Id;
            var items=(Item[])body.inventory.Clone();
            var wallTarget=Main.npc[7];var openTarget=Main.npc[8];
            var itemRules=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionItemRules",true);
            Func<int,bool> eligible=type=>(bool)itemRules.GetMethod("WeaponEligible",Flags).Invoke(null,new object[]{new Item(type)});
            Require(eligible(ItemID.CoinGun) && eligible(5519) && eligible(ItemID.BlandWhip) && !eligible(ItemID.WoodenArrow) && !eligible(ItemID.CopperPickaxe),
                "shared native eligibility distinguishes coin-ammo weapon, projectile axe and whip from ammunition and tools");
            try
            {
                for(int x=80;x<148;x++)TileAt(x,73,TileID.Stone);
                for(int y=62;y<73;y++)TileAt(104,y,TileID.Stone);
                body.position=new Vector2(110*16,73*16-body.height);body.velocity=Vector2.Zero;
                human.position=body.position-new Vector2(246,0);
                body.inventory[40]=new Item(ItemID.WoodenSword);
                body.inventory[41]=new Item(ItemID.WoodenBow);
                body.inventory[42]=new Item(ItemID.Shuriken){stack=30};
                body.inventory[54]=new Item(ItemID.WoodenArrow){stack=40};
                InitializeTarget(wallTarget,7,body.Center+new Vector2(-155,-8));
                InitializeTarget(openTarget,8,body.Center+new Vector2(265,-8));
                // Zero preferred weapons naturally find a funded bow and a
                // visible farther actor-centered target. Prove a real hit.
                var zero=new CompanionBehavior{Follow=false,HealthRecoveryEnabled=false};
                body.inventory[42]=new Item();
                Call(host,"SetBehavior",id,zero);
                for(int tick=0;tick<100 && openTarget.life==openTarget.lifeMax;tick++)NativeCombatStep(host,body);
                Require(!(bool)Get(actor,"Faulted") && openTarget.life<openTarget.lifeMax && wallTarget.life==wallTarget.lifeMax && body.inventory[54].stack<40,
                    "wall behind closer enemy falls back to a real native bow hit on the farther reachable enemy: "+Get(actor,"Status")+" selected="+body.selectedItem+" aim="+Get(actor,"Aim"));
                var preferred=zero.Clone();
                preferred.MainWeapons=new[]{new CompanionWeaponPreference(ItemID.WoodenSword,0,40),new CompanionWeaponPreference(ItemID.WoodenBow,0,41)};
                Call(host,"SetBehavior",id,preferred);
                body.inventory[42]=new Item(ItemID.Shuriken){stack=30};
                body.inventory[54]=new Item();
                for(int tick=0;tick<100 && body.inventory[42].stack==30;tick++)NativeCombatStep(host,body);
                Require(body.selectedItem==42 && body.inventory[42].stack<30,
                    "unfunded bow and out-of-range other preference permit actual use of a third own-bag weapon");
                Require(((CompanionProfile)Get(actor,"Profile")).Behavior.MainWeapons.Length==2,"fallback never becomes a new preference");
                body.inventory[54]=new Item(ItemID.WoodenArrow){stack=40};
                for(int tick=0;tick<100 && body.inventory[54].stack==40;tick++)NativeCombatStep(host,body);
                Require(body.selectedItem==41 && body.inventory[54].stack<40,"replenished bow again starts a native attack at its preferred range");
                var combat=Get(actor,"Combat");
                int sweeps=(int)Get(combat,"CandidateSweeps"),probes=(int)Get(combat,"PairProbes"),shots=(int)Get(combat,"BasicShotReplays");
                for(int tick=0;tick<12;tick++)
                { NativeCombatStep(host,body);Require(body.selectedItem==41,"small unchanged distance keeps the same selected weapon"); }
                int newSweeps=(int)Get(combat,"CandidateSweeps")-sweeps,newProbes=(int)Get(combat,"PairProbes")-probes;
                Require(newSweeps>0 && newSweeps<=12 && newProbes<=newSweeps,"native attack workload admits at most one sweep and one pair probe per world step");
                int newShots=(int)Get(combat,"BasicShotReplays")-shots;
                Require(newShots>0 && newShots<=12*4,"stable open and blocked native targets reuse bounded basic shot checks before the actual item consumer");
                wallTarget.active=false;
                openTarget.Center=body.Center+new Vector2(46,-4);
                for(int tick=0;tick<100 && (body.selectedItem!=40 || body.itemAnimation<=0);tick++)NativeCombatStep(host,body);
                Require(body.selectedItem==40 && body.itemAnimation>0,"equally preferred sword becomes the actual native hand at close range");
                var melee=preferred.Clone();melee.MainWeapons=new[]{new CompanionWeaponPreference(ItemID.WoodenSword,0,40)};
                body.inventory[41]=new Item();body.inventory[42]=new Item();body.inventory[54]=new Item();
                Call(host,"SetBehavior",id,melee);
                foreach(var shot in Main.projectile)if(shot.active && shot.owner==body.whoAmI)shot.active=false;
                Array.Clear(openTarget.immune,0,openTarget.immune.Length);
                openTarget.Center=body.Center+new Vector2(155,0);
                float initial=body.position.X;int health=openTarget.life;
                for(int tick=0;tick<120 && openTarget.life==health;tick++)NativeCombatStep(host,body);
                Require(body.position.X>initial+36 && openTarget.life<health,"ordinary usable melee plan closes real distance and strikes using the native body");
                NativeRangedReposition(host,actor,body,human,openTarget,id,zero);
                NativeManaStandstill(host,body,human,openTarget,id,zero);
                NativeSummonHands(host,body,openTarget,id,zero);
                Console.WriteLine("PASS companion combat selection: blocked near receiver, actual far arrow hit, own ammo/fallback/restock, preference stability and native melee approach.");
            }
            finally
            {
                wallTarget.active=openTarget.active=false;
                Array.Copy(items,body.inventory,items.Length);
                foreach(var shot in Main.projectile)if(shot.active && shot.owner==body.whoAmI)shot.active=false;
            }
        }

        private static void NativeRangedReposition(object host,object actor,Player body,Player human,NPC target,Guid id,CompanionBehavior preferences)
        {
            // Keep both the incoming body and its chosen firing position within
            // the stock 40-tile player boundary of this 160-tile test world.
            const int wall=113;
            var upper=Main.tile[wall,71];var lower=Main.tile[wall,72];
            var harmony=new Harmony("JueMingR.Tests.CompanionBasicReposition");
            var aim=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackAim",true);
            var previousBehavior=Get(actor,"Behavior");
            basicRangedBody=body;
            harmony.Patch(aim.GetMethod("ActorSolution",Flags),prefix:new HarmonyMethod(typeof(NativeCompanionNavigationChecks),nameof(UseBasicGeometry)));
            try
            {
                TileAt(wall,71,TileID.Stone);TileAt(wall,72,TileID.Stone);
                var start=new Vector2(wall*16-body.width-28,73*16-body.height);
                body.position=human.position=start;
                body.velocity=Vector2.Zero;body.releaseJump=true;body.releaseUseItem=true;
                body.itemAnimation=body.itemTime=body.reuseDelay=0;body.channel=false;
                body.inventory[40]=new Item();body.inventory[41]=new Item(ItemID.WoodenBow);
                body.inventory[54]=new Item(ItemID.WoodenArrow){stack=20};
                body.selectedItemState.Select(41);
                foreach(var shot in Main.projectile)if(shot.active && shot.owner==body.whoAmI)shot.active=false;
                InitializeTarget(target,8,body.Center+new Vector2(150,0));target.damage=0;
                Set(actor,"Behavior",Activator.CreateInstance(previousBehavior.GetType(),true));
                Call(Get(actor,"Combat"),"Reset");
                var select=preferences.Clone();select.MainWeapons=new[]{new CompanionWeaponPreference(ItemID.WoodenBow,0,41)};
                Call(host,"SetBehavior",id,select);
                NativeCombatStep(host,body);
                Require(body.inventory[54].stack==20,"an initially blocked low-wall shot preserves real ammo");
                float peak=body.position.Y;
                object behavior = Get(actor, "Behavior");
                object movement = Get(behavior, "navigation");
                bool advisedJump = false;
                for(int tick=0;tick<105 && body.inventory[54].stack==20;tick++)
                {
                    NativeCombatStep(host,body);
                    peak=Math.Min(peak,body.position.Y);
                    advisedJump |= (bool)Get(behavior, "lateralJump");
                }
                Require(body.inventory[54].stack<20 && peak<start.Y-9 &&
                    Main.projectile.Any(p=>p.active && p.owner==body.whoAmI && p.type==ProjectileID.WoodenArrowFriendly),
                    "the only target cannot spend ammo until a real local takeoff opens a native shot: position="+body.position+" peak="+peak+" start="+start+" status="+Get(actor,"Status")+
                    " elevated=" + advisedJump + " localGoal=" + Get(behavior, "lateralGoal") + " cachedJump=" + Get(movement, "cachedResult") +
                    " jumpBuilds=" + Get(movement, "TrajectoryBuilds"));
                Console.WriteLine("PASS blocked low wall: actual native movement opened the only bow shot and paid ammo.");
            }
            finally
            {
                basicRangedBody=null;harmony.UnpatchAll(harmony.Id);
                Main.tile[wall,71]=upper;Main.tile[wall,72]=lower;
                body.inventory[41]=new Item();body.inventory[54]=new Item();
                Set(actor,"Behavior",previousBehavior);
                foreach(var shot in Main.projectile)if(shot.active && shot.owner==body.whoAmI)shot.active=false;
            }
        }

        private static void NativeManaStandstill(object host,Player body,Player human,NPC target,Guid id,CompanionBehavior preferences)
        {
            var spell=new Item(ItemID.DiamondStaff);
            Require(spell.mana>0,"the locked test spell actually has a mana cost");
            body.inventory[40]=spell;
            body.statMana=0;body.manaRegenDelay=300;
            target.Center=body.Center+new Vector2(230,-8);
            human.position=body.position;
            var select=preferences.Clone();select.Follow=true;select.ManaRecoveryEnabled=false;
            select.MainWeapons=new[]{new CompanionWeaponPreference(spell.type,spell.prefix,40)};
            Call(host,"SetBehavior",id,select);
            bool waiting=false;
            for(int tick=0;tick<80;tick++)
            {
                NativeCombatStep(host,body);
                Require(!body.controlLeft && !body.controlRight,"a visible target with no mana does not request an unnecessary combat strafe");
                if((string)Call(host,"Status",id)=="等待魔力恢复"){waiting=true;break;}
            }
            Require(waiting && !body.controlUseItem && body.statMana<spell.mana,"resource-only denial reports mana and emits no native cast");
        }

        private static void NativeSummonHands(object host,Player body,NPC target,Guid id,CompanionBehavior preferences)
        {
            body.inventory[40]=new Item(ItemID.SlimeStaff);
            body.inventory[41]=new Item(ItemID.QueenSpiderStaff);
            body.statMana=body.statManaMax=body.statManaMax2=200;
            target.Center=body.Center+new Vector2(135,-8);
            var select=preferences.Clone();
            select.MainWeapons=new[]{new CompanionWeaponPreference(ItemID.SlimeStaff,0,40)};
            Call(host,"SetBehavior",id,select);
            Func<int,int> count=type=>Main.projectile.Count(p=>p.active && p.owner==body.whoAmI && p.type==type);
            for(int tick=0;tick<120 && count(ProjectileID.BabySlime)==0;tick++)NativeCombatStep(host,body);
            Require(count(ProjectileID.BabySlime)==1 && body.FindBuffIndex(BuffID.BabySlime)>=0,
                "native .8 Slime Staff fills an available slot and applies its native minion buff: count="+count(ProjectileID.BabySlime)+
                " held="+body.HeldItem.type+" slots="+body.slotsMinions+"/"+body.maxMinions+" mana="+body.statMana+" status="+Call(host,"Status",id));
            select.MainWeapons=new[]{new CompanionWeaponPreference(ItemID.QueenSpiderStaff,0,41)};
            Call(host,"SetBehavior",id,select);
            for(int tick=0;tick<180 && count(ProjectileID.SpiderHiver)==0;tick++)NativeCombatStep(host,body);
            var sentry=Main.projectile.FirstOrDefault(p=>p.active && p.owner==body.whoAmI && p.type==ProjectileID.SpiderHiver);
            Require(sentry!=null,"real native sentry staff places its own turret: held="+body.HeldItem.type+" slots="+body.maxTurrets+" status="+Call(host,"Status",id));
            for(int tick=0;tick<105;tick++)NativeCombatStep(host,body);
            Require(count(ProjectileID.BabySlime)==1 && count(ProjectileID.SpiderHiver)==1 && sentry.active,
                "full minion and sentry slots keep existing native instances without automatic replacement");
            body.AddBuff(BuffID.WarTable,3600);
            for(int tick=0;tick<180 && count(ProjectileID.SpiderHiver)<2;tick++)NativeCombatStep(host,body);
            Require(body.maxTurrets==2 && count(ProjectileID.SpiderHiver)==2 && sentry.active,
                "native extra turret capacity adds a second identical sentry without replacing the first");
            var second=Main.projectile.First(p=>p.active && p.owner==body.whoAmI && p.type==ProjectileID.SpiderHiver && !ReferenceEquals(p,sentry));
            for(int tick=0;tick<150;tick++)NativeCombatStep(host,body);
            Require(count(ProjectileID.SpiderHiver)==2 && sentry.active && second.active,
                "two full native slots cause no duplicate placement or replacement after cooldown");
            body.ClearBuff(BuffID.WarTable);
        }

        private static void NativeAirEscape(object host,object actor,Player body,Player human,Guid id)
        {
            Call(host,"SetBehavior",id,new CompanionBehavior{Follow=true,AttackEnabled=false});
            Set(actor,"Behavior",Activator.CreateInstance(Get(actor,"Behavior").GetType(),true));
            for(int x=96;x<=105;x++)for(int y=65;y<73;y++)
            {
                Main.tile[x,y]=new Tile { liquid=255 };
            }
            body.position=new Vector2(100*16,66*16);
            body.velocity=new Vector2(0,2);
            body.wet=true;body.releaseJump=false;body.breath=body.breathMax/4;
            human.Center=body.Center-new Vector2(0,180);
            NativeStep(host,body);
            Require(!body.controlJump && body.releaseJump && body.velocity.Y>0 && !body.accFlipper,
                "ordinary native body falling underwater releases rejected jump for the next legal input");
            bool landed=false,tookOff=false;
            for(int tick=0;tick<80 && !tookOff;tick++)
            {
                NativeStep(host,body);
                landed|=body.velocity.Y==0 && Support(body);
                tookOff=landed && body.controlJump && body.velocity.Y<0;
            }
            Require(tookOff && body.wet && !(bool)Get(actor,"Faulted"),
                "after a real pool-bottom collision the same ordinary body can make a native jump: position="+body.position+" velocity="+body.velocity+" status="+Get(actor,"Status"));
            for(int x=96;x<=105;x++)for(int y=65;y<73;y++)Main.tile[x,y].liquid=0;
            human.Center=body.Center;
            for(int tick=0;tick<55;tick++)NativeStep(host,body);
            Require(!body.wet && !body.controlJump && body.releaseJump,
                "dry-ground transition retires the water escape hold in the real native controls");
            Console.WriteLine("PASS ordinary low-air descent, native pool-bottom jump retry and dry-land input release.");
        }

        private static void NativePetAndEventItems(object host,Player body)
        {
            var originalCursor=Main.mouseItem;
            var originalTrash=body.trashItem;
            var originalEnergy=body.inventory[52];
            var originalPet=body.miscEquips[0];var originalLight=body.miscEquips[1];
            bool petsHidden=body.hideMisc[0], lightHidden=body.hideMisc[1], ongoing=DD2Event.Ongoing;
            try
            {
                var cursor=new Item(ItemID.DD2EnergyCrystal){stack=3};Main.mouseItem=cursor;
                var energy=new Item(ItemID.DD2EnergyCrystal){stack=7};body.inventory[52]=energy;
                body.trashItem=new Item(ItemID.DD2EnergyCrystal){stack=2};
                var pet=new Item(ItemID.ZephyrFish);var light=new Item(ItemID.ShadowOrb);
                body.miscEquips[0]=pet;body.miscEquips[1]=light;body.hideMisc[0]=body.hideMisc[1]=false;
                DD2Event.Ongoing=true;
                for(int tick=0;tick<6;tick++)NativeCombatStep(host,body);
                Require(energy.stack==7 && body.trashItem.stack==2,"active native Old One's Army keeps the actual actor energy");
                foreach(var equipment in new[]{pet,light})
                    Require(body.FindBuffIndex(equipment.buffType)>=0 &&
                        Main.projectile.Any(p=>p.active && p.owner==body.whoAmI && p.type==equipment.shoot && !p.isAPreviewDummy),
                        "equipped item generates its native in-world pet projectile and buff: "+equipment.type);
                DD2Event.Ongoing=false;
                NativeStep(host,body);
                Require(body.inventory[52].IsAir && body.trashItem.IsAir && ReferenceEquals(Main.mouseItem,cursor) && cursor.stack==3,
                    "ended event purges only the actor's native item slots and leaves human cursor intact");
            }
            finally
            {
                Main.mouseItem=originalCursor;body.trashItem=originalTrash;body.inventory[52]=originalEnergy;
                body.miscEquips[0]=originalPet;body.miscEquips[1]=originalLight;
                body.hideMisc[0]=petsHidden;body.hideMisc[1]=lightHidden;DD2Event.Ongoing=ongoing;
            }
            Console.WriteLine("PASS actual owned vanity/light projectiles, active/ended event energy lifetime and human cursor isolation.");
        }

        private static bool NoSave() { return false; }
        private static void NativePaladinShare(object host,Player body,Player human)
        {
            var harmony=new Harmony("JueMingR.Tests.CompanionShieldSave");
            harmony.Patch(AccessTools.Method(typeof(WorldGen),"saveToonWhilePlaying"),prefix:new HarmonyMethod(typeof(NativeCompanionNavigationChecks),nameof(NoSave)));
            try
            {
                human.position=body.position+new Vector2(35,0);
                human.team=body.team=1;
                body.statLife=body.statLifeMax=body.statLifeMax2=400;
                body.armor[3]=new Item(ItemID.PaladinsShield);
                for(int tick=0;tick<12;tick++)NativeStep(host,body);
                Require(body.hasPaladinShield && human.FindBuffIndex(BuffID.PaladinsShield)>=0,
                    "native companion equipment gives the local human its regular team buff");
                human.UpdateBuffs(human.whoAmI);
                human.statLife=100;human.statDefense=0;human.immune=false;human.immuneTime=0;
                int defenderLife=body.statLife;
                human.Hurt(PlayerDeathReason.ByOther(0),40,0,dodgeable:false);
                Require(human.statLife==70 && body.statLife<defenderLife &&
                    body.hurtCooldowns[ImmunityCooldownID.PaladinsShield]>0,
                    "native human takes 75 percent and charges the single selected AI through its shield Hurt cooldown: human="+
                    human.statLife+" AI="+body.statLife+"/"+defenderLife+" aura="+human.defendedByPaladin+
                    " cooldown="+body.hurtCooldowns[ImmunityCooldownID.PaladinsShield]);

                body.armor[3]=new Item();human.armor[3]=new Item(ItemID.PaladinsShield);
                human.statLife=30;human.statLifeMax=human.statLifeMax2=100;
                human.UpdateEquips(human.whoAmI);
                for(int tick=0;tick<12;tick++)NativeStep(host,body);
                Require(!body.hasPaladinShield && human.hasPaladinShield && body.FindBuffIndex(BuffID.PaladinsShield)>=0,
                    "native human equipment in turn gives a close same-team AI the regular buff");
                body.statDefense=0;body.immune=false;body.immuneTime=0;
                int patientLife=body.statLife;
                Main.mapFullscreen=true;
                var cursor=Main.mouseItem;Main.mouseItem=new Item(ItemID.Wood){stack=3};
                body.Hurt(PlayerDeathReason.ByOther(0),160,0,dodgeable:false);
                Require(body.statLife==patientLife-120 && human.dead && !Main.mapFullscreen &&
                    ReferenceEquals(Main.mouseItem,human.inventory[58]) && ReferenceEquals(Main.LocalPlayer,human),
                    "AI takes native 75 percent; lethal recipient preserves real human death cursor and map changes after scope restoration");
                Main.mouseItem=cursor;
            }
            finally{harmony.UnpatchAll(harmony.Id);}
            Console.WriteLine("PASS native shield aura and damage transfer in both directions, including lethal human recipient ownership.");
        }

        private static void InitializeTarget(NPC target,int slot,Vector2 center)
        {
            target.SetDefaults(NPCID.Zombie);target.whoAmI=slot;target.active=true;target.Center=center;
            target.velocity=Vector2.Zero;target.noGravity=target.noTileCollide=true;target.life=target.lifeMax=100000;
            target.aiStyle=-1;target.knockBackResist=0;
        }

        private static void NativeCombatStep(object host,Player body)
        {
            NativeStep(host,body);
            for(int i=7;i<=8;i++)if(Main.npc[i].active)Main.npc[i].UpdateNPC(i);
            int previous=Main.ProjectileUpdateLoopIndex;
            try
            {
                for(int i=0;i<Main.projectile.Length;i++)
                    if(Main.projectile[i].active && Main.projectile[i].owner==body.whoAmI)
                    { Main.ProjectileUpdateLoopIndex=i;Main.projectile[i].Update(i); }
            }
            finally { Main.ProjectileUpdateLoopIndex=previous; }
        }
        private static void NativeStep(object host,Player body)
        { NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",(ulong)Main.GameUpdateCount);body.Update(body.whoAmI); }
        private static long TimedNativeStep(object host, Player body)
        {
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            NativeStep(host, body);
            return System.Diagnostics.Stopwatch.GetTimestamp() - start;
        }
        private static double Milliseconds(long ticks) => ticks * 1000d / System.Diagnostics.Stopwatch.Frequency;
    }
}
