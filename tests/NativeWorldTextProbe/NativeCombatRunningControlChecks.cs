using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatRunningControlChecks
    {
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;int cases=0;
            for(int i=0;i<Main.dust.Length;i++)if(Main.dust[i]==null)Main.dust[i]=new Dust();
            foreach(int type in new[]{86,155,315,329,410,423,546})foreach(int branch in new[]{0,1,2,3})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,960-n.height);n.oldPosition=n.position-Vector2.UnitX;n.velocity=Vector2.UnitX;n.oldVelocity=n.velocity;n.target=p.whoAmI;n.direction=n.spriteDirection=1;n.ai[3]=branch==2?30:branch==3?29:0;n.justHit=branch==3;n.timeLeft=750;
                if(branch==1){int col=(int)((n.position.X+n.width/2+(n.width/2+2)+6)/16);for(int y=57;y<60;y++){Main.tile[col,y].active(true);Main.tile[col,y].type=Terraria.ID.TileID.Stone;}}
                NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1,"AI26 real Source finite run/jump/block prefix type="+type+" branch="+branch);
                object[] gravity={0f};n.GetType().GetMethod("UpdateNPC_UpdateGravity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(n,gravity);n.oldTarget=n.target;n.AI();if(!n.noGravity)n.velocity.Y=Math.Min((float)gravity[0],n.velocity.Y+(float)Get(n,"gravity"));if(Math.Abs(n.velocity.X)<.005f)n.velocity.X=0;Call(n,"UpdateCollision");Require(Math.Abs(n.position.X-path[1].Bounds.X)<.01f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.01f && Math.Abs(n.velocity.X-path[1].Vx)<.01f && Math.Abs(n.velocity.Y-path[1].Vy)<.01f,"AI26 native run/obstacle/stuck/JustHit first action type="+type+" branch="+branch+" native="+n.position+" V="+n.velocity+" model="+path[1].Bounds.X+","+path[1].Bounds.Y+" V="+path[1].Vx+","+path[1].Vy);cases++;
                if(branch==1)for(int x=35;x<50;x++)for(int y=57;y<60;y++)Main.tile[x,y].active(false);
            }
            Console.WriteLine("PASS AI26 seven native members run/jump/stuck29-30/JustHit Source first="+cases);
            Frozen(context);
            WindAndNeighbor(context);
            Choice(context);
        }
        private static void Frozen(object context)
        {
            var host=Get(context,"CombatObservation");var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");var p=Main.LocalPlayer;
            bool savedMoon=Main.pumpkinMoon;int savedMode=Main.netMode;Main.pumpkinMoon=true;
            foreach(int branch in new[]{0,1,2,3,4,5})
            {
                int type=branch==0?86:branch==1?155:branch==2?315:branch==3?329:branch==4?423:410;
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,960-n.height);n.oldPosition=n.position-Vector2.UnitX;n.velocity=Vector2.UnitX;n.oldVelocity=n.velocity;n.target=p.whoAmI;n.direction=n.directionY=1;n.spriteDirection=type==423 || type==410?-1:1;n.timeLeft=750;
                n.ai[1]=branch==4?58:branch==5?237:0;n.ai[2]=branch==4?1:0;Main.netMode=branch==5?0:savedMode;
                if(branch==0){int col=(int)((n.position.X+n.width/2+(n.width/2+2)+6)/16);for(int y=57;y<60;y++){Main.tile[col,y].active(true);Main.tile[col,y].type=Terraria.ID.TileID.Stone;}}
                cache.Demand(0,1,120);NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1,"AI26 finite window branch="+branch);
                bool jumped=false,recovered=false;for(int step=1;step<path.Count;step++)
                {object[] gravity={0f};n.GetType().GetMethod("UpdateNPC_UpdateGravity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(n,gravity);n.oldTarget=n.target;n.AI();jumped|=n.velocity.Y<0;recovered|=branch==4 && n.ai[2]==0;if(!n.noGravity)n.velocity.Y=Math.Min((float)gravity[0],n.velocity.Y+(float)Get(n,"gravity"));if(Math.Abs(n.velocity.X)<.005f)n.velocity.X=0;Call(n,"UpdateCollision");bool priorServer=Main.dedServ;Main.dedServ=true;n.FindFrame();Main.dedServ=priorServer;Require(Math.Abs(n.position.X-path[step].Bounds.X)<.01f && Math.Abs(n.position.Y-path[step].Bounds.Y)<.01f && Math.Abs(n.velocity.X-path[step].Vx)<.01f && Math.Abs(n.velocity.Y-path[step].Vy)<.01f,"AI26 before-action frozen phase type="+type+" step="+step+" native="+n.position+" V="+n.velocity+" model="+path[step].Bounds.X+","+path[step].Bounds.Y+" V="+path[step].Vx+","+path[step].Vy);}
                if(branch==5){n.AI();Require(!n.active && path.Stop==PredictionStop.Despawn && path.Count==3,"AI26 410 exact 240 clock terminal");}
                else{Require(path.Count==121,"AI26 full finite120 branch="+branch);Require(branch!=0 || jumped,"AI26 obstacle jump actually occurs");Require(branch!=4 || recovered,"AI26 423 random cooldown recovery shares deterministic motor");}
                Console.WriteLine("PASS AI26 Source frozen branch="+branch+" actions="+(path.Count-1)+" stop="+path.Stop+" jump="+jumped+" recovery="+recovered);
                if(branch==0)for(int x=35;x<50;x++)for(int y=57;y<60;y++)Main.tile[x,y].active(false);
            }
            Main.pumpkinMoon=savedMoon;Main.netMode=savedMode;
        }
        private static void WindAndNeighbor(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;float oldWind=Main.windSpeedTarget;bool desert=p.ZoneDesert,sand=p.ZoneSandstorm;
            p.ZoneDesert=p.ZoneSandstorm=true;
            foreach(bool neighbor in new[]{false,true})foreach(float wind in new[]{-.8f,.8f})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(546);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,800);n.oldPosition=n.position-Vector2.UnitX;n.velocity=new Vector2(4,.1f);n.oldVelocity=n.velocity;n.target=p.whoAmI;n.direction=1;n.spriteDirection=-1;n.timeLeft=750;Main.windSpeedTarget=wind;
                var other=Main.npc[3];if(neighbor){other.SetDefaults(546);other.whoAmI=3;other.active=true;other.friendly=true;other.position=n.position+new Vector2(5,2);}
                NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1 && ReferenceEquals(path.Identity.Token,n),"AI26 real selected actor wind/neighbor Source prefix");
                Main.windSpeedTarget=-wind;Call(source,"Prepare",path.Identity,(long)Main.GameUpdateCount);var changed=cache.Read(0);Require(changed!=null && !ReferenceEquals(path,changed),"AI26 same-tick wind driver invalidates even unchanged current wind");Main.windSpeedTarget=wind;Call(source,"Prepare",path.Identity,(long)Main.GameUpdateCount);path=cache.Read(0);
                if(neighbor){other.position.X-=10;Call(source,"Prepare",path.Identity,(long)Main.GameUpdateCount);changed=cache.Read(0);Require(changed!=null && !ReferenceEquals(path,changed),"AI26 same-tick current neighbor impulse invalidates");other.position.X+=10;Call(source,"Prepare",path.Identity,(long)Main.GameUpdateCount);path=cache.Read(0);}
                object[] gravity={0f};n.GetType().GetMethod("UpdateNPC_UpdateGravity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(n,gravity);n.oldTarget=n.target;n.AI();if(!n.noGravity)n.velocity.Y=Math.Min((float)gravity[0],n.velocity.Y+(float)Get(n,"gravity"));Call(n,"UpdateCollision");Require(Math.Abs(n.position.X-path[1].Bounds.X)<.01f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.01f && Math.Abs(n.velocity.X-path[1].Vx)<.01f,"AI26 wind cap/current exact neighbor impulse first wind="+wind+" neighbor="+neighbor+" native="+n.velocity+" model="+path[1].Vx+","+path[1].Vy);
            }
            Main.windSpeedTarget=oldWind;p.ZoneDesert=desert;p.ZoneSandstorm=sand;Console.WriteLine("PASS AI26 actual target wind +/- and finite current neighbor first; same-tick wind invalidation");
        }
        private static void Choice(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var p=Main.LocalPlayer;var old=Main.player[1];var remote=new Player{active=true,whoAmI=1,width=20,height=40,tankPet=-1,carpetFrame=-1,gravity=.4f,maxFallSpeed=10,maxRunSpeed=3,accRunSpeed=6,runAcceleration=.08f,runSlowdown=.2f};remote.position=new Vector2(1200,920);Main.player[1]=remote;
            foreach(int branch in new[]{0,1,2,3})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(86);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,960-n.height);n.oldPosition=n.position-Vector2.UnitX;n.velocity=Vector2.UnitX;n.target=1;n.direction=n.spriteDirection=1;n.ai[3]=branch==0?29:30;n.justHit=branch>=2;n.timeLeft=750;remote.dead=branch==3;
                int expected=branch==1?1:p.whoAmI;NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Require(path!=null && path.Count>1 && (int)Get(source,"targetPlayer")==expected,"AI26 same-action counter/JustHit controls true premise target branch="+branch+" actual="+Get(source,"targetPlayer"));
                object[] gravity={0f};n.GetType().GetMethod("UpdateNPC_UpdateGravity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(n,gravity);n.oldTarget=n.target;n.AI();if(!n.noGravity)n.velocity.Y=Math.Min((float)gravity[0],n.velocity.Y+(float)Get(n,"gravity"));Call(n,"UpdateCollision");Require(n.target==expected && Math.Abs(n.position.X-path[1].Bounds.X)<.01f && Math.Abs(n.position.Y-path[1].Bounds.Y)<.01f,"AI26 old/new numbered role follows actual counter action branch="+branch);
            }
            Main.player[1]=old;Console.WriteLine("PASS AI26 blocked29/30 x JustHit old alive/dead true Source and Step choice");
        }
    }
}
