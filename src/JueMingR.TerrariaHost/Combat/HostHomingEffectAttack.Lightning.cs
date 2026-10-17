using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.Combat
{
    internal static partial class HostHomingEffectAttack
    {
        // Generate native local-seed geometry with collision disabled on a
        // private generator, then stop at the identical first leased solid /
        // liquid point. No Main.rand, Damage, particles or live tile writes.
        internal static bool ArcPoints(uint seed,Vector2 start,Vector2 end,PredictionTerrain terrain,out Vector2[] points)
        {
            var generator=LightningGenerator.GetArcSurgeWeaponGenerator(start,end);generator.SolidTileCollision=false;
            if(start==end){points=new[]{end};return true;}
            var bolt=generator.GenerateMainBoltPath(seed,end,end-start);points=bolt.positions;var endTile=end.ToTileCoordinates();
            for(int i=0;i<points.Length;i++)
            {
                CombatPredictionWork.Check();var cell=points[i].ToTileCoordinates();if(cell==endTile || !WorldGen.InWorld(cell))continue;
                PredictionTile tile;PredictionStop stop;if(!terrain.Tile(cell.X,cell.Y,out tile,out stop))return false;
                if(tile.Active && tile.Solid && !tile.SolidTop || tile.Liquid>0 && (int)points[i].Y%16>16*(255-tile.Liquid)/255)
                {Array.Resize(ref points,i+1);break;}
            }
            return true;
        }
        internal static bool StormTarget(Projectile p,HostProjectileTargets targets,NpcTrajectory path,int tick,out Vector2 center)
        {
            center=p.Center;int npc=(int)p.ai[0];
            if(p.ai[0]>=0)
            {
                for(int i=0;i<targets.Count;i++)if(targets.Slot(i)==npc){var b=targets.Bounds(i,path,tick);center=new Vector2(b.CenterX,b.CenterY);return Vector2.Distance(p.Center,center)<=600;}
                return false;
            }
            if(p.ai[0]<=-2)
            {int slot=-(int)p.ai[0]-2;if(slot<0 || slot>=255)return false;var player=Main.player[slot];if(player==null || !player.active || player.dead || player.dashDelay<0 || Vector2.Distance(p.Center,player.Center)>600)return false;center=player.Center;return true;}
            float radius=p.type==1117?12:32;
            for(int i=0;i<targets.Count;i++)
            {
                if(targets.Slot(i)==path.Identity.Slot && !path[tick].CanReceive)continue;
                var b=targets.Bounds(i,path,tick);var closest=new Vector2(MathHelper.Clamp(p.Center.X,b.X,b.X+b.Width),MathHelper.Clamp(p.Center.Y,b.Y,b.Y+b.Height));
                if(Vector2.Distance(p.Center,closest)>radius)continue;p.ai[0]=targets.Slot(i);center=new Vector2(b.CenterX,b.CenterY);return true;
            }
            if(p.type==1117)return true;
            int required=Main.Difficulty<GameDifficultyLevel.Legendary?5:0;float playerRadius=required==0?32:14;
            for(int i=0;i<255;i++)
            {
                var player=Main.player[i];if(player==null || !player.active || player.dead || Vector2.Distance(p.Center,player.Hitbox.ClosestPointInRect(p.Center))>playerRadius)continue;
                if(p.localAI[2]<required){p.localAI[2]++;return true;}p.ai[0]=-i-2;center=player.Center;return true;
            }
            return true;
        }
        private static bool ArcOuter(List<Member> rows,HostProjectileBirthSlots slots,Player player,HostProjectileTargets targets,NpcTrajectory path,PredictionTerrain terrain,int tick,int outer,Vector2 aim,out AttackContact contact)
        {
            contact=null;
            foreach(var m in rows)
            {
                var p=m.Shot;if(!p.active)continue;Vector2 center;
                if(!StormTarget(p,targets,path,tick,out center)){p.active=false;slots.Release(p.whoAmI);continue;}
                p.Center=p.Center.MoveTowards(center,32);p.ai[1]++;
                // 1122's native timeLeft is ONE, not AI203's240 windup.
                // Its first Kill is the actual damage stage; the waiting body
                // has localAI0=0 and cannot deliver an ordinary body hit.
                if(--p.timeLeft>0 && p.ai[1]<240)continue;
                var start=player.RotatedRelativePoint(player.HandPosition??player.MountedCenter);Vector2[] points;
                if(!ArcPoints((uint)p.ai[2],start,p.Center,terrain,out points))return false;
                var hitbox=new MultiPointHitbox(new Point(16,16),points);p.maxPenetrate=p.penetrate=-1;p.localAI[0]=2;
                if(m.Receive!=null && p.friendly && !p.hostile && p.damage>0)
                {
                    m.Receive.MakeUnlimited();int before=m.Receive.SelectedHits;
                    m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,
                        b=>hitbox.Intersects(new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height)),unlimited:true,consumeSelected:true);
                    if(m.Source && m.Receive.SelectedHits>before)
                    {contact=AttackIntercept.ProjectileShapeContact(path,tick,0,true,aim.X,aim.Y,p.Center.X,p.Center.Y,m.Representative?AttackConfidence.Representative:AttackConfidence.Conditional);return true;}
                }
                p.active=false;slots.Release(p.whoAmI);
            }
            return true;
        }
        private static AttackContact ArcSurge(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,PredictionTerrain terrain,HostAttackObstacles root,int delay)
        {
            root.CaptureHomingInputs(player,true);root.CaptureShotFacing(player);
            var policy=new Projectile();policy.SetDefaults(1122);policy.owner=player.whoAmI;
            var targets=root.CaptureAttachmentTargets(policy);var chase=root.CaptureChaseTargets(policy);var scene=Capture(root,1122);var pool=root.CaptureBirthSlots();
            for(int seed=0;seed<9;seed++)
            {
                int at=Math.Min(path.Count-1,age+1+seed*2);var box=path[at].ProjectileReceiveBounds;var aim=HostDirectionalControl.Cursor(player,new Vector2(box.CenterX,box.CenterY));player.LimitPointToPlayerReachableArea(ref aim);
                var direction=(aim-origin).SafeNormalize(Vector2.UnitX);var point=aim;float minimum=ammo.Speed*20,distance=Vector2.Distance(point,origin);if(distance<minimum)point+=direction*(minimum-distance);
                root.BeginCandidate();var rows=Copy(scene,root);var slots=pool.Copy();bool valid=true;
                for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!ArcOuter(rows,slots,player,targets,path,terrain,age-delay+wait,wait,aim,out ignored)){valid=false;break;}}if(!valid)continue;
                // The zero circular offset and seed0 are legal RNG values.
                // Extra targets are separate native random selections with
                // replacement; one first qualifying target is a legal choice
                // for each attempt, never two guaranteed distinct receivers.
                var births=new List<Vector2>{point};int attempts=0;var possible=new List<int>();
                for(int i=0;i<chase.Count;i++)
                {var b=chase.Bounds(i);if(new Rectangle((int)player.Center.X-500,(int)player.Center.Y-400,1000,800).Intersects(new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height)))possible.Add(i);}
                foreach(int i in possible)
                {
                    var b=chase.Bounds(i);if(!new Rectangle((int)player.Center.X-500,(int)player.Center.Y-400,1000,800).Intersects(new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height)))continue;
                    var target=new Vector2(b.CenterX,b.CenterY);if(Vector2.Dot((target-origin).SafeNormalize(Vector2.UnitX),direction)<.5f)continue;
                    var trace=origin;
                    while(Vector2.Distance(trace,target)>8)
                    {var step=Vector2.Zero.MoveTowards(target-trace,8);bool clear;PredictionStop stop;if(!terrain.CanHitLine(new MotionRect(trace.X,trace.Y,0,0),new MotionRect(trace.X+step.X,trace.Y+step.Y,0,0),out clear,out stop)){valid=false;break;}if(!clear)break;trace+=step;}
                    if(!valid)break;if(Vector2.Distance(trace,target)>16)continue;
                    int availableAttempts=Math.Min(2,possible.Count);for(;attempts<availableAttempts;attempts++)births.Add(trace);break;
                }
                if(!valid)continue;
                foreach(var birth in births)
                {int slot;if(!slots.TryAllocate(out slot))return null;var p=new Projectile();p.SetDefaults(1122);p.active=true;p.whoAmI=slot;p.owner=player.whoAmI;p.Center=birth;p.velocity=Vector2.Zero;p.ai[0]=-1;p.ai[2]=0;p.damage=ammo.Damage;p.knockBack=player.HeldItem.knockBack;rows.Add(new Member{Shot=p,Receive=root.ForkForSpawn(p),Source=true,Representative=true});}
                rows.Sort((a,b)=>a.Shot.whoAmI.CompareTo(b.Shot.whoAmI));AttackContact contact;if(!ArcOuter(rows,slots,player,targets,path,terrain,age+1,delay+1,aim,out contact))continue;if(contact!=null)return contact;
            }
            return null;
        }
    }
}
