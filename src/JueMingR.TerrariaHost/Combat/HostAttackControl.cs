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
    internal sealed class HostAttackControl
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
            internal HostAttackControl Owner;internal Source Source;internal ShotReceipt Previous;internal CombatCursorScope Cursor;internal bool Ended;internal HostAttackPhase Phase;
        }
        private sealed class Controlled
        {internal Projectile Shot;internal int Key,Type;internal Source Source;internal AttackContact Contact,NextKillContact;internal Vector2 KillOrigin,NextKillOrigin,Point;internal bool ChildLaterSlot,HasPoint;internal AttackAmmoSnapshot Ammo;internal uint Prepared;internal NpcIdentity Target;internal HostYoyoNavigation Navigation;internal HostBeamAttack.Child ContactChild;internal readonly List<HostBeamAttack.Child> Children=new List<HostBeamAttack.Child>(6);}
        private readonly HostCombat combat;private readonly HostCombatObservation observation;
        private readonly Dictionary<int,Controlled> shots=new Dictionary<int,Controlled>();
        private readonly List<int> retired=new List<int>(64);
        private readonly PredictionTerrain terrain=new PredictionTerrain();
        private ShotReceipt owner;
        private NpcTrajectory preparedTimeline;private HostAttackPhase preparedPhase;
        private Source opening;private AttackAmmoSnapshot openingAmmo;private Vector2 openingPoint;private uint openingStep;private bool openingUsable;
        internal HostAttackControl(HostCombat combat,HostCombatObservation observation){this.combat=combat;this.observation=observation;}
        private bool Allowed(Source source)
        {return !combat.Attack.Failed && observation.Settings.CanRun && observation.Options.Aim && combat.Admitted(combat.Player) && ReferenceEquals(source.Player,combat.Player) && source.Session==observation.Session && source.Selection==combat.Tools.SelectionIntent && source.Slot==combat.Player.selectedItem && ReferenceEquals(combat.Player.HeldItem,source.Weapon) && source.Weapon.type==source.Type && source.Weapon.prefix==source.Prefix;}
        internal bool Pending
        {get{foreach(var entry in shots.Values)if((entry.Type==444 || HostFlailAttack.Handles(entry.Type)) && Valid(entry))return true;return false;}}
        private bool Valid(Controlled entry)
        {return entry.Shot.active && entry.Shot.owner==combat.Player?.whoAmI && (int)entry.Shot.key==entry.Key && entry.Shot.type==entry.Type && Allowed(entry.Source);}
        internal ShotReceipt BeginShot(Player player,Item item,CombatCursorScope cursor)
        {
            var receipt=new ShotReceipt{Owner=this,Cursor=cursor,Previous=owner,Phase=preparedPhase};owner=receipt;
            if(ReferenceEquals(player,combat.Player) && observation.Options.Aim && combat.Attack.Permission && item!=null && !item.IsAir)receipt.Source=new Source(combat,observation,item);
            return receipt;
        }
        internal ShotReceipt BeginDamage(Projectile shot)
        {
            Controlled entry;if(!shots.TryGetValue((int)shot.key,out entry) || !ReferenceEquals(entry.Shot,shot) || entry.Navigation==null || !Valid(entry) || shot.ai[0]<0)return null;
            // Only a naturally executing registered ball Damage can lend its
            // source to a glove's real second-ball birth. Matching type/owner
            // in an arbitrary current array is not causal authorization.
            var receipt=new ShotReceipt{Owner=this,Source=entry.Source,Previous=owner,Phase=HostAttackPhase.Projectiles};owner=receipt;return receipt;
        }
        internal void EndShot(ShotReceipt receipt)
        {
            if(receipt==null || receipt.Ended)return;receipt.Ended=true;receipt.Cursor?.End();
            while(owner!=null && owner.Ended)owner=owner.Previous;
            // Newly born controllers can get their first AI in the SAME native
            // projectile pass. Reuse the already captured shared future at the
            // real birth boundary; never recapture/select inside each AI call.
            if(receipt.Source!=null && preparedTimeline!=null)try{PrepareBorn(receipt.Source,receipt.Phase);}catch(Exception error){combat.Attack.FailLocal(error);}
        }
        internal void Created(Player player,Projectile shot)
        {
            if(shot.type==632 || shot.type==461)
            {
                Controlled parent;int key=(int)(Terraria.DataStructures.ProjectileKey)shot.ai[1];
                if(shots.TryGetValue(key,out parent) && Valid(parent) && parent.Children.Count<6 && (shot.type==632 && parent.Type==633 || shot.type==461 && parent.Type==460))parent.Children.Add(new HostBeamAttack.Child(shot));
                return;
            }
            if(owner?.Source==null || !ReferenceEquals(player,combat.Player) || !Allowed(owner.Source) || shots.Count>=64)return;
            if(!HostGuidedAttack.Handles(shot.type) && shot.type!=444 && !HostHeldAttack.Handles(shot.type) && !HostYoyoNavigation.Handles(shot))return;
            if(HostYoyoNavigation.Handles(shot) && shot.ai[0]<0)return;
            shots[(int)shot.key]=new Controlled{Shot=shot,Key=(int)shot.key,Type=shot.type,Source=owner.Source,Navigation=HostYoyoNavigation.Handles(shot)?new HostYoyoNavigation():null};
        }
        internal void Clear(){opening=null;openingUsable=false;foreach(var entry in shots.Values){entry.Contact=entry.NextKillContact=null;entry.HasPoint=false;entry.ContactChild=null;}}
        internal void Reset(){Clear();shots.Clear();preparedTimeline=null;opening=null;}
        internal void Prepare(NpcTrajectory timeline,HostAttackPhase phase)
        {
            Clear();retired.Clear();terrain.Reset();preparedTimeline=timeline;preparedPhase=phase;
            opening=null;
            if(timeline!=null && (HostHeldAttack.Weapon(combat.Player.HeldItem.type) || HostYoyoNavigation.Weapon(combat.Player.HeldItem)))
            {
                opening=new Source(combat,observation,combat.Player.HeldItem);openingAmmo=AttackAmmoSnapshot.Capture(combat.Player,opening.Weapon);openingStep=Main.GameUpdateCount;
                if(openingAmmo!=null)
                {if(HostYoyoNavigation.Weapon(opening.Weapon))openingUsable=HostYoyoNavigation.OpeningPoint(combat.Player,opening.Weapon,timeline,out openingPoint);else{openingPoint=HostHeldAttack.OpeningPoint(combat.Player,opening.Weapon,openingAmmo,timeline);openingUsable=true;}}
            }
            foreach(var pair in shots)
            {
                var entry=pair.Value;if(!Valid(entry)){retired.Add(pair.Key);continue;}
                if(timeline==null || !timeline.Identity.Equals(observation.Selection.Target))continue;
                PrepareEntry(entry,timeline,phase);
            }
            foreach(int key in retired)shots.Remove(key);
        }
        private void PrepareBorn(Source source,HostAttackPhase phase)
        {foreach(var entry in shots.Values)if(ReferenceEquals(entry.Source,source) && Valid(entry))PrepareEntry(entry,preparedTimeline,phase);}
        private void PrepareEntry(Controlled entry,NpcTrajectory timeline,HostAttackPhase phase)
        {
            var clock=new HostAttackClock(timeline,phase);int age=clock.Age;if(age<0 || age>1)return;
            if(entry.Navigation!=null)entry.Contact=entry.Navigation.Prepare(combat.Player,entry.Shot,timeline,clock,out entry.Point,out entry.HasPoint);
            if(HostGuidedAttack.Handles(entry.Type) && combat.Player.channel && entry.Shot.ai[0]>=0 && combat.Player.HeldItem.shoot==entry.Type)
                entry.Contact=HostGuidedAttack.Solve(combat.Player,entry.Shot,timeline,clock,terrain);
            if(HostHeldAttack.Handles(entry.Type) && (combat.Player.channel || HostFlailAttack.Handles(entry.Type)))
            {
                entry.Contact=HostHeldAttack.Solve(combat.Player,entry.Shot,timeline,clock,terrain,out entry.Point,out entry.Ammo);entry.HasPoint=!HostFlailAttack.Handles(entry.Type) || combat.Player.channel || entry.Contact!=null;
                if(entry.Type==633 || entry.Type==460)entry.Contact=HostBeamAttack.Solve(combat.Player,entry.Shot,entry.Children,entry.Point,timeline,clock,terrain,out entry.ContactChild);
            }
            if(entry.Type==444)
            {
                AttackMotion motion;if(HostAttackModels.TryResolved((int)entry.Shot.localAI[0],entry.Shot.localAI[1],entry.Source.Type,out motion))
                {
                    // A full pool uses oldest replacement (possibly this
                    // still-active parent), and debug random slots have no
                    // deterministic phase. Neither is an early-slot receipt.
                    bool later;if(!TryChildLaterSlot(entry.Shot,out later))return;
                    entry.ChildLaterSlot=later;entry.KillOrigin=entry.Shot.Center;
                    // Kill runs after NPCs. A child in an earlier slot first
                    // updates next tick; a later slot updates in this pass.
                    int childAge=age+(entry.ChildLaterSlot?0:1);Projectile sample;
                    if(!ContentSamples.ProjectilesByType.TryGetValue((int)entry.Shot.localAI[0],out sample))return;
                    var receive=HostAttackReceive.Capture(combat.Player,sample,timeline.Identity.Slot,clock.BeforeNpc,clock.NextWorld || !entry.ChildLaterSlot);
                    entry.Contact=AttackIntercept.Solve(entry.KillOrigin.X,entry.KillOrigin.Y,motion,timeline,childAge,0,Passage,clock.FirstTick-age,receive.Allows);
                    if(entry.Shot.timeLeft<=entry.Shot.extraUpdates+1)
                    {
                        Vector2 center=entry.Shot.Center,velocity=entry.Shot.velocity;bool clear=true;
                        // AI78 steers back toward its sealed launch angle on
                        // every subupdate before damping and movement. Natural
                        // expiry consumes the resulting Center, not a straight
                        // .96 extrapolation of its random birth velocity.
                        for(int step=0;step<entry.Shot.timeLeft;step++)
                        {
                            var old=center;double angle=entry.Shot.ai[0].ToRotationVector2().ToRotation()-velocity.ToRotation();
                            if(angle>Math.PI)angle-=Math.PI*2;if(angle<-Math.PI)angle+=Math.PI*2;
                            velocity=velocity.RotatedBy(angle*.05000000074505806);velocity*=.96f;center+=velocity;
                            if(!Passage(old.X,old.Y,center.X,center.Y,entry.Shot.width,entry.Shot.height)){clear=false;break;}
                        }
                        entry.NextKillOrigin=center;
                        if(clear)entry.NextKillContact=AttackIntercept.Solve(center.X,center.Y,motion,timeline,childAge,0,Passage,clock.FirstTick-age,receive.Allows);
                    }
                }
            }
            entry.Prepared=Main.GameUpdateCount;entry.Target=timeline.Identity;
        }
        private bool Passage(float x,float y,float nx,float ny,float width,float height){return terrain.ProjectilePassage(x,y,nx,ny,(int)width,(int)height);}
        internal CombatCursorScope BeginOpening(Player player,Item item)
        {
            if(!openingUsable || opening==null || !combat.Attack.Permission || !ReferenceEquals(player,opening.Player) || !ReferenceEquals(item,opening.Weapon) || !Allowed(opening) || openingStep!=Main.GameUpdateCount || openingAmmo==null || !openingAmmo.IdentityMatches(player,item) || !ReferenceEquals(preparedTimeline,observation.Prediction.Cache.Read(1)) || !preparedTimeline.Identity.Equals(observation.Selection.Target))return null;
            return CombatCursorScope.Begin(openingPoint);
        }
        private static bool TryChildLaterSlot(Projectile parent,out bool later)
        {later=false;if(Terraria.Testing.DebugOptions.Shared_RandomizeProjectileSlots)return false;for(int i=0;i<Main.maxProjectiles;i++)if(!Main.projectile[i].active){later=i>parent.whoAmI;return true;}return false;}
        internal CombatCursorScope BeginKill(Projectile shot)
        {
            Controlled entry;if(shot.type!=444 || !shots.TryGetValue((int)shot.key,out entry) || !ReferenceEquals(entry.Shot,shot))return null;
            bool later;
            if(!Valid(entry) || Main.GameUpdateCount<entry.Prepared || Main.GameUpdateCount-entry.Prepared>1 || !entry.Target.Equals(observation.Selection.Target) || !terrain.Unchanged || !TryChildLaterSlot(shot,out later) || entry.ChildLaterSlot!=later)
            {entry.Contact=entry.NextKillContact=null;return null;}
            var result=Vector2.DistanceSquared(shot.Center,entry.KillOrigin)<.01f?entry.Contact:Vector2.DistanceSquared(shot.Center,entry.NextKillOrigin)<.01f?entry.NextKillContact:null;
            entry.Contact=entry.NextKillContact=null;
            if(result==null || !ReferenceEquals(result.Timeline,observation.Prediction.Cache.Read(1)))return null;
            return CombatCursorScope.Begin(new Vector2(result.AimX,result.AimY));
        }
        internal AttackContact ExpectedImpact
        {
            get
            {
                foreach(var entry in shots.Values)
                {
                    var result=entry.Type==444?entry.NextKillContact:entry.Contact;
                    if(!Valid(entry) || entry.Prepared!=Main.GameUpdateCount || result==null || !entry.Target.Equals(observation.Selection.Target) || !ReferenceEquals(result.Timeline,observation.Prediction.Cache.Read(1)))continue;
                    if(HostGuidedAttack.Handles(entry.Type) && (!combat.Player.channel || entry.Shot.ai[0]<0 || combat.Player.HeldItem.shoot!=entry.Type))continue;
                    if(HostHeldAttack.Handles(entry.Type) && !HostFlailAttack.Handles(entry.Type) && !combat.Player.channel)continue;
                    if(entry.Navigation!=null && (!combat.Player.channel || entry.Shot.ai[0]<0))continue;
                    if(entry.ContactChild!=null && !entry.ContactChild.Valid(entry.Shot))continue;
                    return result;
                }
                return null;
            }
        }
        internal CombatCursorScope BeginAI(Projectile shot)
        {
            Controlled entry;if(!shots.TryGetValue((int)shot.key,out entry) || !ReferenceEquals(entry.Shot,shot))return null;
            // Bubble AI never reads the cursor. Its separate Kill receipt must
            // survive these natural AI calls, including a stationary bubble
            // whose current and expiry origins are exactly the same.
            if(entry.Type==444)return null;
            if(!Valid(entry) || entry.Prepared!=Main.GameUpdateCount || entry.Contact==null && !entry.HasPoint || !observation.Selection.HasTarget || !entry.Target.Equals(observation.Selection.Target) || !ReferenceEquals(preparedTimeline,observation.Prediction.Cache.Read(1)) || !terrain.Unchanged)
            {entry.Contact=null;return null;}
            if(HostHeldAttack.Handles(entry.Type))
            {
                if(!HostHeldAttack.Consumes(combat.Player,shot))return null;
                if(entry.Ammo!=null && !entry.Ammo.IdentityMatches(combat.Player,combat.Player.HeldItem)){entry.Contact=null;entry.HasPoint=false;return null;}
                return CombatCursorScope.Begin(entry.Point);
            }
            if(entry.Navigation!=null)
            {if(!entry.HasPoint || !combat.Player.channel || shot.ai[0]<0){entry.Contact=null;return null;}return CombatCursorScope.Begin(entry.Point);}
            if(!HostGuidedAttack.Handles(entry.Type) || shot.ai[0]<0 || !combat.Player.channel || combat.Player.HeldItem.shoot!=entry.Type){entry.Contact=null;return null;}
            return CombatCursorScope.Begin(new Microsoft.Xna.Framework.Vector2(entry.Contact.AimX,entry.Contact.AimY));
        }
    }
}
