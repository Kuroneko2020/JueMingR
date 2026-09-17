using System;
using JueMingR.Features.KeepFavorited;
using Terraria;
using Terraria.UI;

namespace JueMingR.TerrariaHost.KeepFavorited
{
    internal static partial class FavoriteHooks
    {
        private static BucketScope bucket;
        private static PutScope output;
        private static void InstallTransfers()
        {
            Type[] fill={typeof(Item),typeof(GetItemSettings),typeof(Item),typeof(int)};
            Patch(typeof(Player),"GetItem",new[]{typeof(Item),typeof(GetItemSettings)},nameof(GetBefore),nameof(GetAfter),nameof(GetFinal));
            Patch(typeof(Player),"GetItem_FillIntoOccupiedSlot",fill,nameof(FillBefore),nameof(FillAfter),nameof(FillFinal));
            Patch(typeof(Player),"GetItem_FillEmptyInventorySlot",fill,nameof(FillBefore),nameof(FillAfter),nameof(FillFinal));
            Patch(typeof(Player),"ItemCheck_UseBuckets",new[]{typeof(Item)},nameof(BucketBefore),nameof(BucketAfter),nameof(BucketFinal));
            Patch(typeof(Player),"PutItemInInventoryFromItemUsage",new[]{typeof(int)},nameof(PutBefore),nameof(PutAfter),nameof(PutFinal));
            Patch(typeof(Player),"dropItemCheck",Type.EmptyTypes,nameof(MouseBefore),nameof(MouseAfter),nameof(MouseFinal));
            Patch(typeof(Player),"ItemCheck",Type.EmptyTypes,nameof(MirrorBefore),nameof(MirrorAfter),nameof(MouseFinal));
            Patch(typeof(ChestUI),"TryPlacingInChest",new[]{typeof(Item[]),typeof(int),typeof(bool),typeof(int)},nameof(ChestBefore),nameof(AfterSlot),nameof(SlotFinal));
            Patch(typeof(Player),"DropSelectedItem",new[]{typeof(int),typeof(Item).MakeByRefType()},nameof(DropBefore),nameof(GetAfter),nameof(GetFinal));
            Patch(typeof(ItemSlot),"SwapEquip",new[]{typeof(Item).MakeByRefType(),typeof(int)},nameof(RefEquipBefore),nameof(GetAfter),nameof(GetFinal));
            Patch(typeof(ItemSlot),"Handle",new[]{typeof(Item).MakeByRefType(),typeof(int),typeof(bool)},nameof(HandleBefore),nameof(GetAfter),nameof(GetFinal));
            Patch(typeof(WorldGen),"SaveAndQuit",new[]{typeof(Action)},nameof(WorldBoundary));
        }
        private static void WorldBoundary(){host?.Clear();}
        private sealed class OperationScope{internal bool Finished;}
        private static OperationScope BeginOperation(){if(!Active)return null;host.Begin();return new OperationScope();}
        private static void FinishOperation(OperationScope s){if(s==null || s.Finished)return;s.Finished=true;host?.End();}
        private static void GetBefore(Player __instance,Item __0,out OperationScope __state)
        {
            __state=ReferenceEquals(__instance,host?.Player)?BeginOperation():null;
            if(__state!=null && output!=null && output.TargetSlot<0 && __0!=null && __0.type==output.Type && __0.stack==1)
                host.Link(output.Claim,__0);
        }
        private static void GetAfter(OperationScope __state){FinishOperation(__state);}
        private static Exception GetFinal(OperationScope __state,Exception __exception){if(__exception!=null)FinishOperation(__state);return __exception;}
        private static void DropBefore(Player __instance,out OperationScope __state){__state=ReferenceEquals(__instance,host?.Player)?BeginOperation():null;}
        private static void RefEquipBefore(out OperationScope __state){__state=BeginOperation();}
        // The ref wrapper commits the trash/misc field after its inner array
        // call; keep that exact transfer alive until the owning field is written.
        private static void HandleBefore(bool __2,out OperationScope __state)
        {__state=__2 && (Main.mouseLeft && Main.mouseLeftRelease || Main.mouseRight)?BeginOperation():null;}
        private sealed class FillScope
        {internal Item Source,Target;internal Player Player;internal int Slot,Count;internal FavoriteClaim Claim;internal bool Finished;}
        private static void FillBefore(Player __instance,Item __2,int __3,out FillScope __state)
        {
            __state=null;if(!Active || !ReferenceEquals(__instance,host.Player) || __3<0 || __3>=50)return;
            var claim=host.TransferClaim(__2,true);if(!claim.Valid)return;
            host.Begin();__state=new FillScope{Source=__2,Target=__instance.inventory[__3],Player=__instance,Slot=__3,Count=__instance.inventory[__3].stack,Claim=claim};
        }
        private static void FillAfter(FillScope __state){FinishFill(__state);}
        private static void FinishFill(FillScope s)
        {
            if(s==null || s.Finished)return;s.Finished=true;
            Item target=s.Player.inventory[s.Slot];
            if(Active && (ReferenceEquals(target,s.Source) || ReferenceEquals(target,s.Target) && target.stack>s.Count))host.Link(s.Claim,target);
            host.End();
        }
        private static Exception FillFinal(FillScope __state,Exception __exception){if(__exception!=null)FinishFill(__state);return __exception;}
        private sealed class BucketScope{internal BucketScope Previous;internal FavoriteClaim Claim;internal bool Finished;}
        private static void BucketBefore(Player __instance,Item __0,out BucketScope __state)
        {
            __state=null;if(!Active || !ReferenceEquals(__instance,host.Player))return;
            FavoriteClaim claim=host.Claim(__0);if(!claim.Valid)return;
            host.Begin();__state=new BucketScope{Previous=bucket,Claim=claim};bucket=__state;
        }
        private static void BucketAfter(BucketScope __state){FinishBucket(__state);}
        private static void FinishBucket(BucketScope s){if(s==null || s.Finished)return;s.Finished=true;if(ReferenceEquals(bucket,s))bucket=s.Previous;host?.End();}
        private static Exception BucketFinal(BucketScope __state,Exception __exception){if(__exception!=null)FinishBucket(__state);return __exception;}
        private sealed class PutScope{internal PutScope Previous;internal Player Player;internal FavoriteClaim Claim;internal int Type,TargetSlot,Count;internal Item Target;internal bool Finished;}
        private static void PutBefore(Player __instance,int __0,out PutScope __state)
        {
            __state=null;if(!Active || bucket==null || !bucket.Claim.Valid || !ReferenceEquals(__instance,host.Player))return;
            int slot=-1;
            for(int i=0;i<58;i++){Item item=__instance.inventory[i];if(item.stack>0 && item.type==__0 && item.stack<item.maxStack){slot=i;break;}}
            if(slot<0 && __instance.selectedItem>=0 && __instance.inventory[__instance.selectedItem].IsAir)slot=__instance.selectedItem;
            Item target=slot<0?null:__instance.inventory[slot];host.Begin();
            __state=new PutScope{Previous=output,Player=__instance,Claim=bucket.Claim,Type=__0,TargetSlot=slot,Target=target,Count=target?.stack??0};output=__state;
        }
        private static void PutAfter(PutScope __state){FinishPut(__state);}
        private static void FinishPut(PutScope s)
        {
            if(s==null || s.Finished)return;s.Finished=true;
            // Slot 58 is admitted only by this exact bucket output operation,
            // with a previously sourced mouse mirror. It is never a scan domain.
            if(Active && s.TargetSlot>=0 && (s.TargetSlot<50 || s.TargetSlot==58 && host.Find(s.Target)?.MirrorSource!=null))
            {
                Item target=s.Player.inventory[s.TargetSlot];
                if(ReferenceEquals(target,s.Target) && target.type==s.Type && target.stack==s.Count+1)host.Link(s.Claim,target);
            }
            if(ReferenceEquals(output,s))output=s.Previous;host?.End();
        }
        private static Exception PutFinal(PutScope __state,Exception __exception){if(__exception!=null)FinishPut(__state);return __exception;}
        private struct MirrorScope{internal bool Active;internal Item Source,OldTarget;internal int Type,Prefix,Count;internal FavoriteClaim Claim;}
        private static MirrorScope BeginMirror(Player player,bool toMouse)
        {
            if(!Active || !ReferenceEquals(player,host.Player))return default(MirrorScope);
            if(toMouse ? player.selectedItem!=58 || host.Find(player.inventory[58])?.MirrorSource==null : !HostKeepFavorited.Present(Main.mouseItem))return default(MirrorScope);
            host.Begin();Item source=toMouse?player.inventory[58]:Main.mouseItem;
            return new MirrorScope{Active=true,Source=source,OldTarget=toMouse?Main.mouseItem:player.inventory[58],Type=source?.type??0,Prefix=source?.prefix??0,Count=source?.stack??0,Claim=host.Claim(source)};
        }
        private static void MouseBefore(Player __instance,out MirrorScope __state){__state=BeginMirror(__instance,false);}
        private static void MirrorBefore(Player __instance,out MirrorScope __state){__state=BeginMirror(__instance,true);}
        private static void MouseAfter(Player __instance,MirrorScope __state){FinishMirror(__instance,__state,false);}
        private static void MirrorAfter(Player __instance,MirrorScope __state){FinishMirror(__instance,__state,true);}
        private static void FinishMirror(Player player,MirrorScope s,bool toMouse)
        {
            if(!s.Active)return;
            Item target=toMouse?Main.mouseItem:player.inventory[58];
            if(Active && !ReferenceEquals(target,s.OldTarget) && HostKeepFavorited.Present(target) && s.Source!=null && target.type==s.Source.type && target.prefix==s.Source.prefix && target.stack==s.Source.stack)
                host.Mirror(s.Claim,s.Source,target,toMouse);
            host.End();
        }
        private static Exception MouseFinal(MirrorScope __state,Exception __exception){if(__exception!=null && __state.Active)host?.End();return __exception;}
        private static void ChestBefore(Item[] __0,int __1,bool __2,int __3,out SlotScope __state){__state=__2?null:BeginSlot(__0,__3,__1);}
    }
}
