using System;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Guidance;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeGuidancePresentationChecks
    {
        private const BindingFlags Flags = NativeGuidanceChecks.Flags;
        private static bool failNext;
        private static void MeasureFailure() { if (failNext) { failNext = false; throw new InvalidOperationException("isolated glyph failure"); } }
        internal static void Run(object context, object host, object npcs)
        {
            object world=Get(host,"World"); var player=Main.LocalPlayer; var oldPosition=Main.screenPosition; var oldZoom=Main.GameViewMatrix.Zoom;
            var npc=Main.npc[1]; var position=npc.position; float gravity=player.gravDir;
            try
            {
                foreach(float zoom in new[]{.8f,1f,1.6f}) foreach(float g in new[]{1f,-1f})
                {
                    Main.GameViewMatrix.Zoom=new Vector2(zoom); Main.screenPosition=new Vector2(113,57); player.gravDir=g;
                    for(int i=0;i<24;i++)
                    {
                        double angle=(i*15+1.7)*Math.PI/180;
                        npc.position=player.Center+new Vector2((float)Math.Cos(angle),(float)Math.Sin(angle))*900-new Vector2(npc.width,npc.height)/2;
                        Call(context,"UpdateRuntime"); Call(world,"Prepare");
                        Require((bool)Get(world,"rareVisible"),"real prepared circle visible");
                        var point=Vector2.Transform((Vector2)Get(world,"rarePoint"),Main.GameViewMatrix.ZoomMatrix);
                        Vector2 center=(Vector2)world.GetType().GetMethod("Project",Flags).Invoke(null,new object[]{player.Center,Main.GameViewMatrix.ZoomMatrix});
                        Require(Math.Abs(Vector2.Distance(point,center)-46)<.001,"final transformed circle, camera offset/zoom/gravity");
                        float angleDraw=(float)Get(world,"rotation"); Vector2 direction=new Vector2((float)Math.Cos(angleDraw),(float)Math.Sin(angleDraw));
                        direction*=new Vector2(Main.GameViewMatrix.ZoomMatrix.M11,Main.GameViewMatrix.ZoomMatrix.M22); direction.Normalize();
                        Vector2 expected=point-center; expected.Normalize(); Require(Vector2.Dot(direction,expected)>.99999,"draw rotation aligns with final radius direction");
                    }
                }
            }
            finally { npc.position=position; Main.screenPosition=oldPosition; Main.GameViewMatrix.Zoom=oldZoom; player.gravDir=gravity; }
            Call(context,"UpdateRuntime"); Call(world,"Prepare");
            var oldUi = Main.UIScaleMatrix;
            try
            {
                typeof(Main).GetField("_uiScaleMatrix",Flags).SetValue(null,Matrix.Identity);
                Call(world,"Prepare");
                float first=(float)Get(Get(world,"MerchantText"),"Height");
                typeof(Main).GetField("_uiScaleMatrix",Flags).SetValue(null,Matrix.CreateScale(1.5f,1.5f,1));
                Call(world,"Prepare");
                float second=(float)Get(Get(world,"MerchantText"),"Height");
                Require(Math.Abs((second-12)/(first-12)-1.5f)<.001,"direction text follows the information window's UI scaling independently of Game zoom");
            }
            finally {typeof(Main).GetField("_uiScaleMatrix",Flags).SetValue(null,oldUi);Call(world,"Prepare");}
            var warning=(EquipmentWarning)Get(host,"Equipment"); int shows=warning.Notifications;
            PopupText.popupText=new PopupText[20]; for(int i=0;i<20;i++)PopupText.popupText[i]=new PopupText();
            Main.combatText=new CombatText[100]; for(int i=0;i<100;i++)Main.combatText[i]=new CombatText();
            Main.showItemText=true; FontAssets.CombatText[0]=FontAssets.MouseText; FontAssets.CombatText[1]=FontAssets.MouseText;
            object feedback=Activator.CreateInstance(host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Items.ItemDiscardFeedback"),Flags,null,new object[]{(Func<bool>)(()=>true)},null);
            for(int i=0;i<3;i++)Call(feedback,"Complete",player,"测试物品",2);
            int reforge=PopupText.NewText(new AdvancedPopupRequest {Text="重铸提示",DurationInFrames=60,Color=Color.White},player.Top-new Vector2(30,45));
            Require(reforge>=0,"real native popup accepts second local source");
            for(int i=0;i<4;i++) if(PopupText.popupText[i].active) PopupText.popupText[i].scale=1;
            Main.combatText[0]=new CombatText {active=true,text="123",position=player.Top-new Vector2(25,42),scale=1,lifeTime=45,alpha=1};
            var before=(PopupText[])PopupText.popupText.Clone(); var ttls=new int[20]; for(int i=0;i<20;i++)ttls[i]=before[i].lifeTime;
            object occupancy=Get(world,"Occupancy"); int passes=(int)Get(occupancy,"PoolPasses");
            for(int i=0;i<10;i++){Call(context,"UpdateRuntime");Call(world,"Prepare");}
            Require((int)Get(occupancy,"PoolPasses")==passes+10 && warning.Notifications==shows,"visible warning reads finite pools once per Prepare, unrelated messages never restart lifetime");
            for(int i=0;i<20;i++)Require(ReferenceEquals(before[i],PopupText.popupText[i])&&ttls[i]==PopupText.popupText[i].lifeTime,"equipment cannot mutate native pool reference or TTL");
            Require(Main.combatText[0].active&&Main.combatText[0].lifeTime==45,"combat source lifetime preserved");
            Call(host,"SetEnabled",GuidanceKind.Equipment,false); Call(context,"UpdateRuntime"); Call(world,"Prepare"); passes=(int)Get(occupancy,"PoolPasses");
            for(int i=0;i<20;i++){Call(context,"UpdateRuntime");Call(world,"Prepare");}
            Require((int)Get(occupancy,"PoolPasses")==passes,"closed equipment has zero pool reads");
            Call(host,"SetEnabled",GuidanceKind.Equipment,true);
            // Real metric failure must not poison a same-key cached layout.
            var patch=new Harmony("JueMingR.Guidance.GlyphRecovery");
            var measure=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.UiTextMetrics").GetMethod("Measure",Flags);
            try
            {
                Call(host,"SetEnabled",GuidanceKind.Rare,false); Call(host,"SetEnabled",GuidanceKind.Equipment,false);
                Call(host,"SetEnabled",GuidanceKind.Merchant,true); failNext=true;
                patch.Patch(measure,prefix:new HarmonyMethod(typeof(NativeGuidancePresentationChecks).GetMethod(nameof(MeasureFailure),Flags)));
                Call(context,"UpdateRuntime"); Call(world,"Prepare"); Require(((int)Get(world,"Failures")&2)!=0,"one measured failure is local");
                Call(host,"SetEnabled",GuidanceKind.Merchant,true); Call(world,"Prepare");
                Require((bool)Get(world,"merchantVisible") && (float)Get(Get(world,"MerchantText"),"Width")>0,"explicit retry with identical font/text repairs failed cached layout");
            }
            finally { patch.Unpatch(measure,HarmonyPatchType.All,patch.Id); failNext=false; }
            foreach(GuidanceKind kind in Enum.GetValues(typeof(GuidanceKind)))Call(host,"SetEnabled",kind,true);
            Console.WriteLine("PASS: real Host final projection at 144 camera/zoom/gravity poses; actual discard + native popup/combat pool independence, zero closed pool reads, same-key glyph recovery. CPU geometry is not visual acceptance.");
        }
    }
}
