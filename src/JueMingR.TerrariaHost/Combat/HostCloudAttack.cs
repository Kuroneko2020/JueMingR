using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // AI045's hand is a placement command. Only its derived rain receives
    // damage. Replay the complete owner/other-owner cloud family in native
    // slot order, including rain born into a slot already visited this frame.
    internal static class HostCloudAttack
    {
        internal static bool RawCursor(int item){return item==1244 || item==1256;}
        internal static bool Handles(int type){return type==237 || type==243;}
        internal static bool Family(int type){return type>=237 && type<=239 || type>=243 && type<=245;}
        private sealed class Member
        {internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source,Random;}
        private static Member Make(Projectile p,HostAttackObstacles root,bool source,bool random)
        {return new Member{Shot=p,Receive=p.owner==Main.myPlayer?root.ForkForSpawn(p):null,Source=source,Random=random};}
        private static Projectile[] Capture(HostAttackObstacles root)
        {
            var rows=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && Family(p.type))rows.Add(p);
            var scene=rows.ToArray();root.CaptureProjectileFamily(rows.FindAll(p=>p.type<=239).ToArray(),237,239);root.CaptureProjectileFamily(rows.FindAll(p=>p.type>=243).ToArray(),243,245);return scene;
        }
        private static List<Member> Copy(Projectile[] scene,Projectile[] sources,HostAttackObstacles root)
        {
            var rows=new List<Member>();foreach(var p in scene){bool source=false;if(sources!=null)foreach(var q in sources)if(ReferenceEquals(p,q)){source=true;break;}rows.Add(Make(HostBetsyFlameAttack.Copy(p),root,source,false));}return rows;
        }
        private static bool Spawn(Member parent,int type,Vector2 center,Vector2 velocity,List<Member> rows,HostProjectileBirthSlots slots,PredictionTerrain terrain,HostAttackObstacles root,bool random)
        {
            int slot;if(!slots.TryAllocate(out slot))return false;var p=new Projectile();p.SetDefaults(type);p.active=true;p.owner=parent.Shot.owner;p.whoAmI=slot;p.Center=center;p.velocity=velocity;p.damage=parent.Shot.damage;p.knockBack=type==239 || type==245?0:parent.Shot.knockBack;
            bool wet;byte kind;if(!terrain.ProjectileWet(center.X,center.Y,p.width,p.height,out wet,out kind,true))return false;p.wet=!p.ignoreWater && wet;p.honeyWet=wet && kind==2;
            rows.Add(Make(p,root,parent.Source,parent.Random || random));return true;
        }
        private static bool Kill(Member m,List<Member> rows,HostProjectileBirthSlots slots,PredictionTerrain terrain,HostAttackObstacles root)
        {
            var p=m.Shot;if(!p.active)return true;p.timeLeft=0;
            // Native Kill creates the stationary cloud before deactivating
            // the moving parent's slot. A child cannot steal that slot early.
            if(Handles(p.type) && p.owner==Main.myPlayer && !Spawn(m,p.type+1,p.Center,Vector2.Zero,rows,slots,terrain,root,false))return false;
            p.active=false;slots.Release(p.whoAmI);return true;
        }
        internal static bool RainClear(Projectile p,PredictionTerrain terrain,out bool clear)
        {
            clear=false;int x=(int)p.Center.X,y=(int)(p.position.Y+p.height),left=(int)(x/16f),right=(int)((x+2)/16f),top=(int)(y/16f),bottom=(int)((y+20)/16f);
            if(left<0 || top<0 || right>=Main.maxTilesX || bottom>=Main.maxTilesY-40)return true;
            for(int tx=left;tx<=right;tx++)for(int ty=top;ty<=bottom;ty++)
            {PredictionTile tile;PredictionStop stop;if(!terrain.Tile(tx,ty,out tile,out stop))return false;if(tile.Active && tile.Solid && !tile.SolidTop)return true;}
            clear=true;return true;
        }
        private static bool Step(Member m,List<Member> rows,HostProjectileBirthSlots slots,PredictionTerrain terrain,HostAttackObstacles root)
        {
            var p=m.Shot;
            if(p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld){p.active=false;slots.Release(p.whoAmI);return true;}
            if(Handles(p.type))
            {
                if(p.ai[0]!=0 && p.ai[1]!=0)
                {
                    bool x=p.velocity.X==0 || p.velocity.X<0 && p.Center.X<p.ai[0] || p.velocity.X>0 && p.Center.X>p.ai[0];
                    bool y=p.velocity.Y==0 || p.velocity.Y<0 && p.Center.Y<p.ai[1] || p.velocity.Y>0 && p.Center.Y>p.ai[1];
                    if(x)p.velocity.X=0;if(y)p.velocity.Y=0;if(x && y && p.owner==Main.myPlayer)return Kill(m,rows,slots,terrain,root);
                }
            }
            else if(p.type==238 || p.type==244)
            {
                bool clear;if(!RainClear(p,terrain,out clear))return false;p.ai[1]++;
                if(p.ai[1]>=18000){p.alpha+=5;if(p.alpha>255)return Kill(m,rows,slots,terrain,root);}
                else if(clear && ++p.ai[0]>(p.type==244?10:8))
                {
                    p.ai[0]=0;if(p.owner==Main.myPlayer && !Spawn(m,p.type+1,new Vector2((int)p.Center.X,(int)(p.position.Y+p.height)),new Vector2(0,5),rows,slots,terrain,root,true))return false;
                }
                if(++p.localAI[0]>=10)
                {
                    p.localAI[0]=0;int count=0;Member oldest=null;float clock=0;
                    foreach(var q in rows)if(q.Shot.active && q.Shot.owner==p.owner && q.Shot.type==p.type && q.Shot.ai[1]<18000)
                    {count++;if(q.Shot.ai[1]>clock || q.Shot.ai[1]==clock && oldest!=null && q.Shot.whoAmI<oldest.Shot.whoAmI){oldest=q;clock=q.Shot.ai[1];}}
                    if(count>(p.type==244?1:2) && oldest!=null)oldest.Shot.ai[1]=18000;
                }
            }
            else
            {
                int x=(int)(p.Center.X/16),y=(int)((p.position.Y+p.height)/16);
                if(x>=10 && y>=10 && x<Main.maxTilesX-10 && y<Main.maxTilesY-10)
                {PredictionTile tile;PredictionStop stop;if(!terrain.Tile(x,y,out tile,out stop))return false;if(tile.Liquid==255 && tile.ShimmerLiquid && p.velocity.Y>0)p.velocity.Y=-p.velocity.Y;}
            }
            if(!p.active)return true;
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            Vector2 wetVelocity=p.velocity;
            if(!p.ignoreWater)
            {
                bool wet,lava;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true) || wet && kind==3)return false;
                p.wet=wet;if(lava)p.lavaWet=true;if(wet && kind==2)p.honeyWet=true;if(!wet)p.lavaWet=p.honeyWet=false;
            }
            Vector2 position=p.position,velocity=p.velocity;bool hit=false;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet,p.honeyWet,out wetVelocity,out hit,nativeClip:true))return false;
            p.position=position;p.velocity=velocity;
            if(hit){p.position+=p.velocity;return Kill(m,rows,slots,terrain,root);}
            p.position+=p.wet?wetVelocity:p.velocity;return true;
        }
        private static bool Outer(List<Member> rows,HostProjectileBirthSlots slots,NpcTrajectory path,PredictionTerrain terrain,HostAttackObstacles root,int tick,int outer,Vector2 aim,out AttackContact contact)
        {
            contact=null;
            foreach(var m in rows)if(m.Shot.active && m.Receive!=null && !m.Receive.BeforeNpc(tick,outer,m.Shot.Center.X,m.Shot.Center.Y,m.Shot.width,m.Shot.height))return false;
            int after=-1;
            for(int visit=0;visit<1000;visit++)
            {
                Member m=null;foreach(var q in rows)if(q.Shot.active && q.Shot.whoAmI>after && (m==null || q.Shot.whoAmI<m.Shot.whoAmI))m=q;
                if(m==null)return true;var p=m.Shot;after=p.whoAmI;
                for(int sub=0;sub<=p.extraUpdates && p.active;sub++)
                {
                    CombatPredictionWork.Check();if(!Step(m,rows,slots,terrain,root))return false;if(!p.active)break;
                    if(p.friendly && p.damage>0 && m.Receive!=null)
                    {
                        int hits=m.Receive.SelectedHits;bool passes=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true);p.penetrate=m.Receive.Remaining;
                        if(m.Source && m.Receive.SelectedHits>hits){contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,m.Random?AttackConfidence.Representative:AttackConfidence.Conditional);return true;}
                        if(!passes && p.penetrate!=0)return false;
                        if(p.penetrate==0 && !Kill(m,rows,slots,terrain,root))return false;
                    }
                    if(p.active && --p.timeLeft<=0 && !Kill(m,rows,slots,terrain,root))return false;
                }
            }
            return false;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay)
        {
            if(!Handles(ammo.Projectile) || age-delay<0 || age+1>=path.Count)return null;root.CaptureCursorMapping(player);var scene=Capture(root);var pool=root.CaptureBirthSlots();var inputs=new List<Vector2>();
            for(int seed=0;seed<27;seed++)
            {
                var b=path[Math.Min(path.Count-1,age+1+(seed%9)*4)].ProjectileReceiveBounds;
                // The native command's distance determines the cloud position.
                // Search receiver top and two rain head starts, preserving the
                // true command point rather than extending it to 10000 pixels.
                var aim=new Vector2(b.CenterX,b.Y-(seed/9)*40);aim=new Vector2((int)(aim.X-Main.screenPosition.X)+Main.screenPosition.X,(int)(aim.Y-Main.screenPosition.Y)+Main.screenPosition.Y);
                if(inputs.Contains(aim))continue;inputs.Add(aim);root.BeginCandidate();var rows=Copy(scene,null,root);var slots=pool.Copy();bool valid=true;
                for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,slots,path,terrain,root,age-delay+wait,wait,aim,out ignored)){valid=false;break;}}if(!valid)continue;
                int slot;if(!slots.TryAllocate(out slot))return null;var p=new Projectile();p.SetDefaults(ammo.Projectile);p.active=true;p.owner=player.whoAmI;p.whoAmI=slot;p.Center=origin;p.damage=ammo.Damage;p.knockBack=player.HeldItem.knockBack;p.ai[0]=aim.X;p.ai[1]=aim.Y;
                var logical=aim;if(player.gravDir==-1)logical.Y=Main.screenPosition.Y+Main.screenHeight-(aim.Y-Main.screenPosition.Y);p.velocity=(logical-origin).SafeNormalize(Vector2.UnitX*player.direction)*ammo.Speed;
                bool wet;byte kind;if(!terrain.ProjectileWet(origin.X,origin.Y,p.width,p.height,out wet,out kind,true))return null;p.wet=wet;p.honeyWet=wet && kind==2;rows.Add(Make(p,root,true,false));
                for(int step=1;step<=Math.Min(120,path.Count-age-1);step++){AttackContact contact;if(!Outer(rows,slots,path,terrain,root,age+step,delay+step,aim,out contact))break;if(contact!=null)return AttackIntercept.WithPointSemantics(contact,AttackPointSemantics.WorldPoint);}
            }
            return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(sources==null || sources.Length==0 || clock.LaunchDelay!=0 || clock.Phase==HostAttackPhase.Projectiles)return null;
            foreach(var p in sources)if(p==null || !p.active || !Family(p.type) || p.owner!=player.whoAmI)return null;
            var scene=Capture(root);root.BeginCandidate();var rows=Copy(scene,sources,root);var slots=root.CaptureBirthSlots().Copy();var aim=sources[0].Center;
            for(int step=0;step<Math.Min(120,path.Count-clock.FirstTick);step++){AttackContact contact;if(!Outer(rows,slots,path,terrain,root,clock.FirstTick+step,step+1,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
    }
}
