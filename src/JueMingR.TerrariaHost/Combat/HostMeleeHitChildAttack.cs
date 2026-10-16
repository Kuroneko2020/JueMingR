using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // These are the weapon's own finite NPC-hit offspring. KnownFamily starts
    // AFTER native Player has chosen a birth; HostMeleeHitFamilyAttack reuses
    // these exact motors for the current hand's finite pre-birth continuation.
    // Neither grants children cursor input. Item1320 is
    // another native 1111 caller, but remains a tool rather than a Combat hand.
    internal static class HostMeleeHitChildAttack
    {
        internal static bool Family(int type){return type==1111 || type==524 || type==977 || type==978;}
        internal static bool Weapon(int type){return type==1166 || type==3211 || type==155 || type==121;}
        private static int Style(int type){return type==1111?2:type==524?96:type==977?152:type==978?189:-1;}
        private static void Kill(Projectile p){p.active=false;p.timeLeft=0;}
        internal static bool Ai(Projectile p)
        {
            if(!Family(p.type) || p.aiStyle!=Style(p.type))return false;
            if(p.type==1111)
            {
                // AI002 has an unconditional wind term IN ADDITION to the
                // normal Update wind gate, even inside walls/below surface.
                if(Main.windPhysics)p.velocity.X+=Main.windSpeedCurrent*Main.windPhysicsStrength;
                p.rotation+=(Math.Abs(p.velocity.X)+Math.Abs(p.velocity.Y))*.03f*p.direction;
                p.ai[0]++;if(p.ai[0]>=20){p.velocity.Y+=.4f;p.velocity.X*=.97f;}if(p.velocity.Y>32)p.velocity.Y=32;
            }
            else if(p.type==524)
            {p.ai[0]+=.6f;if(p.ai[0]>500)Kill(p);p.velocity.Y+=.008f;}
            else if(p.type==977)
            {
                p.alpha=Math.Max(100,p.alpha-10);
                if(p.ai[0]!=0)p.velocity=p.velocity.RotatedBy(p.ai[0]/(10*p.MaxUpdates));
                p.velocity*=.96f;p.rotation=p.velocity.ToRotation()+MathHelper.PiOver2;p.tileCollide=false;
            }
            else
            {
                p.alpha=(int)(255*(1-Utils.Remap(p.ai[0],0,2,0,1)*Utils.Remap(p.ai[0],6,10,1,0)));
                p.scale=1+p.ai[0]/15;
                if(++p.frameCounter>=2){p.frameCounter=0;if(++p.frame>=5){Kill(p);return true;}}
                p.ai[0]++;
            }
            return true;
        }
        internal static bool Tile(Projectile p,Vector2 prior)
        {
            if(p.type==524)
            {
                p.ai[0]+=100;if(p.velocity.X!=prior.X)p.velocity.X=-prior.X;if(p.velocity.Y!=prior.Y)p.velocity.Y=-prior.Y;return true;
            }
            if(!Family(p.type))return false;
            p.position+=p.velocity;Kill(p);return true;
        }
        internal static bool Step(Projectile p,PredictionTerrain terrain)
        {
            if(HostRaySpellAttack.WorldRetired(p)){Kill(p);return true;}
            var wetVelocity=p.velocity;if(!Ai(p))return false;
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(p.owner<255 && !Main.player[p.owner].active)Kill(p);
            if(!p.ignoreWater)
            {
                bool wet,lava;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;
                if(lava)p.lavaWet=true;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;
                if(p.wet!=wet && p.wetCount==0)p.wetCount=10;p.wet=wet;if(!wet)p.honeyWet=p.lavaWet=p.shimmerWet=false;if(p.wetCount>0)p.wetCount--;
            }
            if(p.shimmerWet)
            {
                if(p.type==1111){p.velocity.Y-=.7f;p.timeLeft-=20;}
                else if(p.type==977)
                {
                    int x=(int)(p.Center.X/16),y=(int)(p.position.Y/16);bool full=false;
                    if(WorldGen.InWorld(x,y) && WorldGen.InWorld(x,y-1)){PredictionTile cell,above;PredictionStop stop;if(!terrain.Tile(x,y,out cell,out stop))return false;if(cell.Liquid==255 && cell.ShimmerLiquid){if(!terrain.Tile(x,y-1,out above,out stop))return false;full=above.Liquid>0 && above.ShimmerLiquid;}}
                    if(full)Kill(p);else if(p.velocity.Y>0){p.velocity.Y=-p.velocity.Y;p.shimmerWet=p.wet=false;}
                }
                // Native Shimmer has no AI096/189 movement branch.
            }
            var prior=p.velocity;var position=p.position;var velocity=p.velocity;bool hit=false;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;
            p.position=position;p.velocity=velocity;if(hit && !Tile(p,prior))return false;
            // Native still completes this displacement after an AI/tile Kill.
            // None of these four Kill paths creates a damaging descendant.
            p.position+=p.wet?wetVelocity:p.velocity;if(!p.manualDirectionChange)p.direction=p.velocity.X<0?-1:1;return true;
        }
        internal static bool Collides(Projectile p,MotionRect bounds)
        {return p.Hitbox.Intersects(new Rectangle((int)bounds.X,(int)bounds.Y,(int)bounds.Width,(int)bounds.Height));}
        private sealed class Member{internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source;}
        internal static AttackContact KnownFamily(Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(sources==null || sources.Length==0 || clock.Phase==HostAttackPhase.Projectiles || clock.Age<0 || clock.LaunchDelay!=0)return null;
            var scene=new List<Projectile>();bool ownerCheck=false;
            foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && Family(p.type)){scene.Add(p);ownerCheck|=p.ownerHitCheck || p.usesOwnerMeleeHitCD;}
            root.CaptureOwnedProjectileFamily(scene.ToArray(),player.whoAmI,new[]{1111,524,977,978});root.BeginCandidate();var rows=new List<Member>();
            foreach(var p in scene){var q=HostNoteAttack.Copy(p);rows.Add(new Member{Shot=q,Receive=root.ForkForSpawn(q),Source=Array.IndexOf(sources,p)>=0});}
            var body=HostMeleeAttack.PlayerCopy(player);HostAttackPlayerPrefix owners=null;
            if(ownerCheck){root.CaptureArcOwner(player,player.HeldItem);root.CaptureHomingInputs(player,false,true);owners=new HostAttackPlayerPrefix(HostAttackPlayerFrame.Capture(player,path),terrain,121);}
            var aim=sources[0].Center+sources[0].velocity*1000;
            for(int action=0;action<Math.Min(120,path.Count-clock.FirstTick);action++)
            {
                int tick=clock.FirstTick+action,outer=action+1;
                if(owners!=null){PredictionPlayerMotion pose;if(!owners.TryRead(action+(clock.NextWorld?1:0),out pose))return null;HostAttackPlayerFrame.ApplyPose(body,pose);}
                foreach(var m in rows)if(m.Shot.active && !m.Receive.BeforeNpc(tick,outer,m.Shot.Center.X,m.Shot.Center.Y,m.Shot.width,m.Shot.height))return null;
                foreach(var m in rows){var p=m.Shot;for(int sub=0;sub<=p.extraUpdates && p.active;sub++)
                {
                    CombatPredictionWork.Check();if(!Step(p,terrain))return null;if(!p.active)break;
                    int hits=m.Receive.SelectedHits;
                    bool pass=m.Receive.PassArcOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,b=>Collides(p,b),(b,noTile)=>HostMeleeAttack.OwnerAllows(body,p,b,noTile,terrain),0,true,(slot,remaining)=>{if(p.type==524)p.ai[0]+=50;return true;});p.penetrate=m.Receive.Remaining;
                    if(m.Source && m.Receive.SelectedHits>hits)return AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,AttackConfidence.Conditional);
                    if(!pass && p.penetrate!=0)return null;if(!pass || --p.timeLeft<=0)Kill(p);
                }}
            }
            return null;
        }
    }
}
