using System;
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
        internal static Vector2 ActorPoint(Player player,NPC target,long session,NpcPredictionSource prediction,ref NpcTrajectory timeline,ref PredictionTerrain.CellLease terrain,bool capture,long sampleTick,ref PredictionPlayerMotion? playerPremise,ref uint playerTick,ref uint playerAnchor,Projectile controlled,out HostAttackSolution result)
        {
            result=ActorSolution(player,player.HeldItem,target,session,prediction,ref timeline,ref terrain,capture,sampleTick,ref playerPremise,ref playerTick,ref playerAnchor,controlled);
            return result.HasPoint && result.Reachability==HostAttackReachability.Reachable?result.Point:target.Center+target.netOffset;
        }

        internal static bool ActorDirectLine(PredictionTerrain terrain,Vector2 from,Vector2 to)
        {
            terrain.Reset();
            bool clear;PredictionStop stop;
            var source=new MotionRect(from.X,from.Y,0,0);var target=new MotionRect(to.X,to.Y,0,0);
            if(!terrain.CanHitLine(source,target,out clear,out stop))return false;
            // Small receivers stand partly within CanHitLine's adjacent tile
            // strip. The native center trace can still prove a basic opening
            // there without mistaking unloaded or solid center cells for air.
            return clear || terrain.CanHit(source,target,out clear,out stop) && clear;
        }

        internal static HostAttackSolution ActorSolution(Player player,Item item,NPC target,long session,NpcPredictionSource prediction,ref NpcTrajectory timeline,ref PredictionTerrain.CellLease terrain,bool capture,long sampleTick,ref PredictionPlayerMotion? playerPremise,ref uint playerTick,ref uint playerAnchor,Projectile controlled=null)
        {
            if(item==null || item.IsAir || item.shoot<=0)return default(HostAttackSolution);
            var identity=CombatSelection.Identity(target,session);
            AttackAmmoSnapshot captured=null;
            bool continuing=false;
            try{
            if(capture)
            {
                // Use the normal flight demand for a fresh attached hand.
                // A slow bone javelin can arrive past the old fixed 60-frame
                // window. This only changes the desired prefix under the
                // existing shared actor allowance.
                if(HostAttachedShotAttack.Handles(item.shoot))captured=AttackAmmoSnapshot.Capture(player,item);
                int horizon=captured!=null && HostAttachedShotAttack.Handles(captured.Projectile)?BalancedFlightPreferred(player,item,captured,target):60;
                prediction.CompletedPlayerBoundary=player.whoAmI;prediction.Cache.Demand(1,1,horizon);
                // Primary and secondary packets received at T are consumed by
                // the next real player phase T+1, before NPCs move. The sample
                // epoch is T, not today's player clock (which would give -1).
                prediction.PrepareActionPrefix(identity,sampleTick,horizon,horizon);timeline=prediction.Cache.Read(1);terrain=null;
                playerPremise=timeline?.PlayerCondition.Kind==PredictionPlayerConditionKind.HeldControls?(PredictionPlayerMotion?)timeline.PlayerCondition.Sample:null;
                if(playerPremise.HasValue)playerAnchor=playerTick=Main.GameUpdateCount-(playerPremise.Value.PlayerIndex>player.whoAmI?1U:0U);
            }
            else
            {
                if(timeline==null || !timeline.Identity.Equals(identity) || terrain==null || !terrain.Current)return default(HostAttackSolution);
                var condition=timeline.PlayerCondition;
                if(condition!=null && condition.Kind!=PredictionPlayerConditionKind.None)
                {
                    var old=condition.Sample;int slot=old.PlayerIndex;var body=slot>=0 && slot<Main.maxPlayers?Main.player[slot]:null;
                    if(body==null || !ReferenceEquals(body,old.PlayerToken) || !condition.Alive)return default(HostAttackSolution);
                    var observed=NpcPredictionSource.ReadPlayer(body);
                    if(condition.Kind!=PredictionPlayerConditionKind.HeldControls || !playerPremise.HasValue)return default(HostAttackSolution);
                    var expected=playerPremise.Value;uint current=Main.GameUpdateCount-(slot>player.whoAmI?1U:0U);
                    if(current<playerTick || current-playerTick>timeline.Count)return default(HostAttackSolution);
                    var world=HostAttackPlayerFrame.Capture(body,timeline).Environment;
                    // Continue the sampled ordinary player, not today's pose
                    // relabeled into an old NPC phase. Complex source-bound
                    // transitions await a fresh coherent publication locally.
                    if(current!=playerTick && (expected.GravitySourceExpected || expected.WallSourceExpected || timeline.GravitySources!=null || timeline.WallSources!=null))return default(HostAttackSolution);
                    continuing=prediction.Terrain.BeginContinuation(terrain);if(!continuing)return default(HostAttackSolution);
                    for(uint tick=playerTick;tick<current;tick++)
                    {
                        PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref expected,world,prediction.Terrain,out stop))return default(HostAttackSolution);
                        if(expected.WallImmunityCaptured && expected.WallImmune && --expected.WallImmuneTime<=0)expected.WallImmune=false;
                    }
                    // ReadPlayer has physical facts but no trajectory ordinal.
                    // Bind only this comparison copy to the actual accumulated
                    // ordinary player actions since capture, not NPC age or the
                    // mutable last-success tick. All physical/source inputs are
                    // still compared by the existing fixed-input predicate.
                    if(timeline.GravitySources==null && !expected.GravitySourceExpected && expected.ForcedGravityTicks==0)
                    {
                        if(current<playerAnchor || current-playerAnchor>timeline.Count || observed.GravitySourceExpected || observed.ForcedGravityTicks>0)return default(HostAttackSolution);
                        if(observed.GravityControlCaptured)observed.GravityAction=condition.Sample.GravityAction+(int)(current-playerAnchor);
                    }
                    if(!RetainedNextMotionCurrent(expected,observed,timeline,world))return default(HostAttackSolution);
                    playerPremise=expected;playerTick=current;
                }
                if(!continuing){continuing=prediction.Terrain.BeginContinuation(terrain);if(!continuing)return default(HostAttackSolution);}
            }
            if(timeline==null || timeline.Count<2)return default(HostAttackSolution);
            var clock=new HostAttackClock(timeline,HostAttackPhase.BeforeNpc);
            int age=clock.Age;
            if(age<0 || age>=timeline.Count-1)return default(HostAttackSolution);
            if(!capture)
            {
                var point=timeline[age];
                if(Vector2.DistanceSquared(target.position,new Vector2(point.Bounds.X,point.Bounds.Y))>4 || Vector2.DistanceSquared(target.velocity,new Vector2(point.Vx,point.Vy))>.01f)return default(HostAttackSolution);
            }
            if(captured==null)captured=AttackAmmoSnapshot.Capture(player,item);
            if(captured==null)return default(HostAttackSolution);
            var start=HostAttackModels.LaunchOrigin(player,item);
            HostAttackSolution result;
            // This dispatch matches the human hand: ordinary actions replay
            // representative reachability, while specialist mechanisms use
            // their actual Contact solver. The player's mutable HostAttackAim
            // settings, cursor, and candidate selection never enter this path.
            if(BalancedHeld(item))
            {
                var sample=controlled;
                if(sample==null){sample=new Projectile();sample.SetDefaults(item.shoot);sample.owner=player.whoAmI;sample.scale=1;}
                var ammo=AttackAmmoSnapshot.CaptureController(player,item,item.shoot==705?12:14);
                result=HostHeldAttack.PrepareRepresentative(player,sample,ammo,timeline,clock,prediction.Terrain,HostAttackPlayerFrame.Capture(player,timeline));
            }
            else if(controlled!=null && HostHeldAttack.Handles(controlled.type))
                result=HostAttackSolution.FromContact(HostHeldAttack.Solve(player,controlled,timeline,clock,prediction.Terrain,out _,out _,out _));
            else if(BalancedHand(player,item,captured))
                result=SolveBalancedHand(player,item,captured,timeline,start,clock,prediction.Terrain,out _);
            else
                result=HostAttackSolution.FromContact(SolveOnTerrain(player,item,captured,timeline,start,age,true,prediction.Terrain,0,out _,out _));
            terrain=prediction.Terrain.FreezeCells();
            return result;
            }finally{if(continuing)prediction.Terrain.EndContinuation();}
        }
    }
}
