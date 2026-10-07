using System;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatRetargetChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        // UpdateNPC bypasses collision entirely for a noTileCollide actor.
        private static void Move(NPC n)
        {if(n.noTileCollide){n.oldPosition=n.position;n.position+=n.velocity;}else Call(n,"UpdateCollision");}
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var local=Main.LocalPlayer;
            var remote=Main.player[1];if(remote==null)Main.player[1]=remote=new Player{whoAmI=1};remote.active=true;remote.dead=false;remote.ghost=false;remote.position=new Vector2(1200,local.position.Y);remote.velocity=Vector2.Zero;remote.gravity=.4f;remote.gravDir=1;remote.maxFallSpeed=10;remote.maxRunSpeed=3;remote.accRunSpeed=6;remote.runAcceleration=.08f;remote.runSlowdown=.2f;remote.carpetFrame=-1;remote.tankPet=-1;remote.aggro=0;
            int cases=0;
            foreach(int type in new[]{82,316})foreach(int branch in type==82?new[]{0,1,2,3}:new[]{0,1,2,3})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,700);n.velocity=Vector2.UnitX;n.target=1;n.ai[0]=n.position.X;n.ai[1]=n.position.Y;n.ai[2]=-20;n.ai[3]=0;n.justHit=type==82;n.timeLeft=750;
                remote.dead=type==316 && branch!=1;remote.position=new Vector2(type==316 && branch==1?5000:1200,local.position.Y);
                if(type==82){n.justHit=branch<2;n.ai[2]=branch==1 || branch==3?20:-20;}
                else if(branch==2){n.ai[3]=1;n.localAI[3]=1;n.alpha=100;n.direction=n.directionY=1;}
                else if(branch==3){local.active=false;local.dead=true;remote.active=false;cache.Demand(0,1,120);}
                if(type==316 && branch==3){var read=source.GetType().GetMethod("Read",Flags);var scalar=(NpcMotionState)read.Invoke(null,new object[]{n,1L});Call(source,"Prepare",scalar.Identity,(long)Main.GameUpdateCount);}
                else NativeCombatObservationChecks.Fresh(context,host);
                var frozen=cache.Read(0);bool oldKept=type==82 && branch==2 || type==316 && branch>=2;int expected=oldKept?1:local.whoAmI;
                Require(frozen!=null && frozen.Count>1 && (int)Get(source,"targetPlayer")==expected,"R03 Source preflight matches native first target type="+type+" branch="+branch+" count="+frozen?.Count+" stop="+frozen?.Stop+" premise="+Get(source,"targetPlayer"));
                var state=((NpcMotionState[])Get(source,"states"))[0];var env=new PredictionEnvironment{PlayerIndex=expected,PlayerX=Main.player[expected].Center.X,PlayerY=Main.player[expected].Center.Y,PlayerWidth=Main.player[expected].width,PlayerHeight=Main.player[expected].height,PlayerDead=Main.player[expected].dead,WorldSurface=20,Multiplayer=true};var kernel=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcFiniteFlightMotion").GetMethod("Step",Flags);object[] action={state,env,(IPredictionTerrain)Get(source,"Terrain"),false,PredictionStop.None};Require((bool)kernel.Invoke(null,action),"R03 finite first movement is known");state=(NpcMotionState)action[0];
                n.oldTarget=n.target;n.AI();Move(n);Require(n.target==expected && state.Target==n.target && state.Direction==n.direction && state.DirectionY==n.directionY && Math.Abs(n.position.X-frozen[1].Bounds.X)<.3f && Math.Abs(n.position.Y-frozen[1].Bounds.Y)<.3f,"R03 frozen native target/facing before-action type="+type+" branch="+branch);
                if(type==316 && branch==3)Require((frozen.Assumptions&PredictionAssumption.NoPlayerMotionNeeded)!=0,"No effective candidate retains independent departure without local substitution");
                if(type==82 && branch==2 || type==316 && branch>=2)
                {
                    for(int step=2;step<frozen.Count;step++){n.oldTarget=n.target;n.AI();Move(n);if(type==316 && branch==3)n.CheckActive();Require(Math.Abs(n.position.X-frozen[step].Bounds.X)<.3f && Math.Abs(n.position.Y-frozen[step].Bounds.Y)<.3f,"R03 retained prefix matches original at terminal boundary type="+type+" branch="+branch+" step="+step+" native="+n.position+" vel="+n.velocity+" model="+frozen[step].Bounds.X+","+frozen[step].Bounds.Y+" vel="+frozen[step].Vx+","+frozen[step].Vy);}
                    if(type==82){n.oldTarget=n.target;n.AI();Require(n.target==local.whoAmI && frozen.Count==21 && frozen.Stop==PredictionStop.MissingDependency,"Negative no-hit clock acquires DIFFERENT required player after twenty known actions");}
                    else if(branch==2){n.oldTarget=n.target;n.AI();Require(!n.active && frozen.Stop==PredictionStop.Despawn,"Original fade deactivates at the declared next terminal action");}
                    else Require(n.active && n.timeLeft<=0 && frozen.Stop==PredictionStop.None && (frozen.Assumptions&PredictionAssumption.NetworkObservation)!=0,"Client CheckActive keeps departure after expiry, pending server authority");
                }
                Console.WriteLine("PASS R03 type="+type+" branch="+branch+" JustHit="+n.justHit+" oldDead="+remote.dead+" oldFar="+(remote.position.X>3000)+" premise="+Get(source,"targetPlayer")+" future="+frozen.Count+" stop="+frozen.Stop);cases++;local.active=true;local.dead=false;remote.active=true;
            }
            remote.active=false;
            foreach(var npc in Main.npc)npc.active=false;var leaving=Main.npc[2];leaving.SetDefaults(316);leaving.whoAmI=2;leaving.active=true;leaving.dontTakeDamage=leaving.immortal=leaving.friendly=false;leaving.position=new Vector2(650,700);leaving.velocity=Vector2.UnitX;leaving.direction=1;leaving.target=1;local.active=false;local.dead=true;Main.netMode=0;cache.Demand(0,1,120);
            var snapshot=(NpcMotionState)source.GetType().GetMethod("Read",Flags).Invoke(null,new object[]{leaving,1L});Call(source,"Prepare",snapshot.Identity,(long)Main.GameUpdateCount);Require(cache.Read(0)==null && (PredictionStop)Get(source,"outcomeStop")==PredictionStop.Despawn,"Same no-candidate singleplayer sample retires at actual CheckActive boundary");leaving.oldTarget=leaving.target;leaving.AI();leaving.CheckActive();Require(!leaving.active,"Original singleplayer no active owner retires independently of target geometry");local.active=true;local.dead=false;Main.netMode=1;
            Console.WriteLine("PASS R03 direct boundary cases="+cases);
        }
    }
}
