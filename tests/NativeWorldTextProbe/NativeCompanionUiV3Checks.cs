using System;
using System.Collections;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Runs actual production Draw against the supplied original XNB content.
    // This is an isolated graphics consumer, not an installed-game/OS-input test.
    internal static class NativeCompanionUiV3Checks
    {
        internal static void Draw(object context,ProbeGraphics graphics,string output)
        {
            var shell=Get(context,"Shell");var host=Get(shell,"Companions");var window=Get(shell,"CompanionUi");
            var owner=Main.LocalPlayer;var archive=Get(host,"profiles");
            NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return (bool)Get(archive,"Ready");});
            Require((bool)Call(host,"Create","图形队友"),"real archive accepts isolated visual character");
            NativeQuickItemChecks.Until(()=>{Call(archive,"Pump");return ((IEnumerable)Get(host,"Profiles")).Cast<object>().Any();});
            Guid id=(Guid)Get(((IEnumerable)Get(host,"Profiles")).Cast<object>().Last(),"Id");
            var ai=(Player)Call(host,"GetBody",id);
            graphics.LoadItemTextures(owner.inventory.Concat(ai.inventory).Where(i=>i!=null&&!i.IsAir).Select(i=>i.type).Distinct());
            int[] prior={owner.statLife,owner.statLifeMax,owner.statLifeMax2,owner.statMana,owner.statManaMax,owner.statManaMax2};
            string previousStyle=Main.ResourceSetsManager.ActiveSetKeyName;
            var previousRng=Main.rand;bool previousShampoo=owner.leinforsHair;
            int previousAfk=owner.afkCounter,previousKiting=owner.afkCounterForKiting;
            try
            {
                owner.statLife=463;owner.statLifeMax=500;owner.statLifeMax2=600;owner.statMana=371;owner.statManaMax=200;owner.statManaMax2=400;
                owner.leinforsHair=true;owner.afkCounter=239;owner.afkCounterForKiting=157;
                Main.rand=new Terraria.Utilities.UnifiedRandom(933);var expected=new Terraria.Utilities.UnifiedRandom(933);
                Main.screenWidth=1280;Main.screenHeight=800;Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();Main.UIScale=1;
                Call(window,"Close");NativeToolsUiChecks.Mouse(Vector2.Zero,false);Call(window,"Toggle",Get(Get(Get(shell,"State"),"Layout"),"Viewport"));
                Set(window,"selected",id);Set(window,"page",0);Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(1280,800));
                var ownerView=Get(window,"ownerView");var aiView=Get(window,"companionView");
                Require((float)Get(ownerView,"Right")<(float)Get(aiView,"X"),"actual dual columns are disjoint");
                Require((float)Get(Get(window,"listView"),"Width")<70,"actual avatar rail remains narrow");
                foreach(string style in new[]{"Default","New","NewWithText","HorizontalBars","HorizontalBarsWithText","HorizontalBarsWithFullText"})
                {
                    Call(Main.ResourceSetsManager,"SetActive",style);Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(1280,800));
                    graphics.Image(Path.Combine(output,"companion-v3-"+style+"-1280x800.png"),()=>Call(window,"Draw"),Main.UIScaleMatrix,1280,800);
                    Require((string)Get(Get(window,"resourceDisplay"),"StyleKey")==style,"actual resource adapter follows native key "+style);
                }
                var preview=Get(window,"ownerPreview");var display=(Player)Get(preview,"Display");
                Require(!ReferenceEquals(display,owner)&&!display.leinforsHair&&owner.leinforsHair,"preview isolates native particle-only shampoo flag");
                Require(owner.afkCounter==239&&owner.afkCounterForKiting==157,"preview selected-item state never calls the real owner");
                Require(Main.rand.Next()==expected.Next(),"native preview draw preserves world RNG stream");
                int rebuilds=(int)Get(preview,"Rebuilds"),layouts=(int)Get(window,"LayoutBuilds");
                int life=owner.statLife,mana=owner.statMana,items=owner.inventory.Sum(i=>i?.stack??0),dust=Main.dust.Count(d=>d!=null&&d.active);
                for(int i=0;i<20;i++) {Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(1280,800));graphics.Render(()=>Call(window,"Draw"),Main.UIScaleMatrix);}
                Require((int)Get(preview,"Rebuilds")==rebuilds&&(int)Get(window,"LayoutBuilds")==layouts,"stable full native Draw does not rebuild preview or layout");
                Require(owner.statLife==life&&owner.statMana==mana&&owner.inventory.Sum(i=>i?.stack??0)==items&&Main.dust.Count(d=>d!=null&&d.active)==dust,"preview leaves real resources and native world dust unchanged");
                var point=NativeToolsUiChecks.Point(ownerView);NativeToolsUiChecks.Mouse(point,false);
                Call(window,"Process",true,new KeyboardState(),point,Main.UIScaleMatrix,new Vector2(1280,800),true,-120);
                Require((int)Get(window,"ownerOffset")>0&&(int)Get(window,"bodyOffset")==0,"actual owner wheel leaves AI scroll independent");
                graphics.Image(Path.Combine(output,"companion-v3-independent-scroll.png"),()=>Call(window,"Draw"),Main.UIScaleMatrix,1280,800);
                Main.screenWidth=1366;Main.screenHeight=768;Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();Main.UIScale=1.25f;
                Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(1366,768));
                graphics.Image(Path.Combine(output,"companion-v3-1366x768-native-scale.png"),()=>Call(window,"Draw"),Main.UIScaleMatrix,1366,768);
                Console.WriteLine("COMPANION V3 requestedScale=1.25 nativeScale="+Main.UIScale+" matrix="+Main.UIScaleMatrix+" AI="+ai.statLife+"/"+ai.statLifeMax2+" owner="+owner.statLife+"/"+owner.statLifeMax2);
                Call(window,"StartCreation");Call(window,"CancelEdit");Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(1366,768));
                graphics.Image(Path.Combine(output,"companion-v3-create.png"),()=>Call(window,"Draw"),Main.UIScaleMatrix,1366,768);
                Console.WriteLine("PASS production dual-pane full-body Draw, all six original resource keys, 600 life/400 mana and partial segments, independent scroll, preview stable work/RNG/AFK isolation and original-resource creation screenshot. Installed game/OS IME/FPS remain unverified.");
            }
            finally
            {
                Call(window,"Close");Call(Main.ResourceSetsManager,"SetActive",previousStyle);Main.rand=previousRng;owner.leinforsHair=previousShampoo;owner.afkCounter=previousAfk;owner.afkCounterForKiting=previousKiting;
                owner.statLife=prior[0];owner.statLifeMax=prior[1];owner.statLifeMax2=prior[2];owner.statMana=prior[3];owner.statManaMax=prior[4];owner.statManaMax2=prior[5];
                NativeToolsUiChecks.Mouse(Vector2.Zero,false);
            }
        }
    }
}
