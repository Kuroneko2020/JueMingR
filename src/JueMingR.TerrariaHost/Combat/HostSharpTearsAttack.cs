using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // Blood Thorn's native birth is terrain-selected. Its fixed AI157 body
    // receives along a growing line; velocity is direction, never movement.
    internal static class HostSharpTearsAttack
    {
        internal static bool Handles(int type){return type==756;}
        private sealed class Member{internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source,Representative;}
        private static bool LeaseScan(PredictionTerrain terrain,Vector2 start,Vector2 end)
        {
            // HitLine clamps endpoints and reads their side neighbours. The
            // 4px three-ray scan fits this enclosing finite rectangle. Use
            // original geometry only AFTER every possible tile joins the
            // preparation lease, as for PredictionTerrain.MeleeLine.
            int left=Math.Max(0,(int)(Math.Min(start.X,end.X)/16)-2),right=Math.Min(Main.maxTilesX-1,(int)(Math.Max(start.X,end.X)/16)+2);
            int top=Math.Max(0,Math.Min(Main.maxTilesY-40,(int)(Math.Min(start.Y,end.Y)/16))-2),bottom=Math.Min(Main.maxTilesY-1,Math.Max(1,Math.Min(Main.maxTilesY-40,(int)(Math.Max(start.Y,end.Y)/16)))+2);
            for(int x=left;x<=right;x++)for(int y=top;y<=bottom;y++){PredictionTile tile;PredictionStop stop;if(!terrain.Tile(x,y,out tile,out stop))return false;}return true;
        }
        private static bool Solid(PredictionTerrain terrain,int x,int y,bool broad,out bool value)
        {
            value=false;if(x<0 || y<0 || x>=Main.maxTilesX || y>=Main.maxTilesY)return true;PredictionTile tile;PredictionStop stop;if(!terrain.Tile(x,y,out tile,out stop))return false;
            value=tile.Active && tile.RawSolid && !tile.Half && (broad || !tile.SolidTop) && (tile.Slope==0 || broad && tile.Platform && (tile.Slope==1 || tile.Slope==2));return true;
        }
        internal static bool Spots(Player player,Vector2 target,PredictionTerrain terrain,out Point[] candidates)
        {
            candidates=null;if(!LeaseScan(terrain,player.Center,target))return false;Vector2 delta;float[] samples;Collision.AimingLaserScan(player.Center,target,4,3,out delta,out samples);float distance=float.PositiveInfinity;foreach(float sample in samples)distance=Math.Min(distance,sample);target=player.Center+delta.SafeNormalize(Vector2.Zero)*distance;
            var point=target.ToTileCoordinates();var area=new Rectangle(point.X,point.Y,1,1);area.Inflate(6,16);var world=new Rectangle(0,0,Main.maxTilesX,Main.maxTilesY);world.Inflate(-40,-40);area=Rectangle.Intersect(area,world);var opened=new List<Point>();var closed=new List<Point>();
            for(int x=area.Left;x<=area.Right;x++)for(int y=area.Top;y<=area.Bottom;y++)
            {
                bool solid;if(!Solid(terrain,x,y,true,out solid))return false;if(!solid || Vector2.Distance(target,new Vector2(x*16+8,y*16+8))>200)continue;bool open=false,side;
                if(x>point.X){if(!Solid(terrain,x-1,y,false,out side))return false;open|=!side;}if(!open && x<point.X){if(!Solid(terrain,x+1,y,false,out side))return false;open|=!side;}if(!open && y>point.Y){if(!Solid(terrain,x,y-1,false,out side))return false;open|=!side;}if(!open && y<point.Y){if(!Solid(terrain,x,y+1,false,out side))return false;open|=!side;}(open?opened:closed).Add(new Point(x,y));
            }
            // Zero is inside native NextVector2Square(-2,2), so this is one
            // legal fallback tile when no solid candidate exists.
            if(opened.Count==0 && closed.Count==0)opened.Add(player.Center.ToTileCoordinates());candidates=(opened.Count>0?opened:closed).ToArray();return true;
        }
        internal static Projectile Birth(Player player,Vector2 aim,Point tile,int damage)
        {var q=HostSuperStarAttack.Copy(ContentSamples.ProjectilesByType[756]);q.active=true;q.owner=player.whoAmI;q.Center=tile.ToWorldCoordinates(8,8);q.velocity=(aim-q.Center).SafeNormalize(-Vector2.UnitY)*16;q.damage=damage;q.knockBack=player.HeldItem.knockBack;q.ai[0]=0;q.ai[1]=1.05f;return q;}
        internal static bool Ai(Projectile p)
        {
            bool grow=p.ai[0]<20 && p.ai[0]>=0,fade=p.ai[0]>=30,kill=p.ai[0]>=35;p.ai[0]++;
            if(p.ai[0]<0){p.alpha=255;p.scale=.1f*p.ai[1];}if(p.localAI[0]==0 && p.ai[0]>=0){p.localAI[0]=1;p.rotation=p.velocity.ToRotation();}
            if(grow){p.Opacity+=.1f;p.scale=p.Opacity*p.ai[1];return true;}if(fade)p.Opacity-=.2f;if(kill){p.active=false;p.timeLeft=0;}return p.active;
        }
        internal static bool Collides(Projectile p,MotionRect b)
        {if(p.ai[0]<0)return false;float point=0;var box=new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height);return Collision.CheckAABBvLineCollision(box.TopLeft(),box.Size(),p.Center,p.Center+p.velocity.SafeNormalize(-Vector2.UnitY)*200*p.scale,22*p.scale,ref point);}
        private static Projectile[] Capture(Player player,HostAttackObstacles root)
        {var list=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && p.type==756)list.Add(p);var scene=list.ToArray();root.CaptureOwnedProjectileFamily(scene,player.whoAmI,new[]{756});return scene;}
        private static List<Member> Copy(Projectile[] scene,Projectile[] sources,HostAttackObstacles root)
        {var rows=new List<Member>();foreach(var p in scene){var q=HostSuperStarAttack.Copy(p);rows.Add(new Member{Shot=q,Receive=root.ForkForSpawn(q),Source=sources!=null && Array.IndexOf(sources,p)>=0});}return rows;}
        private static bool Outer(List<Member> rows,NpcTrajectory path,int tick,int outer,HostProjectileBirthSlots slots,Vector2 aim,out AttackContact contact)
        {
            contact=null;foreach(var row in rows)if(row.Shot.active && !row.Receive.BeforeNpc(tick,outer,row.Shot.Center.X,row.Shot.Center.Y,row.Shot.width,row.Shot.height))return false;
            foreach(var m in rows)
            {
                var p=m.Shot;if(!p.active)continue;if(p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld){p.active=false;slots.Release(p.whoAmI);continue;}
                for(int sub=0;sub<=p.extraUpdates && p.active;sub++)
                {
                    CombatPredictionWork.Check();Ai(p);if(p.owner<255 && !Main.player[p.owner].active){p.active=false;p.timeLeft=0;}if(!p.active){slots.Release(p.whoAmI);break;}
                    if(p.damage>0 && p.ai[0]>=0)
                    {
                        int hits=m.Receive.SelectedHits;bool terminal=false;
                        bool pass=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,b=>Collides(p,b),consumeSelected:true,beforeHitPenetration:(slot,left)=>left==1?-1:left,onHit:(slot,left)=>{if(left==-1){p.damage=0;terminal=true;return false;}return true;});p.penetrate=m.Receive.Remaining;
                        if(m.Source && m.Receive.SelectedHits>hits){var b=path[tick].ProjectileReceiveBounds;contact=AttackIntercept.ProjectileShapeContact(path,tick,sub,true,aim.X,aim.Y,b.CenterX,b.CenterY,m.Representative?AttackConfidence.Representative:AttackConfidence.Conditional);return true;}
                        if(!pass && !terminal)return false;
                    }
                    if(--p.timeLeft<=0){p.active=false;slots.Release(p.whoAmI);}
                }
            }
            return true;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(sources==null || sources.Length==0 || clock.Phase==HostAttackPhase.Projectiles || clock.LaunchDelay!=0 || clock.Age<0)return null;var scene=Capture(player,root);root.BeginCandidate();var slots=root.CaptureBirthSlots().Copy();var rows=Copy(scene,sources,root);var aim=sources[0].Center+sources[0].velocity*100;
            for(int action=0;action<Math.Min(120,path.Count-clock.FirstTick);action++){AttackContact contact;if(!Outer(rows,path,clock.FirstTick+action,action+1,slots,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay)
        {
            if(ammo.Projectile!=756 || ammo.Damage<=0 || age-delay<0 || age+1>=path.Count)return null;root.CaptureHomingInputs(player,false,true);root.CaptureShotFacing(player);root.CaptureSharpTearsView();var scene=Capture(player,root);var pool=root.CaptureBirthSlots();
            for(int seed=0;seed<9;seed++)
            {
                var b=path[Math.Min(path.Count-1,age+1+seed*4)].ProjectileReceiveBounds;var aim=HostDirectionalControl.Cursor(player,new Vector2(b.CenterX,b.CenterY));player.LimitPointToPlayerReachableArea(ref aim);Point[] spots;if(!Spots(player,aim,terrain,out spots))return null;
                foreach(var tile in spots)
                {
                    root.BeginCandidate();var rows=Copy(scene,null,root);var slots=pool.Copy();bool valid=true;for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,path,age-delay+wait,wait,slots,aim,out ignored)){valid=false;break;}}if(!valid)continue;int slot;if(!slots.TryAllocate(out slot))return null;var q=Birth(player,aim,tile,ammo.Damage);q.whoAmI=slot;rows.Add(new Member{Shot=q,Receive=root.ForkForSpawn(q),Source=true,Representative=true});rows.Sort((a,c)=>a.Shot.whoAmI.CompareTo(c.Shot.whoAmI));
                    for(int action=1;action<=Math.Min(120,path.Count-age-1);action++){AttackContact contact;if(!Outer(rows,path,age+action,delay+action,slots,aim,out contact))break;if(contact!=null)return contact;}
                }
            }
            return null;
        }
    }
}
