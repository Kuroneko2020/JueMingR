using System;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatTargetMarkerChecks
    {
        internal static void Run(object context)
        {
            var marker=typeof(ObservationOptions).GetProperty("Marker");
            Require(marker!=null,"Target marker is an actual independent preference.");
            Require(!(bool)marker.GetValue(new ObservationOptions()),"Marker defaults OFF.");
            var host=Get(context,"CombatObservation");var selection=Get(host,"Selection");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");
            foreach(var n in Main.npc)n.active=false;
            Main.LocalPlayer.position=new Vector2(640,640);
            var target=Main.npc[2];target.SetDefaults(371);target.whoAmI=2;target.active=true;target.dontTakeDamage=false;target.immortal=false;target.friendly=false;target.position=new Vector2(720,650);
            var options=(ObservationOptions)Activator.CreateInstance(typeof(ObservationOptions),new object[]{false,false,false,false,false,25,true});
            NativeCombatObservationChecks.Save(host,options);NativeCombatObservationChecks.Fresh(context,host);
            Console.WriteLine("MARKER GATE enabled="+Get(host,"Enabled")+" marker="+Get(host,"Marker")+" session="+Get(host,"Session")+" active="+Main.LocalPlayer.active+" dead="+Main.LocalPlayer.dead+" targetLife="+target.life+" count="+Get(Get(context,"nativeNpcs"),"Count")+" selected="+Get(selection,"HasTarget"));
            Require((bool)Get(selection,"HasTarget") && ((NpcIdentity)Get(selection,"Target")).Slot==2,"Marker-only uses actual shared selection.");
            Require(cache.Required==0 && GetOptional(cache,"result")==null,"Marker-only has zero future demand or fabricated cache result.");
            Require(GetOptional(source,"Native")==null,"Marker-only does not start the exact comparison helper.");
            Call(host,"Poll");Require((bool)Get(selection,"HasTarget"),"Poll keeps marker-only target alive.");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
            Require(!(bool)Get(selection,"HasTarget"),"Final consumer OFF retires selection.");
            Console.WriteLine("PASS TARGET-MARKER default/off, marker-only shared identity and zero prediction demand.");
        }
    }
}
