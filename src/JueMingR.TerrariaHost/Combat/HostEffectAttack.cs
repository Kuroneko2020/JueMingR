using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal static class HostEffectAttack
    {
        internal static bool Handles(int type){return HostOrnamentAttack.Handles(type) || HostInfernoAttack.Handles(type) || HostPaperAirplaneAttack.Handles(type) || HostRaySpellAttack.Handles(type) || HostBlizzardAttack.Handles(type) || HostFlareAttack.Handles(type) || HostIceRodAttack.Handles(type) || HostAi29SpellAttack.Handles(type) || HostSkyFractureAttack.Handles(type) || HostNoteAttack.Handles(type) || HostNebulaBlazeAttack.Handles(type) || HostAttachedShotAttack.Handles(type) || HostBottleAttack.Handles(type) || HostStreamAttack.Handles(type) || HostThornAttack.Handles(type) || HostBetsyFlameAttack.Handles(type) || HostElectrosphereAttack.Handles(type) || HostBarrelAttack.Handles(type) || HostLaserPaintAttack.Handles(type) || HostDartAttack.Handles(type) || HostWaspBatAttack.Handles(type) || HostBeeGunAttack.Handles(type) || HostFlameAttack.Handles(type) || HostPoisonCloudAttack.Handles(type) || HostGemStaffAttack.Handles(type) || HostFiniteShotAttack.Handles(type) || HostHarpoonAttack.Handles(type) || HostExplosiveAttack.Handles(type) || HostSpecialArrowAttack.Handles(type) || HostPulseAttack.Handles(type) || HostFiniteThrowAttack.Handles(type) || HostCrystalAttack.Handles(type) || HostMagicBounceAttack.Handles(type) || type==207 || type==297 || type==36;}
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay=0,HostAttackPlayerFrame? preparedPlayer=null)
        {
            if(HostOrnamentAttack.Handles(ammo.Projectile))return HostOrnamentAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostInfernoAttack.Handles(ammo.Projectile))return HostInfernoAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostPaperAirplaneAttack.Handles(ammo.Projectile))return HostPaperAirplaneAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostRaySpellAttack.Handles(ammo.Projectile))return HostRaySpellAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostBlizzardAttack.Handles(ammo.Projectile))return HostBlizzardAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay,preparedPlayer??HostAttackPlayerFrame.Capture(player,timeline),preparedPlayer.HasValue && !beforeNpc);
            if(HostFlareAttack.Handles(ammo.Projectile))return HostFlareAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostIceRodAttack.Handles(ammo.Projectile))return HostIceRodAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay,preparedPlayer??HostAttackPlayerFrame.Capture(player,timeline),preparedPlayer.HasValue && !beforeNpc);
            if(HostAi29SpellAttack.Handles(ammo.Projectile))return HostAi29SpellAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostSkyFractureAttack.Handles(ammo.Projectile))return HostSkyFractureAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostNoteAttack.Handles(ammo.Projectile))return HostNoteAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostNebulaBlazeAttack.Handles(ammo.Projectile))return HostNebulaBlazeAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostAttachedShotAttack.Handles(ammo.Projectile))return HostAttachedShotAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostBottleAttack.Handles(ammo.Projectile))return HostBottleAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostStreamAttack.Handles(ammo.Projectile))return HostStreamAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostThornAttack.Handles(ammo.Projectile))return HostThornAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostBetsyFlameAttack.Handles(ammo.Projectile))return HostBetsyFlameAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostElectrosphereAttack.Handles(ammo.Projectile))return HostElectrosphereAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostBarrelAttack.Handles(ammo.Projectile))return HostBarrelAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostLaserPaintAttack.Handles(ammo.Projectile))return HostLaserPaintAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostDartAttack.Handles(ammo.Projectile))return HostDartAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostWaspBatAttack.Handles(ammo.Projectile))return HostWaspBatAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostBeeGunAttack.Handles(ammo.Projectile))return HostBeeGunAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostFlameAttack.Handles(ammo.Projectile))return HostFlameAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostPoisonCloudAttack.Handles(ammo.Projectile))return HostPoisonCloudAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostGemStaffAttack.Handles(ammo.Projectile))return HostGemStaffAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostFiniteShotAttack.Handles(ammo.Projectile))return HostFiniteShotAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostScourgeAttack.Handles(ammo.Projectile))return HostScourgeAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostHarpoonAttack.Handles(ammo.Projectile))return HostHarpoonAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostExplosiveAttack.Handles(ammo.Projectile))return HostExplosiveAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostSpecialArrowAttack.Handles(ammo.Projectile))return HostSpecialArrowAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostPulseAttack.Handles(ammo.Projectile))return HostPulseAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostFiniteThrowAttack.Handles(ammo.Projectile))return HostFiniteThrowAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostCrystalAttack.Handles(ammo.Projectile))return HostCrystalAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(HostMagicBounceAttack.Handles(ammo.Projectile))return HostMagicBounceAttack.Solve(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            if(ammo.Projectile==36)return Bounce(player,ammo,timeline,origin,age,beforeNpc,terrain,obstacles,delay);
            var sample=Terraria.ID.ContentSamples.ProjectilesByType[ammo.Projectile];var receive=HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,beforeNpc,!beforeNpc);
            // A player bullet may directly hit an admitted target that native
            // homing does not chase (notably an immortal training dummy).
            var target=Main.npc[timeline.Identity.Slot];if(target.dontTakeDamage)return null;
            int updates=sample.extraUpdates+1,limit=Math.Min(120,timeline.Count-age-1);
            bool spectre=ammo.Projectile==297;var candidates=CaptureCandidates();var directions=new Vector2[9];int directionCount=0;
            // Native autonomous selection is an independent stage. Read its
            // currently known candidates; do not steer a born bullet or claim
            // that a different native target must remain the selected NPC.
            for(int seed=0;seed<9;seed++)
            {
                var seedBox=timeline[Math.Min(timeline.Count-1,age+1+seed*4)].Bounds;Vector2 aim=origin+(new Vector2(seedBox.CenterX,seedBox.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction)*10000;
                Vector2 direction=(aim-origin).SafeNormalize(Vector2.UnitX);bool duplicate=false;for(int i=0;i<directionCount;i++)if(directions[i]==direction){duplicate=true;break;}if(duplicate)continue;directions[directionCount++]=direction;
                Vector2 velocity=direction*ammo.Speed;if(sample.aiStyle==1)while(velocity.X>=16 || velocity.X<=-16 || velocity.Y>=16 || velocity.Y< -16)velocity*=.97f;
                float initialSpeed=velocity.Length();Vector2 center=origin;int locked=-1;
                for(int step=0;step<limit;step++)for(int sub=0;sub<updates;sub++)
                {
                    if(sub==0 && !obstacles.BeforeNpc(age+step+1,delay+step+1,center.X,center.Y)){step=limit;break;}
                    int tick=age+step+1;var body=timeline[tick].Bounds;
                    if(spectre || locked<0)
                    {
                        float nearest=spectre?400:300;if(spectre)locked=-1;
                        for(int i=0;i<candidates.Length;i++)
                        {
                            var candidate=candidates[i];if(!candidate.Chaseable)continue;int slot=candidate.Slot;
                            var bounds=slot==timeline.Identity.Slot?body:candidate.Bounds;float distance=Math.Abs(center.X-bounds.CenterX)+Math.Abs(center.Y-bounds.CenterY);
                            // Spectre additionally applies an Euclidean gate
                            // before its Manhattan tie/order comparison.
                            bool clear;PredictionStop stop;var sight=spectre?new MotionRect(bounds.CenterX,bounds.CenterY,1,1):bounds;
                            if(distance<nearest && (!spectre || Vector2.Distance(center,new Vector2(bounds.CenterX,bounds.CenterY))<nearest) && terrain.CanHit(new MotionRect(center.X,center.Y,1,1),sight,out clear,out stop) && clear){locked=slot;nearest=distance;}
                        }
                    }
                    if(locked>=0)
                    {
                        var npc=Main.npc[locked];var bounds=locked==timeline.Identity.Slot?body:new MotionRect(npc.position.X,npc.position.Y,npc.width,npc.height);var delta=new Vector2(bounds.CenterX,bounds.CenterY)-center;
                        if(!npc.active || !npc.CanBeChasedBy(null,true) || npc.dontTakeDamage)locked=-1;
                        else if(spectre)velocity=(velocity*20+delta.SafeNormalize(Vector2.Zero)*6)/21;
                        else if(Math.Abs(delta.X)+Math.Abs(delta.Y)<1000)velocity=(velocity*7+delta.SafeNormalize(Vector2.UnitX)*initialSpeed)/8;
                    }
                    Vector2 old=center;if(!HostProjectileEnvironment.Dry(sample,terrain,old.X,old.Y,velocity.X)){step=limit;break;}
                    Vector2 half=new Vector2(sample.width,sample.height)*.5f,position=old-half,wetVelocity;bool hit=false;
                    if(sample.tileCollide && (!terrain.ProjectileTileMotion(sample,ref position,ref velocity,false,false,out wetVelocity,out hit) || hit)){step=limit;break;}center=position+half+velocity;
                    if(!obstacles.PassOuter(step*updates+sub+1,tick,delay+step+1,center.X,center.Y)){step=limit;break;}
                    if(receive.Allows(step+delay+1))
                    {
                        var contact=AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,center.X,center.Y,sample.width,sample.height,AttackConfidence.Representative);if(contact!=null)return contact;
                    }
                }
            }
            return null;
        }
        private readonly struct HomingCandidate
        {
            internal readonly int Slot;internal readonly MotionRect Bounds;internal readonly bool Chaseable;
            internal HomingCandidate(NPC npc,int slot){Slot=slot;Bounds=new MotionRect(npc.position.X,npc.position.Y,npc.width,npc.height);Chaseable=npc.CanBeChasedBy(null);}
        }
        private static HomingCandidate[] CaptureCandidates()
        {
            // Native autonomous targeting is represented by the current known
            // scene. Freeze qualification once per solve; nine angular seeds
            // do not independently reread the identical full live NPC set.
            var entries=new System.Collections.Generic.List<HomingCandidate>();
            for(int slot=0;slot<Main.maxNPCs;slot++)if(Main.npc[slot].active)entries.Add(new HomingCandidate(Main.npc[slot],slot));
            return entries.ToArray();
        }
        private static AttackContact Bounce(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            var sample=Terraria.ID.ContentSamples.ProjectilesByType[36];var receive=HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,beforeNpc,!beforeNpc);int updates=sample.extraUpdates+1,limit=Math.Min(sample.timeLeft,(timeline.Count-age-1)*updates);
            // One ordinary MeteorShot reflection consumes one penetration.
            // Finite angular representatives can find a first reflected route;
            // its body still damages only at discrete native movement endpoints.
            for(int seed=-1;seed<32;seed++)
            {
                var b=timeline[Math.Min(timeline.Count-1,age+1)].ProjectileReceiveBounds;Vector2 unit=seed<0?(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):(seed*MathHelper.TwoPi/32).ToRotationVector2();Vector2 aim=origin+unit*10000,velocity=unit*ammo.Speed,center=origin;
                while(velocity.X>=16 || velocity.X<=-16 || velocity.Y>=16 || velocity.Y< -16)velocity*=.97f;int remaining=sample.penetrate;
                for(int k=1;k<=limit;k++)
                {
                    if((k-1)%updates==0 && !obstacles.BeforeNpc(age+(k-1)/updates+1,delay+(k-1)/updates+1,center.X,center.Y))break;
                    Vector2 half=new Vector2(sample.width,sample.height)*.5f,position=center-half,lastVelocity=velocity,wetVelocity;bool hit;
                    if(!HostProjectileEnvironment.Dry(sample,terrain,center.X,center.Y,velocity.X) || !terrain.ProjectileTileMotion(sample,ref position,ref velocity,false,false,out wetVelocity,out hit))break;
                    if(hit){if(remaining<=1)break;remaining--;if(velocity.X!=lastVelocity.X)velocity.X=-lastVelocity.X;if(velocity.Y!=lastVelocity.Y)velocity.Y=-lastVelocity.Y;}
                    center=position+half+velocity;int step=(k-1)/updates,tick=age+step+1;
                    if(!obstacles.PassOuter(k,tick,delay+step+1,center.X,center.Y,remaining))break;remaining=obstacles.Remaining;
                    if(!receive.Allows(step+delay+1))continue;
                    var contact=AttackIntercept.BodyContact(timeline,tick,(k-1)%updates,aim.X,aim.Y,center.X,center.Y,sample.width,sample.height,AttackConfidence.Conditional);if(contact!=null)return contact;
                }
            }
            return null;
        }
    }
}
