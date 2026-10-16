using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.Combat
{
    internal static class HostDirectionalStormAttack
    {
        private static bool Solid(Vector2 point,PredictionTerrain terrain,out bool solid)
        {PredictionTile tile;PredictionStop stop;solid=false;var cell=point.ToTileCoordinates();if(!WorldGen.InWorld(cell))return true;if(!terrain.Tile(cell.X,cell.Y,out tile,out stop))return false;solid=tile.Active && tile.Solid && !tile.SolidTop && !tile.Platform;return true;}
        internal static bool Birth(Player player,Vector2 origin,Vector2 aim,float speed,PredictionTerrain terrain,out Vector2 center)
        {
            player.LimitPointToPlayerReachableArea(ref aim);var velocity=HostThornAttack.BirthVelocity(player,origin,aim,speed);center=origin;
            if(velocity==Vector2.Zero)return false;
            for(int count=0;count<2048;count++)
            {
                CombatPredictionWork.Check();bool clear;PredictionStop stop;if(!terrain.CanHitLine(new MotionRect(player.position.X,player.position.Y,player.width,player.height),new MotionRect(center.X,center.Y,1,1),out clear,out stop))return false;if(!clear)break;
                center+=velocity;if(Vector2.Distance(center,aim)<20+Math.Abs(velocity.X)+Math.Abs(velocity.Y)){center=aim;break;}
                if(count==2047)return false;
            }
            var unit=velocity.SafeNormalize(Vector2.One);
            for(int i=0;i<16;i++){bool solid;if(!Solid(center,terrain,out solid))return false;if(!solid)break;center-=unit;}return true;
        }
        private static bool LiquidLine(Vector2 center,Vector2 target,PredictionTerrain terrain,ref bool available)
        {
            bool known=true;
            bool connected=Utils.PlotLine(center.ToTileCoordinates(),target.ToTileCoordinates(),(x,y)=>
            {PredictionTile tile;PredictionStop stop;if(!WorldGen.InWorld(x,y))return false;if(!terrain.Tile(x,y,out tile,out stop)){known=false;return false;}return tile.Liquid!=0;});
            available&=known;return connected;
        }
        internal static AttackContact Solve(Player player,Projectile source,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,Vector2 aim,out HostAttackObstacles obstacles,bool opening=false,float speed=0)
        {
            var p=HostThornAttack.Copy(source);p.ownerHitCheckDistance=source.ownerHitCheckDistance;
            aim=HostDirectionalControl.Cursor(player,aim);
            if(opening){Vector2 birth;if(!Birth(player,source.Center,aim,speed,terrain,out birth)){obstacles=null;return null;}p.Center=birth;p.velocity=Vector2.Zero;p.ai[0]=-1;p.ai[1]=p.ai[2]=0;p.damage=player.GetWeaponDamage(player.HeldItem);}
            obstacles=new HostAttackObstacles(player,p,path,clock.LaunchDelay,clock.BeforeNpc,clock.NextWorld);obstacles.CaptureHomingInputs(player,false);var targets=obstacles.CaptureChaseTargets(p);obstacles.BeginCandidate();
            // Keep the actual seeded native bolt for an observed source. An
            // opening's seed0 is only one legal random future representative.
            for(int step=0;step<30 && clock.FirstTick+step<path.Count;step++)
            {
                CombatPredictionWork.Check();int tick=clock.FirstTick+step;Vector2 target;
                if(!HostHomingEffectAttack.StormTarget(p,targets,path,tick,out target))return null;
                if(p.ai[0]>-2 && p.ai[0]<0)
                {p.velocity=aim-p.Center;if(p.velocity.Length()>16)p.velocity*=16/p.velocity.Length();bool solid;if(!Solid(p.Center+p.velocity,terrain,out solid))return null;if(solid)p.velocity=Vector2.Zero;}
                p.Center=p.Center.MoveTowards(target,32);p.ai[1]++;
                if(p.ai[1]>=30 || p.timeLeft<=1)
                {
                    // LightningStrikeWeapon has SolidTileCollision=false in
                    // the locked native version. Liquid at its endpoint still
                    // chooses the connected-liquid500 collider instead of the
                    // ordinary136/64 burst; use leased cells for that query.
                    var bolt=LightningGenerator.LightningStrikeWeapon.GenerateMainBoltPath((uint)p.ai[2],p.Center);var hitbox=new MultiPointHitbox(new Point(16,16),bolt.positions);
                    PredictionTile tile;PredictionStop stop;var cell=p.Center.ToTileCoordinates();if(!terrain.Tile(cell.X,cell.Y,out tile,out stop))return null;bool liquid=tile.Liquid!=0,available=true;
                    Func<MotionRect,bool> collides=b=>
                    {
                        var rect=new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height);
                        if(!liquid && new Rectangle((int)p.position.X-60,(int)p.position.Y-60,p.width+120,p.height+120).Intersects(rect))return true;
                        if(liquid && Vector2.Distance(p.Center,rect.Center.ToVector2())<500 && LiquidLine(p.Center,rect.Center.ToVector2(),terrain,ref available))return true;
                        return hitbox.Intersects(rect);
                    };
                    var policy=HostThornAttack.Copy(p);policy.maxPenetrate=policy.penetrate=-1;policy.localAI[0]=liquid?3:1;
                    var receive=obstacles.ForkForSpawn(policy);receive.MakeUnlimited();int before=receive.SelectedHits;
                    receive.PassCustomOuter(2,tick,clock.LaunchDelay+step+1,p.Center.X,p.Center.Y,p.width,p.height,collides,unlimited:true,consumeSelected:true);
                    if(!available)return null;
                    return AttackIntercept.ProjectileShapeContact(path,tick,0,receive.SelectedHits>before,aim.X,aim.Y,p.Center.X,p.Center.Y,opening?AttackConfidence.Representative:AttackConfidence.Conditional);
                }
                float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return null;p.velocity.X+=wind;p.position+=p.velocity;p.timeLeft--;
            }
            return null;
        }
    }
}
