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
        internal static Vector2 ActorPoint(Player player,NPC target,long session,NpcPredictionSource prediction,ref NpcTrajectory timeline,ref PredictionTerrain.CellLease terrain,bool capture,long sampleTick,ref PredictionPlayerMotion? playerPremise,ref uint playerTick)
        {
            var item=player.HeldItem;var basic=target.Center+target.netOffset;
            if(item==null || item.IsAir || item.shoot<=0)return basic;
            var identity=CombatSelection.Identity(target,session);
            bool continuing=false;
            try{
            if(capture)
            {
                prediction.CompletedPlayerBoundary=player.whoAmI;prediction.Cache.Demand(1,1,60);
                // Primary and secondary packets received at T are consumed by
                // the next real player phase T+1, before NPCs move. The sample
                // epoch is T, not today's player clock (which would give -1).
                prediction.PrepareActionPrefix(identity,sampleTick,60,60);timeline=prediction.Cache.Read(1);terrain=null;
                playerPremise=timeline?.PlayerCondition.Kind==PredictionPlayerConditionKind.HeldControls?(PredictionPlayerMotion?)timeline.PlayerCondition.Sample:null;
                if(playerPremise.HasValue)playerTick=Main.GameUpdateCount-(playerPremise.Value.PlayerIndex>player.whoAmI?1U:0U);
            }
            else
            {
                if(timeline==null || !timeline.Identity.Equals(identity) || terrain==null || !terrain.Current)return basic;
                var condition=timeline.PlayerCondition;
                if(condition!=null && condition.Kind!=PredictionPlayerConditionKind.None)
                {
                    var old=condition.Sample;int slot=old.PlayerIndex;var body=slot>=0 && slot<Main.maxPlayers?Main.player[slot]:null;
                    if(body==null || !ReferenceEquals(body,old.PlayerToken) || !condition.Alive)return basic;
                    var observed=NpcPredictionSource.ReadPlayer(body);
                    if(condition.Kind!=PredictionPlayerConditionKind.HeldControls || !playerPremise.HasValue)return basic;
                    var expected=playerPremise.Value;uint current=Main.GameUpdateCount-(slot>player.whoAmI?1U:0U);
                    if(current<playerTick || current-playerTick>timeline.Count)return basic;
                    var world=HostAttackPlayerFrame.Capture(body,timeline).Environment;
                    // Continue the sampled ordinary player, not today's pose
                    // relabeled into an old NPC phase. Complex source-bound
                    // transitions await a fresh coherent publication locally.
                    if(current!=playerTick && (expected.GravitySourceExpected || expected.WallSourceExpected || timeline.GravitySources!=null || timeline.WallSources!=null))return basic;
                    continuing=prediction.Terrain.BeginContinuation(terrain);if(!continuing)return basic;
                    for(uint tick=playerTick;tick<current;tick++)
                    {
                        PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref expected,world,prediction.Terrain,out stop))return basic;
                        if(expected.WallImmunityCaptured && expected.WallImmune && --expected.WallImmuneTime<=0)expected.WallImmune=false;
                    }
                    if(!RetainedNextMotionCurrent(expected,observed,timeline,world))return basic;
                    playerPremise=expected;playerTick=current;
                }
                if(!continuing){continuing=prediction.Terrain.BeginContinuation(terrain);if(!continuing)return basic;}
            }
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
