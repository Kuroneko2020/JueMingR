using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;

namespace JueMingR.TerrariaHost.Combat
{
    // A known finite family is one captured causal set, not all same-owner
    // projectiles. Full revalidation is confined to Prepare/Consume; Draw
    // continues to use its same-update presentation lease without scanning.
    internal sealed class HostProjectileFamilyLease
    {
        private sealed class Member
        {
            internal Projectile Body;internal ProjectileKey Key;internal Vector2 Position,Velocity;
            internal int Slot,Type,Owner,Width,Height,Damage,Penetration,Maximum,Left,Extra,Alpha,ImmunityIdentity,LocalCooldown,StaticCooldown;
            internal bool Active,Friendly,Hostile,Tile,IgnoreWater,Wet,Honey,Lava,Shimmer,LocalImmunity,StaticImmunity,SingleImmunity,CorrectSlope;
            internal float[] Ai,Local;
            internal Vector2[] DamageTrail;
            private readonly int[] retargetLocal;
            private readonly bool arc,paper,ornament,ammoKernel,arrow,meleeChild;
            private readonly Vector2 oldPosition;
            private readonly float rotation,scale,ownerDistance;
            private readonly int direction,wetCount,aiStyle;
            private readonly int frame,frameCounter;
            private readonly bool ownerCheck,ownerMelee,stopDamage,manualDirection;
            private readonly bool retiredPrefix;
            internal Member(Projectile p,Projectile actual=null)
            {Body=actual??p;Key=Body.key;retiredPrefix=actual!=null && !p.active;Slot=p.whoAmI;Position=p.position;Velocity=p.velocity;Type=p.type;Owner=p.owner;Width=p.width;Height=p.height;Damage=p.damage;Penetration=p.penetrate;Maximum=p.maxPenetrate;Left=p.timeLeft;Extra=p.extraUpdates;Alpha=p.alpha;Active=p.active;Friendly=p.friendly;Hostile=p.hostile;Tile=p.tileCollide;IgnoreWater=p.ignoreWater;Wet=p.wet;Honey=p.honeyWet;Lava=p.lavaWet;Shimmer=p.shimmerWet;ImmunityIdentity=p.immunityIdentity;LocalCooldown=p.localNPCHitCooldown;StaticCooldown=p.idStaticNPCHitCooldown;LocalImmunity=p.usesLocalNPCImmunity;StaticImmunity=p.usesIDStaticNPCImmunity;SingleImmunity=p.appliesImmunityTimeOnSingleHits;CorrectSlope=p.correctSlopeCollision;Ai=(float[])p.ai.Clone();Local=(float[])p.localAI.Clone();
                // Tile retargeting reads these counters even if the body does
                // not use local immunity for Damage. Preserve ambient members
                // too; their target can change a later shared receive. This is
                // a bounded private-array comparison, never an NPC/LOS query.
                ammoKernel=HostHeldAmmoFamilyAttack.Family(p.type);
                // Shared ammo replay receives through a private body copy.
                // Its receive lease cannot observe edits to the original
                // member's local counters. Preserve those real counters here;
                // a held lower-slot projection supplies the exactly once per
                // native world decrement before the parent consumes input.
                if(p.type==225 || p.type==285 || ammoKernel && p.usesLocalNPCImmunity)retargetLocal=(int[])p.localNPCImmunity.Clone();
                // AI001/711 receives against its trail; 640 also reads oldPos
                // to retire a stopped trail. Both affect damage/lifetime and
                // subsequent pool slots, rather than presentation alone.
                if((p.type==711 || p.type==640 || p.type==617) && p.oldPos!=null)DamageTrail=(Vector2[])p.oldPos.Clone();
                // AI29/521 can die before Update assigns oldPosition. Its
                // Kill children use that saved origin, even at unchanged X/Y.
                if(p.type==521)oldPosition=p.oldPosition;
                arc=p.type==342 || p.type==974 || p.type==997 || p.type==699 || p.type==708 || p.type==367 || p.type==595 || p.type==735 || p.type==697 || p.type==707 || p.type==972 || p.type==973 || p.type>=982 && p.type<=985 || p.type==1126 || p.type==1128 || p.type==1129;
                if(arc){rotation=p.rotation;scale=p.scale;direction=p.direction;wetCount=p.wetCount;ownerDistance=p.ownerHitCheckDistance;ownerCheck=p.ownerHitCheck;ownerMelee=p.usesOwnerMeleeHitCD;stopDamage=p.stopsDealingDamageAfterPenetrateHits;manualDirection=p.manualDirectionChange;}
                paper=HostPaperAirplaneAttack.Handles(p.type);
                if(paper){rotation=p.rotation;direction=p.direction;wetCount=p.wetCount;manualDirection=p.manualDirectionChange;}
                ornament=HostOrnamentAttack.Family(p.type);
                if(ornament){aiStyle=p.aiStyle;ownerCheck=p.ownerHitCheck;ownerMelee=p.usesOwnerMeleeHitCD;manualDirection=p.manualDirectionChange;}
                // Sonic's receiving line consumes scale even when its raw
                // 16px body is unchanged. Include ambient members too.
                if(p.type==615 || p.type==630 || p.type==705 || p.type==90 || p.type==756 || p.type==684 || p.type==250 || p.type==451 || p.type==617)scale=p.scale;
                // Crystal shrink, ammo steering and Onyx movement dispatch on aiStyle.
                // Alpha is already captured above. Onyx scale is only written
                // to a constant; changing it does not resize its raw body.
                if(p.type==90 || p.type==661 || HostAmmoSteeringAttack.Family(p.type) || ammoKernel)aiStyle=p.aiStyle;
                if(ammoKernel)arrow=p.arrow; // Native motor/charm admission is part of the sealed birth.
                meleeChild=HostMeleeHitChildAttack.Family(p.type);
                if(meleeChild){aiStyle=p.aiStyle;ownerCheck=p.ownerHitCheck;ownerDistance=p.ownerHitCheckDistance;ownerMelee=p.usesOwnerMeleeHitCD;manualDirection=p.manualDirectionChange;}
                // AI189 retires on its frame clock and receives along a line
                // scaled by this value; these are not cosmetic animation.
                if(p.type==1125 || p.type==974){frame=p.frame;frameCounter=p.frameCounter;scale=p.scale;}
                if(p.type==978){frame=p.frame;frameCounter=p.frameCounter;}
            }
            internal bool Current
            {
                get
                {
                    // A predicted retirement has no further motor/receive.
                    // Keep its original identity AND terminal motion: an edit
                    // before that earlier native update must not be hidden by
                    // retirement. Dead visual/dust scalars are not input.
                    if(retiredPrefix)return Slot>=0 && Slot<Main.projectile.Length && ReferenceEquals(Main.projectile[Slot],Body) && Body.whoAmI==Slot && Body.key.Equals(Key) && !Body.active && Body.type==Type && Body.owner==Owner && Body.position==Position && Body.velocity==Velocity;
                    var p=Body;if(p.whoAmI!=Slot || Slot<0 || Slot>=Main.projectile.Length || !ReferenceEquals(Main.projectile[Slot],p) || !p.key.Equals(Key) || p.position!=Position || p.velocity!=Velocity || p.type!=Type || p.owner!=Owner || p.width!=Width || p.height!=Height || p.damage!=Damage || p.penetrate!=Penetration || p.maxPenetrate!=Maximum || p.timeLeft!=Left || p.extraUpdates!=Extra || p.alpha!=Alpha || p.active!=Active || p.friendly!=Friendly || p.hostile!=Hostile || p.tileCollide!=Tile || p.ignoreWater!=IgnoreWater || p.wet!=Wet || p.honeyWet!=Honey || p.lavaWet!=Lava || p.shimmerWet!=Shimmer || p.immunityIdentity!=ImmunityIdentity || p.localNPCHitCooldown!=LocalCooldown || p.idStaticNPCHitCooldown!=StaticCooldown || p.usesLocalNPCImmunity!=LocalImmunity || p.usesIDStaticNPCImmunity!=StaticImmunity || p.appliesImmunityTimeOnSingleHits!=SingleImmunity || p.correctSlopeCollision!=CorrectSlope || p.ai.Length!=Ai.Length || p.localAI.Length!=Local.Length)return false;
                    for(int i=0;i<Ai.Length;i++)if(p.ai[i]!=Ai[i])return false;
                    // AI001/706 stores only its tracked sound SlotId in
                    // localAI[0]. Native audio creation/retirement changes it
                    // without changing motion, receive or birth eligibility.
                    // Keep localAI[1] and the separate local NPC counters exact.
                    for(int i=0;i<Local.Length;i++)if(!(Type==706 && i==0) && p.localAI[i]!=Local[i])return false;
                    if(retargetLocal!=null){if(p.localNPCImmunity.Length!=retargetLocal.Length)return false;for(int i=0;i<retargetLocal.Length;i++)if(p.localNPCImmunity[i]!=retargetLocal[i])return false;}
                    if(DamageTrail!=null){if(p.oldPos==null || p.oldPos.Length!=DamageTrail.Length)return false;for(int i=0;i<DamageTrail.Length;i++)if(p.oldPos[i]!=DamageTrail[i])return false;}
                    if(Type==521 && p.oldPosition!=oldPosition)return false;
                    // Arc geometry consumes these values directly. Scope the
                    // lease to that family; unrelated visual trails stay out.
                    if(arc && (p.rotation!=rotation || p.scale!=scale || p.direction!=direction || p.wetCount!=wetCount || p.ownerHitCheckDistance!=ownerDistance || p.ownerHitCheck!=ownerCheck || p.usesOwnerMeleeHitCD!=ownerMelee || p.stopsDealingDamageAfterPenetrateHits!=stopDamage || p.manualDirectionChange!=manualDirection))return false;
                    // AI159 rotation feeds its next motor; these are not just
                    // drawing fields. Unrelated families keep their own lease.
                    if(paper && (p.rotation!=rotation || p.direction!=direction || p.wetCount!=wetCount || p.manualDirectionChange!=manualDirection))return false;
                    // AI30 children share the captured receiving policy. A
                    // same-update policy/style edit retires that finite family.
                    if(ornament && (p.aiStyle!=aiStyle || p.ownerHitCheck!=ownerCheck || p.usesOwnerMeleeHitCD!=ownerMelee || p.manualDirectionChange!=manualDirection))return false;
                    if((Type==615 || Type==630 || Type==705 || Type==90 || Type==756 || Type==684 || Type==250 || Type==451 || Type==617) && p.scale!=scale)return false;
                    if((Type==90 || Type==661 || HostAmmoSteeringAttack.Family(Type) || ammoKernel) && p.aiStyle!=aiStyle)return false;
                    if(ammoKernel && p.arrow!=arrow)return false;
                    if(meleeChild && (p.aiStyle!=aiStyle || p.ownerHitCheck!=ownerCheck || p.ownerHitCheckDistance!=ownerDistance || p.usesOwnerMeleeHitCD!=ownerMelee || p.manualDirectionChange!=manualDirection))return false;
                    if((Type==1125 || Type==974) && (p.frame!=frame || p.frameCounter!=frameCounter || p.scale!=scale))return false;
                    if(Type==978 && (p.frame!=frame || p.frameCounter!=frameCounter))return false;
                    return true;
                }
            }
        }
        private readonly List<Member> members=new List<Member>();
        private readonly int firstSceneType,lastSceneType;
        private readonly int sceneOwner=-1;
        private readonly int[] sceneTypes;
        private readonly bool[] ownerActive,sceneSlots;
        internal HostProjectileFamilyLease(Projectile[] source,int firstSceneType=0,int lastSceneType=0)
            :this(source,firstSceneType,lastSceneType,-1,null){}
        internal HostProjectileFamilyLease(Projectile[] source,int owner,int[] types)
            :this(source,0,0,owner,types){}
        private HostProjectileFamilyLease(Projectile[] source,int firstSceneType,int lastSceneType,int owner,int[] types)
        {
            this.firstSceneType=firstSceneType;this.lastSceneType=lastSceneType;sceneOwner=owner;sceneTypes=types;
            if(firstSceneType>0 || types!=null){ownerActive=new bool[255];sceneSlots=new bool[Main.projectile.Length];}
            foreach(var p in source){members.Add(new Member(p));if(sceneSlots!=null){sceneSlots[p.whoAmI]=true;if(p.owner>=0 && p.owner<255)ownerActive[p.owner]=Main.player[p.owner].active;}}
        }
        private HostProjectileFamilyLease(HostProjectileFamilyLease original,Dictionary<Projectile,Projectile> advanced)
        {
            firstSceneType=original.firstSceneType;lastSceneType=original.lastSceneType;sceneOwner=original.sceneOwner;sceneTypes=original.sceneTypes;ownerActive=original.ownerActive;
            if(original.sceneSlots!=null)sceneSlots=(bool[])original.sceneSlots.Clone();
            foreach(var member in original.members)
            {Projectile expected;if(advanced.TryGetValue(member.Body,out expected)){members.Add(new Member(expected,member.Body));if(sceneSlots!=null)sceneSlots[member.Slot]=expected.active;}else members.Add(member);}
        }
        // Immutable validation snapshots only. Advancing the private motor is
        // owned by the finite family; validation never calls AI or rewrites
        // the live pool. Other families retain their original Current path.
        internal HostProjectileFamilyLease AfterHeldPrefix(Dictionary<Projectile,Projectile> advanced)
        {return new HostProjectileFamilyLease(this,advanced);}
        private bool SceneMember(Projectile p)
        {
            if(!p.active)return false;
            if(sceneTypes==null)return p.type>=firstSceneType && p.type<=lastSceneType;
            if(p.owner!=sceneOwner)return false;
            foreach(int type in sceneTypes)if(p.type==type)return true;return false;
        }
        // Only already captured members, with no pool qualification scan.
        // Same-update presentation uses this bounded scalar identity lease.
        internal bool MembersCurrent
        {
            get
            {
                foreach(var member in members)
                    if(!member.Current || sceneSlots!=null && member.Owner>=0 && member.Owner<255 && Main.player[member.Owner].active!=ownerActive[member.Owner])return false;
                return true;
            }
        }
        internal bool Current
        {
            get
            {
                if(!MembersCurrent)return false;
                // Competition uses the family's exact qualification set:
                // all owners for scene roles, or the captured owner for a
                // local attached-shot family. Birth and in-place
                // type conversion can add a contributor without changing any
                // captured member, so checking only members is insufficient.
                // This full qualification scan belongs to Prepare/Consume;
                // presentation continues to use the same-update lease.
                if(sceneSlots!=null)
                    foreach(var p in Main.projectile)
                        if(SceneMember(p))
                        {
                            if(p.whoAmI<0 || p.whoAmI>=sceneSlots.Length || !sceneSlots[p.whoAmI])return false;
                        }
                return true;
            }
        }
    }
}
