using System;
using JueMingR.Platform.Companions;
using JueMingR.Platform.Items;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        internal ItemOperationOwnership OwnerOperations {get;set;}
        internal bool Transfer(Guid id,bool fromOwner,CompanionInventoryArea area,int slot,bool wholeStack)
        {return Resource(id,a=>CompanionResources.Transfer(owner,a.Body,fromOwner,area,slot,wholeStack,OwnerOperations));}
        internal bool Equip(Guid id,bool fromOwner,int inventorySlot)
        {return Resource(id,a=>CompanionResources.Equip(owner,a.Body,fromOwner,inventorySlot,OwnerOperations));}
        internal bool Unequip(Guid id,int armorSlot)
        {return Unequip(id,CompanionInventoryArea.Armor,armorSlot);}
        internal bool Unequip(Guid id,CompanionInventoryArea area,int slot)
        {return Resource(id,a=>CompanionResources.Unequip(owner,a.Body,area,slot,OwnerOperations));}
        private bool Resource(Guid id,Func<Actor,CompanionResourceResult> command)
        {
            Actor a;if(!CanTransfer(id) || !actors.TryGetValue(id,out a) || !profiles.OwnsActivation(id,a.Generation))return Result(false,"队友尚未就绪，当前不能交接物品。");
            try
            {
                var result=command(a);Feedback=result.Message;if(!result.Changed)return false;
                a.Profile=profiles.CaptureAndSave(id,a.Body,a.Generation);
                if(Main.netMode==1)
                {
                    using(CompanionHooks.Enter(a,a.Aim))foreach(int slot in a.NetworkSlotIds)NetMessage.SendData(5,-1,-1,null,a.Body.whoAmI,slot);
                    for(int slot=0;slot<59;slot++)NetMessage.SendData(5,-1,-1,null,owner.whoAmI,slot);
                }
                return true;
            }
            catch(Exception e){return Result(false,e.Message);}
        }
    }
}
