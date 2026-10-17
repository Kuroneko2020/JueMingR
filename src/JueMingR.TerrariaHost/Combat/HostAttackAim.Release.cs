using System;
using JueMingR.Platform.Combat;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackAim
    {
        private ReleaseHand releaseHand;
        private sealed class ReleaseHand
        {
            internal Player Player;internal Item Item;internal int Slot,Type,Prefix;
            internal long Session,Frame;internal ulong Tick;internal NpcIdentity Target;
        }
        private bool ReleaseAdmission(Player player)
        {return player!=null && ReferenceEquals(player,combat.Player) && observation.SelectionAvailable && observation.Settings.CanRun && observation.Options.Aim && combat.Admitted(player) && CurrentReceiver;}
        // ShootsOnUseRelease suppresses vanilla Shoot while Use is held. Its
        // already started native hand remains a real source after release;
        // a bare mouse-up or an unrelated animation never creates this lease.
        private void StartedReleaseHand(Player player,Item item)
        {
            releaseHand=null;
            if(item==null || !ReferenceEquals(item,player?.HeldItem) || !ItemID.Sets.ShootsOnUseRelease[item.type] || player.itemAnimation<=0 ||
                !(combat.Left || player.controlUseItem) || !ReleaseAdmission(player))return;
            releaseHand=new ReleaseHand{Player=player,Item=item,Slot=player.selectedItem,Type=item.type,Prefix=item.prefix,Session=observation.Session,
                Target=observation.Selection.Target,Frame=combat.Tools.Input.Frame,Tick=Main.GameUpdateCount};
        }
        private bool ReleaseHandCurrent
        {
            get
            {
                var r=releaseHand;var player=combat.Player;
                if(r==null)return false;
                long frame=combat.Tools.Input.Frame;ulong tick=Main.GameUpdateCount;
                if(!ReferenceEquals(player,r.Player) || !ReleaseAdmission(player) || player.selectedItem!=r.Slot || !ReferenceEquals(player.HeldItem,r.Item) ||
                    r.Item.type!=r.Type || r.Item.prefix!=r.Prefix || r.Session!=observation.Session || !r.Target.Equals(observation.Selection.Target) ||
                    player.itemAnimation<=0 || player.selectedItemState.HasBufferedChange || tick<r.Tick || tick-r.Tick>1 || frame<r.Frame || frame-r.Frame>1)
                {releaseHand=null;return false;}
                return true;
            }
        }
        // Only an actual ItemCheck entry renews continuity. Draw, a preview
        // query, or a stale publication cannot bridge a skipped input epoch.
        private void ObserveReleaseHand()
        {if(ReleaseHandCurrent){releaseHand.Frame=combat.Tools.Input.Frame;releaseHand.Tick=Main.GameUpdateCount;}}
    }
}
