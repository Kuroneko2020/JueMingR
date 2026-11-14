using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using JueMingR.Platform.Combat;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackAim
    {
        // This is the same explicit-player weapon/terrain solver as the human
        // hand. Selection, budget and retained action remain the actor owner's.
        internal static Vector2 ActorPoint(Player player,NPC target,long session,NpcPredictionSource prediction,ref NpcTrajectory timeline,ref PredictionTerrain.CellLease terrain,bool capture)
        {
            var item=player.HeldItem;var basic=target.Center+target.netOffset;
            if(item==null || item.IsAir || item.shoot<=0)return basic;
            var identity=CombatSelection.Identity(target,session);
            bool continuing=false;
            if(capture)
            {
                prediction.CompletedPlayerBoundary=player.whoAmI;prediction.Cache.Demand(1,1,60);
                prediction.PrepareActionPrefix(identity,Main.GameUpdateCount,60,60);timeline=prediction.Cache.Read(1);terrain=null;
            }
            else
            {
                if(timeline==null || !timeline.Identity.Equals(identity) || terrain==null || !terrain.Current)return basic;
                var condition=timeline.PlayerCondition;
                if(condition!=null && condition.Kind!=PredictionPlayerConditionKind.None)
                {
                    var old=condition.Sample;int slot=old.PlayerIndex;var body=slot>=0 && slot<Main.maxPlayers?Main.player[slot]:null;
                    if(body==null || !ReferenceEquals(body,old.PlayerToken) || !condition.Alive || !NpcPredictionSource.ReadPlayer(body).Equals(old))return basic;
                }
                continuing=prediction.Terrain.BeginContinuation(terrain);if(!continuing)return basic;
            }
            try{
            if(timeline==null || timeline.Count<2)return basic;
            int age=new HostAttackClock(timeline,HostAttackPhase.BeforeNpc).Age;
            if(age<0 || age>=timeline.Count-1)return basic;
            if(!capture)
            {
                var point=timeline[age];
                if(Vector2.DistanceSquared(target.position,new Vector2(point.Bounds.X,point.Bounds.Y))>4 || Vector2.DistanceSquared(target.velocity,new Vector2(point.Vx,point.Vy))>.01f)return basic;
            }
            var captured=AttackAmmoSnapshot.Capture(player,item);if(captured==null)return basic;
            HostProjectileEnvironment environment;HostAttackObstacles obstacles;
            var contact=SolveOnTerrain(player,item,captured,timeline,HostAttackModels.LaunchOrigin(player,item),age,true,prediction.Terrain,0,out environment,out obstacles);
            terrain=prediction.Terrain.FreezeCells();
            return contact==null?basic:new Vector2(contact.AimX,contact.AimY);
            }finally{if(continuing)prediction.Terrain.EndContinuation();}
        }
    }
}
