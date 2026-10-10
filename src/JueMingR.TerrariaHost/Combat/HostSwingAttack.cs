using System;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // Ordinary useStyle1/3 has no aim-angle input. Replay only its native
    // direction/animation pose, on a private player, with the frame delivered
    // by the natural Animate call. It never loads textures or re-enters an
    // animation/use/damage method. Facing remains a separate permission.
    internal sealed class HostSwingAttack
    {
        private static readonly MethodInfo copy=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
        private static readonly FieldInfo volcano=typeof(Player).GetField("_spawnVolcanoExplosion",BindingFlags.Instance|BindingFlags.NonPublic);
        private Item weapon;private Rectangle frame;private float mountOffset;
        internal long Revision {get;private set;}
        internal static bool Handles(Item item){return item!=null && !item.IsAir && (item.useStyle==1 || item.useStyle==3) && (item.shoot<=0 || HostPhasebladeAttack.Handles(item.shoot) || HostFiniteShotAttack.Handles(item.shoot)) && item.damage>0 && !item.noMelee && item.pick==0 && item.axe==0 && item.hammer==0 && !item.summon;}
        internal static bool Mixed(Item item){return Handles(item) && item.shoot>0 && !HostPhasebladeAttack.Handles(item.shoot);}
        // OwnerOnlyCode arms the capsule at the start of a real Volcano use.
        // An idle preview hypothesizes that start on a private body. Later
        // real hits may consume it, so this state also invalidates preview reuse.
        internal static bool CapsuleOnly(Player player,Item item)
        {return item.type==121 && (player.itemAnimation<=0 || player.ItemAnimationJustStarted || (bool)volcano.GetValue(player));}
        internal void Observe(Item item,Rectangle actualFrame,float offset){if(!ReferenceEquals(weapon,item) || frame!=actualFrame || mountOffset!=offset)Revision++;weapon=item;frame=actualFrame;mountOffset=offset;}
        internal void Clear(){weapon=null;frame=Rectangle.Empty;}
        // Idle Animate supplies the same native item frame as an attack. Only
        // the future animation clock is hypothesized on a private player; the
        // real player never uses the item or grants facing/input permission.
        internal AttackContact Preview(Player player,Item item,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,int steps=120)
        {return PreviewPrepared(player,item,timeline,clock,terrain,HostAttackPlayerFrame.Capture(player,timeline),steps);}
        internal AttackContact PreviewPrepared(Player player,Item item,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,HostAttackPlayerFrame playerFrame,int steps=120)
        {
            if(player.itemAnimation>0)return SolvePrepared(player,item,timeline,clock,terrain,playerFrame,steps:steps);
            var body=(Player)copy.Invoke(player,null);
            int frames=item.melee && !ItemID.Sets.NoMeleeSpeedBonus[item.type]?Math.Max(1,(int)(item.useAnimation*player.meleeSpeed)):item.useAnimation;
            body.itemAnimation=body.itemAnimationMax=frames;
            if(item.type==121)volcano.SetValue(body,true);
            return SolvePrepared(body,item,timeline,clock,terrain,playerFrame,steps:steps);
        }
        internal AttackContact Solve(Player player,Item item,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,bool naturalPose=false,int steps=120)
        {return SolvePrepared(player,item,timeline,clock,terrain,HostAttackPlayerFrame.Capture(player,timeline),naturalPose,steps);}
        private AttackContact SolvePrepared(Player player,Item item,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,HostAttackPlayerFrame playerFrame,bool naturalPose=false,int steps=120)
        {
            if(!ReferenceEquals(item,weapon) || frame.Width<=0 || frame.Height<=0 || player.itemAnimation<=0 || player.itemAnimationMax<=0)return null;
            var body=(Player)copy.Invoke(player,null);var motion=playerFrame.Motion;
            // Every simulated pose below is inside this one useAnimation.
            // Umbrella's idle HoldStyle cap resumes only after that window.
            motion.UmbrellaHold=false;
            var environment=playerFrame.Environment;
            int first=clock.NextWorld?1:0,slot=timeline.Identity.Slot;var npc=Main.npc[slot];
            bool capsuleOnly=CapsuleOnly(player,item);
            if(!player.CanNPCBeHitByPlayerOrPlayerProjectile(npc))return null;
            for(int step=first;step<player.itemAnimation && step<120 && step-first<steps;step++)
            {
                if(step>0){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,environment,terrain,out stop))return null;}
                HostAttackPlayerFrame.ApplyPose(body,motion);body.itemAnimation=player.itemAnimation-step;
                int receiveTick=clock.Age+step-(clock.NextWorld?1:0);if(receiveTick<0 || receiveTick>=timeline.Count)return null;
                int elapsed=clock.LaunchDelay+step;
                if(npc.immune[player.whoAmI]!=0 && (npc.immune[player.whoAmI]<0 || npc.immune[player.whoAmI]>elapsed-1) || player.attackCD>elapsed || player.meleeNPCHitCooldown[slot]>elapsed+(naturalPose?1:0))continue;
                if(!(naturalPose && step==0)){body.itemLocation=Pose(body,item,frame,mountOffset);body.itemRotation=Rotation(body,item);}
                bool blocked;Rectangle box;body.ItemCheck_GetMeleeHitbox(item,frame,out blocked,out box);if(blocked)continue;
                var target=timeline[receiveTick].ReceiveBounds;bool clear;PredictionStop failure;
                if(!timeline[receiveTick].NoTileCollide && (!terrain.CanHit(new MotionRect(body.position.X,body.position.Y,body.width,body.height),target,out clear,out failure) || !clear && !Lines(body,target,terrain)))continue;
                AttackContact result;
                if(item.type==121)
                {
                    // Use the locked pure shape query with a future PRIVATE
                    // pose. Native's pending explosion admits only its capsule;
                    // an already consumed real strike also admits the rectangle.
                    Vector2 a,b,outward;body.GetPointOnSwungItemPath(70,70,0,body.GetAdjustedItemScale(item),out a,out outward);body.GetPointOnSwungItemPath(70,70,.9f,body.GetAdjustedItemScale(item),out b,out outward);
                    var rectangle=new Rectangle((int)target.X,(int)target.Y,(int)target.Width,(int)target.Height);
                    bool overlap=Utils.LineRectangleDistance(rectangle,a,b)<=16 || !capsuleOnly && box.Intersects(rectangle);
                    var point=rectangle.ClosestPointInRect(b);
                    result=AttackIntercept.MeleeShapeContact(timeline,receiveTick,overlap,target.CenterX,target.CenterY,point.X,point.Y);
                }
                else result=AttackIntercept.MeleeContact(timeline,receiveTick,box.X,box.Y,box.Width,box.Height,target.CenterX,target.CenterY);
                if(result!=null)return result;
            }
            return null;
        }
        private static bool Lines(Player p,MotionRect target,PredictionTerrain terrain)
        {Vector2 upper=p.Center+new Vector2(p.direction*p.width/2,p.gravDir*-p.height/3f),side=p.Center+new Vector2(p.direction*p.width/2,0);return terrain.MeleeLine(upper.X,upper.Y,target.CenterX,target.CenterY-(int)target.Height/3) || terrain.MeleeLine(upper.X,upper.Y,target.CenterX,target.CenterY) || terrain.MeleeLine(side.X,side.Y,target.CenterX,target.CenterY+(int)target.Height/3);}
        private static float Rotation(Player p,Item item)
        {
            float rotation=item.useStyle==3?(p.itemAnimation>p.itemAnimationMax*.666?-1.3f:.8f)*p.direction:
                ((float)p.itemAnimation/p.itemAnimationMax-.5f)*-p.direction*3.5f-p.direction*.3f;
            return p.gravDir==-1?-rotation:rotation;
        }
        // Finite .8 ApplyUseStyle pose, including the umbrella's initial
        // non-damaging third and its later thrust. No live Animate is replayed.
        private static Vector2 Pose(Player p,Item item,Rectangle f,float mount)
        {
            if(item.useStyle==3)
            {
                Vector2 thrust;
                if(p.itemAnimation>p.itemAnimationMax*.666)thrust=new Vector2(-1000,-1000);
                else
                {
                    thrust=new Vector2(p.position.X+p.width*.5f+(f.Width*.5f-4)*p.direction,p.position.Y+24+mount);
                    float offset=(float)p.itemAnimation/p.itemAnimationMax*f.Width*p.direction*p.GetAdjustedItemScale(item)*1.2f-10*p.direction;
                    if(offset> -4 && p.direction==-1)offset=-8;if(offset<4 && p.direction==1)offset=8;
                    thrust.X-=offset;if(item.type==946 || item.type==4707)thrust.X-=6*p.direction;
                }
                if(p.gravDir==-1)thrust.Y=p.position.Y+p.height+(p.position.Y-thrust.Y);return thrust;
            }
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
