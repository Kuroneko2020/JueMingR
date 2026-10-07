using System;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatPositionControlChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static void Aim(object context,object host,NPC n)
        {typeof(NativeCombatPositionRelationChecks).GetMethod("Aim",Flags).Invoke(null,new object[]{context,host,n});}
        private static void Native(NPC n)
        {typeof(NativeCombatFighterControlChecks).GetMethod("Native",Flags).Invoke(null,new object[]{n});}
        internal static void ParentDeparting(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;var old=Main.player[1];int hook=p.grappling[0],hooks=p.grapCount,cases=0;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,radius:0));
            try
            {
                foreach(int slot in new[]{1,3})foreach(bool departing in new[]{true,false})foreach(bool reliable in new[]{true,false})
                {
                    foreach(var npc in Main.npc)npc.active=false;old.active=true;old.dead=true;old.ghost=false;
                    var owner=Main.npc[slot]=new NPC();owner.SetDefaults(35);owner.whoAmI=slot;owner.position=new Vector2(800,800);owner.velocity=new Vector2(0,departing?-3:3);owner.noGravity=owner.noTileCollide=true;owner.ai[1]=0;owner.target=0;
                    var n=Main.npc[2]=new NPC();n.SetDefaults(36);n.whoAmI=2;n.target=1;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(600,800-(departing?201:199));n.velocity=Vector2.Zero;n.noGravity=n.noTileCollide=true;n.ai[0]=1;n.ai[1]=slot;n.ai[2]=1;n.ai[3]=20;
                    p.grappling[0]=reliable?hook:Main.maxProjectiles+10;p.grapCount=reliable?hooks:1;Aim(context,host,n);cache.Demand(0,1,120);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,n),"Departing actual selected child");
                    if(slot<2)owner.position+=owner.velocity;Native(n);if(slot>2)owner.position+=owner.velocity;bool aim=n.ai[2]==2;
                    Require(aim==(departing?slot>2:slot<2),"Native actor order makes the actual departing/entering Aim gate");
                    Console.WriteLine("PARENT DEPART slot="+slot+" departing="+departing+" reliable="+reliable+" nativeAim="+aim+" target="+n.target+" prepared="+Get(source,"targetPlayer")+" count="+path?.Count+" stop="+Get(source,"outcomeStop"));
                    if(aim && !reliable)Require(path==null && (PredictionStop)Get(source,"outcomeStop")==PredictionStop.PhaseBoundary,"Actual Aim still rejects unavailable real new-player future");
                    else Require(path!=null && path.Count>1 && Math.Abs(path[1].Bounds.X-n.position.X)<.02f && Math.Abs(path[1].Bounds.Y-n.position.Y)<.02f && (!aim || (int)Get(source,"targetPlayer")==0),"Old dead target cannot erase known independent motion or real new-winner Aim");
                    cases++;
                }
            }
            finally{old.active=false;old.dead=false;p.grappling[0]=hook;p.grapCount=hooks;}
            Console.WriteLine("PASS parent departing/entering actor-time old-dead/new-winner Source cases="+cases);
        }
        internal static void ParentRecovery(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;var old=Main.player[1];int hook=p.grappling[0],hooks=p.grapCount,cases=0;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,radius:0));
            try
            {
                foreach(int type in new[]{129,130})foreach(float distance in new[]{350f,400f})foreach(bool reliable in new[]{true,false})foreach(bool resting in new[]{false,true})
                {
                    if(resting && distance==400)continue;foreach(var npc in Main.npc)npc.active=false;old.active=true;old.dead=true;var owner=Main.npc[1]=new NPC();owner.SetDefaults(127);owner.whoAmI=1;owner.position=new Vector2(800,800);owner.velocity=Vector2.Zero;owner.noGravity=owner.noTileCollide=true;owner.ai[1]=resting?0:1;owner.target=0;
                    var n=Main.npc[2]=new NPC();n.SetDefaults(type);n.whoAmI=2;n.target=1;n.dontTakeDamage=n.immortal=n.friendly=false;n.Center=new Vector2(owner.Center.X-200,owner.position.Y+230-distance);n.velocity=Vector2.Zero;n.noGravity=n.noTileCollide=true;n.ai[0]=1;n.ai[1]=1;n.ai[2]=99;n.ai[3]=20;
                    p.grappling[0]=reliable?hook:Main.maxProjectiles+10;p.grapCount=reliable?hooks:1;Aim(context,host,n);cache.Demand(0,1,120);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Native(n);bool chase=n.ai[2]==0;
                    Require(chase==(distance<400),"Actual99 recovery strict400 threshold");Console.WriteLine("PARENT RECOVERY type="+type+" distance="+distance+" reliable="+reliable+" resting="+resting+" target="+n.target+" prepared="+Get(source,"targetPlayer")+" count="+path?.Count+" stop="+Get(source,"outcomeStop"));
                    if(chase && !resting && !reliable)Require(path==null && (PredictionStop)Get(source,"outcomeStop")==PredictionStop.PhaseBoundary,"Actual recovered-home chase still needs reliable winner future");
                    else Require(path!=null && path.Count>1 && Math.Abs(path[1].Bounds.X-n.position.X)<.02f && Math.Abs(path[1].Bounds.Y-n.position.Y)<.02f && (!chase || resting || (int)Get(source,"targetPlayer")==0),"99 recovering query prepares actual new winner, far recovery keeps independent prefix");cases++;
                }
            }
            finally{old.active=false;old.dead=false;p.grappling[0]=hook;p.grapCount=hooks;}
            Console.WriteLine("PASS parent99 recovered home query/strict400 independent Source cases="+cases);
        }
        internal static void ParentBoundary(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;int hook=p.grappling[0],hookCount=p.grapCount,cases=0;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,radius:0));
            try
            {
                foreach(int type in new[]{36,130,129})foreach(bool horizontal in new[]{false,true})foreach(int ownerSlot in new[]{1,3})foreach(bool reliable in new[]{false,true})
                {
                    if(horizontal && type==129)continue;foreach(var npc in Main.npc)npc.active=false;
                    var owner=Main.npc[ownerSlot]=new NPC();owner.SetDefaults(type==36?35:127);owner.whoAmI=ownerSlot;owner.position=new Vector2(800,800);owner.velocity=horizontal?new Vector2(-3,0):new Vector2(0,3);owner.noGravity=owner.noTileCollide=true;owner.ai[1]=0;owner.target=p.whoAmI;
                    var n=Main.npc[2]=new NPC();n.SetDefaults(type);n.whoAmI=2;n.dontTakeDamage=n.immortal=n.friendly=false;n.target=p.whoAmI;n.ai[0]=1;n.ai[1]=ownerSlot;n.ai[2]=horizontal?4:1;n.ai[3]=20;n.noGravity=n.noTileCollide=true;n.velocity=Vector2.Zero;
                    Require(n.aiStyle==(type==36?12:type==129?33:34),"Actual parent member dispatch supports this raising boundary");
                    n.position=new Vector2(owner.Center.X+(horizontal?499:-200)-n.width*.5f,horizontal?owner.position.Y+200:owner.position.Y-(type==130?279:199));
                    p.grappling[0]=reliable?hook:Main.maxProjectiles+10;p.grapCount=reliable?hookCount:1;Aim(context,host,n);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,n),"Parent boundary actual receiver is the member, not its nearby owner");
                    if(ownerSlot<2)owner.position+=owner.velocity;Native(n);if(ownerSlot>2)owner.position+=owner.velocity;
                    bool entered=n.ai[2]==(horizontal?5:2);Require(entered==(ownerSlot<2),"Actual native parent actor slot crosses Aim threshold type="+type+" horizontal="+horizontal);
                    Console.WriteLine("PARENT GATE type="+type+" horizontal="+horizontal+" slot="+ownerSlot+" trusted="+reliable+" count="+path?.Count+" stop="+Get(source,"outcomeStop")+" layer="+Get(source,"outcomeLayer")+" targetPlayer="+Get(source,"targetPlayer"));
                    if(reliable)Require(path!=null && path.Count>1 && (path.Assumptions&PredictionAssumption.HeldPlayerControls)!=0 && Math.Abs(n.position.X-path[1].Bounds.X)<.02f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.02f && path[0].Bounds.Y==(horizontal?800+200:800-(type==130?279:199)),"Actor-time first Aim restarts trusted player from original point0 type="+type+" slot="+ownerSlot);
                    else if(ownerSlot<2)Require(path==null && (PredictionStop)Get(source,"outcomeStop")==PredictionStop.PhaseBoundary,"Unknown necessary player stops before same-action Aim");
                    else Require(path!=null && path.Count==2 && path.Stop==PredictionStop.PhaseBoundary && Math.Abs(n.position.X-path[1].Bounds.X)<.02f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.02f && (path.Assumptions&PredictionAssumption.NoPlayerMotionNeeded)!=0,"Known raising/horizontal first action survives until real Aim boundary");
                    Console.WriteLine("PARENT ACTOR type="+type+" horizontal="+horizontal+" ownerSlot="+ownerSlot+" trusted="+reliable+" nativePhase="+n.ai[2]+" future="+(path?.Count-1)+" stop="+Get(source,"outcomeStop"));cases++;
                }
            }
            finally{p.grappling[0]=hook;p.grapCount=hookCount;}
            Console.WriteLine("PASS relation6 actor-time raising/horizontal→Aim Source positive/negative same-point0 cases="+cases+"; owner motion remains observed condition");
        }
        internal static void MovingFollow(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;var saved=p.position;var velocity=p.velocity;bool left=p.controlLeft,right=p.controlRight;
            try
            {
                for(int branch=0;branch<4;branch++)
                {
                    bool movingAway=branch==0 || branch==3;
                    foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2]=new NPC();n.SetDefaults(111);n.whoAmI=2;n.position=new Vector2(650,960-n.height);n.velocity=Vector2.UnitX;n.target=p.whoAmI;n.direction=1;n.ai[0]=1000;n.ai[1]=20;n.ai[3]=-2;
                    var owner=Main.npc[1]=new NPC();owner.SetDefaults(branch>=2?1:25);owner.whoAmI=1;owner.Center=n.Center+new Vector2(branch>=2?600:100,-60);owner.velocity=Vector2.Zero;owner.friendly=true;owner.noGravity=owner.noTileCollide=true;owner.target=p.whoAmI;owner.ai[0]=20;if(branch==1)owner.active=false;
                    var other=Main.player[1];int hook=other.grappling[0],hookCount=other.grapCount;if(branch>=2){other.active=true;other.dead=false;other.Center=owner.Center;other.tankPet=-1;other.grappling[0]=Main.maxProjectiles+10;other.grapCount=1;owner.target=1;}
                    p.Center=n.Center+new Vector2(movingAway?199:201,0);p.velocity=new Vector2(movingAway?3:-3,0);p.controlRight=movingAway;p.controlLeft=!movingAway;p.tankPet=-1;
                    Aim(context,host,n);cache.Demand(0,1,120);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Console.WriteLine("AI111 MOVING branch="+branch+" nearBefore="+movingAway+" count="+path?.Count+" stop="+path?.Stop+" sourceStop="+Get(source,"outcomeStop")+" preparedPlayer="+Get(source,"targetPlayer"));
                    if(branch==3){Require(path==null || path.Count<=1,"Still-follow necessary owner with unavailable different-player future stays D");other.active=false;other.grappling[0]=hook;other.grapCount=hookCount;continue;}
                    p.HorizontalMovement();p.velocity=Collision.TileCollision(p.position,p.velocity,p.width,p.height);p.position+=p.velocity;
                    Require((p.Distance(n.Center)<200)!=movingAway,"AI111 real held first player move crosses release boundary");if(movingAway && owner.active)Native(owner);Native(n);
                    Require(path!=null && path.Count>1 && (movingAway?n.ai[3]<0:n.ai[3]==0) && Math.Abs(n.position.X-path[1].Bounds.X)<.01f && Math.Abs(n.velocity.X-path[1].Vx)<.01f,"AI111 actual first moving release/follow preserves only necessary owner movingAway="+movingAway);
                    if(branch>=2){other.active=false;other.grappling[0]=hook;other.grapCount=hookCount;}
                }
                Console.WriteLine("PASS AI111 held player crosses near boundary before actual owner read; released missing owner remains irrelevant");
            }
            finally{p.position=saved;p.velocity=velocity;p.controlLeft=left;p.controlRight=right;}
        }
        internal static void Trend(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");int cases=0;
            // Static reachability: every false Independent horizontal member
            // is now intercepted by the finite shooter clock. The only other
            // false branches are the three Entry actors and Waiting family.
            var horizontal=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.FighterHorizontalMotion");var independent=horizontal.GetMethod("Independent",Flags);var shooter=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcFighterClockMotion").GetMethod("Shooter",Flags);
            for(int type=0;type<Terraria.ID.NPCID.Count;type++)if((bool)independent.Invoke(null,new object[]{type}))Require((bool)shooter.Invoke(null,new object[]{type}),"Current horizontal Independent false is intercepted by finite Clock type="+type);
            foreach(int side in new[]{-1,1})foreach(int ownerSlot in new[]{1,3})
            {
                foreach(var npc in Main.npc)npc.active=false;var owner=Main.npc[ownerSlot]=new NPC();owner.SetDefaults(398);owner.whoAmI=ownerSlot;owner.position=new Vector2(900,400);owner.velocity=new Vector2(.5f,0);owner.noGravity=owner.noTileCollide=true;
                var n=Main.npc[2]=new NPC();n.SetDefaults(397);n.whoAmI=2;n.dontTakeDamage=n.immortal=n.friendly=false;n.Center=owner.Center+new Vector2(side*500,-100);n.velocity=new Vector2(side*4,0);n.target=Main.myPlayer;n.ai[3]=ownerSlot;n.ai[2]=side<0?0:1;n.wet=true;n.wetCount=1;n.noTileCollide=false;n.timeLeft=750;Aim(context,host,n);NativeCombatObservationChecks.Fresh(context,host);var wet=cache.Read(0);
                Require(wet!=null && wet.Count>13 && ReferenceEquals(wet.Identity.Token,n),"Current TR01 real relation4 Source publishes a future");
                var rolling=Get(source,"rolling");var work=(NpcMotionState[])Get(rolling,"work");bool routed=false;foreach(var state in work)if(state.Identity.Equals(wet.Identity))routed=state.PositionRelation==4 && state.TrendInitialized && state.TrendBaseVx==side*2;
                Require(routed && wet[1].Vx==side*2 && wet[2].Vx==side*2 && wet[13].Vx==side*2,"TR01 actual nested Trend initialized and no-contact dry physics owns future baseline side="+side+" slot="+ownerSlot);
                Require((wet.Assumptions&(PredictionAssumption.CurrentConnection|PredictionAssumption.ApproximateMechanism))==(PredictionAssumption.CurrentConnection|PredictionAssumption.ApproximateMechanism),"Real constraint does not turn the observed hand/Boss velocity into complete AI");
                Call(source,"Prepare",wet.Identity,wet.CaptureTick);Require(ReferenceEquals(wet,cache.Read(0)),"Current nested Trend repeated identical sample reuses immutable path");
                n.wet=false;n.velocity=new Vector2(side*4,0);NativeCombatObservationChecks.Fresh(context,host);var dry=cache.Read(0);Require(dry!=null && dry[13].Vx==side*4,"Current relation4 unchanged dry positive");
                n.position+=n.velocity;n.velocity=new Vector2(side*3.8f,0);NativeQuickItemChecks.BeginWorldStep();Call(Get(context,"nativeNpcs"),"BeginTick");cache.Demand(0,1,6);Call(source,"Prepare",dry.Identity,(long)Main.GameUpdateCount);var shortPath=cache.Read(0);
                // Short demand and its growth are solves of one frozen sample,
                // not new observations. Every common point must be identical.
                Require(shortPath!=null && shortPath.Count==7,"Nested Trend real short consumer demand");
                cache.Demand(0,1,120);Call(source,"Prepare",shortPath.Identity,shortPath.CaptureTick);var grown=cache.Read(0);Require(grown!=null && grown.Count==121 && grown.CaptureTick==shortPath.CaptureTick,"Nested Trend demand growth preserves frozen capture tick");
                Require(Math.Abs(grown[13].Vx-side*3.8f)<.00001f && Math.Abs(grown[3].Vx)<3.8f,"Current relation4 recent correction retires without permanent acceleration");
                for(int point=0;point<shortPath.Count;point++)Require(shortPath[point].Bounds.X==grown[point].Bounds.X && shortPath[point].Bounds.Y==grown[point].Bounds.Y && shortPath[point].Vx==grown[point].Vx,"Nested Trend demand growth does not re-accumulate transient point="+point);
                for(int repeat=0;repeat<3;repeat++){Call(source,"Prepare",grown.Identity,grown.CaptureTick);Require(ReferenceEquals(grown,cache.Read(0)),"Nested Trend repeated growth sample never advances itself");}
                n.position+=n.velocity;n.velocity=new Vector2(side*3.6f,0);n.wet=true;n.wetCount=1;NativeCombatObservationChecks.Fresh(context,host);var takeover=cache.Read(0);
                Require(takeover!=null && Math.Abs(takeover[1].Vx-side*1.7f)<.00001f && takeover[1].Vx==takeover[2].Vx && takeover[2].Vx==takeover[13].Vx,"Liquid takes over a recent Trend correction exactly once, retaining actual physical result");
                work=(NpcMotionState[])Get(rolling,"work");foreach(var state in work)if(state.Identity.Equals(takeover.Identity))Require(state.ObservedAccelerationX==0 && state.ObservedTurn==0,"Physical takeover retires old transient rather than accumulating it");grown=takeover;
                owner.position.X+=10;Call(source,"Prepare",grown.Identity,grown.CaptureTick);var revised=cache.Read(0);Require(revised!=null && !ReferenceEquals(grown,revised) && revised.CaptureTick==grown.CaptureTick && revised[0].Bounds.X==grown[0].Bounds.X,"Same-tick owner revision invalidates path without moving point zero");
                var replacement=new NPC();replacement.SetDefaults(398);replacement.whoAmI=ownerSlot;replacement.position=owner.position;replacement.velocity=owner.velocity;replacement.noGravity=replacement.noTileCollide=true;Main.npc[ownerSlot]=owner=replacement;Call(source,"Prepare",revised.Identity,revised.CaptureTick);var replaced=cache.Read(0);Require(replaced!=null && !ReferenceEquals(replaced,revised),"Relation4 same-slot new owner token cannot reuse prior trajectory");owner.life=0;Call(source,"Prepare",replaced.Identity,replaced.CaptureTick);Require(cache.Read(0)==null,"Current relation4 dead necessary owner D");owner.life=owner.lifeMax;
                owner.active=false;Call(source,"Prepare",revised.Identity,revised.CaptureTick);Require(cache.Read(0)==null,"Current relation4 inactive necessary owner D");cases++;
            }
            Console.WriteLine("PASS current TR01 real relation4 nested Trend wet/dry, transient retirement, demand growth/repetition and same-tick owner revision cases="+cases);
        }
        internal static void Follow(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;var saved=p.position;int cases=0;
            foreach(int side in new[]{-1,1})
            {
                foreach(var npc in Main.npc)npc.active=false;var owner=Main.npc[1]=new NPC();owner.SetDefaults(25);owner.whoAmI=1;owner.friendly=true;owner.position=new Vector2(650+side*100,900);owner.velocity=new Vector2(.5f,0);owner.noGravity=owner.noTileCollide=true;owner.target=p.whoAmI;owner.ai[0]=20;
                var n=Main.npc[2]=new NPC();n.SetDefaults(111);n.whoAmI=2;n.position=new Vector2(650,960-n.height);n.velocity=Vector2.UnitX;n.target=p.whoAmI;n.direction=1;n.ai[0]=999;n.ai[1]=20;n.ai[3]=-2;Aim(context,host,n);cache.Demand(0,1,12);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>3 && ReferenceEquals(path.Identity.Token,n),"AI111 actual Source follow before-action identity");
                for(int step=1;step<=3;step++){Native(owner);Native(n);Require(Math.Abs(n.position.X-path[step].Bounds.X)<.01f && Math.Abs(n.position.Y-path[step].Bounds.Y)<.01f && Math.Abs(n.velocity.X-path[step].Vx)<.01f,"AI111 observed owner direction/decay before common motor side="+side+" step="+step+" native="+n.position+" V="+n.velocity+" model="+path[step].Bounds.X+","+path[step].Bounds.Y+" V="+path[step].Vx);}
                cases++;
            }
            foreach(int ownerSlot in new[]{1,3})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2]=new NPC();n.SetDefaults(111);n.whoAmI=2;n.position=new Vector2(650,960-n.height);n.velocity=Vector2.UnitX;n.target=p.whoAmI;n.direction=1;n.ai[0]=1299;n.ai[1]=20;n.ai[3]=-ownerSlot-1;
                var owner=Main.npc[ownerSlot]=new NPC();owner.SetDefaults(25);owner.whoAmI=ownerSlot;owner.friendly=true;owner.Center=n.Center+new Vector2(-1,-60);owner.velocity=new Vector2(3,0);owner.noGravity=owner.noTileCollide=true;owner.target=p.whoAmI;owner.ai[0]=20;
                Aim(context,host,n);cache.Demand(0,1,12);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1 && ReferenceEquals(path.Identity.Token,n),"AI111 owner crossing before-action Source");
                if(ownerSlot<2)Native(owner);Native(n);if(ownerSlot>2)Native(owner);
                Require(n.ai[0]==1000 && n.direction==(ownerSlot<2?1:-1) && Math.Abs(n.velocity.X-path[1].Vx)<.01f && Math.Abs(n.position.X-path[1].Bounds.X)<.01f,"AI111 actual owner crossing uses actor-slot geometry, counter1300 wraps ownerSlot="+ownerSlot+" nativeVx="+n.velocity.X+" modelVx="+path[1].Vx);
                Console.WriteLine("AI111 CROSS ownerSlot="+ownerSlot+" direction="+n.direction+" vx="+n.velocity.X+" clock="+n.ai[0]);cases++;
                var replacement=new NPC();replacement.SetDefaults(25);replacement.whoAmI=ownerSlot;replacement.position=owner.position;replacement.velocity=owner.velocity;replacement.friendly=true;replacement.noGravity=replacement.noTileCollide=true;replacement.target=p.whoAmI;replacement.ai[0]=20;Main.npc[ownerSlot]=replacement;
                NativeCombatObservationChecks.Fresh(context,host);var revised=cache.Read(0);Require(revised==null || !ReferenceEquals(revised,path),"AI111 same-slot owner replacement cannot reuse old frozen trajectory");
            }
            foreach(bool guardian in new[]{false,true})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2]=new NPC();n.SetDefaults(111);n.whoAmI=2;n.position=new Vector2(650,960-n.height);n.velocity=Vector2.UnitX;n.target=p.whoAmI;n.direction=1;n.ai[0]=1000;n.ai[1]=20;n.ai[3]=-2;
                var owner=Main.npc[1]=new NPC();owner.SetDefaults(25);owner.whoAmI=1;owner.friendly=true;owner.Center=n.Center+new Vector2(100,-60);owner.noGravity=owner.noTileCollide=true;owner.target=p.whoAmI;owner.ai[0]=20;
                p.position=new Vector2(1100,960-p.height);p.tankPet=-1;
                if(guardian){var pet=Main.projectile[0];pet.SetDefaults(625);pet.whoAmI=0;pet.owner=p.whoAmI;pet.active=true;pet.width=pet.height=40;pet.Center=n.Center-new Vector2(90,0);p.tankPet=0;}
                else{n.confused=true;n.buffType[0]=31;n.buffTime[0]=200;}
                Aim(context,host,n);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1,"AI111 first query facing actual Source guardian="+guardian);Native(n);
                Console.WriteLine("AI111 FIRST FACE guardian="+guardian+" nativeVx="+n.velocity.X+" modelVx="+path[1].Vx);
                Require(n.velocity.X==0 && Math.Abs(path[1].Vx-n.velocity.X)<.01f,"AI111 TargetClosest(false) guardian/confused precedes owner sign and executes once guardian="+guardian);cases++;p.tankPet=-1;Main.projectile[0].active=false;
            }
            foreach(bool hit in new[]{true,false})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2]=new NPC();n.SetDefaults(111);n.whoAmI=2;n.position=new Vector2(650,960-n.height);n.target=p.whoAmI;n.direction=1;n.ai[1]=20;n.ai[3]=-200;n.justHit=hit;p.position=hit?saved:new Vector2(730,960-p.height);Aim(context,host,n);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1,"AI111 hit/near release does not require former missing owner hit="+hit);int prepared=(int)Get(source,"targetPlayer");Native(n);Require(n.ai[3]==0 && prepared==n.target && Math.Abs(n.position.X-path[1].Bounds.X)<.01f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.01f,"AI111 release before owner and actual common first action hit="+hit);cases++;
            }
            p.position=saved;foreach(var npc in Main.npc)npc.active=false;var missing=Main.npc[2]=new NPC();missing.SetDefaults(111);missing.whoAmI=2;missing.position=new Vector2(650,960-missing.height);missing.target=p.whoAmI;missing.direction=1;missing.ai[3]=-200;Aim(context,host,missing);NativeCombatObservationChecks.Fresh(context,host);var stopped=cache.Read(0);Require((stopped==null || stopped.Count<=1) && (PredictionStop)Get(source,"outcomeStop")==PredictionStop.MissingDependency,"AI111 still-follow missing necessary owner refuses motor substitute");Console.WriteLine("PASS AI111 observed owner before/after-slot crossing/counter, direction/decay and hit/near release cases="+cases+"; missing owner D");
        }
    }
}
