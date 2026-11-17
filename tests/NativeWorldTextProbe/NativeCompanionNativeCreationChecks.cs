using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.GameContent.UI.States;
using Terraria.GameInput;
using Terraria.IO;
using Terraria.UI;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Native UI dispatch, real controls, original fonts/textures and native text
    // queue; no installed-game, physical OS keyboard or IME acceptance claim.
    internal static class NativeCompanionNativeCreationChecks
    {
        private static IEnumerable<object> Controls(object window) {return ((IEnumerable)Get(window,"controls")).Cast<object>();}
        private static TimeSpan clock;
        private static object currentContext;
        private static int playerFiles;
        private static bool proveWorldUse;
        private static UIColoredSlider observedSlider;
        private static object observedSession;
        private static int sliderTraces;
        private static void TraceSlider(UIColoredSlider __instance)
        {
            if(!ReferenceEquals(__instance,observedSlider)||sliderTraces++>=12)return;
            Console.WriteLine("NATIVE-SLIDER-DRAW point="+Main.mouseX+","+Main.mouseY+" mainLeft="+Main.mouseLeft+" triggerLeft="+PlayerInput.Triggers.Current.MouseLeft+" locked="+ReferenceEquals(UISliderBase.CurrentLockedSlider,__instance)+" aimed="+ReferenceEquals(UISliderBase.CurrentAimedSlider,__instance)+" gamepad="+PlayerInput.UsingGamepad+" trusted="+Get(observedSession,"InputAllowed")+" flushed="+Get(observedSession,"flushedFrame")+" frame="+Get(Get(observedSession,"input"),"Frame")+" rect="+__instance.GetDimensions().ToRectangle()+" hair="+((Player)Get(observedSession,"Draft")).hairColor);
        }
        private static bool FrozenKeyboard(ref Microsoft.Xna.Framework.Input.KeyboardState __result) {__result=Main.keyState;return false;}
        private static bool RejectPlayerFile() {playerFiles++;throw new InvalidOperationException("native AI creator reached ordinary PlayerFileData.CreateAndSave");}
        private static void TraceSpawnFailure(Exception __exception,MethodBase __originalMethod)
        {if(__exception!=null)Console.Error.WriteLine("NATIVE-CREATOR actual spawn failure "+__originalMethod.DeclaringType.FullName+"."+__originalMethod.Name+": "+__exception);}
        private static object Session(object host)
        {return host.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.CompanionCreationSession",true).GetProperty("Active",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);}
        private static IEnumerable<UIElement> Descendants(UIElement parent)
        {yield return parent;foreach(var child in parent.Children)foreach(var descendant in Descendants(child))yield return descendant;}
        private static UIElement Snap(UIState state,string name,int id)
        {return Descendants(state).Single(e=> {SnapPoint snap;return e.GetSnapPoint(out snap)&&snap.Name==name&&snap.Id==id;});}
        private static void Step(Vector2 logical,bool left,ProbeGraphics graphics)
        {
            NativeFrame(currentContext,logical*Main.UIScale,left,new Microsoft.Xna.Framework.Input.Keys[0]);
            graphics.Render(()=>Main.InGameUI.Draw(Main.spriteBatch,new GameTime(clock,TimeSpan.FromMilliseconds(250))),Main.UIScaleMatrix);
            Require(!Main.mouseLeft&&!PlayerInput.Triggers.Current.MouseLeft,"native UI Draw does not return its held mouse sample to world actions");
            if(proveWorldUse)WorldBlocked();
        }
        private static void NativeFrame(object context,Vector2 physical,bool left,Microsoft.Xna.Framework.Input.Keys[] keys)
        {
            var input=Get(context,"Input");var shell=Get(context,"Shell");Call(input,"BeginUpdate");if(!(bool)Get(input,"IsFocused"))Call(shell,"CancelForFocusLoss");
            clock+=TimeSpan.FromMilliseconds(250);Main.gameTimeCache=new GameTime(clock,TimeSpan.FromMilliseconds(250));
            // Real .8 order: UI first, then native mouse/mapping/keyboard and
            // the production InputPostfix continuation. Never inject a logical
            // point immediately before UI.Update to bypass the world/UI zoom.
            PlayerInput.SetZoom_UI();
            Main.InGameUI.Update(new GameTime(clock,TimeSpan.FromMilliseconds(250)));
            PlayerInput.SetZoom_Unscaled();Call(shell,"BeforeInput");
            NativeToolsUiChecks.Mouse(physical,left);var mouseKeys=new List<string>();if(left)mouseKeys.Add("Mouse1");Call(input,"AfterNativeMouse",mouseKeys);
            PlayerInput.Triggers.Reset();var configuration=PlayerInput.CurrentProfile.InputModes[InputMode.Keyboard];foreach(var token in mouseKeys)configuration.Processkey(PlayerInput.Triggers.Current,token,InputMode.Keyboard);foreach(var key in keys)configuration.Processkey(PlayerInput.Triggers.Current,key.ToString(),InputMode.Keyboard);PlayerInput.Triggers.Update();Main.mouseLeft=left;Main.mouseRight=false;Main.mouseX=(int)physical.X;Main.mouseY=(int)physical.Y;
            Call(input,"AfterMapping");PlayerInput.CacheMousePositionForZoom();PlayerInput.SetZoom_MouseInWorld();Main.keyState=new Microsoft.Xna.Framework.Input.KeyboardState(keys);Call(input,"AfterKeyboardRefresh");
            int worldX=Main.mouseX,worldY=Main.mouseY,width=Main.screenWidth,height=Main.screenHeight;
            bool modal=Session(Get(context,"Companions"))!=null;
            Call(context,"ProcessNativeCompanionCreationInput");
            Require(Main.mouseX==worldX&&Main.mouseY==worldY&&Main.screenWidth==width&&Main.screenHeight==height,"deferred UI dispatch restores the native world zoom coordinates and dimensions");
            Call(shell,"ProcessInput");Call(context,"UpdateRuntime");Call(context,"UpdateShell");Require(!(bool)Get(shell,"Failed"),"live-order creator host frame remains healthy");
            if(proveWorldUse&&(modal||Session(Get(context,"Companions"))!=null||(bool)Call(Get(shell,"CompanionUi"),"Contains",physical.X/Main.UIScale,physical.Y/Main.UIScale)))WorldBlocked();
        }
        private static void WorldBlocked()
        {PlayerInput.Triggers.Current.CopyInto(Main.LocalPlayer);Main.LocalPlayer.ItemCheck();Require(!Main.LocalPlayer.controlUseItem&&!Main.LocalPlayer.controlUseTile&&!Main.mouseLeft&&Main.LocalPlayer.itemAnimation==0,"real world CopyInto/ItemCheck cannot begin weapon or tile use from the owned UI sample");}
        private static void Click(UIElement element,ProbeGraphics graphics)
        {
            var point=element.GetDimensions().Center();
            Require(point.X>=0&&point.Y>=0&&point.X<Main.InGameUI.GetDimensions().Width&&point.Y<Main.InGameUI.GetDimensions().Height,"actual native target is inside logical viewport: "+element.GetType().Name);
            Step(point,false,graphics);Step(point,true,graphics);Step(point,false,graphics);
        }
        private static void Capture(ProbeGraphics graphics,string output,string name)
        {graphics.Image(Path.Combine(output,name+".png"),()=>Main.InGameUI.Draw(Main.spriteBatch,new GameTime(clock,TimeSpan.FromMilliseconds(16))),Main.UIScaleMatrix,Main.screenWidth,Main.screenHeight);}
        internal static void CancelEntry(object context,ProbeGraphics graphics,string output)
        {
            var host=Get(context,"Companions");var window=Get(Get(context,"Shell"),"CompanionUi");
            currentContext=context;var session=Session(host);Require(session!=null&&Main.InGameUI.CurrentState is UICharacterCreation&&Main.inFancyUI&&!Main.gameMenu&&!(bool)Get(window,"Visible"),"actual plus hands management to the real ingame native creator");
            Require(((Player)Get(session,"Draft")).difficulty==3,"real Journey-world plus retains Journey default after native constructor forcibly wrote zero");
            Capture(graphics,output,"companion-native-creation-first");
            Click(Snap(Main.InGameUI.CurrentState,"Back",0),graphics);
            Require(Session(host)==null&&!Main.inFancyUI&&(bool)Get(window,"Visible"),"actual native Back returns to the same independent manager without a draft archive");
        }
        internal static void LiveInput(object context,ProbeGraphics graphics,string output)
        {
            NativeCombatAimChecks.Initialize();Main.spawnTileX=60;Main.spawnTileY=58;Main.leftWorld=Main.topWorld=0;Main.rightWorld=Main.maxTilesX*16;Main.bottomWorld=Main.maxTilesY*16;
            for(int x=2;x<118;x++){Main.tile[x,60].active(true);Main.tile[x,60].type=Terraria.ID.TileID.Stone;}
            new Harmony("JueMingR.Tests.QuickItemOutlets").Unpatch(AccessTools.Method(typeof(Player),"Spawn"),HarmonyPatchType.Prefix,"JueMingR.Tests.QuickItemOutlets");
            NativeCompanionCoreLifecycleChecks.PrepareHuman();graphics.LoadCompanionAssets();
            var host=Get(context,"Companions");var shell=Get(context,"Shell");var window=Get(shell,"CompanionUi");var input=Get(context,"Input");var archive=Get(host,"profiles");
            NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return (bool)Get(archive,"Ready");});
            var human=Main.LocalPlayer;var item=human.inventory[0];int selected=human.selectedItem;var ownerArray=Main.player.ToArray();var pending=Main.PendingPlayer;
            string ownerName=human.name;var foreground=Get(input,"foregroundWindow");var zoom=Main.GameViewMatrix.Zoom;
            var inventoryBindings=PlayerInput.CurrentProfile.InputModes[InputMode.Keyboard].KeyStatus["Inventory"];var originalInventoryKeys=inventoryBindings.ToArray();
            var guard=new Harmony("JueMingR.Tests.CompanionLiveNativeInput");playerFiles=0;
            guard.Patch(AccessTools.Method(typeof(PlayerFileData),"CreateAndSave"),prefix:new HarmonyMethod(typeof(NativeCompanionNativeCreationChecks),nameof(RejectPlayerFile)));
            guard.Patch(AccessTools.Method(typeof(Microsoft.Xna.Framework.Input.Keyboard),"GetState",Type.EmptyTypes),prefix:new HarmonyMethod(typeof(NativeCompanionNativeCreationChecks),nameof(FrozenKeyboard)));
            currentContext=context;proveWorldUse=true;
            try
            {
                human.name="验证主人";human.inventory[0]=new Item();human.inventory[0].SetDefaults(Terraria.ID.ItemID.CopperShortsword);human.selectedItemState.Select(0);human.itemAnimation=human.itemTime=0;graphics.LoadItemTextures(new int[]{Terraria.ID.ItemID.CopperShortsword});
                Main.screenWidth=1366;Main.screenHeight=900;PlayerInput.CacheOriginalScreenDimensions();Main.UIScale=1.25f;Main.GameViewMatrix.Zoom=new Vector2(1.5f);
                var renderer=Get(shell,"renderer");Call(renderer,"RefreshResources");Call(renderer,"Prepare",Get(shell,"State"),1366f,900f,Main.UIScale);
                Call(Main.ResourceSetsManager,"SetActive","NewWithText");Call(shell,"OpenCompanions",Get(Get(Get(shell,"State"),"Layout"),"Viewport"));Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(1366,900));
                Action<string> managerClick=prefix=>{var c=Controls(window).Single(v=>((string)Get(v,"Key")).StartsWith(prefix+"/",StringComparison.Ordinal));var p=NativeToolsUiChecks.Point(Get(c,"Rect"))*Main.UIScale;foreach(bool left in new[]{false,true,false})NativeFrame(context,p,left,new Microsoft.Xna.Framework.Input.Keys[0]);};
                NativeFrame(context,Vector2.Zero,false,new Microsoft.Xna.Framework.Input.Keys[0]);NativeFrame(context,Vector2.Zero,false,new Microsoft.Xna.Framework.Input.Keys[0]);managerClick("create");
                Require(Session(host)!=null&&!(bool)Get(input,"CanStartActions"),"actual plus admits native modal ownership in the opening Shell.ProcessInput frame");WorldBlocked();
                var creator=(UICharacterCreation)Main.InGameUI.CurrentState;var session=Session(host);var draft=(Player)Get(session,"Draft");
                Click((UIElement)Get(creator,"_clothingStylesCategoryButton"),graphics);Require(Convert.ToInt32(Get(creator,"_selectedPicker"))==1,"real pre-input native UI update followed by host finalized dispatch accepts actual category click");
                Click(((UIColoredImageButton[])Get(creator,"_colorPickers"))[3],graphics);var slider=Descendants(creator).OfType<UIColoredSlider>().First();var rect=slider.GetDimensions();var color=draft.hairColor;
                observedSlider=slider;observedSession=session;sliderTraces=0;guard.Patch(AccessTools.Method(typeof(UIColoredSlider),"DrawSelf"),postfix:new HarmonyMethod(typeof(NativeCompanionNativeCreationChecks),nameof(TraceSlider)));
                Console.WriteLine("NATIVE-SLIDER-TARGET picker="+Get(creator,"_selectedPicker")+" color="+color+" hsl="+Get(creator,"_currentColorHSL")+" rect="+rect.ToRectangle()+" barWidth="+TextureWidth("ColorBar"));
                // Native DrawSelf puts the value bar at Y+10+8; use its
                // interior, including physical -> fractional UI integer mapping.
                var a=new Vector2(rect.X+rect.Width-10-TextureWidth("ColorBar")+20,rect.Y+18);var b=a+new Vector2(105,0);
                Step(a,false,graphics);Step(a,true,graphics);Step(b,true,graphics);Step(b,false,graphics);Capture(graphics,output,"companion-live-slider-diagnostic");Require(draft.hairColor!=color,"native held-left color slider Draw consumes its trusted private pointer after world input was cleared");observedSlider=null;observedSession=null;
                Capture(graphics,output,"companion-live-creation-color");
                Click((UIElement)Get(creator,"_charInfoCategoryButton"),graphics);Click((UIElement)Get(creator,"_charName"),graphics);
                var keyboard=Main.InGameUI.CurrentState;Require(keyboard is UIVirtualKeyboard,"real naming click opens native keyboard");Capture(graphics,output,"companion-live-name-keyboard");
                var cancel=(UIElement)Get(keyboard,"_cancelButton");var point=cancel.GetDimensions().Center();Step(point,false,graphics);Step(point,true,graphics);
                Set(input,"foregroundWindow",(Func<IntPtr>)(()=>new IntPtr(2)));Step(point,false,graphics);Require(ReferenceEquals(Main.InGameUI.CurrentState,keyboard),"focus-neutral release cannot cancel/submit the native keyboard");
                Set(input,"foregroundWindow",foreground);Step(Vector2.Zero,false,graphics);Step(Vector2.Zero,false,graphics);Click(cancel,graphics);Require(ReferenceEquals(Main.InGameUI.CurrentState,creator),"trusted native keyboard cancel returns to same creator");
                Click((UIElement)Get(creator,"_charName"),graphics);Require(Main.InGameUI.CurrentState is UIVirtualKeyboard,"native naming can reopen before mapped inventory return");
                inventoryBindings.Clear();inventoryBindings.Add(Microsoft.Xna.Framework.Input.Keys.Tab.ToString());
                NativeFrame(context,Vector2.Zero,false,new[]{Microsoft.Xna.Framework.Input.Keys.Tab});Require(ReferenceEquals(Main.InGameUI.CurrentState,creator),"actual remapped Inventory reaches owner.ToggleInv/Close and returns keyboard to creator");
                NativeFrame(context,Vector2.Zero,false,new[]{Microsoft.Xna.Framework.Input.Keys.Tab});Require(ReferenceEquals(Main.InGameUI.CurrentState,creator),"same held Inventory action is accepted only once across native page return");
                NativeFrame(context,Vector2.Zero,false,new Microsoft.Xna.Framework.Input.Keys[0]);NativeFrame(context,Vector2.Zero,false,new[]{Microsoft.Xna.Framework.Input.Keys.Tab});Require(Session(host)==null&&(bool)Get(window,"Visible"),"fresh Inventory press exits creator to same manager");
                NativeFrame(context,Vector2.Zero,false,new[]{Microsoft.Xna.Framework.Input.Keys.Tab});Require(!PlayerInput.Triggers.Current.Inventory&&!Main.playerInventory,"closing Inventory key tail cannot reopen human inventory");NativeFrame(context,Vector2.Zero,false,new Microsoft.Xna.Framework.Input.Keys[0]);
                managerClick("create");creator=(UICharacterCreation)Main.InGameUI.CurrentState;
                Click(Snap(creator,"Create",0),graphics);Require(Main.InGameUI.CurrentState is UIVirtualKeyboard,"actual empty-name Create enters native keyboard");
                Step(Vector2.Zero,false,graphics);const string name="输入样本";Main.keyCount=name.Length;for(int i=0;i<name.Length;i++){Main.keyInt[i]=name[i];Main.keyString[i]=name[i].ToString();}
                graphics.Render(()=>Main.InGameUI.Draw(Main.spriteBatch,new GameTime(clock,TimeSpan.FromMilliseconds(16))),Main.UIScaleMatrix);Require(((UIVirtualKeyboard)Main.InGameUI.CurrentState).Text==name,"original keyboard consumes controlled native Chinese text queue");
                Click((UIElement)Get(Main.InGameUI.CurrentState,"_submitButton"),graphics);Require(Session(host)==null&&(bool)Get(window,"Visible"),"actual keyboard submit creates once and exits to management");
                var id=(Guid)Get(host,"LastCreatedId");NativeQuickItemChecks.Until(()=>{Call(host,"Update",(ulong)Main.GameUpdateCount);return (bool)Call(archive,"IsSaved",id);});
                var body=(Player)Call(host,"GetBody",id);graphics.LoadItemTextures(body.inventory.Where(v=>v!=null&&!v.IsAir).Select(v=>v.type));Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(1366,900));
                NativeFrame(context,Vector2.Zero,false,new Microsoft.Xna.Framework.Input.Keys[0]);managerClick("actor-state");Require((bool)Call(host,"IsJoined",id)&&(string)Get(host,"Feedback")!="正在加入人物。","backpack embedded state joins and actual ready activation retires joining progress");
                NativeCompanionFeedbackChecks.Run(host,id);managerClick("team:4");Require(((Player)Call(host,"GetBody",id)).team==4,"backpack native team glyph updates AI's real team");
                NativeFrame(context,Vector2.Zero,false,new Microsoft.Xna.Framework.Input.Keys[0]);
                graphics.Image(Path.Combine(output,"companion-live-management.png"),()=>Call(window,"Draw"),Main.UIScaleMatrix,1366,900);
                Require(playerFiles==0&&ReferenceEquals(Main.PendingPlayer,pending)&&Main.player[Main.myPlayer]==human,"live native page never enters human file save/PendingPlayer ownership");
                NativeCompanionUiChecks.StatusPressIdentity(shell,id);Call(shell,"OpenCompanions",Get(Get(Get(shell,"State"),"Layout"),"Viewport"));Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(1366,900));
                managerClick("actor-state");NativeQuickItemChecks.Until(()=>{Call(host,"Update",(ulong)Main.GameUpdateCount);return !(bool)Call(host,"IsJoined",id);});
                managerClick("create");creator=(UICharacterCreation)Main.InGameUI.CurrentState;Click(Snap(creator,"Back",0),graphics);Require(Session(host)==null&&(bool)Get(window,"Visible"),"actual native Back exits without world replay");
                Require(Main.player.SequenceEqual(ownerArray),"after actual recall, native UI keeps all human/other player array identities");
                Console.WriteLine("PASS true native early-UI -> mapping/final sample -> production context dispatch, physical UI1.25/worldZoom1.5, real plus/category/held-slider/keyboard cancel-submit/Back, world CopyInto+ItemCheck and neutral tails, embedded state/team and feedback; no OS IME/installed-game claim.");
            }
            finally {proveWorldUse=false;observedSlider=null;observedSession=null;inventoryBindings.Clear();inventoryBindings.AddRange(originalInventoryKeys);Set(input,"foregroundWindow",foreground);var s=Session(host);if(s!=null)Call(s,"Cancel");Call(window,"Close");guard.UnpatchAll(guard.Id);human.inventory[0]=item;human.selectedItemState.Select(selected);human.name=ownerName;Main.GameViewMatrix.Zoom=zoom;currentContext=null;NativeToolsUiChecks.Mouse(Vector2.Zero,false);}
        }
        internal static void Run(object context,ProbeGraphics graphics,string output)
        {
            currentContext=context;var host=Get(context,"Companions");var window=Get(Get(context,"Shell"),"CompanionUi");
            var originalPlayer=Main.ActivePlayerFileData.Player;var originalArray=(Player[])Main.player.Clone();
            var pending=Main.PendingPlayer;var originalHair=Main.Hairstyles;var originalRng=Main.rand;int originalShader=Mount.currentShader;
            var dirty=typeof(UICharacterCreation).GetField("dirty",BindingFlags.NonPublic|BindingFlags.Static);object originalDirty=dirty.GetValue(null);
            var guard=new Harmony("JueMingR.NativeCompanionCreation.FileGuard");playerFiles=0;
            guard.Patch(AccessTools.Method(typeof(PlayerFileData),"CreateAndSave"),prefix:new HarmonyMethod(typeof(NativeCompanionNativeCreationChecks),nameof(RejectPlayerFile)));
            try
            {
                // The caller has just used the production plus control, including
                // its native focus/physical tail handling and management close.
                var session=Session(host);Require(session!=null&&Main.InGameUI.CurrentState is UICharacterCreation,"plus opens native creator, not a parallel text-form creator");
                var creator=(UICharacterCreation)Get(session,"Creator");var draft=(Player)Get(session,"Draft");
                byte desired=((Player)Call(host,"NewCreationDraft")).difficulty;
                Require(draft.whoAmI==255&&draft.difficulty==desired&&draft.miscEquips.All(i=>i.IsAir)&&!Main.player.Contains(draft),"native constructor preserves world default difficulty and owns an inventory-free detached draft");
                Require(Descendants((UIElement)Get(creator,"_hairstylesContainer")).OfType<UIHairStyleButton>().Count()==((IEnumerable)Call(host,"GetCreationHairstyles")).Cast<int>().Count(),"native constructor really built the full original creation hair grid before any category click");
                Capture(graphics,output,"companion-native-creation-info");CheckNativeBody(creator,draft,graphics,output);
                int oldWidth=Main.screenWidth,oldHeight=Main.screenHeight;float oldScale=Main.UIScale;
                Main.screenWidth=840;Main.screenHeight=640;PlayerInput.CacheOriginalScreenDimensions();Main.UIScale=1.5f;
                Step(Vector2.Zero,false,graphics);Capture(graphics,output,"companion-native-creation-small");
                Click((UIElement)Get(creator,"_clothingStylesCategoryButton"),graphics);Require(Convert.ToInt32(Get(creator,"_selectedPicker"))==1,"original category hit target follows outer layout in small native viewport");
                var createRect=Snap(creator,"Create",0).GetDimensions();var logicalViewport=Main.InGameUI.GetDimensions();
                Require(createRect.Y>=0&&createRect.Y+createRect.Height<=logicalViewport.Height,"original actions remain visible at the actual native accepted scale");
                Console.WriteLine("NATIVE-CREATOR small requestedScale=1.5 accepted="+Main.UIScale+" logical="+logicalViewport.Width+"x"+logicalViewport.Height);
                Main.screenWidth=oldWidth;Main.screenHeight=oldHeight;PlayerInput.CacheOriginalScreenDimensions();Main.UIScale=oldScale;
                Step(Vector2.Zero,false,graphics);Click((UIElement)Get(creator,"_charInfoCategoryButton"),graphics);
                var expectedRng=new Terraria.Utilities.UnifiedRandom(46131);Main.rand=new Terraria.Utilities.UnifiedRandom(46131);Mount.currentShader=71;
                int dust=Main.dust.Count(d=>d!=null&&d.active),gore=Main.gore.Count(g=>g!=null&&g.active),afk=originalPlayer.afkCounter;
                var closedOwnerPet=Get(Get(window,"ownerPreview"),"Pet");var closedAiPet=Get(Get(window,"companionPreview"),"Pet");
                int closedOwnerDraws=(int)Get(closedOwnerPet,"Draws"),closedAiDraws=(int)Get(closedAiPet,"Draws");
                for(int i=0;i<8;i++) {Main.mouseLeft=false;Main.mouseX=Main.mouseY=0;Main.InGameUI.Update(new GameTime(clock,TimeSpan.FromMilliseconds(16)));graphics.Render(()=>Main.InGameUI.Draw(Main.spriteBatch,new GameTime(clock,TimeSpan.FromMilliseconds(16))),Main.UIScaleMatrix);}
                Require(Main.rand.Next()==expectedRng.Next()&&Mount.currentShader==71&&Main.dust.Count(d=>d!=null&&d.active)==dust&&Main.gore.Count(g=>g!=null&&g.active)==gore&&originalPlayer.afkCounter==afk,"stable original preview leaves world RNG/particles/shared mount shader and owner AFK unchanged");
                Require(GetOptional(closedOwnerPet,"Dummy")==null&&GetOptional(closedAiPet,"Dummy")==null&&(int)Get(closedOwnerPet,"Draws")==closedOwnerDraws&&(int)Get(closedAiPet,"Draws")==closedAiDraws,"closed management clears both private pet dummies and performs zero pet drawing during native creation");
                foreach(byte difficulty in new byte[]{0,1,2,3}) {Click(Descendants(creator).OfType<UIDifficultyButton>().Single(b=>(byte)Get(b,"_difficulty")==difficulty),graphics);Require(draft.difficulty==difficulty,"native difficulty control changes detached draft "+difficulty);}
                Click(Descendants(creator).OfType<UIDifficultyButton>().Single(b=>(byte)Get(b,"_difficulty")==desired),graphics);
                Click((UIElement)Get(creator,"_clothingStylesCategoryButton"),graphics);
                Click(Descendants(creator).OfType<UIClothStyleButton>().Single(b=>b.ClothStyleId==9),graphics);
                Require(draft.skinVariant==9,"real original cloth control updates native skinVariant");Capture(graphics,output,"companion-native-creation-clothes");
                Click((UIElement)Get(creator,"_randomizePlayerButton"),graphics);
                Require(draft.difficulty==desired&&String.IsNullOrEmpty(draft.name),"native random appearance preserves difficulty and unfinished empty name");
                Click((UIElement)Get(creator,"_hairStylesCategoryButton"),graphics);
                Click(Descendants(creator).OfType<UIHairStyleButton>().First(b=>b.HairStyleId==1),graphics);
                Require(draft.hair==1,"actual native hairstyle click is accepted");Capture(graphics,output,"companion-native-creation-hair");
                var pickers=(UIColoredImageButton[])Get(creator,"_colorPickers");
                foreach(int category in Enumerable.Range(3,7))
                {
                    Click(pickers[category],graphics);
                    var slider=Descendants(creator).OfType<UIColoredSlider>().First();var rect=slider.GetDimensions();
                    // The native value bar is right-anchored 10px inside the
                    // control. Actual Draw consumes the locked slider gesture.
                    Vector2 point=new Vector2(rect.X+rect.Width-10-TextureWidth("ColorBar")+130,rect.Y+18);
                    Step(point,false,graphics);Step(point,true,graphics);Step(point,false,graphics);
                }
                Capture(graphics,output,"companion-native-creation-color");
                Click((UIElement)Get(creator,"_clothingStylesCategoryButton"),graphics);
                int voice=draft.voiceVariant;Click((UIElement)Get(creator,"_voiceNext"),graphics);Require(draft.voiceVariant!=voice,"original voice control edits voice variant");
                float oldPitch=draft.voicePitchOffset;var pitchRect=((UIElement)Get(creator,"_pitchSlider")).GetDimensions();var pitchPoint=new Vector2(pitchRect.X+pitchRect.Width-10-TextureWidth("ColorBar")+130,pitchRect.Y+18);
                Step(pitchPoint,false,graphics);Step(pitchPoint,true,graphics);Step(pitchPoint,false,graphics);Require(Math.Abs(draft.voicePitchOffset-oldPitch)>.01f&&draft.voicePitchOffset>=-1&&draft.voicePitchOffset<=1,"real native pitch slider edits bounded voice pitch");
                graphics.LoadTexture("ArmorHead","Images/Armor_Head_3",3);graphics.LoadTexture("ArmorBodyComposite","Images/Armor/Armor_3",3);graphics.LoadTexture("ArmorLeg","Images/Armor_Legs_3",3);
                var previewButtons=(UIElement[])Get(creator,"_previewArmorButton");Click(previewButtons[2],graphics);
                Require(draft.armor[0].type==91&&draft.armor[1].type==82&&draft.armor[2].type==78,"native Silver preview genuinely equips only the detached draft's display armor");
                Capture(graphics,output,"companion-native-creation-voice");
                Click((UIElement)Get(creator,"_charInfoCategoryButton"),graphics);
                Click((UIElement)Get(creator,"_charName"),graphics);
                Require(Main.InGameUI.CurrentState is UIVirtualKeyboard,"original name button opens original ingame virtual keyboard");Capture(graphics,output,"companion-native-name-keyboard");
                UIVirtualKeyboard.Cancel();Require(ReferenceEquals(Main.InGameUI.CurrentState,creator)&&ReferenceEquals(draft,Get(session,"Draft")),"cancel native naming returns to the same draft/creator");
                Click(Snap(creator,"Create",0),graphics);
                Require(Main.InGameUI.CurrentState is UIVirtualKeyboard,"empty-name Create enters the same native keyboard");
                NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false,new Microsoft.Xna.Framework.Input.Keys[0]);
                const string name="原生创建样本";
                Main.keyCount=name.Length;for(int i=0;i<name.Length;i++){Main.keyInt[i]=name[i];Main.keyString[i]=name[i].ToString();}
                graphics.Render(()=>Main.InGameUI.Draw(Main.spriteBatch,new GameTime(clock,TimeSpan.FromMilliseconds(16))),Main.UIScaleMatrix);
                Require(((UIVirtualKeyboard)Main.InGameUI.CurrentState).Text==name&&Main.keyCount==0,"original native text consumer receives Chinese queue and consumes it once");
                var expected=new[]{draft.hairColor,draft.eyeColor,draft.skinColor,draft.shirtColor,draft.underShirtColor,draft.pantsColor,draft.shoeColor};int hair=draft.hair,variant=draft.skinVariant,voiceVariant=draft.voiceVariant;float pitch=draft.voicePitchOffset;
                var input=Get(context,"Input");var foreground=Get(input,"foregroundWindow");var keyboard=Main.InGameUI.CurrentState;
                var submit=(UIElement)Get(keyboard,"_submitButton");var submitPoint=submit.GetDimensions().Center();
                Step(submitPoint,false,graphics);Step(submitPoint,true,graphics);
                try {Set(input,"foregroundWindow",(Func<IntPtr>)(()=>new IntPtr(2)));Step(submitPoint,false,graphics);Require(ReferenceEquals(Main.InGameUI.CurrentState,keyboard)&&ReferenceEquals(Session(host),session),"focus-loss neutral release retires native submit press while preserving the same keyboard and draft");}
                finally {Set(input,"foregroundWindow",foreground);}
                Step(Vector2.Zero,false,graphics);Step(Vector2.Zero,false,graphics);
                Require(ReferenceEquals(Main.InGameUI.CurrentState,keyboard),"reactivation never replays the old native submit release");
                int before=((IEnumerable)Get(host,"Profiles")).Cast<object>().Count();UIVirtualKeyboard.Submit();
                Require(Session(host)==null&&!Main.inFancyUI&&(bool)Get(window,"Visible"),"native naming submission completes through AI outlet and returns management");
                var id=(Guid)Get(host,"LastCreatedId");
                Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(Main.screenWidth,Main.screenHeight));
                graphics.Image(Path.Combine(output,"companion-native-created-pending.png"),()=>Call(window,"Draw"),Main.UIScaleMatrix,Main.screenWidth,Main.screenHeight);
                Require((Guid)Get(window,"selected")==id,"immediate management Prepare before Host.Update preserves accepted pending identity instead of falling back to the old profile");
                var finished=typeof(UICharacterCreation).GetMethod("FinishCreatingCharacter",BindingFlags.NonPublic|BindingFlags.Instance);
                var menuState=Main.MenuUI.CurrentState;int menuMode=Main.menuMode;
                finished.Invoke(creator,null);Call(creator,"HandleBackButtonUsage");Call(creator,"Click_GoBack",null,null);
                Require(playerFiles==0&&ReferenceEquals(Main.MenuUI.CurrentState,menuState)&&Main.menuMode==menuMode&&(Guid)Get(host,"LastCreatedId")==id,"retired exact native creator confirmation/back callbacks stay inert before ordinary file/menu exits");
                var archive=Get(host,"profiles");NativeQuickItemChecks.Until(()=>{Call(host,"Update",(ulong)Main.GameUpdateCount);return (bool)Call(archive,"IsSaved",id);});
                Require(((IEnumerable)Get(host,"Profiles")).Cast<object>().Count()==before+1,"first actual worker publication retains exactly the one accepted identity");
                Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(Main.screenWidth,Main.screenHeight));Require((Guid)Get(window,"selected")==id,"first publication keeps new profile selected");
                var saved=(Player)Call(host,"GetBody",id);Require(saved.name==name&&saved.difficulty==desired&&saved.hair==hair&&saved.skinVariant==variant&&saved.voiceVariant==voiceVariant&&Math.Abs(saved.voicePitchOffset-pitch)<.001f&&new[]{saved.hairColor,saved.eyeColor,saved.skinColor,saved.shirtColor,saved.underShirtColor,saved.pantsColor,saved.shoeColor}.SequenceEqual(expected),"native edited appearance, seven colors, voice and difficulty are in the actual AI native archive");
                Require(saved.armor.Take(3).All(i=>i.IsAir),"original preview armor never enters the newly initialized AI archive");
                // The outer quick-item scope intercepts only recall Spawns.
                // This step intentionally enters the real AI Spawn, matching
                // the established creation/session fixtures, so retire that prefix.
                new Harmony("JueMingR.Tests.QuickItemOutlets").Unpatch(AccessTools.Method(typeof(Player),"Spawn"),HarmonyPatchType.Prefix,"JueMingR.Tests.QuickItemOutlets");
                foreach(var method in new[]{AccessTools.Method(typeof(Player),"Spawn"),AccessTools.Method(host.GetType(),"Spawn")})
                    guard.Patch(method,finalizer:new HarmonyMethod(typeof(NativeCompanionNativeCreationChecks),nameof(TraceSpawnFailure)));
                Require((bool)Call(host,"Join",id),"new native-created AI can join through the actual host: "+Get(host,"Feedback"));
                Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(Main.screenWidth,Main.screenHeight));
                graphics.Image(Path.Combine(output,"companion-native-created-joined.png"),()=>Call(window,"Draw"),Main.UIScaleMatrix,Main.screenWidth,Main.screenHeight);
                CheckAvatarSelection(window,id,graphics);CheckConfigurationRespawn(window,host,id,graphics,output);CheckStackPictures(window,host,id,graphics,output);Call(host,"Recall",id);
                NativeQuickItemChecks.Until(()=>{Call(host,"Update",(ulong)Main.GameUpdateCount);return !(bool)Call(host,"IsJoined",id);});
                Set(window,"page",1);Set(window,"bodyOffset",0);Call(window,"Invalidate");Call(window,"Prepare",Main.UIScaleMatrix,new Vector2(Main.screenWidth,Main.screenHeight));Require(RespawnLabel(window)=="","configuration clears respawn tag for actual recalled archive");
                Require(playerFiles==0&&ReferenceEquals(Main.ActivePlayerFileData.Player,originalPlayer)&&ReferenceEquals(Main.PendingPlayer,pending)&&Main.player.SequenceEqual(originalArray),"AI native UI never borrows owner/PendingPlayer/player slots or reaches ordinary player-file save");
                Require(ReferenceEquals(Main.Hairstyles,originalHair)&&Equals(dirty.GetValue(null),originalDirty),"native creator/keyboard exit restores original hairstyle helper and static dirty state");
                Console.WriteLine("PASS original ingame UICharacterCreation real Update/Draw/clicks, native keyboard Chinese queue, cancel/return, four difficulty controls, full hair grid, cloth/colors/voice, unique AI archive/Join and owner/file/global isolation; no OS IME claim.");
            }
            finally {var session=Session(host);if(session!=null)Call(session,"Cancel");guard.UnpatchAll(guard.Id);Main.rand=originalRng;Mount.currentShader=originalShader;Main.mouseLeft=false;PlayerInput.Triggers.Current.MouseLeft=false;NativeToolsUiChecks.Mouse(Vector2.Zero,false);currentContext=null;}
        }
        private static void CheckAvatarSelection(object window,Guid id,ProbeGraphics graphics)
        {
            var card=Controls(window).Single(c=>(Guid)Get(c,"ProfileId")==id);Require((bool)Get(card,"Selected")&&(bool)Get(card,"Joined"),"same avatar represents both selected and joined states");
            var rect=Get(card,"Rect");float x=(float)Get(rect,"X"),y=(float)Get(rect,"Y"),w=(float)Get(rect,"Width"),h=(float)Get(rect,"Height"),scale=Main.UIScaleMatrix.M11;
            var pixels=graphics.Pixels(()=>Call(window,"Draw"),Main.UIScaleMatrix,Main.screenWidth,Main.screenHeight);
            Func<float,float,bool> green=(px,py)=>{var c=pixels[(int)py*Main.screenWidth+(int)px];return c.R==Color.LightGreen.R&&c.G==Color.LightGreen.G&&c.B==Color.LightGreen.B;};
            float centerX=(float)Math.Round((x+w/2)*scale),centerY=(float)Math.Round((y+h/2)*scale);
            bool top=green(centerX,(float)Math.Round((y+6)*scale)+1),left=green((float)Math.Round((x+6)*scale)+1,centerY),right=green((float)Math.Round((x+w-6-2/scale)*scale)+1,centerY);
            bool oldBottom=green(centerX,(float)Math.Round((y+h-6-2/scale)*scale)+1),joined=green(centerX,(float)Math.Round((y+h-3-2/scale)*scale)+1);
            Require(top&&!left&&!right&&!oldBottom&&joined,"actual avatar pixels keep only the selected top inset line and an independent joined underline");
            Console.WriteLine("PASS avatar actual selected top-only green line and separate joined underline coexist; former left/right/bottom marker pixels absent.");
        }
        private static string RespawnLabel(object window)
        {return (string)Get(((IEnumerable)Get(window,"labels")).Cast<object>().ElementAt((int)Get(window,"respawnLabel")),"Text");}
        private static void CheckConfigurationRespawn(object window,object host,Guid id,ProbeGraphics graphics,string output)
        {
            var actor=((IDictionary)Get(host,"actors"))[id];var body=(Player)Get(actor,"Body");bool dead=body.dead,pending=(bool)Get(actor,"PermanentDeathPending");int timer=body.respawnTimer;
            var original=((IEnumerable)Get(host,"Profiles")).Cast<JueMingR.Platform.Companions.CompanionProfile>().Single(p=>p.Id==id).Behavior;
            var screen=new Vector2(Main.screenWidth,Main.screenHeight);
            try
            {
                // This bounded projection check changes only the already-tested
                // actor's real scalar predicates. It runs no KillMe/Player.Update
                // and makes no new death-lifecycle or revival claim.
                body.dead=true;body.respawnTimer=122;Set(window,"page",1);Set(window,"bodyOffset",0);Call(window,"Invalidate");Call(window,"Prepare",Main.UIScaleMatrix,screen);
                Require(RespawnLabel(window)=="复活：3秒","configuration has an independent name-side live native respawn countdown");
                var follow=Controls(window).Single(c=>((string)Get(c,"Key")).StartsWith("follow/",StringComparison.Ordinal));var point=NativeToolsUiChecks.Point(Get(follow,"Rect"));
                foreach(bool left in new[]{false,true}) {NativeToolsUiChecks.Mouse(point,left);Call(window,"Process",true,new Microsoft.Xna.Framework.Input.KeyboardState(),point,Main.UIScaleMatrix,screen,true,0);}
                body.respawnTimer=61;Call(window,"Prepare",Main.UIScaleMatrix,screen);Require(RespawnLabel(window)=="复活：2秒"&&GetOptional(window,"armedKey")!=null,"real timer text refresh leaves the valid configuration press armed");
                NativeToolsUiChecks.Mouse(point,false);Call(window,"Process",true,new Microsoft.Xna.Framework.Input.KeyboardState(),point,Main.UIScaleMatrix,screen,true,0);
                var disabled=original.Clone();disabled.AutoRespawn=false;Require((bool)Call(host,"SetBehavior",id,disabled),"existing behavior owner accepts auto-respawn preference");Call(window,"Prepare",Main.UIScaleMatrix,screen);
                Require(RespawnLabel(window)=="自动复活已关闭","configuration reflects real disabled preference");
                graphics.Image(Path.Combine(output,"companion-config-respawn-off.png"),()=>Call(window,"Draw"),Main.UIScaleMatrix,Main.screenWidth,Main.screenHeight);
                Set(actor,"PermanentDeathPending",true);Call(window,"Prepare",Main.UIScaleMatrix,screen);Require(RespawnLabel(window)=="","terminal actor predicate suppresses false configuration countdown");
                Console.WriteLine("PASS configuration live countdown/off/terminal binding and timer refresh preserves pressed control; UI projection only, no death matrix rerun.");
            }
            finally {Set(actor,"PermanentDeathPending",pending);body.dead=dead;body.respawnTimer=timer;Call(host,"SetBehavior",id,original);Set(window,"page",0);Set(window,"bodyOffset",0);Call(window,"Invalidate");Call(window,"Prepare",Main.UIScaleMatrix,screen);NativeToolsUiChecks.Mouse(Vector2.Zero,false);}
        }
        private static void CheckStackPictures(object window,object host,Guid id,ProbeGraphics graphics,string output)
        {
            var owner=Main.LocalPlayer;var original=owner.inventory[0];
            try
            {
                var fixture=new Item();fixture.SetDefaults(8);fixture.stack=4321;owner.inventory[0]=fixture;
                var screen=new Vector2(Main.screenWidth,Main.screenHeight);Call(window,"Prepare",Main.UIScaleMatrix,screen);
                graphics.Image(Path.Combine(output,"companion-native-four-digit-stack.png"),()=>Call(window,"Draw"),Main.UIScaleMatrix,Main.screenWidth,Main.screenHeight);
                var slot=Controls(window).Single(c=>((string)Get(c,"Key")).StartsWith("slot:True:Inventory:0/",StringComparison.Ordinal));var point=NativeToolsUiChecks.Point(Get(slot,"Rect"));
                foreach(bool left in new[]{false,true}) {NativeToolsUiChecks.Mouse(point,left);Call(window,"Process",true,new Microsoft.Xna.Framework.Input.KeyboardState(),point,Main.UIScaleMatrix,screen,true,0);}
                Require((bool)Get(host,"HasItemGesture")&&((Item)Get(host,"CursorItem")).stack==4321,"real left press renders the same four-digit draft cursor through native transaction preview");
                graphics.Image(Path.Combine(output,"companion-native-four-digit-cursor.png"),()=>Call(window,"Draw"),Main.UIScaleMatrix,Main.screenWidth,Main.screenHeight);
                NativeToolsUiChecks.Mouse(Vector2.Zero,false);Call(window,"Process",true,new Microsoft.Xna.Framework.Input.KeyboardState(),Vector2.Zero,Main.UIScaleMatrix,screen,true,0);
                Require(!(bool)Get(host,"HasItemGesture")&&ReferenceEquals(owner.inventory[0],fixture),"pointer leave cancels uncommitted pictured cursor without replacing actual owner source");
            }
            finally {Call(host,"CancelItemGesture");owner.inventory[0]=original;NativeToolsUiChecks.Mouse(Vector2.Zero,false);}
        }
        private static void CheckNativeBody(UICharacterCreation creator,Player draft,ProbeGraphics graphics,string output)
        {
            var original=new[]{draft.skinColor,draft.shirtColor,draft.underShirtColor,draft.pantsColor,draft.shoeColor};
            var markers=new[]{new Color(30,240,80),new Color(40,100,240),new Color(220,30,220),new Color(245,230,35)};
            try
            {
                draft.skinColor=markers[0];draft.shirtColor=draft.underShirtColor=markers[1];draft.pantsColor=markers[2];draft.shoeColor=markers[3];
                var pixels=graphics.Pixels(()=>Main.InGameUI.Draw(Main.spriteBatch,new GameTime(clock,TimeSpan.FromMilliseconds(16))),Main.UIScaleMatrix,Main.screenWidth,Main.screenHeight);
                var rect=Descendants(creator).OfType<UICharacter>().First().GetDimensions();float scale=Main.UIScaleMatrix.M11;
                int x0=Math.Max(0,(int)((rect.X-20)*scale)),x1=Math.Min(Main.screenWidth,(int)((rect.X+rect.Width+20)*scale));
                int y0=Math.Max(0,(int)((rect.Y-30)*scale)),y1=Math.Min(Main.screenHeight,(int)((rect.Y+rect.Height+10)*scale));
                int[] count=new int[4];
                for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++) {var pixel=pixels[y*Main.screenWidth+x];for(int i=0;i<4;i++)if(pixel.A>200&&Math.Abs(pixel.R-markers[i].R)<55&&Math.Abs(pixel.G-markers[i].G)<55&&Math.Abs(pixel.B-markers[i].B)<55)count[i]++;}
                Console.WriteLine("NATIVE-CREATOR-BODY face/shirt/pants/shoes="+String.Join("/",count));
                Require(count[0]>10&&count[1]>20&&count[2]>10&&count[3]>4,"actual original UICharacter contains visible face, shirt, pants and shoes");
                Capture(graphics,output,"companion-native-creation-body-pixels");
            }
            finally {draft.skinColor=original[0];draft.shirtColor=original[1];draft.underShirtColor=original[2];draft.pantsColor=original[3];draft.shoeColor=original[4];}
        }
        private static int TextureWidth(string field) {return ((ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>)typeof(Terraria.GameContent.TextureAssets).GetField(field).GetValue(null)).Value.Width;}
    }
}
