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
            internal Member(Projectile p)
            {Body=p;Key=p.key;Slot=p.whoAmI;Position=p.position;Velocity=p.velocity;Type=p.type;Owner=p.owner;Width=p.width;Height=p.height;Damage=p.damage;Penetration=p.penetrate;Maximum=p.maxPenetrate;Left=p.timeLeft;Extra=p.extraUpdates;Alpha=p.alpha;Active=p.active;Friendly=p.friendly;Hostile=p.hostile;Tile=p.tileCollide;IgnoreWater=p.ignoreWater;Wet=p.wet;Honey=p.honeyWet;Lava=p.lavaWet;Shimmer=p.shimmerWet;ImmunityIdentity=p.immunityIdentity;LocalCooldown=p.localNPCHitCooldown;StaticCooldown=p.idStaticNPCHitCooldown;LocalImmunity=p.usesLocalNPCImmunity;StaticImmunity=p.usesIDStaticNPCImmunity;SingleImmunity=p.appliesImmunityTimeOnSingleHits;CorrectSlope=p.correctSlopeCollision;Ai=(float[])p.ai.Clone();Local=(float[])p.localAI.Clone();
                // AI001/711 receives against its actual stored trail. Other
                // families keep their existing scalar lease and allocation cost.
                if(p.type==711 && p.oldPos!=null)DamageTrail=(Vector2[])p.oldPos.Clone();
            }
            internal bool Current
            {
                get
                {
                    var p=Body;if(p.whoAmI!=Slot || Slot<0 || Slot>=Main.projectile.Length || !ReferenceEquals(Main.projectile[Slot],p) || !p.key.Equals(Key) || p.position!=Position || p.velocity!=Velocity || p.type!=Type || p.owner!=Owner || p.width!=Width || p.height!=Height || p.damage!=Damage || p.penetrate!=Penetration || p.maxPenetrate!=Maximum || p.timeLeft!=Left || p.extraUpdates!=Extra || p.alpha!=Alpha || p.active!=Active || p.friendly!=Friendly || p.hostile!=Hostile || p.tileCollide!=Tile || p.ignoreWater!=IgnoreWater || p.wet!=Wet || p.honeyWet!=Honey || p.lavaWet!=Lava || p.shimmerWet!=Shimmer || p.immunityIdentity!=ImmunityIdentity || p.localNPCHitCooldown!=LocalCooldown || p.idStaticNPCHitCooldown!=StaticCooldown || p.usesLocalNPCImmunity!=LocalImmunity || p.usesIDStaticNPCImmunity!=StaticImmunity || p.appliesImmunityTimeOnSingleHits!=SingleImmunity || p.correctSlopeCollision!=CorrectSlope || p.ai.Length!=Ai.Length || p.localAI.Length!=Local.Length)return false;
                    for(int i=0;i<Ai.Length;i++)if(p.ai[i]!=Ai[i])return false;for(int i=0;i<Local.Length;i++)if(p.localAI[i]!=Local[i])return false;
                    if(DamageTrail!=null){if(p.oldPos==null || p.oldPos.Length!=DamageTrail.Length)return false;for(int i=0;i<DamageTrail.Length;i++)if(p.oldPos[i]!=DamageTrail[i])return false;}
                    return true;
                }
            }
        }
        private readonly List<Member> members=new List<Member>();
        private readonly int firstSceneType,lastSceneType;
        private readonly bool[] ownerActive,sceneSlots;
        internal HostProjectileFamilyLease(Projectile[] source,int firstSceneType=0,int lastSceneType=0)
        {
            this.firstSceneType=firstSceneType;this.lastSceneType=lastSceneType;
            if(firstSceneType>0){ownerActive=new bool[255];sceneSlots=new bool[Main.projectile.Length];}
            foreach(var p in source){members.Add(new Member(p));if(firstSceneType>0){sceneSlots[p.whoAmI]=true;if(p.owner>=0 && p.owner<255)ownerActive[p.owner]=Main.player[p.owner].active;}}
        }
        internal bool Current
        {
            get
            {
                foreach(var member in members)
                    if(!member.Current || firstSceneType>0 && member.Owner>=0 && member.Owner<255 && Main.player[member.Owner].active!=ownerActive[member.Owner])return false;
                // Scene competition observes every owner. Birth and in-place
                // type conversion can add a contributor without changing any
                // captured member, so checking only members is insufficient.
                // This full qualification scan belongs to Prepare/Consume;
                // presentation continues to use the same-update lease.
                if(firstSceneType>0)
                    foreach(var p in Main.projectile)
                        if(p.active && p.type>=firstSceneType && p.type<=lastSceneType)
                        {
                            if(p.whoAmI<0 || p.whoAmI>=sceneSlots.Length || !sceneSlots[p.whoAmI])return false;
                        }
                return true;
            }
        }
    }
}
