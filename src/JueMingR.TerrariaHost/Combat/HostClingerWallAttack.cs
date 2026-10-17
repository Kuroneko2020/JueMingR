using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
namespace JueMingR.TerrariaHost.Combat
{
    // Native Clinger's MouseWorld placement ray and floor/ceiling scan create an
    // actual variable-height body. It has no later cursor consumer.
    internal static class HostClingerWallAttack
    {
        internal static bool Handles(int type){return type==482;}
        internal static bool Placement(int item){return item==3014;}
        private static bool Tile(PredictionTerrain terrain,int x,int y,out PredictionTile tile)
        {PredictionStop stop;return terrain.Tile(x,y,out tile,out stop);}
        private static bool Solid(PredictionTile tile){return tile.Active && tile.Solid && !tile.SolidTop && !tile.Half && tile.Slope==0;}
        internal static bool Birth(Player player,Vector2 origin,Vector2 aim,float speed,PredictionTerrain terrain,out Projectile shot)
        {
            shot=null;if(!(speed>0))return false;
            // Main.MouseWorld already reverses gravity. Unlike the cloud
            // weapons' explicit mouseX/Y stores, both this destination and
            // initial velocity consume that same logical world point. Retain
            // the integer screen cursor boundary before native LimitPoint.
            var screen=aim-Main.screenPosition;if(player.gravDir==-1)screen.Y=Main.screenHeight-screen.Y;
            var raw=new Vector2((int)screen.X+Main.screenPosition.X,player.gravDir==-1?Main.screenPosition.Y+Main.screenHeight-(int)screen.Y:Main.screenPosition.Y+(int)screen.Y);
            player.LimitPointToPlayerReachableArea(ref raw);
            var velocity=HostThornAttack.BirthVelocity(player,origin,aim,speed);var position=origin;
            while(true)
            {
                CombatPredictionWork.Check();bool clear;PredictionStop stop;
                if(!terrain.CanHitLine(new MotionRect(player.position.X,player.position.Y,player.width,player.height),new MotionRect(position.X,position.Y,1,1),out clear,out stop))return false;
                if(!clear)break;position+=velocity;
                if(Vector2.Distance(position,raw)<20+Math.Abs(velocity.X)+Math.Abs(velocity.Y)){position=raw;break;}
            }
            int x=(int)position.X/16,start=(int)position.Y/16,down=start;PredictionTile tile;
            while(down<Main.maxTilesY-10 && down-start<30)
            {if(!Tile(terrain,x,down,out tile))return false;if(Solid(tile) || tile.Platform || tile.Type==380)break;down++;}
            if(!Tile(terrain,x,down,out tile))return false;if(!Solid(tile) && !tile.Platform && tile.Type!=380)return true;
            int up=start;
            while(up>10 && start-up<30){if(!Tile(terrain,x,up,out tile))return false;if(Solid(tile))break;up--;}
            int bottom=down*16,top=up*16+16,height=Math.Min(240,bottom-top);top=bottom-height;
            shot=new Projectile();shot.SetDefaults(482);shot.active=true;shot.owner=player.whoAmI;shot.Center=new Vector2((int)(position.X/16)*16,position.Y);shot.velocity=Vector2.Zero;shot.ai[0]=top;shot.ai[1]=height;return true;
        }
        internal static void Ai(Projectile shot,Projectile[] family,Vector2 owner)
        {
            shot.position.Y=shot.ai[0];shot.height=(int)shot.ai[1];shot.direction=shot.Center.X>owner.X?1:-1;shot.velocity.X=shot.direction*.000001f;
            if(shot.owner==Main.myPlayer)foreach(var other in family)
                if(other!=shot && other.active && other.type==482 && other.owner==shot.owner && other.timeLeft>shot.timeLeft){shot.active=false;break;}
        }
        private sealed class Member {internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source;}
        private static Projectile[] Capture(Player player,HostAttackObstacles root)
        {var list=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && p.type==482)list.Add(p);var scene=list.ToArray();root.CaptureOwnedProjectileFamily(scene,player.whoAmI,new[]{482});return scene;}
        private static List<Member> Prepare(Projectile[] scene,Projectile[] sources,HostAttackObstacles root)
        {
            var rows=new List<Member>();foreach(var p in scene){bool source=false;if(sources!=null)foreach(var q in sources)if(ReferenceEquals(p,q)){source=true;break;}var copy=HostRaySpellAttack.Copy(p);rows.Add(new Member{Shot=copy,Receive=root.ForkForSpawn(copy),Source=source});}return rows;
        }
        private static Projectile[] Members(List<Member> rows)
        {var family=new Projectile[rows.Count];for(int i=0;i<rows.Count;i++)family[i]=rows[i].Shot;return family;}
        private static bool Outer(List<Member> rows,Projectile[] family,HostProjectileBirthSlots slots,NpcTrajectory path,PredictionTerrain terrain,int tick,int outer,Vector2 aim,Vector2 owner,out AttackContact contact)
        {
            contact=null;
            foreach(var row in rows)
            {
                var p=row.Shot;if(!p.active)continue;if(!row.Receive.BeforeNpc(tick,outer,p.Center.X,p.Center.Y,p.width,p.height))return false;
                if(HostRaySpellAttack.WorldRetired(p)){p.active=false;slots.Release(p.whoAmI);continue;}
                // Do not kill every old wall up front. The native earlier slot
                // has already spent timeLeft when a later slot compares it;
                // a one-clock age difference can therefore preserve both walls.
                Ai(p,family,owner);if(!p.active){slots.Release(p.whoAmI);continue;}
                float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;p.position+=p.velocity;
                if(p.damage>0 && p.height>0)
                {
                    int before=row.Receive.SelectedHits;bool passed=row.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true);p.penetrate=row.Receive.Remaining;
                    if(row.Source && row.Receive.SelectedHits>before){contact=AttackIntercept.BodyContact(path,tick,0,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,AttackConfidence.Conditional);return true;}
                    if(!passed && p.penetrate!=0)return false;
                }
                if(--p.timeLeft<=0 || p.penetrate==0){p.active=false;slots.Release(p.whoAmI);}
            }
            return true;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay)
        {
            if(!Handles(ammo.Projectile) || age-delay<0 || age+1>=path.Count)return null;root.CaptureShotFacing(player);var scene=Capture(player,root);var pool=root.CaptureBirthSlots();var aims=new List<Vector2>();
            for(int seed=0;seed<17;seed++)
            {
                var b=path[Math.Min(path.Count-1,age+1+seed*4)].ProjectileReceiveBounds;var aim=new Vector2(b.CenterX,b.CenterY);if(aims.Contains(aim))continue;aims.Add(aim);
                Projectile source;if(!Birth(player,origin,aim,ammo.Speed,terrain,out source) || source==null)continue;
                root.BeginCandidate();var slots=pool.Copy();var rows=Prepare(scene,null,root);var family=Members(rows);bool valid=true;
                for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,family,slots,path,terrain,age-delay+wait,wait,aim,player.Center,out ignored)){valid=false;break;}}if(!valid)continue;
                int slot;if(!slots.TryAllocate(out slot))return null;source.whoAmI=slot;source.damage=ammo.Damage;source.knockBack=player.GetWeaponKnockback(player.HeldItem,player.HeldItem.knockBack);
                rows.Add(new Member{Shot=source,Receive=root.ForkForSpawn(source),Source=true});rows.Sort((a,c)=>a.Shot.whoAmI.CompareTo(c.Shot.whoAmI));family=Members(rows);
                for(int step=1;step<=Math.Min(120,path.Count-age-1);step++){AttackContact result;if(!Outer(rows,family,slots,path,terrain,age+step,delay+step,aim,player.Center,out result))break;if(result!=null)return result;}
            }
            return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(sources==null || sources.Length==0 || clock.LaunchDelay!=0 || clock.Phase==HostAttackPhase.Projectiles)return null;
            foreach(var p in sources)if(p==null || !p.active || p.type!=482 || p.owner!=player.whoAmI)return null;
            var scene=Capture(player,root);var pool=root.CaptureBirthSlots();root.BeginCandidate();var rows=Prepare(scene,sources,root);var family=Members(rows);var slots=pool.Copy();var aim=sources[0].Center;
            for(int step=0;step<Math.Min(120,path.Count-clock.FirstTick);step++){AttackContact result;if(!Outer(rows,family,slots,path,terrain,clock.FirstTick+step,step+1,aim,player.Center,out result))break;if(result!=null)return result;}return null;
        }
    }
}
