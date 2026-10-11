using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // Locked .8 AI159 consumes WindForVisuals even when windPhysics is false.
    // The random values only choose clocks, but those clocks eventually change
    // the motor. Observed clocks stay exact; a future draw is a legal finite
    // representative, never a prediction of live RNG. Kill has no extra Damage.
    internal static class HostPaperAirplaneAttack
    {
        internal readonly struct State
        {
            internal readonly float Wind;
            internal readonly double Visual;
            private readonly uint tick;
            internal State(bool capture){Wind=Main.windSpeedCurrent;Visual=Main.timeForVisualEffects;tick=Main.GameUpdateCount;}
            internal bool Current
            {
                get
                {
                    uint age=unchecked(Main.GameUpdateCount-tick);
                    // Paused/menu visual updates or a different wind observation
                    // revoke this lease. Do not infer time from Main.time or TL.
                    if(age>120 || Wind!=Main.windSpeedCurrent)return false;
                    if(age==0)return Visual==Main.timeForVisualEffects;
                    // Native initialized visual time is integral; a nonstandard
                    // clock gets no cross-update waiver. Presentation is O(1).
                    return Visual>=0 && Visual<216000 && Visual==Math.Truncate(Visual) && (Visual+age)%216000==Main.timeForVisualEffects;
                }
            }
        }
        internal static bool Handles(int type){return type==761 || type==762;}
        internal static Projectile Copy(Projectile p){return HostNoteAttack.Copy(p);}
        internal static double At(double visual,int actions)
        {
            // Main adds one before each real world action and resets at 216000.
            // Preserve its reset (not modulo) for a nonstandard captured value.
            if(actions>=0 && visual>=0 && visual<216000 && visual==Math.Truncate(visual))return (visual+(double)actions)%216000;
            for(int i=0;i<actions;i++){visual+=1;if(visual>=216000)visual=0;}return visual;
        }
        private static void Kill(Projectile p){if(p.active){p.active=false;p.timeLeft=0;}}
        internal static bool Ai(Projectile p,float wind,double visual,int initial,int reset,int turn,out bool drew)
        {
            drew=false;if(!Handles(p.type) || p.aiStyle!=159 || !p.manualDirectionChange || float.IsNaN(wind) || float.IsInfinity(wind) || double.IsNaN(visual) || double.IsInfinity(visual))return false;
            if(initial<30 || initial>=80 || reset<120 || reset>=600 || turn<120 || turn>=300)return false;
            if(p.ai[1]==0)
            {
                p.direction=p.velocity.X>0?1:-1;p.rotation=p.velocity.ToRotation();p.ai[1]=1;p.ai[0]=-initial;drew=true;
            }
            if(p.wet && p.owner==Main.myPlayer)Kill(p);
            p.ai[0]+=1;
            Vector2 vector=p.rotation.ToRotationVector2()*8f;
            float y=(float)Math.Sin((float)Math.PI*2f*(float)(visual%90.0/90.0))*(float)p.direction*wind;
            Vector2 v=vector+new Vector2(wind,y);
            bool aligned=p.direction==Math.Sign(wind) && p.velocity.Length()>3f;
            bool looping=p.ai[0]>=20 && p.ai[0]<=69;
            if(p.ai[0]==70){p.ai[0]=-reset;drew=true;}
            bool circle=looping && aligned;
            if(circle){float blend=Utils.GetLerpValue(0,30,p.ai[0],true);v=vector.RotatedBy((float)(-p.direction)*((float)Math.PI*2f)*.02f*blend);}
            p.velocity=v.SafeNormalize(Vector2.UnitY)*p.velocity.Length();
            if(!circle)
            {
                float bob=MathHelper.Lerp(.15f,.05f,Math.Abs(wind));
                if(p.timeLeft%40<20)p.velocity.Y-=bob;else p.velocity.Y+=bob;
                if(p.velocity.Y< -2)p.velocity.Y=-2;if(p.velocity.Y>2)p.velocity.Y=2;
                p.velocity.X=MathHelper.Clamp(p.velocity.X+wind*.006f,-6,6);
                if(p.velocity.X*p.oldVelocity.X<0){p.direction*=-1;p.ai[0]=-turn;drew=true;}
            }
            p.rotation=p.velocity.ToRotation();p.spriteDirection=p.direction;return true;
        }
        internal static bool Step(Projectile p,PredictionTerrain terrain,float wind,double visual,int initial,int reset,int turn,out bool drew)
        {
            drew=false;if(!p.active)return true;
            if(p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld || float.IsNaN(p.position.X) || float.IsNaN(p.position.Y)){p.active=false;return true;}
            if(p.correctSlopeCollision || p.ownerHitCheck || p.usesOwnerMeleeHitCD || p.extraUpdates<0 || p.extraUpdates>16 || p.owner<0 || p.owner>=Main.player.Length)return false;
            p.oldVelocity=p.velocity;if(!Ai(p,wind,visual,initial,reset,turn,out drew))return false;
            float impulse;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out impulse))return false;p.velocity.X+=impulse;
            if(p.owner<255 && !Main.player[p.owner].active)Kill(p);
            if(!p.ignoreWater)
            {
                bool wet,lava;byte kind;
                if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;
                if(lava)p.lavaWet=true;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;
                if(p.wet && !wet && p.wetCount==0)p.wetCount=10;p.wet=wet;
                if(!wet)p.lavaWet=p.honeyWet=p.shimmerWet=false;if(p.wetCount>0)p.wetCount--;
            }
            // Shimmer() has no AI159 branch. Its wet flag still participates
            // in the ordinary wet movement; do not borrow AI33 bounce/kill.
            // Update continues HandleMovement after an AI wet-owner Kill. Its
            // pre-AI wetVelocity survives when tileCollide is false. There is
            // no automatically changed direction for manualDirectionChange.
            p.oldPosition=p.position;p.oldDirection=p.direction;
            var position=p.position;var velocity=p.velocity;var wetVelocity=p.oldVelocity;bool hit=false;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;
            p.position=position;p.velocity=velocity;
            if(hit){p.position+=p.velocity;Kill(p);}
            p.position+=p.wet?wetVelocity:p.velocity;return true;
        }
        internal static void Finish(Projectile p){if(p.active && (--p.timeLeft<=0 || p.penetrate==0))Kill(p);}
        internal static Projectile Birth(Player player,AttackAmmoSnapshot ammo,Vector2 origin,Vector2 aim,PredictionTerrain terrain)
        {
            if(!Handles(ammo.Projectile) || ammo.WeaponType!=4343 && ammo.WeaponType!=4344 || ammo.Damage<=0)return null;
            var p=new Projectile();p.SetDefaults(ammo.Projectile);p.active=true;p.owner=player.whoAmI;p.Center=origin;p.velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);p.damage=ammo.Damage;
            bool wet;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true))return null;p.wet=wet;p.honeyWet=wet && kind==2;p.shimmerWet=wet && kind==3;return p;
        }
        private static AttackContact Replay(Projectile source,NpcTrajectory path,int first,int outerFirst,int visualFirst,State state,PredictionTerrain terrain,HostAttackObstacles root,Vector2 aim,int representative)
        {
            var p=Copy(source);root.BeginCandidate();var receive=root.ForkForSpawn(p);bool random=false;
            int initial=representative==0?30:representative==1?55:79,reset=representative==0?120:representative==1?360:599,turn=representative==0?120:representative==1?210:299;
            for(int step=0;step<Math.Min(120,path.Count-first) && p.active;step++)
            {
                int tick=first+step,outer=outerFirst+step;if(!receive.BeforeNpc(tick,outer,p.Center.X,p.Center.Y,p.width,p.height))return null;
                double visual=At(state.Visual,visualFirst+step);
                for(int sub=0;sub<p.extraUpdates+1 && p.active;sub++)
                {
                    bool drew;if(!Step(p,terrain,state.Wind,visual,initial,reset,turn,out drew) || !p.active || p.damage<=0)return null;random|=drew;
                    int hits=receive.SelectedHits;bool passed=receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,unlimited:p.penetrate<0,consumeSelected:true);
                    if(receive.SelectedHits>hits)return AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,random?AttackConfidence.Representative:AttackConfidence.Conditional);
                    p.penetrate=receive.Remaining;if(!passed)return null;Finish(p);
                }
            }
            return null;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay)
        {
            if(!Handles(ammo.Projectile) || ammo.WeaponType!=4343 && ammo.WeaponType!=4344 || age+1>=path.Count || delay<0)return null;
            var state=new State(true);root.CapturePaperClock(state);root.CaptureShotFacing(player);root.CaptureCursorMapping(player);
            var directions=new Vector2[41];int count=0;
            for(int seed=0;seed<41;seed++)
            {
                var box=path[Math.Min(path.Count-1,age+1+Math.Min(seed,8)*4)].ProjectileReceiveBounds;
                Vector2 direction=seed<9?(new Vector2(box.CenterX,box.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();
                bool duplicate=false;for(int i=0;i<count;i++)if(directions[i]==direction){duplicate=true;break;}if(duplicate)continue;directions[count++]=direction;
                var aim=origin+direction*10000;var p=Birth(player,ammo,origin,aim,terrain);if(p==null)return null;
                for(int choice=0;choice<3;choice++){var contact=Replay(p,path,age+1,delay+1,delay+(beforeNpc?0:1),state,terrain,root,aim,choice);if(contact!=null)return contact;}
            }
            return null;
        }
        internal static AttackContact Known(Player player,Projectile p,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(p==null || !p.active || !Handles(p.type) || p.owner!=player.whoAmI || !p.friendly || p.hostile || p.damage<=0 || clock.LaunchDelay!=0)return null;
            var state=new State(true);root.CapturePaperClock(state);root.CaptureProjectileFamily(new[]{p});
            for(int choice=0;choice<3;choice++){var contact=Replay(p,path,clock.FirstTick,1,clock.NextWorld?1:0,state,terrain,root,p.Center+p.velocity*1000,choice);if(contact!=null)return contact;}return null;
        }
    }
}
