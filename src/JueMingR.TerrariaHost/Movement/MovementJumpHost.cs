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
        private bool active,intent,jumpEntered;
        private bool masked,candidate,fallRelease,flightHandoff;
        private int beforeJump;
        private MovementJumpPhase phase;
        private MovementJumpOwnership ownership;

        internal void BeginUpdate(Player value,long generation,long updateTick,long mapping)
        {
            if(value==null || Main.myPlayer<0 || Main.myPlayer>=Main.maxPlayers || !ReferenceEquals(value,Main.player[Main.myPlayer]))return;
            if(active)EndUpdate(player);
            if(ReferenceEquals(intentPlayer,value) && session==generation && updateTick<=tick)return;
            if(!ReferenceEquals(intentPlayer,value) || session!=generation){Cancel();Publish(value,false);version++;}
            player=value;session=generation;tick=updateTick;
            thread=Thread.CurrentThread.ManagedThreadId;active=true;jumpEntered=false;masked=candidate=false;
            fallRelease=false;ownership.Reset();published=this;
        }
        private bool Current(Player value)
        {return active && ReferenceEquals(value,player) && Thread.CurrentThread.ManagedThreadId==thread && Main.myPlayer>=0 && Main.myPlayer<Main.maxPlayers && ReferenceEquals(value,Main.player[Main.myPlayer]);}
        internal void BeforeJump(Player value,bool enabled,bool permitted,bool heldJump,bool heldDown,long inputVersion)
        {
            if(!Current(value))return;
            bool holding=enabled && permitted && heldJump && !heldDown && value.controlJump && !value.controlDown && !value.dead && !value.ghost && ownership.Owner!=MovementJumpOwner.FallProtection && !ExcludedMount(value);
            if(lastInputVersion!=inputVersion){lastInputVersion=inputVersion;version++;}
            Publish(value,holding);
            if(jumpEntered)return;
            jumpEntered=true;
            if(!holding){flightHandoff=false;SetPhase(MovementJumpPhase.None);return;}
            if(!ownership.TryAcquire(MovementJumpOwner.SimulatedJump))return;
            bool water=value.wet && value.accFlipper && (!value.mount.Active || !value.mount.Cart) || value.mount.Active && value.mount.IsConsideredASlimeMount && value.wetSlime>0;
            if(TakenOver(value))
            {
                // A real grapple owns subsequent Jump semantics. Never lend a
                // synthetic release edge (or rocket release) that would retract
                // it; the player's actual controlJump still reaches vanilla.
                flightHandoff=false;SetPhase(MovementJumpPhase.None);masked=candidate=false;return;
            }
            if(water){flightHandoff=false;SetPhase(MovementJumpPhase.None);}
            else SetPhase(MovementJumpRules.Before(phase,holding,value.controlDown,value.velocity.Y*value.gravDir));
            beforeJump=value.jump;
            // JumpMovement precedes every wing consumer. An already armed
            // extra may start now unless our own Coast still protects its tail;
            // later flight eligibility must never hide that real first reader.
            bool armedExtra=phase!=MovementJumpPhase.Coast && value.jump<=0 && value.releaseJump && ExtraJump(value);
            bool activeFlight=ActiveFlightInput(value);
            bool protectedExtra=phase==MovementJumpPhase.Coast && value.releaseJump && ExtraJump(value);
            if(!water && !armedExtra && !protectedExtra && activeFlight)return;
            if(flightHandoff)
            {
                flightHandoff=false;
                // This is one observation of completed native flight, not a
                // stored fuel/velocity model. Only its next inactive boundary
                // claims the CURRENT upward tail; a new discrete jump wins.
                if(!water && !armedExtra && value.jump<=0 && value.velocity.Y*value.gravDir<0)
                    SetPhase(MovementJumpPhase.Coast);
            }
            // Choose one input for the whole native update. Coast delays our
            // next discrete edge; it must not suppress an already legal native
            // held-flight consumer. In particular the last held tick remains
            // pressed all the way through carpet, wings and both hover reads.
            masked=phase==MovementJumpPhase.Coast && (protectedExtra || !PassiveWingInput(value)) || !water && NeedsBootRelease(value);
            if(masked)ownership.MarkWritten(MovementJumpOwner.SimulatedJump);
            bool opportunity=value.sliding || value.velocity.Y==0 || ExtraJump(value) || value.wet && value.accFlipper && (!value.mount.Active || !value.mount.Cart) || value.mount.Active && value.mount.IsConsideredASlimeMount && value.wetSlime>0;
            bool release=!masked && MovementJumpRules.Release(holding,value.controlDown,value.jump,opportunity);
            // A synthetic release flag is not a legal input edge. If vanilla
            // still needs rearming, every consumer sees this release update;
            // only a later update may press. Already-armed/native auto-jump
            // opportunities pass immediately, without a forced wait.
            if(release && !value.releaseJump && !(value.autoJump && (value.velocity.Y==0 || value.sliding)))
            {masked=true;ownership.MarkWritten(MovementJumpOwner.SimulatedJump);release=false;}
            candidate=release && !water && !TakenOver(value);
        }
        internal void AfterJump(Player value,bool permitted,bool heldJump,bool heldDown)
        {
            if(!Current(value) || !jumpEntered)return;
            bool holding=intent && permitted && heldJump && !heldDown && value.controlJump && !value.controlDown && !value.dead && !value.ghost && !ExcludedMount(value);
            if(!holding){Publish(value,false);flightHandoff=false;SetPhase(MovementJumpPhase.None);masked=false;return;}
            if(ownership.Owner==MovementJumpOwner.SimulatedJump && !TakenOver(value))
            {
                SetPhase(MovementJumpRules.After(phase,candidate,beforeJump,value.jump,value.velocity.Y*value.gravDir));
                if(candidate && beforeJump<=0 && value.jump>0)flightHandoff=false;
            }
            // Do not change this update's effective input after JumpMovement.
            // Native false input alone rearms boots; their later consumer must
            // also see that complete release update before a subsequent press.
        }
        internal void EndUpdate(Player value)
        {
            if(!active || !ReferenceEquals(value,player) || Thread.CurrentThread.ManagedThreadId!=thread)return;
            // Observe actual native takeovers. No release/resource fields are
            // borrowed or restored; completed native effects survive cleanup.
            // Carpet/boots and late-only hover have no WingMovement callback.
            // Their existing end-of-update observation is sufficient: the
            // native frame records actual carpet/rocket consumption (including
            // a last rocketDelay 1->0), while late hover retains positive fuel.
            // A stale carpet frame with no remaining time is not eligibility.
            if(ownership.Owner==MovementJumpOwner.SimulatedJump && jumpEntered && intent && !masked && !TakenOver(value) && !value.dead && !value.ghost &&
                (value.carpetFrame>=0 && value.carpetTime>0 || value.rocketFrame || LateHoverInput(value)))
                ObserveFlight();
            masked=candidate=false;active=false;player=null;ownership.Reset();
        }
        internal bool Cancel()
        {
            bool changed=intent || phase!=MovementJumpPhase.None || flightHandoff;
            // Disabling this feature withdraws its own request, not the shared
            // current-update scope or a future protection owner's reservation.
            Publish(intentPlayer,false);flightHandoff=false;SetPhase(MovementJumpPhase.None);masked=candidate=false;return changed;
        }
        internal void ClearSession()
        {
            if(active)EndUpdate(player);
            Cancel();intentPlayer=null;player=null;version++;
            if(ReferenceEquals(published,this))published=null;
        }
        internal bool ReserveFallProtection(Player value) { return ReserveFallInput(value,false); }
        // Fall release shares the already verified native read boundaries.
        // It lasts through downstream flight, not merely JumpMovement's postfix.
        internal bool ReserveFallInput(Player value,bool release)
        {
            if(!Current(value) || !ownership.TryAcquire(MovementJumpOwner.FallProtection))return false;
            Publish(value,false);flightHandoff=false;SetPhase(MovementJumpPhase.None);masked=false;fallRelease=release;return true;
        }
        private void SetPhase(MovementJumpPhase value){if(phase!=value){phase=value;version++;}}
        private static bool TakenOver(Player p)
        {return p.pulley || p.tongued || p.CCed || p.grappling!=null && p.grappling.Length>0 && p.grappling[0]>=0;}
        private static bool BootInput(Player p)
        {return p.jump<=0 && p.velocity.Y!=0 && !p.sliding && p.rocketBoots!=0 && p.rocketTime>0 && (p.wingTime==0 || p.wingsLogic==0) && p.rocketDelay==0 && p.CanUseBootFlyingAbilities;}
        private static bool NeedsBootRelease(Player p)
        {return BootInput(p) && !ExtraJump(p) && !p.rocketRelease;}
        private static bool ActiveFlightInput(Player p)
        {
            // These are current consumers, not equipment presence or a past
            // takeover. Early hover intentionally lacks CanUseWingAbilities;
            // its 29/32 family differs from late hover's 31 family in .8.
            if(p.wingsLogic>0 && !p.isPerformingJump_DownDash && p.wingTime>0 &&
                (p.jump==0 && p.velocity.Y!=0 || p.TryingToHoverDown && EarlyHover(p.wingsLogic)))return true;
            if(LateHoverInput(p))return true;
            if(p.carpet && p.jump==0 && p.velocity.Y!=0 && !ExtraJump(p) && p.rocketTime==0 && p.wingTime==0 && !p.mount.Active && (p.canCarpet || p.carpetTime>0))return true;
            if(p.rocketDelay>0)return true;
            // canRocket is established after CarpetMovement. Include that exact
            // upcoming velocity gate, while leaving fuel/delay/use permission
            // and the actual native action untouched.
            return BootInput(p) && p.rocketRelease && (p.canRocket || p.velocity.Y!=0 &&
                (p.gravDir==1 && p.velocity.Y> -Player.jumpSpeed || p.gravDir==-1 && p.velocity.Y<Player.jumpSpeed));
        }
        private static bool EarlyHover(int wings)
        {return wings==22 || wings==28 || wings==30 || wings==32 || wings==29 || wings==33 || wings==35 || wings==37 || wings==45;}
        private static bool LateHoverInput(Player p)
        {return p.TryingToHoverDown && p.wingTime>0 && p.CanUseWingAbilities && (p.wingsLogic==22 || p.wingsLogic==28 || p.wingsLogic==30 || p.wingsLogic==31 || p.wingsLogic==33 || p.wingsLogic==35 || p.wingsLogic==37 || p.wingsLogic==45);}
        private static bool PassiveWingInput(Player p)
        {
            // The exhausted-wing third-gravity branch uses ABSOLUTE positive
            // Y, including reversed-gravity ascent. It may preserve Coast's
            // input but must not prevent a later legal extra release/press.
            return p.wingsLogic>0 && p.velocity.Y>0 && !p.isPerformingJump_DownDash;
        }
        internal bool EffectiveJump(Player value,bool nativeJump)
        {return nativeJump && !(Current(value) && (ownership.Owner==MovementJumpOwner.FallProtection && fallRelease || masked && intent && ownership.Owner==MovementJumpOwner.SimulatedJump && !value.dead && !value.ghost && !value.controlDown));}
        internal void AfterFlight(Player value)
        {
            // Hover wings can start while jump is still positive. Once the
            // native consumer has actually completed, its flight supersedes
            // our registered jump tail; this is observation, not a new action.
            if(!Current(value) || ownership.Owner!=MovementJumpOwner.SimulatedJump || !intent)return;
            ObserveFlight();
        }
        private void ObserveFlight(){flightHandoff=true;SetPhase(MovementJumpPhase.None);}
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
