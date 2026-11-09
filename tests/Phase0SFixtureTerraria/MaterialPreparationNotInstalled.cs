using System;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Input;
using JueMingR.TerrariaHost.Tools;
using Terraria;

namespace JueMingR.TerrariaHost.Items
{
    // This source-linked Items receiver never installs ToolHooks/preparation.
    // Keep only production's Current==null static behavior for the real item
    // guards. Native supply checks own all actual receipt/ItemSlot semantics.
    internal sealed class ItemMaterialPreparation
    {
        private static Exception Missing(){return new InvalidOperationException("Use the real native material preparation fixture.");}
        internal static ItemMaterialPreparation Current {get{return null;}}
        internal ItemMaterialPreparation(HostTools tools){throw Missing();}
        internal struct NativeReturnScope { }
        internal sealed class ManualSortState {internal ManualSortState(){throw Missing();}}
        internal static bool BlocksNativeCursorUse(Player player,Item item){return false;}
        internal static bool BlocksNativeCursorDrop(Player player,int slot){return false;}
        internal static NativeReturnScope BeforeNativeReturn(Player player,Item item,int slot){return default(NativeReturnScope);}
        internal static void AfterNativeReturn(NativeReturnScope scope,Exception error){ }
        internal bool NativeMutation {get{throw Missing();}}
        internal bool AllowsNativeSlot(Item[] inventory,int context,int slot){throw Missing();}
        internal void AfterManualMutation(Item beforeSlot,Item beforeCursor,Item afterSlot,Item afterCursor,bool mainInventory){throw Missing();}
        internal ManualSortState ManualSortItems(){throw Missing();}
        internal void AfterManualSort(ManualSortState state){throw Missing();}
    }
    internal static class UninstalledPreparationChecks
    {
        internal static void Run()
        {
            if(ItemMaterialPreparation.Current!=null || ItemMaterialPreparation.BlocksNativeCursorUse(null,null) ||
                ItemMaterialPreparation.BlocksNativeCursorDrop(null,58))throw new InvalidOperationException("Absent preparation owner cannot claim native cursor input.");
            var scope=ItemMaterialPreparation.BeforeNativeReturn(null,null,-1);
            ItemMaterialPreparation.AfterNativeReturn(scope,new InvalidOperationException("unowned callback"));
            bool rejected=false;
            try{new ItemMaterialPreparation(null);}catch(InvalidOperationException){rejected=true;}
            if(!rejected)throw new InvalidOperationException("Uninstalled preparation construction must fail.");
            var accidental=(ItemMaterialPreparation)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ItemMaterialPreparation));
            rejected=false;
            try{accidental.AllowsNativeSlot(null,0,0);}catch(InvalidOperationException){rejected=true;}
            if(!rejected)throw new InvalidOperationException("An accidental preparation instance cannot grant a native slot bypass.");
            var input=new HostInputState(()=>IntPtr.Zero,()=>IntPtr.Zero){BlueprintMapping=null};
            var layout=new F5Layout{BlueprintAttached=false};
            if(input.BlueprintMapping!=null || layout.BlueprintAttached)throw new InvalidOperationException("This fixture does not install Blueprint composition.");
            Console.WriteLine("PASS fixture uninstalled preparation: no-owner cursor/return exits; construction and instance misuse fail closed; Blueprint not attached.");
        }
    }
}
