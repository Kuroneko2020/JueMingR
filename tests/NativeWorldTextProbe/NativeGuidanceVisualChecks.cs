using System;
using System.IO;
using System.Reflection;
using JueMingR.Features.Guidance;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeGuidanceVisualChecks
    {
        private const BindingFlags Flags = NativeGuidanceChecks.Flags;
        internal static void Run(ProbeGraphics graphics, string output)
        {
            Directory.CreateDirectory(output); string root=Path.Combine(Terraria.Program.SavePath,"guidance-visual"); Directory.CreateDirectory(root);
            Main.gameMenu=Main.dedServ=Main.hideUI=Main.mapFullscreen=Main.inFancyUI=Main.onlyDrawFancyUI=Main.ingameOptionsWindow=false;
            Main.netMode=Main.myPlayer=0; Main.screenWidth=960;Main.screenHeight=640; Main.screenPosition=Vector2.Zero;
            typeof(Main).GetField("_uiScaleMatrix",Flags).SetValue(null,Matrix.Identity);
            typeof(Terraria.GameInput.PlayerInput).GetField("_originalScreenWidth",Flags).SetValue(null,960);
            typeof(Terraria.GameInput.PlayerInput).GetField("_originalScreenHeight",Flags).SetValue(null,640);
            Main.GameViewMatrix=new Terraria.Graphics.SpriteViewMatrix(graphics.GraphicsDevice); Main.GameViewMatrix.SetViewportOverride(new Viewport(0,0,960,640));
            Main.player[0]=new Player {active=true,accCritterGuide=true,position=new Vector2(470,320),gravDir=1}; Main.ActiveWorldFileData=new Terraria.IO.WorldFileData(Path.Combine(root,"isolated.wld"),false);
            Main.npc=new NPC[Main.maxNPCs]; Main.npc[1]=NativeGuidanceChecks.Npc(1,45,4,1300); Main.npc[1].GivenName="稀有目标长名字：金色生物与待救角色";
            Main.npc[2]=NativeGuidanceChecks.Npc(2,368,0,2500); Main.npc[3]=NativeGuidanceChecks.Npc(3,4,0,150); Main.npc[3].boss=true;
            Main.player[0].armor[3]=NativeGuidanceEquipmentChecks.Accessory(Terraria.ID.ItemID.Toolbelt);
            Main.maxTilesX=8400;Main.maxTilesY=2400;Main.worldSurface=400;Main.dayTime=true; Main.eclipse=false;Main.invasionType=0;
            Main.dedServ=true;try{System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Terraria.Graphics.Capture.CaptureManager).TypeHandle);}finally{Main.dedServ=false;}
            var assembly=Assembly.LoadFrom(Path.Combine(Program.Repository,"artifacts/build/Debug/work/bin/JueMingR.TerrariaHost/x86/Debug/net472/JueMingR.TerrariaHost.dll"));
            NativeGuidanceVisualRepairChecks.Stroke(graphics,assembly);
            object context=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker").GetNestedType("PostfixContext",Flags),Flags,null,
                new object[]{"direction-equipment-"+new string('8',40),Path.Combine(root,"evidence.txt"),root},null);
            try
            {
                Call(context,"InitializeRuntime",true); object host=Get(context,"Guidance"),world=Get(host,"World"),input=Get(context,"Input");
                foreach(string flag in new[]{"mapped","finalized","nativePermission"})Set(input,flag,true);Set(input,"IsFocused",true);Set(input,"quarantine",false);
                NativeGuidanceChecks.Until(()=>{Call(context,"UpdateRuntime");return(bool)Get(host,"ControlsEnabled");});
                Set(host,"LayerStatus",Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Rendering.WorldLayerStatus"),"Ready"));
                foreach(GuidanceKind kind in Enum.GetValues(typeof(GuidanceKind)))Call(host,"SetEnabled",kind,true);
                Action<string> scene=name=>
                {
                    Call(context,"UpdateRuntime");Call(world,"Prepare");
                    graphics.Image(Path.Combine(output,name+".png"),()=>
                    {
                        Vector2 center=Main.LocalPlayer.Center-Main.screenPosition; if(Main.LocalPlayer.gravDir==-1)center.Y=Main.screenHeight-center.Y;
                        var pixel=TextureAssets.MagicPixel.Value;
                        Main.spriteBatch.Draw(pixel,new Rectangle((int)center.X-8,(int)center.Y-15,16,30),new Rectangle(0,0,1,1),new Color(90,140,185));
                        Call(world,"Draw");
                    },Main.GameViewMatrix.ZoomMatrix);
                    Require((int)Get(world,"Failures")==0,"actual Guidance GPU draw stays healthy: "+name);
                };
                Call(host,"SetEnabled",GuidanceKind.Merchant,false);Call(host,"SetEnabled",GuidanceKind.Equipment,false);
                for(int i=0;i<4;i++)
                {
                    double angle=new[]{0,22.7,91.3,179.4}[i]*Math.PI/180;
                    Main.npc[1].position=Main.LocalPlayer.Center+new Vector2((float)Math.Cos(angle),(float)Math.Sin(angle))*900-new Vector2(10,20);
                    scene("rare-continuous-"+i);
                }
                Main.GameViewMatrix.Zoom=new Vector2(1.5f);Main.LocalPlayer.gravDir=-1;Main.screenPosition=new Vector2(170,20);scene("rare-inverted-zoom-camera");
                Main.GameViewMatrix.Zoom=Vector2.One;Main.LocalPlayer.gravDir=1;Main.screenPosition=Vector2.Zero;
                Main.npc[1].position=Main.LocalPlayer.Center+new Vector2(8,0)-new Vector2(10,20);scene("rare-close-target");
                Main.screenWidth=640;Main.screenHeight=360;Main.GameViewMatrix.SetViewportOverride(new Viewport(0,0,640,360));
                Main.LocalPlayer.position=new Vector2(5000,4950);Main.screenPosition=new Vector2(4930,4650);
                Main.npc[1].GivenName=new string('稀',100)+"长名字";Main.npc[1].position=Main.LocalPlayer.Center-new Vector2(900,200);
                scene("rare-small-viewport-long-name");
                Main.screenWidth=960;Main.screenHeight=640;Main.GameViewMatrix.SetViewportOverride(new Viewport(0,0,960,640));Main.LocalPlayer.position=new Vector2(470,320);Main.screenPosition=Vector2.Zero;
                Call(host,"SetEnabled",GuidanceKind.Rare,false);Call(host,"SetEnabled",GuidanceKind.Merchant,true);scene("merchant-offscreen-location");
                Call(host,"SetEnabled",GuidanceKind.Equipment,true);
                PopupText.popupText=new PopupText[20];for(int i=0;i<20;i++)PopupText.popupText[i]=new PopupText(); Main.showItemText=true;
                Main.combatText=new CombatText[100];for(int i=0;i<100;i++)Main.combatText[i]=new CombatText();
                object feedback=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Items.ItemDiscardFeedback"),Flags,null,new object[]{(Func<bool>)(()=>true)},null);
                Call(feedback,"Complete",Main.LocalPlayer,"石块",3);
                var item=new Item();item.SetDefaults(Terraria.ID.ItemID.CopperShortsword);
                PopupText.NewText(PopupTextContext.ItemReforge,item,Main.LocalPlayer.Center-new Vector2(0,32),1,true);
                Main.combatText[0]=new CombatText{active=true,text="123",position=Main.LocalPlayer.Top-new Vector2(15,45),scale=1,alpha=1,lifeTime=45,color=Color.OrangeRed};
                for(int i=0;i<20;i++)if(PopupText.popupText[i].active)PopupText.popupText[i].scale=1;
                Call(context,"UpdateRuntime");Call(world,"Prepare");
                graphics.Image(Path.Combine(output,"equipment-native-coexistence.png"),()=>
                {
                    PopupText.DrawItemTextPopups(1); var t=Main.combatText[0]; var font=FontAssets.CombatText[0].Value;
                    var half=font.MeasureString(t.text)*.5f;
                    // Same native CombatText draw arguments from .8 Main's
                    // inline loop; no AI, damage or game update is executed.
                    Main.spriteBatch.DrawString(font,t.text,t.position-Main.screenPosition+half,t.color,t.rotation,half,t.scale,SpriteEffects.None,0);
                    Call(world,"Draw");
                },Main.GameViewMatrix.ZoomMatrix);
                Require((int)Get(world,"Failures")==0,"native popup/combat + equipment actual font draw");
                object shell=Get(context,"Shell"),state=Get(shell,"State"),renderer=Get(shell,"renderer");Call(renderer,"RefreshResources");Set(state,"Ready",true);
                foreach(int page in new[]{1,2,8})
                {
                    Call(state,"Navigate",page);Call(state,"RestoreVisible");Call(renderer,"Prepare",state,960f,640f,1f);
                    graphics.Image(Path.Combine(output,"guidance-f5-page-"+page+".png"),()=>Call(renderer,"Draw",state,Matrix.Identity,false,false),Matrix.Identity);
                }
                NativeGuidanceVisualRepairChecks.Cadence(graphics,context,host);
                NativeGuidanceVisualRepairChecks.LargeWindow(graphics,context,host,output);
                Console.WriteLine("PASS: actual-resource Guidance previews produced; inspect images for appearance (not live game acceptance).");
            }
            finally{StopContext(context);}
        }
    }
}
