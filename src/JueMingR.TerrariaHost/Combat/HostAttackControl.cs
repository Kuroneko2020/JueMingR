using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;
using Terraria.ID;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.Combat
{
    // This is the single source-attack/role owner. Registration comes from a
    // real Shoot receipt and real native birth, never current HeldItem alone.
    // Native lifetime, release, damage, resource use and projectile fields have
    // no write path here. Source revocation retires only our input capability.
    internal sealed partial class HostAttackControl
    {
        internal sealed class Source
        {
            internal readonly Player Player;internal readonly Item Weapon;
            internal readonly long Session,Selection;internal readonly int Slot,Type,Prefix;
            internal Source(HostCombat combat,HostCombatObservation observation,Item item)
            {Player=combat.Player;Weapon=item;Session=observation.Session;Selection=combat.Tools.SelectionIntent;Slot=Player.selectedItem;Type=item.type;Prefix=item.prefix;}
        }
        internal sealed class ShotReceipt
        {
            internal HostAttackControl Owner;internal Source Source;internal ShotReceipt Previous;internal CombatCursorScope Cursor;internal bool Ended;internal HostAttackPhase Phase;internal ulong BirthCutoff;
        }
        private sealed class Controlled
        {internal Projectile Shot;internal int Key,Type;internal Source Source;internal AttackContact Contact,NextKillContact,AlternateKillContact,AlternateNextKillContact;internal Vector2 KillOrigin,NextKillOrigin,Point;internal bool ChildLaterSlot,HasPoint,BornNext,ManagedYoyo,YoyoEnhanced;internal Vector2 YoyoPoint;internal AttackAmmoSnapshot Ammo;internal HostAttackObstacles Obstacles,AlternateKillObstacles;internal uint Prepared,Born;internal ulong BirthOrdinal;internal NpcIdentity Target;internal HostYoyoNavigation Navigation,PreviewNavigation;internal HostBeamAttack.Child ContactChild;internal List<HostBeamAttack.Child> Children=new List<HostBeamAttack.Child>(6);}
        private readonly HostCombat combat;private readonly HostCombatObservation observation;
        private readonly Dictionary<int,Controlled> shots=new Dictionary<int,Controlled>();
        private readonly List<int> retired=new List<int>(64);
        private readonly PredictionTerrain terrain=new PredictionTerrain();
        private HostProjectileEnvironment.State environmentState;
        private ShotReceipt owner;
        private ulong birthOrdinal;
        private NpcTrajectory preparedTimeline;private HostAttackPhase preparedPhase;
        private Source opening;private AttackAmmoSnapshot openingAmmo;private Vector2 openingPoint;private uint openingStep;private bool openingUsable;
        private HostYoyoNavigation openingNavigation;
        private Controlled presentation;
        internal HostAttackControl(HostCombat combat,HostCombatObservation observation){this.combat=combat;this.observation=observation;}
        // A real birth remains an observation while Aim is off. Observation is
        // not an input lease: every actual cursor consumer still requires
        // Allowed, independently of the read-only preview's lifetime.
        private bool Observed(Source source)
        {return source!=null && combat.Attack.AttackObservationDemand && ReferenceEquals(source.Player,combat.Player) && source.Session==observation.Session && source.Selection==combat.Tools.SelectionIntent && source.Slot==combat.Player.selectedItem && ReferenceEquals(combat.Player.HeldItem,source.Weapon) && source.Weapon.type==source.Type && source.Weapon.prefix==source.Prefix;}
        private bool Allowed(Source source)
        {return Observed(source) && observation.Options.Aim && combat.Admitted(combat.Player);}
        internal bool Pending
        {get{foreach(var entry in shots.Values)if((entry.Type==444 || HostWhipAttack.Pending(entry.Shot) || HostFlailAttack.Handles(entry.Type)) && Valid(entry))return true;return false;}}
        // Scheduling reads an already prepared input, never mints permission.
        // Safe navigation/held points need no damage contact or red marker.
        internal bool HasPreparedInput
        {
            get
            {
                if(!combat.Attack.Permission || !combat.Attack.CurrentReceiver || !combat.Attack.TimelineCurrent(preparedTimeline))return false;
                if(openingUsable && openingStep==Main.GameUpdateCount && opening!=null && Allowed(opening))return true;
                foreach(var entry in shots.Values)
                {
                    if(entry.ManagedYoyo && entry.YoyoEnhanced && entry.Born==Main.GameUpdateCount && Valid(entry) && entry.Ammo!=null && entry.Ammo.IdentityMatches(combat.Player,entry.Source.Weapon) && entry.Target.Equals(observation.Selection.Target) && combat.Use.ManagedYoyoHeld(entry.Shot))return true;
                    if(Valid(entry) && entry.Prepared==Main.GameUpdateCount && entry.Target.Equals(observation.Selection.Target) && (entry.HasPoint || entry.Contact!=null || entry.NextKillContact!=null))return true;
                }
                return false;
            }
        }
        internal bool HasObservedSolar
        {get{foreach(var entry in shots.Values)if(entry.Type==611 && Observed(entry))return true;return false;}}
        internal int SolarOpeningDelay
        {
            get
            {
                int delay=0;
                foreach(var entry in shots.Values)if(entry.Type==611 && Observed(entry))
                {
                    // AI75 kills at ai0=30 and still calls SetDummyItemTime(2)
                    // on that action. The following player update spends its
                    // tail; only the next update can emit a fresh arc.
                    var shot=entry.Shot;int remaining=Math.Max(0,(int)Math.Ceiling((30-shot.ai[0])/shot.MaxUpdates));
                    delay=Math.Max(delay,remaining+1);
                }
                return delay;
            }
        }
        internal bool HasObservedPulse
        {get{foreach(var entry in shots.Values)if(HostPulseAttack.Handles(entry.Type) && Observed(entry))return true;return false;}}
        private bool Valid(Controlled entry)
        {return entry.Shot.active && entry.Shot.owner==combat.Player?.whoAmI && (int)entry.Shot.key==entry.Key && entry.Shot.type==entry.Type && Allowed(entry.Source);}
        private bool Observed(Controlled entry)
        {return entry.Shot.active && entry.Shot.owner==combat.Player?.whoAmI && (int)entry.Shot.key==entry.Key && entry.Shot.type==entry.Type && Observed(entry.Source);}
        internal ShotReceipt BeginShot(Player player,Item item,CombatCursorScope cursor)
        {
            var receipt=new ShotReceipt{Owner=this,Cursor=cursor,Previous=owner,Phase=preparedPhase,BirthCutoff=birthOrdinal};owner=receipt;
            if(ReferenceEquals(player,combat.Player) && combat.Attack.AttackObservationDemand && item!=null && !item.IsAir)receipt.Source=new Source(combat,observation,item);
            return receipt;
        }
        internal ShotReceipt BeginDamage(Projectile shot)
        {
            Controlled entry;if(!shots.TryGetValue((int)shot.key,out entry) || !ReferenceEquals(entry.Shot,shot) || entry.Navigation==null || !Observed(entry) || shot.ai[0]<0)return null;
            // Only a naturally executing registered ball Damage can lend its
            // source to a glove's real second-ball birth. Matching type/owner
            // in an arbitrary current array is not causal authorization.
            var receipt=new ShotReceipt{Owner=this,Source=entry.Source,Previous=owner,Phase=HostAttackPhase.Projectiles,BirthCutoff=birthOrdinal};owner=receipt;return receipt;
        }
        internal ShotReceipt BeginDerived(Projectile shot)
        {
            Controlled entry;if(shot.type!=439 || !shots.TryGetValue((int)shot.key,out entry) || !ReferenceEquals(entry.Shot,shot) || !Observed(entry) || !HostLaserAttack.Consumes(combat.Player,shot))return null;
            // Only an observed parent's real AI call lends its source to
            // sealed pulses. This receipt carries identity, never input.
            var receipt=new ShotReceipt{Owner=this,Source=entry.Source,Previous=owner,Phase=HostAttackPhase.Projectiles,BirthCutoff=birthOrdinal};owner=receipt;return receipt;
        }
        internal void EndShot(ShotReceipt receipt)
        {
            if(receipt==null || receipt.Ended)return;receipt.Ended=true;receipt.Cursor?.End();
            while(owner!=null && owner.Ended)owner=owner.Previous;
            // Newly born controllers can get their first AI in the SAME native
            // projectile pass. Reuse the already captured shared future at the
            // real birth boundary; never recapture/select inside each AI call.
            if(receipt.Source!=null && preparedTimeline!=null && birthOrdinal>receipt.BirthCutoff)
            {
                try{using(observation.PredictionWork.Enter(Main.GameUpdateCount)){PrepareBorn(receipt.Source,receipt.Phase,receipt.BirthCutoff);}}
                catch(CombatPredictionBudgetExceededException){Clear();}
                catch(Exception error){combat.Attack.FailLocal(error);}
            }
        }
        internal void Created(Player player,Projectile shot)
        {
            if(shot.type==632 || shot.type==461)
            {
                Controlled parent;int key=(int)(Terraria.DataStructures.ProjectileKey)shot.ai[1];
                if(shots.TryGetValue(key,out parent) && Observed(parent) && parent.Children.Count<6 && (shot.type==632 && parent.Type==633 || shot.type==461 && parent.Type==460))parent.Children.Add(new HostBeamAttack.Child(shot));
                return;
            }
            // Continuous pulses can outlive many later firing windows. The
            // native pool bounds this identity registry; sealed roles never
            // enter execution preparation or acquire an input capability.
            if(owner?.Source==null || !ReferenceEquals(player,combat.Player) || !Observed(owner.Source) || shots.Count>=Main.maxProjectiles)return;
            if(shot.type==1035 && !HostWhipAttack.Primary(shot))return;
            if(!HostGuidedAttack.Handles(shot.type) && shot.type!=444 && shot.type!=1035 && shot.type!=611 && !HostPulseAttack.Handles(shot.type) && !HostHarpoonAttack.Handles(shot.type) && !HostHeldAttack.Handles(shot.type) && !HostYoyoNavigation.Handles(shot))return;
            if(HostYoyoNavigation.Handles(shot) && shot.ai[0]<0)return;
            bool managedYoyo=HostYoyoNavigation.Handles(shot) && combat.Use.ManagedYoyoHeld(shot);
            // Only the managed birth borrows the opening's ammo receipt. Other
            // controllers keep their existing independently prepared members;
            // a previous opening must not supply a later unrelated source.
            shots[(int)shot.key]=new Controlled{Shot=shot,Key=(int)shot.key,Type=shot.type,Source=owner.Source,BirthOrdinal=++birthOrdinal,Born=Main.GameUpdateCount,BornNext=Main.ProjectileUpdateLoopIndex>=0 && shot.whoAmI<=Main.ProjectileUpdateLoopIndex,ManagedYoyo=managedYoyo,YoyoPoint=openingPoint,YoyoEnhanced=openingUsable,Ammo=managedYoyo?openingAmmo:null,Navigation=HostYoyoNavigation.Handles(shot)?new HostYoyoNavigation():null};
        }
        internal void Clear(){presentation=null;preparedTimeline=null;opening=null;openingNavigation=null;openingUsable=false;foreach(var entry in shots.Values)Retire(entry);}
        // An individual natural consumer can revoke only its own prepared
        // capability. Source/registry lifetime belongs to Prepare/Reset; an
        // unrelated or repeated projectile callback must never walk that pool.
        private static void Retire(Controlled entry)
        {entry.Contact=entry.NextKillContact=entry.AlternateKillContact=entry.AlternateNextKillContact=null;entry.HasPoint=false;entry.ContactChild=null;entry.Obstacles=entry.AlternateKillObstacles=null;}
        // Execution retirement alone leaves a read-only fact intact. Only an
        // actual consumer's failed dependency check revokes its bound fact;
        // no other source is searched and Draw never checks terrain here.
        private void RejectDependencies(Controlled entry)
        {Retire(entry);if(ReferenceEquals(previewSource,entry))ClearPreview();}
        internal void Reset(){Clear();ClearPreview();shots.Clear();preparedTimeline=null;opening=null;birthOrdinal=0;}
        internal void Prepare(NpcTrajectory timeline,HostAttackPhase phase)
        {
            Clear();retired.Clear();terrain.Reset();environmentState=new HostProjectileEnvironment.State(true);preparedTimeline=timeline;preparedPhase=phase;
            opening=null;
            // Native AI99 primary/paired control refreshes dummy timers to 2;
            // those actions cannot emit from the hand. Detached magic-string
            // balls do not own that timing. Gate only new yoyo openings, never
            // the shared demand, live-entry preparation or 1035's later windows.
            if(timeline!=null && (HostHeldAttack.Weapon(combat.Player.HeldItem.type) || HostYoyoNavigation.Weapon(combat.Player.HeldItem) && HostAttackWindow.Opening(combat,combat.Player,combat.Player.HeldItem) || HostWhipAttack.Weapon(combat.Player.HeldItem)))
            {
                opening=new Source(combat,observation,combat.Player.HeldItem);openingAmmo=AttackAmmoSnapshot.Capture(combat.Player,opening.Weapon);openingStep=Main.GameUpdateCount;
                if(openingAmmo!=null)
                {if(HostYoyoNavigation.Weapon(opening.Weapon)){if(combat.Use.ManagedAttackIntent(combat.Player) && combat.Player.magicString)openingUsable=HostYoyoReleaseAttack.TryPoint(combat.Player,opening.Weapon,openingAmmo.Speed,timeline,new HostAttackClock(timeline,phase),terrain,out openingPoint);else openingUsable=HostYoyoNavigation.OpeningPoint(combat.Player,opening.Weapon,timeline,out openingPoint,out openingNavigation);}else if(HostWhipAttack.Weapon(opening.Weapon))openingUsable=HostWhipAttack.TryPoint(combat.Player,null,timeline,new HostAttackClock(timeline,phase),terrain,out openingPoint);else{openingPoint=HostHeldAttack.OpeningPoint(combat.Player,opening.Weapon,openingAmmo,timeline);openingUsable=true;}}
            }
            foreach(var pair in shots)
            {
                var entry=pair.Value;if(!Observed(entry)){retired.Add(pair.Key);continue;}if(!Valid(entry) || HostPulseAttack.Handles(entry.Type) || HostHarpoonAttack.Handles(entry.Type))continue;
                if(timeline==null || !timeline.Identity.Equals(observation.Selection.Target))continue;
                PrepareEntry(entry,timeline,phase);
            }
            foreach(int key in retired)shots.Remove(key);
        }
        // A reused parent source is causal authority, not a request to replay
        // that parent between its AI and Movement/Damage. Only births inside
        // this receipt receive same-pass preparation; siblings keep their phase.
        private void PrepareBorn(Source source,HostAttackPhase phase,ulong cutoff)
        {foreach(var entry in shots.Values)if(entry.BirthOrdinal>cutoff && ReferenceEquals(entry.Source,source) && Valid(entry) && !HostPulseAttack.Handles(entry.Type) && !HostHarpoonAttack.Handles(entry.Type))PrepareEntry(entry,preparedTimeline,phase);}
        private void PrepareEntry(Controlled entry,NpcTrajectory timeline,HostAttackPhase phase)
        {if(entry.ManagedYoyo && !combat.Use.ManagedAttackIntent(combat.Player))entry.ManagedYoyo=false;if(entry.ManagedYoyo){entry.Prepared=Main.GameUpdateCount;entry.Target=timeline.Identity;return;}SolveEntry(entry,timeline,phase,terrain,combat.Player);}
        private static void SolveEntry(Controlled entry,NpcTrajectory timeline,HostAttackPhase phase,PredictionTerrain terrain,Player player,int delay=0,HostAttackPlayerFrame? preparedPlayer=null)
        {
            if(entry.BornNext && entry.Born==Main.GameUpdateCount && phase==HostAttackPhase.Projectiles)phase=HostAttackPhase.CompletedWorld;
            var clock=new HostAttackClock(timeline,phase,delay);int age=clock.Age;if(age<0 || age>1+delay || clock.FirstTick>=timeline.Count)return;
            var playerFrame=preparedPlayer??(entry.Navigation!=null || HostGuidedAttack.Handles(entry.Type) || HostHeldAttack.Handles(entry.Type)?HostAttackPlayerFrame.Capture(player,timeline):default(HostAttackPlayerFrame));
            if(HostPulseAttack.Handles(entry.Type))entry.Contact=HostPulseAttack.Known(player,entry.Shot,timeline,clock,terrain,out entry.Obstacles);
            if(HostHarpoonAttack.Handles(entry.Type))entry.Contact=HostHarpoonAttack.Known(player,entry.Shot,timeline,clock,terrain,out entry.Obstacles);
            if(entry.Type==1035){entry.HasPoint=HostWhipAttack.Pending(entry.Shot) && HostWhipAttack.TryPoint(player,entry.Shot,timeline,clock,terrain,out entry.Point);entry.Contact=null;}
            if(entry.Navigation!=null)entry.Contact=entry.Navigation.PrepareWithPlayer(player,entry.Shot,timeline,clock,out entry.Point,out entry.HasPoint,playerFrame);
            if(HostGuidedAttack.Handles(entry.Type) && (entry.Type==491 || HostGuidedAttack.Consumes(player,entry.Shot)))
                entry.Contact=HostGuidedAttack.SolvePrepared(player,entry.Shot,timeline,clock,terrain,playerFrame);
            if(HostHeldAttack.Handles(entry.Type) && (player.channel || HostFlailAttack.Handles(entry.Type)))
            {
                entry.Contact=HostHeldAttack.SolvePrepared(player,entry.Shot,timeline,clock,terrain,out entry.Point,out entry.Ammo,out entry.Obstacles,playerFrame);entry.HasPoint=!HostFlailAttack.Handles(entry.Type) || player.channel || entry.Contact!=null;
                if(entry.Type==633 || entry.Type==460)entry.Contact=HostBeamAttack.SolvePrepared(player,entry.Shot,entry.Children,entry.Point,timeline,clock,terrain,out entry.ContactChild,playerFrame);
            }
            if(entry.Type==444)
            {
                AttackMotion motion;if(HostAttackModels.TryResolved((int)entry.Shot.localAI[0],entry.Shot.localAI[1],entry.Source?.Type??player.HeldItem.type,out motion))
                {
                    // A full pool uses oldest replacement (possibly this
                    // still-active parent), and debug random slots have no
                    // deterministic phase. Neither is an early-slot receipt.
                    bool later;if(!TryChildLaterSlot(entry.Shot,out later))return;
                    entry.ChildLaterSlot=later;entry.KillOrigin=entry.Shot.Center;
                    Projectile sample;
                    if(!ContentSamples.ProjectilesByType.TryGetValue((int)entry.Shot.localAI[0],out sample))return;
                    if(player.magicQuiver && sample.arrow && sample.extraUpdates<1)motion=motion.WithUpdates(2,1);
                    bool expiry=false;
                    if(entry.Shot.timeLeft<=entry.Shot.extraUpdates+1)
                    {
                        Vector2 center=entry.Shot.Center,velocity=entry.Shot.velocity;float sealedAngle=entry.Shot.ai[0];bool clear=true;
                        // AI78 steers back toward its sealed launch angle on
                        // every subupdate before damping and movement. Natural
                        // expiry consumes the resulting Center, not a straight
                        // .96 extrapolation of its random birth velocity.
                        for(int step=0;step<entry.Shot.timeLeft;step++)
                        {
                            if(!HostBubbleAttack.Advance(entry.Shot,terrain,ref center,ref velocity,ref sealedAngle)){clear=false;break;}
                        }
                        entry.NextKillOrigin=center;
                        expiry=clear;
                    }
                    PrepareBubbleOrder(entry,player,sample,motion,timeline,clock,terrain,later,expiry,out entry.Contact,out entry.NextKillContact,out entry.Obstacles);
                    // A preceding native sibling may free an earlier slot in
                    // this same pass. Prepare both finite receive schedules;
                    // Kill only chooses the actual order, never solves again.
                    if(later?entry.Shot.whoAmI>0:entry.Shot.whoAmI<Main.maxProjectiles-1)
                        PrepareBubbleOrder(entry,player,sample,motion,timeline,clock,terrain,!later,expiry,out entry.AlternateKillContact,out entry.AlternateNextKillContact,out entry.AlternateKillObstacles);
                }
            }
            entry.Prepared=Main.GameUpdateCount;entry.Target=timeline.Identity;
        }
        private static void PrepareBubbleOrder(Controlled entry,Player player,Projectile sample,AttackMotion motion,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,bool later,bool expiry,out AttackContact current,out AttackContact next,out HostAttackObstacles obstacles)
        {
            // Kill runs after NPCs. A lower-slot child first updates next
            // world tick, so its target sample and immunity clock both shift.
            int childAge=clock.Age+(later?0:1);
            var receive=HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,clock.BeforeNpc,clock.NextWorld || !later);
            var lease=new HostAttackObstacles(player,sample,timeline,0,clock.BeforeNpc,clock.NextWorld || !later);obstacles=lease;
            AttackPassage passage=(x,y,nx,ny,w,h)=>HostProjectileEnvironment.Dry(sample,terrain,x,y,nx-x) && terrain.ProjectilePassage(x,y,nx,ny,(int)w,(int)h);
            Func<Vector2,AttackContact> solve=origin=>AttackIntercept.Solve(origin.X,origin.Y,motion,timeline,childAge,0,passage,clock.FirstTick-clock.Age,receive.Allows,(k,t,x,y)=>lease.PassOuter(k,t,motion.OuterAtSubupdate(k),x,y),(k,t,x,y)=>motion.SubupdateInOuter(k)!=0 || later && motion.OuterAtSubupdate(k)==1 || lease.BeforeNpc(t,motion.OuterAtSubupdate(k),x,y));
            current=solve(entry.KillOrigin);next=expiry?solve(entry.NextKillOrigin):null;
        }
        private bool Passage(float x,float y,float nx,float ny,float width,float height){return terrain.ProjectilePassage(x,y,nx,ny,(int)width,(int)height);}
        internal CombatCursorScope BeginOpening(Player player,Item item)
        {
            var enhanced=BeginEnhancedOpening(player,item);
            if(enhanced!=null)return enhanced;
            HostAttackInput input;
            return combat.Attack.TryBasicInput(player,item,null,HostAttackPhase.BeforeNpc,out input)?input.Begin():null;
        }
        private CombatCursorScope BeginEnhancedOpening(Player player,Item item)
        {
            if(!openingUsable || opening==null)return null;
            if(!combat.Attack.CurrentReceiver){openingUsable=false;return null;}
            if(!environmentState.Current || !terrain.Unchanged || !openingUsable || opening==null || openingNavigation!=null && !openingNavigation.Current(player) || !combat.Attack.Permission || !ReferenceEquals(player,opening.Player) || !ReferenceEquals(item,opening.Weapon) || !Allowed(opening) || openingStep!=Main.GameUpdateCount || openingAmmo==null || !openingAmmo.IdentityMatches(player,item) || !combat.Attack.TimelineCurrent(preparedTimeline) || !preparedTimeline.Identity.Equals(observation.Selection.Target))return null;
            var scope=CombatCursorScope.Begin(openingPoint);if(scope!=null)combat.Attack.RecordControlInput(openingPoint,null,HostAttackInputKind.Enhanced);return scope;
        }
        private static bool TryChildLaterSlot(Projectile parent,out bool later)
        {later=false;if(Terraria.Testing.DebugOptions.Shared_RandomizeProjectileSlots)return false;for(int i=0;i<Main.maxProjectiles;i++)if(!Main.projectile[i].active){later=i>parent.whoAmI;return true;}return false;}
        internal CombatCursorScope BeginKill(Projectile shot)
        {
            Controlled entry;
            if(shot.type!=444 || !shots.TryGetValue((int)shot.key,out entry) || !ReferenceEquals(entry.Shot,shot) || !Valid(entry) || !combat.Attack.BasicPermission || !combat.Attack.CurrentReceiver)return null;
            var enhanced=BeginEnhancedKill(shot);
            if(enhanced!=null)return enhanced;
            // Native bubble Kill still reads a fresh direction even when no
            // child receive/slot schedule could be proved. Its real source
            // and native death receipt authorize only this one consumer.
            HostAttackInput input;
            return combat.Attack.TryBasicInput(combat.Player,entry.Source.Weapon,shot,HostAttackPhase.Projectiles,out input)?input.Begin():null;
        }
        private CombatCursorScope BeginEnhancedKill(Projectile shot)
        {
            Controlled entry;if(shot.type!=444 || !shots.TryGetValue((int)shot.key,out entry) || !ReferenceEquals(entry.Shot,shot))return null;
            if(!combat.Attack.CurrentReceiver){Retire(entry);return null;}
            bool later;
            if(!Valid(entry) || Main.GameUpdateCount<entry.Prepared || Main.GameUpdateCount-entry.Prepared>1 || !entry.Target.Equals(observation.Selection.Target))
            {Retire(entry);return null;}
            if(!environmentState.Current || !terrain.Unchanged){RejectDependencies(entry);return null;}
            if(!TryChildLaterSlot(shot,out later)){Retire(entry);return null;}
            bool alternate=later!=entry.ChildLaterSlot;var lease=alternate?entry.AlternateKillObstacles:entry.Obstacles;
            if(lease==null){Retire(entry);return null;}
            if(!lease.Unchanged){RejectDependencies(entry);return null;}
            var result=Vector2.DistanceSquared(shot.Center,entry.KillOrigin)<.01f?(alternate?entry.AlternateKillContact:entry.Contact):Vector2.DistanceSquared(shot.Center,entry.NextKillOrigin)<.01f?(alternate?entry.AlternateNextKillContact:entry.NextKillContact):null;
            Retire(entry);
            if(result==null || !combat.Attack.TimelineCurrent(result.Timeline))return null;
            return combat.Attack.ContactInput(result,entry.Source.Weapon,shot,HostAttackPhase.Projectiles).Begin();
        }
        internal AttackContact ExpectedImpact
        {
            get
            {
                if(!combat.Attack.CurrentReceiver){Clear();return null;}
                if(shots.Count==0)return null;
                if(!environmentState.Current || !terrain.Unchanged)return null;
                foreach(var entry in shots.Values)
                {
                    var result=entry.Type==444?entry.NextKillContact:entry.Contact;
                    if(!Valid(entry) || entry.Obstacles!=null && !entry.Obstacles.Unchanged || entry.Prepared!=Main.GameUpdateCount || result==null || !entry.Target.Equals(observation.Selection.Target) || !combat.Attack.TimelineCurrent(result.Timeline))continue;
                    if(HostGuidedAttack.Handles(entry.Type) && !HostGuidedAttack.Consumes(combat.Player,entry.Shot))continue;
                    if(HostHeldAttack.Handles(entry.Type) && !HostFlailAttack.Handles(entry.Type) && !combat.Player.channel)continue;
                    if(entry.Navigation!=null && (!combat.Player.channel || entry.Shot.ai[0]<0 || !entry.Navigation.Current(combat.Player))){entry.Contact=null;entry.HasPoint=false;continue;}
                    if(entry.ContactChild!=null && !entry.ContactChild.Valid(entry.Shot))continue;
                    return result;
                }
                return null;
            }
        }
        internal void BindPresentation(AttackContact result)
        {presentation=null;if(result!=null)foreach(var entry in shots.Values)if(ReferenceEquals(result,entry.Type==444?entry.NextKillContact:entry.Contact)){presentation=entry;break;}}
        internal bool PresentationCurrent(AttackContact result)
        {
            if(!combat.Attack.CurrentReceiver)return false;
            // The entry reference was bound once at Capture. No registry lookup,
            // projectile pool, terrain or candidate-set scan is allowed in Draw.
            var entry=presentation;
            if(entry==null || !environmentState.Current || !Valid(entry) || entry.Prepared!=Main.GameUpdateCount || !ReferenceEquals(result,entry.Type==444?entry.NextKillContact:entry.Contact) || !entry.Target.Equals(observation.Selection.Target) || entry.Ammo!=null && !entry.Ammo.MembersMatch(combat.Player,combat.Player.HeldItem) || entry.Obstacles!=null && !entry.Obstacles.BirthInputsCurrent)return false;
            if(HostGuidedAttack.Handles(entry.Type) && !HostGuidedAttack.Consumes(combat.Player,entry.Shot))return false;
            if(HostHeldAttack.Handles(entry.Type) && !HostFlailAttack.Handles(entry.Type) && !combat.Player.channel)return false;
            if(entry.Navigation!=null && (!combat.Player.channel || entry.Shot.ai[0]<0 || !entry.Navigation.PresentationCurrent(combat.Player)))return false;
            return entry.ContactChild==null || entry.ContactChild.Valid(entry.Shot);
        }
        internal CombatCursorScope BeginAI(Projectile shot)
        {Vector2 point;return TryPoint(shot,out point)?CombatCursorScope.Begin(point):null;}
        // G11A can read this same prepared capability before entering its
        // outer cursor scope. Reading never advances cadence or solves again.
        internal bool TryPoint(Projectile shot,out Vector2 point)
        {
            point=default(Vector2);Controlled entry;
            if(!shots.TryGetValue((int)shot.key,out entry) || !ReferenceEquals(entry.Shot,shot) || !Valid(entry) || !combat.Attack.BasicPermission || !combat.Attack.CurrentReceiver)return false;
            // The native next-action split copies this birth-action direction.
            // Do not replace it with held navigation, or lend a cursor once the
            // cadence has released. The autonomous child is never registered.
            if(entry.ManagedYoyo)
            {
                if(!combat.Use.ManagedYoyoHeld(shot))return false;
                if(entry.YoyoEnhanced && entry.Ammo!=null && entry.Ammo.IdentityMatches(combat.Player,entry.Source.Weapon) && entry.Born==Main.GameUpdateCount && environmentState.Current && terrain.Unchanged && combat.Attack.TimelineCurrent(preparedTimeline) && entry.Target.Equals(observation.Selection.Target))
                {point=entry.YoyoPoint;combat.Attack.RecordControlInput(point,shot,HostAttackInputKind.Enhanced);return true;}
                HostAttackInput basic;if(!combat.Attack.TryBasicInput(combat.Player,entry.Source.Weapon,shot,HostAttackPhase.Projectiles,out basic))return false;
                point=basic.Point;combat.Attack.RecordInput(basic);return true;
            }
            // These predicates are vanilla's actual cursor-reading windows.
            // A sealed child, released missile, returning ball/flail or bubble
            // AI cannot obtain control by falling back from a missing contact.
            bool consumes=entry.Type==1035?HostWhipAttack.Consumes(shot):entry.Navigation!=null?combat.Player.channel && shot.ai[0]>=0:
                HostHeldAttack.Handles(entry.Type)?HostHeldAttack.Consumes(combat.Player,shot):HostGuidedAttack.Handles(entry.Type) && HostGuidedAttack.Consumes(combat.Player,shot);
            if(!consumes)return false;
            if(TryEnhancedPoint(shot,out point)){combat.Attack.RecordControlInput(point,shot,HostAttackInputKind.Enhanced);return true;}
            HostAttackInput input;
            if(!combat.Attack.TryBasicInput(combat.Player,entry.Source.Weapon,shot,HostAttackPhase.Projectiles,out input))return false;
            point=input.Point;combat.Attack.RecordInput(input);return true;
        }
        private bool TryEnhancedPoint(Projectile shot,out Vector2 point)
        {
            point=default(Vector2);Controlled entry;if(!shots.TryGetValue((int)shot.key,out entry) || !ReferenceEquals(entry.Shot,shot))return false;
            // Bubble AI never reads the cursor. Its separate Kill receipt must
            // survive these natural AI calls, including a stationary bubble
            // whose current and expiry origins are exactly the same.
            if(entry.Type==444 || HostPulseAttack.Handles(entry.Type))return false;
            if(!combat.Attack.CurrentReceiver){Retire(entry);return false;}
            if(!Valid(entry) || entry.Prepared!=Main.GameUpdateCount || entry.Contact==null && !entry.HasPoint || !observation.Selection.HasTarget || !entry.Target.Equals(observation.Selection.Target) || !combat.Attack.TimelineCurrent(preparedTimeline))
            {Retire(entry);return false;}
            if(!environmentState.Current || entry.Obstacles!=null && !entry.Obstacles.Unchanged || !terrain.Unchanged)
            {RejectDependencies(entry);return false;}
            if(entry.Type==1035){point=entry.Point;return HostWhipAttack.Consumes(shot);}
            if(HostHeldAttack.Handles(entry.Type))
            {
                if(!HostHeldAttack.Consumes(combat.Player,shot))return false;
                if(entry.Ammo!=null && !entry.Ammo.IdentityMatches(combat.Player,combat.Player.HeldItem)){entry.Contact=null;entry.HasPoint=false;return false;}
                point=entry.Point;return true;
            }
            if(entry.Navigation!=null)
            {
                if(!entry.HasPoint || !combat.Player.channel || shot.ai[0]<0){entry.Contact=null;entry.HasPoint=false;return false;}
                if(!entry.Navigation.Current(combat.Player)){RejectDependencies(entry);return false;}
                point=entry.Point;return true;
            }
            if(!HostGuidedAttack.Handles(entry.Type) || !HostGuidedAttack.Consumes(combat.Player,shot)){entry.Contact=null;return false;}
            point=new Vector2(entry.Contact.AimX,entry.Contact.AimY);return true;
        }
    }
}
