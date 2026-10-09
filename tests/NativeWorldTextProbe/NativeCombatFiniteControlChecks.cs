using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatFiniteControlChecks
    {
        internal static void JellyfishPremise(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;int oldHook=p.grappling[0],oldCount=p.grapCount,oldMode=Main.GameMode;p.grappling[0]=0;p.grapCount=0;Main.GameMode=1;
            foreach(int branch in new[]{0,1,2})
            {
                for(int x=39;x<=45;x++)for(int y=42;y<=47;y++)Main.tile[x,y].liquid=(byte)(branch==0?0:255);
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(63);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,700);n.velocity=new Vector2(2,.2f);n.oldVelocity=n.velocity;n.target=p.whoAmI;n.direction=n.directionY=1;n.wet=branch!=0;n.wetCount=(byte)(n.wet?1:0);n.ai[1]=branch==1?1:0;n.ai[2]=branch==1?119:0;n.timeLeft=750;cache.Demand(0,1,120);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);
                if(branch==0)Require(path!=null && path.Count==121 && (path.Assumptions&PredictionAssumption.NoPlayerMotionNeeded)!=0,"AI18 dry physics survives untrusted player future");
                else if(branch==1){Require(path!=null && path.Count==2 && path.Stop==PredictionStop.PhaseBoundary,"AI18 current frozen gate retains independent action before uncertain pursuit");n.oldTarget=n.target;n.AI();Call(n,"UpdateCollision");Require(Math.Abs(n.position.X-path[1].Bounds.X)<.01f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.01f,"AI18 frozen first original action retained");}
                else Require(path==null || path.Count<=1,"AI18 actual wet pursuit requires unavailable player future");
            }
            for(int x=39;x<=45;x++)for(int y=42;y<=47;y++)Main.tile[x,y].liquid=0;p.grappling[0]=oldHook;p.grapCount=oldCount;Main.GameMode=oldMode;Console.WriteLine("PASS AI18 dry/frozen independent vs actual pursuit necessary player future");
        }
        internal static void MimicAir(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;int oldHook=p.grappling[0],oldCount=p.grapCount;p.grappling[0]=0;p.grapCount=0;
            foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(85);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,960-n.height-30);n.velocity=new Vector2(.3f,-2);n.target=p.whoAmI;n.direction=1;n.ai[0]=1;n.ai[3]=1;n.timeLeft=750;cache.Demand(0,1,120);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1,"AI25 known airborne control retains independent prefix with unavailable player future");
            for(int step=1;step<path.Count;step++){object[] gravity={0f};n.GetType().GetMethod("UpdateNPC_UpdateGravity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(n,gravity);n.oldTarget=n.target;n.AI();if(!n.noGravity)n.velocity.Y=Math.Min((float)gravity[0],n.velocity.Y+(float)Get(n,"gravity"));if(Math.Abs(n.velocity.X)<.005f)n.velocity.X=0;Call(n,"UpdateCollision");Require(Math.Abs(n.position.X-path[step].Bounds.X)<.01f && Math.Abs(n.position.Y-path[step].Bounds.Y)<.01f,"AI25 unavailable future keeps real independent air/wait step="+step);}
            Require(n.velocity.Y==0 && n.ai[2]==11 && path.Stop==PredictionStop.PhaseBoundary,"AI25 stops only at next genuinely target-dependent jump after air and wait prefix, stop="+path.Stop+" clock="+n.ai[2]);p.grappling[0]=oldHook;p.grapCount=oldCount;Console.WriteLine("PASS AI25 independent air/wait prefix actions="+(path.Count-1)+" then necessary jump PhaseBoundary");
        }
        internal static void MimicBirth(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;var saved=p.position;var prior=Main.player[1];var remote=new Player{active=true,whoAmI=1,width=p.width,height=p.height,tankPet=-1,carpetFrame=-1,gravity=.4f,maxFallSpeed=10,maxRunSpeed=3,accRunSpeed=6,runAcceleration=.08f,runSlowdown=.2f};Main.player[1]=remote;
            foreach(bool crossed in new[]{true,false})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(85);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,960-n.height);n.velocity=Vector2.Zero;n.target=p.whoAmI;n.direction=-1;n.ai[0]=1;n.ai[2]=11;n.ai[3]=0;n.timeLeft=750;p.position=new Vector2(n.Center.X-10-p.width/2,960-p.height);remote.position=new Vector2(n.Center.X+(crossed?12:30)-remote.width/2,p.position.Y);int expected=crossed?1:p.whoAmI;
                NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1 && (int)Get(source,"targetPlayer")==expected,"AI25 +8 actual query center owns Source winner crossed="+crossed+" premise="+Get(source,"targetPlayer"));object[] gravity={0f};n.GetType().GetMethod("UpdateNPC_UpdateGravity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(n,gravity);n.oldTarget=n.target;n.AI();if(!n.noGravity)n.velocity.Y=Math.Min((float)gravity[0],n.velocity.Y+(float)Get(n,"gravity"));Call(n,"UpdateCollision");Require(n.target==expected && n.direction==(crossed?1:-1) && Math.Abs(n.position.X-path[1].Bounds.X)<.01f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.01f,"AI25 actual birth-before-query target/direction/jump crossed="+crossed);
            }
            p.position=saved;Main.player[1]=prior;Console.WriteLine("PASS AI25 +8 pre-action winner crossing and same-winner controls");
        }
        internal static void Jellyfish(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;bool priorWet=p.wet;int priorMode=Main.GameMode,cases=0;
            foreach(int type in new[]{63,64,103,221,242,256})foreach(int branch in new[]{0,1,2,3,4,5})
            {
                for(int x=54;x<=60;x++)for(int y=56;y<60;y++)Main.tile[x,y].liquid=(byte)(branch<4?255:0);
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,700);n.velocity=branch==1?Vector2.Zero:branch==4?new Vector2(4,0):new Vector2(2,.2f);n.oldVelocity=n.velocity;n.target=p.whoAmI;n.direction=1;n.directionY=1;n.wet=branch!=4;n.wetCount=(byte)(n.wet?1:0);n.ai[0]=-1;n.ai[1]=branch==2?1:0;n.ai[2]=branch==2?119:branch==3?419:0;n.timeLeft=750;p.wet=branch<4;Main.GameMode=branch==2 || branch==3?1:0;if(branch==5)n.collideX=n.collideY=true;
                NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1,"AI18 real Source known phase type="+type+" branch="+branch);
                n.oldTarget=n.target;n.AI();if(Math.Abs(n.velocity.X)<.005f)n.velocity.X=0;Call(n,"UpdateCollision");Require(Math.Abs(n.position.X-path[1].Bounds.X)<.01f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.01f && Math.Abs(n.velocity.X-path[1].Vx)<.01f && Math.Abs(n.velocity.Y-path[1].Vy)<.01f,"AI18 swim/charge/frozen-clock/dry/contact first action type="+type+" branch="+branch+" native="+n.position+" V="+n.velocity+" model="+path[1].Bounds.X+","+path[1].Bounds.Y+" V="+path[1].Vx+","+path[1].Vy);cases++;
            }
            for(int x=54;x<=60;x++)for(int y=56;y<60;y++)Main.tile[x,y].liquid=0;p.wet=priorWet;Main.GameMode=priorMode;Console.WriteLine("PASS AI18 six members wet chase/launch/expert clocks/dry/old contact first actions="+cases);JellyfishFrozen(context);JellyfishChoice(context);
        }
        private static void JellyfishChoice(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;var old=Main.player[1];var position=p.position;bool wet=p.wet;int mode=Main.GameMode;Main.GameMode=1;
            var remote=new Player{active=true,whoAmI=1,width=p.width,height=p.height,tankPet=-1,carpetFrame=-1,gravity=.4f,maxFallSpeed=10,maxRunSpeed=3,accRunSpeed=6,runAcceleration=.08f,runSlowdown=.2f};Main.player[1]=remote;
            foreach(bool oldWet in new[]{true,false})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(63);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,700);n.velocity=new Vector2(1,.3f);n.oldVelocity=n.velocity;n.target=1;n.direction=1;n.wet=true;n.wetCount=1;n.ai[2]=417;n.timeLeft=750;
                p.position=new Vector2(n.Center.X+30-p.width/2,n.Center.Y-p.height/2);remote.position=new Vector2(n.Center.X+100-remote.width/2,n.Center.Y-remote.height/2);p.wet=!oldWet;remote.wet=oldWet;
                for(int x=41;x<=45;x++)for(int y=41;y<=48;y++)Main.tile[x,y].liquid=(byte)(p.wet?255:0);
                NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1 && (int)Get(source,"targetPlayer")==p.whoAmI,"AI18 actual later winner supplies pursuit premise");n.oldTarget=n.target;n.AI();if(Math.Abs(n.velocity.X)<.005f)n.velocity.X=0;Call(n,"UpdateCollision");Require(n.target==p.whoAmI && n.ai[1]==(oldWet?1:0) && Math.Abs(n.position.X-path[1].Bounds.X)<.01f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.01f && Math.Abs(n.velocity.X-path[1].Vx)<.01f && Math.Abs(n.velocity.Y-path[1].Vy)<.01f,"AI18 old-target expert wet clock before new-winner wet motor oldWet="+oldWet+" native="+n.velocity+" model="+path[1].Vx+","+path[1].Vy);
            }
            for(int x=41;x<=45;x++)for(int y=41;y<=48;y++)Main.tile[x,y].liquid=0;p.position=position;p.wet=wet;Main.player[1]=old;Main.GameMode=mode;Console.WriteLine("PASS AI18 old expert target wet eligibility vs new pursuit winner wet opposite controls");
            foreach(var npc in Main.npc)npc.active=false;var missing=Main.npc[2];missing.SetDefaults(63);missing.whoAmI=2;missing.active=true;missing.dontTakeDamage=missing.immortal=missing.friendly=false;missing.position=new Vector2(650,700);missing.velocity=Vector2.UnitX;missing.target=p.whoAmI;missing.wet=true;missing.wetCount=1;missing.timeLeft=750;
            int cx=(int)missing.Center.X/16,cy=(int)missing.Bottom.Y/16+1;var savedTile=Main.tile[cx,cy];Main.tile[cx,cy]=null;NativeCombatObservationChecks.Fresh(context,host);var absent=cache.Read(0);Require((absent==null || absent.Count<=1) && (PredictionStop)Get(source,"outcomeStop")==PredictionStop.TerrainUnavailable && Main.tile[cx,cy]==null,"AI18 missing necessary slope/liquid cell stops without fabricating tile");Main.tile[cx,cy]=savedTile;Console.WriteLine("PASS AI18 necessary local cell missing TerrainUnavailable; no tile writes");
        }
        private static void JellyfishFrozen(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;int priorMode=Main.GameMode;bool priorWet=p.wet;Main.GameMode=1;
            foreach(int branch in new[]{0,1,2,3})
            {
                for(int x=35;x<=60;x++)for(int y=40;y<60;y++)Main.tile[x,y].liquid=(byte)(branch==2?0:255);p.wet=branch!=2;
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(63);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,700);n.velocity=branch==2?new Vector2(4,0):new Vector2(2,.2f);n.oldVelocity=n.velocity;n.target=p.whoAmI;n.direction=n.directionY=1;n.wet=branch!=2;n.wetCount=(byte)(n.wet?1:0);n.ai[0]=-1;n.ai[1]=branch==1 || branch==3?1:0;n.ai[2]=branch==1 || branch==3?118:branch==0?419:0;n.timeLeft=750;if(branch==3)n.position=new Vector2(p.Center.X-100-n.width/2,900);cache.Demand(0,1,120);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count==121,"AI18 known phase frozen120 branch="+branch);
                bool entered=false,resumed=false;for(int step=1;step<path.Count;step++){n.oldTarget=n.target;n.AI();entered|=n.ai[1]==1;resumed|=branch==1 && step>2 && !n.dontTakeDamage;if(branch==0 && step==1)Require(n.ai[1]==1 && !n.dontTakeDamage,"AI18 entering action uses old swim gate");if(branch==3 && step==2)Require(n.ai[1]==1 && n.dontTakeDamage,"AI18 near-player slower clock survives replay from capture origin");if(branch==1 && step==2)Require(n.ai[1]==0 && n.dontTakeDamage,"AI18 leaving action uses old frozen gate");if(Math.Abs(n.velocity.X)<.005f)n.velocity.X=0;Call(n,"UpdateCollision");Require(Math.Abs(n.position.X-path[step].Bounds.X)<.01f && Math.Abs(n.position.Y-path[step].Bounds.Y)<.01f && Math.Abs(n.velocity.X-path[step].Vx)<.01f && Math.Abs(n.velocity.Y-path[step].Vy)<.01f,"AI18 frozen clock/motor/dry120 branch="+branch+" step="+step+" native="+n.position+" V="+n.velocity+" model="+path[step].Bounds.X+","+path[step].Bounds.Y+" V="+path[step].Vx+","+path[step].Vy);}
                Require(branch!=0 || entered,"AI18 actual immune entry exists");Require(branch!=1 || resumed,"AI18 actual swim resumes after finite immune clock");Console.WriteLine("PASS AI18 Source frozen120 phaseEntry/phaseExit/dry branch="+branch);
            }
            for(int x=35;x<=60;x++)for(int y=40;y<60;y++)Main.tile[x,y].liquid=0;p.wet=priorWet;Main.GameMode=priorMode;
        }
        internal static void MimicPremise(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;int oldHook=p.grappling[0],oldCount=p.grapCount;bool oldMoon=Main.snowMoon;int oldMode=Main.netMode;p.grappling[0]=0;p.grapCount=0;
            foreach(int branch in new[]{0,1,2,3})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(branch==1 || branch==3?341:85);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,960-n.height);n.target=p.whoAmI;n.direction=1;n.ai[0]=branch==0?0:1;n.ai[3]=1;n.timeLeft=750;Main.snowMoon=branch==3;cache.Demand(0,1,120);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);
                if(branch<2)Require(path!=null && path.Count==121 && (path.Assumptions&PredictionAssumption.NoPlayerMotionNeeded)!=0,"AI25 MP dormant/fixed target movement cannot demand untrusted player future branch="+branch);
                else Require(path!=null && path.Count==12 && path.Stop==PredictionStop.PhaseBoundary,"AI25 independent wait is retained before real jump needs unavailable player geometry, including snowMoon341 branch="+branch);
            }
            p.grappling[0]=oldHook;p.grapCount=oldCount;Main.snowMoon=false;Main.netMode=0;
            foreach(var npc in Main.npc)npc.active=false;var active=Main.npc[2];active.SetDefaults(85);active.whoAmI=2;active.active=true;active.dontTakeDamage=active.immortal=active.friendly=false;active.position=new Vector2(p.position.X-50,960-active.height);active.target=p.whoAmI;active.ai[3]=0;active.timeLeft=750;NativeCombatObservationChecks.Fresh(context,host);var frozen=cache.Read(0);Require(frozen!=null && frozen.Count>20,"AI25 singleplayer known near activation future");bool jumped=false;
            for(int step=1;step<Math.Min(30,frozen.Count);step++){object[] gravity={0f};active.GetType().GetMethod("UpdateNPC_UpdateGravity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(active,gravity);active.oldTarget=active.target;active.AI();jumped|=active.velocity.Y<0;if(!active.noGravity)active.velocity.Y=Math.Min((float)gravity[0],active.velocity.Y+(float)Get(active,"gravity"));Call(active,"UpdateCollision");Require(Math.Abs(active.position.X-frozen[step].Bounds.X)<.01f && Math.Abs(active.position.Y-frozen[step].Bounds.Y)<.01f,"AI25 singleplayer activation/init then real future jump step="+step);}
            Require(jumped,"AI25 actual near activation reaches predicted jump");Main.netMode=oldMode;Main.snowMoon=oldMoon;Console.WriteLine("PASS AI25 dormant/fixed independent vs jump necessary future and singleplayer activation+8/jump");
        }
        internal static void SwordPremise(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;var prior=Main.player[1];Main.player[1]=new Player{active=true,dead=true,whoAmI=1,width=20,height=40};int priorGrapple=p.grappling[0],priorCount=p.grapCount;p.grappling[0]=0;p.grapCount=0;
            foreach(int phase in new[]{1,2,0})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(83);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,700);n.velocity=new Vector2(4,-1);n.target=1;n.direction=-1;n.ai[0]=phase;n.timeLeft=750;cache.Demand(0,1,120);
                NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);
                if(phase==0){Require(path==null || path.Count<=1,"AI23 launch rejects genuinely unavailable complex player future");continue;}
                Require(path!=null && path.Count==121 && (path.Assumptions&PredictionAssumption.NoPlayerMotionNeeded)!=0 && (int)Get(source,"targetPlayer")==p.whoAmI,"AI23 dead-old choice alone cannot require full complex player future phase="+phase+" count="+path?.Count+" stop="+path?.Stop);
                for(int step=1;step<path.Count;step++){n.oldTarget=n.target;n.AI();if(Math.Abs(n.velocity.X)<.005f)n.velocity.X=0;n.position+=n.velocity;var point=path[step];Require(n.target==p.whoAmI && Math.Abs(n.position.X-point.Bounds.X)<.01f && Math.Abs(n.position.Y-point.Bounds.Y)<.01f,"AI23 independent coast after actual dead target replacement phase="+phase+" step="+step);}
            }
            p.grappling[0]=priorGrapple;p.grapCount=priorCount;Main.player[1]=prior;Console.WriteLine("PASS AI23 invalid old target current choice preserves independent untrusted-player coast120; actual launch refuses");
        }
        internal static void Mimics(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;int cases=0;
            foreach(int type in new[]{85,341,629})foreach(int branch in new[]{0,1,2,3,4})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,960-n.height);n.velocity=Vector2.Zero;n.target=p.whoAmI;n.direction=1;n.ai[0]=branch==4?0:1;n.ai[1]=branch==1?1:0;n.ai[2]=branch==1?19:branch==0?11:0;n.ai[3]=branch==3?0:1;n.timeLeft=750;if(branch==2)n.velocity=new Vector2(.3f,-2);
                NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1,"AI25 Source initial/finite phase prefix type="+type+" branch="+branch);
                for(int step=1;step<path.Count;step++)
                {
                    object[] gravity={0f};n.GetType().GetMethod("UpdateNPC_UpdateGravity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(n,gravity);n.oldTarget=n.target;n.AI();if(!n.noGravity)n.velocity.Y=Math.Min((float)gravity[0],n.velocity.Y+(float)Get(n,"gravity"));if(Math.Abs(n.velocity.X)<.005f)n.velocity.X=0;Call(n,"UpdateCollision");var point=path[step];Require(Math.Abs(n.position.X-point.Bounds.X)<.01f && Math.Abs(n.position.Y-point.Bounds.Y)<.01f && Math.Abs(n.velocity.X-point.Vx)<.01f && Math.Abs(n.velocity.Y-point.Vy)<.01f,"AI25 before-action initial/wait/small-big jump type="+type+" branch="+branch+" step="+step+" native="+n.position+" V="+n.velocity+" model="+point.Bounds.X+","+point.Bounds.Y+" V="+point.Vx+","+point.Vy);
                }
                Require(path.Count==121,"AI25 finite known phase full window");cases++;
            }
            Console.WriteLine("PASS AI25 three members dormant/init/wait/small-big/air Source frozen120="+cases);
        }
        internal static void Swords(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;int cases=0;
            foreach(int type in new[]{83,84,179})foreach(int phase in new[]{0,1,2})foreach(bool hit in new[]{false,true})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,700);n.velocity=new Vector2(4,-1);n.target=p.whoAmI;n.direction=1;n.ai[0]=phase;n.ai[1]=phase==1?99:phase==2?119:0;n.justHit=hit;n.timeLeft=750;
                NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1 && ReferenceEquals(path.Identity.Token,n),"AI23 real Source publishes actual actor type="+type);
                for(int step=1;step<path.Count;step++)
                {
                    n.oldTarget=n.target;n.AI();if(Math.Abs(n.velocity.X)<.005f)n.velocity.X=0;n.position+=n.velocity;n.justHit=false;var point=path[step];
                    Require(Math.Abs(n.position.X-point.Bounds.X)<.01f && Math.Abs(n.position.Y-point.Bounds.Y)<.01f && Math.Abs(n.velocity.X-point.Vx)<.01f && Math.Abs(n.velocity.Y-point.Vy)<.01f,"AI23 frozen launch/decelerate/relaunch type="+type+" phase="+phase+" hit="+hit+" step="+step+" native="+n.position+" V="+n.velocity+" model="+point.Bounds.X+","+point.Bounds.Y+" V="+point.Vx+","+point.Vy);
                }
                Require(path.Count==121 && path.Stop==JueMingR.Platform.Combat.PredictionStop.None,"AI23 finite known phase spans full requested window");cases++;
            }
            Console.WriteLine("PASS AI23 Source frozen120 three members launch/decelerate/clock/JustHit="+cases);
            foreach(var npc in Main.npc)npc.active=false;var moving=Main.npc[2];moving.SetDefaults(83);moving.whoAmI=2;moving.active=true;moving.dontTakeDamage=moving.immortal=moving.friendly=false;moving.position=new Vector2(650,700);moving.velocity=new Vector2(4,-1);moving.target=p.whoAmI;moving.direction=1;moving.ai[0]=2;moving.ai[1]=100;moving.timeLeft=750;
            var saved=p.position;p.controlLeft=true;p.velocity=new Vector2(-3,0);cache.Demand(0,1,120);NativeCombatObservationChecks.Fresh(context,host);var frozen=cache.Read(0);Require(frozen!=null && frozen.Count==121 && (frozen.Assumptions&PredictionAssumption.NoPlayerMotionNeeded)==0,"AI23 later launch prepares actual moving player from original timeline");float maximum=0;bool relaunched=false;
            for(int step=1;step<frozen.Count;step++)
            {
                p.HorizontalMovement();p.position.X+=p.velocity.X;moving.oldTarget=moving.target;moving.AI();if(Math.Abs(moving.velocity.X)<.005f)moving.velocity.X=0;moving.position+=moving.velocity;maximum=Math.Max(maximum,Vector2.Distance(moving.position,new Vector2(frozen[step].Bounds.X,frozen[step].Bounds.Y)));if(step==21)relaunched=moving.ai[0]==1 && Math.Abs(moving.velocity.Length()-9)<.001f;
            }
            Require(relaunched && maximum<.3f,"AI23 delayed relaunch uses true future player geometry maximum="+maximum);p.position=saved;p.controlLeft=false;p.velocity=Vector2.Zero;Console.WriteLine("PASS AI23 moving numbered player delayed action21 launch frozen120 max="+maximum);
        }
    }
}
