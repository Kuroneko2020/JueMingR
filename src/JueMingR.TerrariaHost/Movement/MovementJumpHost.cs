using System;
using System.Threading;
using JueMingR.Features.Movement;
using JueMingR.Platform.Combat;
using Terraria;

namespace JueMingR.TerrariaHost.Movement
{
    // Only MovementSession admits the real local Update. JumpMovement may also
    // be invoked for prediction clones or in isolation: neither gains a scope.
    internal sealed class MovementJumpHost
    {
        private static MovementJumpHost published;
        private Player player,intentPlayer;
        private long session,tick,version,lastInputVersion;
        private int thread;
        private bool active,intent,releaseOwned,autoOwned,rocketOwned,jumpEntered;
        private bool masked,candidate;
        private int beforeJump;
        private MovementJumpPhase phase;
        private MovementJumpOwnership ownership;

        internal void BeginUpdate(Player value,long generation,long updateTick,long mapping)
        {
            if(active)EndUpdate(player);
            if(value==null || Main.myPlayer<0 || Main.myPlayer>=Main.maxPlayers || !ReferenceEquals(value,Main.player[Main.myPlayer]))return;
            if(ReferenceEquals(intentPlayer,value) && session==generation && updateTick<=tick)return;
            if(!ReferenceEquals(intentPlayer,value) || session!=generation){Cancel();Publish(value,false);version++;}
            player=value;session=generation;tick=updateTick;
            thread=Thread.CurrentThread.ManagedThreadId;active=true;jumpEntered=false;masked=candidate=false;
            ownership.Reset();published=this;
        }
        private bool Current(Player value)
        {return active && ReferenceEquals(value,player) && Thread.CurrentThread.ManagedThreadId==thread && Main.myPlayer>=0 && Main.myPlayer<Main.maxPlayers && ReferenceEquals(value,Main.player[Main.myPlayer]);}
        internal void BeforeJump(Player value,bool enabled,bool permitted,bool heldJump,bool heldDown,long inputVersion)
        {
            if(!Current(value))return;
            bool holding=enabled && permitted && heldJump && !heldDown && value.controlJump && !value.controlDown && ownership.Owner!=MovementJumpOwner.FallProtection && !ExcludedMount(value);
            if(lastInputVersion!=inputVersion){lastInputVersion=inputVersion;version++;}
            Publish(value,holding);
            if(jumpEntered)return;
            jumpEntered=true;
            if(!holding){SetPhase(MovementJumpPhase.None);return;}
            if(!ownership.TryAcquire(MovementJumpOwner.SimulatedJump))return;
            bool water=value.wet && value.accFlipper && (!value.mount.Active || !value.mount.Cart) || value.mount.Active && value.mount.IsConsideredASlimeMount && value.wetSlime>0;
            if(TakenOver(value))
            {
                // A real grapple owns subsequent Jump semantics. Never lend a
                // synthetic release edge (or rocket release) that would retract
                // it; the player's actual controlJump still reaches vanilla.
                SetPhase(MovementJumpPhase.None);masked=candidate=false;return;
            }
            if(water)SetPhase(MovementJumpPhase.None);
            else SetPhase(MovementJumpRules.Before(phase,holding,value.controlDown,value.velocity.Y*value.gravDir));
            masked=phase==MovementJumpPhase.Coast;
            if(masked)ownership.MarkWritten(MovementJumpOwner.SimulatedJump);
            beforeJump=value.jump;
            bool opportunity=value.sliding || value.velocity.Y==0 || ExtraJump(value) || value.wet && value.accFlipper && (!value.mount.Active || !value.mount.Cart) || value.mount.Active && value.mount.IsConsideredASlimeMount && value.wetSlime>0;
            bool release=!masked && MovementJumpRules.Release(holding,value.controlDown,value.jump,opportunity);
            candidate=release && !water && !TakenOver(value);
            if(!release || value.releaseJump)return;
            ownership.MarkWritten(MovementJumpOwner.SimulatedJump);
            value.releaseJump=true;releaseOwned=true;
            // JumpMovement changes velocity and consumes releaseJump before
            // Update's wing/rocket refill. autoJump && justJumped carries ONLY
            // this legal ground jump across that resource-refresh consumer.
            // Sliding explicitly clears autoJump inside the original method.
            if(value.velocity.Y==0 && !value.sliding && !value.autoJump){value.autoJump=true;autoOwned=true;}
        }
        internal void AfterJump(Player value,bool permitted,bool heldJump,bool heldDown)
        {
            if(!Current(value) || !jumpEntered)return;
            // A consumed false is native state and must never be rolled back.
            if(releaseOwned && !value.releaseJump)releaseOwned=false;
            bool holding=intent && permitted && heldJump && !heldDown && value.controlJump && !value.controlDown && !ExcludedMount(value);
            if(!holding){Publish(value,false);SetPhase(MovementJumpPhase.None);masked=false;return;}
            if(ownership.Owner==MovementJumpOwner.SimulatedJump && !TakenOver(value))
                SetPhase(MovementJumpRules.After(phase,candidate,beforeJump,value.jump,value.velocity.Y*value.gravDir));
            // The final held tick has already reset native velocity. Its same
            // Update carpet/wing/boot/hover consumers must now see release.
            masked=phase==MovementJumpPhase.Coast;
            if(masked && !ownership.Written)ownership.MarkWritten(MovementJumpOwner.SimulatedJump);
            if(masked)return;
            if(ownership.Owner!=MovementJumpOwner.SimulatedJump || TakenOver(value) || value.jump!=0 || ExtraJump(value) || value.rocketBoots==0 || value.rocketTime<=0 || value.rocketRelease)return;
            // canRocket is deliberately NOT sampled here: Update establishes
            // it after CarpetMovement. Its full later gate still owns wings,
            // cooldown, CanUseBootFlyingAbilities, fuel and actual movement.
            if(!ownership.Written)ownership.MarkWritten(MovementJumpOwner.SimulatedJump);
            value.rocketRelease=true;rocketOwned=true;
        }
        internal void EndUpdate(Player value)
        {
            if(!active || !ReferenceEquals(value,player) || Thread.CurrentThread.ManagedThreadId!=thread)return;
            // Clear only booleans changed false -> true by this scope. These
            // fields have no subsequent native true-writer while Jump is held
            // in locked .8; original false writes and consumed motion survive.
            if(releaseOwned && value.releaseJump)value.releaseJump=false;
            if(autoOwned && value.autoJump)value.autoJump=false;
            if(rocketOwned && value.rocketRelease)value.rocketRelease=false;
            releaseOwned=autoOwned=rocketOwned=false;masked=candidate=false;active=false;player=null;ownership.Reset();
        }
        internal bool Cancel()
        {
            bool changed=intent || phase!=MovementJumpPhase.None;
            // Disabling this feature withdraws its own request, not the shared
            // current-update scope or a future protection owner's reservation.
            if(active && ownership.Owner==MovementJumpOwner.SimulatedJump)
            {
                if(releaseOwned && player.releaseJump)player.releaseJump=false;
                if(autoOwned && player.autoJump)player.autoJump=false;
                if(rocketOwned && player.rocketRelease)player.rocketRelease=false;
                releaseOwned=autoOwned=rocketOwned=false;
            }
            Publish(intentPlayer,false);SetPhase(MovementJumpPhase.None);masked=candidate=false;return changed;
        }
        internal void ClearSession()
        {
            if(active)EndUpdate(player);
            Cancel();intentPlayer=null;player=null;version++;
            if(ReferenceEquals(published,this))published=null;
        }
        internal bool ReserveFallProtection(Player value)
        {
            if(!Current(value) || !ownership.TryAcquire(MovementJumpOwner.FallProtection))return false;
            Publish(value,false);SetPhase(MovementJumpPhase.None);masked=false;return true;
        }
        private void SetPhase(MovementJumpPhase value){if(phase!=value){phase=value;version++;}}
        private static bool TakenOver(Player p)
        {return p.pulley || p.tongued || p.CCed || p.carpetFrame>=0 || p.rocketDelay>0 || p.grappling!=null && p.grappling.Length>0 && p.grappling[0]>=0;}
        internal bool EffectiveJump(Player value,bool nativeJump)
        {return nativeJump && !(Current(value) && masked && intent && ownership.Owner==MovementJumpOwner.SimulatedJump && !value.dead && !value.ghost && !value.controlDown);}
        internal void AfterFlight(Player value)
        {
            // Hover wings can start while jump is still positive. Once the
            // native consumer has actually completed, its flight supersedes
            // our registered jump tail; this is observation, not a new action.
            if(!Current(value) || ownership.Owner!=MovementJumpOwner.SimulatedJump)return;
            SetPhase(MovementJumpPhase.None);masked=false;
        }
        private void Publish(Player value,bool holding)
        {if(!ReferenceEquals(intentPlayer,value) || intent!=holding){version++;intentPlayer=value;intent=holding;}}
        internal static bool ReadIntent(Player value,out long intentVersion)
        {MovementJumpPhase ignored;return ReadState(value,out intentVersion,out ignored);}
        internal static bool ReadState(Player value,out long intentVersion,out MovementJumpPhase jumpPhase)
        {
            var owner=published;intentVersion=0;jumpPhase=MovementJumpPhase.None;
            if(owner==null || value==null || !ReferenceEquals(owner.intentPlayer,value))return false;
            intentVersion=owner.version;
            bool holding=owner.intent && value.controlJump && !value.controlDown && !value.dead && !value.ghost;
            if(holding)jumpPhase=owner.phase;
            return holding;
        }
        internal static bool ExtraJump(Player p)
        {return p.canJumpAgain_Cloud || p.canJumpAgain_Sandstorm || p.canJumpAgain_Blizzard || p.canJumpAgain_Fart || p.canJumpAgain_Sail || p.canJumpAgain_Unicorn || p.canJumpAgain_Santank || p.canJumpAgain_WallOfFleshGoat || p.canJumpAgain_Basilisk;}
        internal static bool ExcludedMount(Player p)
        {
            if(!p.mount.Active)return false;
            // Locked .8 flightTimeMax > 0 OR usesHover, including conditional
            // fish/queen-slime flight and the selective-flight type 54. This
            // is a capability family, independent of today's wet/frame state.
            switch(p.mount.Type)
            {case 0:case 2:case 5:case 7:case 8:case 12:case 23:case 44:case 48:case 49:case 50:case 54:case 56:case 61:return true;default:return false;}
        }
    }
}
