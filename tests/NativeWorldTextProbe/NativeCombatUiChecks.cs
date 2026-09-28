using System;
using JueMingR.Platform.Hotkeys;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatUiChecks
    {
        internal static void Run(object context)
        {
            var shell=Get(context,"Shell");var registry=(HotkeyRegistry)Get(Get(shell,"hotkeys"),"Registry");
            foreach(string id in new[]{"auto-click","flail","quick-switch","revolver","magic-string","facing","report","goblin"})
                Require(registry.Find("combat."+id)!=null,"complete frozen registry needs combat."+id);
            Require(GetOptional(Get(shell,"renderer"),"CombatControls")!=null && GetOptional(shell,"CombatInterval")!=null,"actual combat UI and interval owner are attached");
            Console.WriteLine("PASS G11A eight shared hotkeys and actual combat page controls composed.");
        }
    }
}
