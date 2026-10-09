using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatStructuralControlChecks
    {
        internal static void MissingSupport(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;
            foreach(int absent in new[]{1,2})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(69);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,936);n.velocity=Vector2.Zero;n.target=p.whoAmI;n.timeLeft=750;
                int l=(int)n.position.X/16,m=(int)(n.position.X+n.width/2)/16,r=(int)(n.position.X+n.width)/16;var left=Main.tile[l,60];var missing=Main.tile[absent==1?m:r,60];left.active(true);left.type=Terraria.ID.TileID.Stone;Main.tile[absent==1?m:r,60]=null;
                try
                {
                    NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require((path==null || path.Count<=1) && (PredictionStop)Get(source,"outcomeStop")==PredictionStop.TerrainUnavailable && ReferenceEquals(Main.tile[l,60],left) && left.active() && Main.tile[absent==1?m:r,60]==null,"AI19 all three pre-read cells necessary even with left solid absent="+absent);
                    var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;var state=(NpcMotionState)source.GetType().GetMethod("Read",flags).Invoke(null,new object[]{n,Get(host,"Session")});var terrain=(IPredictionTerrain)Get(source,"Terrain");terrain.Reset();var env=new PredictionEnvironment{PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height};object[] args={state,env,terrain,false,PredictionStop.None};bool advanced=(bool)typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcSupportMotion").GetMethod("Step",flags).Invoke(null,args);Require(!advanced && (PredictionStop)args[4]==PredictionStop.TerrainUnavailable,"AI19 production support kernel cannot use solid-left before reading necessary neighbor absent="+absent);
                }
                finally{Main.tile[absent==1?m:r,60]=missing;}
            }
            Console.WriteLine("PASS AI19 solid-left plus missing-middle/right stops D without Tile writes");
        }
        internal static void HungryDefense(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var terrain=(IPredictionTerrain)Get(source,"Terrain");var p=Main.LocalPlayer;int mode=Main.GameMode,net=Main.netMode,wof=Main.wofNPCIndex,top=Main.wofDrawAreaTop,bottom=Main.wofDrawAreaBottom;Main.netMode=0;int cases=0;
            for(int i=0;i<Main.dust.Length;i++)if(Main.dust[i]==null)Main.dust[i]=new Dust();
            for(int x=40;x<45;x++)for(int y=43;y<47;y++){Main.tile[x,y].active(false);Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(1);}
            try
            {
                foreach(bool expert in new[]{false,true})foreach(int life in new[]{3999,4000,5999,6000})foreach(bool free in new[]{false,true})
                {
                    Main.GameMode=expert?1:0;foreach(var npc in Main.npc)npc.active=false;var owner=Main.npc[1];owner.SetDefaults(113);owner.whoAmI=1;owner.active=true;owner.position=new Vector2(400,600);owner.velocity=Vector2.Zero;owner.life=life;owner.lifeMax=8000;owner.target=p.whoAmI;owner.timeLeft=750;Main.wofNPCIndex=1;Main.wofDrawAreaTop=600;Main.wofDrawAreaBottom=900;
                    var n=Main.npc[2];n.SetDefaults(115);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,700);n.velocity=Vector2.Zero;n.target=p.whoAmI;n.direction=1;n.life=60;n.ai[0]=.5f;n.ai[1]=5;n.noTileCollide=free;n.immune[255]=0;n.defense=30;n.timeLeft=750;NativeCombatObservationChecks.Fresh(context,host);
                    if(expert && life==3999 && !free)
                    {
                        var cache=(NpcPredictionCache)Get(source,"Cache");var before=cache.Read(0);Require(before!=null && before.Count>1,"AI29 Expert lava has a future before default-defense revision");int original=n.defDefense;n.defDefense+=2;Call(source,"Prepare",before.Identity,(long)Main.GameUpdateCount);var changed=cache.Read(0);Require(changed!=null && !ReferenceEquals(before,changed),"AI29 same-tick defDefense alone invalidates actual Source forecast");n.defDefense=original;Call(source,"Prepare",before.Identity,(long)Main.GameUpdateCount);
                    }
                    var group=(NpcMotionState[])Get(source,"states");int count=(int)Get(source,"observedCount"),selected=-1;for(int i=0;i<count;i++)if(ReferenceEquals(group[i].Identity.Token,n))selected=i;Require(selected>=0,"AI29 defense check consumes actual Source sampled child");var state=group[selected];var env=new PredictionEnvironment{PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,Expert=expert,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,WorldSurface=(float)Main.worldSurface};terrain.Reset();PredictionStop stop;bool advanced=NpcMotion.Step(ref state,group,count,env,terrain,1,true,out stop);
                    n.oldTarget=n.target;n.AI();if(n.noTileCollide)n.position+=n.velocity;else Call(n,"UpdateCollision");n.justHit=false; // Original UpdateNPC clears it after collision/CheckActive.
                    Require(advanced && state.Health.Defense==n.defense && state.Life==n.life && state.JustHit==n.justHit,"AI29 native defense before actual collision lava vs default free tail expert="+expert+" ownerLife="+life+" free="+free+" nativeDefense="+n.defense+" life="+n.life+" modelDefense="+state.Health.Defense+" life="+state.Life+" stop="+stop);cases++;
                }
            }
            finally{Main.GameMode=mode;Main.netMode=net;Main.wofNPCIndex=wof;Main.wofDrawAreaTop=top;Main.wofDrawAreaBottom=bottom;for(int x=40;x<45;x++)for(int y=43;y<47;y++){Main.tile[x,y].liquid=0;Main.tile[x,y].liquidType(0);}}
            Console.WriteLine("PASS AI29 defense strict life/Expert default/ordinary retained defense with actual lava collision and default free tail cases="+cases);
        }
        internal static void Straight(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;for(int i=0;i<Main.dust.Length;i++)if(Main.dust[i]==null)Main.dust[i]=new Dust();int cases=0;
            foreach(int type in new[]{516,25,30,33,112,665,666})foreach(bool initial in new[]{false,true})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,700);n.velocity=new Vector2(4,-1);n.oldVelocity=n.velocity;n.target=initial?255:p.whoAmI;n.direction=1;n.ai[0]=1;n.timeLeft=750;NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);
                if(type==516 && initial){Require((path==null || path.Count<=1) && (PredictionStop)Get(source,"outcomeStop")==PredictionStop.MissingDependency,"AI516 missing old numbered player is not repaired by later common retarget");continue;}
                Require(path!=null && path.Count>1 && ReferenceEquals(path.Identity.Token,n),"AI9 real Source actual initialized/initial target phase type="+type);Console.WriteLine("AI9 native case type="+type+" initial="+initial);
                n.oldTarget=n.target;n.AI();if(n.noTileCollide)n.position+=n.velocity;else Call(n,"UpdateCollision");Require(Math.Abs(n.position.X-path[1].Bounds.X)<.01f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.01f && Math.Abs(n.velocity.X-path[1].Vx)<.01f && Math.Abs(n.velocity.Y-path[1].Vy)<.01f,"AI9 actual tracking/init/extra displacement type="+type+" initial="+initial+" native="+n.position+" V="+n.velocity+" model="+path[1].Bounds.X+","+path[1].Bounds.Y+" V="+path[1].Vx+","+path[1].Vy);cases++;
            }
            Console.WriteLine("PASS AI9 seven direct members tracking/first target/extra displacement Source first="+cases);
            StraightBoundaries(context);
            StraightFuture(context);
        }
        private static void StraightFuture(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;
            foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(516);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(p.Center.X-220,p.Center.Y-100);n.velocity=new Vector2(4,-1);n.target=p.whoAmI;n.ai[0]=1;n.timeLeft=750;cache.Demand(0,1,120);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>2 && path.Stop==PredictionStop.Despawn,"AI516 frozen pursuit predicts near20 termination ahead");
            for(int step=1;step<path.Count;step++){n.oldTarget=n.target;n.AI();Require(n.active,"AI516 published prefix precedes native termination");n.position+=n.velocity;Require(Math.Abs(n.position.X-path[step].Bounds.X)<.01f && Math.Abs(n.position.Y-path[step].Bounds.Y)<.01f,"AI516 frozen tracking turning prefix step="+step);}
            n.oldTarget=n.target;n.AI();Require(!n.active,"AI516 next native action actually retires at predicted boundary");Console.WriteLine("PASS AI516 frozen pursuit/turn and ahead near20 termination actions="+(path.Count-1));
            bool good=Main.getGoodWorld;Main.getGoodWorld=true;
            foreach(int branch in new[]{0,1,2,3})
            {
                foreach(var npc in Main.npc)npc.active=false;int type=branch==0?25:branch<3?33:666;var boss=Main.npc[1];boss.SetDefaults(type==25?113:35);boss.whoAmI=1;boss.active=branch<3;boss.position=new Vector2(400,600);boss.velocity=Vector2.Zero;boss.timeLeft=750;
                n=Main.npc[2];n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,700);n.velocity=Vector2.Zero;n.target=255;n.ai[0]=2;n.ai[3]=branch==2?1:0;n.timeLeft=750;NativeCombatObservationChecks.Fresh(context,host);path=cache.Read(0);Require(path!=null && path.Count>1,"AI9 world/boss-dependent initialization Source branch="+branch);n.oldTarget=n.target;n.AI();if(n.noTileCollide)n.position+=n.velocity;else Call(n,"UpdateCollision");Require(Math.Abs(n.position.X-path[1].Bounds.X)<.01f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.01f && Math.Abs(n.velocity.X-path[1].Vx)<.01f && Math.Abs(n.velocity.Y-path[1].Vy)<.01f,"AI9 known good-world boss/redhat initialization branch="+branch);
            }
            Main.getGoodWorld=good;Console.WriteLine("PASS AI9 good-world boss/redhat/666 finite initialization four first actions");
        }
        private static void StraightBoundaries(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;int hook=p.grappling[0],count=p.grapCount;p.grappling[0]=0;p.grapCount=0;
            foreach(int type in new[]{25,30,33,112,665,666})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,700);n.velocity=new Vector2(4,-1);n.target=p.whoAmI;n.ai[0]=1;n.timeLeft=750;cache.Demand(0,1,120);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count==121 && (path.Assumptions&PredictionAssumption.NoPlayerMotionNeeded)!=0,"AI9 initialized ordinary straight survives unavailable irrelevant player future type="+type);
                for(int step=1;step<path.Count;step++){n.oldTarget=n.target;n.AI();if(n.noTileCollide)n.position+=n.velocity;else Call(n,"UpdateCollision");Require(Math.Abs(n.position.X-path[step].Bounds.X)<.01f && Math.Abs(n.position.Y-path[step].Bounds.Y)<.01f,"AI9 initialized ordinary/extra position frozen120 type="+type+" step="+step);}
                n.target=255;NativeCombatObservationChecks.Fresh(context,host);path=cache.Read(0);Require(path==null || path.Count<=1,"AI9 real new launch needs unavailable target future type="+type);
            }
            p.grappling[0]=hook;p.grapCount=count;Console.WriteLine("PASS AI9 six initialized independent frozen120 vs true target255 launch dependency");
            foreach(int branch in new[]{0,1,2,3,4})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(branch<3?516:branch==3?112:666);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=branch==1?new Vector2(p.Center.X-n.width/2+10,p.Center.Y-n.height/2):new Vector2(650,700);n.velocity=new Vector2(4,-1);n.oldVelocity=n.velocity;n.target=p.whoAmI;n.direction=1;n.ai[0]=branch==0?0:1;n.collideX=branch==2;n.timeLeft=750;
                if(branch>=3){Main.tile[41,43].active(true);Main.tile[41,43].type=Terraria.ID.TileID.Stone;}
                NativeCombatObservationChecks.Fresh(context,host);Require((PredictionStop)Get(source,"outcomeStop")== (branch==0?PredictionStop.RandomDecision:PredictionStop.Despawn),"AI9 exact unknown birth vs collision/near20/post-extra solid terminal branch="+branch);
                if(branch>0){n.oldTarget=n.target;n.AI();Require(!n.active,"AI9 corresponding native terminal actually happens branch="+branch);}if(branch>=3)Main.tile[41,43].active(false);
            }
            Console.WriteLine("PASS AI516 unknown birth random boundary, near20/old collide and112/666 extra displacement solid terminal");
        }
        internal static void Hungry(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;int wof=Main.wofNPCIndex,top=Main.wofDrawAreaTop,bottom=Main.wofDrawAreaBottom;
            foreach(var npc in Main.npc)npc.active=false;var owner=Main.npc[1];owner.SetDefaults(113);owner.whoAmI=1;owner.active=true;owner.position=new Vector2(400,600);owner.velocity=Vector2.Zero;owner.life=6000;owner.lifeMax=8000;owner.target=p.whoAmI;owner.timeLeft=750;Main.wofNPCIndex=1;Main.wofDrawAreaTop=600;Main.wofDrawAreaBottom=900;
            var n=Main.npc[2];n.SetDefaults(115);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,700);n.velocity=new Vector2(4,0);n.target=p.whoAmI;n.direction=1;n.ai[0]=.5f;n.ai[2]=100;n.timeLeft=750;NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1 && ReferenceEquals(path.Identity.Token,n),"AI29 real Source child future");n.oldTarget=n.target;n.AI();if(n.noTileCollide)n.position+=n.velocity;else Call(n,"UpdateCollision");Require(Math.Abs(n.position.X-path[1].Bounds.X)<.01f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.01f && Math.Abs(n.velocity.X-path[1].Vx)<.01f,"AI29 owner anchor/life/radius actual first native="+n.position+" V="+n.velocity+" model="+path[1].Bounds.X+","+path[1].Bounds.Y+" V="+path[1].Vx+","+path[1].Vy);
            Console.WriteLine("PASS AI29 true owner anchor/radius first");
            int mode=Main.GameMode;int cases=0;
            foreach(bool expert in new[]{false,true})foreach(int life in new[]{8000,6000,5999,4000,3999})foreach(int clock in new[]{99,100,200})
            {
                Main.GameMode=expert?1:0;owner.life=life;n.SetDefaults(115);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,700);n.velocity=new Vector2(10,-2);n.target=p.whoAmI;n.direction=1;n.ai[0]=.5f;n.ai[2]=clock;n.timeLeft=750;NativeCombatObservationChecks.Fresh(context,host);path=cache.Read(0);Require(path!=null && path.Count>1,"AI29 health/clock Source phase");n.oldTarget=n.target;n.AI();n.position+=n.velocity;Require(Math.Abs(n.position.X-path[1].Bounds.X)<.01f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.01f && Math.Abs(n.velocity.X-path[1].Vx)<.01f && Math.Abs(n.velocity.Y-path[1].Vy)<.01f,"AI29 strict life thresholds/integer expert ratio/radius clock first life="+life+" expert="+expert+" clock="+clock+" native="+n.velocity+" model="+path[1].Vx+","+path[1].Vy);cases++;
            }
            Console.WriteLine("PASS AI29 owner life threshold/expert integer ratio/100-200 radius first="+cases);Main.GameMode=mode;
            owner.life=6000;n.SetDefaults(115);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,700);n.velocity=new Vector2(1,.2f);n.target=p.whoAmI;n.direction=1;n.ai[0]=.5f;n.ai[2]=99;n.timeLeft=750;cache.Demand(0,1,120);NativeCombatObservationChecks.Fresh(context,host);path=cache.Read(0);Require(path!=null && path.Count==121,"AI29 stationary observed owner frozen120");
            for(int step=1;step<path.Count;step++){n.oldTarget=n.target;n.AI();n.position+=n.velocity;Require(Math.Abs(n.position.X-path[step].Bounds.X)<.01f && Math.Abs(n.position.Y-path[step].Bounds.Y)<.01f && Math.Abs(n.velocity.X-path[step].Vx)<.01f && Math.Abs(n.velocity.Y-path[step].Vy)<.01f,"AI29 known owner anchor radius expansion/reset frozen step="+step);}
            Console.WriteLine("PASS AI29 fixed observed owner radius expansion/reset frozen120");
            int hook=p.grappling[0],count=p.grapCount;p.grappling[0]=0;p.grapCount=0;n.SetDefaults(115);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,700);n.velocity=Vector2.UnitX;n.target=p.whoAmI;n.ai[0]=.5f;n.justHit=true;n.timeLeft=750;NativeCombatObservationChecks.Fresh(context,host);path=cache.Read(0);Require(path!=null && path.Count==11 && path.Stop==PredictionStop.PhaseBoundary,"AI29 JustHit ten recovery actions retain prefix before real pursuit needs unavailable player");for(int step=1;step<path.Count;step++){n.oldTarget=n.target;n.AI();n.position+=n.velocity;n.justHit=false;Require(Math.Abs(n.position.X-path[step].Bounds.X)<.01f && Math.Abs(n.position.Y-path[step].Bounds.Y)<.01f,"AI29 hit recovery independent exact action="+step);}p.grappling[0]=hook;p.grapCount=count;Console.WriteLine("PASS AI29 ten hit-recovery independent prefix then necessary pursuit PhaseBoundary");
            Main.wofNPCIndex=-1;NativeCombatObservationChecks.Fresh(context,host);Require((PredictionStop)Get(source,"outcomeStop")==PredictionStop.Despawn,"AI29 explicit missing wof index retires");Main.wofNPCIndex=1;owner.active=false;NativeCombatObservationChecks.Fresh(context,host);Require((PredictionStop)Get(source,"outcomeStop")==PredictionStop.MissingDependency,"AI29 stale inactive owner cannot prolong child");owner.active=true;
            Main.wofNPCIndex=wof;Main.wofDrawAreaTop=top;Main.wofDrawAreaBottom=bottom;
        }
        internal static void Support(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;
            for(int i=0;i<Main.dust.Length;i++)if(Main.dust[i]==null)Main.dust[i]=new Dust();
            foreach(int branch in new[]{0,1,2,3})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(69);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,936);n.velocity=Vector2.Zero;n.target=p.whoAmI;n.direction=n.directionY=1;n.timeLeft=750;
                int left=(int)n.position.X/16,mid=(int)(n.position.X+n.width/2)/16,right=(int)(n.position.X+n.width)/16;for(int x=left;x<=right;x++)Main.tile[x,60].active(false);if(branch<3){int col=branch==0?left:branch==1?mid:right;Main.tile[col,60].active(true);Main.tile[col,60].type=Terraria.ID.TileID.Stone;}
                cache.Demand(0,1,120);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1 && ReferenceEquals(path.Identity.Token,n),"AI19 actual Source support phase prefix");
                object[] gravity={0f};n.GetType().GetMethod("UpdateNPC_UpdateGravity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(n,gravity);n.oldTarget=n.target;n.AI();if(!n.noGravity)n.velocity.Y=Math.Min((float)gravity[0],n.velocity.Y+(float)Get(n,"gravity"));if(n.noTileCollide)n.position+=n.velocity;else Call(n,"UpdateCollision");Require(Math.Abs(n.position.Y-path[1].Bounds.Y)<.01f && Math.Abs(n.velocity.Y-path[1].Vy)<.01f,"AI19 any one of three support cells controls gravity/collision native="+n.position+" V="+n.velocity+" modelY="+path[1].Bounds.Y+" Vy="+path[1].Vy+" branch="+branch);
                for(int x=left;x<=right;x++){Main.tile[x,60].active(true);Main.tile[x,60].type=Terraria.ID.TileID.Stone;}
            }
            Console.WriteLine("PASS AI19 left/middle/right support vs air actual Source first");
            int oldHook=p.grappling[0],oldCount=p.grapCount;p.grappling[0]=0;p.grapCount=0;
            foreach(bool horizontal in new[]{false,true})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(69);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,936);n.velocity=horizontal?Vector2.UnitX:Vector2.Zero;n.target=p.whoAmI;n.direction=n.directionY=1;n.timeLeft=750;NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);
                if(horizontal){Require(path==null || path.Count<=1,"AI19 nonzero X reads future upward-facing gate, cannot waive necessary player");continue;}
                Require(path!=null && path.Count==121 && (path.Assumptions&PredictionAssumption.NoPlayerMotionNeeded)!=0,"AI19 support path survives unavailable irrelevant aim future");
                for(int step=1;step<path.Count;step++){object[] gravity={0f};n.GetType().GetMethod("UpdateNPC_UpdateGravity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(n,gravity);n.oldTarget=n.target;n.AI();if(!n.noGravity)n.velocity.Y=Math.Min((float)gravity[0],n.velocity.Y+(float)Get(n,"gravity"));if(n.noTileCollide)n.position+=n.velocity;else Call(n,"UpdateCollision");Require(Math.Abs(n.position.Y-path[step].Bounds.Y)<.01f && Math.Abs(n.velocity.Y-path[step].Vy)<.01f,"AI19 original support release/recontact frozen120 step="+step);}
            }
            p.grappling[0]=oldHook;p.grapCount=oldCount;
            foreach(var npc in Main.npc)npc.active=false;var missing=Main.npc[2];missing.SetDefaults(69);missing.whoAmI=2;missing.active=true;missing.dontTakeDamage=missing.immortal=missing.friendly=false;missing.position=new Vector2(650,936);missing.target=p.whoAmI;missing.timeLeft=750;
            int l=(int)missing.position.X/16,m=(int)(missing.position.X+missing.width/2)/16,r=(int)(missing.position.X+missing.width)/16;for(int x=l;x<=r;x++)Main.tile[x,60].active(false);var saved=Main.tile[m,60];Main.tile[m,60]=null;NativeCombatObservationChecks.Fresh(context,host);var absent=cache.Read(0);Require((absent==null || absent.Count<=1) && (PredictionStop)Get(source,"outcomeStop")==PredictionStop.TerrainUnavailable && Main.tile[m,60]==null,"AI19 unknown necessary foot cell stops without writing terrain");Main.tile[m,60]=saved;for(int x=l;x<=r;x++)Main.tile[x,60].active(true);Console.WriteLine("PASS AI19 support/release/recontact independent frozen120; actual facing X gate needs player; missing support D");
        }
    }
}
