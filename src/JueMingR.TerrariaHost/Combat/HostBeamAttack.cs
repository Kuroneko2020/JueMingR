using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;

namespace JueMingR.TerrariaHost.Combat
{
    internal static class HostBeamAttack
    {
        internal sealed class Child
        {
            internal readonly Projectile Shot;internal readonly ProjectileKey Key,Parent;
            internal Child(Projectile shot){Shot=shot;Key=shot.key;Parent=(ProjectileKey)shot.ai[1];}
            internal bool Valid(Projectile parent){return Shot.active && Shot.key.Equals(Key) && Parent.Equals(parent.key) && ((ProjectileKey)Shot.ai[1]).Equals(parent.key) && Shot.owner==parent.owner;}
        }
        internal static AttackContact Solve(Player player,Projectile parent,List<Child> children,Vector2 aim,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,out Child receipt)
        {
            int age=clock.Age;receipt=null;
            if(children.Count==0 || !player.channel)return null;
            var receives=new HostAttackReceive[children.Count];for(int i=0;i<receives.Length;i++)receives[i]=HostAttackReceive.Capture(player,children[i].Shot,timeline.Identity.Slot,clock.BeforeNpc,clock.NextWorld);
            Vector2 direction=parent.velocity.SafeNormalize(Vector2.UnitY),mounted=player.RotatedRelativePoint(player.MountedCenter),armOffset=player.GetArmPosition()-mounted;
            var motion=NpcPredictionSource.ReadPlayer(player);var environment=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,Remix=Main.remixWorld};
            var lengths=new float[children.Count];for(int i=0;i<lengths.Length;i++)lengths[i]=children[i].Shot.localAI[1];
            var fees=new HostAttackResources.FeeClock(player,parent);
            bool scannedRay=false;Vector2 scannedOrigin=default(Vector2),scannedUnit=default(Vector2);float scannedLength=0;
            for(int step=0;step<24 && age+step+1<timeline.Count;step++)
            {
                // The player's movement is already complete at action sampling;
                // a completed-world preparation predicts the next player's step.
                if(clock.MovePlayer(step)){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,environment,terrain,out stop))return null;}
                Vector2 origin=mounted+new Vector2(motion.X-player.position.X,motion.Y-player.position.Y);
                Vector2 requested=(aim-origin).SafeNormalize(Vector2.UnitY);
                float phase;bool readsDirection;if(!fees.Advance(out phase,out readsDirection))return null;
                if(parent.type==633)direction=Vector2.Lerp(requested,direction,.92f).SafeNormalize(Vector2.UnitY);
                else if(readsDirection)direction=requested;
                Vector2 parentCenter=parent.type==460?origin+armOffset-direction*(player.HeldItem.shootSpeed*parent.scale):origin;
                for(int i=0;i<children.Count;i++)
                {
                    var child=children[i];if(!child.Valid(parent))continue;
                    Vector2 center,unit;float scale;int nativeAge=clock.FirstTick+step;
                    if(parent.type==633)
                    {
                        if(phase<=30)continue;
                        float index=child.Shot.ai[0]-2.5f,spread=phase<180?1-phase/180:0;
                        float period=phase<120?20-4*(phase/120):phase<180?16-10*((phase-120)/60):1.75f;
                        float lateral=phase<180?20-phase/180*14:6,back=phase<180?-22+phase/180*20:-2;
                        float angle=(phase+index*period)/(period*6)*MathHelper.TwoPi;
                        Vector2 wave=Vector2.UnitY.RotatedBy(angle);
                        center=parentCenter+direction*(16+back)+new Vector2(wave.X*4,wave.Y*lateral).RotatedBy(direction.ToRotation())-new Vector2(0,parent.gfxOffY);
                        unit=direction.RotatedBy(wave.Y*MathHelper.Pi/6*spread);scale=1.4f*(1-spread);
                    }
                    else
                    {
                        if(phase<180 || child.Shot.ai[0]+step+1>=300)continue;
                        center=parentCenter+direction*16-new Vector2(0,parent.gfxOffY);unit=direction;
                        scale=Math.Min(1,(float)Math.Sin((child.Shot.ai[0]+step+1)*Math.PI/300)*10);
                    }
                    // Native LaserScan samples zero-width rays. A pure bounded
                    // trace subtracts two tiles on obstruction; this is a safe
                    // lower bound, not a claim to reproduce every slope scan.
                    Vector2 scan=parent.type==633 && phase>=180?parentCenter:center;
                    // Full-charge prism children share the exact scan ray,
                    // despite separate visible lateral positions and smoothed
                    // lengths. Reuse only exact origin/direction in this one
                    // immutable solve; changing rays or the next prepare scan
                    // afresh. Each child's native length smoothing stays independent.
                    if(!scannedRay || scan!=scannedOrigin || unit!=scannedUnit)
                    {scannedLength=Length(scan,unit,terrain);scannedOrigin=scan;scannedUnit=unit;scannedRay=true;}
                    lengths[i]=MathHelper.Lerp(lengths[i],scannedLength,parent.type==633?.75f:.5f);
                    if(!receives[i].Allows(step+1))continue;
                    var contact=AttackIntercept.LineContact(timeline,nativeAge,aim.X,aim.Y,center.X,center.Y,center.X+unit.X*lengths[i],center.Y+unit.Y*lengths[i],22*scale);
                    if(contact!=null){receipt=child;return contact;}
                }
            }
            return null;
        }
        private static float Length(Vector2 origin,Vector2 unit,PredictionTerrain terrain)
        {
            Vector2 old=origin;
            for(int distance=8;distance<=2400;distance+=8)
            {Vector2 point=origin+unit*distance;if(!terrain.ProjectilePassage(old.X,old.Y,point.X,point.Y,1,1))return Math.Max(0,distance-32);old=point;}
            return 2400;
        }
    }
}
