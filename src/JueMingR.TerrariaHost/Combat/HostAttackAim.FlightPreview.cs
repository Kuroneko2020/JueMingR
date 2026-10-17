using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackAim
    {
        private readonly PredictionTerrain flightPreviewTerrain=new PredictionTerrain();
        private bool flightPass,flightPassReady,flightPassLive;
        private NpcTrajectory flightPassTimeline;
        private HostAttackPhase flightPassPhase;
        private FlightPreview flightPassResult;
        private void BeginFlightPreparation(bool beforeNpc)
        {EndFlightPreparation();flightPass=!beforeNpc;}
        private void EndFlightPreparation()
        {flightPass=flightPassReady=flightPassLive=false;flightPassTimeline=null;flightPassResult=null;}
        private bool FlightPreparationMatches(NpcTrajectory timeline,HostAttackPhase phase,FlightPreview flight)
        {return flightPass && flightPassReady && ReferenceEquals(flightPassTimeline,timeline) && flightPassPhase==phase && ReferenceEquals(flightPassResult,flight);}
        // Selection and final presentation run synchronously without a native
        // actor/input action between them. Share a replay only in that scope,
        // for the exact same immutable path instance and native phase. The
        // presentation finally closes the scope; no future tick, different
        // candidate, or changed source can inherit a no-contact verdict.
        private FlightPreview ReadFlight(Player player,NpcTrajectory timeline,HostAttackPhase phase,out bool live)
        {
            if(flightPass && flightPassReady && ReferenceEquals(flightPassTimeline,timeline) && flightPassPhase==phase)
            {live=flightPassLive;return flightPassResult;}
            var result=ReadFlightCore(player,timeline,phase,out live);
            // Freeze the real replay reader once after all family candidates
            // have been visited. Same-pass memoization and presentation copies
            // keep this lease; OnlyFlight has never used plan.Terrain.
            if(result!=null)result.Terrain=flightPreviewTerrain.FreezeCells();
            if(flightPass){flightPassReady=true;flightPassTimeline=timeline;flightPassPhase=phase;flightPassLive=live;flightPassResult=result;}
            return result;
        }
        // A sealed AI013/phaseblade flight can be observed after enabling the path or
        // returning to its weapon. This does not manufacture a birth receipt
        // or grant any cursor consumer authority over that existing attack.
        private FlightPreview ReadFlightCore(Player player,NpcTrajectory timeline,HostAttackPhase phase,out bool live)
        {
            live=false;if(BalancedWeapon)return null;
            bool phaseblade=HostPhasebladeAttack.Handles(player.HeldItem.shoot);
            bool gem=HostGemStaffAttack.Handles(player.HeldItem.shoot);
            bool attached=HostAttachedShotAttack.Handles(player.HeldItem.shoot);
            bool arc=HostArcSwingAttack.Weapon(player.HeldItem.type);
            bool ice=HostIceRodAttack.Handles(player.HeldItem.shoot);
            bool paper=HostPaperAirplaneAttack.Handles(player.HeldItem.shoot);
            bool ornament=HostOrnamentAttack.Handles(player.HeldItem.shoot);
            bool inferno=player.HeldItem.type==1445 && player.HeldItem.shoot==295;
            bool sonic=player.HeldItem.type==3827 && player.HeldItem.shoot==684;
            bool book=player.HeldItem.type==3852 && player.HeldItem.shoot==712;
            bool skull=player.HeldItem.type==1313 && player.HeldItem.shoot==837;
            var finiteFamily=FiniteFlightFamilyFor(player.HeldItem);
            bool arrows=player.HeldItem.useAmmo==Terraria.ID.AmmoID.Arrow;
            bool bullets=player.HeldItem.useAmmo==Terraria.ID.AmmoID.Bullet;
            bool homing=HostHomingEffectAttack.Handles(player.HeldItem.shoot) || player.HeldItem.type==4953;
            int spearChild=HostMeleeAttack.DerivedSpearChild(player.HeldItem.shoot);
            bool spear=HostMeleeAttack.KnownSpearParent(player.HeldItem.shoot) || HostMeleeAttack.KnownWhipParent(player.HeldItem.shoot);
            if(!phaseblade && !gem && !attached && !arc && !ice && !paper && !ornament && !inferno && !sonic && !book && !skull && finiteFamily==FiniteFlightFamily.None && !arrows && !bullets && !homing && !spear && !HostHarpoonAttack.Handles(player.HeldItem.shoot))return null;
            FlightPreview best=null;flightPreviewTerrain.Reset();var clock=new HostAttackClock(timeline,phase);
            if(finiteFamily!=FiniteFlightFamily.None)return ReadFiniteFlightFamily(finiteFamily,player,timeline,clock,out live);
            if(arrows || bullets || homing)
            {
                // Eventide can leave both ordinary arrows and the special
                // streak alive. Each solver receives its real motor family;
                // the earliest observed receive wins, never a new hand permit.
                FlightPreview observed=null;
                var sources=new List<Projectile>();
                if(arrows || bullets)
                {
                    // One sealed volley keeps ordinary ammo, its finite damage
                    // children and weapon auxiliaries in native slot order.
                    // Observation grants no held or hand cursor permission.
                    bool mixedStreak=false;foreach(var shot in Main.projectile)
                    {if(shot!=null && shot.active && shot.owner==player.whoAmI){mixedStreak|=shot.type==932;if(HostHeldAmmoFamilyAttack.Family(shot.type) && shot.damage>0 && !shot.hostile)sources.Add(shot);}}
                    bool charm=player.accHarpyCharm;
                    foreach(var source in sources)if(source.arrow && new Projectile.AI_001_Features(source.ai[2]).HarpyCharmed)charm=true;
                    if(charm && !mixedStreak)
                    {
                        var charmed=new List<Projectile>();bool completeHarpy=true;foreach(var shot in Main.projectile)if(HostHarpyAttack.FamilyForObservation(shot,player)){charmed.Add(shot);completeHarpy&=HostHeldAmmoFamilyAttack.Family(shot.type);}
                        // When the shared world owns every observable Harpy
                        // member, it also owns non-arrow retirement/slot reuse.
                        // The old arrow-only world cannot supply an Earlier:
                        // a retiring low bullet otherwise stays occupied and
                        // sends a Kill child to a high slot one world early.
                        // Keep the old reader for its actual out-of-batch
                        // families rather than silently hiding those members.
                        if(charmed.Count>0 && !completeHarpy)
                        {
                            live=true;var primary=charmed[0];var receivers=new HostAttackObstacles(player,primary,timeline,0,clock.BeforeNpc,clock.NextWorld);HostHarpyAttack.State state;
                            var contact=HostHarpyAttack.KnownPrepared(player,charmed.ToArray(),timeline,clock,flightPreviewTerrain,receivers,out state);
                            if(contact!=null)observed=new FlightPreview(player,primary,contact,arc:receivers,harpy:state);
                        }
                    }
                    if(sources.Count>0)
                    {
                        live=true;var primary=sources[0];var receivers=new HostAttackObstacles(player,primary,timeline,0,clock.BeforeNpc,clock.NextWorld);
                        var contact=HostHeldAmmoFamilyAttack.KnownFamily(player,sources.ToArray(),timeline,clock,flightPreviewTerrain,receivers);
                        if(Earlier(contact,observed?.Contact))observed=new FlightPreview(player,primary,contact,arc:receivers);
                    }
                }
                if(homing)
                {
                    foreach(int type in new[]{931,932,1116,1122})
                    {
                        // 932 was already replayed with the complete arrow
                        // scene. An isolated earlier hit must not override it.
                        if(type==932 && (arrows || bullets))continue;
                        sources.Clear();foreach(var shot in Main.projectile)
                            if(shot!=null && shot.active && shot.owner==player.whoAmI && shot.type==type && shot.damage>0 && !shot.hostile)sources.Add(shot);
                        if(sources.Count==0)continue;live=true;var primary=sources[0];var receivers=new HostAttackObstacles(player,primary,timeline,0,clock.BeforeNpc,clock.NextWorld);
                        var contact=HostHomingEffectAttack.KnownFamily(player,sources.ToArray(),timeline,clock,flightPreviewTerrain,receivers);
                        if(Earlier(contact,observed?.Contact))observed=new FlightPreview(player,primary,contact,arc:receivers);
                    }
                }
                return observed;
            }
            if(spear)
            {
                // One captured finite family follows the actual pool order.
                // Sealed parent/child sources grant no new cursor authority;
                // the next hypothetical hand remains a separate candidate.
#if DEBUG
                HostMeleeAttack.SpearFlightPoolScans++;
#endif
                var sources=new List<Projectile>();bool snowflake=false;
                foreach(var shot in Main.projectile)
                {if(shot==null || !shot.active || shot.owner!=player.whoAmI)continue;if(player.HeldItem.shoot==342 && shot.type==344)snowflake=true;if((shot.type==player.HeldItem.shoot || shot.type==spearChild) && shot.damage>0 && shot.friendly && !shot.hostile)sources.Add(shot);}
                live=sources.Count>0;if(!live || snowflake)return null;var primary=sources[0];
                var receivers=new HostAttackObstacles(player,primary,timeline,0,clock.BeforeNpc,clock.NextWorld);HostMeleeAttack.SpearFlightState state;
                var contact=HostMeleeAttack.KnownSpearFamily(player,sources.ToArray(),player.HeldItem.shoot,timeline,clock,flightPreviewTerrain,receivers,out state);
                return contact==null?null:new FlightPreview(player,primary,contact,arc:receivers,spear:state);
            }
            if(ornament || inferno || sonic || book || skull)
            {
                // Existing bodies keep their genuine birth/AI clocks. In
                // particular, Inferno's current child can consume the owner
                // receive window that makes the next hand unproductive.
                // Observe that live contact separately, without input control.
                var sources=new List<Projectile>();
                for(int slot=0;slot<Main.maxProjectiles;slot++)
                {var shot=Main.projectile[slot];if(shot!=null && shot.active && shot.owner==player.whoAmI && (ornament?HostOrnamentAttack.Family(shot.type):skull?HostSkullAttack.Handles(shot.type):book?HostBookStaffAttack.Handles(shot.type):sonic?HostSonicBoomAttack.Handles(shot.type):HostInfernoAttack.Handles(shot.type)) && shot.damage>0 && shot.friendly && !shot.hostile)sources.Add(shot);}
                live=sources.Count>0;if(!live)return null;var primary=sources[0];
                var receivers=new HostAttackObstacles(player,primary,timeline,0,clock.BeforeNpc,clock.NextWorld);
                var contact=ornament?HostOrnamentAttack.KnownFamily(player,sources.ToArray(),timeline,clock,flightPreviewTerrain,receivers):skull?HostSkullAttack.KnownFamily(player,sources.ToArray(),timeline,clock,flightPreviewTerrain,receivers):book?HostBookStaffAttack.KnownFamily(player,sources.ToArray(),timeline,clock,flightPreviewTerrain,receivers):sonic?HostSonicBoomAttack.KnownFamily(player,sources.ToArray(),timeline,clock,flightPreviewTerrain,receivers):HostInfernoAttack.KnownFamily(player,sources.ToArray(),timeline,clock,flightPreviewTerrain,receivers);
                return contact==null?null:new FlightPreview(player,primary,contact,arc:receivers);
            }
            if(ice)
            {
                // A failed placement retains a damaging autonomous tail. The
                // finite family supplies its actual clock and placement state;
                // observation grants no cursor control to an existing bolt.
                var sources=new List<Projectile>();
                for(int slot=0;slot<Main.maxProjectiles;slot++)
                {var shot=Main.projectile[slot];if(shot!=null && shot.active && shot.owner==player.whoAmI && shot.type==80 && shot.damage>0 && shot.friendly && !shot.hostile)sources.Add(shot);}
                live=sources.Count>0;if(!live)return null;var primary=sources[0];
                var receivers=new HostAttackObstacles(player,primary,timeline,0,clock.BeforeNpc,clock.NextWorld);
                var contact=HostIceRodAttack.Known(player,sources.ToArray(),timeline,clock,flightPreviewTerrain,receivers,HostAttackPlayerFrame.Capture(player,timeline));
                return contact==null?null:new FlightPreview(player,primary,contact,arc:receivers);
            }
            if(arc)
            {
                var sources=new List<Projectile>();
                for(int slot=0;slot<Main.maxProjectiles;slot++)
                {
                    var shot=Main.projectile[slot];if(shot==null || !shot.active || shot.owner!=player.whoAmI || shot.damage<=0 || !shot.friendly || shot.hostile || !ArcFlightRole(player.HeldItem.type,shot.type) || shot.type==1128 && shot.ai[1]>=1)continue;sources.Add(shot);
                }
                live=sources.Count>0;if(!live)return null;
                var primary=sources[0];var receivers=new HostAttackObstacles(player,primary,timeline,0,clock.BeforeNpc,clock.NextWorld);
                var contact=HostArcSwingAttack.KnownFamily(player,sources.ToArray(),timeline,clock,flightPreviewTerrain,receivers,HostAttackPlayerFrame.Capture(player,timeline));
                return contact==null?null:new FlightPreview(player,primary,contact,arc:receivers);
            }
            if(attached)
            {
                // Native card recall also visits retained inactive cards.
                // Only an active held-role shot owns a visible flight mark;
                // the other captured local stages supply causal dependencies.
                var observed=new List<Projectile>();Projectile primary=null;
                for(int slot=0;slot<Main.maxProjectiles;slot++)
                {
                    var shot=Main.projectile[slot];if(shot==null || shot.owner!=player.whoAmI || !HostAttachedShotAttack.Handles(shot.type) && shot.type!=953 || !shot.active && shot.type!=1024)continue;
                    observed.Add(shot);if(shot.active && shot.type==player.HeldItem.shoot && shot.damage>0 && shot.friendly && !shot.hostile && primary==null)primary=shot;
                }
                live=primary!=null;if(!live)return null;
                HostAttachedShotAttack.FlightState state;var contact=HostAttachedShotAttack.KnownFamily(player,observed.ToArray(),timeline,clock,flightPreviewTerrain,out state);
                return contact==null?null:new FlightPreview(player,primary,contact,attached:state);
            }
            if(gem)
            {
                // One real pool scan captures local gem competition. Only
                // the held role contributes a mark; other local gem roles
                // remain ordered owner-immunity dependencies, not inputs.
#if DEBUG
                HostGemStaffAttack.FlightPoolScans++;
#endif
                var observed=new List<Projectile>();Projectile primary=null;
                for(int slot=0;slot<Main.maxProjectiles;slot++)
                {var shot=Main.projectile[slot];if(shot==null || !shot.active || shot.owner!=player.whoAmI || !HostGemStaffAttack.Handles(shot.type))continue;observed.Add(shot);if(shot.type==player.HeldItem.shoot && shot.friendly && !shot.hostile && primary==null)primary=shot;}
                live=primary!=null;if(!live)return null;
                HostGemStaffAttack.FlightState state;var contact=HostGemStaffAttack.KnownFamily(player,observed.ToArray(),timeline,clock,flightPreviewTerrain,out state);
                return contact==null?null:new FlightPreview(player,primary,contact,state);
            }
            for(int i=0;i<Main.maxProjectiles;i++)
            {
                var shot=Main.projectile[i];if(shot==null || !shot.active || shot.owner!=player.whoAmI || shot.type!=player.HeldItem.shoot)continue;
                live=true;HostAttackObstacles receivers;
                AttackContact contact;
                if(paper)
                {receivers=new HostAttackObstacles(player,shot,timeline,0,clock.BeforeNpc,clock.NextWorld);contact=HostPaperAirplaneAttack.Known(player,shot,timeline,clock,flightPreviewTerrain,receivers);}
                else if(phaseblade)
                {receivers=new HostAttackObstacles(player,shot,timeline,0,clock.BeforeNpc,clock.NextWorld);contact=HostPhasebladeAttack.Known(player,shot,timeline,clock,flightPreviewTerrain,receivers);}
                else contact=HostHarpoonAttack.Known(player,shot,timeline,clock,flightPreviewTerrain,out receivers);
                if(contact!=null && Earlier(contact,best?.Contact))best=new FlightPreview(player,shot,contact,arc:receivers);
                if(best!=null && best.Contact.Tick==clock.FirstTick && best.Contact.Subupdate==0)break;
            }
            return best;
        }
        private static bool Earlier(AttackContact a,AttackContact b)
        {return a!=null && (b==null || a.Tick<b.Tick || a.Tick==b.Tick && a.Subupdate<b.Subupdate);}
        private static bool ArcFlightRole(int item,int type)
        {return item==368?type==982:item==1826?type==997 || type==321:item==273?type==972:item==674?type==983:item==675?type==972 || type==973:item==757?type==984 || type==985:item==1226 && (type==1126 || type==1128 || type==1129);}
        private static AttackContact OnlyFlight(PreviewPlan plan,Player player,NpcTrajectory timeline,FlightPreview flight)
        {plan.Clear();plan.SetMembers(player,player.HeldItem);plan.Timeline=timeline;plan.Flight=flight;plan.Contact=flight?.Contact;return plan.Contact;}
        private sealed class FlightPreview
        {
            internal readonly AttackContact Contact;
            internal PredictionTerrain.CellLease Terrain;
            internal bool DependenciesCurrent {get{return Terrain!=null && Terrain.Current && (gem==null || gem.DependenciesCurrent) && (attached==null || attached.DependenciesCurrent) && (arc==null || arc.Unchanged) && (harpy==null || harpy.Current);}}
            private readonly Player player;private readonly Projectile shot;private readonly int key,type,owner,time,extra,penetration;
            private readonly Vector2 position,velocity;private readonly float ai0,ai1,local0;
            private readonly bool use,wet,honey,lava,shimmer,tile;
            private readonly HostGemStaffAttack.FlightState gem;
            private readonly HostAttachedShotAttack.FlightState attached;
            private readonly HostAttackObstacles arc;
            private readonly HostMeleeAttack.SpearFlightState spear;
            private readonly HostHarpyAttack.State harpy;
            internal FlightPreview(Player player,Projectile shot,AttackContact contact,HostGemStaffAttack.FlightState gem=null,HostAttachedShotAttack.FlightState attached=null,HostAttackObstacles arc=null,HostMeleeAttack.SpearFlightState spear=null,HostHarpyAttack.State harpy=null)
            {
                this.player=player;this.shot=shot;this.gem=gem;this.attached=attached;this.arc=arc;this.spear=spear;this.harpy=harpy;Contact=contact;key=(int)shot.key;type=shot.type;owner=shot.owner;time=shot.timeLeft;extra=shot.extraUpdates;penetration=shot.penetrate;
                position=shot.position;velocity=shot.velocity;ai0=shot.ai[0];ai1=shot.ai[1];local0=shot.localAI[0];use=player.controlUseItem;wet=shot.wet;honey=shot.honeyWet;lava=shot.lavaWet;shimmer=shot.shimmerWet;tile=shot.tileCollide;
            }
            // Draw checks the bound member, never scans the projectile pool.
            internal bool Current {get{return (gem==null || gem.MembersCurrent) && (attached==null || attached.MembersCurrent) && (arc==null || arc.FamilyMembersCurrent) && (harpy==null || harpy.MembersCurrent) && (spear==null || spear.Current) && shot.active && (int)shot.key==key && shot.type==type && shot.owner==owner && shot.timeLeft==time && shot.extraUpdates==extra && shot.penetrate==penetration && shot.position==position && shot.velocity==velocity && shot.ai[0]==ai0 && shot.ai[1]==ai1 && shot.localAI[0]==local0 && player.controlUseItem==use && shot.wet==wet && shot.honeyWet==honey && shot.lavaWet==lava && shot.shimmerWet==shimmer && shot.tileCollide==tile;}}
            // A sealed-flight visual follows the display's bounded capture
            // age. The existing family member leases still cancel on member
            // retirement/phase or captured pose changes. Only the primary
            // shot's ordinary travel/time is relaxed; none of this grants a
            // hand input lease or scans the projectile pool during Draw.
            internal bool PresentationCurrent(int age)
            {return age>=0 && age<=HostCombatObservation.DisplayMaximumAge && (gem==null || gem.MembersCurrent) && (attached==null || attached.MembersCurrent) && (arc==null || arc.FamilyMembersCurrent) && (harpy==null || harpy.MembersCurrent) && (spear==null || spear.Current) && shot.active && (int)shot.key==key && shot.type==type && shot.owner==owner && shot.extraUpdates==extra && shot.penetrate==penetration && shot.ai[0]==ai0 && shot.ai[1]==ai1 && player.controlUseItem==use && shot.wet==wet && shot.honeyWet==honey && shot.lavaWet==lava && shot.shimmerWet==shimmer && shot.tileCollide==tile;}
        }
    }
}
