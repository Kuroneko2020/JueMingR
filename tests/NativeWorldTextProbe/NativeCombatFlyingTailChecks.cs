using System;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Utilities;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatFlyingTailChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static void Aim(object context,object host,NPC n)
        {typeof(NativeCombatPositionRelationChecks).GetMethod("Aim",Flags).Invoke(null,new object[]{context,host,n});}
        private static void Native(NPC n)
        {typeof(NativeCombatFighterControlChecks).GetMethod("Native",Flags).Invoke(null,new object[]{n});}
        internal static void Pet(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;
            for(int branch=0;branch<3;branch++)
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2]=new NPC();n.SetDefaults(210);n.whoAmI=2;n.position=new Vector2(650,790);n.velocity=new Vector2(1,0);n.oldVelocity=n.velocity;n.collideX=true;n.target=0;n.direction=1;n.ai[1]=120;
                p.Center=new Vector2(n.Center.X+200,960-p.height*.5f);p.tankPet=5;p.npcTypeNoAggro[210]=branch==2;var pet=Main.projectile[5];pet.SetDefaults(625);pet.whoAmI=5;pet.owner=0;pet.active=true;pet.Center=n.Center-new Vector2(80,0);pet.velocity=Vector2.Zero;
                for(int y=40;y<=57;y++){Main.tile[39,y].active(branch==1);Main.tile[39,y].type=1;}
                var search=NPCUtils.SearchForTarget(n,NPCUtils.TargetSearchFlag.All,null,NPCUtils.SearchFilters.NonBeeNPCs);
                Require(search.FoundTank && (search.NearestTankType==NPCUtils.TargetType.TankPet)==(branch==0),"Real guardian query positive/LOS/qualification branch="+branch);
                Aim(context,host,n);cache.Demand(0,1,120);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Native(n);
                Console.WriteLine("BEE PET branch="+branch+" kind="+search.NearestTankType+" count="+path?.Count+" stop="+Get(source,"outcomeStop")+" native="+n.velocity);
                Require(path!=null && path.Count>1 && Math.Abs(path[1].Vx-n.velocity.X)<.01f && Math.Abs(path[1].Vy-n.velocity.Y)<.01f,"Actual Source guardian finite winner uses valid LOS cells and owner motion");
            }
            p.tankPet=-1;p.npcTypeNoAggro[210]=false;Main.projectile[5].active=false;for(int y=40;y<=57;y++)Main.tile[39,y].active(false);
            Console.WriteLine("PASS bee guardian winner/blocked LOS/no-aggro negative Source first actions=3");
        }
        internal static void Tail(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;
            foreach(int branch in new[]{0,1,2,3,4})
            {
                foreach(var npc in Main.npc)npc.active=false;Main.dayTime=branch>=2;Main.remixWorld=branch==3;Main.netMode=branch==4?0:1;NPC.mechQueen=-1;
                var n=Main.npc[2]=new NPC();n.SetDefaults(branch==4?619:139);n.whoAmI=2;n.position=new Vector2(650,960-n.height);n.velocity=new Vector2(1,2);n.oldVelocity=n.velocity;n.target=0;n.direction=1;n.ai[3]=branch==0?1:0;n.ai[2]=-1;n.localAI[0]=branch==4?119:0;n.alpha=0;n.dontTakeDamage=n.immortal=n.friendly=false;
                p.position=new Vector2(850,960-p.height);p.velocity=Vector2.Zero;Aim(context,host,n);cache.Demand(0,1,120);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Native(n);
                Console.WriteLine("FLY TAIL branch="+branch+" type="+n.type+" count="+path?.Count+" stop="+Get(source,"outcomeStop")+" native="+n.velocity+" phase="+n.ai[3]+" local="+n.localAI[0]);
                Require(path!=null && path.Count>1 && Math.Abs(path[1].Vx-n.velocity.X)<.01f && Math.Abs(path[1].Vy-n.velocity.Y)<.01f,"AI5 real detach/free/IsItDay-remix/recoil native first motion branch="+branch);
            }
            Main.dayTime=Main.remixWorld=false;Main.netMode=1;Console.WriteLine("PASS AI5 finite detach/free/world day/recoil Source first actions=5");
        }
        internal static void Mech(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;int cases=0;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,radius:0));
            foreach(int slot in new[]{1,3})foreach(int sign in new[]{-1,1})
            {
                foreach(var npc in Main.npc)npc.active=false;var link=Main.npc[slot]=new NPC();link.SetDefaults(134);link.whoAmI=slot;link.Center=new Vector2(680,800);link.velocity=new Vector2(1,.5f);link.rotation=.4f;
                int queenSlot=slot==1?3:1;var queen=Main.npc[queenSlot]=new NPC();queen.SetDefaults(127);queen.whoAmI=queenSlot;queen.position=new Vector2(700,750);queen.velocity=new Vector2(2,-1);NPC.mechQueen=queenSlot;
                var n=Main.npc[2]=new NPC();n.SetDefaults(139);n.whoAmI=2;n.Center=new Vector2(650,800);n.velocity=new Vector2(1,2);n.target=0;n.ai[3]=sign;n.ai[2]=-1;n.dontTakeDamage=n.immortal=n.friendly=false;p.position=new Vector2(850,960-p.height);
                Aim(context,host,n);cache.Demand(0,1,120);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>12 && (int)Get(source,"observedCount")==1 && (path.Assumptions&PredictionAssumption.CurrentConnection)!=0,"139 observes only actual queen/link scalars, not full owner AI chain");
                for(int step=1;step<=12;step++){if(slot<2)link.position+=link.velocity;Native(n);if(slot>2)link.position+=link.velocity;Require(Math.Abs(path[step].Bounds.X-n.position.X)<.02f && Math.Abs(path[step].Bounds.Y-n.position.Y)<.02f && !path[step].CanReceive && n.dontTakeDamage && n.ai[2]==slot,"139 observed link pose/rotation, queen velocity and receiving identity at actual slot step="+step);}
                Console.WriteLine("PASS139 connection bounded12 linkSlot="+slot+" sign="+sign+" count="+path.Count);cases++;
            }
            foreach(var npc in Main.npc)npc.active=false;var q=Main.npc[1]=new NPC();q.SetDefaults(127);q.whoAmI=1;q.velocity=new Vector2(1,0);NPC.mechQueen=1;var a=Main.npc[3]=new NPC();a.SetDefaults(134);a.whoAmI=3;a.Center=new Vector2(680,800);a.rotation=.2f;
            var d=Main.npc[2]=new NPC();d.SetDefaults(139);d.whoAmI=2;d.Center=new Vector2(650,800);d.velocity=new Vector2(1,2);d.target=0;d.ai[3]=1;d.ai[2]=3;d.dontTakeDamage=d.immortal=d.friendly=false;Aim(context,host,d);NativeCombatObservationChecks.Fresh(context,host);var initial=cache.Read(0);var id=initial.Identity;long tick=initial.CaptureTick;
            a.rotation+=.2f;Call(source,"Prepare",id,tick);var changed=cache.Read(0);Require(changed!=null && !ReferenceEquals(initial,changed) && changed[0].Bounds.X==initial[0].Bounds.X && changed[1].Bounds.Y!=initial[1].Bounds.Y,"139 same-tick link rotation revises pure captured pose");
            q.velocity.X+=1;Call(source,"Prepare",id,tick);var velocity=cache.Read(0);Require(velocity!=null && !ReferenceEquals(changed,velocity) && velocity[1].Vx!=changed[1].Vx,"139 same-tick queen velocity revises path");
            var replacement=Main.npc[3]=new NPC();replacement.SetDefaults(134);replacement.whoAmI=3;replacement.Center=a.Center;replacement.rotation=a.rotation;Call(source,"ObserveNpcReset",replacement);Require(cache.Read(0)==null,"139 same-slot link generation retires publication");Call(source,"Prepare",id,tick);Require(cache.Read(0)!=null && !ReferenceEquals(velocity,cache.Read(0)),"139 replacement is sampled as a new full dependency identity");
            replacement.active=false;Call(source,"Prepare",id,tick);var detached=cache.Read(0);Native(d);Require(detached!=null && detached.Count>1 && Math.Abs(detached[1].Vx-d.velocity.X)<.01f && Math.Abs(detached[1].Vy-d.velocity.Y)<.01f && d.ai[3]==0 && detached[1].CanReceive,"Known inactive link detaches without same-action Axis or missing-owner rejection");cases++;
            // A free mech with ai2==0 is constrained outside the known 120px
            // player circle; this is not an attachment to an owner instance.
            d.dontTakeDamage=false;d.ai[3]=0;d.ai[2]=0;d.Center=p.Center-new Vector2(50,0);d.velocity=Vector2.Zero;Aim(context,host,d);NativeCombatObservationChecks.Fresh(context,host);var circle=cache.Read(0);Native(d);Require(circle!=null && circle.Count>1 && Math.Abs(circle[1].Bounds.X-d.position.X)<.02f && Math.Abs(circle[1].Bounds.Y-d.position.Y)<.02f,"139 free MechQueen ai2 zero actual120 circle");cases++;
            NPC.mechQueen=-1;d.ai[2]=-1;d.ai[3]=0;d.position=new Vector2(650,960-d.height);d.velocity=new Vector2(4,0);Main.zenithWorld=false;Aim(context,host,d);NativeCombatObservationChecks.Fresh(context,host);var normal=cache.Read(0);Main.zenithWorld=true;Call(source,"Prepare",normal.Identity,normal.CaptureTick);var zenith=cache.Read(0);Native(d);Require(zenith!=null && !ReferenceEquals(normal,zenith) && zenith[0].Bounds.X==normal[0].Bounds.X && Math.Abs(zenith[1].Vx-d.velocity.X)<.01f && zenith[1].Vx<normal[1].Vx,"Actual Zenith speed3 same-tick environment revision preserves point0");cases++;
            Main.zenithWorld=false;NPC.mechQueen=-1;Console.WriteLine("PASS139 finite attach/detach/circle/Zenith cases="+cases+"; later owner pose remains explicit observed condition");
        }
        internal static void Recoil(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;
            for(int branch=0;branch<6;branch++)
            {
                foreach(var npc in Main.npc)npc.active=false;Main.netMode=branch==2?1:0;Main.dayTime=branch==4;Main.remixWorld=branch==4;
                var n=Main.npc[2]=new NPC();n.SetDefaults(619);n.whoAmI=2;n.position=new Vector2(650,960-n.height);n.velocity=new Vector2(1,0);n.target=0;n.alpha=0;n.localAI[0]=branch==1?109:119;n.justHit=branch==1;n.dontTakeDamage=n.immortal=n.friendly=false;p.position=new Vector2(branch==5?1150:850,960-p.height);
                for(int y=40;y<60;y++){Main.tile[48,y].active(branch==3);Main.tile[48,y].type=1;}
                Aim(context,host,n);cache.Demand(0,1,120);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Native(n);
                bool fires=branch!=2 && branch!=3 && branch!=5;Require((n.localAI[0]==0)==fires,"Native619 deterministic clock/JustHit/MP/blocked/far shooting gate");
                Require(path!=null && path.Count>1 && Math.Abs(path[1].Vx-n.velocity.X)<.01f && Math.Abs(path[1].Vy-n.velocity.Y)<.01f,"619 Source deterministic own recoil before projectile RNG branch="+branch);
                if(branch==0)
                {
                    float max=0;for(int step=2;step<=120;step++){Native(n);max=Math.Max(max,Vector2.Distance(n.position,new Vector2(path[step].Bounds.X,path[step].Bounds.Y)));}
                    Require(max<.02f,"619 frozen120 actually predicts own recoil and subsequent finite return motor error="+max);Console.WriteLine("PASS619 frozen120 actual body recoil/return max="+max);
                }
                Console.WriteLine("RECOIL branch="+branch+" actual="+n.velocity+" local="+n.localAI[0]+" future="+(path.Count-1));
            }
            for(int y=40;y<60;y++)Main.tile[48,y].active(false);Main.dayTime=Main.remixWorld=false;Main.netMode=1;Console.WriteLine("PASS619 recoil/JustHit/MP/LOS/far/raw-day-remix Source first actions=6");
        }
        internal static void DeadTail(object context)
        {
            var source=Get(Get(context,"CombatObservation"),"Prediction");var p=Main.LocalPlayer;var terrain=(IPredictionTerrain)Get(source,"Terrain");var read=source.GetType().GetMethod("Read",Flags);var capture=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation").GetMethod("Capture",Flags);var kernel=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcFlyingMotion").GetMethod("Step",Flags);
            foreach(bool day in new[]{false,true})
            {
                foreach(var npc in Main.npc)npc.active=false;p.dead=true;Main.dayTime=day;Main.remixWorld=true;Main.netMode=0;var n=Main.npc[2]=new NPC();n.SetDefaults(619);n.whoAmI=2;n.position=new Vector2(650,700);n.velocity=new Vector2(1,2);n.target=0;n.direction=1;n.alpha=0;n.localAI[0]=119;
                terrain.Reset();var state=(NpcMotionState)read.Invoke(null,new object[]{n,1L});object[] observation={n,state,terrain};capture.Invoke(null,observation);state=(NpcMotionState)observation[1];var env=new PredictionEnvironment{PlayerIndex=0,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerDead=true,Day=day,Remix=true};object[] action={state,env,terrain,1,PredictionStop.None,false};Require((bool)kernel.Invoke(null,action),"619 dead finite common motion available");state=(NpcMotionState)action[0];n.oldTarget=n.target;n.AI();
                Console.WriteLine("DEAD619 day="+day+" actual="+n.velocity+" model="+state.Vx+","+state.Vy+" time="+n.timeLeft+" target="+n.target+" kind="+n.GetTargetData().Type);
                Require(Math.Abs(state.Vx-n.velocity.X)<.0001f && Math.Abs(state.Vy-n.velocity.Y)<.0001f && state.TimeLeft==10 && n.timeLeft==10 && state.L0==119 && n.localAI[0]==119,"619 dead common10 tail overrides initial raw-day60, shooting clock stays untouched");
            }
            p.dead=false;Main.dayTime=Main.remixWorld=false;Main.netMode=1;Console.WriteLine("PASS619 dead common tail2 pure captured first-action native AI; not Source alive-player acceptance");
        }
    }
}
