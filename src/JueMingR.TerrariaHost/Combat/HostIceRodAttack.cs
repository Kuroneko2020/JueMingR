using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // Locked .8 item496/AI22. Placement is a receive-time value query, not a
    // world operation. Its failure leaves a damaging sixty-action suffix.
    // The private family records only changed ice cells. A later world action
    // continues only when the exact NPC path never read those cells, and the
    // owner's private continuation rejects any affected terrain consumer.
    internal static class HostIceRodAttack
    {
        internal delegate bool Placement(int x,int y,out bool placed);
        private static readonly MethodInfo clone=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
        private const int MaximumMembers=64;
        private sealed class World
        {
            internal readonly Dictionary<long,bool> Ice=new Dictionary<long,bool>();internal bool Changed;internal int Revision,ProvedRevision=-1;
            internal void Change(int x,int y,bool active){Ice[Key(x,y)]=active;Changed=true;Revision++;}
            internal IEnumerable<Point> ChangedCells(){foreach(var pair in Ice)yield return new Point((int)(pair.Key>>32),(int)pair.Key);}
        }
        private sealed class Member
        {internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source,Reflected;}
        internal static bool Handles(int type){return type==80;}
        internal static Projectile Copy(Projectile p){return HostThornAttack.Copy(p);}
        private static long Key(int x,int y){return ((long)x<<32)|(uint)y;}
        private static bool InWorld(int x,int y){return x>=0 && y>=0 && x<Main.maxTilesX && y<Main.maxTilesY;}
        private static bool Tile(PredictionTerrain terrain,World world,int x,int y,out PredictionTile tile)
        {
            PredictionStop stop;if(!terrain.Tile(x,y,out tile,out stop))return false;
            bool active;if(world!=null && world.Ice.TryGetValue(Key(x,y),out active))
            {tile.Active=tile.RawActive=active;tile.RawSolid=true;tile.Solid=active;tile.SolidTop=tile.Platform=false;tile.Half=false;tile.Slope=0;tile.Type=127;}
            return true;
        }
        private static bool Solid(PredictionTerrain terrain,World world,int x,int y,out bool solid)
        {solid=false;if(!InWorld(x,y))return true;PredictionTile tile;if(!Tile(terrain,world,x,y,out tile))return false;solid=tile.Active && tile.Solid && !tile.SolidTop && !tile.Half && tile.Slope==0;return true;}
        private static bool Open(PredictionTerrain terrain,World world,int x,int y,out bool open)
        {open=false;if(!InWorld(x,y))return true;bool solid;if(!Solid(terrain,world,x,y,out solid))return false;open=!solid;return true;}
        private static bool Kill(Projectile p,PredictionTerrain terrain,World world)
        {
            if(!p.active)return true;int x=(int)p.position.X/16,y=(int)p.position.Y/16;
            if(!InWorld(x,y))return false;PredictionTile tile;if(!Tile(terrain,world,x,y,out tile))return false;
            if(tile.RawActive && tile.Type==127)world.Change(x,y,false);
            p.active=false;p.timeLeft=0;return true;
        }
        private static bool Adjust(Projectile p,PredictionTerrain terrain,World world,ref int x,ref int y)
        {
            bool solid;if(!Solid(terrain,world,x,y,out solid))return false;if(!solid)return true;
            // Native priority depends on the dominant velocity axis; equality
            // uses the vertical branch. Preserve the centre comparison store.
            int[] dx=Math.Abs(p.velocity.X)>Math.Abs(p.velocity.Y)?new[]{0,0,0,-1,1,-1}:new[]{-1,1,-1,0,0,0};
            int[] dy=Math.Abs(p.velocity.X)>Math.Abs(p.velocity.Y)?new[]{-1,1,-1,0,0,0}:new[]{0,0,0,-1,1,-1};
            for(int i=0;i<6;i++)
            {
                bool directional=i==0 || i==3;
                if(directional && !(dx[i]!=0?p.Center.X<x*16+8:p.Center.Y<y*16+8))continue;
                bool open;if(!Open(terrain,world,x+dx[i],y+dy[i],out open))return false;if(open){x+=dx[i];y+=dy[i];return true;}
            }
            return true;
        }
        private static bool Ai(Projectile p,PredictionTerrain terrain,World world,Placement placement)
        {
            if(p.velocity==Vector2.Zero)p.alpha=255;
            if(p.ai[1]<0)
            {
                if(p.timeLeft>60)p.timeLeft=60;p.rotation+=p.velocity.X>0?.3f:-.3f;
                bool clear;if(!terrain.IceCoreClear(p.position.X,p.position.Y,p.width,p.height,true,out clear))return false;
                if(!clear && !Kill(p,terrain,world))return false;return true;
            }
            if(p.ai[0]<0)
            {
                int x=(int)p.position.X/16,y=(int)p.position.Y/16;PredictionTile tile;if(!InWorld(x,y) || !Tile(terrain,world,x,y,out tile))return false;
                if(!tile.RawActive && !Kill(p,terrain,world))return false;p.ai[0]-=1;
                if(p.ai[0]<=-900 && (p.owner==Main.myPlayer || Main.netMode==2) && tile.RawActive && tile.Type==127)
                {world.Change(x,y,false);if(!Kill(p,terrain,world))return false;}
                return true;
            }
            bool empty;if(!terrain.IceCoreClear(p.position.X,p.position.Y,p.width,p.height,false,out empty))return false;
            if(!empty && !Kill(p,terrain,world))return false;if(p.lavaWet && !Kill(p,terrain,world))return false;
            int cx=(int)(p.Center.X/16f),cy=(int)(p.Center.Y/16f);PredictionTile liquid;
            if(InWorld(cx,cy)){if(!Tile(terrain,world,cx,cy,out liquid))return false;if(liquid.Liquid>0 && liquid.ShimmerLiquid && !Kill(p,terrain,world))return false;}
            if(!p.active)return true;
            int tx=(int)p.ai[0],ty=(int)p.ai[1];if(!Adjust(p,terrain,world,ref tx,ref ty))return false;p.rotation+=p.velocity.X>0?.3f:-.3f;
            if(p.owner!=Main.myPlayer)return true;
            int x2=(int)((p.position.X+p.width/2)/16f),y2=(int)((p.position.Y+p.height/2)/16f);
            bool reached=x2==tx && y2==ty || (p.velocity.X<=0 && x2<=tx || p.velocity.X>=0 && x2>=tx) && (p.velocity.Y<=0 && y2<=ty || p.velocity.Y>=0 && y2>=ty);
            if(!reached)return true;
            bool placed=false;
            if(InWorld(tx,ty))
            {
                PredictionTile tile;if(!Tile(terrain,world,tx,ty,out tile))return false;
                // Raw active (including actuated/non-solid/127) always makes
                // Collision.EmptyTile fail. Only an empty cell needs bodies.
                if(!tile.RawActive && (placement==null || !placement(tx,ty,out placed)))return false;
            }
            if(placed){p.damage=0;p.ai[0]=-1;p.velocity*=0;p.alpha=255;p.position=new Vector2(tx*16,ty*16);world.Change(tx,ty,true);}
            else p.ai[1]=-1;
            return true;
        }
        private static bool Motor(Projectile p,PredictionTerrain terrain,World world,Placement placement)
        {
            if(!p.active)return true;
            if(p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld || float.IsNaN(p.position.X) || float.IsNaN(p.position.Y))
            {p.active=false;return true;} // Native direct retirement is not Kill.
            if(p.type!=80 || p.aiStyle!=22 || p.width!=16 || p.height!=16 || p.extraUpdates!=0 || p.tileCollide || p.ignoreWater || p.ownerHitCheck || p.manualDirectionChange || p.correctSlopeCollision || p.usesOwnerMeleeHitCD)return false;
            if(!Ai(p,terrain,world,placement))return false;
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(p.owner<255 && !Main.player[p.owner].active && !Kill(p,terrain,world))return false;
            bool wet,lava;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;
            // Type80 forcibly clears wet before the normal liquid tail, so it
            // neither slows in water/honey nor keeps their flags. Current lava
            // kills only the still-unplaced projectile, after this AI action.
            p.wet=p.lavaWet=p.honeyWet=p.shimmerWet=false;if(p.wetCount>0)p.wetCount--;
            if(lava && p.ai[0]>=0 && !Kill(p,terrain,world))return false;
            // Update's post-AI active check is after HandleMovement. A killed
            // ice bolt still receives this final movement/direction store.
            p.position+=p.velocity;p.direction=p.velocity.X<0?-1:1;return true;
        }
        internal static bool Step(Projectile p,PredictionTerrain terrain,Placement placement,out bool worldChanged)
        {var world=new World();bool valid=Motor(p,terrain,world,placement);worldChanged=world.Changed;return valid;}
        internal static bool Finish(Projectile p,PredictionTerrain terrain,out bool worldChanged)
        {var world=new World();if(p.active){p.timeLeft--;if((p.timeLeft<=0 || p.penetrate==0) && !Kill(p,terrain,world)){worldChanged=world.Changed;return false;}}worldChanged=world.Changed;return true;}
        internal static Projectile Birth(Player player,AttackAmmoSnapshot ammo,Vector2 origin,Vector2 aim,PredictionTerrain terrain)
        {
            var q=new Projectile();q.SetDefaults(80);q.active=true;q.owner=player.whoAmI;q.Center=origin;q.velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);q.damage=ammo.Damage;q.knockBack=player.GetWeaponKnockback(player.HeldItem,player.HeldItem.knockBack);
            var screen=aim-Main.screenPosition;if(player.gravDir==-1)screen.Y=Main.screenHeight-screen.Y;
            float x=(int)screen.X+Main.screenPosition.X,y=player.gravDir==-1?Main.screenPosition.Y+Main.screenHeight-(int)screen.Y:(int)screen.Y+Main.screenPosition.Y;
            q.ai[0]=Math.Max(5,Math.Min(Main.maxTilesX-5,(int)(x/16f)));q.ai[1]=Math.Max(5,Math.Min(Main.maxTilesY-5,(int)(y/16f)));
            q.armorPenetration+=player.HeldItem.armorPenetration;q.bonusTagDamage+=player.HeldItem.bonusTagDamage;
            bool wet;byte kind;if(!terrain.ProjectileWet(q.Center.X,q.Center.Y,q.width,q.height,out wet,out kind,true))return null;q.wet=wet;q.honeyWet=wet && kind==2;q.shimmerWet=wet && kind==3;return q;
        }
        private static Projectile[] Capture(HostAttackObstacles root)
        {var list=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.type==80)list.Add(p);root.CaptureProjectileFamily(list.ToArray(),80,80);return list.ToArray();}
        private static List<Member> Prepare(Projectile[] all,Projectile[] sources,HostAttackObstacles root)
        {var rows=new List<Member>(all.Length+1);foreach(var p in all){bool source=false;if(sources!=null)foreach(var q in sources)if(ReferenceEquals(q,p)){source=true;break;}var copy=Copy(p);rows.Add(new Member{Shot=copy,Source=source,Receive=p.owner==Main.myPlayer?root.ForkForSpawn(copy):null});}return rows;}
        private static bool WorldCurrent(World world,NpcTrajectory path)
        {
            if(!world.Changed || world.ProvedRevision==world.Revision)return true;
            if(!NpcPredictionSource.IceTrajectoryUnaffected(path,world.ChangedCells()))return false;
            world.ProvedRevision=world.Revision;return true;
        }
        private static bool Advance(ref PredictionPlayerMotion motion,PredictionEnvironment environment,PredictionTerrain terrain,World world,out PredictionStop stop)
        {
            if(!world.Changed)return PlayerMotionContinuation.Advance(ref motion,environment,terrain,out stop);
            stop=PredictionStop.TerrainUnavailable;if(!terrain.BeginIcePlayerReadGuard(world.ChangedCells()))return false;
            try{return PlayerMotionContinuation.Advance(ref motion,environment,terrain,out stop);}finally{terrain.EndIcePlayerReadGuard();}
        }
        private static bool Outer(List<Member> rows,World world,NpcTrajectory path,PredictionTerrain terrain,int tick,int outer,bool beforeNpc,Vector2 aim,Player owner,out AttackContact contact)
        {
            contact=null;if(!WorldCurrent(world,path))return false;
            if(beforeNpc)foreach(var m in rows)if(m.Shot.active && m.Receive!=null && !m.Receive.BeforeNpc(tick,outer,m.Shot.Center.X,m.Shot.Center.Y,m.Shot.width,m.Shot.height)){m.Shot.active=false;m.Reflected=true;}
            rows.Sort((a,b)=>a.Shot.whoAmI.CompareTo(b.Shot.whoAmI));
            foreach(var m in rows)
            {
                var p=m.Shot;Placement place=(int x,int y,out bool placed)=>m.Receive.TryIcePlacement(terrain,x,y,tick,outer,new MotionRect(owner.position.X,owner.position.Y,owner.width,owner.height),out placed);
                if(!Motor(p,terrain,world,place))return false;
                if(p.active && p.damage>0 && p.friendly && !p.hostile && m.Receive!=null)
                {
                    int hits=m.Receive.SelectedHits;bool passed=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,16,16,null,consumeSelected:true);p.penetrate=m.Receive.Remaining;
                    if(m.Source && m.Receive.SelectedHits>hits){contact=AttackIntercept.BodyContact(path,tick,0,aim.X,aim.Y,p.Center.X,p.Center.Y,16,16,AttackConfidence.Conditional);if(contact!=null)return true;}
                    if(!passed){if(p.penetrate==0){if(!Kill(p,terrain,world))return false;}else{p.active=false;m.Reflected=true;}}
                }
                if(p.active){p.timeLeft--;if((p.timeLeft<=0 || p.penetrate==0) && !Kill(p,terrain,world))return false;}
            }
            return true;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay,HostAttackPlayerFrame frame,bool launchPlayerAdvanced=false)
        {
            // Completed-world preview already supplies the launch player's
            // next action. Its first projectile placement must use that pose,
            // while an ordinary completed-world capture still advances once.
            if(ammo.WeaponType!=496 || ammo.Projectile!=80 || ammo.Damage<=0 || age+1>=path.Count || delay<0 || age-delay<0)return null;
            root.CaptureShotFacing(player);root.CaptureCursorMapping(player);var all=Capture(root);if(all.Length>=MaximumMembers)return null;var pool=root.CaptureBirthSlots();var seen=new HashSet<Point>();
            for(int seed=0;seed<17;seed++)
            {
                var box=path[Math.Min(path.Count-1,age+1+seed*4)].ProjectileReceiveBounds;var aim=new Vector2(box.CenterX,box.CenterY);var tile=new Point((int)(aim.X/16),(int)(aim.Y/16));if(!seen.Add(tile))continue;
                root.BeginCandidate();var rows=Prepare(all,null,root);var slots=pool.Copy();var world=new World();var actual=Main.player[player.whoAmI];var waitFrame=delay==0?frame:HostAttackPlayerFrame.Capture(actual,path);var body=(Player)clone.Invoke(actual,null);var motion=waitFrame.Motion;bool valid=true;
                for(int wait=0;wait<delay;wait++){if(!WorldCurrent(world,path)){valid=false;break;}if(!beforeNpc || wait>0){PredictionStop stop;if(!Advance(ref motion,waitFrame.Environment,terrain,world,out stop)){valid=false;break;}}HostAttackPlayerFrame.ApplyPose(body,motion);AttackContact ignored;if(!Outer(rows,world,path,terrain,age-delay+wait+1,wait+1,true,aim,body,out ignored)){valid=false;break;}}
                if(!valid)continue;int slot;if(!slots.TryAllocate(out slot))return null;
                body=(Player)clone.Invoke(player,null);motion=frame.Motion;HostAttackPlayerFrame.ApplyPose(body,motion);var q=Birth(body,ammo,origin,aim,terrain);if(q==null)continue;q.whoAmI=slot;rows.Add(new Member{Shot=q,Source=true,Receive=root.ForkForSpawn(q)});
                for(int action=0;action<Math.Min(120,path.Count-age-1);action++){if(!WorldCurrent(world,path))break;if(!beforeNpc && !launchPlayerAdvanced || action>0){PredictionStop stop;if(!Advance(ref motion,frame.Environment,terrain,world,out stop))break;}HostAttackPlayerFrame.ApplyPose(body,motion);AttackContact found;if(!Outer(rows,world,path,terrain,age+action+1,delay+action+1,true,aim,body,out found))break;if(found!=null)return found;}
            }
            return null;
        }
        internal static AttackContact Known(Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root,HostAttackPlayerFrame frame)
        {
            if(sources==null || sources.Length==0 || clock.LaunchDelay!=0 || clock.Phase==HostAttackPhase.Projectiles)return null;foreach(var p in sources)if(p==null || !p.active || p.type!=80 || p.owner!=player.whoAmI)return null;
            var all=Capture(root);if(all.Length>=MaximumMembers)return null;root.BeginCandidate();var rows=Prepare(all,sources,root);var world=new World();var body=(Player)clone.Invoke(player,null);var motion=frame.Motion;var aim=sources[0].Center+sources[0].velocity*1000;
            for(int action=0;action<Math.Min(120,path.Count-clock.FirstTick);action++){if(!WorldCurrent(world,path))break;if(clock.MovePlayer(action)){PredictionStop stop;if(!Advance(ref motion,frame.Environment,terrain,world,out stop))break;}HostAttackPlayerFrame.ApplyPose(body,motion);AttackContact found;if(!Outer(rows,world,path,terrain,clock.FirstTick+action,action+1,clock.BeforeNpc || clock.NextWorld,aim,body,out found))break;if(found!=null)return found;}
            return null;
        }
    }
}
