using System;
using JueMingR.Platform.Combat;
using Terraria;
using Terraria.Utilities;

namespace JueMingR.TerrariaHost.Combat
{
    // Pure scalar observation of native tracking. It never calls TargetClosest
    // on a real NPC, writes targetRect, or borrows the R selected identity.
    internal static class NpcTrackingObservation
    {
        internal static void Capture(NPC n,ref NpcMotionState state,IPredictionTerrain terrain)
        {
            state.TargetCaptured=true;state.PlayerIndex=state.ClosestPlayerIndex=-1;
            if(n.type==139)
            {
                state.MechFactsCaptured=true;state.MechLinkSlot=(int)n.ai[2];
                int queen=NPC.mechQueen;
                // Do not call IsMechQueenUp: its failure path writes the global
                // slot. Only current pure facts are solidified on this thread.
                if(queen>=0 && queen<Main.maxNPCs && Main.npc[queen]!=null && Main.npc[queen].active && Main.npc[queen].type==127)
                {
                    var owner=Main.npc[queen];state.MechQueenIdentity=CombatSelection.Identity(owner,state.Identity.Session);state.MechQueenVx=owner.velocity.X;state.MechQueenVy=owner.velocity.Y;
                    int link=state.MechLinkSlot;if(link<0 || link>=Main.maxNPCs){link=-1;for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i]!=null && Main.npc[i].active && Main.npc[i].type==134){link=i;break;}}
                    state.MechLinkSlot=link;
                    if(link>=0 && link<Main.maxNPCs && Main.npc[link]!=null && Main.npc[link].active && Main.npc[link].type==134)
                    {var anchor=Main.npc[link];state.MechLinkIdentity=CombatSelection.Identity(anchor,state.Identity.Session);state.MechLinkArea=ExactArea(anchor);state.MechLinkVx=anchor.velocity.X;state.MechLinkVy=anchor.velocity.Y;state.MechLinkRotation=anchor.rotation;}
                }
            }
            if(n.type==546 || n.type==425 || n.type==427 || n.type==426)for(int i=0;i<Main.maxNPCs;i++){var other=Main.npc[i];if(i!=n.whoAmI && other!=null && other.active && other.type==n.type && Math.Abs(n.position.X-other.position.X)+Math.Abs(n.position.Y-other.position.Y)<n.width){float push=n.type==425?.15f:n.type==426?.1f:.05f;state.RunPushX+=n.position.X<other.position.X?-push:push;state.RunPushY+=n.position.Y<other.position.Y?-push:push;}}
            if(n.type==410)for(int i=0;i<Main.maxPlayers;i++){var p=Main.player[i];if(p!=null && p.active && !p.dead && p.Distance(n.Center)<800 && p.Center.Y<n.Center.Y && Math.Abs(p.Center.X-n.Center.X)<20){state.RunRetirePlayer=true;break;}}
            if(n.type==210 || n.type==211){Bee(n,ref state);return;}
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
                var direct=Main.player[n.target];state.PlayerIndex=n.target;state.HasPlayer=true;state.PlayerArea=Area(direct);state.PlayerDead=direct.dead || !direct.active;state.PlayerWet=direct.wet;state.PlayerGraveyard=direct.ZoneGraveyard;state.PlayerDesert=direct.ZoneDesert;state.PlayerSandstorm=direct.ZoneSandstorm;state.PlayerIdle=direct.itemAnimation==0 && direct.aggro<0;state.PlayerAttackHidden=direct.stealth==0 && direct.itemAnimation==0;state.TargetNoAggro=direct.npcTypeNoAggro[n.type];
            }
            if(n.SupportsNPCTargets && n.HasNPCTarget)
            {
                int slot=n.TranslatedTargetIndex;var target=Main.npc[slot];
                if(target!=null && target.active && target.life>0)
                {state.TrackingKind=3;state.TrackingArea=Area(target);state.TrackingVx=target.velocity.X;state.TrackingVy=target.velocity.Y;}
                return;
            }
            // AI25's known birth displacement precedes its first real query.
            // Sample that scalar query point without moving the live actor.
            float queryX=n.Center.X+(n.aiStyle==25 && n.ai[3]==0?8:0),queryY=n.Center.Y;
            if(n.type==427 && n.localAI[0]+1+Math.Abs(n.velocity.X)/2>=1200 && Main.netMode!=1 && !Main.getGoodWorld)
            {
                int x=(int)n.Center.X/16-2,y=(int)n.Center.Y/16-3;bool empty=x>=0 && y>=0 && x+4<Main.maxTilesX && y+4<Main.maxTilesY-40;
                for(int tx=x;empty && tx<=x+4;tx++)for(int ty=y;empty && ty<=y+4;ty++){PredictionTile tile;PredictionStop stop;if(!terrain.Tile(tx,ty,out tile,out stop) || tile.Active && tile.Solid && !tile.SolidTop)empty=false;}
                // Transform keeps X/Bottom and chooses after the new size.
                // Its SetDefaults clears confused for this query's facing.
                if(empty){state.FighterFormReady=true;queryX+=5;queryY-=17;}
            }
            float distance=0;bool found=false;int playerSlot=-1,tankSlot=-1;
            // Locked TryTrackingTarget order is significant: a later nearer
            // player clears an earlier guardian candidate, rather than globally
            // minimizing all pets. Only observed, owned live claims are read.
            for(int i=0;i<Main.maxPlayers;i++)
            {
                var p=Main.player[i];if(p==null || !p.active || p.dead || p.ghost)continue;
                float real=Math.Abs(p.Center.X-queryX)+Math.Abs(p.Center.Y-queryY),score=real-p.aggro;
                if(p.npcTypeNoAggro[n.type] && n.direction!=0)score+=1000;
                if(!found || score<distance){found=true;playerSlot=i;tankSlot=-1;state.TargetChoiceUnknown=false;distance=score;}
                int pet=p.tankPet;if(pet<0 || pet>=Main.maxProjectiles || p.npcTypeNoAggro[n.type])continue;
                var guardian=Main.projectile[pet];if(guardian==null || !guardian.active || guardian.owner!=i)continue;
                float petDistance=Math.Abs(guardian.Center.X-queryX)+Math.Abs(guardian.Center.Y-queryY)-200;
                if(petDistance>=distance || petDistance>=200)continue;
                bool clear;PredictionStop stop;
                if(!terrain.CanHit(new MotionRect(queryX,queryY,1,1),new MotionRect(guardian.Center.X,guardian.Center.Y,1,1),out clear,out stop))state.TargetChoiceUnknown=true;
                else if(clear){tankSlot=pet;state.TargetChoiceUnknown=false;}
            }
            if(playerSlot<0)return;var player=Main.player[playerSlot];
            state.ClosestPlayerIndex=playerSlot;state.HasClosestPlayer=true;state.ClosestPlayerArea=Area(player);state.ClosestPlayerDead=player.dead;state.ClosestPlayerWet=player.wet;state.ClosestPlayerGraveyard=player.ZoneGraveyard;state.ClosestPlayerDesert=player.ZoneDesert;state.ClosestPlayerSandstorm=player.ZoneSandstorm;state.ClosestPlayerIdle=player.itemAnimation==0 && player.aggro<0;state.ClosestPlayerAttackHidden=player.stealth==0 && player.itemAnimation==0;state.ClosestPlayerNoAggro=player.npcTypeNoAggro[n.type];
            state.TrackingKind=1;state.TrackingArea=state.ClosestPlayerArea;
            if(tankSlot>=0)
            {var pet=Main.projectile[tankSlot];state.TrackingKind=2;state.TrackingArea=Area(pet);state.TrackingVx=pet.velocity.X;state.TrackingVy=pet.velocity.Y;}
        }
        private static void Bee(NPC n,ref NpcMotionState state)
        {
            // SearchForTarget is a read-only locked native query. Do not call
            // TargetClosestNonBees/FaceTarget on the live actor and roll back.
            // Only its current winner is copied; future steps never rescan.
            var choice=NPCUtils.SearchForTarget(n,NPCUtils.TargetSearchFlag.All,null,NPCUtils.SearchFilters.NonBeeNPCs);
            int target=choice.FoundTarget?choice.NearestTargetIndex:n.target;
            state.BeeTarget=target;state.BeeFacingArea=choice.FoundTarget?new MotionRect(choice.NearestTargetHitbox.X,choice.NearestTargetHitbox.Y,choice.NearestTargetHitbox.Width,choice.NearestTargetHitbox.Height):new MotionRect(n.targetRect.X,n.targetRect.Y,n.targetRect.Width,n.targetRect.Height);
            state.BeeFaceForced=choice.FoundTarget && choice.NearestTargetType!=NPCUtils.TargetType.Player;
            // UpdateNPC copies target to oldTarget BEFORE the coming AI.
            // Evaluate ShouldFaceTarget's pure rule against that future copy;
            // the completed live oldTarget can still refer to the prior action.
            state.BeeFoundTarget=choice.FoundTarget;state.BeeOldTarget=n.target;
            // Preserve only the two actual nearest competitors returned by
            // this same search. Their relative distance can change as the bee
            // moves; holding only today's winner would invent a wrong turn.
            if(choice.FoundNPC)
            {
                var other=choice.NearestNPC;state.BeeHasNpcCandidate=true;
                state.TrackingIdentity=CombatSelection.Identity(other,state.Identity.Session);state.TrackingArea=ExactArea(other);state.TrackingVx=other.velocity.X;state.TrackingVy=other.velocity.Y;
            }
            if(choice.FoundTank)
            {
                var owner=choice.NearestTankOwner;state.ClosestPlayerIndex=choice.NearestTankOwnerIndex;state.HasClosestPlayer=true;
                state.ClosestPlayerArea=Area(owner);state.ClosestPlayerDead=owner.dead;state.ClosestPlayerWet=owner.wet;state.ClosestPlayerGraveyard=owner.ZoneGraveyard;state.ClosestPlayerIdle=owner.itemAnimation==0 && owner.aggro<0;state.ClosestPlayerNoAggro=owner.npcTypeNoAggro[n.type];
                state.BeeTankAggro=owner.aggro;state.TrackingPlayerToken=owner;state.PlayerIndex=state.ClosestPlayerIndex;state.HasPlayer=true;state.PlayerArea=state.ClosestPlayerArea;state.PlayerDead=state.ClosestPlayerDead;state.PlayerWet=state.ClosestPlayerWet;state.PlayerGraveyard=state.ClosestPlayerGraveyard;state.PlayerIdle=state.ClosestPlayerIdle;state.TargetNoAggro=state.ClosestPlayerNoAggro;
                state.BeeTankPet=choice.NearestTankType==NPCUtils.TargetType.TankPet;
                if(state.BeeTankPet){var pet=Main.projectile[owner.tankPet];state.BeeFacingArea=Area(pet);state.BeeFacingVx=pet.velocity.X;state.BeeFacingVy=pet.velocity.Y;}
            }
            if(target>=300 && target<300+Main.maxNPCs)
            {
                var other=Main.npc[target-300];if(other==null || !other.active)return;
                state.TrackingKind=3;if(!state.BeeHasNpcCandidate){state.TrackingIdentity=CombatSelection.Identity(other,state.Identity.Session);state.TrackingArea=ExactArea(other);state.TrackingVx=other.velocity.X;state.TrackingVy=other.velocity.Y;}state.BeeTargetValid=true;return;
            }
            if(target<0 || target>=255)return;
            var player=Main.player[target];if(player==null || !player.active || player.dead || player.ghost)return;
            // GetTargetData() ignores the guardian by default. Its numbered
            // owner supplies motion, even when guardian geometry supplies face.
            state.TrackingKind=1;state.TrackingPlayerToken=player;state.BeeTargetValid=true;state.PlayerIndex=target;state.HasPlayer=true;state.PlayerArea=Area(player);state.PlayerDead=player.dead;state.PlayerWet=player.wet;state.PlayerGraveyard=player.ZoneGraveyard;state.PlayerIdle=player.itemAnimation==0 && player.aggro<0;state.TargetNoAggro=player.npcTypeNoAggro[n.type];
        }
        private static MotionRect ExactArea(Entity entity){return new MotionRect(entity.position.X,entity.position.Y,entity.width,entity.height);}
        private static MotionRect Area(Entity entity){return new MotionRect((int)entity.position.X,(int)entity.position.Y,entity.width,entity.height);}
    }
}
