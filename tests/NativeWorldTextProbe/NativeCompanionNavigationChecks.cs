using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
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
        private static bool Query(string name,Player body,params object[] tail)
        { return (bool)navigation.GetMethod(name,Flags).Invoke(null,new object[]{body}.Concat(tail).ToArray()); }
        private static bool Support(Player body,bool platforms=true)
        { return Query("Supported",body,body.position,platforms); }
        private static void TileAt(int x,int y,int type,int slope=0,bool half=false)
        { var tile=new Tile();tile.active(true);tile.type=(ushort)type;tile.slope((byte)slope);tile.halfBrick(half);Main.tile[x,y]=tile; }
        internal static void Run(object context)
        {
            NativeCombatAimChecks.Initialize();
            new Harmony("JueMingR.Tests.QuickItemOutlets").Unpatch(AccessTools.Method(typeof(Player),"Spawn"),HarmonyPatchType.Prefix,"JueMingR.Tests.QuickItemOutlets");
            Main.spawnTileX=60;Main.spawnTileY=58;Main.leftWorld=Main.topWorld=0;Main.rightWorld=Main.maxTilesX*16;Main.bottomWorld=Main.maxTilesY*16;
            for(int x=2;x<118;x++)TileAt(x,60,TileID.Stone);
            NativeCompanionCoreLifecycleChecks.PrepareHuman();
            var host=Get(context,"Companions");var profiles=Get(host,"profiles");
            NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");Call(host,"Update",0UL);return (bool)Get(profiles,"Ready");});
            Require((bool)Call(host,"Create","斜平台原生代表"),"create native navigation profile");
            NativeQuickItemChecks.Until(()=>{Call(profiles,"Pump");return ((IEnumerable)Get(host,"Profiles")).Cast<CompanionProfile>().Any(p=>p.Name=="斜平台原生代表");});
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
                Console.WriteLine("PASS companion navigation: native slope/halfbrick/platform contact, continuous uphill, cliff/drop, scratch/tile identity, bounded dependency cache and actual update landing-direction retirement. No historical exact terrain/Steam/FPS claim.");
            }
            finally
            {
                Main.tile=oldTiles;Main.maxTilesX=oldX;Main.maxTilesY=oldY;Main.worldSurface=surface;Main.bottomWorld=bottom;Main.rightWorld=right;
                body.position=originalPosition;body.velocity=originalVelocity;human.position=humanPosition;
                for(int i=0;i<scratch.Length;i++)scratch[i].SetValue(null,scratchValues[i]);Call(host,"Recall",id);
            }
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
            var start=body.position;human.position=body.position-new Vector2(0,80);bool airborne=false,landed=false;
            for(int tick=0;tick<150;tick++)
            {
                NativeStep(host,body);
                Require(!(bool)Get(actor,"Faulted"),"actual native navigation must not hide update exception: "+Get(actor,"Status"));
                airborne|=body.position.Y<start.Y-2 && body.velocity.Y<0;
                if(airborne && body.velocity.Y>=0 && Support(body)){landed=true;break;}
            }
            Require(airborne && landed,"actual follow input takes off and lands on slope platform");
            human.position=body.position-new Vector2(100,0);
            NativeStep(host,body);
            Require(body.controlLeft && !body.controlRight && !(bool)Get(actor,"Faulted"),"after genuine slope landing old jump direction retires and new owner direction enters Player.Update");
            var at=body.position;body.Update(body.whoAmI);Require(body.position==at,"same world-step duplicate native body update remains suppressed");
        }
        private static void NativeStep(object host,Player body)
        { NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",(ulong)Main.GameUpdateCount);body.Update(body.whoAmI); }
    }
}
