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
            private readonly bool arc,paper;
            private readonly Vector2 oldPosition;
            private readonly float rotation,scale,ownerDistance;
            private readonly int direction,wetCount;
            private readonly bool ownerCheck,ownerMelee,stopDamage,manualDirection;
            internal Member(Projectile p)
            {Body=p;Key=p.key;Slot=p.whoAmI;Position=p.position;Velocity=p.velocity;Type=p.type;Owner=p.owner;Width=p.width;Height=p.height;Damage=p.damage;Penetration=p.penetrate;Maximum=p.maxPenetrate;Left=p.timeLeft;Extra=p.extraUpdates;Alpha=p.alpha;Active=p.active;Friendly=p.friendly;Hostile=p.hostile;Tile=p.tileCollide;IgnoreWater=p.ignoreWater;Wet=p.wet;Honey=p.honeyWet;Lava=p.lavaWet;Shimmer=p.shimmerWet;ImmunityIdentity=p.immunityIdentity;LocalCooldown=p.localNPCHitCooldown;StaticCooldown=p.idStaticNPCHitCooldown;LocalImmunity=p.usesLocalNPCImmunity;StaticImmunity=p.usesIDStaticNPCImmunity;SingleImmunity=p.appliesImmunityTimeOnSingleHits;CorrectSlope=p.correctSlopeCollision;Ai=(float[])p.ai.Clone();Local=(float[])p.localAI.Clone();
                // AI001/711 receives against its actual stored trail. Other
                // families keep their existing scalar lease and allocation cost.
                if(p.type==711 && p.oldPos!=null)DamageTrail=(Vector2[])p.oldPos.Clone();
                // AI29/521 can die before Update assigns oldPosition. Its
                // Kill children use that saved origin, even at unchanged X/Y.
                if(p.type==521)oldPosition=p.oldPosition;
                arc=p.type==972 || p.type==973 || p.type>=982 && p.type<=985 || p.type==1126 || p.type==1128 || p.type==1129;
                if(arc){rotation=p.rotation;scale=p.scale;direction=p.direction;wetCount=p.wetCount;ownerDistance=p.ownerHitCheckDistance;ownerCheck=p.ownerHitCheck;ownerMelee=p.usesOwnerMeleeHitCD;stopDamage=p.stopsDealingDamageAfterPenetrateHits;manualDirection=p.manualDirectionChange;}
                paper=HostPaperAirplaneAttack.Handles(p.type);
                if(paper){rotation=p.rotation;direction=p.direction;wetCount=p.wetCount;manualDirection=p.manualDirectionChange;}
            }
            internal bool Current
            {
                get
                {
                    var p=Body;if(p.whoAmI!=Slot || Slot<0 || Slot>=Main.projectile.Length || !ReferenceEquals(Main.projectile[Slot],p) || !p.key.Equals(Key) || p.position!=Position || p.velocity!=Velocity || p.type!=Type || p.owner!=Owner || p.width!=Width || p.height!=Height || p.damage!=Damage || p.penetrate!=Penetration || p.maxPenetrate!=Maximum || p.timeLeft!=Left || p.extraUpdates!=Extra || p.alpha!=Alpha || p.active!=Active || p.friendly!=Friendly || p.hostile!=Hostile || p.tileCollide!=Tile || p.ignoreWater!=IgnoreWater || p.wet!=Wet || p.honeyWet!=Honey || p.lavaWet!=Lava || p.shimmerWet!=Shimmer || p.immunityIdentity!=ImmunityIdentity || p.localNPCHitCooldown!=LocalCooldown || p.idStaticNPCHitCooldown!=StaticCooldown || p.usesLocalNPCImmunity!=LocalImmunity || p.usesIDStaticNPCImmunity!=StaticImmunity || p.appliesImmunityTimeOnSingleHits!=SingleImmunity || p.correctSlopeCollision!=CorrectSlope || p.ai.Length!=Ai.Length || p.localAI.Length!=Local.Length)return false;
                    for(int i=0;i<Ai.Length;i++)if(p.ai[i]!=Ai[i])return false;for(int i=0;i<Local.Length;i++)if(p.localAI[i]!=Local[i])return false;
                    if(DamageTrail!=null){if(p.oldPos==null || p.oldPos.Length!=DamageTrail.Length)return false;for(int i=0;i<DamageTrail.Length;i++)if(p.oldPos[i]!=DamageTrail[i])return false;}
                    if(Type==521 && p.oldPosition!=oldPosition)return false;
                    // Arc geometry consumes these values directly. Scope the
                    // lease to that family; unrelated visual trails stay out.
                    if(arc && (p.rotation!=rotation || p.scale!=scale || p.direction!=direction || p.wetCount!=wetCount || p.ownerHitCheckDistance!=ownerDistance || p.ownerHitCheck!=ownerCheck || p.usesOwnerMeleeHitCD!=ownerMelee || p.stopsDealingDamageAfterPenetrateHits!=stopDamage || p.manualDirectionChange!=manualDirection))return false;
                    // AI159 rotation feeds its next motor; these are not just
                    // drawing fields. Unrelated families keep their own lease.
                    if(paper && (p.rotation!=rotation || p.direction!=direction || p.wetCount!=wetCount || p.manualDirectionChange!=manualDirection))return false;
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
