using System;
using System.IO;
using System.Linq;
using JueMingR.Features.Guidance;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;
namespace NativeWorldTextProbe
{
    internal static class NativeGuidanceCameraChecks
    {
        internal static void Run(object context,ProbeGraphics graphics,string output)
        {
            var host=Get(context,"Guidance");var world=Get(host,"World");var npcs=Get(context,"nativeNpcs");var clock=(System.Diagnostics.Stopwatch)Get(host,"clock");clock.Stop();
            try
            {
                NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return (bool)Get(host,"ControlsEnabled");});
                foreach(var n in Main.npc)n.active=false;Main.LocalPlayer.position=new Vector2(640,640);Main.LocalPlayer.accCritterGuide=true;Main.LocalPlayer.hideInfo[11]=false;Main.LocalPlayer.armor[3]=NativeGuidanceEquipmentChecks.Accessory(ItemID.Toolbelt);
                Main.npc[1]=NativeGuidanceChecks.Npc(1,NPCID.Tim,4,1600);Main.npc[1].GivenName="稀有相机检验";Main.npc[2]=NativeGuidanceChecks.Npc(2,NPCID.TravellingMerchant,0,1700);Main.npc[3]=NativeGuidanceChecks.Npc(3,4,0,200);Main.npc[3].boss=true;
                Set(host,"LayerStatus",Enum.Parse(Get(host,"LayerStatus").GetType(),"Ready"));foreach(GuidanceKind kind in Enum.GetValues(typeof(GuidanceKind)))Call(host,"SetEnabled",kind,true);
                NativeQuickItemChecks.BeginWorldStep();Call(npcs,"BeginCompleted",(long)Main.GameUpdateCount);Call(host,"Update",(ulong)Main.GameUpdateCount);
                var rare=(RareCreatureDirection)Get(host,"Rare");Require(rare.Visible,"Actual rare direction world fact is selected.");int discoveries=rare.Discoveries;for(int i=0;i<30;i++)Call(host,"Update",(ulong)Main.GameUpdateCount);Require(rare.Discoveries==discoveries,"Outer updates do not spend the rare15-world-action discovery clock.");
                var occupancy=Get(world,"Occupancy");Main.combatText[0].active=true;Main.combatText[0].text="实际占位";Main.combatText[0].position=Main.LocalPlayer.Top-new Vector2(20,26);Main.combatText[0].scale=1;
                foreach(int gravity in new[]{1,-1})foreach(float zoom in new[]{.8f,1.4f})
                {
                    Main.LocalPlayer.gravDir=gravity;Main.screenPosition=new Vector2(200,200);Main.GameViewMatrix.Zoom=Vector2.One;Call(world,"Prepare");int passes=(int)Get(occupancy,"PoolPasses"),measured=(int)Get(occupancy,"Measurements"),reads=(int)Get(npcs,"DirectionReads");
                    Main.screenPosition=new Vector2(470,330);Main.GameViewMatrix.Zoom=new Vector2(zoom);var actual=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);Console.WriteLine("GUIDANCE CAMERA gravity="+gravity+" zoom="+zoom+" rare="+Get(world,"rareVisible")+" outside="+Get(world,"rareOutside")+" merchant="+Get(world,"merchantVisible")+" equipment="+Get(world,"equipmentVisible"));Require(actual.Any(p=>p.A>0) && (bool)Get(world,"rareVisible") && (bool)Get(world,"rareOutside") && (bool)Get(world,"merchantVisible"),"Rare arrow/label and merchant actually draw under the final camera.");
                    Require((int)Get(occupancy,"PoolPasses")==passes && (int)Get(occupancy,"Measurements")==measured && (int)Get(npcs,"DirectionReads")==reads,"Real Draw projects captured occupancy and identity without scanning native pools or NPC discovery.");
                    Call(world,"Prepare");var oracle=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);Require(actual.SequenceEqual(oracle),"Prepare-before-camera-change matches a fresh final-camera rendering for rare/merchant/equipment.");graphics.Image(Path.Combine(output,"guidance-final-camera-"+gravity+"-"+zoom.ToString(System.Globalization.CultureInfo.InvariantCulture)+".png"),()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);
                }
                Console.WriteLine("PASS GUIDANCE final-camera real XNA rare-arrow/text merchant/equipment occupancy; gravity2 zoom2; repeated30 outer updates preserve rare discovery clock.");
            }
            finally{Main.combatText[0].active=false;Main.LocalPlayer.gravDir=1;Main.GameViewMatrix.Zoom=Vector2.One;foreach(GuidanceKind kind in Enum.GetValues(typeof(GuidanceKind)))Call(host,"SetEnabled",kind,false);clock.Start();}
        }
    }
}
