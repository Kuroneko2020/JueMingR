using System;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackAim
    {
        private static readonly MethodInfo previewCopy=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
        private PreviewPlan preview=new PreviewPlan();
        private AttackContact previewPresentation;
        private uint previewStep;
        internal bool PreviewDemand {get{return observation.Settings.CanRun && combat.Player!=null && combat.Player.active && !combat.Player.dead && !combat.Player.ghost && (observation.Path || observation.Options.Aim) && Eligible(combat.Player.HeldItem);}}
        internal AttackContact ExpectedPreview {get{return CurrentReceiver && PreviewDemand && previewStep==Main.GameUpdateCount && preview.Timeline!=null && preview.Timeline.Identity.Equals(observation.Selection.Target) && ReferenceEquals(preview.Timeline,observation.Prediction.Cache.Read(1)) && preview.WeaponCurrent(combat.Player)?preview.Contact:null;}}
        internal void BindPreview(AttackContact result){previewPresentation=result;}
        internal bool PreviewCurrent(AttackContact result)
        {
            var player=combat.Player;
            // Draw uses only the preparation's identity and member lease. It
            // neither searches terrain nor acquires a native input capability.
            return result!=null && ReferenceEquals(result,previewPresentation) && ReferenceEquals(result,preview.Contact) && previewStep==Main.GameUpdateCount && PreviewDemand && CurrentReceiver && result.Timeline.Identity.Equals(observation.Selection.Target) && ReferenceEquals(result.Timeline,observation.Prediction.Cache.Read(1)) && preview.MembersCurrent(player) && (!preview.Controlled || Control.PreviewCurrent(result));
        }
        private void ClearPreview(){preview.Clear();previewPresentation=null;}
        private void PreparePreview(HostAttackPhase phase)
        {
            previewPresentation=null;previewStep=Main.GameUpdateCount;
            if(!PreviewDemand || !CurrentReceiver){preview.Clear();return;}
            var timeline=observation.Prediction.Cache.Read(1);
            if(timeline==null || !timeline.Identity.Equals(observation.Selection.Target)){preview.Clear();return;}
            // The completed-world candidate and the visible preview have the
            // same input phase. Let PreparePlan revalidate/rebind that exact
            // plan instead of solving the chosen receiver a second time.
            var selectedPlan=observation.Options.ClearLine?candidatePlans[timeline.Identity.Slot*2]:null;
            if(selectedPlan!=null && selectedPlan.Prepared && !selectedPlan.Controlled && selectedPlan.Timeline!=null && selectedPlan.Timeline.Identity.Equals(timeline.Identity))preview=selectedPlan;
            var context=FaultContextFor(timeline.Identity);
            if(preview.Faults.Blocks(context)){preview.Clear();return;}
            try{PreparePlan(preview,timeline,phase);}
            catch(Exception error){PropagateFatal(error);preview.Faults.Record(context);preview.Clear();}
        }
        private AttackContact PreparePlan(PreviewPlan plan,NpcTrajectory timeline,HostAttackPhase phase)
        {
            var player=combat.Player;var item=player.HeldItem;
            if(HostPlacementAttack.Handles(item)){plan.Clear();return null;}
            int delay=HostAttackWindow.PreviewDelay(player,item);var clock=new HostAttackClock(timeline,phase);
            if(clock.Age<0 || clock.Age>1){plan.Clear();return null;}
            bool hasFlight;var flight=ReadFlight(player,timeline,phase,out hasFlight);
            // All AI013 hand roles except GolemFist refresh native dummy use
            // timers until catch. Their current flight owns the preview even
            // when it was born before observation was enabled.
            if(hasFlight && item.shoot!=481 && !HostGemStaffAttack.Handles(item.shoot) && !HostAttachedShotAttack.Handles(item.shoot) && !HostArcSwingAttack.Weapon(item.type) && !HostIceRodAttack.Handles(item.shoot))return OnlyFlight(plan,player,timeline,flight);
            if(delay<0 || clock.Age+delay>=timeline.Count-1)
            {if(!HostSwingAttack.Mixed(item))return OnlyFlight(plan,player,timeline,flight);delay=-1;}
            if(HostHeldAttack.Weapon(item.type) || HostYoyoNavigation.Weapon(item) || HostWhipAttack.Weapon(item))
            {
                plan.SetMembers(player,item);plan.Controlled=true;plan.Timeline=timeline;
                plan.Contact=Control.Preview(timeline,phase,delay);return plan.Contact;
            }
            if(item.type==3473 && Control.HasObservedSolar)
            {
                var live=Control.Preview(timeline,phase,delay,false);
                if(live!=null){plan.Clear();plan.SetMembers(player,item);plan.Controlled=true;plan.Timeline=timeline;plan.Contact=live;return live;}
                // A late native arc may have no remaining receive opportunity.
                // Its existence does not prevent the next legal Solar use:
                // Include its remaining native dummy-timer ownership and the
                // following legal hand window. Never keep the exhausted
                // arc's old contact or hypothesize an immediate new source.
                delay=Math.Max(delay,Control.SolarOpeningDelay);
                if(clock.Age+delay>=timeline.Count-1){plan.Clear();return null;}
            }
            if(Control.HasObservedPulse)
            {
                var live=Control.Preview(timeline,phase,0,false);
                if(live!=null){plan.Clear();plan.SetMembers(player,item);plan.Controlled=true;plan.Timeline=timeline;plan.Contact=live;return live;}
            }
            var captured=AttackAmmoSnapshot.Capture(player,item);
            if(captured==null && !HostSwingAttack.Handles(item))return OnlyFlight(plan,player,timeline,flight);
            var motion=NpcPredictionSource.ReadPlayer(player);var receiver=Main.npc[timeline.Identity.Slot];
            Projectile sample=null;if(captured!=null && !ContentSamples.ProjectilesByType.TryGetValue(captured.Projectile,out sample))return OnlyFlight(plan,player,timeline,flight);
            var receive=sample==null?default(HostAttackReceive):HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,phase==HostAttackPhase.BeforeNpc,phase!=HostAttackPhase.BeforeNpc);
            int immune=receiver.immune[player.whoAmI];
            bool reuse=!hasFlight && PlanInputsCurrent(plan,player,item,timeline.Identity.Slot,phase,clock.Age,delay,captured,motion,receive,immune) && AttackIntercept.Equivalent(plan.Timeline,timeline);
            if(reuse)
            {
                plan.Contact=AttackIntercept.Rebind(plan.Contact,timeline);plan.Timeline=timeline;return plan.Contact;
            }
            plan.Clear();plan.SetMembers(player,item);plan.Prepared=true;plan.Phase=phase;plan.Age=clock.Age;plan.Delay=delay;plan.PlayerMotion=motion;plan.Immune=immune;plan.AttackCooldown=player.attackCD;plan.MeleeCooldown=player.meleeNPCHitCooldown[timeline.Identity.Slot];plan.Animation=player.itemAnimation;plan.AnimationMax=player.itemAnimationMax;plan.SwingRevision=swing.Revision;plan.MeleeSpeed=player.meleeSpeed;plan.Direction=player.direction;plan.Ammo=captured;plan.Timeline=timeline;
            plan.Receive=receive;plan.Terrain.Reset();plan.EnvironmentState=new HostProjectileEnvironment.State(true);
            plan.Using=combat.Left || player.controlUseItem;
            plan.CapsuleOnly=HostSwingAttack.CapsuleOnly(player,item);
            if(HostPhasebladeAttack.Handles(item.shoot))return PreparePhasebladePreview(plan,player,item,timeline,phase,delay);
            if(HostSwingAttack.Mixed(item))return PrepareMixedSwingPreview(plan,player,item,timeline,phase,delay);
            if(HostHarpyAttack.Handles(player,captured))
            {
                // This stage owns both cooldown Player updates and the future
                // launch. Passing an already advanced private player would
                // apply motion/charm twice and move the marker off its path.
                plan.Obstacles=new HostAttackObstacles(player,sample,timeline,delay,phase==HostAttackPhase.BeforeNpc,phase!=HostAttackPhase.BeforeNpc);
                plan.Contact=AttackIntercept.WithLaunchDelay(HostHarpyAttack.Solve(player,captured,timeline,HostAttackModels.LaunchOrigin(player,item),new HostAttackClock(timeline,phase,delay),plan.Terrain,plan.Obstacles,out plan.HarpyState),delay);
                return plan.Contact;
            }
            var body=player;
            var playerWorld=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,Remix=Main.remixWorld,Expert=Main.expertMode,WallViewWidth=Main.MaxWorldViewSize.X};
            NpcPredictionSource.TryBindPlayerGravity(timeline,(long)Main.GameUpdateCount,ref motion,ref playerWorld);
            NpcPredictionSource.TryBindPlayerWall(timeline,(long)Main.GameUpdateCount,ref motion,ref playerWorld);
            // Cooldown is a future launch premise. Advance private motion and
            // preserve the live controls, RNG, timers, resources and inventory.
            if(delay>0)
            {
                for(int i=0;i<delay;i++){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,playerWorld,plan.Terrain,out stop))return OnlyFlight(plan,player,timeline,flight);}
                body=(Player)previewCopy.Invoke(player,null);HostAttackPlayerFrame.ApplyPose(body,motion);body.itemTime=0;body.itemAnimation=0;body.reuseDelay=0;
            }
            int age=clock.Age+delay;Vector2 start=HostAttackModels.LaunchOrigin(body,item);
            if(HostSwingAttack.Handles(item))
            {plan.Contact=AttackIntercept.WithLaunchDelay(swing.PreviewPrepared(body,item,timeline,new HostAttackClock(timeline,phase,delay),plan.Terrain,new HostAttackPlayerFrame(motion,playerWorld)),delay);return plan.Contact;}
            if(phase==HostAttackPhase.CompletedWorld && !HostMeleeAttack.Handles(sample))
            {
                PredictionStop stop;
                if(!PlayerMotionContinuation.Advance(ref motion,playerWorld,plan.Terrain,out stop))return OnlyFlight(plan,player,timeline,flight);
                if(ReferenceEquals(body,player))body=(Player)previewCopy.Invoke(player,null);
                HostAttackPlayerFrame.ApplyPose(body,motion);start=HostAttackModels.LaunchOrigin(body,item);
            }
            plan.Contact=AttackIntercept.WithLaunchDelay(SolveOnTerrain(body,item,captured,timeline,start,age,phase==HostAttackPhase.BeforeNpc,plan.Terrain,delay,out plan.Environment,out plan.Obstacles,new HostAttackPlayerFrame(motion,playerWorld)),delay);
            // GolemFist can legally launch again while an older fist flies.
            // Present the first useful contact of the two real stages.
            if(flight!=null && Earlier(flight.Contact,plan.Contact)){plan.Contact=flight.Contact;plan.Flight=flight;}
            return plan.Contact;
        }
        private bool PlanInputsCurrent(PreviewPlan plan,Player player,Item item,int target,HostAttackPhase phase,int age,int delay,AttackAmmoSnapshot captured,PredictionPlayerMotion motion,HostAttackReceive receive,int immune)
        {
            return plan.Flight==null && plan.Prepared && !plan.Controlled && plan.Phase==phase && plan.Age==age && plan.Delay==delay && plan.MembersCurrent(player) && PlayerMotionContinuation.SameFixedInput(plan.PlayerMotion,motion,plan.Timeline.WallSources!=null) && plan.Immune==immune && plan.AttackCooldown==player.attackCD && plan.MeleeCooldown==player.meleeNPCHitCooldown[target] && plan.Animation==player.itemAnimation && plan.AnimationMax==player.itemAnimationMax && plan.SwingRevision==swing.Revision && plan.CapsuleOnly==HostSwingAttack.CapsuleOnly(player,item) && plan.MeleeSpeed==player.meleeSpeed && plan.Direction==player.direction && plan.EnvironmentState.Current && plan.Terrain.Unchanged && (plan.Environment==null || plan.Environment.Unchanged) && (plan.Obstacles==null || plan.Obstacles.Unchanged) && SameAmmo(plan.Ammo,captured) && plan.Receive.Same(receive) && plan.Using==(combat.Left || player.controlUseItem) && (plan.HarpyState==null || plan.HarpyState.Current);
        }
        private bool CandidateMissCurrent(PreviewPlan plan,NpcIdentity target,long sampleTick,HostAttackPhase phase)
        {
            if(plan==null || !plan.Prepared || plan.Controlled || plan.Contact!=null || plan.Timeline==null || !plan.Timeline.Identity.Equals(target))return false;
            var player=combat.Player;var item=player.HeldItem;
            // New independent flights are a changed premise even without a
            // new use or inventory change. Their own observation path checks
            // the pool; do not turn an old opening miss into a flight miss.
            if(HostArcSwingAttack.Weapon(item.type) || HostHarpoonAttack.Handles(item.shoot) || HostGemStaffAttack.Handles(item.shoot) || HostAttachedShotAttack.Handles(item.shoot) || HostPhasebladeAttack.Handles(item.shoot) || Control.HasObservedPulse || item.type==3473 && Control.HasObservedSolar)return false;
            var captured=AttackAmmoSnapshot.Capture(player,item);Projectile sample=null;
            if(captured!=null && !ContentSamples.ProjectilesByType.TryGetValue(captured.Projectile,out sample))return false;
            int age=(int)((long)Main.GameUpdateCount-sampleTick)-(phase==HostAttackPhase.BeforeNpc?1:0);
            var receive=sample==null?default(HostAttackReceive):HostAttackReceive.Capture(player,sample,target.Slot,phase==HostAttackPhase.BeforeNpc,phase!=HostAttackPhase.BeforeNpc);
            return PlanInputsCurrent(plan,player,item,target.Slot,phase,age,HostAttackWindow.PreviewDelay(player,item),captured,NpcPredictionSource.ReadPlayer(player),receive,Main.npc[target.Slot].immune[player.whoAmI]);
        }
        internal static bool SameAmmo(AttackAmmoSnapshot a,AttackAmmoSnapshot b)
        {return a==null?b==null:b!=null && ReferenceEquals(a.Ammo,b.Ammo) && a.Type==b.Type && a.Stack==b.Stack && a.Projectile==b.Projectile && a.Speed==b.Speed && a.Damage==b.Damage && a.GemFeatures==b.GemFeatures && a.Mode==b.Mode && a.Offset==b.Offset && a.CardFireType==b.CardFireType && a.NoteViewAxis==b.NoteViewAxis;}
        private sealed class PreviewPlan
        {
            // Clear retires a fact, not its bounded fault history. Only a new
            // concrete context or explicit feature retry permits another try.
            internal readonly FaultGate Faults=new FaultGate();
            internal readonly PredictionTerrain Terrain=new PredictionTerrain();
            internal AttackContact Contact;internal NpcTrajectory Timeline;internal AttackAmmoSnapshot Ammo;
            internal HostProjectileEnvironment Environment;internal HostProjectileEnvironment.State EnvironmentState;internal HostAttackObstacles Obstacles;
            internal HostHarpyAttack.State HarpyState;
            internal FlightPreview Flight;
            internal HostAttackReceive Receive;
            internal PredictionPlayerMotion PlayerMotion;internal HostAttackPhase Phase;internal int Age,Delay,Immune,AttackCooldown,MeleeCooldown,Animation,AnimationMax;
            internal int Direction;internal long SwingRevision;internal float MeleeSpeed;internal bool Prepared,Controlled,CapsuleOnly,Using;private Item item;private int slot,type,prefix;private float scale;
            internal void SetMembers(Player player,Item weapon){item=weapon;slot=player.selectedItem;type=weapon.type;prefix=weapon.prefix;scale=player.GetAdjustedItemScale(weapon);Direction=player.direction;}
            internal bool WeaponCurrent(Player player){return player!=null && item!=null && player.selectedItem==slot && ReferenceEquals(player.HeldItem,item) && item.type==type && item.prefix==prefix && player.GetAdjustedItemScale(item)==scale;}
            internal bool MembersCurrent(Player player){return WeaponCurrent(player) && Direction==player.direction && (Ammo==null || Ammo.MembersMatch(player,item)) && (Obstacles==null || Obstacles.BirthInputsCurrent) && (HarpyState==null || HarpyState.MembersCurrent) && (Flight==null || Flight.Current);}
            internal void Clear(){Contact=null;Timeline=null;Ammo=null;Environment=null;Obstacles=null;HarpyState=null;Flight=null;item=null;Prepared=Controlled=false;}
        }
    }
}
