using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.KeepFavorited;
using Terraria;
using Terraria.UI;

namespace JueMingR.TerrariaHost.KeepFavorited
{
    internal static partial class FavoriteHooks
    {
        private static HostKeepFavorited host;
        private static readonly Harmony harmony=new Harmony("JueMingR.KeepFavorited");
        internal static void Install(HostKeepFavorited value)
        {
            host=value;
            Type[] slot={typeof(Item[]),typeof(int),typeof(int)};
            Patch(typeof(ItemSlot),"ToggleFavorited",new[]{typeof(Item)},null,nameof(Favorite));
            Patch(typeof(ItemSlot),"LeftClick",slot,nameof(BeforeSlot),nameof(AfterLeft),nameof(SlotFinal));
            Patch(typeof(ItemSlot),"OverrideLeftClick",slot,nameof(BeforeOverride),nameof(AfterOverride),nameof(SlotFinal));
            Patch(typeof(ItemSlot),"RightClick",slot,nameof(BeforeRight),nameof(AfterSlot),nameof(SlotFinal));
            Patch(typeof(ItemSlot),"PickupItemIntoMouse",new[]{typeof(Item[]),typeof(int),typeof(int),typeof(Player)},nameof(BeforePickup),nameof(AfterPickup),nameof(SlotFinal));
            Patch(typeof(ItemSlot),"EquipSwap",new[]{typeof(Item),typeof(Item[]),typeof(int),typeof(bool).MakeByRefType()},nameof(BeforeEquip),nameof(AfterEquip),nameof(EquipFinal));
            Patch(typeof(ItemSlot),"SwapEquip",slot,nameof(BeforeAnySlot),nameof(AfterSlot),nameof(SlotFinal));
            Patch(typeof(ItemSlot),"SwapVanityEquip",new[]{typeof(Item[]),typeof(int),typeof(int),typeof(Player)},nameof(BeforeAnySlot),nameof(AfterSlot),nameof(SlotFinal));
            Patch(typeof(Item),"ChangeItemType",new[]{typeof(int)},nameof(BeforeChange),nameof(AfterChange));
            Patch(typeof(Player),"TrySwitchingLoadout",new[]{typeof(int)},nameof(BeforeLoadout),nameof(AfterLoadout),nameof(LoadoutFinal));
            Patch(typeof(Player),"DropItems",new[]{typeof(bool)},nameof(DropBoundary),nameof(AfterDrop));
            InstallTransfers();
        }
        private static void Patch(Type type,string name,Type[] args,string before=null,string after=null,string final=null)
        {
            var method=type.GetMethod(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static,null,args,null);
            if(method==null || method.GetMethodBody()==null)throw new MissingMethodException("favorite-abi:"+name);
            harmony.Patch(method,Hook(before),Hook(after),null,Hook(final));
        }
        private static HarmonyMethod Hook(string name){return name==null?null:new HarmonyMethod(typeof(FavoriteHooks).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic));}
        internal static void Uninstall(){foreach(var method in harmony.GetPatchedMethods().ToArray())harmony.Unpatch(method,HarmonyPatchType.All,harmony.Id);host=null;}
        private static bool Active {get{return host!=null && host.Active;}}
        private static void Favorite(Item __0){if(Active)host.Explicit(__0);}
        private static void BeforeChange(Item __instance,out int __state){__state=__instance.type;}
        private static void AfterChange(Item __instance,int __state){if(Active)host.Mutated(__instance,__state);}
        private static void DropBoundary(Player __instance,bool __0)
        {if(!__0 && ReferenceEquals(__instance,host?.Player))host.Clear();}
        private static void AfterDrop(Player __instance){if(ReferenceEquals(__instance,host?.Player))host.Observe();}
        private sealed class SlotScope
        {
            internal Item[] Array;internal int Slot,Context,Cursor;internal Item A,B,Trash;internal int AType,APrefix,AStack,BType,BPrefix,BStack;
            internal FavoriteClaim AClaim,BClaim;internal bool AFavorite,BFavorite,Finished;
        }
        private static SlotScope BeginSlot(Item[] inv,int context,int slot)
        {
            if(!Active || inv==null || slot<0 || slot>=inv.Length)return null;
            host.Begin();Item a=inv[slot],b=Main.mouseItem;
            return new SlotScope{Array=inv,Slot=slot,Context=context,Cursor=Main.cursorOverride,A=a,B=b,Trash=host.Player.trashItem,
                AType=a?.type??0,APrefix=a?.prefix??0,AStack=a?.stack??0,BType=b?.type??0,BPrefix=b?.prefix??0,BStack=b?.stack??0,
                AFavorite=a?.favorited??false,BFavorite=b?.favorited??false,
                AClaim=host.TransferClaim(a,context==0 || context==1 || context==2 || context==32),BClaim=host.TransferClaim(b,true)};
        }
        private static void BeforeSlot(Item[] __0,int __1,int __2,out SlotScope __state){__state=Main.mouseLeft && Main.mouseLeftRelease?BeginSlot(__0,__1,__2):null;}
        private static void BeforeOverride(Item[] __0,int __1,int __2,out SlotScope __state){__state=BeginSlot(__0,__1,__2);}
        private static void BeforeRight(Item[] __0,int __1,int __2,out SlotScope __state){__state=Main.mouseRight?BeginSlot(__0,__1,__2):null;}
        private static void BeforeAnySlot(Item[] __0,int __1,int __2,out SlotScope __state){__state=BeginSlot(__0,__1,__2);}
        private static void BeforePickup(Item[] __0,int __1,int __2,out SlotScope __state){__state=BeginSlot(__0,__1,__2);}
        private static void AfterLeft(SlotScope __state)
        {
            if(__state==null)return;
            var s=__state;
            if(Active && s.Context!=29)
            {
                // Both original participants are known. Increased quantity at
                // one requires decreased quantity at the other: no aggregate diff.
                if(s.A!=null && s.B!=null && s.AType==s.BType && s.APrefix==s.BPrefix && s.AType>0)
                {
                    if(s.A.stack>s.AStack && s.B.stack<s.BStack)host.Link(s.BClaim,s.A);
                    if(s.B.stack>s.BStack && s.A.stack<s.AStack)host.Link(s.AClaim,s.B);
                    host.CorrectParticipant(s.A,s.AFavorite);host.CorrectParticipant(s.B,s.BFavorite);
                }
                // A one-piece clone placed from a larger mouse stack is a new
                // split, not a favorite transfer. The remaining mouse member
                // retains its intent; a final singleton moves by reference.
            }
            Finish(s);
        }
        private static void AfterOverride(SlotScope __state)
        {
            if(__state==null)return;
            var s=__state;Item trash=host?.Player?.trashItem;
            if(Active && s.Cursor==6 && s.A!=null && s.A.stack<=0 && !ReferenceEquals(trash,s.Trash) && trash!=null && trash.type==s.AType && trash.prefix==s.APrefix && trash.stack==s.AStack)host.Link(s.AClaim,trash);
            Finish(s);
        }
        private static void AfterPickup(SlotScope __state)
        {
            if(__state==null)return;
            var s=__state;Item mouse=Main.mouseItem;
            // Right-click can collect several pieces into the same mouse stack.
            // Partial extraction never lends it the source favorite. Only the
            // final whole source participates in the existing merge/transfer rule;
            // an already-favorited mouse stack keeps its own independent intent.
            if(Active && s.Context!=29 && s.AStack==1 && s.A!=null && s.A.stack==0 && mouse!=null && mouse.type==s.AType && mouse.prefix==s.APrefix && mouse.stack==s.BStack+1)host.Link(s.AClaim,mouse);
            Finish(s);
        }
        private static void AfterSlot(SlotScope __state){Finish(__state);}
        private static void Finish(SlotScope s){if(s==null || s.Finished)return;s.Finished=true;host?.End();}
        private static Exception SlotFinal(SlotScope __state,Exception __exception){if(__exception!=null)Finish(__state);return __exception;}
        private sealed class EquipScope{internal FavoriteClaim Source,Target;internal Item[] Array;internal int Slot;internal bool Finished;}
        private static void BeforeEquip(Item __0,Item[] __1,int __2,out EquipScope __state)
        {__state=null;if(!Active || __1==null || __2<0 || __2>=__1.Length)return;host.Begin();__state=new EquipScope{Source=host.Claim(__0),Target=host.Claim(__1[__2]),Array=__1,Slot=__2};}
        private static void AfterEquip(bool __3,Item __result,EquipScope __state)
        {if(__state==null)return;if(Active && __3){host.Link(__state.Source,__state.Array[__state.Slot]);host.Link(__state.Target,__result);}FinishEquip(__state);}
        private static void FinishEquip(EquipScope s){if(s==null || s.Finished)return;s.Finished=true;host?.End();}
        private static Exception EquipFinal(EquipScope __state,Exception __exception){if(__exception!=null)FinishEquip(__state);return __exception;}
        private sealed class LoadoutScope{internal Player Player;internal int OldIndex;internal Item[] Armor;internal bool Finished;}
        private static void BeforeLoadout(Player __instance,out LoadoutScope __state)
        {__state=null;if(!Active || !ReferenceEquals(__instance,host.Player))return;host.Begin();__state=new LoadoutScope{Player=__instance,OldIndex=__instance.CurrentLoadoutIndex,Armor=(Item[])__instance.armor.Clone()};}
        private static void AfterLoadout(LoadoutScope __state)
        {
            if(__state==null)return;
            var s=__state;
            if(Active && s.Player.CurrentLoadoutIndex!=s.OldIndex)
                for(int i=0;i<s.Armor.Length;i++)host.KeepInactive(s.Armor[i],s.Player.Loadouts[s.OldIndex].Armor,i);
            FinishLoadout(s);
        }
        private static void FinishLoadout(LoadoutScope s){if(s==null || s.Finished)return;s.Finished=true;host?.End();}
        private static Exception LoadoutFinal(LoadoutScope __state,Exception __exception){if(__exception!=null)FinishLoadout(__state);return __exception;}
    }
}
