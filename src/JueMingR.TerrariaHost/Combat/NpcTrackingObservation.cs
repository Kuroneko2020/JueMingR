using System;
using JueMingR.Platform.Combat;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // Pure scalar observation of native tracking. It never calls TargetClosest
    // on a real NPC, writes targetRect, or borrows the R selected identity.
    internal static class NpcTrackingObservation
    {
        internal static void Capture(NPC n,ref NpcMotionState state,IPredictionTerrain terrain)
        {
            state.TargetCaptured=true;state.PlayerIndex=state.ClosestPlayerIndex=-1;
            if(n.type==620)
            {
                var aimed=n.GetTargetData(true);state.LiquidTargetY=aimed.Center.Y;state.LiquidTargetPlayer=-1;
                if(n.HasValidTarget)
                {
                    if(n.SupportsNPCTargets && n.HasNPCTarget){state.LiquidTargetKind=3;state.LiquidTargetVy=Main.npc[n.TranslatedTargetIndex].velocity.Y;}
                    else{state.LiquidTargetKind=1;state.LiquidTargetPlayer=n.target;}
                }
            }
            // Collision/direct-player reads keep this NPC's current numbered
            // target until its own modeled TargetClosest call changes it.
            if(n.target>=0 && n.target<Main.maxPlayers && Main.player[n.target]!=null)
            {
                var direct=Main.player[n.target];state.PlayerIndex=n.target;state.HasPlayer=true;state.PlayerArea=Area(direct);state.PlayerDead=direct.dead || !direct.active;state.PlayerWet=direct.wet;state.PlayerGraveyard=direct.ZoneGraveyard;state.PlayerIdle=direct.itemAnimation==0 && direct.aggro<0;state.TargetNoAggro=direct.npcTypeNoAggro[n.type];
            }
            if(n.SupportsNPCTargets && n.HasNPCTarget)
            {
                int slot=n.TranslatedTargetIndex;var target=Main.npc[slot];
                if(target!=null && target.active && target.life>0)
                {state.TrackingKind=3;state.TrackingArea=Area(target);state.TrackingVx=target.velocity.X;state.TrackingVy=target.velocity.Y;}
                return;
            }
            float distance=0;bool found=false;int playerSlot=-1,tankSlot=-1;
            // Locked TryTrackingTarget order is significant: a later nearer
            // player clears an earlier guardian candidate, rather than globally
            // minimizing all pets. Only observed, owned live claims are read.
            for(int i=0;i<Main.maxPlayers;i++)
            {
                var p=Main.player[i];if(p==null || !p.active || p.dead || p.ghost)continue;
                float real=Math.Abs(p.Center.X-n.Center.X)+Math.Abs(p.Center.Y-n.Center.Y),score=real-p.aggro;
                if(p.npcTypeNoAggro[n.type] && n.direction!=0)score+=1000;
                if(!found || score<distance){found=true;playerSlot=i;tankSlot=-1;state.TargetChoiceUnknown=false;distance=score;}
                int pet=p.tankPet;if(pet<0 || pet>=Main.maxProjectiles || p.npcTypeNoAggro[n.type])continue;
                var guardian=Main.projectile[pet];if(guardian==null || !guardian.active || guardian.owner!=i)continue;
                float petDistance=Math.Abs(guardian.Center.X-n.Center.X)+Math.Abs(guardian.Center.Y-n.Center.Y)-200;
                if(petDistance>=distance || petDistance>=200)continue;
                bool clear;PredictionStop stop;
                if(!terrain.CanHit(new MotionRect(n.Center.X,n.Center.Y,1,1),new MotionRect(guardian.Center.X,guardian.Center.Y,1,1),out clear,out stop))state.TargetChoiceUnknown=true;
                else if(clear){tankSlot=pet;state.TargetChoiceUnknown=false;}
            }
            if(playerSlot<0)return;var player=Main.player[playerSlot];
            state.ClosestPlayerIndex=playerSlot;state.HasClosestPlayer=true;state.ClosestPlayerArea=Area(player);state.ClosestPlayerDead=player.dead;state.ClosestPlayerWet=player.wet;state.ClosestPlayerGraveyard=player.ZoneGraveyard;state.ClosestPlayerIdle=player.itemAnimation==0 && player.aggro<0;state.ClosestPlayerNoAggro=player.npcTypeNoAggro[n.type];
            state.TrackingKind=1;state.TrackingArea=state.ClosestPlayerArea;
            if(tankSlot>=0)
            {var pet=Main.projectile[tankSlot];state.TrackingKind=2;state.TrackingArea=Area(pet);state.TrackingVx=pet.velocity.X;state.TrackingVy=pet.velocity.Y;}
        }
        private static MotionRect Area(Entity entity){return new MotionRect((int)entity.position.X,(int)entity.position.Y,entity.width,entity.height);}
    }
}
