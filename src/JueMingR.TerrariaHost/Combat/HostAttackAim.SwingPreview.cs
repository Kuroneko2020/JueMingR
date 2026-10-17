using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackAim
    {
        private AttackContact PrepareMixedSwingPreview(PreviewPlan plan,Player player,Item item,NpcTrajectory timeline,HostAttackPhase phase,int delay)
        {
            // The native hand can still strike during a projectile cooldown.
            // Its current animation and the next legal shot have independent
            // clocks. Only the shot's solver can mint a cursor receipt; this
            // earlier hand contact is exclusively an observation fact.
            AttackContact hand=player.itemAnimation>0?swing.Preview(player,item,timeline,new HostAttackClock(timeline,phase),plan.Terrain):null;
            if(delay<0 || plan.Ammo==null){plan.Contact=hand;return hand;}
            var body=player;var motion=plan.PlayerMotion;
            var environment=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,Remix=Main.remixWorld,Expert=Main.expertMode,WallViewWidth=Main.MaxWorldViewSize.X};
            NpcPredictionSource.TryBindPlayerGravity(timeline,(long)Main.GameUpdateCount,ref motion,ref environment);
            NpcPredictionSource.TryBindPlayerWall(timeline,(long)Main.GameUpdateCount,ref motion,ref environment);
            if(delay>0)
            {
                for(int i=0;i<delay;i++){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,environment,plan.Terrain,out stop)){plan.Contact=hand;return hand;}}
                body=(Player)previewCopy.Invoke(player,null);HostAttackPlayerFrame.ApplyPose(body,motion);body.itemAnimation=body.itemTime=body.reuseDelay=0;
            }
            if(player.itemAnimation<=0)hand=AttackIntercept.WithLaunchDelay(swing.PreviewPrepared(body,item,timeline,new HostAttackClock(timeline,phase,delay),plan.Terrain,new HostAttackPlayerFrame(motion,environment)),delay);
            if(phase==HostAttackPhase.CompletedWorld)
            {
                PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,environment,plan.Terrain,out stop)){plan.Contact=hand;return hand;}
                if(ReferenceEquals(body,player))body=(Player)previewCopy.Invoke(player,null);
                HostAttackPlayerFrame.ApplyPose(body,motion);
            }
            var shot=AttackIntercept.WithLaunchDelay(SolveOnTerrain(body,item,plan.Ammo,timeline,HostAttackModels.LaunchOrigin(body,item),new HostAttackClock(timeline,phase,delay).Age,phase==HostAttackPhase.BeforeNpc,plan.Terrain,delay,out plan.Environment,out plan.Obstacles,new HostAttackPlayerFrame(motion,environment)),delay);
            plan.Contact=Earlier(hand,shot)?hand:shot;return plan.Contact;
        }
    }
}
