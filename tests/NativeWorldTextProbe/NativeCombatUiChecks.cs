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
            Require(GetOptional(Get(shell,"renderer"),"CombatObservationControls")!=null && GetOptional(shell,"CombatRadius")!=null,"actual shared card, display rows and radius owner are attached");
            foreach(string id in new[]{"combat.collision-display","combat.npc-path"})Require(registry.Find(id)!=null,"independent display binding registered: "+id);
            var host=Get(context,"CombatObservation");var status=Get(host,"LayerStatus");
            Set(host,"LayerStatus",Enum.Parse(status.GetType(),"Unavailable"));
            Require(Call(host,"Unavailable",0)!=null && Call(host,"Unavailable",1)!=null && !(bool)Get(host,"Collision") && !(bool)Get(host,"Path"),"permanent presentation failure cannot masquerade as an effective display");
            Set(host,"LayerStatus",status);
            Console.WriteLine("PASS G11A eight shared hotkeys and actual combat page controls composed.");
        }
    }
}
