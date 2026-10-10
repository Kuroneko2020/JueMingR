using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackAim
    {
        private void PreparePhasebladeRelease(Player player,Item item,NpcTrajectory timeline,HostAttackPhase phase)
        {
            // A previous real press owns this release opportunity. Merely
            // holding the item grants no input permission and starts no use.
            // Native ItemCheck remains the sole authority that actually fires.
            if(player.itemAnimation<=0 || player.itemTime>1)return;
            var captured=AttackAmmoSnapshot.Capture(player,item);if(captured==null)return;
            var clock=new HostAttackClock(timeline,phase);
            if(clock.Age<0 || clock.Age>1)return;
            var sample=ContentSamples.ProjectilesByType[captured.Projectile];
            obstacles=new HostAttackObstacles(player,sample,timeline,0,clock.BeforeNpc,clock.NextWorld);
            origin=HostAttackModels.LaunchOrigin(player,item);
            current=HostPhasebladeAttack.ReleaseSolve(player,captured,timeline,origin,clock,terrain,obstacles);next=null;
            weapon=item;slot=player.selectedItem;type=item.type;prefix=item.prefix;session=observation.Session;prepared=Main.GameUpdateCount;ammo=captured;
        }
        private AttackContact PreparePhasebladePreview(PreviewPlan plan,Player player,Item item,NpcTrajectory timeline,HostAttackPhase phase,int delay)
        {
            if(player.itemAnimation>0 && plan.Using)
            {
                // Holding continues this actual swing. Its future release time
                // has not been chosen, so do not invent a thrown projectile.
                plan.Contact=swing.Preview(player,item,timeline,new HostAttackClock(timeline,phase),plan.Terrain);
                return plan.Contact;
            }
            bool idle=player.itemAnimation<=0;
            if(!idle && (player.itemAnimation<=1 || player.itemTime>1))return null;
            AttackContact tap=null;
            if(idle)
            {
                var body=player;var motion=plan.PlayerMotion;
                var world=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,Remix=Main.remixWorld,Expert=Main.expertMode,WallViewWidth=Main.MaxWorldViewSize.X};
                NpcPredictionSource.TryBindPlayerGravity(timeline,(long)Main.GameUpdateCount,ref motion,ref world);
                NpcPredictionSource.TryBindPlayerWall(timeline,(long)Main.GameUpdateCount,ref motion,ref world);
                if(delay>0)
                {
                    for(int i=0;i<delay;i++){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,world,plan.Terrain,out stop))return null;}
                    body=(Player)previewCopy.Invoke(player,null);HostAttackPlayerFrame.ApplyPose(body,motion);
                }
                // The idle hypothesis is one complete press update, then a
                // release update. Only that first melee pose belongs to it;
                // a full swing and immediate throw cannot occur together.
                tap=AttackIntercept.WithLaunchDelay(swing.PreviewPrepared(body,item,timeline,new HostAttackClock(timeline,phase,delay),plan.Terrain,new HostAttackPlayerFrame(motion,world),1),delay);
            }
            int launchDelay=idle?delay+1:0;
            var clock=new HostAttackClock(timeline,phase,launchDelay);
            var sample=ContentSamples.ProjectilesByType[plan.Ammo.Projectile];
            plan.Obstacles=new HostAttackObstacles(player,sample,timeline,launchDelay,clock.BeforeNpc,clock.NextWorld);
            var release=AttackIntercept.WithLaunchDelay(HostPhasebladeAttack.ReleaseSolve(player,plan.Ammo,timeline,HostAttackModels.LaunchOrigin(player,item),clock,plan.Terrain,plan.Obstacles),launchDelay);
            plan.Contact=Earlier(tap,release)?tap:release;return plan.Contact;
        }
    }
}
