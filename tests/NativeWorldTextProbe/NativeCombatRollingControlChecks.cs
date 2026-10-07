using System;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatRollingControlChecks
    {
        internal static void Choices(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;var prior=Main.player[1];var remote=new Player{whoAmI=1,active=true,position=new Vector2(1400,p.position.Y),width=p.width,height=p.height,carpetFrame=-1,tankPet=-1};Main.player[1]=remote;int cases=0;
            foreach(int type in Environment.GetEnvironmentVariable("JUEMINGR_STRATEGY_ONLY")=="rollchoice39"?new int[0]:new[]{177,174,378})foreach(bool dead in new[]{false,true})
            {
                foreach(var npc in Main.npc)npc.active=false;remote.dead=dead;var n=Main.npc[2];n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,960-n.height);n.velocity=new Vector2(.5f,0);n.oldVelocity=n.velocity;n.target=1;n.direction=-1;n.ai[0]=-1;n.ai[2]=1;n.timeLeft=750;
                NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1 && (int)Get(source,"targetPlayer")==0,"AI41 launch Source chooses real new numbered player, oldDead="+dead+" type="+type+" count="+path?.Count+" stop="+path?.Stop+" premise="+Get(source,"targetPlayer"));
                object[] gateGravity={0f};n.GetType().GetMethod("UpdateNPC_UpdateGravity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(n,gateGravity);n.oldTarget=n.target;n.AI();n.velocity.Y+=(float)Get(n,"gravity");Call(n,"UpdateCollision");Require(n.target==0 && Math.Abs(n.position.X-path[1].Bounds.X)<.01f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.01f,"AI41 before-action real new target jump type="+type);cases++;
            }
            foreach(int branch in new[]{0,1,2,3})
            {
                foreach(var npc in Main.npc)npc.active=false;remote.dead=branch==1;var n=Main.npc[2];n.SetDefaults(174);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,960-n.height);n.velocity=new Vector2(.5f,0);n.oldVelocity=n.velocity;n.target=1;n.direction=-1;n.ai[0]=-1000;n.ai[2]=1;n.timeLeft=750;n.wet=branch!=3;n.wetCount=(byte)(n.wet?1:0);n.collideY=branch<2;int expected=branch<2?0:1;
                NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1 && (int)Get(source,"targetPlayer")==expected,"AI41 wet collision preflight and non-retarget control branch="+branch);
                object[] gateGravity={0f};n.GetType().GetMethod("UpdateNPC_UpdateGravity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(n,gateGravity);n.oldTarget=n.target;n.AI();n.velocity.Y=Math.Min((float)gateGravity[0],n.velocity.Y+(float)Get(n,"gravity"));Call(n,"UpdateCollision");Require(n.target==expected && Math.Abs(n.position.X-path[1].Bounds.X)<.01f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.01f && Math.Abs(n.velocity.Y-path[1].Vy)<.01f,"AI41 wet collision/old-target first action branch="+branch);cases++;
            }
            foreach(var npc in Main.npc)npc.active=false;var actor=Main.npc[2];actor.SetDefaults(153);actor.whoAmI=2;actor.active=true;actor.dontTakeDamage=actor.immortal=actor.friendly=false;actor.position=new Vector2(720-actor.width+1,960-actor.height);actor.velocity=Vector2.UnitX;actor.oldVelocity=actor.velocity;actor.target=1;actor.direction=0;actor.ai[0]=4;actor.ai[2]=.5f;actor.wet=true;actor.wetCount=1;remote.dead=true;var saved=p.position;p.position.Y=952-actor.height/2-p.height/2;Main.tile[45,59].active(true);Main.tile[45,59].type=Terraria.ID.TileID.Stone;
            NativeCombatObservationChecks.Fresh(context,host);var frozen=cache.Read(0);Require(frozen!=null && frozen.Count>1,"AI39 target-before-step Source prefix exists");object[] gravity={0f};actor.GetType().GetMethod("UpdateNPC_UpdateGravity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(actor,gravity);actor.oldTarget=actor.target;actor.AI();if(!actor.noGravity)actor.velocity.Y=Math.Min((float)gravity[0],actor.velocity.Y+(float)Get(actor,"gravity"));Call(actor,"UpdateCollision");Require(actor.target==0 && actor.directionY==-1 && actor.position.Y<960-actor.height && Math.Abs(actor.position.Y-frozen[1].Bounds.Y)<.01f && Math.Abs(actor.velocity.Y-frozen[1].Vy)<.01f,"AI39 pre-step Face keeps original upward wet response native="+actor.position+" V="+actor.velocity+" model="+frozen[1].Bounds.Y+","+frozen[1].Vy);Main.tile[45,59].active(false);p.position=saved;Main.player[1]=prior;
            Console.WriteLine("PASS rolling Source real pre-action choice cases="+cases+" and AI39 pre-step wet center crossing");
        }
        internal static void UnknownBounce(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;var savedRandom=Main.rand;
            for(int x=37;x<=55;x+=18)for(int y=2;y<79;y++){Main.tile[x,y].active(true);Main.tile[x,y].type=Terraria.ID.TileID.Stone;}
            for(int i=0;i<Main.gore.Length;i++)if(Main.gore[i]==null)Main.gore[i]=new Gore();for(int i=0;i<Main.dust.Length;i++)if(Main.dust[i]==null)Main.dust[i]=new Dust();
            foreach(int budget in new[]{2,3,4})
            {
                int seed=0;while(new Terraria.Utilities.UnifiedRandom(seed).Next(2,5)!=budget)seed++;Main.rand=new Terraria.Utilities.UnifiedRandom(seed);
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(417);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,850);n.velocity=new Vector2(.5f,0);n.oldVelocity=n.velocity;n.target=p.whoAmI;n.direction=1;n.ai[0]=1;n.ai[1]=29;n.timeLeft=750;
                cache.Demand(0,1,120);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>2,"AI39 preparation preserves known future launch before unknown bounce branch");
                for(int step=1;step<path.Count;step++)
                {
                    object[] gravity={0f};n.GetType().GetMethod("UpdateNPC_UpdateGravity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(n,gravity);n.oldTarget=n.target;n.AI();if(step==1)Require(n.ai[2]==budget && n.height==32,"Original seeded AI39 generated true 2/3/4 budget and prepared shape");if(!n.noGravity)n.velocity.Y=Math.Min((float)gravity[0],n.velocity.Y+(float)Get(n,"gravity"));if(Math.Abs(n.velocity.X)<.005f)n.velocity.X=0;Call(n,"UpdateCollision");n.justHit=false;var point=path[step];Require(Math.Abs(n.position.X-point.Bounds.X)<.01f && Math.Abs(n.position.Y-point.Bounds.Y)<.01f && Math.Abs(n.velocity.X-point.Vx)<.01f && Math.Abs(n.velocity.Y-point.Vy)<.01f && n.height==point.Bounds.Height,"Unknown AI39 budget common frozen motion budget="+budget+" step="+step);
                }
                Require(path.Stop==JueMingR.Platform.Combat.PredictionStop.RandomDecision && n.ai[2]==budget-1,"Unknown 2/3/4 counter preserves first real collision, stops before second divergence");
                object[] nextGravity={0f};n.GetType().GetMethod("UpdateNPC_UpdateGravity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(n,nextGravity);n.oldTarget=n.target;n.AI();if(!n.noGravity)n.velocity.Y=Math.Min((float)nextGravity[0],n.velocity.Y+(float)Get(n,"gravity"));Call(n,"UpdateCollision");Require(n.ai[2]==budget-2,"Original next action is exact second collision decision");
                Console.WriteLine("PASS AI39 before-preparation Source shared budget="+budget+" prefixActions="+(path.Count-1)+" stop="+path.Stop);
            }
            for(int x=37;x<=55;x+=18)for(int y=2;y<79;y++)Main.tile[x,y].active(false);Main.tile[37,60].active(true);Main.tile[55,60].active(true);Main.rand=savedRandom;
        }
        internal static void Run(object context,bool hopper)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;int cases=0;
            for(int i=0;i<Main.gore.Length;i++)if(Main.gore[i]==null)Main.gore[i]=new Gore();
            for(int i=0;i<Main.dust.Length;i++)if(Main.dust[i]==null)Main.dust[i]=new Dust();
            foreach(int type in hopper?new[]{177,174,378}:new[]{153,154,496,497,417})foreach(int branch in new[]{0,1,2,3,4,5})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,960-n.height);n.velocity=new Vector2(.5f,0);n.oldVelocity=n.velocity;n.target=p.whoAmI;n.direction=1;n.directionY=-1;n.timeLeft=750;
                if(hopper){n.ai[0]=-1;n.ai[1]=branch==1?2:branch==2?3:0;n.ai[2]=1;if(branch==3)n.velocity=new Vector2(.5f,-2);if(branch==4)n.wet=true;if(branch==5){n.position=new Vector2(p.position.X,960-n.height);}}
                else {n.ai[0]=branch==0?0:branch==1?1:branch==2?3:branch==3?4:branch==4?5:type==417?6:0;n.ai[1]=branch==1?28:branch==3?89:0;n.ai[2]=branch==3?.01f:3;if(branch==5){n.justHit=true;if(type==417){n.height=32;n.position.Y=850;n.ai[1]=1;}}n.wet=branch==0;}
                n.wetCount=(byte)(n.wet?1:0);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1,"Native rolling Source has real actor prefix type="+type+" branch="+branch+" count="+path?.Count+" stop="+path?.Stop);
                object[] gravity={0f};n.GetType().GetMethod("UpdateNPC_UpdateGravity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(n,gravity);n.oldTarget=n.target;n.AI();if(!n.noGravity)n.velocity.Y=Math.Min((float)gravity[0],n.velocity.Y+(float)Get(n,"gravity"));if(Math.Abs(n.velocity.X)<.005f)n.velocity.X=0;if(n.noTileCollide)n.position+=n.velocity;else Call(n,"UpdateCollision");
                Require(Math.Abs(n.position.X-path[1].Bounds.X)<.0001f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.0001f && Math.Abs(n.velocity.X-path[1].Vx)<.0001f && Math.Abs(n.velocity.Y-path[1].Vy)<.0001f && n.height==path[1].Bounds.Height,"Native rolling first movement type="+type+" branch="+branch+" native="+n.position+" V="+n.velocity+" H="+n.height+" model="+path[1].Bounds.X+","+path[1].Bounds.Y+" V="+path[1].Vx+","+path[1].Vy+" H="+path[1].Bounds.Height);cases++;
            }
            Console.WriteLine("PASS AI"+(hopper?41:39)+" Source phase/launch/recovery/wet controls="+cases);
            Frozen(context,hopper);
        }
        private static void Frozen(object context,bool hopper)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;
            foreach(int type in hopper?new[]{177,174,378}:new[]{496,417})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,type==417?850:960-n.height);n.velocity=new Vector2(.5f,0);n.oldVelocity=n.velocity;n.target=p.whoAmI;n.direction=1;n.directionY=-1;n.timeLeft=750;
                if(hopper){n.ai[0]=-45;n.ai[1]=type==177?2:3;n.ai[2]=1;if(type==378){n.ai[1]=5;n.ai[2]=3;}}
                else {n.ai[0]=type==417?6:1;n.ai[1]=type==496?28:0;n.ai[2]=3;if(type==417)n.height=32;}
                cache.Demand(0,1,120);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1,"Known rolling frozen window exists type="+type);
                int bigJumps=0;for(int step=1;step<path.Count;step++)
                {
                    object[] gravity={0f};n.GetType().GetMethod("UpdateNPC_UpdateGravity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(n,gravity);n.oldTarget=n.target;n.AI();Require(n.active,"Rolling freeze does not include an already retired original action");if(!n.noGravity)n.velocity.Y=Math.Min((float)gravity[0],n.velocity.Y+(float)Get(n,"gravity"));if(Math.Abs(n.velocity.X)<.005f)n.velocity.X=0;if(n.noTileCollide)n.position+=n.velocity;else Call(n,"UpdateCollision");n.justHit=false;
                    if(hopper && type!=378 && n.ai[0]==-200 && n.ai[1]==0)bigJumps++;var point=path[step];Require(Math.Abs(n.position.X-point.Bounds.X)<.01f && Math.Abs(n.position.Y-point.Bounds.Y)<.01f && Math.Abs(n.velocity.X-point.Vx)<.01f && Math.Abs(n.velocity.Y-point.Vy)<.01f && n.height==point.Bounds.Height,"Rolling frozen action type="+type+" step="+step+" phase="+n.ai[0]+" native="+n.position+" V="+n.velocity+" model="+point.Bounds.X+","+point.Bounds.Y+" V="+point.Vx+","+point.Vy);
                }
                if(hopper && type!=378)Require(bigJumps>0,"Frozen hopper anticipates the later large jump after small jumps");
                if(type==378){n.oldTarget=n.target;n.AI();Require(!n.active && path.Stop==JueMingR.Platform.Combat.PredictionStop.Despawn,"Observed explosion retires at exact original action");}
                Console.WriteLine("PASS AI"+(hopper?41:39)+" Source frozen type="+type+" actions="+(path.Count-1)+" stop="+path.Stop+" finalPhase="+n.ai[0]);
            }
        }
    }
}
