using System;
using System.Collections.Generic;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // Only currently observed other receivers are held stationary. Their
    // future motion/other attacks remain conditions, not a second prediction
    // world. Damage uses endpoint rectangles and NPC array order, not distance.
    internal sealed partial class HostAttackObstacles
    {
        private sealed class Receiver
        {
            internal NPC Npc;internal NpcIdentity Identity;internal Rectangle Box,RawBox;internal Vector2 Position,Offset;internal HostAttackReceive Receive,ArcReceive;
            internal int OwnerImmune,LocalImmune;internal uint StaticExpiry;internal bool Unknown,Chaseable,Reflects,RandomReflection,NoTileCollide;
        }
        private sealed class HitHistory
        {
            internal int[] OwnerNext;
            internal bool[] OwnerWritten;
            internal Dictionary<int,int[]> StaticNext;
            internal ArcOwnerLease ArcOwner;
            internal int[] ArcMelee;
            internal bool ArcHandReset;
            internal IcePlacementLease IcePlacement;
            internal HostPaperAirplaneAttack.State? PaperClock;
        }
        private sealed class AiReflector
        {
            internal NPC Npc;internal NpcIdentity Identity;internal Rectangle Box;internal float Phase,Clock;
        }
        private readonly List<Receiver> receivers=new List<Receiver>();
        private readonly List<AiReflector> aiReflectors=new List<AiReflector>();
        private readonly Player player;private readonly Projectile shot;private NpcTrajectory capturedTimeline;private readonly HostAttackObstacles timelineOwner;
        private NpcTrajectory timeline {get{return timelineOwner==null?capturedTimeline:timelineOwner.timeline;}}
        private readonly HostAttackReceive selected;
        private readonly HostAttackReceive arcSelected;
        private readonly int delay;private readonly bool beforeNpc,nextWorld,selectedChaseable,selectedReflects,goodWorld,expert,multiplayer;private int remaining;
        private readonly HitHistory history;private readonly bool ownsHistory;
        private readonly int[] initialLocal,localNext,policyLocal;private readonly List<int> otherHitSlots=new List<int>();
        private HostProjectileBirthSlots birthSlots;
        private bool? beeOwnerStrong;
        private Player beeOwner;
        private int beeOwnerSlot=-1;
        private bool localCleared;
        private Player cursorPlayer;
        private int cursorSlot,cursorScreenHeight,cursorWorldWidth,cursorWorldHeight;
        private float cursorGravity;
        private float shotRotation;
        private bool shotFacingCaptured;
        private Player phoenixOwner;
        private int phoenixOwnerSlot,phoenixCounter;
        private bool phoenixApprentice;
        private Vector2 cursorScreen;
        private readonly List<HostProjectileTargets> targetLeases=new List<HostProjectileTargets>();
        private readonly List<HostProjectileFamilyLease> familyLeases=new List<HostProjectileFamilyLease>();
        private readonly List<HostAttackObstacles> receiveLeases=new List<HostAttackObstacles>();
        private readonly List<HostAttackObstacles> receivePolicies=new List<HostAttackObstacles>();
        private readonly List<HostAttackObstacles> preparationLeases=new List<HostAttackObstacles>();
        private readonly PolicyInputs policyInputs;
        private readonly struct PolicyInputs
        {
            private readonly int type,owner,penetration,identity;
            private readonly bool local,shared,melee,trap,npc,friendly,hostile,minion,sentry;
            internal PolicyInputs(Projectile p){type=p.type;owner=p.owner;penetration=p.maxPenetrate;identity=p.immunityIdentity;local=p.usesLocalNPCImmunity;shared=p.usesIDStaticNPCImmunity;melee=p.usesOwnerMeleeHitCD;trap=p.trap;npc=p.npcProj;friendly=p.friendly;hostile=p.hostile;minion=p.minion;sentry=p.sentry;}
            internal bool Matches(Projectile p){return type==p.type && owner==p.owner && penetration==p.maxPenetrate && identity==p.immunityIdentity && local==p.usesLocalNPCImmunity && shared==p.usesIDStaticNPCImmunity && melee==p.usesOwnerMeleeHitCD && trap==p.trap && npc==p.npcProj && friendly==p.friendly && hostile==p.hostile && minion==p.minion && sentry==p.sentry;}
        }
        internal int Remaining {get{return remaining;}}
        internal int OtherHits {get;private set;}
        internal int SelectedHits {get;private set;}
        internal int OtherHitSlot(int index){return otherHitSlots[index];}
        internal void MakeUnlimited(){remaining=-1;}
        internal void BeginCandidate()
        {
            remaining=shot.penetrate;OtherHits=SelectedHits=0;otherHitSlots.Clear();localCleared=false;
            // Children share writes made by earlier native slot updates. Only
            // the root candidate owns the reset; a child's first Pass must not
            // erase an already received sibling hit.
            if(ownsHistory){history.ArcHandReset=false;if(history.OwnerNext!=null){Array.Clear(history.OwnerNext,0,history.OwnerNext.Length);Array.Clear(history.OwnerWritten,0,history.OwnerWritten.Length);}if(history.StaticNext!=null)history.StaticNext.Clear();if(localNext!=null)Array.Copy(initialLocal,localNext,localNext.Length);if(history.ArcOwner!=null)Array.Copy(history.ArcOwner.Melee,history.ArcMelee,history.ArcMelee.Length);}
        }
        internal HostAttackObstacles(Player player,Projectile shot,NpcTrajectory timeline,int delay,bool beforeNpc,bool nextWorld)
        {
            selectedReflects=Main.npc[timeline.Identity.Slot].reflectsProjectiles;goodWorld=Main.getGoodWorld;expert=Main.expertMode;multiplayer=Main.netMode==1;
            history=new HitHistory();ownsHistory=true;
            // Ordinary owner-only attacks keep no 200-entry private arrays
            // until a real preceding receive actually writes shared immunity.
            if(shot.usesLocalNPCImmunity){initialLocal=new int[Main.maxNPCs];localNext=new int[Main.maxNPCs];policyLocal=(int[])shot.localNPCImmunity.Clone();for(int slot=0;slot<initialLocal.Length;slot++){int value=shot.localNPCImmunity[slot];initialLocal[slot]=value<0?int.MaxValue:value;localNext[slot]=initialLocal[slot];}}
            this.player=player;this.shot=shot;capturedTimeline=timeline;policyInputs=new PolicyInputs(shot);this.delay=delay;this.beforeNpc=beforeNpc;this.nextWorld=nextWorld;selected=HostAttackReceive.Capture(player,shot,timeline.Identity.Slot,beforeNpc,nextWorld);selectedChaseable=Main.npc[timeline.Identity.Slot].CanBeChasedBy(shot);arcSelected=shot.usesOwnerMeleeHitCD?HostAttackReceive.CaptureIgnoringMelee(player,shot,timeline.Identity.Slot,beforeNpc,nextWorld):selected;
            for(int slot=0;slot<Main.maxNPCs;slot++)
            {
                var npc=Main.npc[slot];if(slot==timeline.Identity.Slot)continue;
                // AI87 scans shots before its own movement even while it is
                // invulnerable. It is not a Damage receiver qualification.
                if(expert && AiReflectorEligible(npc))aiReflectors.Add(new AiReflector{Npc=npc,Identity=CombatSelection.Identity(npc,timeline.Identity.Session),Box=npc.Hitbox,Phase=npc.ai[0],Clock=npc.ai[1]});
                if(!Eligible(player,shot,npc))continue;
                var receive=HostAttackReceive.Capture(player,shot,slot,beforeNpc,nextWorld);receivers.Add(new Receiver{Npc=npc,Identity=CombatSelection.Identity(npc,timeline.Identity.Session),Box=CombatSelection.ReceiveBounds(npc),RawBox=ReflectionBounds(npc),Reflects=npc.reflectsProjectiles,RandomReflection=goodWorld && npc.aiStyle==53,Position=npc.position,Offset=npc.netOffset,OwnerImmune=npc.immune[player.whoAmI],LocalImmune=shot.localNPCImmunity[slot],StaticExpiry=shot.usesIDStaticNPCImmunity?Projectile.perIDStaticNPCImmunity[shot.immunityIdentity,slot]:0,Receive=receive,ArcReceive=shot.usesOwnerMeleeHitCD?HostAttackReceive.CaptureIgnoringMelee(player,shot,slot,beforeNpc,nextWorld):receive,NoTileCollide=npc.noTileCollide,Unknown=NPCID.Sets.ZappingJellyfish[npc.type],Chaseable=npc.CanBeChasedBy(shot)});
            }
            receivePolicies.Add(this);
        }
        private HostAttackObstacles(HostAttackObstacles parent,Projectile spawned,bool copyLocal,bool candidate=false)
        {
            selectedReflects=parent.selectedReflects;goodWorld=parent.goodWorld;expert=parent.expert;multiplayer=parent.multiplayer;aiReflectors=parent.aiReflectors;
            targetLeases=parent.targetLeases;
            familyLeases=parent.familyLeases;
            receiveLeases=parent.receiveLeases;
            receivePolicies=parent.receivePolicies;preparationLeases=parent.preparationLeases;
            player=parent.player;shot=spawned;capturedTimeline=parent.timeline;timelineOwner=candidate?null:parent;policyInputs=new PolicyInputs(spawned);delay=parent.delay;beforeNpc=parent.beforeNpc;nextWorld=parent.nextWorld;history=candidate?new HitHistory():parent.history;ownsHistory=candidate;
            // Capture immutable eligibility BEFORE allocating/capturing another
            // receiver set. Local immunity is part of the key, never collapsed
            // between observed members; mutable owner/static history is not.
            HostAttackObstacles policy=null;foreach(var prior in receivePolicies)if(prior.CanShareReceive(spawned)){policy=prior;break;}
            if(spawned.usesLocalNPCImmunity)
            {
                // The policy owns this immutable initial array. Sharing it
                // after exact matching avoids another 200-counter capture per
                // sibling/direction; only mutable deadlines belong to the fork.
                policyLocal=policy!=null?policy.policyLocal:(int[])spawned.localNPCImmunity.Clone();
                localNext=copyLocal && parent.localNext!=null?(int[])parent.localNext.Clone():new int[Main.maxNPCs];
                if(!copyLocal)for(int slot=0;slot<localNext.Length;slot++){int value=policyLocal[slot];localNext[slot]=value<0?int.MaxValue:value;}
                initialLocal=candidate?(int[])localNext.Clone():localNext;
            }
            if(policy!=null){selected=policy.selected;arcSelected=policy.arcSelected;selectedChaseable=policy.selectedChaseable;receivers=policy.receivers;remaining=spawned.penetrate;return;}
            selected=HostAttackReceive.Capture(player,spawned,timeline.Identity.Slot,beforeNpc,nextWorld);selectedChaseable=Main.npc[timeline.Identity.Slot].CanBeChasedBy(spawned);arcSelected=spawned.usesOwnerMeleeHitCD?HostAttackReceive.CaptureIgnoringMelee(player,spawned,timeline.Identity.Slot,beforeNpc,nextWorld):selected;
            foreach(var prior in parent.receivers)
            {
                if(!Eligible(player,spawned,prior.Npc))continue;
                var receive=HostAttackReceive.Capture(player,spawned,prior.Identity.Slot,beforeNpc,nextWorld);receivers.Add(new Receiver{Npc=prior.Npc,Identity=prior.Identity,Box=prior.Box,RawBox=prior.RawBox,Reflects=prior.Reflects,RandomReflection=prior.RandomReflection,Position=prior.Position,Offset=prior.Offset,OwnerImmune=prior.OwnerImmune,LocalImmune=spawned.localNPCImmunity[prior.Identity.Slot],StaticExpiry=spawned.usesIDStaticNPCImmunity?Projectile.perIDStaticNPCImmunity[spawned.immunityIdentity,prior.Identity.Slot]:0,Receive=receive,ArcReceive=spawned.usesOwnerMeleeHitCD?HostAttackReceive.CaptureIgnoringMelee(player,spawned,prior.Identity.Slot,beforeNpc,nextWorld):receive,NoTileCollide=prior.NoTileCollide,Unknown=prior.Unknown,Chaseable=prior.Npc.CanBeChasedBy(spawned)});
            }
            remaining=spawned.penetrate;
            // A child's native immunity identity can differ from its parent's
            // (e.g. bottle510 -> shared cloud511, arrow183 -> bee181). Keep
            // each distinct captured receive policy in the root consume
            // lease. Repeated search directions must not retain duplicates.
            if(!SameReceivePolicy(parent))
            {bool found=false;foreach(var lease in receiveLeases)if(SameReceivePolicy(lease)){found=true;break;}if(!found)receiveLeases.Add(this);}
            receivePolicies.Add(this);
        }
        private bool CanShareReceive(Projectile value)
        {
            if(!policyInputs.Matches(value))return false;
            if(!value.usesLocalNPCImmunity)return true;
            // Compare original counters, not a candidate's written
            // deadlines. The live/initial shot arrays are never replay scratch.
            for(int slot=0;slot<Main.maxNPCs;slot++)if(policyLocal[slot]!=value.localNPCImmunity[slot])return false;return true;
        }
        // Independent directions own distinct histories/checkpoints. Siblings
        // spawned inside one direction still share that direction's history.
        internal HostAttackObstacles ForkCandidate(){return new HostAttackObstacles(this,shot,false,true);}
        internal void ExtendPreparation(NpcTrajectory value)
        {if(!HostAttackAim.SolvePreparation.Extends(timeline,value))throw new InvalidOperationException("Attack prefix changed during continuation.");capturedTimeline=value;}
        internal void IncludePreparation(HostAttackObstacles value){if(!ReferenceEquals(this,value) && !preparationLeases.Contains(value))preparationLeases.Add(value);}
        private bool SameReceivePolicy(HostAttackObstacles other)
        {
            if(shot.type!=other.shot.type || shot.maxPenetrate!=other.shot.maxPenetrate || shot.usesLocalNPCImmunity!=other.shot.usesLocalNPCImmunity || shot.usesIDStaticNPCImmunity!=other.shot.usesIDStaticNPCImmunity || shot.immunityIdentity!=other.shot.immunityIdentity || shot.usesOwnerMeleeHitCD!=other.shot.usesOwnerMeleeHitCD || shot.trap!=other.shot.trap || shot.npcProj!=other.shot.npcProj || selectedChaseable!=other.selectedChaseable || !selected.Same(other.selected) || receivers.Count!=other.receivers.Count)return false;
            for(int i=0;i<receivers.Count;i++)
            {var a=receivers[i];var b=other.receivers[i];if(!a.Identity.Equals(b.Identity) || a.LocalImmune!=b.LocalImmune || a.StaticExpiry!=b.StaticExpiry || a.Chaseable!=b.Chaseable || !a.Receive.Same(b.Receive))return false;}
            return true;
        }
        // owner/static writes belong to this finite attack family. Local
        // writes belong to one projectile and copy only where native spawn
        // explicitly calls CopyLocalNPCImmunityTo (710), never by default.
        internal HostAttackObstacles ForkForSpawn(Projectile privateShot,bool copyLocal=false)
        {return new HostAttackObstacles(this,privateShot,copyLocal);}
        internal HostProjectileBirthSlots CaptureBirthSlots()
        {return birthSlots??(birthSlots=HostProjectileBirthSlots.Capture());}
        internal bool CaptureBeeOwner()
        {
            if(!beeOwnerStrong.HasValue)
            {
                // Idle/cooldown previews advance a private Player pose. Its
                // equipment value is the calculation premise, but changes
                // must be checked against the actual numbered owner later.
                beeOwnerStrong=player.strongBees;beeOwnerSlot=player.whoAmI;
                if(beeOwnerSlot>=0 && beeOwnerSlot<Main.player.Length)beeOwner=Main.player[beeOwnerSlot];
            }
            return beeOwnerStrong.Value;
        }
        // O(1) member identity check is also safe for presentation. The full
        // Unchanged scene/terrain lease remains a preparation/consume check.
        internal bool BeeOwnerCurrent
        {get{return !beeOwnerStrong.HasValue || beeOwner!=null && beeOwnerSlot>=0 && beeOwnerSlot<Main.player.Length && ReferenceEquals(Main.player[beeOwnerSlot],beeOwner) && beeOwnerStrong.Value==beeOwner.strongBees;}}
        // Only births using screen-to-world conversion capture this premise.
        // Keep that lease local to their prepare/consume path; no scene scan
        // or unrelated camera dependency is added to other attack families.
        internal void CaptureCursorMapping(Player value)
        {
            // Prediction may pass a private future pose. Validate against its
            // numbered live owner, as with the equipment premise above.
            cursorSlot=value.whoAmI;cursorPlayer=cursorSlot>=0 && cursorSlot<Main.player.Length?Main.player[cursorSlot]??value:value;
            cursorGravity=value.gravDir;cursorScreen=Main.screenPosition;cursorScreenHeight=Main.screenHeight;cursorWorldWidth=Main.maxTilesX;cursorWorldHeight=Main.maxTilesY;
        }
        private bool CursorMappingCurrent
        {get{return cursorPlayer==null || cursorSlot>=0 && cursorSlot<Main.player.Length && ReferenceEquals(Main.player[cursorSlot],cursorPlayer) && cursorPlayer.gravDir==cursorGravity && Main.screenPosition==cursorScreen && Main.screenHeight==cursorScreenHeight && Main.maxTilesX==cursorWorldWidth && Main.maxTilesY==cursorWorldHeight;}}
        internal void CaptureShotFacing(Player value)
        {CaptureCursorMapping(value);shotRotation=value.fullRotation;shotFacingCaptured=true;}
        internal bool CapturePhoenixOwner(Player value)
        {
            phoenixOwnerSlot=value.whoAmI;phoenixOwner=phoenixOwnerSlot>=0 && phoenixOwnerSlot<Main.player.Length?Main.player[phoenixOwnerSlot]??value:value;
            phoenixCounter=value.phantomPhoneixCounter;phoenixApprentice=value.setApprenticeT3;return phoenixApprentice;
        }
        private bool PhoenixOwnerCurrent
        {get{return phoenixOwner==null || phoenixOwnerSlot>=0 && phoenixOwnerSlot<Main.player.Length && ReferenceEquals(Main.player[phoenixOwnerSlot],phoenixOwner) && phoenixOwner.phantomPhoneixCounter==phoenixCounter && phoenixOwner.setApprenticeT3==phoenixApprentice;}}
        // Constant-time owner inputs are safe during presentation. The family,
        // terrain and receive scene scans stay in preparation/real consumption.
        internal bool BirthInputsCurrent
        {get{return BirthInputsMatch(false);}}
        // All candidate forks share this one preparation's observed wind and
        // visual clock. Resetting private hit history must not recapture time.
        internal void CapturePaperClock(HostPaperAirplaneAttack.State value){history.PaperClock=value;}
        private bool BirthInputsMatch(bool afterHandStart)
        {return BookHandCurrent && BeeOwnerCurrent && CursorMappingCurrent && PhoenixOwnerCurrent && (!history.PaperClock.HasValue || history.PaperClock.Value.Current) && (history.ArcOwner==null || (afterHandStart?history.ArcOwner.MembersAfterHandStart:history.ArcOwner.MembersCurrent)) && (!shotFacingCaptured || cursorPlayer.fullRotation==shotRotation);}
        internal void CaptureProjectileFamily(Projectile[] members,int firstSceneType=0,int lastSceneType=0)
        {familyLeases.Add(new HostProjectileFamilyLease(members,firstSceneType,lastSceneType));CaptureBirthSlots();}
        internal void CaptureOwnedProjectileFamily(Projectile[] members,int owner,int[] sceneTypes)
        {familyLeases.Add(new HostProjectileFamilyLease(members,owner,sceneTypes));CaptureBirthSlots();}
        internal bool FamilyMembersCurrent
        {get{if(!BirthInputsCurrent)return false;foreach(var family in familyLeases)if(!family.MembersCurrent)return false;return true;}}
        // Chase permission is a separate native query from Damage permission.
        // Each finite stage captures it once at Prepare; all candidate replays
        // and spawned siblings share immutable rows and this consume lease.
        // Draw uses the existing presentation lease and never runs Current.
        internal HostProjectileTargets CaptureChaseTargets(Projectile privateShot)
        {var lease=HostProjectileTargets.Capture(privateShot,timeline.Identity.Session);targetLeases.Add(lease);return lease;}
        internal HostProjectileTargets CaptureWetChaseTargets(Projectile privateShot)
        {var lease=HostProjectileTargets.CaptureWetChase(privateShot,timeline.Identity.Session);targetLeases.Add(lease);return lease;}
        internal HostProjectileTargets CaptureTownBodies(Projectile privateShot)
        {var lease=HostProjectileTargets.CaptureTownBodies(privateShot,timeline.Identity.Session);targetLeases.Add(lease);return lease;}
        internal HostProjectileTargets CaptureAttachmentTargets(Projectile privateShot)
        {var lease=HostProjectileTargets.CaptureAttachments(privateShot,timeline.Identity.Session);targetLeases.Add(lease);return lease;}
        private bool DynamicAllows(int slot,int outer)
        {
            if(!(shot.maxPenetrate==1 && !shot.usesLocalNPCImmunity && !shot.usesIDStaticNPCImmunity) && history.OwnerNext!=null && outer<history.OwnerNext[slot])return false;
            if(!shot.usesLocalNPCImmunity && !shot.usesIDStaticNPCImmunity)return true;
            int local=shot.usesLocalNPCImmunity?localNext[slot]:int.MaxValue,shared=int.MaxValue;int[] values;
            if(shot.usesIDStaticNPCImmunity)shared=history.StaticNext!=null && history.StaticNext.TryGetValue(shot.immunityIdentity,out values)?values[slot]:0;
            return outer>=Math.Min(local,shared);
        }
        private bool CapturedAllows(HostAttackReceive receive,int slot,int outer)
        {
            if(localCleared)receive=receive.WithoutLocal();
            if(history.OwnerWritten!=null && history.OwnerWritten[slot])receive=receive.WithoutOwner();
            return receive.Allows(outer);
        }
        internal bool SelectedAllows(int outer){return CapturedAllows(selected,timeline.Identity.Slot,outer) && DynamicAllows(timeline.Identity.Slot,outer);}
        internal void ClearLocalExceptFresh(int outer)
        {
            // Native gem Kill preserves only hits whose local counter still
            // equals its full cooldown. All other old local entries clear.
            // Private absolute deadlines preserve that same outer boundary.
            if(!shot.usesLocalNPCImmunity || shot.usesIDStaticNPCImmunity)return;
            localCleared=true;for(int i=0;i<localNext.Length;i++)if(localNext[i]!=outer+shot.localNPCHitCooldown)localNext[i]=0;
        }
        internal void ResetLocalHitHistory()
        {
            // Attached-shot release/recall calls ResetLocalNPCImmunity before
            // its immediate Damage pass. Only this projectile's local history
            // clears; owner and shared static deadlines retain their order.
            localCleared=true;if(localNext!=null)Array.Clear(localNext,0,localNext.Length);
        }
        // FindTargetWithLineOfSight excludes local immunity only; owner and
        // static Damage gates must not silently remove an autonomous target.
        internal bool LocalTargetAllowed(int slot,int outer)
        {return localNext==null || outer>=localNext[slot];}
        private static bool Eligible(Player player,Projectile shot,NPC npc)
        {
            return npc!=null && npc.active && !npc.dontTakeDamage && shot.friendly && (!npc.friendly || shot.type==318 || npc.type==22 && player.killGuide || npc.type==54 && player.killClothier || NPCID.Sets.ZappingJellyfish[npc.type]) && player.CanNPCBeHitByPlayerOrPlayerProjectile(npc,shot);
        }
        internal bool Unchanged
        {get{return ReadUnchanged(false);}}
        // Only the real hand consumer may recognize the exact native changes
        // already simulated by SolvePrepared: new-animation melee reset and
        // the once-per-ItemCheck chlorophyte counter decrement. An observed
        // flight or ordinary preview never borrows this transition allowance.
        internal bool UnchangedForHandStart
        {get{return ReadUnchanged(false) || history.ArcHandReset && ReadUnchanged(true);}}
        private bool ReadUnchanged(bool afterHandStart)
        {
                // Native chase permission is distinct from Damage permission.
                // The selected body is excluded from the other receiver rows,
                // so its autonomous-targeting dependency needs its own lease.
                if(goodWorld!=Main.getGoodWorld || expert!=Main.expertMode || multiplayer!=(Main.netMode==1) || selectedReflects!=Main.npc[timeline.Identity.Slot].reflectsProjectiles || !BirthInputsMatch(afterHandStart) || history.ArcOwner!=null && !(afterHandStart?history.ArcOwner.CurrentAfterHandStart:history.ArcOwner.Current) || birthSlots!=null && !birthSlots.Current)return false;
                foreach(var targets in targetLeases)if(!targets.Current)return false;
                if(history.IcePlacement!=null && !history.IcePlacement.Current)return false;
                foreach(var family in familyLeases)if(!family.Current)return false;
                if(!ReceiveUnchanged())return false;
                foreach(var lease in receiveLeases)if(!ReferenceEquals(lease,this) && !lease.ReceiveUnchanged())return false;
                foreach(var lease in preparationLeases)if(!lease.Unchanged)return false;
                return true;
        }
        private bool SameCapturedReceive(HostAttackReceive normal,HostAttackReceive arc,int slot)
        {return history.ArcOwner==null?normal.Same(HostAttackReceive.Capture(player,shot,slot,beforeNpc,nextWorld)):arc.Same(HostAttackReceive.CaptureIgnoringMelee(player,shot,slot,beforeNpc,nextWorld));}
        private bool ReceiveUnchanged()
        {
                if(selectedChaseable!=Main.npc[timeline.Identity.Slot].CanBeChasedBy(shot) || !SameCapturedReceive(selected,arcSelected,timeline.Identity.Slot))return false;
                int at=0,reflectAt=0;
                // Recheck the qualification set too: an inactive slot or an
                // invulnerable/friendly body can become a new receiver between
                // preparation and consumption without changing a stored row.
                for(int slot=0;slot<Main.maxNPCs;slot++)
                {
                    if(slot==timeline.Identity.Slot)continue;
                    if(ownsHistory && expert && AiReflectorEligible(Main.npc[slot]))
                    {
                        if(reflectAt>=aiReflectors.Count || aiReflectors[reflectAt].Identity.Slot!=slot)return false;
                        var reflector=aiReflectors[reflectAt++];var npc=Main.npc[slot];
                        if(!CombatSelection.Valid(reflector.Identity,timeline.Identity.Session) || reflector.Box!=npc.Hitbox || reflector.Phase!=npc.ai[0] || reflector.Clock!=npc.ai[1])return false;
                    }
                    if(!Eligible(player,shot,Main.npc[slot]))continue;
                    if(at>=receivers.Count || receivers[at].Identity.Slot!=slot)return false;var r=receivers[at++];
                    if(history.ArcOwner!=null && r.Npc.noTileCollide!=r.NoTileCollide || r.Reflects!=r.Npc.reflectsProjectiles || r.RandomReflection!=(goodWorld && r.Npc.aiStyle==53) || r.RawBox!=ReflectionBounds(r.Npc) || r.Chaseable!=r.Npc.CanBeChasedBy(shot) || !CombatSelection.Valid(r.Identity,timeline.Identity.Session) || r.Npc.position!=r.Position || r.Npc.netOffset!=r.Offset || CombatSelection.ReceiveBounds(r.Npc)!=r.Box || r.Npc.immune[player.whoAmI]!=r.OwnerImmune || shot.localNPCImmunity[r.Identity.Slot]!=r.LocalImmune || shot.usesIDStaticNPCImmunity && Projectile.perIDStaticNPCImmunity[shot.immunityIdentity,r.Identity.Slot]!=r.StaticExpiry || !SameCapturedReceive(r.Receive,r.ArcReceive,slot))return false;
                }
                return at==receivers.Count && (!ownsHistory || reflectAt==aiReflectors.Count);
        }
        private static bool AiReflectorEligible(NPC npc)
        {return npc!=null && npc.active && npc.aiStyle==87;}
        // Called once before the FIRST projectile subupdate of an outer.
        // AI87 reflects the pre-AI raw Hitbox, before NPC movement and without
        // damage/immunity gates. A shot born later in Projectiles has already
        // missed that action's NPC scan; derived callers skip their birth outer.
        internal bool BeforeNpc(int tick,int outer,float x,float y,int width=0,int height=0)
        {
            if(!expert || !Reflectable(shot) || !beforeNpc && !nextWorld && outer<=1)return true;
            if(width==0)width=shot.width;if(height==0)height=shot.height;
            var body=new Rectangle((int)(x-width*.5f),(int)(y-height*.5f),width,height);
            if(timeline.Identity.Type>=473 && timeline.Identity.Type<=476)
            {
                if(tick<1 || tick>=timeline.Count)return false;
                var prior=timeline[tick-1].Bounds;var next=timeline[tick];
                if((next.ReflectsProjectiles || next.ProjectileReflectionUnknown) && body.Intersects(new Rectangle((int)prior.X,(int)prior.Y,(int)prior.Width,(int)prior.Height)))return false;
            }
            int actions=outer-(!beforeNpc && !nextWorld?1:0);
            foreach(var reflector in aiReflectors)
            {
                // Other bodies retain the same explicit stationary-observer
                // condition as Damage obstacles. Advance the observed guard's
                // finite clock; never invent a future random action choice.
                if(reflector.Phase==3 && (multiplayer || actions<=Math.Max(1,(int)Math.Ceiling(180-reflector.Clock))) && body.Intersects(reflector.Box))return false;
            }
            return true;
        }
        internal bool Pass(int k,int tick,float x,float y)
        {return Pass(k,tick,x,y,0);}
        internal bool Pass(int k,int tick,float x,float y,int maximumRemaining)
        {return PassOuter(k,tick,delay+(k-1)/(shot.extraUpdates+1)+1,x,y,maximumRemaining);}
        internal bool PassOuter(int k,int tick,int outer,float x,float y,int maximumRemaining=0)
        {return PassShapeOuter(k,tick,outer,x,y,shot.width,shot.height,false,maximumRemaining);}
        internal bool PassShapeOuter(int k,int tick,int outer,float x,float y,int width,int height,bool unlimited=false,int maximumRemaining=0,Func<int,int,bool> onOtherHit=null)
        {return PassCore(k,tick,outer,x,y,width,height,unlimited,maximumRemaining,onOtherHit,null,false);}
        // A native custom collider (e.g. gem trails) is one NPC-array pass,
        // not separate damage passes per rectangle. Reflection still tests
        // the real current projectile body supplied by width/height.
        internal bool PassCustomOuter(int k,int tick,int outer,float x,float y,int width,int height,Func<MotionRect,bool> collision,bool unlimited=false,int maximumRemaining=0,bool consumeSelected=false,Func<int,int,bool> onHit=null,Func<int,int,int> beforeHitPenetration=null)
        {return PassCore(k,tick,outer,x,y,width,height,unlimited,maximumRemaining,onHit,collision,consumeSelected,beforeHitPenetration);}
        private bool PassCore(int k,int tick,int outer,float x,float y,int width,int height,bool unlimited,int maximumRemaining,Func<int,int,bool> onOtherHit,Func<MotionRect,bool> collision,bool consumeSelected,Func<int,int,int> beforeHitPenetration=null,Func<MotionRect,bool,bool> ownerQualification=null,int meleeCooldown=0,bool arc=false)
        {
            if(k==1)BeginCandidate();
            if(maximumRemaining>0 && remaining>maximumRemaining)remaining=maximumRemaining;
            if(remaining==0 && !unlimited)return false;
            // Only an actual nonpositive-penetration Damage stage bypasses
            // consumption (e.g. Meowmere's temporary -1, or 710 Kill after
            // body penetration reached zero). A positive tile-death 710
            // still consumes its budget. Candidate history never resets here.
            var body=new Rectangle((int)(x-width*.5f),(int)(y-height*.5f),width,height);
            var target=timeline[tick];var b=target.ProjectileReceiveBounds;bool hitsTarget=target.CanReceive && (arc?ArcAllows(arcSelected,timeline.Identity.Slot,outer):SelectedAllows(outer)) && (ownerQualification==null || ownerQualification(target.OwnerBounds,target.NoTileCollide)) && (collision==null?body.Intersects(new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height)):collision(b));
            // Reflection happens after receive permission and before Strike,
            // at the native body after Damage_StartIteratingNPC adds netOffset.
            // Unknown reflection affects this attack's proof, not the path
            // or ordinary hand melee's independent receive permission.
            var raw=target.ReceiveBounds;
            bool reflectedTarget=hitsTarget && Reflectable(shot) && (target.ReflectsProjectiles || target.ProjectileReflectionUnknown || StarReflects(timeline.Identity.Type)) && body.Intersects(new Rectangle((int)raw.X,(int)raw.Y,(int)raw.Width,(int)raw.Height));
            bool selectedVisited=false;
            for(int at=0;at<=receivers.Count;at++)
            {
                if(hitsTarget && !selectedVisited && (at==receivers.Count || receivers[at].Identity.Slot>timeline.Identity.Slot))
                {
                    if(reflectedTarget)return false;
                    if(!consumeSelected)return true;
                    // A native pre-Strike exception may spend penetration
                    // before immunity is written (Molotov flame vs Eater).
                    // Keep it distinct from the later OnHit/normal decrement.
                    if(beforeHitPenetration!=null && !unlimited)remaining=beforeHitPenetration(timeline.Identity.Slot,remaining);
                    // An observed ambient family can consume the same local
                    // owner's static immunity before this weapon's member.
                    // It contributes no Contact, but remains a real ordered
                    // Damage event, including later slots in the same pass.
                    selectedVisited=true;SelectedHits++;
                    RecordHit(timeline.Identity.Slot,outer,unlimited?-1:remaining);
                    if(arc)RecordArcMelee(timeline.Identity.Slot,meleeCooldown);
                    if(onOtherHit!=null && !onOtherHit(timeline.Identity.Slot,remaining))return false;
                    if(!unlimited && remaining>0 && --remaining==0)return false;
                }
                if(at==receivers.Count)break;var r=receivers[at];
                if(!(arc?ArcAllows(r.ArcReceive,r.Identity.Slot,outer):DynamicAllows(r.Identity.Slot,outer) && CapturedAllows(r.Receive,r.Identity.Slot,outer)) || ownerQualification!=null && !ownerQualification(new MotionRect(r.Position.X+r.Offset.X,r.Position.Y+r.Offset.Y,r.RawBox.Width,r.RawBox.Height),r.NoTileCollide) || !(collision==null?body.Intersects(r.Box):collision(new MotionRect(r.Box.X,r.Box.Y,r.Box.Width,r.Box.Height))))continue;
                // A stationary foreign body's geometry does not freeze its
                // AI53 random reflection flag. Only a post-NPC birth's first
                // outer may use the observed flag before another NPC action.
                if(Reflectable(shot) && (r.Reflects || r.RandomReflection && (beforeNpc || nextWorld || outer>1) || StarReflects(r.Identity.Type)) && body.Intersects(r.RawBox))return false;
                if(r.Unknown)return false; // Native reflection/damage special cases are not ordinary consumption.
                if(beforeHitPenetration!=null && !unlimited)remaining=beforeHitPenetration(r.Identity.Slot,remaining);
                // Only a receiver that passes the native permission, immunity
                // and array-order gates is an OnHit event. Specialized motors
                // may advance their own copied clock after this real event.
                OtherHits++;otherHitSlots.Add(r.Identity.Slot);
                RecordHit(r.Identity.Slot,outer,unlimited?-1:remaining);
                if(arc)RecordArcMelee(r.Identity.Slot,meleeCooldown);
                // Specialized native OnHit may retarget immediately. It sees
                // this slot's written immunity but not a later slot's future
                // hit, and runs before native penetration is spent.
                if(onOtherHit!=null && !onOtherHit(r.Identity.Slot,remaining))return false;
                // Native writes immunity and executes OnHit before spending
                // the final penetration. A Kill burst and copied child local
                // immunity must see that last qualified receiver too.
                if(!unlimited && remaining>0 && --remaining==0)return false;
            }
            return !reflectedTarget;
        }
        private bool StarReflects(int type)
        {return goodWorld && (shot.type==955 || shot.type==728) && NPCID.Sets.ReflectStarShotsInForTheWorthy[type];}
        private static Rectangle ReflectionBounds(NPC npc)
        {return new Rectangle((int)(npc.position.X+npc.netOffset.X),(int)(npc.position.Y+npc.netOffset.Y),npc.width,npc.height);}
        private static bool Reflectable(Projectile p)
        {
            // Templates have no assigned damage yet. Every caller already
            // admits a damaging stage; this is the native finite role gate.
            return p.friendly && !p.hostile && (p.type==728 || p.type==955 || p.aiStyle==1 || p.aiStyle==2 || p.aiStyle==8 || p.aiStyle==21 || p.aiStyle==24 || p.aiStyle==28 || p.aiStyle==29 || p.aiStyle==131);
        }
        private void RecordHit(int slot,int outer,int penetration)
        {
            if(shot.usesIDStaticNPCImmunity)
            {
                if(penetration!=1 || shot.appliesImmunityTimeOnSingleHits)
                {WriteOwner(slot,0);int[] values;if(history.StaticNext==null)history.StaticNext=new Dictionary<int,int[]>();if(!history.StaticNext.TryGetValue(shot.immunityIdentity,out values)){values=new int[Main.maxNPCs];history.StaticNext.Add(shot.immunityIdentity,values);}values[slot]=shot.idStaticNPCHitCooldown<0?int.MaxValue:outer+shot.idStaticNPCHitCooldown;}
                return;
            }
            // These attached hand shots take native KillOldestJavelin's
            // branch instead of the ordinary immunity-write chain. Their
            // family model owns that cap; this hit preserves owner history.
            if(shot.type==598 || shot.type==636)return;
            int ownerCooldown=OwnerCooldown(shot.type);
            if(ownerCooldown!=0){WriteOwner(slot,outer+ownerCooldown);return;}
            if(shot.usesLocalNPCImmunity && shot.localNPCHitCooldown!=-2)
            {WriteOwner(slot,0);localNext[slot]=shot.localNPCHitCooldown<0?int.MaxValue:outer+shot.localNPCHitCooldown;}
            else if(penetration!=1 || shot.appliesImmunityTimeOnSingleHits)WriteOwner(slot,outer+10);
        }
        private void WriteOwner(int slot,int next)
        {
            // Zero is a real native assignment, distinct from an untouched
            // slot. Children share the latest write, not max(old,new).
            if(history.OwnerNext==null){history.OwnerNext=new int[Main.maxNPCs];history.OwnerWritten=new bool[Main.maxNPCs];}
            history.OwnerNext[slot]=next;history.OwnerWritten[slot]=true;
        }
        private static int OwnerCooldown(int type)
        {
            switch(type)
            {
                case 514:return 1;case 927:return 4;
                case 595:case 735:case 424:case 425:case 426:case 634:case 635:case 659:return 5;
                case 409:return 6;case 246:case 249:case 582:case 902:return 7;
                case 16:case 443:case 1110:return 8;default:return 0;
            }
        }
    }
}
