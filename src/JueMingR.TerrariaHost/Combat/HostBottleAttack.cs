using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // AI68 bottles share their flight and 80px fuse body, not their Kill.
    // Ale calls Damage on its SAME 144px body; Molotov allocates six random
    // flames before its parent slot is released. No live RNG is consumed.
    internal static class HostBottleAttack
    {
        internal static bool Handles(int type){return type==399 || type==669;}
        private static bool Family(int type){return Handles(type) || type>=400 && type<=402;}
        internal sealed class DeathState
        {
            internal readonly Vector2 Center,Velocity;
            internal readonly int Damage;
            internal DeathState(Projectile p){Center=p.Center;Velocity=p.velocity;Damage=p.damage;}
        }
        internal static Projectile Copy(Projectile p)
        {
            var q=new Projectile();q.SetDefaults(p.type);q.active=p.active;q.whoAmI=p.whoAmI;q.owner=p.owner;q.position=p.position;q.velocity=p.velocity;q.width=p.width;q.height=p.height;q.scale=p.scale;q.rotation=p.rotation;q.direction=p.direction;q.spriteDirection=p.spriteDirection;
            q.damage=p.damage;q.knockBack=p.knockBack;q.timeLeft=p.timeLeft;q.extraUpdates=p.extraUpdates;q.alpha=p.alpha;q.penetrate=p.penetrate;q.maxPenetrate=p.maxPenetrate;
            q.friendly=p.friendly;q.hostile=p.hostile;q.magic=p.magic;q.ranged=p.ranged;q.melee=p.melee;q.npcProj=p.npcProj;q.trap=p.trap;q.tileCollide=p.tileCollide;q.ignoreWater=p.ignoreWater;q.correctSlopeCollision=p.correctSlopeCollision;
            q.wet=p.wet;q.honeyWet=p.honeyWet;q.lavaWet=p.lavaWet;q.shimmerWet=p.shimmerWet;q.wetCount=p.wetCount;
            q.usesLocalNPCImmunity=p.usesLocalNPCImmunity;q.localNPCHitCooldown=p.localNPCHitCooldown;q.usesIDStaticNPCImmunity=p.usesIDStaticNPCImmunity;q.immunityIdentity=p.immunityIdentity;q.idStaticNPCHitCooldown=p.idStaticNPCHitCooldown;q.appliesImmunityTimeOnSingleHits=p.appliesImmunityTimeOnSingleHits;
            Array.Copy(p.ai,q.ai,p.ai.Length);Array.Copy(p.localAI,q.localAI,p.localAI.Length);Array.Copy(p.localNPCImmunity,q.localNPCImmunity,p.localNPCImmunity.Length);return q;
        }
        internal static void Kill(Projectile p,out DeathState death)
        {KillCore(p,out death,null);}
        private static void KillCore(Projectile p,out DeathState death,Action<DeathState> onKill)
        {
            death=null;if(!p.active)return;death=new DeathState(p);p.timeLeft=0;
            if(p.type==669)p.Resize(144,144);
            if(onKill!=null)onKill(death);
            p.active=false;
        }
        private static bool Liquid(Projectile p,PredictionTerrain terrain)
        {
            if(p.ignoreWater)return true;bool wet,lava;byte kind;
            if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;
            if(lava)p.lavaWet=true;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;
            if(p.wet && !wet && p.wetCount==0)p.wetCount=10;p.wet=wet;if(!wet)p.honeyWet=p.lavaWet=p.shimmerWet=false;if(p.wetCount>0)p.wetCount--;return true;
        }
        // killed captures the native Kill center, which can precede movement
        // in this interval. World retirement is deliberately a separate exit.
        internal static bool Step(Projectile p,PredictionTerrain terrain,out bool killed,out DeathState death)
        {return Motor(p,terrain,out killed,out death,null);}
        private static bool Motor(Projectile p,PredictionTerrain terrain,out bool killed,out DeathState death,Action<DeathState> onKill)
        {
            killed=false;death=null;if(!p.active)return true;if(!Family(p.type))return false;
            if(p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld || float.IsNaN(p.position.X) || float.IsNaN(p.position.Y)){p.active=false;return true;}
            Vector2 wetVelocity=p.velocity;
            if(Handles(p.type))
            {
                p.rotation+=.25f*p.direction;p.ai[0]++;
                if(p.ai[0]>=3){p.alpha-=40;if(p.alpha<0)p.alpha=0;}
                if(p.ai[0]>=15){p.velocity.Y+=.2f;if(p.velocity.Y>16)p.velocity.Y=16;p.velocity.X*=.99f;}
                p.spriteDirection=p.direction;
                if(p.timeLeft<=3){p.tileCollide=false;p.alpha=255;p.position.X+=p.width/2;p.position.Y+=p.height/2;p.width=p.height=80;p.position.X-=p.width/2;p.position.Y-=p.height/2;p.knockBack=8;}
                // The prior wet bit is tested AFTER resize. First wet entry
                // only shortens the fuse; its 80px body starts next interval.
                if(p.wet && p.timeLeft>3)p.timeLeft=3;
            }
            else
            {
                p.ai[0]++;if(p.ai[0]>5){p.ai[0]=5;if(p.velocity.Y==0 && p.velocity.X!=0){p.velocity.X*=.97f;if((double)p.velocity.X>-.01 && (double)p.velocity.X<.01)p.velocity.X=0;}p.velocity.Y+=.2f;}
                p.rotation+=p.velocity.X*.1f;if(p.wet){KillCore(p,out death,onKill);killed=true;}
                if(p.velocity.Y<.25f && p.velocity.Y>.15f)p.velocity.X*=.8f;
                p.rotation=-p.velocity.X*.05f;if(p.velocity.Y>16)p.velocity.Y=16;
            }
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(p.owner<255 && !Main.player[p.owner].active && p.active){KillCore(p,out death,onKill);killed=true;}
            if(!Liquid(p,terrain))return false;
            if(p.shimmerWet)
            {
                if(p.type==669)
                {
                    int x=(int)(p.Center.X/16),y=(int)(p.position.Y/16);bool full=false;
                    if(WorldGen.InWorld(x,y) && WorldGen.InWorld(x,y-1)){PredictionTile tile,above;PredictionStop stop;if(!terrain.Tile(x,y,out tile,out stop))return false;if(tile.Liquid==255 && tile.ShimmerLiquid){if(!terrain.Tile(x,y-1,out above,out stop))return false;full=above.Liquid>0 && above.ShimmerLiquid;}}
                    // Ale is the explicit second Shimmer group: no600/60
                    // fuse penalty. Molotov AI68 has no Shimmer response.
                    if(full && p.active){KillCore(p,out death,onKill);killed=true;}else if(!full && p.velocity.Y>0){p.velocity.Y=-p.velocity.Y;p.wet=p.shimmerWet=false;}
                }
                else if(p.type>=400 && p.type<=402){if(p.timeLeft>3000)p.timeLeft-=60;p.velocity.Y-=.5f;}
            }
            Vector2 position=p.position,velocity=p.velocity,prior=p.velocity;bool hit=false;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;
            p.position=position;p.velocity=velocity;
            if(hit){if(Handles(p.type)){p.velocity*=0;p.alpha=255;p.timeLeft=3;}else if(p.velocity.X!=prior.X)p.velocity.X=prior.X*-.1f;}
            p.position+=p.wet?wetVelocity:p.velocity;p.direction=p.velocity.X<0?-1:1;return true;
        }
        internal static void Tail(Projectile p,out bool killed,out DeathState death)
        {Finish(p,out killed,out death,null);}
        private static void Finish(Projectile p,out bool killed,out DeathState death,Action<DeathState> onKill)
        {killed=false;death=null;if(p.active && (--p.timeLeft<=0 || p.penetrate==0)){KillCore(p,out death,onKill);killed=true;}}
        internal static Vector2 FlameVelocity(Vector2 parent,int xFactor,int xJitter,int yFactor,int yJitter)
        {return new Vector2(-parent.X*xFactor*.01f+xJitter*.4f,-Math.Abs(parent.Y)*yFactor*.01f+yJitter*.4f);}
        private sealed class Member
        {
            internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source,Random,Reflected;internal int Selected,SelectedType;internal Vector2 OwnerCenter;internal bool SelectedDamage;
            internal float SelectedCenterX;internal ReceiverFacts Facts;
            internal Func<int,int,bool> Hit;internal Func<int,int,int> Penetration;internal readonly Action<DeathState> OnKill;
            internal List<Member> Rows;internal HostProjectileBirthSlots Slots;internal NpcTrajectory Path;
            internal PredictionTerrain Terrain;internal int Tick,Outer,Sub;internal Vector2 Aim;
            internal bool DeathAvailable;internal AttackContact DeathContact;
            internal Member(){Hit=OnHit;Penetration=BeforeHit;OnKill=Killed;}
            private void Killed(DeathState death)
            {
                // Kill Damage/children happen synchronously before the
                // original Update resumes its remaining liquid/movement.
                DeathAvailable=Death(this,death,Rows,Slots,Path,Terrain,Tick,Outer,Sub,Aim,out DeathContact);
                Reflected=false; // even a reflection inside Kill ends inactive
            }
            private int BeforeHit(int slot,int remaining)
            {
                int type=slot==Selected?SelectedType:Facts.Types[slot];
                if(Shot.type>=400 && Shot.type<=402 && type>=13 && type<=15 && remaining>1)return remaining-1;
                return remaining;
            }
            private bool OnHit(int slot,int remaining)
            {
                // Damage_PVE checks positive damage once before its whole
                // NPC loop. Ale's earlier OnHit may reduce damage1 to0,
                // but that same call can still Strike a higher NPC slot.
                if(slot==Selected)SelectedDamage=true;
                float centerX=slot==Selected?SelectedCenterX:Facts.Centers[slot];
                if(Shot.type==669){Shot.damage=(int)((double)Shot.damage*.8);Shot.timeLeft=1;Shot.direction=centerX<OwnerCenter.X?-1:1;}
                else if(Shot.type==399){if(Shot.timeLeft>3)Shot.timeLeft=3;Shot.direction=centerX<Shot.Center.X?-1:1;}
                return true;
            }
        }
        private sealed class ReceiverFacts
        {
            // Other NPC types/centers are frozen alongside the existing
            // obstacle identity/body lease. The selected body comes from its
            // actual future timeline, never a future read of Main.npc.
            internal readonly int[] Types=new int[Main.maxNPCs];internal readonly float[] Centers=new float[Main.maxNPCs];
            internal ReceiverFacts(){for(int i=0;i<Types.Length;i++){var n=Main.npc[i];Types[i]=n.type;Centers[i]=n.Center.X+n.netOffset.X;}}
        }
        private static Projectile[] Capture(HostAttackObstacles root)
        {
            var all=new List<Projectile>();var fire=new List<Projectile>();var ale=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && Family(p.type)){all.Add(p);if(p.type==669)ale.Add(p);else fire.Add(p);}
            root.CaptureProjectileFamily(fire.ToArray(),399,402);root.CaptureProjectileFamily(ale.ToArray(),669,669);return all.ToArray();
        }
        private static List<Member> Prepare(Projectile[] scene,Projectile[] source,HostAttackObstacles root,Player player,NpcTrajectory path,ReceiverFacts facts)
        {
            var rows=new List<Member>();foreach(var p in scene){bool own=false;if(source!=null)foreach(var q in source)if(ReferenceEquals(p,q)){own=true;break;}var copy=Copy(p);rows.Add(new Member{Shot=copy,Receive=copy.owner==Main.myPlayer?root.ForkForSpawn(copy):null,Source=own,OwnerCenter=player.Center,Selected=path.Identity.Slot,SelectedType=path.Identity.Type,Facts=facts});}return rows;
        }
        private static AttackContact Receive(Member m,NpcTrajectory path,int tick,int outer,int sub,Vector2 aim)
        {
            var p=m.Shot;if(m.Receive==null || !p.friendly || p.hostile || p.damage<=0)return null;m.SelectedDamage=false;m.SelectedCenterX=path[tick].OwnerBounds.CenterX;
            bool passed=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true,onHit:m.Hit,beforeHitPenetration:m.Penetration);
            p.penetrate=m.Receive.Remaining;if(!passed && p.penetrate!=0){m.Reflected=true;p.active=false;}
            return m.Source && m.SelectedDamage?AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,m.Random?AttackConfidence.Representative:AttackConfidence.Conditional):null;
        }
        private static bool Death(Member m,DeathState death,List<Member> rows,HostProjectileBirthSlots slots,NpcTrajectory path,PredictionTerrain terrain,int tick,int outer,int sub,Vector2 aim,out AttackContact contact)
        {
            contact=null;var p=m.Shot;
            if(p.type==669)
            {
                // The same receiver retains ordinary owner writes. Kill does
                // not clear immunity or reconstruct a fresh damage budget.
                contact=Receive(m,path,tick,outer,sub,aim);return true;
            }
            if(p.type!=399 || p.owner!=Main.myPlayer)return true;
            // From this point all future shared-history facts can depend on
            // legal random flames, even if the parent was an ambient member.
            foreach(var member in rows)if(member.Source)member.Random=true;
            // One legal representative, never a promise that six random
            // native births repeat these values. Observed children use Copy.
            for(int i=0;i<6;i++)
            {
                int slot;if(!slots.TryAllocate(out slot))return false;Vector2 v=FlameVelocity(death.Velocity,35,(i-3)*4,40,-8+i*2);var q=new Projectile();q.SetDefaults(400+i%3);q.active=true;q.owner=p.owner;q.whoAmI=slot;q.Center=death.Center+v;q.velocity=v;q.damage=(int)((double)death.Damage*.5);q.knockBack=0;
                bool wet;byte kind;if(!terrain.ProjectileWet(q.Center.X,q.Center.Y,q.width,q.height,out wet,out kind,true))return false;q.wet=wet;
                rows.Add(new Member{Shot=q,Receive=m.Receive.ForkForSpawn(q),Source=m.Source,Random=true,OwnerCenter=m.OwnerCenter,Selected=m.Selected,SelectedType=m.SelectedType,Facts=m.Facts});
            }
            return true;
        }
        private static bool Outer(List<Member> rows,HostProjectileBirthSlots slots,NpcTrajectory path,PredictionTerrain terrain,int tick,int outer,Vector2 aim,out AttackContact contact)
        {
            contact=null;int after=-1;
            for(int visit=0;visit<1000;visit++)
            {
                Member m=null;foreach(var q in rows)if(q.Shot.active && q.Shot.whoAmI>after && (m==null || q.Shot.whoAmI<m.Shot.whoAmI))m=q;if(m==null)return true;var p=m.Shot;after=p.whoAmI;
                for(int sub=0;sub<=p.extraUpdates;sub++)
                {
                    m.Rows=rows;m.Slots=slots;m.Path=path;m.Terrain=terrain;m.Tick=tick;m.Outer=outer;m.Sub=sub;m.Aim=aim;m.DeathAvailable=true;m.DeathContact=null;
                    bool killed;DeathState death;bool available=Motor(p,terrain,out killed,out death,m.OnKill);contact=m.DeathContact;if(contact!=null)return true;if(!available || !m.DeathAvailable)return false;
                    if(p.active)
                    {
                        contact=Receive(m,path,tick,outer,sub,aim);if(contact!=null)return true;
                        if(m.Reflected)break;
                        Finish(p,out killed,out death,m.OnKill);contact=m.DeathContact;if(contact!=null)return true;if(!m.DeathAvailable)return false;
                    }
                    if(!p.active){if(!m.Reflected)slots.Release(p.whoAmI);break;}
                }
            }
            return false;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            if(!Handles(ammo.Projectile) || ammo.Damage<=0 || delay<0 || age-delay<0 || age+1>=timeline.Count)return null;
            obstacles.CaptureCursorMapping(player);var scene=Capture(obstacles);var facts=new ReceiverFacts();var pool=obstacles.CaptureBirthSlots();var body=Terraria.ID.ContentSamples.ProjectilesByType[ammo.Projectile];
            var ballistic=AttackBallisticCandidates.Generate(timeline,age,origin.X,origin.Y,ammo.Speed,body.extraUpdates+1,false,0,Math.Max(body.width,body.height)*.5f);
            var directions=new Vector2[ballistic.Length+33];int count=0;
            for(int seed=0;seed<directions.Length;seed++)
            {
                var b=timeline[age+1].ProjectileReceiveBounds;int fallback=seed-ballistic.Length;
                // This shares only the dry candidate arithmetic with 711;
                // bottle fuse, random children and ordinary owner immunity
                // remain in this family's exact replay.
                Vector2 aim=seed<ballistic.Length?origin+new Vector2(ballistic[seed].X,ballistic[seed].Y)*10000:fallback==0?new Vector2(b.CenterX,b.CenterY):origin+((fallback-1)*MathHelper.TwoPi/32).ToRotationVector2()*10000;
                var screen=aim-Main.screenPosition;if(player.gravDir==-1)screen.Y=Main.screenHeight-screen.Y;Vector2 cursor=new Vector2((int)screen.X+Main.screenPosition.X,player.gravDir==-1?Main.screenPosition.Y+Main.screenHeight-(int)screen.Y:(int)screen.Y+Main.screenPosition.Y);
                // NewProjectile10897 clips only AI001 components. These
                // AI068 openers retain the selected hand speed unchanged.
                var v=cursor-origin;float length=(float)Math.Sqrt(v.X*v.X+v.Y*v.Y);if(length==0)v=Vector2.UnitX*player.direction*ammo.Speed;else v*=ammo.Speed/length;
                bool duplicate=false;for(int i=0;i<count;i++)if(directions[i]==v){duplicate=true;break;}if(duplicate)continue;directions[count++]=v;
                obstacles.BeginCandidate();var rows=Prepare(scene,null,obstacles,player,timeline,facts);var slots=pool.Copy();bool valid=true;
                for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,slots,timeline,terrain,age-delay+wait,wait,aim,out ignored)){valid=false;break;}}if(!valid)continue;
                int slot;if(!slots.TryAllocate(out slot))return null;var q=new Projectile();q.SetDefaults(ammo.Projectile);q.active=true;q.whoAmI=slot;q.owner=player.whoAmI;q.Center=origin;q.velocity=v;q.damage=ammo.Damage;q.knockBack=player.HeldItem.knockBack;
                bool randomHistory=false;foreach(var member in rows)if(member.Random){randomHistory=true;break;}
                bool wet;byte kind;if(!terrain.ProjectileWet(origin.X,origin.Y,q.width,q.height,out wet,out kind,true))return null;q.wet=wet;rows.Add(new Member{Shot=q,Receive=obstacles.ForkForSpawn(q),Source=true,Random=randomHistory,OwnerCenter=player.Center,Selected=timeline.Identity.Slot,SelectedType=timeline.Identity.Type,Facts=facts});
                for(int step=1;step<=Math.Min(120,timeline.Count-age-1);step++){AttackContact contact;if(!Outer(rows,slots,timeline,terrain,age+step,delay+step,aim,out contact))break;if(contact!=null)return contact;}
            }
            return null;
        }
        internal static int PreferredRange(Player player,AttackAmmoSnapshot ammo,NPC target)
        {
            var delta=target.Center-player.RotatedRelativePoint(player.MountedCenter);int updates=Terraria.ID.ContentSamples.ProjectilesByType[ammo.Projectile].extraUpdates+1;
            return AttackBallisticCandidates.PreferredWindow(delta.X,delta.Y,ammo.Speed,updates,false,0,(float)Math.Sqrt(target.width*target.width+target.height*target.height)*.5f+10);
        }
        // Explicit observed-parent/child oracle only; autonomous flight never
        // acquires input authority. Unknown future Molotov births stay random.
        // A mid-projectile-array snapshot has no current-slot epoch here:
        // replaying lower slots would invent a second update, so reject it.
        internal static AttackContact KnownFamily(Player player,Projectile[] source,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            if(source==null || source.Length==0 || clock.LaunchDelay!=0 || clock.Phase==HostAttackPhase.Projectiles)return null;foreach(var p in source)if(p==null || !p.active || !Family(p.type) || p.owner!=player.whoAmI)return null;
            var scene=Capture(obstacles);obstacles.BeginCandidate();var rows=Prepare(scene,source,obstacles,player,timeline,new ReceiverFacts());var slots=obstacles.CaptureBirthSlots().Copy();var aim=source[0].Center+source[0].velocity*1000;
            for(int step=0;step<Math.Min(120,timeline.Count-clock.FirstTick);step++){AttackContact contact;if(!Outer(rows,slots,timeline,terrain,clock.FirstTick+step,step+1,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
    }
}
