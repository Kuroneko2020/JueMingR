using System;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackAim
    {
        private PlacementUse placementUse;
        internal sealed class PlacementUse
        {
            private readonly HostAttackAim owner;internal readonly PlacementUse Previous;
            internal readonly Player Player;internal readonly Item Item;internal readonly NpcIdentity Target;
            internal readonly long Session;internal readonly uint Tick;internal readonly int Slot,Type,Prefix;
            internal readonly Vector2 Point;internal bool Shot,Ended;
            internal PlacementUse(HostAttackAim owner,Player player,Item item)
            {
                this.owner=owner;Previous=owner.placementUse;Player=player;Item=item;Target=owner.observation.Selection.Target;
                Session=owner.observation.Session;Tick=Main.GameUpdateCount;Slot=player.selectedItem;Type=item.type;Prefix=item.prefix;
                Point=HostPlacementAttack.Input(Main.npc[Target.Slot]);
            }
            internal void End()
            {if(Ended)return;Ended=true;while(owner.placementUse!=null && owner.placementUse.Ended)owner.placementUse=owner.placementUse.Previous;}
        }
        internal PlacementUse EnterPlacementUse(Player player)
        {
            if(!ReferenceEquals(player,combat.Player) || !Permission || !CurrentReceiver || !HostPlacementAttack.Handles(player.HeldItem) ||
                !HostAttackWindow.Opening(combat,player,player.HeldItem) || !HostPlacementAttack.ReadsCursor(player,player.HeldItem))return null;
            var receipt=new PlacementUse(this,player,player.HeldItem);placementUse=receipt;return receipt;
        }
        // Native CanUse reads the DD2 landing point before native Shoot does.
        // Both borrow the SAME action point only inside the actual ItemCheck
        // receipt. Queries elsewhere and later autonomous AI cannot acquire it.
        internal CombatCursorScope BeginPlacement(Player player,Item item,bool shot)
        {
            var r=placementUse;
            if(r==null || r.Ended || r.Shot || !ReferenceEquals(player,r.Player) || !ReferenceEquals(item,r.Item) ||
                r.Tick!=Main.GameUpdateCount || r.Session!=observation.Session || player.selectedItem!=r.Slot || item.type!=r.Type || item.prefix!=r.Prefix ||
                !ReferenceEquals(player.HeldItem,item) || !Permission || !CurrentReceiver || !r.Target.Equals(observation.Selection.Target) || player.altFunctionUse==2)return null;
            if(shot)r.Shot=true;
            return CombatCursorScope.Begin(r.Point,HostPlacementAttack.ReverseGravity(item));
        }
    }
}
