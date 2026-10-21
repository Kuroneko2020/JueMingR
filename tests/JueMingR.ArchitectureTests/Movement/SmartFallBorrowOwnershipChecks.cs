using System;
using System.Collections.Generic;
using JueMingR.Platform.Items;

namespace JueMingR.ArchitectureTests
{
    internal static class SmartFallBorrowOwnershipChecks
    {
        internal static void Check(List<string> failures)
        {
            try
            {
                var owner=new ItemOperationOwnership();owner.SetSession(9);
                long token=owner.NewUseToken();
                Require(owner.TryBeginFallEquipment(9,1UL<<12,1U<<3,0,token),"real source/armor acquired");
                Require(owner.IsProtected(12) && owner.IsFallEquipmentSlot(false,3) && !owner.IsFallEquipmentSlot(false,4),"only intersecting endpoints protected");
                Require(!owner.TryBeginUse(9,12,owner.NewUseToken()) && !owner.TryBeginDiscard(9,12) && !owner.TryBeginStore(9,1UL<<12) && !owner.TryBeginSale(9),"use/discard/store/sale cannot consume parked original");
                long other=owner.NewUseToken();Require(owner.TryBeginUse(9,13,other),"unrelated source remains available");
                Require(!owner.TryMoveFallEquipment(9,1UL<<13,1U<<3,0,token) && owner.IsProtected(12),"rehome cannot steal a concurrent use source");
                Require(!owner.TryMoveFallEquipment(9,0,0,0,token),"a live equipment owner cannot become an empty invisible range");
                Require(owner.TryMoveFallEquipment(9,1UL<<14,1U<<3,0,token) && !owner.IsProtected(12) && owner.IsProtected(14),"exact owner moves return address before its first write");
                owner.EndUse(9,other);
                Require(owner.TryMoveFallEquipment(9,1UL<<12,1U<<3,0,token),"original endpoint may be reclaimed under the same exact operation");
                owner.EndFallEquipment(8,token);Require(owner.IsProtected(12),"old generation cannot unlock successor");
                owner.EndFallEquipment(9,token);Require(!owner.IsProtected(12) && !owner.IsFallEquipmentSlot(false,3),"exact owner retires its masks");
                owner.SetSession(10);Require(!owner.AnyProtected,"session replacement revokes only interpretation, no item writes");
                token=owner.NewUseToken();Require(owner.TryBeginFallEquipment(10,0,0,1U<<4,token) && owner.AnyProtected,"grapple-only endpoint remains visible to equipment coordination");
                Require(owner.OwnsFallEquipment(10,token,true,4) && !owner.OwnsFallEquipment(9,token,true,4) && !owner.OwnsFallEquipment(10,token+1,true,4) &&
                    !owner.OwnsFallEquipment(10,token,false,4) && !owner.OwnsFallEquipment(10,token,true,3),"native use requires exact current generation, operation and misc endpoint");
                owner.EndFallEquipment(10,token);
                Require(!owner.OwnsFallEquipment(10,token,true,4),"retired native equipment token cannot grant use");
                Require(!owner.TryBeginFallEquipment(10,1UL<<50,0,0,owner.NewUseToken()) && !owner.TryBeginFallEquipment(10,0,1U<<20,0,owner.NewUseToken()) && !owner.TryBeginFallEquipment(10,0,0,1U<<5,owner.NewUseToken()),"no bank, invalid inventory, armor or misc endpoints admitted");
            }
            catch(Exception error){failures.Add("Smart fall borrowing ownership: "+error.Message);}
        }
        private static void Require(bool condition,string reason){if(!condition)throw new InvalidOperationException(reason);}
    }
}
