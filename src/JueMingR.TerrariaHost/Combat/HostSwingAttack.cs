using System;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // Ordinary useStyle1 has no aim-angle input. Replay only its native
    // direction/animation pose, on a private player, with the frame delivered
    // by the natural Animate call. It never loads textures or re-enters an
    // animation/use/damage method. Facing remains a separate permission.
    internal sealed class HostSwingAttack
    {
        private static readonly MethodInfo copy=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
        private Item weapon;private Rectangle frame;private float mountOffset;
        // Volcano's pending strike replaces the ordinary rectangle with a
        // capsule. Its existing live geometry is not a future pose oracle.
        internal static bool Handles(Item item){return item!=null && !item.IsAir && item.type!=121 && item.useStyle==1 && item.shoot<=0 && item.damage>0 && !item.noMelee && item.pick==0 && item.axe==0 && item.hammer==0 && !item.summon;}
        internal void Observe(Item item,Rectangle actualFrame,float offset){weapon=item;frame=actualFrame;mountOffset=offset;}
        internal void Clear(){weapon=null;frame=Rectangle.Empty;}
        internal AttackContact Solve(Player player,Item item,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,bool naturalPose=false)
        {
            if(!ReferenceEquals(item,weapon) || frame.Width<=0 || frame.Height<=0 || player.itemAnimation<=0 || player.itemAnimationMax<=0)return null;
            var body=(Player)copy.Invoke(player,null);var motion=NpcPredictionSource.ReadPlayer(player);
            var environment=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,Remix=Main.remixWorld};
            int first=clock.NextWorld?1:0,slot=timeline.Identity.Slot;var npc=Main.npc[slot];
            if(!player.CanNPCBeHitByPlayerOrPlayerProjectile(npc))return null;
            for(int step=first;step<player.itemAnimation && step<120;step++)
            {
                if(step>0){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,environment,terrain,out stop))return null;}
                body.position=new Vector2(motion.X,motion.Y);body.itemAnimation=player.itemAnimation-step;
                int receiveTick=clock.Age+step-(clock.NextWorld?1:0);if(receiveTick<0 || receiveTick>=timeline.Count)return null;
                if(npc.immune[player.whoAmI]!=0 && (npc.immune[player.whoAmI]<0 || npc.immune[player.whoAmI]>step-1) || player.attackCD>step || player.meleeNPCHitCooldown[slot]>step+(naturalPose?1:0))continue;
                if(!(naturalPose && step==0))body.itemLocation=Pose(body,item,frame,mountOffset);
                bool blocked;Rectangle box;body.ItemCheck_GetMeleeHitbox(item,frame,out blocked,out box);if(blocked)continue;
                var target=timeline[receiveTick].ReceiveBounds;bool clear;PredictionStop failure;
                if(!npc.noTileCollide && (!terrain.CanHit(new MotionRect(body.position.X,body.position.Y,body.width,body.height),target,out clear,out failure) || !clear && !Lines(body,target,terrain)))continue;
                var result=AttackIntercept.MeleeContact(timeline,receiveTick,box.X,box.Y,box.Width,box.Height,target.CenterX,target.CenterY);if(result!=null)return result;
            }
            return null;
        }
        private static bool Lines(Player p,MotionRect target,PredictionTerrain terrain)
        {Vector2 upper=p.Center+new Vector2(p.direction*p.width/2,p.gravDir*-p.height/3f),side=p.Center+new Vector2(p.direction*p.width/2,0);return terrain.MeleeLine(upper.X,upper.Y,target.CenterX,target.CenterY-(int)target.Height/3) || terrain.MeleeLine(upper.X,upper.Y,target.CenterX,target.CenterY) || terrain.MeleeLine(side.X,side.Y,target.CenterX,target.CenterY+(int)target.Height/3);}
        // Finite extraction of .8 ApplyUseStyle's useStyle1 location branch;
        // rotation affects rendering, not this phased axis-aligned hitbox.
        private static Vector2 Pose(Player p,Item item,Rectangle f,float mount)
        {
            bool late=p.itemAnimation<p.itemAnimationMax*.333,mid=!late && p.itemAnimation<p.itemAnimationMax*.666;
            float dx,dy;Vector2 correction;
            if(Item.claw[item.type]){dx=late?10:mid?8:6;dy=late?26:mid?24:20;correction=Vector2.Zero;}
            else
            {
                dx=late?10:mid?10:6;
                if(f.Width>32)dx=late?14:mid?18:14;if(!late && !mid && f.Width>=48)dx=18;
                if(f.Width>=52)dx=24;if(f.Width>=64)dx=28;if(f.Width>=92)dx=38;
                bool extra=item.type==2330 || item.type==2320 || item.type==2341;if(extra)dx+=late?8:4;if(item.type==671)dx+=late?12:mid?6:8;
                dy=late?24:10;if(!late){if(mid && f.Height>32)dy=8;if(f.Height>52)dy=12;if(f.Height>64)dy=14;if(extra)dy+=4;if(item.type==671)dy+=mid?10:8;}
                correction=late?new Vector2(-4,1):mid?new Vector2(-6,-4):new Vector2(4,-2);
                if(!ItemID.Sets.UsesBetterMeleeItemLocation[item.type])correction=Vector2.Zero;
            }
            Vector2 result=new Vector2(p.position.X+p.width*.5f+(f.Width*.5f-dx)*p.direction*(late || mid?1:-1),p.position.Y+dy+mount)+correction*p.Directions;
            if(p.gravDir==-1)result.Y=p.position.Y+p.height+(p.position.Y-result.Y);return result;
        }
    }
}
