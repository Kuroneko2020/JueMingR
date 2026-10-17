using System;
using System.Collections;
using System.Linq;
using JueMingR.Features.Combat;
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
            Require(registry.Find("combat.aim")!=null,"standard independent aim binding is registered");
            var host=Get(context,"CombatObservation");var status=Get(host,"LayerStatus");
            var state=Get(shell,"State");var renderer=Get(shell,"renderer");Call(state,"Navigate",8);Call(state,"RestoreVisible");Call(renderer,"RefreshResources");
            Action prepare=()=>Call(renderer,"Prepare",state,960f,640f,1f);
            Func<object[]> elements=()=>((IEnumerable)Get(Get(state,"Layout"),"Elements")).Cast<object>().ToArray();
            NativeCombatObservationChecks.Save(host,new ObservationOptions());prepare();var closed=elements();float closedHeight=(float)Get(Get(state,"Layout"),"ContentHeight");
            Require(closed.Any(e=>Get(e,"Command").ToString()=="ObservationAimOn") && !closed.Any(e=>Get(e,"Command").ToString()=="ObservationRadius"),"default OFF has standard row and no hidden card/hit targets");
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,false,true,true,true,50,true,true));prepare();
            Require(elements().Count(e=>Get(e,"Command").ToString()=="ObservationRadius")==1 && (float)Get(Get(state,"Layout"),"ContentHeight")>closedHeight,"committed ON expands one card and content height");
            Call(state,"ScrollTo",float.MaxValue);
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,true,true,50,true,false));prepare();
            Require(!elements().Any(e=>Get(e,"Command").ToString()=="ObservationRadius") && (float)Get(state,"Scroll")<=(float)Get(Get(state,"Layout"),"MaxScroll"),"fold removes hit targets and clamps scroll");
            var options=((ObservationSettings)Get(host,"Settings")).Value;
            Require(options.Path && options.Marker && options.ClearLine && options.MouseCenter && options.Dummy && options.Radius==50,"fold retains shared policies, marker and independent path");
            Require(elements().Any(e=>Get(e,"Command").ToString()=="ObservationPathOn") && elements().Any(e=>Get(e,"Command").ToString()=="ObservationCollisionOn"),"independent rows survive fold");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());Call(shell,"CloseAndSubmitPosition");
            Set(host,"LayerStatus",Enum.Parse(status.GetType(),"Unavailable"));
            Require(Call(host,"Unavailable",0)!=null && Call(host,"Unavailable",1)!=null && !(bool)Get(host,"Collision") && !(bool)Get(host,"Path"),"permanent presentation failure cannot masquerade as an effective display");
            Set(host,"LayerStatus",status);
            Console.WriteLine("PASS G11A eight shared hotkeys and actual combat page controls composed.");
        }
    }
}
