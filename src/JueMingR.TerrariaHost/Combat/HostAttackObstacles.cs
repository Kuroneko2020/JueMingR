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
    internal sealed class HostAttackObstacles
    {
        private sealed class Receiver
        {
            internal NPC Npc;internal NpcIdentity Identity;internal Rectangle Box;internal Vector2 Position,Offset;internal HostAttackReceive Receive;
            internal int OwnerImmune,LocalImmune,Next;internal uint StaticExpiry;internal bool Unknown;
        }
        private readonly List<Receiver> receivers=new List<Receiver>();
        private readonly Player player;private readonly Projectile shot;private readonly NpcTrajectory timeline;private readonly HostAttackReceive selected;
        private readonly int delay;private int remaining;
        internal int Remaining {get{return remaining;}}
        internal HostAttackObstacles(Player player,Projectile shot,NpcTrajectory timeline,int delay,bool beforeNpc,bool nextWorld)
        {
            this.player=player;this.shot=shot;this.timeline=timeline;this.delay=delay;selected=HostAttackReceive.Capture(player,shot,timeline.Identity.Slot,beforeNpc,nextWorld);
            for(int slot=0;slot<Main.maxNPCs;slot++)
            {
                var npc=Main.npc[slot];if(slot==timeline.Identity.Slot || !Eligible(player,shot,npc))continue;
                receivers.Add(new Receiver{Npc=npc,Identity=CombatSelection.Identity(npc,timeline.Identity.Session),Box=CombatSelection.ReceiveBounds(npc),Position=npc.position,Offset=npc.netOffset,OwnerImmune=npc.immune[player.whoAmI],LocalImmune=shot.localNPCImmunity[slot],StaticExpiry=shot.usesIDStaticNPCImmunity?Projectile.perIDStaticNPCImmunity[shot.immunityIdentity,slot]:0,Receive=HostAttackReceive.Capture(player,shot,slot,beforeNpc,nextWorld),Unknown=NPCID.Sets.ZappingJellyfish[npc.type]});
            }
        }
        private static bool Eligible(Player player,Projectile shot,NPC npc)
        {
            return npc!=null && npc.active && !npc.dontTakeDamage && shot.friendly && (!npc.friendly || npc.type==22 && player.killGuide || npc.type==54 && player.killClothier || NPCID.Sets.ZappingJellyfish[npc.type]) && player.CanNPCBeHitByPlayerOrPlayerProjectile(npc,shot);
        }
        internal bool Unchanged
        {
            get
            {
                int at=0;
                // Recheck the qualification set too: an inactive slot or an
                // invulnerable/friendly body can become a new receiver between
                // preparation and consumption without changing a stored row.
                for(int slot=0;slot<Main.maxNPCs;slot++)
                {
                    if(slot==timeline.Identity.Slot || !Eligible(player,shot,Main.npc[slot]))continue;
                    if(at>=receivers.Count || receivers[at].Identity.Slot!=slot)return false;var r=receivers[at++];
                    if(!CombatSelection.Valid(r.Identity,timeline.Identity.Session) || r.Npc.position!=r.Position || r.Npc.netOffset!=r.Offset || CombatSelection.ReceiveBounds(r.Npc)!=r.Box || r.Npc.immune[player.whoAmI]!=r.OwnerImmune || shot.localNPCImmunity[r.Identity.Slot]!=r.LocalImmune || shot.usesIDStaticNPCImmunity && Projectile.perIDStaticNPCImmunity[shot.immunityIdentity,r.Identity.Slot]!=r.StaticExpiry)return false;
                }
                return at==receivers.Count;
            }
        }
        internal bool Pass(int k,int tick,float x,float y)
        {return Pass(k,tick,x,y,0);}
        internal bool Pass(int k,int tick,float x,float y,int maximumRemaining)
        {
            if(k==1){remaining=shot.penetrate;foreach(var r in receivers)r.Next=0;}
            if(maximumRemaining>0 && remaining>maximumRemaining)remaining=maximumRemaining;
            if(remaining==0)return false;
            int outer=delay+(k-1)/(shot.extraUpdates+1)+1;var body=new Rectangle((int)(x-shot.width*.5f),(int)(y-shot.height*.5f),shot.width,shot.height);
            var target=timeline[tick];var b=target.ProjectileReceiveBounds;bool hitsTarget=target.CanReceive && selected.Allows(outer) && body.Intersects(new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height));
            foreach(var r in receivers)
            {
                if(hitsTarget && r.Identity.Slot>timeline.Identity.Slot)return true;
                if(outer<r.Next || !r.Receive.Allows(outer) || !body.Intersects(r.Box))continue;
                if(r.Unknown)return false; // Native reflection/damage special cases are not ordinary consumption.
                if(remaining>0 && --remaining==0)return false;
                int cooldown=10;
                if(shot.usesLocalNPCImmunity && shot.localNPCHitCooldown!=-2)cooldown=shot.localNPCHitCooldown<0?int.MaxValue:shot.localNPCHitCooldown;
                if(shot.usesIDStaticNPCImmunity)cooldown=shot.idStaticNPCHitCooldown<0?int.MaxValue:shot.usesLocalNPCImmunity?Math.Min(cooldown,shot.idStaticNPCHitCooldown):shot.idStaticNPCHitCooldown;
                r.Next=cooldown==int.MaxValue?int.MaxValue:outer+cooldown;
            }
            return true;
        }
    }
}
