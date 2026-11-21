using System;
using System.Collections.Generic;
using HarmonyLib;
using Terraria;
using Terraria.GameContent.UI;

namespace JueMingR.TerrariaHost.Companions
{
    internal static partial class CompanionHooks
    {
        private static void InstallFurnitureHooks()
        {
            Patch(typeof(Player), "TileInteractionsUse", transpiler: nameof(FurnitureWiresCheck), args: new[] { typeof(int), typeof(int) });
            Patch(typeof(Player), "InteractWithCraftingStation", prefix: nameof(ShowFurnitureCrafting));
        }

        private static bool ShowFurnitureCrafting() => CurrentActor?.Furniture?.Executing != true;
        private static bool FurnitureWiresOpen() => ShowFurnitureCrafting() && WiresUI.Open;

        private static IEnumerable<CodeInstruction> FurnitureWiresCheck(IEnumerable<CodeInstruction> instructions)
        {
            var original = AccessTools.PropertyGetter(typeof(WiresUI), nameof(WiresUI.Open));
            var scoped = AccessTools.DeclaredMethod(typeof(CompanionHooks), nameof(FurnitureWiresOpen));
            int replaced = 0;
            foreach (var instruction in instructions)
            {
                // The wire wheel belongs to the human's interface. Preserve
                // its native gate everywhere except the verified actor use.
                if (instruction.Calls(original)) { instruction.operand = scoped; replaced++; }
                yield return instruction;
            }
            if (replaced != 1) throw new InvalidOperationException("Pinned native tile interaction wire-wheel gate changed.");
        }
    }
}
