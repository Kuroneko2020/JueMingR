using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // ItemCheck_Shoot owns the real batch and random spread. Aim preparation
    // reads one legal main birth, preserving the cursor's native meaning.
    internal static class HostNativeArrowBirthAttack
    {
        internal static bool Weapon(int type){return type==1229 || type==2624 || type==3029 || type==4953;}
        internal static int EventideStage(Player player,int delay)
        {int duration=player.itemAnimationMax>0?player.itemAnimationMax:HostMeleeAttack.AnimationDuration(player,player.HeldItem);int animation=player.itemAnimation-delay;return player.itemAnimation<=0 || animation<=0?0:(duration-animation)/2;}
        private static Vector2 Cursor(Player player,int weapon,Vector2 point)
        {
            // Match the existing actual input exit: sky3029 borrows RAW
            // screen coordinates; the other callers borrow world coordinates.
            if(weapon!=3029)return HostDirectionalControl.Cursor(player,point);
            var screen=point-Main.screenPosition;return new Vector2((int)screen.X,(int)screen.Y)+Main.screenPosition;
        }
        internal static int Facing(Player player,Vector2 origin,Vector2 nativeCursor,bool raw)
        {
            // ChangeDir always reads Main.MouseWorld. Its input is already
            // cursor-quantized: world for three callers, raw for sky3029.
            if(raw && player.gravDir==-1)nativeCursor.Y=Main.screenPosition.Y+Main.screenHeight-(nativeCursor.Y-Main.screenPosition.Y);
            var unit=(nativeCursor-origin).SafeNormalize(Vector2.Zero);
            return Vector2.Dot(Vector2.UnitX.RotatedBy(player.fullRotation),unit)>0?1:-1;
        }
        internal static Projectile[] Births(Player player,AttackAmmoSnapshot ammo,Vector2 origin,Vector2 aim,int stage,PredictionTerrain terrain)
        {
            int weapon=ammo.WeaponType;
            int count=weapon==2624?5:weapon==3029?3:weapon==1229?2:1;var born=new Projectile[count];
            for(int ordinal=0;ordinal<count;ordinal++)
            {
                born[ordinal]=Birth(player,ammo,origin,aim,stage,terrain,ordinal);if(born[ordinal]==null)return null;
            }
            return born;
        }
        private static Projectile Birth(Player player,AttackAmmoSnapshot ammo,Vector2 origin,Vector2 aim,int stage,PredictionTerrain terrain,int ordinal)
        {
                var input=aim;int weapon=ammo.WeaponType;aim=Cursor(player,weapon,aim);int type=weapon==4953 && stage==2?932:ammo.Projectile;
                Vector2 center=origin,velocity=(aim-origin).SafeNormalize(-Vector2.UnitY)*ammo.Speed;
                if(weapon==4953)
                {if(!HostHomingEffectAttack.EventideBirth(player,origin,input,ammo.Speed,stage,terrain,out center,out velocity))return null;}
                else if(weapon==2624)
                {
                    var offset=velocity.SafeNormalize(-Vector2.UnitY)*40;bool clear;PredictionStop stop;
                    if(!terrain.CanHit(new MotionRect(origin.X,origin.Y,0,0),new MotionRect(origin.X+offset.X,origin.Y+offset.Y,0,0),out clear,out stop))return null;
                    var shift=offset.RotatedBy(MathHelper.Pi/10*(ordinal-2));if(!clear)shift-=offset;center=origin+shift;
                    var end=center-velocity;if(!terrain.CanHitLine(new MotionRect(player.MountedCenter.X,player.MountedCenter.Y,4,4),new MotionRect(end.X,end.Y,0,0),out clear,out stop))return null;
                    if(!clear)center-=(center-player.Center).SafeNormalize(Vector2.Zero)*15;
                }
                else if(weapon==3029)
                {
                    // Legal native draws: Next(3)=1 (three for either count
                    // class), Next(201)=100, all symmetric jitter=0, X gain=100.
                    // Every member keeps its native 150px vertical ordinal.
                    // ChangeDir reads gravity-aware Main.MouseWorld even here;
                    // the later sky vector below separately rereads RAW mouseY.
                    float x=aim.X+player.width*.5f-100*Facing(player,origin,aim,true);
                    center=new Vector2((x*10+player.Center.X)/11,player.MountedCenter.Y-600-150*ordinal);
                    var delta=aim-center;delta.Y=Math.Max(20,Math.Abs(delta.Y));velocity=delta.SafeNormalize(Vector2.UnitY)*ammo.Speed;
                }
                // 1229 chooses the legal two-arrow branch and zero jitter for
                // the second arrow. They remain two distinct slots/history.
                var p=HostNoteAttack.Copy(ContentSamples.ProjectilesByType[type]);p.active=true;p.owner=player.whoAmI;p.whoAmI=-1;p.Center=center;p.velocity=velocity;p.damage=ammo.Damage*(weapon==4953 && stage==2?2:1);p.knockBack=ammo.NativeKnockBack;p.noDropItem=weapon!=4953 || type!=932;
                if(type==932){p.velocity=p.velocity.SafeNormalize(Vector2.Zero)*(ammo.Speed*2);p.ai[1]=player.miscCounterNormalized*12%1;}
                else if(p.aiStyle==1)while(p.velocity.X>=16 || p.velocity.X<=-16 || p.velocity.Y>=16 || p.velocity.Y< -16)p.velocity*=.97f;
                return p;
        }
        internal static int PreferredRange(Player player,AttackAmmoSnapshot ammo,NPC selected)
        {
            // The hand's mounted point is not Daedalus's native sky birth.
            // A 16-action source cannot reach an ordinary receiver from -600px;
            // replaying every direction against that prefix exhausted work
            // before the owner could request the next prefix. Schedule useful
            // NPC coverage first, without proving a hit or removing expansion.
            var sample=ContentSamples.ProjectilesByType[ammo.Projectile];
            var origin=HostAttackModels.LaunchOrigin(player,player.HeldItem);
            if(ammo.WeaponType==3029)
            {
                // The native sky X is cursor-relative too. Keeping hand X
                // over-requested long NPC prefixes for a distant horizontal
                // receiver although its representative arrow starts nearby.
                // This is the same legal midpoint as Births, used only as a
                // desired supply hint; it proves neither reach nor a miss.
                var cursor=Cursor(player,ammo.WeaponType,selected.Center+selected.netOffset);
                float x=cursor.X+player.width*.5f-100*Facing(player,origin,cursor,true);
                origin=new Vector2((x*10+player.Center.X)/11,player.MountedCenter.Y-600);
            }
            float speed=ammo.Speed;if(ammo.WeaponType==4953 && (ammo.Projectile==932 || EventideStage(player,0)==2))speed*=2;
            if(sample.aiStyle==1)while(speed>=16)speed*=.97f;
            int updates=sample.extraUpdates+1;if(ammo.ExtraArrowUpdates && sample.arrow)updates=Math.Max(2,updates);
            float closing=Math.Max(1,speed*updates-selected.velocity.Length());
            return Math.Min(NpcPredictionCache.Horizon,Math.Max(16,(int)Math.Ceiling(Vector2.Distance(origin,selected.Center+selected.netOffset)/closing)+8));
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root,bool launchOriginAdvanced=false)
        {return Prepare(player,ammo,path,origin,clock,terrain,root,launchOriginAdvanced).Contact;}
        internal static HostAttackSolution Prepare(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root,bool launchOriginAdvanced=false)
        {
            if(ammo==null || !Weapon(ammo.WeaponType) || clock.FirstTick<0 || clock.FirstTick>=path.Count)return default(HostAttackSolution);
            int stage=EventideStage(player,clock.LaunchDelay+(clock.NextWorld?1:0));
            var owners=new HostAttackPlayerPrefix(HostAttackPlayerFrame.Capture(Main.player[player.whoAmI],path),terrain,clock.LaunchDelay+2);
            PredictionPlayerMotion motion;if(!owners.TryRead(clock.LaunchDelay+(clock.NextWorld?1:0),out motion))return default(HostAttackSolution);
            var launch=HostMeleeAttack.PlayerCopy(player);HostAttackPlayerFrame.ApplyPose(launch,motion);
            // Preview and the explicit next-hand branch already supply a future
            // origin. Their scene prefix still advances, but not that origin twice.
            var launchOrigin=launchOriginAdvanced?origin:HostAttackModels.LaunchOrigin(launch,launch.HeldItem)+(origin-HostAttackModels.LaunchOrigin(player,player.HeldItem));
            if(ammo.WeaponType==3029)return Sky(launch,ammo,path,launchOrigin,clock,terrain,stage);
            int ordinal=ammo.WeaponType==2624?2:0;
            var result=HostRepresentativeAttack.Prepare(launch,ammo,path,launchOrigin,clock,terrain,aim=>Birth(launch,ammo,launchOrigin,aim,stage,terrain,ordinal));
            if(result.Reachability==HostAttackReachability.Reachable || ammo.WeaponType!=2624)return result.WithSemantics(AttackPointSemantics.Direction);
            // The centre owns the normal direction. Only its failed passage
            // pays for the four remaining native offsets, sharing terrain.
            foreach(int side in new[]{0,4,1,3})
            {
                int member=side;var fallback=HostRepresentativeAttack.Prepare(launch,ammo,path,launchOrigin,clock,terrain,aim=>Birth(launch,ammo,launchOrigin,aim,stage,terrain,member));
                if(fallback.Reachability==HostAttackReachability.Reachable)return fallback.WithSemantics(AttackPointSemantics.Direction);
            }
            return result.WithSemantics(AttackPointSemantics.Direction);
        }
        private static HostAttackSolution Sky(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,HostAttackClock clock,PredictionTerrain terrain,int stage)
        {
            // RAW cursor controls the landing region and sky X. A distant
            // direction point would move that region thousands of pixels.
            // Iterate only the representative arrival time; native scatter
            // and the other vertical ordinals remain with ItemCheck_Shoot.
            int tick=clock.FirstTick;var initial=path[tick].ProjectileReceiveBounds;var aim=new Vector2(initial.CenterX,initial.CenterY);HostAttackSolution best=new HostAttackSolution(path,aim,false,HostAttackReachability.Unknown,pointSemantics:AttackPointSemantics.WorldPoint);
            HostProjectileTargets targets=null;
            for(int pass=0;pass<4;pass++)
            {
                var p=Birth(player,ammo,origin,aim,stage,terrain,0);if(p==null)return best;
                // Ammo can retain its autonomous steering after a sky birth.
                // Capture that same finite chase premise as ordinary main
                // replay once, rather than treating a missing required input
                // as an unknown trajectory or reading live targets in Replay.
                if(pass==0 && HostAmmoSteeringAttack.Handles(p.type))targets=HostProjectileTargets.Capture(p,path.Identity.Session);
                AttackMotion motion;if(!HostRepresentativeAttack.TryMotion(ammo,out motion))return best;
                int receive;float nearest;var reach=HostRepresentativeAttack.Replay(player,p,path,clock.FirstTick,terrain,motion,out receive,out nearest,targets);
                if(reach==HostAttackReachability.Reachable)return new HostAttackSolution(path,aim,true,reach,receive,pointSemantics:AttackPointSemantics.WorldPoint);
                // Closest future centre is supplied by a free flight of this
                // same legal sky birth. That gives a bounded fixed point for
                // a moving target without changing RAW coordinate semantics.
                p=Birth(player,ammo,origin,aim,stage,terrain,0);float x=p.Center.X,y=p.Center.Y,vx=p.velocity.X,vy=p.velocity.Y;int chosen=tick;float distance=float.MaxValue;
                for(int k=1;k<=Math.Min(motion.Lifetime,motion.SubupdatesForOuters(path.Count-clock.FirstTick));k++)
                {
                    motion.Advance(ref x,ref y,ref vx,ref vy,k);int t=clock.FirstTick+motion.OuterAtSubupdate(k)-1;var b=path[t].ProjectileReceiveBounds;float d=Math.Abs(y-b.CenterY);
                    if(d<distance){distance=d;chosen=t;}
                }
                var target=path[chosen].ProjectileReceiveBounds;aim=new Vector2(target.CenterX,target.CenterY);tick=chosen;best=new HostAttackSolution(path,aim,true,reach,pointSemantics:AttackPointSemantics.WorldPoint);
            }
            return best;
        }
    }
}
