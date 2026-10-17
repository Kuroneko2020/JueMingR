using System;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // MagicalHarp chooses one of three real bodies. Cursor distance controls
    // both its launch speed and the finite deceleration; shootSpeed is not
    // the emitted velocity. Future type/jitter remain legal representatives.
    internal static class HostNoteAttack
    {
        private static readonly MethodInfo clone=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
        internal static bool Handles(int type){return type>=76 && type<=78;}
        internal static Projectile Copy(Projectile p)
        {var q=(Projectile)clone.Invoke(p,null);q.ai=(float[])p.ai.Clone();q.localAI=(float[])p.localAI.Clone();q.localNPCImmunity=(int[])p.localNPCImmunity.Clone();return q;}
        private static void Kill(Projectile p){p.active=false;p.timeLeft=0;}
        internal static Vector2 RawCursor(Player player,Vector2 aim)
        {var screen=aim-Main.screenPosition;if(player.gravDir==-1)screen.Y=Main.screenHeight-screen.Y;return new Vector2((int)screen.X,(int)screen.Y)+Main.screenPosition;}
        internal static Projectile Birth(Player player,int type,int damage,Vector2 origin,Vector2 aim,float viewAxis)
        {
            var raw=RawCursor(player,aim);var logical=raw;if(player.gravDir==-1)logical.Y=Main.screenPosition.Y+Main.screenHeight-(raw.Y-Main.screenPosition.Y);
            float pitch=(logical-origin).Length()/(viewAxis/2);if(pitch>1)pitch=1;pitch*=1.75f;pitch=pitch*2-1;pitch=MathHelper.Clamp(pitch,-1,1);
            // Zero radial jitter is within the native disk. The actual born
            // flight uses its captured ai2 and never redraws this random value.
            var offset=raw-player.Center;float duration=Utils.Remap(offset.Length(),540,960,20,60);var velocity=offset/duration;velocity*=2;
            var p=new Projectile();p.SetDefaults(type);p.active=true;p.owner=player.whoAmI;p.Center=origin;p.velocity=velocity;p.damage=damage;p.ai[0]=pitch;p.ai[1]=1;p.ai[2]=velocity.Length()/duration;return p;
        }
        internal static bool Step(Projectile p,PredictionTerrain terrain)
        {
            if(!p.active)return true;
            if(p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld || float.IsNaN(p.position.X) || float.IsNaN(p.position.Y)){p.active=false;return true;}
            if(!Handles(p.type) || p.aiStyle!=21 || !p.ignoreWater || p.correctSlopeCollision || p.ownerHitCheck || p.usesOwnerMeleeHitCD)return false;
            p.rotation=Utils.Clamp(p.velocity.X,-6,6)*.1f;p.spriteDirection=-p.direction;
            float threshold=3*Math.Abs(p.ai[0]/2+.5f);if(p.velocity.Length()>threshold)p.velocity=p.velocity.ToRotation().ToRotationVector2()*(p.velocity.Length()-p.ai[2]);
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(p.owner<255 && !Main.player[p.owner].active)Kill(p);
            // ignoreWater preserves a birth's sticky Shimmer flag. Native
            // AI21 still retires in a full pool or reflects downwards at its
            // surface, even though ordinary water never slows the note.
            if(p.shimmerWet)
            {
                int x=(int)(p.Center.X/16),y=(int)(p.position.Y/16);bool full=false;
                if(WorldGen.InWorld(x,y) && WorldGen.InWorld(x,y-1)){PredictionTile tile,above;PredictionStop stop;if(!terrain.Tile(x,y,out tile,out stop))return false;if(tile.Liquid==255 && tile.ShimmerLiquid){if(!terrain.Tile(x,y-1,out above,out stop))return false;full=above.Liquid>0 && above.ShimmerLiquid;}}
                if(full)Kill(p);else if(p.velocity.Y>0){p.velocity.Y=-p.velocity.Y;p.wet=p.shimmerWet=false;}
            }
            // A wall reflects the post-AI velocity without spending penetrate.
            var previous=p.velocity;var position=p.position;var velocity=p.velocity;Vector2 wet;bool hit=false;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,false,false,out wet,out hit,true,p.shimmerWet,true))return false;
            if(hit){if(velocity.X!=previous.X)velocity.X=-previous.X;if(velocity.Y!=previous.Y)velocity.Y=-previous.Y;}
            p.position=position+velocity;p.velocity=velocity;p.direction=velocity.X<0?-1:1;return true;
        }
        internal static void Finish(Projectile p)
        {if(p.active && (--p.timeLeft<=0 || p.penetrate==0))Kill(p);}
        internal static Rectangle DamageBox(Projectile p)
        {var box=p.Hitbox;box.Inflate(10,10);return box;}
        private static AttackContact Replay(Projectile source,NpcTrajectory path,int first,int outerFirst,PredictionTerrain terrain,HostAttackObstacles root,Vector2 aim,AttackConfidence confidence)
        {
            var p=Copy(source);root.BeginCandidate();var receive=root.ForkForSpawn(p);
            for(int step=0;step<Math.Min(120,path.Count-first) && p.active;step++)
            {
                int tick=first+step,outer=outerFirst+step;if(!receive.BeforeNpc(tick,outer,p.Center.X,p.Center.Y,p.width,p.height))return null;
                int updates=p.extraUpdates+1;
                for(int sub=0;sub<updates && p.active;sub++)
                {
                    if(!Step(p,terrain))return null;if(!p.active)break;var box=DamageBox(p);int hits=receive.SelectedHits;
                    bool passed=receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,b=>box.Intersects(new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height)),consumeSelected:true);
                    if(receive.SelectedHits>hits)return AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,box.Center.X,box.Center.Y,box.Width,box.Height,confidence);
                    p.penetrate=receive.Remaining;if(!passed)return null;Finish(p);
                }
            }
            return null;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            if(ammo.WeaponType!=494 || !Handles(ammo.Projectile) || ammo.Damage<=0 || age+1>=path.Count || ammo.NoteViewAxis<=0)return null;
            obstacles.CaptureShotFacing(player);var aims=new Vector2[41];int count=0;
            for(int seed=0;seed<41;seed++)
            {
                var b=path[Math.Min(path.Count-1,age+1+Math.Min(seed,8)*4)].ProjectileReceiveBounds;
                var raw=seed<9?new Vector2(b.CenterX,b.CenterY):origin+((seed-9)*MathHelper.TwoPi/32).ToRotationVector2()*600;
                var aim=raw;if(player.gravDir==-1)aim.Y=2*Main.screenPosition.Y+Main.screenHeight-raw.Y;
                bool duplicate=false;for(int i=0;i<count;i++)if(aims[i]==aim){duplicate=true;break;}if(duplicate)continue;aims[count++]=aim;
                for(int type=76;type<=78;type++)
                {
                    var p=Birth(player,type,ammo.Damage,origin,aim,ammo.NoteViewAxis);bool wet;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true))return null;p.honeyWet=wet && kind==2;p.shimmerWet=wet && kind==3;
                    var contact=Replay(p,path,age+1,delay+1,terrain,obstacles,aim,AttackConfidence.Representative);if(contact!=null)return contact;
                }
            }
            return null;
        }
        internal static AttackContact Known(Player player,Projectile p,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            if(p==null || !p.active || !Handles(p.type) || p.owner!=player.whoAmI || p.damage<=0 || !p.friendly || p.hostile || clock.LaunchDelay!=0)return null;
            obstacles.CaptureProjectileFamily(new[]{p});return Replay(p,path,clock.FirstTick,1,terrain,obstacles,p.Center+p.velocity*100,AttackConfidence.Conditional);
        }
    }
}
