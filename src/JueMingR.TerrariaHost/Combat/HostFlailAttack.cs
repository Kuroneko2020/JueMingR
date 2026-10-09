using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal static class HostFlailAttack
    {
        private static readonly System.Reflection.MethodInfo copy=typeof(object).GetMethod("MemberwiseClone",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        internal static bool Handles(int type){return type==25 || type==26 || type==35 || type==63 || type==154 || type==247 || type==757 || type==947 || type==948 || type==1058;}
        internal static bool Weapon(Item item){return item!=null && Handles(item.shoot);}
        internal static AttackContact Solve(Player player,Projectile shot,NpcTrajectory timeline,int age,bool beforeNpc,PredictionTerrain terrain,out Vector2 aim)
        {
            aim=HostHeldAttack.DirectionPoint(player,timeline,age+1);
            float speed=24,returnSpeed=16,returnAcceleration=3;int outbound=10;
            switch(shot.type){case 25:speed=14;outbound=15;returnSpeed=10;break;case 154:speed=15;outbound=15;returnSpeed=11;break;case 26:speed=16;outbound=15;returnSpeed=13;break;case 35:speed=17;outbound=15;returnSpeed=14;break;case 63:speed=21;outbound=13;returnSpeed=20;break;case 757:speed=22;outbound=13;returnSpeed=22;break;case 247:speed=23;outbound=13;break;case 947:case 948:speed=12;outbound=13;returnSpeed=8;break;case 1058:speed=23;outbound=16;break;}
            speed/=player.meleeSpeed;returnSpeed/=player.meleeSpeed;returnAcceleration/=player.meleeSpeed;
            int state=(int)shot.ai[0];if(state!=0 && state!=1 && state!=2)return null;
            bool release=state==0 && !player.channel;Vector2 center=shot.Center,velocity=shot.velocity;float clock=shot.ai[1];
            // Native release resets this projectile's local array. Model that
            // known future gate privately, without resetting the actual shot.
            var immunity=shot;
            if(release){immunity=(Projectile)copy.Invoke(shot,null);immunity.localNPCImmunity=new int[shot.localNPCImmunity.Length];}
            var receive=HostAttackReceive.Capture(player,immunity,timeline.Identity.Slot,beforeNpc);
            var motion=NpcPredictionSource.ReadPlayer(player);var env=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,Remix=Main.remixWorld};
            Vector2 mounted=player.MountedCenter;
            for(int step=0;step<60 && age+step+1<timeline.Count;step++)
            {
                if(!beforeNpc || step>0){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,env,terrain,out stop))return null;}
                mounted=player.MountedCenter+new Vector2(motion.X-player.position.X,motion.Y-player.position.Y);int tick=age+step+(beforeNpc?1:0);Vector2 old=center;
                if(state==0)
                {
                    if(release){velocity=(aim-mounted).SafeNormalize(Vector2.UnitX*player.direction)*speed+player.velocity;center=mounted;state=1;clock=0;}
                    else
                    {
                        if(shot.localAI[1]+step+1<=12 || !receive.Allows(step+1))continue;
                        // The real spin collision is an owner-centred ellipse,
                        // independent of the ball's displayed orbit angle.
                        var spin=AttackIntercept.EllipseContact(timeline,tick,aim.X,aim.Y,mounted.X,mounted.Y,55,.8f,player.gravDir>0?.4f:.8f);
                        if(spin!=null && HostMeleeAttack.OwnerAllows(player,shot,timeline[tick].Bounds,Main.npc[timeline.Identity.Slot].noTileCollide,terrain))return spin;
                        continue;
                    }
                }
                else if(state==1)
                {if(player.controlUseItem)return null;if(clock++>=outbound || Vector2.Distance(center,mounted)>=800){state=2;clock=0;velocity*=.3f;}}
                else
                {if(player.controlUseItem || Vector2.Distance(center,mounted)<=returnSpeed)return null;velocity*=.98f;velocity=velocity.MoveTowards((mounted-center).SafeNormalize(Vector2.Zero)*returnSpeed,returnAcceleration);}
                center+=velocity;if(!terrain.ProjectilePassage(old.X,old.Y,center.X,center.Y,shot.width,shot.height))return null;
                if(!receive.Allows(step+1))continue;
                var contact=AttackIntercept.BodyContact(timeline,tick,0,aim.X,aim.Y,center.X,center.Y,shot.width,shot.height,AttackConfidence.Conditional);if(contact!=null)return contact;
            }
            return null;
        }
    }
}
