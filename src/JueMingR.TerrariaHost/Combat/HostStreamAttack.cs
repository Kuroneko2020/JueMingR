using System;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Utilities;

namespace JueMingR.TerrariaHost.Combat
{
    // AI012 water/ichor streams retain their full Damage body while scale
    // shrinks. Water's lava branch uses the birth's private ai2 seed, then
    // changes penetration and the NEXT outer's subupdate count. It neither
    // draws from Main.rand nor changes an already-born autonomous shot.
    internal static class HostStreamAttack
    {
        private static readonly MethodInfo clone=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
        internal static bool Handles(int type){return type==22 || type==280;}
        internal static Projectile Copy(Projectile p)
        {
            var q=(Projectile)clone.Invoke(p,null);q.ai=(float[])p.ai.Clone();q.localAI=(float[])p.localAI.Clone();q.localNPCImmunity=(int[])p.localNPCImmunity.Clone();return q;
        }
        private static void Kill(Projectile q){q.active=false;q.timeLeft=0;}
        internal static bool Step(Projectile q,PredictionTerrain terrain)
        {
            if(!q.active)return true;
            if(q.position.X<=Main.leftWorld || q.position.X+q.width>=Main.rightWorld || q.position.Y<=Main.topWorld || q.position.Y+q.height>=Main.bottomWorld || float.IsNaN(q.position.X) || float.IsNaN(q.position.Y))
            {q.active=false;return true;}
            if(!Handles(q.type) || q.aiStyle!=12 || !q.ignoreWater || q.correctSlopeCollision || q.ownerHitCheck || q.usesOwnerMeleeHitCD)return false;
            float shrink=.002f,gravity=.075f;
            if(q.type==22)
            {
                shrink=.01f;gravity=.15f;
                if(q.ai[1]==1){gravity/=2;shrink=0;q.extraUpdates=0;q.penetrate=-1;}
                if(q.velocity.Y>0 && q.ai[1]==0)
                {
                    int x=(int)q.Center.X/16,y=(int)q.Center.Y/16;
                    if(WorldGen.InWorld(x,y))
                    {
                        PredictionTile tile;PredictionStop stop;if(!terrain.Tile(x,y,out tile,out stop))return false;
                        // Native checks the lava flag even when liquid==0.
                        if(tile.Lava)
                        {
                            var random=new LCG32Random((uint)q.ai[2]);q.velocity.Y=random.NextFloat()*-4f-4f;q.velocity.X=(random.NextFloat()-.5f)*5f;q.ai[1]=1;
                        }
                    }
                }
            }
            q.scale-=shrink;if(q.scale<=0)Kill(q);
            if(q.ai[0]>3)q.velocity.Y+=gravity;else q.ai[0]++;
            float wind;if(!HostProjectileEnvironment.Impulse(q,terrain,q.Center.X,q.Center.Y,q.velocity.X,out wind))return false;q.velocity.X+=wind;
            if(q.owner<255 && !Main.player[q.owner].active)Kill(q);
            // Both defaults ignore water. Native Shimmer has no AI012 branch;
            // a birth's sticky honey/shimmer flags do not invent a transform.
            var position=q.position;var velocity=q.velocity;Vector2 wet;bool hit=false;
            if(q.tileCollide && !terrain.ProjectileTileMotion(q,ref position,ref velocity,q.wet,q.honeyWet,out wet,out hit,true,q.shimmerWet,true))return false;
            q.position=position;q.velocity=velocity;
            if(hit){q.position+=velocity;Kill(q);}
            q.position+=velocity;q.direction=q.velocity.X<0?-1:1;return true;
        }
        internal static void Finish(Projectile q)
        {if(q.active && (--q.timeLeft<=0 || q.penetrate==0))Kill(q);}
        private static AttackContact Replay(Projectile source,NpcTrajectory path,int first,int firstOuter,PredictionTerrain terrain,HostAttackObstacles root,Vector2 aim,AttackConfidence confidence)
        {
            var q=Copy(source);root.BeginCandidate();var receive=root.ForkForSpawn(q);
            for(int step=0;step<Math.Min(120,path.Count-first) && q.active;step++)
            {
                int tick=first+step,outer=firstOuter+step;
                if(!receive.BeforeNpc(tick,outer,q.Center.X,q.Center.Y,q.width,q.height))return null;
                // Update latches numUpdates once. AI changing extraUpdates
                // must not truncate the remaining subactions of this outer.
                int updates=q.extraUpdates+1;
                for(int sub=0;sub<updates && q.active;sub++)
                {
                    if(!Step(q,terrain))return null;if(!q.active)break;
                    int hits=receive.SelectedHits;
                    bool passed=receive.PassCustomOuter(2,tick,outer,q.Center.X,q.Center.Y,q.width,q.height,null,unlimited:q.penetrate<0,consumeSelected:true);
                    if(receive.SelectedHits>hits)return AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,q.Center.X,q.Center.Y,q.width,q.height,confidence);
                    if(q.penetrate>=0)q.penetrate=receive.Remaining;
                    if(!passed)return null;Finish(q);
                }
            }
            return null;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            if(!Handles(ammo.Projectile) || age+1>=path.Count || ammo.Damage<=0)return null;
            obstacles.CaptureShotFacing(player);var directions=new Vector2[41];int count=0;
            for(int seed=0;seed<41;seed++)
            {
                var b=path[Math.Min(path.Count-1,age+1+Math.Min(seed,8)*4)].ProjectileReceiveBounds;
                var direction=seed<9?(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();
                bool duplicate=false;for(int i=0;i<count;i++)if(directions[i]==direction){duplicate=true;break;}if(duplicate)continue;directions[count++]=direction;
                var aim=origin+direction*10000;var q=new Projectile();q.SetDefaults(ammo.Projectile);q.active=true;q.owner=player.whoAmI;q.Center=origin;q.velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);q.damage=ammo.Damage;
                // Zero is a legal ItemCheck_Shoot seed. Before a future lava
                // decision the path is deterministic; that decision remains
                // a representative, never a promise of the unseen RNG draw.
                q.ai[2]=0;
                var contact=Replay(q,path,age+1,delay+1,terrain,obstacles,aim,ammo.Projectile==22?AttackConfidence.Representative:AttackConfidence.Conditional);
                if(contact!=null)return contact;
            }
            return null;
        }
        internal static AttackContact Known(Player player,Projectile q,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            if(q==null || !q.active || !Handles(q.type) || q.owner!=player.whoAmI || !q.friendly || q.hostile || q.damage<=0 || clock.LaunchDelay!=0)return null;
            obstacles.CaptureProjectileFamily(new[]{q});return Replay(q,path,clock.FirstTick,1,terrain,obstacles,q.Center+q.velocity*1000,AttackConfidence.Conditional);
        }
    }
}
