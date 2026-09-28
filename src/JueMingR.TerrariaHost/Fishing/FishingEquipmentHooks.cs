using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Terraria;
using Terraria.UI;

namespace JueMingR.TerrariaHost.Fishing
{
    internal static class FishingEquipmentHooks
    {
        private static FishingEquipment owner;
        private static readonly Harmony harmony=new Harmony("JueMingR.FishingEquipment");
        private sealed class SlotScope {internal bool Active;internal Item[] Array;internal int Slot;internal Item Item,Mouse;internal int Type,Stack,MouseType,MouseStack;}
        private sealed class FavoriteScope {internal Item Item;internal bool Value;}
        internal static void Install(FishingEquipment value)
        {
            owner=value;Type[] slot={typeof(Item[]),typeof(int),typeof(int)};
            foreach(string name in new[]{"LeftClick","RightClick","OverrideLeftClick","SwapEquip"})Patch(typeof(ItemSlot),name,slot,nameof(BeforeSlot),null,nameof(AfterSlot));
            foreach(string name in new[]{"SwapVanityEquip","PickupItemIntoMouse"})Patch(typeof(ItemSlot),name,new[]{typeof(Item[]),typeof(int),typeof(int),typeof(Player)},nameof(BeforeSlot),null,nameof(AfterSlot));
            Patch(typeof(ItemSlot),"ToggleFavorited",new[]{typeof(Item)},nameof(BeforeFavorite),null,nameof(Favorite));
            Patch(typeof(ItemSlot),"ToggleLoadoutShare",slot,nameof(BeforeShare),null,nameof(Share));
            Patch(typeof(Player),"TrySwitchingLoadout",new[]{typeof(int)},nameof(BeforeLoadout),null,nameof(AfterLoadout));
            Patch(typeof(Player),"DropItems",new[]{typeof(bool)},nameof(BeforeDrop),null,nameof(AfterDrop),true);
            Patch(typeof(WorldGen),"SaveAndQuit",new[]{typeof(Action)},nameof(BeforeQuit),early:true);
        }
        private static void Patch(Type type,string name,Type[] args,string before=null,string after=null,string final=null,bool early=false)
        {
            var method=type.GetMethod(name,BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic,null,args,null);
            if(method==null || method.GetMethodBody()==null)throw new MissingMethodException("fishing equipment ABI: "+name);
            HarmonyMethod prefix=Hook(before);if(prefix!=null && early)prefix.before=new[]{"JueMingR.Items","JueMingR.Tools","JueMingR.KeepFavorited"};
            harmony.Patch(method,prefix,Hook(after),null,Hook(final));
        }
        private static HarmonyMethod Hook(string name){return name==null?null:new HarmonyMethod(typeof(FishingEquipmentHooks),name);}
        private static void BeforeSlot(Item[] __0,int __2,out SlotScope __state)
        {
            __state=null;if(owner==null || !owner.Active || owner.OperationDepth!=0 || __0==null || __2<0 || __2>=__0.Length)return;
            Item item=__0[__2],mouse=Main.mouseItem;__state=new SlotScope{Active=true,Array=__0,Slot=__2,Item=item,Mouse=mouse,Type=item?.type??0,Stack=item?.stack??0,MouseType=mouse?.type??0,MouseStack=mouse?.stack??0};
        }
        private static Exception AfterSlot(SlotScope __state,Exception __exception)
        {
            var s=__state;if(s!=null && s.Active && (!ReferenceEquals(s.Array[s.Slot],s.Item) || !ReferenceEquals(Main.mouseItem,s.Mouse) || s.Type!=(s.Item?.type??0) || s.Stack!=(s.Item?.stack??0) || s.MouseType!=(s.Mouse?.type??0) || s.MouseStack!=(s.Mouse?.stack??0)))owner?.ManualSlots();
            return __exception;
        }
        private static void BeforeFavorite(Item __0,out FavoriteScope __state){__state=owner!=null && owner.Active && __0!=null?new FavoriteScope{Item=__0,Value=__0.favorited}:null;}
        private static void BeforeShare(Item[] __0,int __2,out FavoriteScope __state){__state=null;if(__0!=null && __2>=0 && __2<__0.Length)BeforeFavorite(__0[__2],out __state);}
        // Native sound/settings can throw after changing the flag. Actual new
        // intention still wins; these finalizers never suppress that exception.
        private static Exception Favorite(FavoriteScope __state,Exception __exception){if(__state!=null && __state.Item.favorited!=__state.Value)owner?.Favorite(__state.Item);return __exception;}
        private static Exception Share(Item[] __0,int __2,FavoriteScope __state,Exception __exception){if(__state!=null && __state.Item.favorited!=__state.Value)owner?.Shared(__0,__2);return __exception;}
        private static void BeforeLoadout(Player __instance,out int __state){__state=__instance.CurrentLoadoutIndex;}
        private static Exception AfterLoadout(Player __instance,int __state,Exception __exception){owner?.ManualLoadout(__instance,__state);return __exception;}
        private static void BeforeDrop(Player __instance){owner?.BeforeBoundary(__instance,false);}
        private static Exception AfterDrop(Player __instance,Exception __exception){owner?.AfterDrop(__instance);return __exception;}
        private static void BeforeQuit(){owner?.BeforeBoundary(Main.LocalPlayer,true);}
        internal static void Uninstall(){foreach(var method in harmony.GetPatchedMethods().ToArray())harmony.Unpatch(method,HarmonyPatchType.All,harmony.Id);owner=null;}
    }
}
