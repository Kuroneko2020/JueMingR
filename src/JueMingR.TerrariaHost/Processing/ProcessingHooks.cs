using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Terraria;
using Terraria.UI;

namespace JueMingR.TerrariaHost.Processing
{
    internal static class ProcessingHooks
    {
        private static HostProcessing host;
        private static Harmony harmony;
        internal static Action<Item[],int,int,Player> Open;
        internal delegate void ExtractCall(Player player,ref Player.ItemCheckContext context);
        internal static ExtractCall Extract;
        internal static void Install(HostProcessing owner)
        {
            host=owner;harmony=new Harmony("JueMingR.Processing");
            var method=typeof(ItemSlot).GetMethod("TryOpenContainer",BindingFlags.NonPublic|BindingFlags.Static,null,new[]{typeof(Item[]),typeof(int),typeof(int),typeof(Player)},null);
            if(method==null)throw new MissingMethodException("ItemSlot.TryOpenContainer");
            harmony.Patch(method,new HarmonyMethod(typeof(ProcessingHooks),nameof(OpenBefore)));
            // Stop at RightClick as well: the empty/stacking branch never calls
            // TryOpenContainer, and the consumed slot still owns this hold tail.
            harmony.Patch(typeof(ItemSlot).GetMethod("RightClick",new[]{typeof(Item[]),typeof(int),typeof(int)}),new HarmonyMethod(typeof(ProcessingHooks),nameof(OpenBefore)));
            Open=(Action<Item[],int,int,Player>)Delegate.CreateDelegate(typeof(Action<Item[],int,int,Player>),method);
            var extraction=typeof(Player).GetMethod("PlaceThing_ItemInExtractinator",BindingFlags.NonPublic|BindingFlags.Instance,null,new[]{typeof(Player.ItemCheckContext).MakeByRefType()},null);
            if(extraction==null)throw new MissingMethodException("Player.PlaceThing_ItemInExtractinator");
            Extract=(ExtractCall)Delegate.CreateDelegate(typeof(ExtractCall),extraction);
            PatchPlayer("PickItemSelectionOverride",new[]{typeof(int).MakeByRefType()},null,nameof(Pick));
            PatchPlayer("TrySyncingInput",Type.EmptyTypes,nameof(Sync));
            PatchPlayer("ItemCheck",Type.EmptyTypes,nameof(CheckBefore),nameof(CheckAfter),nameof(CheckFinal));
            PatchPlayer("PlaceThing",new[]{typeof(bool),typeof(Player.ItemCheckContext).MakeByRefType()},nameof(Place));
            PatchPlayer("DropItemFromExtractinator",new[]{typeof(int),typeof(int)},null,nameof(Product));
        }
        private static void PatchPlayer(string name,Type[] args,string prefix=null,string postfix=null,string finalizer=null)
        {var method=typeof(Player).GetMethod(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,args,null);if(method==null)throw new MissingMethodException("processing-abi:"+name);harmony.Patch(method,Hook(prefix),Hook(postfix),null,Hook(finalizer));}
        private static HarmonyMethod Hook(string name){return name==null?null:new HarmonyMethod(typeof(ProcessingHooks),name);}
        private static void Pick(Player __instance,ref int __0,ref bool __result){host?.Extraction.Pick(__instance,ref __0,ref __result);}
        private static void Sync(Player __instance){host?.Extraction.BeforeSync(__instance);}
        private static void CheckBefore(Player __instance,out Lease __state){__state=new Lease{Token=host?.Extraction.Operation??0,Entered=host!=null && host.Extraction.Begin(__instance)};}
        private static void CheckAfter(Player __instance,Lease __state){host?.Extraction.End(__instance,__state.Token,__state.Entered,null);}
        private static Exception CheckFinal(Player __instance,Lease __state,Exception __exception){if(__exception!=null)host?.Extraction.End(__instance,__state.Token,__state.Entered,__exception);return __exception;}
        private static bool Place(Player __instance,ref Player.ItemCheckContext __1){return host==null || host.Extraction.Place(__instance,ref __1);}
        private static void Product(Player __instance){host?.Extraction.Product(__instance);}
        private struct Lease{internal long Token;internal bool Entered;}
        private static bool OpenBefore(Item[] __0,int __2){return host==null || !host.Bags.SuppressNative(__0,__2);}
        internal static void Uninstall(){if(harmony!=null)foreach(var m in harmony.GetPatchedMethods().ToArray())harmony.Unpatch(m,HarmonyPatchType.All,harmony.Id);host=null;Open=null;}
    }
}
