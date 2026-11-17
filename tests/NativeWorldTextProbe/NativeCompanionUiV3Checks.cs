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
        private static void Click(object window,string key,Vector2 screen,Matrix? projection=null)
        {
            var c=Controls(window).Single(x=>((string)Get(x,"Key")).StartsWith(key+"/",StringComparison.Ordinal));var point=NativeToolsUiChecks.Point(Get(c,"Rect"));
            foreach(bool down in new[]{false,true,false}) {NativeToolsUiChecks.Mouse(point,down);Call(window,"Process",true,new KeyboardState(),point,projection??Main.UIScaleMatrix,screen,true,0);}
        }
        private static void CheckResourcePixels(object window,ProbeGraphics graphics,string output)
        {
            Require(Main.cursorScale>=.8f,"native resource pulse has its normal neutral draw prerequisite");
            var pixels=graphics.Pixels(()=>Call(window,"Draw"),Main.UIScaleMatrix,1280,800);var hero=Get(window,"companionHero");
            int x=(int)(float)Get(hero,"X")+76,y=(int)(float)Get(hero,"Y");int sharedLife=(int)(float)Get(window,"sharedLifeHeight");
            Func<int,int,int,int,Func<Color,bool>,int> count=(px,py,w,h,predicate)=>{int n=0;for(int yy=py;yy<py+h;yy++)for(int xx=px;xx<px+w;xx++)if(predicate(pixels[yy*1280+xx]))n++;return n;};
            int lastHeart=count(x+4*27,y+20,26,29,c=>c.R>150&&c.R>c.G*1.5f&&c.R>c.B*1.5f);
            int onlyStar=count(x,y+sharedLife+20,26,29,c=>c.B>120&&c.B>c.R*1.15f);
            var ownerHero=Get(window,"ownerHero");int ox=(int)(float)Get(ownerHero,"X")+76,oy=(int)(float)Get(ownerHero,"Y");
            int partial=count(ox+5*27,oy+49,26,29,c=>c.R>100&&c.G>60&&c.R>c.B*1.3f);
            Console.WriteLine("RESOURCE-PIXELS cursorScale="+Main.cursorScale+" last-initial-heart="+lastHeart+" only-initial-star="+onlyStar+" partial-life="+partial);
            Require(lastHeart>8&&onlyStar>8&&partial>6,"actual default resource pixels retain initial fifth heart, sole mana star and injured partial segment");
        }
        private static int HeadDifference(Color[] a,Color[] b,object window)
        {
            var hero=Get(window,"ownerPortrait");int x0=(int)(float)Get(hero,"X"),y0=(int)(float)Get(hero,"Y"),changed=0;
            for(int y=y0;y<y0+90;y++)for(int x=x0;x<x0+90;x++)if(a[y*1280+x]!=b[y*1280+x])changed++;
            return changed;
        }
        private static void CheckAppearanceRefresh(object window,ProbeGraphics graphics,Player owner,string output)
        {
            var helmet=owner.armor[0].Clone();var vanity=owner.armor[10].Clone();var dye=owner.dye[0].Clone();int hair=owner.hair;
            var shaders=Terraria.Graphics.Shaders.GameShaders.Armor;
            try
            {
                graphics.LoadItemTextures(new int[]{Terraria.ID.ItemID.IronHelmet,Terraria.ID.ItemID.FamiliarWig,Terraria.ID.ItemID.RedDye});
                graphics.LoadTexture("ArmorHead","Images/Armor_Head_2",2);
                if(shaders.GetShaderIdFromItemId(Terraria.ID.ItemID.RedDye)==0)
                {
                    var method=typeof(Terraria.Initializers.DyeInitializer).GetMethod("LoadBasicColorDye",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic,null,new[]{typeof(int),typeof(float),typeof(float),typeof(float),typeof(float),typeof(int)},null);
                    method.Invoke(null,new object[]{Terraria.ID.ItemID.RedDye,1f,0f,0f,1.2f,1});
                }
                owner.armor[10]=new Item();owner.armor[0].SetDefaults(Terraria.ID.ItemID.IronHelmet);owner.dye[0]=new Item();
                Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(1280,800));var plain=graphics.Pixels(()=>Call(window,"Draw"),Main.UIScaleMatrix,1280,800);
                var display=(Player)Get(Get(window,"ownerPreview"),"Display");Require(display.head==2,"native armor head is derived on display copy without updating the real body");
                owner.dye[0].SetDefaults(Terraria.ID.ItemID.RedDye);Require(owner.dye[0].dye>0,"native red-dye item resolves its real loaded shader");
                Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(1280,800));var colored=graphics.Pixels(()=>Call(window,"Draw"),Main.UIScaleMatrix,1280,800);
                Require(((Player)Get(Get(window,"ownerPreview"),"Display")).cHead==owner.dye[0].dye&&HeadDifference(plain,colored,window)>20,"actual native dye changes the rendered helmet pixels");
                graphics.Image(Path.Combine(output,"companion-preview-real-helmet-dye.png"),()=>Call(window,"Draw"),Main.UIScaleMatrix,1280,800);
                owner.armor[10].SetDefaults(Terraria.ID.ItemID.FamiliarWig);owner.hair=30;Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(1280,800));
                var worn=graphics.Pixels(()=>Call(window,"Draw"),Main.UIScaleMatrix,1280,800);display=(Player)Get(Get(window,"ownerPreview"),"Display");
                Require(display.head==0&&display.hair==30&&HeadDifference(colored,worn,window)>20,"actual vanity and hairstyle replace the helmet in the next prepared preview");
                graphics.Image(Path.Combine(output,"companion-preview-real-vanity-hair.png"),()=>Call(window,"Draw"),Main.UIScaleMatrix,1280,800);
                Console.WriteLine("APPEARANCE-PIXELS native helmet/dye difference="+HeadDifference(plain,colored,window)+" vanity/hair difference="+HeadDifference(colored,worn,window));
            }
            finally {owner.armor[0]=helmet;owner.armor[10]=vanity;owner.dye[0]=dye;owner.hair=hair;graphics.Pixels(()=>Call(window,"Draw"),Main.UIScaleMatrix,1280,800);}
        }
        internal static void InspectBody(object window)
        {
            var display=(Player)Get(Get(window,"ownerPreview"),"Display");
            Console.WriteLine("BODY-TRACE head/body/legs="+display.head+"/"+display.body+"/"+display.legs+" colors="+display.skinColor+"/"+display.shirtColor+"/"+display.pantsColor+" invis="+display.invis+" light="+display.socialIgnoreLight+" frames="+display.bodyFrame+"/"+display.legFrame);
            foreach(int part in new[]{0,3,6,11})
            {
                var asset=Terraria.GameContent.TextureAssets.Players[display.skinVariant,part];var tex=asset.Value;
                Console.WriteLine("BODY-ASSET-STATE part="+part+" state="+asset.State+" null="+(tex==null));
                Require(asset.IsLoaded&&tex!=null,"original player part is GPU-transferred before body proof: "+part);
                var pixels=new Color[tex.Width*tex.Height];tex.GetData(pixels);
                Console.WriteLine("BODY-TEXTURE part="+part+" state="+asset.State+" name="+asset.Name+" size="+tex.Width+"x"+tex.Height+" opaque="+pixels.Count(c=>c.A>0));
            }
            var draw=((IEnumerable)Get(Get(window,"portraits"),"_drawData")).Cast<Terraria.DataStructures.DrawData>().ToArray();
            foreach(var data in draw) Console.WriteLine("BODY-DRAW texture="+data.texture?.Width+"x"+data.texture?.Height+" source="+data.sourceRect+" color="+data.color+" pos="+data.position+" shader="+data.shader);
        }
        private static void CheckBodyPixels(object window,ProbeGraphics graphics,Player player,string heroName,string output,int width,int height)
        {
            Color skin=player.skinColor,shirt=player.shirtColor,under=player.underShirtColor,pants=player.pantsColor,shoes=player.shoeColor;
            Color faceMark=new Color(30,240,80),shirtMark=new Color(40,100,240),pantsMark=new Color(220,30,220),shoeMark=new Color(245,230,35);
            try
            {
                player.skinColor=faceMark;player.shirtColor=player.underShirtColor=shirtMark;player.pantsColor=pantsMark;player.shoeColor=shoeMark;
                var pixels=graphics.Pixels(()=>Call(window,"Draw"),Main.UIScaleMatrix,width,height);
                using(var image=new Microsoft.Xna.Framework.Graphics.Texture2D(graphics.GraphicsDevice,width,height))
                {image.SetData(pixels);using(var file=File.Create(Path.Combine(output,"companion-body-pixels-"+heroName+".png")))image.SaveAsPng(file,width,height);}
                var hero=Get(window,heroName=="ownerHero"?"ownerPortrait":"companionPortrait");float scale=Main.UIScaleMatrix.M11;
                int x0=Math.Max(0,(int)((float)Get(hero,"X")*scale)),y0=Math.Max(0,(int)((float)Get(hero,"Y")*scale));
                int x1=Math.Min(width,(int)(((float)Get(hero,"X")+Math.Min(90,(float)Get(hero,"Width")))*scale)),y1=Math.Min(height,(int)(((float)Get(hero,"Y")+Math.Min(108,(float)Get(hero,"Height")))*scale));
                int face=0,top=0,bottom=0,feet=0;
                Func<Color,Color,bool> near=(a,b)=>a.A>200&&Math.Abs(a.R-b.R)<65&&Math.Abs(a.G-b.G)<65&&Math.Abs(a.B-b.B)<65;
                for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++) {Color c=pixels[y*width+x];if(y<y0+(y1-y0)*.6f&&near(c,faceMark))face++;if(near(c,shirtMark))top++;if(y>y0+(y1-y0)*.4f&&near(c,pantsMark))bottom++;if(near(c,shoeMark))feet++;}
                Console.WriteLine("BODY-PIXELS "+heroName+" face="+face+" shirt="+top+" pants="+bottom+" shoes="+feet);
                Require(face>10&&top>20&&bottom>15&&feet>4,"actual original-resource "+heroName+" contains visible face, shirt, pants and shoes, not only hair/background");
            }
            finally {player.skinColor=skin;player.shirtColor=shirt;player.underShirtColor=under;player.pantsColor=pants;player.shoeColor=shoes;}
        }
        private static object[] Controls(object window) {return ((IEnumerable)Get(window,"controls")).Cast<object>().ToArray();}
        private static void CheckValueRefresh(object window,Player owner)
        {
            var before=Controls(window);int builds=(int)Get(window,"LayoutBuilds");var original=owner.inventory[0].Clone();bool extra=owner.extraAccessory;
            try
            {
                owner.inventory[0].SetDefaults(Terraria.ID.ItemID.Torch);owner.inventory[0].stack=3;
                Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(1280,800));
                var slot=Controls(window).Single(c=>((string)Get(c,"Key")).StartsWith("slot:True:Inventory:0/",StringComparison.Ordinal));
                Require((int)Get(slot,"ItemType")==Terraria.ID.ItemID.Torch&&(int)Get(slot,"Stack")==3,"one-slot icon and count refresh immediately from actual native inventory");
                owner.inventory[0].stack=4;Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(1280,800));
                owner.extraAccessory=!extra;Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(1280,800));
                var lockSlot=Controls(window).Single(c=>((string)Get(c,"Key")).StartsWith("slot:True:Armor:8/",StringComparison.Ordinal));
                Require((bool)Get(lockSlot,"Enabled")==owner.IsItemSlotUnlockedAndUsable(8),"lock qualification follows the actual target player's native predicate");
                Require((int)Get(slot,"Stack")==4&&(int)Get(window,"LayoutBuilds")==builds&&Controls(window).Zip(before,(a,b)=>ReferenceEquals(a,b)).All(x=>x),"type, count and lock refresh preserve both panes' actual control objects and geometry");
                Console.WriteLine("COMPANION V3 value-refresh LayoutBuilds before="+builds+" after="+Get(window,"LayoutBuilds")+" slotUpdates="+Get(window,"ItemPresentationUpdates"));
            }
            finally {owner.inventory[0]=original;owner.extraAccessory=extra;Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(1280,800));}
        }
        private static void CheckAvatarCenter(object window,ProbeGraphics graphics,Player ai,Guid id,string output)
        {
            var armor=ai.armor[0];int hair=ai.hair;
            try
            {
                ai.hair=30;ai.armor[0]=new Item();
                foreach(bool helmet in new[]{false,true})
                {
                    if(helmet){ai.armor[0]=new Item();ai.armor[0].SetDefaults(Terraria.ID.ItemID.IronHelmet);}
                    Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(1280,800));var pixels=graphics.Pixels(()=>Call(window,"Draw"),Main.UIScaleMatrix,1280,800);
                    var c=Controls(window).Single(x=>(Guid)Get(x,"ProfileId")==id);var r=Get(c,"Rect");float scale=Main.UIScaleMatrix.M11;
                    int x0=(int)((float)Get(r,"X")*scale),y0=(int)((float)Get(r,"Y")*scale),x1=(int)((float)Get(r,"Right")*scale),y1=(int)((float)Get(r,"Bottom")*scale);
                    int left=x1,right=x0,top=y1,bottom=y0;
                    for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++) {var color=pixels[y*1280+x];bool faceHair=color.R>80&&color.R>color.G*1.2f&&color.R>color.B*1.3f;bool metal=helmet&&color.R>65&&Math.Abs(color.R-color.G)<20&&Math.Abs(color.G-color.B)<20;
                        if(faceHair||metal){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}}
                    float dx=(left+right+1)/2f-(x0+x1)/2f,dy=(top+bottom+1)/2f-(y0+y1)/2f;
                    Console.WriteLine("AVATAR-PIXELS helmet="+helmet+" offset="+dx+","+dy+" bounds="+left+","+top+".."+right+","+bottom);
                    Require(right>left&&Math.Abs(dx)<=2.5f&&Math.Abs(dy)<=2.5f,"actual native hair/helmet visible head is centered in avatar card");
                    graphics.Image(Path.Combine(output,helmet?"companion-avatar-helmet-centered.png":"companion-avatar-hair-centered.png"),()=>Call(window,"Draw"),Main.UIScaleMatrix,1280,800);
                }
            }
            finally {ai.armor[0]=armor;ai.hair=hair;Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(1280,800));}
        }
        private static void AssertBaselines(object window)
        {
            foreach(string identity in new[]{"Armor:0","Dye:0","Inventory:0","Inventory:50"})
            {
                var own=Controls(window).FirstOrDefault(c=>((string)Get(c,"Key")).StartsWith("slot:True:"+identity+"/",StringComparison.Ordinal));
                var ai=Controls(window).FirstOrDefault(c=>((string)Get(c,"Key")).StartsWith("slot:False:"+identity+"/",StringComparison.Ordinal));
                if(own!=null&&ai!=null)Require(Math.Abs((float)Get(Get(own,"Rect"),"Y")-(float)Get(Get(ai,"Rect"),"Y"))<.01f,"shared header/equipment/bag/money actual baseline "+identity);
            }
            var tabs=Controls(window).Where(c=>((string)Get(c,"Key")).StartsWith("gear:",StringComparison.Ordinal)).ToArray();
            Require(tabs.All(c=>(float)Get(Get(c,"Rect"),"Width")<90),"actual subtabs use compact text widths");
            foreach(var slot in Controls(window).Where(c=>(bool)Get(c,"IsSlot")&&Get(c,"Area").ToString()=="Dye"))Require((int)Get(slot,"EmptyIcon")==1,"actual dye placeholder uses native context12/25 frame1");
        }
        private static void CheckDynamicPress(object window,object host,Guid id)
        {
            var states=(IDictionary)Get(host,"states");object old=states.Contains(id)?states[id]:null;
            var original=((JueMingR.Platform.Companions.CompanionProfile)((IEnumerable)Get(host,"Profiles")).Cast<object>().Single(p=>(Guid)Get(p,"Id")==id)).Behavior;
            Action<Vector2,bool> sample=(point,left)=>{NativeToolsUiChecks.Mouse(point,left);Call(window,"Process",true,new KeyboardState(),point,Main.UIScaleMatrix,new Vector2(1280,800),true,0);};
            try
            {
                var c=Controls(window).Single(x=>((string)Get(x,"Key")).StartsWith("gear:False:1/",StringComparison.Ordinal));var point=NativeToolsUiChecks.Point(Get(c,"Rect"));
                sample(point,false);sample(point,true);states[id]="移动中";Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(1280,800));sample(point,false);
                Require((int)Get(window,"companionGearPage")==1,"actual press, dynamic status refresh, release retains the valid subtab click");
                c=Controls(window).Single(x=>((string)Get(x,"Key")).StartsWith("gear:False:2/",StringComparison.Ordinal));point=NativeToolsUiChecks.Point(Get(c,"Rect"));sample(point,false);sample(point,true);
                var changed=original.Clone();changed.Follow=!changed.Follow;Require((bool)Call(host,"SetBehavior",id,changed),"real profile setting save accepted between press and release");sample(point,false);
                Require((int)Get(window,"companionGearPage")==2,"ordinary real revision/save refresh preserves a valid subtab press with unchanged identity/geometry");
                foreach(int tab in new[]{0,1,2,0,2,1,0}) {Click(window,"gear:False:"+tab,new Vector2(1280,800));Require((int)Get(window,"companionGearPage")==tab,"rapid ordinary tab click accepted without invented cooldown "+tab);}
            }
            finally {if(old==null)states.Remove(id);else states[id]=old;Call(host,"SetBehavior",id,original);NativeToolsUiChecks.Mouse(Vector2.Zero,false);}
        }
        internal static void Draw(object context,ProbeGraphics graphics,string output)
        {
            var shell=Get(context,"Shell");var host=Get(shell,"Companions");var window=Get(shell,"CompanionUi");
            var owner=Main.LocalPlayer;var archive=Get(host,"profiles");
            NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return (bool)Get(archive,"Ready");});
            Require((bool)Call(host,"Create","图形队友"),"real archive accepts isolated visual character");
            NativeQuickItemChecks.Until(()=>{Call(archive,"Pump");return ((IEnumerable)Get(host,"Profiles")).Cast<object>().Any();});
            Guid id=(Guid)Get(((IEnumerable)Get(host,"Profiles")).Cast<object>().Last(),"Id");
            NativeQuickItemChecks.Until(()=>{Call(host,"Update",(ulong)Main.GameUpdateCount);return (bool)Call(archive,"IsSaved",id);});
            Require((string)Get(host,"Feedback")!="正在保存新人物。","normal owning update retires completed initial-save feedback");
            var ai=(Player)Call(host,"GetBody",id);
            graphics.LoadItemTextures(owner.inventory.Concat(ai.inventory).Where(i=>i!=null&&!i.IsAir).Select(i=>i.type).Distinct());
            int[] prior={owner.statLife,owner.statLifeMax,owner.statLifeMax2,owner.statMana,owner.statManaMax,owner.statManaMax2};
            string previousStyle=Main.ResourceSetsManager.ActiveSetKeyName;string ownerName=owner.name;owner.name="验证主人";
            var previousRng=Main.rand;bool previousShampoo=owner.leinforsHair;
            bool previousPoison=owner.poisoned,previousHoney=owner.honeyWet;
            int previousMountShader=Mount.currentShader,previousMisc=owner.miscCounter,previousSwim=owner.swimTime;
            int previousAfk=owner.afkCounter,previousKiting=owner.afkCounterForKiting;
            try
            {
                owner.statLife=463;owner.statLifeMax=500;owner.statLifeMax2=600;owner.statMana=371;owner.statManaMax=200;owner.statManaMax2=400;
                owner.poisoned=true;owner.honeyWet=true;Mount.currentShader=73;
                owner.leinforsHair=true;owner.afkCounter=239;owner.afkCounterForKiting=157;
                Main.rand=new Terraria.Utilities.UnifiedRandom(933);var expected=new Terraria.Utilities.UnifiedRandom(933);
                Main.screenWidth=1280;Main.screenHeight=800;Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();Main.UIScale=1;
                Call(window,"Close");NativeToolsUiChecks.Mouse(Vector2.Zero,false);Call(window,"Toggle",Get(Get(Get(shell,"State"),"Layout"),"Viewport"));
                Set(window,"selected",id);Set(window,"page",0);Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(1280,800));
                var ownerView=Get(window,"ownerView");var aiView=Get(window,"companionView");
                AssertBaselines(window);
                Require((float)Get(ownerView,"Right")<(float)Get(aiView,"X"),"actual dual columns are disjoint");
                Require((float)Get(Get(window,"listView"),"Width")<70,"actual avatar rail remains narrow");
                foreach(string style in new[]{"Default","New","NewWithText","HorizontalBars","HorizontalBarsWithText","HorizontalBarsWithFullText"})
                {
                    Call(Main.ResourceSetsManager,"SetActive",style);Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(1280,800));
                    graphics.Image(Path.Combine(output,"companion-v3-"+style+"-1280x800.png"),()=>Call(window,"Draw"),Main.UIScaleMatrix,1280,800);
                    Require((string)Get(Get(window,"resourceDisplay"),"StyleKey")==style,"actual resource adapter follows native key "+style);
                    if(style=="Default")CheckResourcePixels(window,graphics,output);
                }
                CheckBodyPixels(window,graphics,owner,"ownerHero",output,1280,800);
                CheckBodyPixels(window,graphics,ai,"companionHero",output,1280,800);
                CheckAppearanceRefresh(window,graphics,owner,output);CheckAvatarCenter(window,graphics,ai,id,output);
                var preview=Get(window,"ownerPreview");var display=(Player)Get(preview,"Display");
                Require(!ReferenceEquals(display,owner)&&!display.leinforsHair&&owner.leinforsHair,"preview isolates native particle-only shampoo flag");
                Require(!display.poisoned&&!display.honeyWet&&owner.poisoned&&owner.honeyWet&&Mount.currentShader==73&&owner.miscCounter==previousMisc&&owner.swimTime==previousSwim,"preview excludes native BoringSetup effects and restores shared mount shader without advancing owner timers");
                Require(owner.afkCounter==239&&owner.afkCounterForKiting==157,"preview selected-item state never calls the real owner");
                Require(Main.rand.Next()==expected.Next(),"native preview draw preserves world RNG stream");
                int rebuilds=(int)Get(preview,"Rebuilds"),layouts=(int)Get(window,"LayoutBuilds");
                int life=owner.statLife,mana=owner.statMana,items=owner.inventory.Sum(i=>i?.stack??0),dust=Main.dust.Count(d=>d!=null&&d.active);
                for(int i=0;i<20;i++) {Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(1280,800));graphics.Render(()=>Call(window,"Draw"),Main.UIScaleMatrix);}
                Require((int)Get(preview,"Rebuilds")==rebuilds&&(int)Get(window,"LayoutBuilds")==layouts,"stable full native Draw does not rebuild preview or layout");
                Require(owner.statLife==life&&owner.statMana==mana&&owner.inventory.Sum(i=>i?.stack??0)==items&&Main.dust.Count(d=>d!=null&&d.active)==dust,"preview leaves real resources and native world dust unchanged");
                CheckValueRefresh(window,owner);CheckDynamicPress(window,host,id);
                // Content-derived preferred size now fits all cells at 1280x800.
                // Use a real lower native viewport to prove independent overflow.
                Main.screenWidth=1280;Main.screenHeight=640;Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();Main.UIScale=1;
                Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(1280,640));
                Require((int)Get(window,"ownerMaximum")>0,"independent-wheel fixture has real owner overflow in its actual lower viewport");
                var point=NativeToolsUiChecks.Point(Get(window,"ownerView"));NativeToolsUiChecks.Mouse(point,false);
                Call(window,"Process",true,new KeyboardState(),point,Main.UIScaleMatrix,new Vector2(1280,640),true,-120);
                Require((int)Get(window,"ownerOffset")>0&&(int)Get(window,"bodyOffset")==0,"actual owner wheel leaves AI scroll independent");
                graphics.Image(Path.Combine(output,"companion-v3-independent-scroll.png"),()=>Call(window,"Draw"),Main.UIScaleMatrix,1280,640);
                Call(window,"Process",true,new KeyboardState(),point,Main.UIScaleMatrix,new Vector2(1280,640),true,120);
                Require((int)Get(window,"ownerOffset")==0,"real inverse wheel returns to the visible header before narrow snapshots");
                Main.screenWidth=840;Main.screenHeight=640;Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();Main.UIScale=1.5f;
                Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(840,640));
                Require((bool)Get(window,"Visible")&&Controls(window).All(c=>(float)Get(Get(c,"Rect"),"Width")>0&&(float)Get(Get(c,"Rect"),"Height")>0),"same small-width layout keeps actual control rectangles positive");
                graphics.Image(Path.Combine(output,"companion-followup-narrow-840x640.png"),()=>Call(window,"Draw"),Main.UIScaleMatrix,840,640);
                Click(window,"gear:False:2",new Vector2(840,640));Require((int)Get(window,"companionGearPage")==2,"actual narrow-width equipment tab remains clickable");
                Click(window,"gear:False:0",new Vector2(840,640));
                // Vanilla clamps UIScale so its logical viewport stays >=800x600.
                // Independently exercise the window's explicit matrix contract at
                // a smaller logical projection; do not call it an accepted game scale.
                var narrowProjection=Matrix.CreateScale(1.5f);
                Call(window,"Prepare",narrowProjection,new Vector2(840,640));
                Require(((IEnumerable)Get(window,"labels")).Cast<object>().All(label=>(float)Get(Get(label,"Rect"),"Width")>0),"controlled narrow projection never creates a nonpositive name/status label rectangle");
                graphics.Image(Path.Combine(output,"companion-controlled-narrow-projection.png"),()=>Call(window,"Draw"),narrowProjection,840,640);
                Click(window,"gear:False:2",new Vector2(840,640),narrowProjection);Require((int)Get(window,"companionGearPage")==2,"controlled narrow projection retains its actual subtab hit geometry");
                Click(window,"gear:False:0",new Vector2(840,640),narrowProjection);
                Console.WriteLine("COMPANION controlled narrow projection=1.5; actual vanilla accepted scale="+Main.UIScale);

                Main.screenWidth=1366;Main.screenHeight=768;Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();Main.UIScale=1.25f;
                Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(1366,768));
                graphics.Image(Path.Combine(output,"companion-v3-1366x768-native-scale.png"),()=>Call(window,"Draw"),Main.UIScaleMatrix,1366,768);
                Console.WriteLine("COMPANION V3 requestedScale=1.25 nativeScale="+Main.UIScale+" matrix="+Main.UIScaleMatrix+" AI="+ai.statLife+"/"+ai.statLifeMax2+" owner="+owner.statLife+"/"+owner.statLifeMax2);
                Click(window,"create",new Vector2(1366,768));
                NativeCompanionNativeCreationChecks.Run(context,graphics,output);
                Console.WriteLine("PASS production dual-pane full-body Draw, all six original resource keys, 600 life/400 mana and partial segments, independent scroll, preview stable work/RNG/AFK isolation and real original ingame native creation flow. Installed game/OS IME/FPS remain unverified.");
            }
            finally
            {
                owner.name=ownerName;Call(window,"Close");Call(Main.ResourceSetsManager,"SetActive",previousStyle);Main.rand=previousRng;owner.leinforsHair=previousShampoo;owner.afkCounter=previousAfk;owner.afkCounterForKiting=previousKiting;
                owner.poisoned=previousPoison;owner.honeyWet=previousHoney;Mount.currentShader=previousMountShader;
                owner.statLife=prior[0];owner.statLifeMax=prior[1];owner.statLifeMax2=prior[2];owner.statMana=prior[3];owner.statManaMax=prior[4];owner.statManaMax2=prior[5];
                NativeToolsUiChecks.Mouse(Vector2.Zero,false);
            }
        }
    }
}
