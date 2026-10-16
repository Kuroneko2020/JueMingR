using System.Collections.Generic;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // Native autonomous targeting can chase a body that the player's current
    // Damage policy rejects. Keep this dependency separate from receivers.
    // Capture and Current belong to preparation/consumption, never Draw.
    internal sealed class HostProjectileTargets
    {
        private sealed class Target
        {
            internal NPC Npc;internal NpcIdentity Identity;internal MotionRect Bounds;internal Vector2 Offset,Velocity;internal bool Wet;internal int OwnerImmune;internal bool Alive;
        }
        private readonly Projectile shot;private readonly long session;private readonly bool townBodies,trackWet,ignoreDontTakeDamage,attachments,trackOwner,trackLife,trackVelocity,positiveLife;
        private readonly List<Target> targets=new List<Target>();
        private HostProjectileTargets(Projectile shot,long session,bool townBodies,bool trackWet,bool ignoreDontTakeDamage=false,bool attachments=false,bool trackOwner=false,bool trackLife=false,bool trackVelocity=false,bool positiveLife=false)
        {
            this.shot=shot;this.session=session;this.townBodies=townBodies;this.trackWet=trackWet;this.ignoreDontTakeDamage=ignoreDontTakeDamage;this.attachments=attachments;this.trackOwner=trackOwner;this.trackLife=trackLife;this.trackVelocity=trackVelocity;this.positiveLife=positiveLife;
            for(int slot=0;slot<Main.maxNPCs;slot++)
            {
                var npc=Main.npc[slot];if(!Qualifies(npc))continue;
                targets.Add(new Target{Npc=npc,Identity=CombatSelection.Identity(npc,session),Bounds=new MotionRect(npc.position.X,npc.position.Y,npc.width,npc.height),Offset=npc.netOffset,Velocity=npc.velocity,Wet=npc.wet,Alive=positiveLife?npc.life>0:npc.life>=0,OwnerImmune=trackOwner && shot.owner>=0 && shot.owner<npc.immune.Length?npc.immune[shot.owner]:0});
            }
        }
        private bool Qualifies(NPC npc){return npc!=null && (attachments?npc.active && (ignoreDontTakeDamage || !npc.dontTakeDamage):townBodies?npc.active && npc.townNPC:npc.CanBeChasedBy(shot,ignoreDontTakeDamage));}
        internal static HostProjectileTargets Capture(Projectile shot,long session)
        {return new HostProjectileTargets(shot,session,false,false);}
        // Seedler's Kill query explicitly includes invulnerable chase bodies.
        // Preserve Capture's unique two-argument reflection/API contract; the
        // same explicit qualification flag is used again at consumption.
        internal static HostProjectileTargets CaptureChase(Projectile shot,long session,bool ignoreDontTakeDamage)
        {return new HostProjectileTargets(shot,session,false,false,ignoreDontTakeDamage);}
        internal static HostProjectileTargets CaptureWetChase(Projectile shot,long session)
        {return new HostProjectileTargets(shot,session,false,true);}
        // A hand-thrown snowball's AI kill query includes friendly town NPCs;
        // these are neither ordinary receivers nor chaseable hostile targets.
        internal static HostProjectileTargets CaptureTownBodies(Projectile shot,long session)
        {return new HostProjectileTargets(shot,session,true,false);}
        // AI113 keeps an existing attachment on any active damageable NPC,
        // including training dummies that CanBeChasedBy deliberately rejects.
        internal static HostProjectileTargets CaptureAttachments(Projectile shot,long session)
        {return new HostProjectileTargets(shot,session,false,false,attachments:true);}
        // An already locked AI122 arrow reads active and position even while
        // its victim cannot receive. Damage qualification stays independent.
        internal static HostProjectileTargets CaptureActiveBodies(Projectile shot,long session)
        {return new HostProjectileTargets(shot,session,false,false,ignoreDontTakeDamage:true,attachments:true);}
        internal static HostProjectileTargets CaptureMovingChase(Projectile shot,long session)
        {return new HostProjectileTargets(shot,session,false,false,trackVelocity:true);}
        internal Vector2 Velocity(int index,NpcTrajectory timeline,int tick){return Slot(index)==timeline.Identity.Slot?new Vector2(timeline[tick].Vx,timeline[tick].Vy):targets[index].Velocity;}
        internal static HostProjectileTargets CaptureNailBodies(Projectile shot,long session)
        {return new HostProjectileTargets(shot,session,false,false,ignoreDontTakeDamage:true,attachments:true,trackLife:true);}
        // AI039 retains an attached active body only while life is POSITIVE;
        // unlike Nail's life>=0 boundary, zero already triggers reacquisition.
        internal static HostProjectileTargets CapturePiranhaBodies(Projectile shot,long session)
        {return new HostProjectileTargets(shot,session,false,false,ignoreDontTakeDamage:true,attachments:true,trackLife:true,trackVelocity:true,positiveLife:true);}
        internal bool Alive(int index){return targets[index].Alive;}
        internal static HostProjectileTargets CaptureOwnerChase(Projectile shot,long session)
        {return new HostProjectileTargets(shot,session,false,false,trackOwner:true);}
        internal int OwnerImmune(int index){return targets[index].OwnerImmune;}
        internal int Count {get{return targets.Count;}}
        internal int Slot(int index){return targets[index].Identity.Slot;}
        internal int Type(int index){return targets[index].Identity.Type;}
        internal bool Wet(int index){return targets[index].Wet;}
        internal MotionRect Bounds(int index){return targets[index].Bounds;}
        internal MotionRect Bounds(int index,NpcTrajectory timeline,int tick)
        {return Slot(index)==timeline.Identity.Slot?timeline[tick].Bounds:Bounds(index);}
        internal bool Current
        {
            get
            {
                int at=0;
                for(int slot=0;slot<Main.maxNPCs;slot++)
                {
                    var npc=Main.npc[slot];if(!Qualifies(npc))continue;
                    if(at>=targets.Count)return false;var row=targets[at++];
                    if(row.Identity.Slot!=slot || !ReferenceEquals(row.Npc,npc) || !CombatSelection.Valid(row.Identity,session) || npc.position.X!=row.Bounds.X || npc.position.Y!=row.Bounds.Y || npc.width!=row.Bounds.Width || npc.height!=row.Bounds.Height || npc.netOffset!=row.Offset || trackVelocity && npc.velocity!=row.Velocity || trackLife && (positiveLife?npc.life>0:npc.life>=0)!=row.Alive || trackWet && npc.wet!=row.Wet || trackOwner && npc.immune[shot.owner]!=row.OwnerImmune)return false;
                }
                return at==targets.Count;
            }
        }
    }
}