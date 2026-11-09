#if JMR_AIM_LIGHT
using Terraria;
using Terraria.GameInput;
using Trace=JueMingR.TerrariaHost.Combat.Prediction.AimLightTrace;
using Gate=JueMingR.TerrariaHost.Combat.Prediction.AimLightTrace.PrismGate;

namespace JueMingR.TerrariaHost.Combat
{
    // Diagnostic-only expansion of the original short-circuit expressions.
    // Called solely inside an actual tracked 633 consumer; no speculative
    // re-evaluation after the world step, and no alternative admission policy.
    internal sealed partial class HostCombat
    {
        internal long PrismSampledFrame=>sampledFrame;
        private bool PrismAdmitted(Player player,bool cursor)
        {
            return Trace.Gate(Gate.Available,Available) && Trace.Gate(Gate.CombatPlayer,ReferenceEquals(player,Player)) && Trace.Gate(Gate.CombatSession,Runtime.IsSessionActive) &&
                Trace.Gate(Gate.Fresh,Trace.Gate(Gate.SampleFrame,sampledFrame==Tools.Input.Frame) && Trace.Gate(Gate.InputStart,Tools.Input.CanStartActions)) && Trace.Gate(Gate.Interface,CanInterface?.Invoke()??false) &&
                Trace.Gate(Gate.Gameplay,Trace.Gate(Gate.NotPaused,!Main.gamePaused) && Trace.Gate(Gate.CanUpdate,Main.CanUpdateGameplay)) &&
                Trace.Gate(Gate.PlayerUsable,Trace.Gate(Gate.Alive,!player.dead) && Trace.Gate(Gate.NotGhost,!player.ghost) && Trace.Gate(Gate.NotCced,!player.CCed) && Trace.Gate(Gate.NotCursed,!player.cursed) && Trace.Gate(Gate.ItemsAllowed,!player.noItems)) &&
                Trace.Gate(Gate.InventoryUnlocked,!player.HasLockedInventory()) && Trace.Gate(Gate.NotFilm,!player.isControlledByFilm) &&
                Trace.Gate(Gate.WorldFree,Trace.Gate(Gate.ItemWorldIdle,!Tools.Items.World.Busy) && Trace.Gate(Gate.NoManualOperation,!Tools.Items.World.HasManualOperation)) &&
                Trace.Gate(Gate.NativeUiFree,Trace.Gate(Gate.Map,!Main.mapFullscreen) && Trace.Gate(Gate.NotBlocked,!Main.blockInput) && Trace.Gate(Gate.NotChat,!Main.drawingPlayerChat) && Trace.Gate(Gate.NotSign,!Main.editSign) && Trace.Gate(Gate.NotChest,!Main.editChest) && Trace.Gate(Gate.NotWriting,!PlayerInput.WritingText)) &&
                Trace.Gate(Gate.ContainersFree,Trace.Gate(Gate.ChestClosed,player.chest==-1) && Trace.Gate(Gate.NoNpcTalk,player.talkNPC<0) && Trace.Gate(Gate.NoSignEdit,player.sign<0) && Trace.Gate(Gate.NoShop,Main.npcShop==0)) &&
                Trace.Gate(Gate.CursorFree,cursor || Trace.Gate(Gate.CursorExists,Main.mouseItem!=null) && Trace.Gate(Gate.CursorEmpty,Main.mouseItem.IsAir));
        }
    }
    internal sealed partial class HostAttackControl
    {
        private bool PrismCurrent(Source source)
        {
            var player=combat.Player;
            return Trace.Gate(Gate.Source,source!=null) && Trace.Gate(Gate.Player,player!=null) && Trace.Gate(Gate.PlayerActive,player.active) &&
                Trace.Gate(Gate.PlayerAlive,Trace.Gate(Gate.Alive,!player.dead) && Trace.Gate(Gate.NotGhost,!player.ghost)) && Trace.Gate(Gate.SessionActive,combat.Runtime.IsSessionActive) &&
                Trace.Gate(Gate.SourcePlayer,ReferenceEquals(source.Player,player)) && Trace.Gate(Gate.Session,source.Session==observation.Session) &&
                Trace.Gate(Gate.Selection,source.Selection==combat.Tools.SelectionIntent) && Trace.Gate(Gate.Slot,source.Slot==player.selectedItem) &&
                Trace.Gate(Gate.WeaponReference,ReferenceEquals(player.HeldItem,source.Weapon)) && Trace.Gate(Gate.WeaponType,source.Weapon.type==source.Type) &&
                Trace.Gate(Gate.WeaponPrefix,source.Weapon.prefix==source.Prefix);
        }
        private bool PrismValid(Controlled entry)
        {return Trace.Gate(Gate.ShotActive,entry.Shot.active) && Trace.Gate(Gate.ShotOwner,entry.Shot.owner==combat.Player?.whoAmI) && Trace.Gate(Gate.ShotKey,(int)entry.Shot.key==entry.Key) && Trace.Gate(Gate.ShotType,entry.Shot.type==entry.Type) && Allowed(entry.Source);}
    }
    internal sealed partial class HostAttackAim
    {
        internal HostCombatObservation PrismObservation=>observation;
        internal HostCombat PrismCombat=>combat;
        private bool PrismReceiver()
        {
            if(!Trace.Gate(Gate.HasTarget,observation.Selection.HasTarget))return false;
            var target=observation.Selection.Target;
            // Raw live facts are distinct from policy evaluation: Identity
            // can reject before Receives is called. Its unseen gate stays so.
            var live=Main.npc!=null && target.Slot>=0 && target.Slot<Main.npc.Length?Main.npc[target.Slot]:null;
            Trace.PrismTargetFacts(target,live,observation.Options.Dummy);
            return Trace.Gate(Gate.TargetIdentity,CombatSelection.Valid(target,observation.Session)) && Trace.Gate(Gate.TargetReceives,CombatSelection.Receives(Main.npc[target.Slot],observation.Options.Dummy));
        }
        private bool PrismBasicPermission()
        {
            return Trace.Gate(Gate.SelectionAvailable,observation.SelectionAvailable) && Trace.Gate(Gate.SettingsReady,observation.Settings.CanRun) && Trace.Gate(Gate.AimWanted,observation.Options.Aim) &&
                Trace.Gate(Gate.BasicPlayer,combat.Player!=null) && combat.Admitted(combat.Player) &&
                Trace.Gate(Gate.Intent,combat.Left || combat.Player.controlUseItem || combat.Player.channel || combat.Use.Active || Control.Pending || ReleaseHandCurrent || HostPhasebladeAttack.Handles(combat.Player.HeldItem.shoot) && combat.Player.itemAnimation>0);
        }
        private bool PrismBasicMembers(Player player,Item item,bool consumer)
        {
            return Trace.Gate(Gate.ConsumerPlayer,player!=null) && Trace.Gate(Gate.ConsumerItem,item!=null) && Trace.Gate(Gate.CombatPlayer,ReferenceEquals(player,combat.Player)) &&
                Trace.Gate(Gate.ConsumerWeapon,ReferenceEquals(item,player.HeldItem)) && (consumer?BasicPermission:PreviewDemand) && CurrentReceiver && Trace.Gate(Gate.Eligible,Eligible(item));
        }
    }
}
#endif
