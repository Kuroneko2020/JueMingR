using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using HarmonyLib;
using JueMingR.Platform.Companions;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Settings;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionConfigurationChecks
    {
        private sealed class PreviewIme:RealProxy
        {
            internal PreviewIme(Type type):base(type) { }
            public override IMessage Invoke(IMessage message)
            {
                var call=(IMethodCallMessage)message;
                object value=call.MethodName=="get_Composition"?(object)"":call.MethodName=="get_Candidates"?false:(object)null;
                return new ReturnMessage(value,null,0,call.LogicalCallContext,call);
            }
        }
        private static IEnumerable Controls(object window) {return (IEnumerable)Get(window,"controls");}
        private static object Control(object window,string prefix)
        {return Controls(window).Cast<object>().Single(c=>((string)Get(c,"Key")).StartsWith(prefix,StringComparison.Ordinal));}
        private static Dictionary<string, (object Rect, object Row, object Pane, bool InRow, bool InPane)> toolbarProbe;

        private static void ObserveNativeTool(object __instance, string key, string text, ref float x, object toolbar, object view, float width)
        {
            if (toolbarProbe == null) return;
            if (width == 0) width = (float)Call(__instance, "ToolWidth", text);
            var rect = Activator.CreateInstance(toolbar.GetType(), BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { x, Get(toolbar, "Y"), width, Get(toolbar, "Height") }, null);
            var visible = __instance.GetType().GetMethod("ActorVisible", BindingFlags.Static | BindingFlags.NonPublic);
            toolbarProbe[key] = (rect, toolbar, view, (bool)visible.Invoke(null, new[] { rect, toolbar }),
                (bool)visible.Invoke(null, new[] { rect, view }));
        }

        private static void TraceReportedToolbar(object context, ProbeGraphics graphics, object window, string output)
        {
            var patch = new Harmony("JueMingR.Tests.ReportedToolbarGeometry");
            var probe = new HarmonyMethod(typeof(NativeCompanionConfigurationChecks), nameof(ObserveNativeTool));
            patch.Patch(AccessTools.Method(window.GetType(), "AddTool"), prefix: probe);
            var resourceSet = Main.ResourceSetsManager.ActiveSetKeyName;
            int width = Main.screenWidth, height = Main.screenHeight;
            float scale = Main.UIScale;
            try
            {
                Call(Main.ResourceSetsManager, "SetActive", "HorizontalBarsWithFullText");
                Main.screenWidth = 1920; Main.screenHeight = 1080;
                PlayerInput.CacheOriginalScreenDimensions(); Main.UIScale = 1.15f;
                toolbarProbe = new Dictionary<string, (object, object, object, bool, bool)>();
                NativeToolsUiChecks.UiFrame(context, Vector2.Zero, false);
                NativeToolsUiChecks.UiFrame(context, Vector2.Zero, false);
                var panel = Get(window, "panel");
                string Rect(object value) => String.Join(",", new[] { "X", "Y", "Width", "Height" }
                    .Select(axis => ((float)Get(value, axis)).ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
                Console.WriteLine($"TOOLBAR REPORT physical={Main.screenWidth}x{Main.screenHeight} transform={Main.UIScaleMatrix.M11:R}, panel={Rect(panel)} ownerOffset={Get(window, "ownerOffset")} aiOffset={Get(window, "bodyOffset")}");
                var pixels = graphics.Pixels(() => Call(Get(context,"Shell"), "DrawLayer"), Main.UIScaleMatrix, Main.screenWidth, Main.screenHeight);
                foreach (var key in new[] { "拿走全部", "存放全部", "快速堆叠", "补货", "weapon-select", "item-sort", "item-once", "storage-Inventory" })
                {
                    var item = toolbarProbe[key];
                    var control = Controls(window).Cast<object>().SingleOrDefault(c => ((string)Get(c, "Key")).StartsWith(key + "/", StringComparison.Ordinal));
                    int ink = 0;
                    for (int y = (int)((float)Get(item.Rect, "Y") * Main.UIScale) + 5; y < (float)Get(item.Rect, "Bottom") * Main.UIScale - 5; y++)
                        for (int x = (int)((float)Get(item.Rect, "X") * Main.UIScale) + 5; x < (float)Get(item.Rect, "Right") * Main.UIScale - 5; x++)
                        {
                            var color = pixels[y * Main.screenWidth + x];
                            if (color.R > 105 && color.G > 105 && color.B > 105 && Math.Abs(color.R - color.B) < 45) ink++;
                        }
                    Console.WriteLine($"TOOLBAR REPORT {key} candidate={Rect(item.Rect)} row={Rect(item.Row)} pane={Rect(item.Pane)} inRow={item.InRow} inPane={item.InPane} control={control != null} ink={ink}");
                    Require(item.InRow && item.InPane && control != null && ink > 5,
                        "both real culling checks, the actual control and original drawn text survive for " + key + " at scale " + Main.UIScale);
                }
                graphics.Image(Path.Combine(output, "reported-toolbar-1920-1.15.png"),
                    () => Call(Get(context,"Shell"), "DrawLayer"), Main.UIScaleMatrix, Main.screenWidth, Main.screenHeight);
                CheckToolbarActions(context, graphics, window);
            }
            finally
            {
                toolbarProbe = null; patch.UnpatchAll(patch.Id);
                Main.screenWidth = width; Main.screenHeight = height;
                PlayerInput.CacheOriginalScreenDimensions(); Main.UIScale = scale;
                Call(Main.ResourceSetsManager, "SetActive", resourceSet);
                NativeToolsUiChecks.UiFrame(context, Vector2.Zero, false);
            }
        }

        private static void CheckToolbarActions(object context, ProbeGraphics graphics, object window)
        {
            var host = Get(context, "Companions");
            var id = (Guid)Get(window, "selected");
            var owner = Main.LocalPlayer;
            var body = (Player)Call(host, "GetBody", id);
            var behavior = Profile(host, id).Behavior;
            Require(!owner.dead && !body.dead && (string)Get(Control(window, "actor-state/"), "Text") == "退出",
                "the physical 1920x1080 specimen is the same live-owner and joined-actor topology");
            void Click(string prefix) => NativeToolsUiChecks.Click(context, NativeToolsUiChecks.Point(Get(Control(window, prefix), "Rect")) * Main.UIScale);
            Click("configuration/"); Click("inventory/");
            const int sampleSlot = 41;
            var ownerItem = owner.inventory[sampleSlot]; var actorItem = body.inventory[sampleSlot];
            graphics.LoadItemTextures(new[] { (int)ItemID.Gel });
            try
            {
                owner.inventory[sampleSlot] = new Item(ItemID.Gel) { stack = 3 };
                body.inventory[sampleSlot] = new Item(ItemID.Gel) { stack = 7 };
                Click("快速堆叠/");
                Require(owner.inventory[sampleSlot].IsAir && body.inventory[sampleSlot].type == ItemID.Gel && body.inventory[sampleSlot].stack == 10,
                    "the left real visible toolbar consumes a physical click and transfers exactly the matching stack");
                Click("weapon-select/"); Click("slot:False:Inventory:10/"); Click("weapon-done/");
                Require(Profile(host, id).Behavior.MainWeapons.Single().SlotHint == 10,
                    "the right real visible toolbar selects and saves the exact AI weapon from physical input");
            }
            finally
            {
                owner.inventory[sampleSlot] = ownerItem; body.inventory[sampleSlot] = actorItem;
                Call(host, "SetBehavior", id, behavior);
            }
            Console.WriteLine("PASS after switching actual pages at 1920x1080 scale 1.15, the left and right controls take physical clicks, run quick stack and save a weapon.");
        }
        internal static CompanionProfile Profile(object host,Guid id)
        {return ((IEnumerable)Get(host,"Profiles")).Cast<CompanionProfile>().Single(p=>p.Id==id);}
        private static void Queue(string value)
        {Main.keyCount=value.Length;for(int i=0;i<value.Length;i++){Main.keyInt[i]=value[i];Main.keyString[i]=value[i].ToString();}}
        internal static void Run(object context,ProbeGraphics graphics,string output)
        {
            NativeCombatAimChecks.Initialize();Main.spawnTileX=60;Main.spawnTileY=58;Main.leftWorld=Main.topWorld=0;Main.rightWorld=Main.maxTilesX*16;Main.bottomWorld=Main.maxTilesY*16;
            for(int x=2;x<118;x++){Main.tile[x,60].active(true);Main.tile[x,60].type=TileID.Stone;}
            new Harmony("JueMingR.Tests.QuickItemOutlets").Unpatch(AccessTools.Method(typeof(Player),"Spawn"),HarmonyPatchType.Prefix,"JueMingR.Tests.QuickItemOutlets");
            NativeCompanionCoreLifecycleChecks.PrepareHuman();graphics.LoadCompanionAssets();
            var host=Get(context,"Companions");var archive=Get(host,"profiles");var shell=Get(context,"Shell");var window=Get(shell,"CompanionUi");var input=Get(context,"Input");
            NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return (bool)Get(archive,"Ready");});
            const string profileName="一二三四五六七八九十一二三四五六七八九十";
            Require((bool)Call(host,"Create",profileName),"configuration sample is a real owned profile with a legal twenty-character name");var id=(Guid)Get(host,"LastCreatedId");
            NativeQuickItemChecks.Until(()=>{Call(host,"Update",(ulong)Main.GameUpdateCount);return (bool)Call(archive,"IsSaved",id);});
            Require((bool)Call(host,"Create","另一位队友"),"object switching has a second actual owned identity");var other=(Guid)Get(host,"LastCreatedId");
            NativeQuickItemChecks.Until(()=>{Call(host,"Update",(ulong)Main.GameUpdateCount);return (bool)Call(archive,"IsSaved",other);});
            Require((bool)Call(host,"Join",id),"configuration status sample joins through actual Host: "+Get(host,"Feedback"));
            var body=(Player)Call(host,"GetBody",id);
            body.inventory[10].SetDefaults(ItemID.SDMG);body.inventory[29].SetDefaults(ItemID.WoodenBow);
            body.inventory[48].SetDefaults(ItemID.CopperBroadsword);body.inventory[49].SetDefaults(ItemID.DirtBlock);
            foreach(var npc in Main.npc)npc.active=false;
            var text=Get(window,"textInput");var imeField=text.GetType().GetField("ime",BindingFlags.Instance|BindingFlags.NonPublic);var originalIme=imeField.GetValue(text);
            var owner=Main.LocalPlayer;string ownerName=owner.name;
            var matrix=Matrix.Identity;var screen=new Vector2(1280,800);var neutral=new Keys[0];bool projected=false;
            Action prepare=()=>Call(window,"Prepare",matrix,screen);
            Action<Vector2,bool,Keys[],int> frame=(point,left,keys,wheel)=>{
                if(!projected)NativeToolsUiChecks.UiFrame(context,point*matrix.M11,left,keys,wheel);
                else {NativeToolsUiChecks.Mouse(point,left);Call(window,"BeforeInput",true);Call(window,"Process",true,new KeyboardState(keys),point,matrix,screen,true,wheel);}
            };
            Action rest=()=>frame(Vector2.Zero,false,neutral,0);
            Action<string> click=prefix=>{
                var point=NativeToolsUiChecks.Point(Get(Control(window,prefix),"Rect"));
                frame(point,false,neutral,0);frame(point,true,neutral,0);frame(point,false,neutral,0);
                PlayerInput.Triggers.Current.CopyInto(owner);Require(!owner.controlUseItem&&!owner.controlUseTile&&!Main.mouseLeft,"configuration click has no native world-use pulse");
            };
            Action<string> capture=name=>graphics.Image(Path.Combine(output,name+".png"),()=>Call(window,"Draw"),matrix,(int)screen.X,(int)screen.Y);
            Action<string> image=name=>{rest();capture(name);};
            Action<string> scrollTo=prefix=>{
                int budget=40;
                while(!Controls(window).Cast<object>().Any(c=>((string)Get(c,"Key")).StartsWith(prefix,StringComparison.Ordinal))) {
                    Require(budget-->0&&(int)Get(window,"bodyOffset")<(int)Get(window,"bodyMaximum"),"requested configuration control is scroll reachable: "+prefix);
                    frame(NativeToolsUiChecks.Point(Get(window,"configurationViewport")),false,neutral,-120);
                }
            };
            Action<string> scrollInventoryTo=prefix=>{
                int budget=30;
                while(!Controls(window).Cast<object>().Any(c=>((string)Get(c,"Key")).StartsWith(prefix,StringComparison.Ordinal)))
                {
                    Require(budget-->0&&(int)Get(window,"bodyOffset")<(int)Get(window,"bodyMaximum"),"requested item slot is scroll reachable: "+prefix);
                    frame(NativeToolsUiChecks.Point(Get(window,"companionView")),false,neutral,-120);
                }
            };
            try
            {
                imeField.SetValue(text,new PreviewIme(imeField.FieldType).GetTransparentProxy());owner.name="验证主人";
                var otherBody=(Player)Call(host,"GetBody",other);
                var itemTypes=owner.inventory.Concat(body.inventory).Concat(otherBody.inventory).Where(item=>item!=null&&!item.IsAir).Select(item=>item.type).Distinct().ToArray();
                NativeCompanionSupplyMenuChecks.LoadIcons(graphics,host);
                graphics.LoadItemTextures(itemTypes);Console.WriteLine("CONFIG-ASSETS actual inventory types="+String.Join(",",itemTypes));
                Main.screenWidth=(int)screen.X;Main.screenHeight=(int)screen.Y;PlayerInput.CacheOriginalScreenDimensions();Main.UIScale=1;
                CheckWorldNames(graphics, output, body);
                var renderer=Get(shell,"renderer");Call(renderer,"RefreshResources");Call(renderer,"Prepare",Get(shell,"State"),screen.X,screen.Y,1f);
                Set(window,"selected",id);Call(shell,"OpenCompanions",Get(Get(Get(shell,"State"),"Layout"),"Viewport"));rest();rest();
                TraceReportedToolbar(context, graphics, window, output);
                graphics.LoadItemTextures(new[] { (int)ItemID.WormholePotion });
                CheckBackpackTeleport(context, window, host, id, body, click, rest);
                var panel=Get(window,"panel");
                var leftTools=new[]{"拿走全部","存放全部","快速堆叠","补货"};
                var toolbar=Get(Control(window,"weapon-select/"),"Rect");
                foreach(string name in leftTools.Concat(new[]{"item-sort","item-once","storage-Inventory"}))
                    Require((float)Get(Get(Control(window,name+"/"),"Rect"),"Y")== (float)Get(toolbar,"Y") &&
                        (float)Get(Get(Control(window,name+"/"),"Rect"),"Height")== (float)Get(toolbar,"Height"),
                        "both sets of actual inventory buttons share one tool baseline");
                NativeCompanionSlotVisualChecks.Run(context, window, host, id, graphics, output, matrix, screen);
                image("companion-inventory-tools");
                click("storage-Inventory/");
                Require(Get(window,"panel").Equals(panel) &&
                    !Controls(window).Cast<object>().Any(c=>((string)Get(c,"Key")).StartsWith("slot:False:PiggyBank:",StringComparison.Ordinal)),
                    "cycling to a bank without a nearby entry keeps the panel and displays no false empty bank slots");
                CheckBankTrash(window,host,body,click,frame,prepare);
                click("item-once/");
                Require(GetOptional(window,"onceSelection")!=null&&GetOptional(window,"weaponDraft")==null&&Get(window,"storage").ToString()=="Inventory",
                    "once mode goes back to the main inventory without a weapon draft");
                click("once-cancel/");
                click("configuration/");
                Require(Get(window,"panel").Equals(panel) &&
                    Controls(window).Cast<object>().Count(c=>GetOptional(c,"HotkeyId")!=null)==11 &&
                    !Controls(window).Cast<object>().Any(c=>((string)Get(c,"Key")).StartsWith("config-tab:",StringComparison.Ordinal)),
                    "ten toggle shortcut entries and the separate recall shortcut share the fixed backpack envelope");
                Require(!Controls(window).Cast<object>().Any(c=>((string)Get(c,"Key")).StartsWith("actor-state/",StringComparison.Ordinal)||((string)Get(c,"Key")).StartsWith("team:",StringComparison.Ordinal)),"team and membership actions stay on the backpack page");
                image("companion-config-behavior");
                click("configuration-fishing/");
                Require(Get(window,"panel").Equals(panel) &&
                    Controls(window).Cast<object>().Count(c=>GetOptional(c,"HotkeyId")!=null)==6,
                    "six actual fishing bindings render inside the same companion envelope");
                image("companion-config-fishing");
                click("configuration-general/");
                CheckAutoTeleportPopup(window, host, input, id, other, click, frame, image);
                int builds=(int)Get(window,"LayoutBuilds");for(int i=0;i<100;i++)prepare();Require((int)Get(window,"LayoutBuilds")==builds,"stable configuration prepares rebuild no controls");
                CheckHotkeyOwnership(context, window, host, shell, id, other, click, rest,
                    () => graphics.Image(Path.Combine(output,"companion-independent-hotkey-popup.png"),
                        () => Call(shell,"DrawLayer"),matrix,(int)screen.X,(int)screen.Y));
                NativeCompanionHotkeyFeedbackChecks.Run(context, window, host, id, other, click, rest, graphics, output);
                click("config-Attack/关闭/");Require(!Profile(host,id).Behavior.AttackEnabled&&Profile(host,id).Behavior.Follow,"one explicit switch edits its real field and preserves neighbors");
                click("config-Follow/关闭/");Require(!Profile(host,id).Behavior.Follow,"real follow setting preserves stationary behavior");
                click("config-Respawn/关闭/");Require(!Profile(host,id).Behavior.AutoRespawn,"real auto-respawn setting uses its existing owner");
                var supplyMenus = new NativeCompanionSupplyMenuChecks(window, host, id, frame, click, prepare, capture);
                supplyMenus.CheckWide(body);
                scrollTo("request-reset/");
                long beforeConfirmation=Profile(host,id).DataEpoch;click("request-reset/");scrollTo("cancel-reset/");
                click("cancel-reset/");Require(Profile(host,id).DataEpoch==beforeConfirmation,"confirmation cancellation performs no archive reset");
                long epoch=Profile(host,id).DataEpoch;
                click("inventory/");click("weapon-select/");
                var labelText=new Func<string>(()=>(string)Get(((IEnumerable)Get(window,"labels")).Cast<object>().ElementAt((int)Get(window,"weaponCountLabel")),"Text"));
                Require(labelText()=="已选 0/2","zero-weapon draft shows automatic selection");image("companion-weapon-zero");
                string firstSlot="slot:False:Inventory:10/",secondSlot="slot:False:Inventory:29/",thirdSlot="slot:False:Inventory:48/",materialSlot="slot:False:Inventory:49/";
                scrollInventoryTo(firstSlot);
                int held=body.selectedItem;var gun=body.inventory[10];var bow=body.inventory[29];
                var slotPoint=NativeToolsUiChecks.Point(Get(Control(window,firstSlot),"Rect"));
                frame(slotPoint,false,neutral,0);frame(slotPoint,true,new[]{Keys.LeftShift,Keys.LeftControl},0);frame(slotPoint,false,neutral,0);
                Require(labelText()=="已选 1/2"&&ReferenceEquals(gun,body.inventory[10])&&!(bool)Get(host,"HasItemGesture"),
                    "selection owns modified clicks without transfer, trash or a cursor item");
                scrollInventoryTo(secondSlot);click(secondSlot);Require(labelText()=="已选 2/2","two independent real weapons remain admissible without ammo");image("companion-weapon-two");
                scrollInventoryTo(thirdSlot);click(thirdSlot);Require(labelText()=="已选 2/2"&&((string)Get(window,"notice")).Contains("两把"),"a third choice leaves the first two in place");
                Require(!(bool)Get(Control(window,materialSlot),"Enabled"),"a material is never selectable");
                var done=NativeToolsUiChecks.Point(Get(Control(window,"weapon-done/"),"Rect"));
                frame(done,false,neutral,0);frame(done,true,neutral,0);long beforeSave=Profile(host,id).Revision;
                Call(host,"CaptureActorState",Call(host,"Owned",body),null,null);
                Require(Profile(host,id).Revision>beforeSave&&GetOptional(window,"weaponDraft")!=null,"ordinary background revision leaves the active draft intact");
                frame(done,false,neutral,0);
                Require(Profile(host,id).Behavior.MainWeapons.Select(w=>w.SlotHint).SequenceEqual(new[]{10,29}) &&
                    ReferenceEquals(gun,body.inventory[10])&&ReferenceEquals(bow,body.inventory[29])&&body.selectedItem==held&&GetOptional(window,"weaponDraft")==null,
                    "release after a background save commits precisely the two source items and leaves the active hand alone");
                click("weapon-select/");click(secondSlot);click("weapon-cancel/");
                Require(Profile(host,id).Behavior.MainWeapons.Length==2&&GetOptional(window,"weaponDraft")==null,"cancel keeps the previously saved pair");
                click("weapon-select/");click("profile:"+other+"/");
                Require(GetOptional(window,"weaponDraft")==null&&(Guid)Get(window,"selected")==other,"changing the selected character retires the whole draft");click("profile:"+id+"/");
                click("configuration/");scrollTo("restore-behavior/");click("restore-behavior/");var restored=Profile(host,id).Behavior;
                Require(restored.Follow&&restored.AttackEnabled&&restored.AutoRespawn&&restored.MainWeapons.Length==0&&restored.AllowedGrowthTypes.Length==0&&
                    restored.HealthRecoveryMode==CompanionHealthRecoveryMode.Smart&&restored.ManaRecoveryEnabled&&restored.AllowNurse&&!restored.FurnitureEnabled&&
                    restored.BuffEnabled&&restored.GrowthEnabled&&restored.AllowedBuffTypes.Length==0&&Profile(host,id).DataEpoch==epoch,
                    "restore resets the full configuration while preserving character archive identity");
                click("configuration/");
                CheckPressIdentity(window,host,id,body,matrix,screen);
                // Vanilla clamps its real UI scale. Independently exercise this
                // window's explicit narrow-matrix contract with direct window
                // input; this is not an accepted native scale/OS-input claim.
                screen=new Vector2(840,560);matrix=Matrix.CreateScale(1.5f);projected=true;Main.screenWidth=840;Main.screenHeight=560;PlayerInput.CacheOriginalScreenDimensions();Main.UIScale=1;prepare();
                var nameRect=Get(window,"configurationNameRect");
                Require(Terraria.GameContent.FontAssets.MouseText.Value.MeasureString(profileName).X*.85f>(float)Get(nameRect,"Width"),"the existing measured text renderer must ellipsize this actual twenty-character narrow name");
                frame(NativeToolsUiChecks.Point(nameRect),false,neutral,0);var nameHint=Get(window,"hint");Require((bool)Get(nameHint,"Visible")&&String.Concat(((IEnumerable)Get(nameHint,"Lines")).Cast<object>().Select(line=>(string)Get(line,"Text")))==profileName,"narrow name keeps its complete original through the existing hint path");
                graphics.Image(Path.Combine(output,"companion-config-narrow-name.png"),()=>Call(window,"Draw"),matrix,(int)screen.X,(int)screen.Y);
                scrollTo("config-Buff/配置/");click("config-Buff/配置/");prepare();
                var narrowPanel=Get(window,"panel");
                supplyMenus.CheckNarrow(screen, matrix);
                scrollTo("request-reset/");click("request-reset/");scrollTo("cancel-reset/");
                image("companion-config-narrow-reset");click("cancel-reset/");Require(Profile(host,id).DataEpoch==epoch&&!(bool)Get(window,"confirmingReset")&&
                    Get(window,"panel").Equals(narrowPanel)&&(int)Get(window,"bodyOffset")<=(int)Get(window,"bodyMaximum"),"confirmation cancellation clamps the body without resizing or resetting");
                // Confirm through the actual button on the same real identity.
                body.hairColor=new Color(65,123,212); body.voicePitchOffset=.25f;
                body.inventory[10].SetDefaults(ItemID.SDMG); body.armor[0].SetDefaults(ItemID.CopperHelmet);
                body.statLifeMax=180; body.statManaMax=60;
                var custom=Profile(host,id).Behavior; custom.Follow=false; custom.BuffEnabled=false;
                Require((bool)Call(host,"SetBehavior",id,custom),"the real Reset has non-default gear, growth, appearance and AI settings to compare");
                var customProfile=Profile(host,id); var customBody=(Player)Call(archive,"LoadBody",customProfile);
                scrollTo("request-reset/");Require((string)Get(Control(window,"request-reset/"),"Text")=="清空配置","the original reset now has the approved entry label");
                click("request-reset/");scrollTo("confirm-reset/");
                Require((string)Get(Control(window,"confirm-reset/"),"Text")=="确认清空配置","the reset confirmation uses the new label");
                click("confirm-reset/");
                NativeQuickItemChecks.Until(()=>{Call(host,"Update",(ulong)Main.GameUpdateCount);return !(bool)Call(host,"IsJoined",id)&&Profile(host,id).DataEpoch==epoch+1&&(bool)Call(archive,"IsSaved",id);});
                var cleared=Profile(host,id); var clearedBody=(Player)Call(host,"GetBody",id);
                Require(cleared.Id==id && cleared.OwnerId==customProfile.OwnerId && cleared.Name==profileName &&
                    clearedBody.hairColor==customBody.hairColor && clearedBody.voiceVariant==customBody.voiceVariant &&
                    clearedBody.voicePitchOffset==customBody.voicePitchOffset && clearedBody.difficulty==customBody.difficulty &&
                    clearedBody.inventory[10].IsAir && clearedBody.armor[0].IsAir && clearedBody.statLifeMax==100 && clearedBody.statManaMax==20 &&
                    cleared.Behavior.Follow && cleared.Behavior.BuffEnabled,
                    "confirmed clear retains owner, character, look, voice and difficulty while resetting resources, growth and AI settings");
                NativeCompanionPasswordChecks.WithChallenge(context,_=>{
                    Call(window,"BeforeInput",true);rest();
                    Require((bool)Get(window,"PasswordPromptVisible")&&(bool)Get(window,"OwnsTextToken"),"a real native challenge opens the shared dialog and takes the existing text lease");
                    Queue("draft-only");rest();prepare();
                    Require((string)Get(Get(window,"editor"),"Text")=="draft-only"&&((string)Get(Get(window,"editElement"),"Text")).All(c=>c=='•'),
                        "native committed input reaches the actual buffer while presentation contains masks only");
                    image("companion-config-password");
                    long pendingToken=(long)Get(window,"passwordToken");
                    bool ssc=Main.ServerSideCharacter;
                    try
                    {
                        Main.ServerSideCharacter=true;
                        Require(!(bool)Call(host,"SubmitPassword",pendingToken,"old-owner-credential"),
                            "SSC identity rejects old authentication even before the host update");
                        Call(window,"BeforeInput",true);
                        Require(!(bool)Get(window,"PasswordPromptVisible") && GetOptional(window,"editor")==null &&
                            GetOptional(text,"prepared")==null && !(bool)Get(window,"OwnsTextToken"),
                            "SSC identity drops the old masked prompt and its lease before sampling");
                    }
                    finally { Main.ServerSideCharacter=ssc; }
                    Call(window,"BeforeInput",true);rest();
                    Require((string)Get(Get(window,"editor"),"Text")=="" && (bool)Get(window,"OwnsTextToken"),
                        "the original current identity resumes only with a fresh masked buffer");
                    Queue("focus-draft");rest();
                    Call(window,"CancelForFocusLoss");
                    Require(GetOptional(window,"editor")==null&&GetOptional(text,"prepared")==null&&GetOptional(text,"bound")==null&&
                        !(bool)Get(window,"OwnsTextToken"),"focus loss retires the masked draft from both the dialog and the shared IME input");
                    Call(window,"BeforeInput",true);rest();
                    Require((string)Get(Get(window,"editor"),"Text")==""&&(bool)Get(window,"PasswordPromptVisible"),"the open request can resume with an empty draft");
                    click("password-cancel:");Require(!(bool)Get(window,"PasswordPromptVisible")&&!(bool)Get(window,"OwnsTextToken")&&
                        GetOptional(text,"prepared")==null&&GetOptional(text,"bound")==null,"cancel clears the challenge and drops its masked draft");
                });
                Console.WriteLine("PASS native configuration and password presentation: actual measured controls, independent preferences, 0/1/2 weapon draft, live revision, cancellation, restoration, narrow reset, native challenge and masked input/focus.");
                if ((bool)Get(window,"Visible")) Call(window,"Close");
                screen=new Vector2(1280,800);matrix=Matrix.Identity;projected=false;
                Main.screenWidth=(int)screen.X;Main.screenHeight=(int)screen.Y;PlayerInput.CacheOriginalScreenDimensions();Main.UIScale=1;
                CheckDeletion(window, host, shell, id, other, click, rest, scrollTo, image);
            }
            finally {Call(window,"Close");imeField.SetValue(text,originalIme);owner.name=ownerName;NativeToolsUiChecks.Mouse(Vector2.Zero,false);}
        }

        private static void CheckBackpackTeleport(object context, object window, object host, Guid id, Player body,
            Action<string> click, Action rest)
        {
            const string entry = "actor-wormhole/";
            const int potionSlot = 43;
            var owner = Main.LocalPlayer;
            var prior = body.inventory[potionSlot];
            var originalPosition = body.position;
            var actor = Call(host, "Owned", body);
            long activation = (long)Get(actor, "Generation");
            try
            {
                body.position = owner.position + new Vector2(96, 0);
                rest();
                var button = Control(window, entry);
                Require(!(bool)Get(button, "Enabled") && (string)Get(button, "Hint") == "需要消耗虫洞药水",
                    "backpack teleport uses the shared disabled resource reason");
                body.inventory[potionSlot] = new Item(ItemID.WormholePotion) { stack = 2 };
                rest();
                button = Control(window, entry);
                Require((bool)Get(button, "Enabled") && (string)Get(button, "Hint") == "把AI队友传送到玩家，消耗虫洞药水",
                    "available backpack teleport shows the exact shared action and cost");
                var point = NativeToolsUiChecks.Point(Get(button, "Rect"));
                NativeToolsUiChecks.UiFrame(context, point, true);
                Require(GetOptional(window, "armedKey") != null, "the real backpack button captures its original press");
                Set(actor, "Generation", activation + 1);
                NativeToolsUiChecks.UiFrame(context, point, false);
                Require(body.inventory[potionSlot].stack == 2 && body.Bottom != owner.Bottom,
                    "releasing a previous body activation cannot consume a potion or teleport the replacement");
                Set(actor, "Generation", activation);
                rest();
                click(entry);
                Require(body.inventory[potionSlot].stack == 1 && body.Bottom == owner.Bottom && body.teleportStyle == 3 &&
                    (bool)Call(host, "IsJoined", id), "one physical backpack click executes original-style teleport and consumes exactly one bottle");
            }
            finally
            {
                Set(actor, "Generation", activation);
                body.position = originalPosition;
                body.inventory[potionSlot] = prior;
                rest();
            }
        }

        private static void CheckAutoTeleportPopup(object window, object host, object input, Guid id, Guid other,
            Action<string> click, Action<Vector2, bool, Keys[], int> frame, Action<string> image)
        {
            const string entry = "config-AutoTeleportEnabled/配置/";
            object pane = Get(window, "autoTeleportPopup");
            object blocked = Get(pane, "blocked"), distance = Get(pane, "distance");
            object imeInput = Get(pane, "textInput");
            var ime = imeInput.GetType().GetField("ime", BindingFlags.Instance | BindingFlags.NonPublic);
            object originalIme = ime.GetValue(imeInput);
            Keys[] held = { Keys.W };
            void Step(Vector2 point, bool down, params Keys[] keys) => frame(point, down, keys, 0);
            void HeldClick(object target, string rect)
            {
                Vector2 point = NativeToolsUiChecks.Point(Get(target, rect));
                Step(point, false, held); Step(point, true, held); Step(point, false, held);
            }
            void UnclaimedW() => Require(!(bool)Call(Get(input, "Hotkeys"), "IsSuppressed", (int)Keys.W),
                "ordinary automatic teleport clicks cannot suppress a continuously held W");
            bool Visible() => (bool)Get(pane, "Visible");
            CompanionBehavior Saved(Guid target) => Profile(host, target).Behavior;

            try
            {
                ime.SetValue(imeInput, new PreviewIme(ime.FieldType).GetTransparentProxy());
                var label = ((IEnumerable)Get(window, "labels")).Cast<object>().Single(e => (string)Get(e, "Text") == "自动传送");
                Step(NativeToolsUiChecks.Point(Get(label, "HintRect")), false, held);
                Require((bool)Get(Get(window, "hint"), "Visible") &&
                    (string)Get(Get(label, "Description"), "Text") == "到达条件后AI尝试自动传送到玩家",
                    "the standard row presents its measured feature-name hover explanation");
                HeldClick(Control(window, entry), "Rect"); UnclaimedW();
                Require(Visible() && !(bool)Get(window, "SupplyPopupVisible") && !Saved(id).AutoTeleportEnabled &&
                    Saved(id).AutoTeleportBlockedEnabled && Saved(id).AutoTeleportDistanceEnabled,
                    "disabled automatic teleport opens its own two-condition editor with both conditions checked");
                HeldClick(blocked, "Check"); HeldClick(distance, "Check"); UnclaimedW();
                Require(!Saved(id).AutoTeleportBlockedEnabled && !Saved(id).AutoTeleportDistanceEnabled && !Saved(id).AutoTeleportEnabled,
                    "the two actual checkboxes can both be off while the main switch is unchanged");
                HeldClick(pane, "close"); UnclaimedW();
                Require(!Visible() && GetOptional(window, "Editor") == null, "ordinary close has no numeric lease");
                HeldClick(Control(window, entry), "Rect"); UnclaimedW();

                HeldClick(blocked, "Field"); Step(Vector2.Zero, false);
                Require((bool)Get(window, "OwnsTextToken") && !(bool)Get(window, "PasswordPromptVisible"),
                    "numeric click acquires its own shared native text lease independently of password");
                Queue("12"); Step(Vector2.Zero, false); image("companion-config-auto-teleport");
                Require((string)Get(Get(pane, "editView"), "Text") == "12" && !Saved(id).AutoTeleportBlockedEnabled,
                    "committed native characters are drawn as digits while the adjacent checkbox is untouched");
                var current = Profile(host, id);
                Require((bool)Call(host, "SetAutoTeleportSetting", current.OwnerId, id, Get(host, "CreationSessionToken"),
                    current.DataEpoch, CompanionSetting.AutoTeleportDistanceTiles, 250), "another control can update the latest archive during a draft");
                Queue("\r"); Step(Vector2.Zero, false, Keys.Enter);
                Require(Saved(id).AutoTeleportBlockedSeconds == 12 && Saved(id).AutoTeleportDistanceTiles == 250 && !(bool)Get(window, "OwnsTextToken"),
                    "Enter merges only the edited numeric field and releases the native lease");
                HeldClick(blocked, "Check");
                Require(Saved(id).AutoTeleportBlockedEnabled && Saved(id).AutoTeleportBlockedSeconds == 12,
                    "rechecking the condition keeps its independently saved threshold");

                foreach (string invalid in new[] { "", "0", "-1", "2147483648" })
                {
                    HeldClick(distance, "Field"); Step(Vector2.Zero, false);
                    if (invalid == "") Step(Vector2.Zero, false, Keys.Back);
                    Queue(invalid + "\r"); Step(Vector2.Zero, false, Keys.Enter);
                    Require(Saved(id).AutoTeleportDistanceTiles == 250 && GetOptional(pane, "validation") != null,
                        "invalid submitted text leaves the last accepted threshold intact");
                    Queue("\u001b"); Step(Vector2.Zero, false, Keys.Escape);
                    Require(Visible() && GetOptional(window, "Editor") == null && !(bool)Get(window, "OwnsTextToken"),
                        "Escape cancels only the draft and retires the text lease");
                    Step(Vector2.Zero, false);
                }

                HeldClick(distance, "Field"); Step(Vector2.Zero, false);
                Queue("99"); Step(Vector2.Zero, false);
                HeldClick(Control(window, "profile:" + other + "/"), "Rect");
                Require((Guid)Get(window, "selected") == id && GetOptional(window, "Editor") == null,
                    "the click leaving an editor abandons the draft without activating the covered roster control");
                click("profile:" + other + "/");
                Require(!Visible() && (Guid)Get(window, "selected") == other && Saved(other).AutoTeleportDistanceTiles == 150 &&
                    Saved(id).AutoTeleportDistanceTiles == 250, "a fresh selection retires the old panel without crossing identities");
                click(entry);
                Require(Visible() && !(bool)Call(host, "IsJoined", other),
                    "the offline profile can still open and edit automatic teleport rules");
                HeldClick(distance, "Field"); Step(Vector2.Zero, false);
                Queue("37"); Step(Vector2.Zero, false);
                var archived = Profile(host, other);
                Require((bool)Call(host, "Reset", other) && Profile(host, other).DataEpoch != archived.DataEpoch,
                    "real archived reset advances the identity while a draft is open");
                Queue("\r"); Step(Vector2.Zero, false, Keys.Enter);
                Require(!Visible() && !(bool)Get(window, "OwnsTextToken") && Saved(other).AutoTeleportDistanceTiles == 150,
                    "an Enter queued for the previous archive epoch cannot commit after reset");
                Step(Vector2.Zero, false);
                click("profile:" + id + "/");
                Require((bool)Call(host, "RestoreBehavior", id), "restore the original test profile before testing hotkeys");
                Step(Vector2.Zero, false);
                Console.WriteLine("PASS automatic teleport editor: original Shell clicks and character queue, independent checks, field merge, invalid and cancelled drafts, selection and archive epoch retirement.");
            }
            finally { ime.SetValue(imeInput, originalIme); }
        }

        private static int failDeletionHotkey;

        private static void CheckDeletion(object window, object host, object shell, Guid id, Guid other,
            Action<string> click, Action rest, Action<string> scroll, Action<string> image)
        {
            var keys = Get(shell, "hotkeys");
            var bindings = (HotkeyBindings)Get(keys, "Bindings");
            var registry = (HotkeyRegistry)Get(keys, "Registry");
            var profile = Profile(host, id);
            string prefix = "companions.profile." + profile.OwnerId.ToString("N") + ".";
            string deletedKeys = prefix + id.ToString("N") + ".";
            string otherKeys = prefix + other.ToString("N") + ".";
            string hotkeysPath = Path.Combine(Terraria.Program.SavePath, "composition", "JueMingRData", "config", "hotkeys.json");
            string ownerFolder = Path.Combine(Terraria.Program.SavePath, "composition", "JueMingRData", "companions", "characters", profile.OwnerId.ToString("N"));
            string deletedPath = Path.Combine(ownerFolder, id.ToString("N") + ".bin");
            const string unrelatedAction = "biome-display.toggle";
            Require(HotkeyChord.TryCreate((int)Keys.F13,HotkeyModifiers.None,out var unrelatedChord,out _) &&
                bindings.TrySet(unrelatedAction,unrelatedChord,null,out _,out _), "give an existing unrelated feature its own actual saved chord");
            NativeQuickItemChecks.Until(() => { rest(); return !bindings.Busy; });
            Require(bindings.Get(unrelatedAction)?.MainKey==(int)Keys.F13, "the unrelated baseline chord really committed");
            var retainedRows = bindings.StoredEntries.Where(row => !row.Key.StartsWith(deletedKeys, StringComparison.Ordinal)).ToArray();
            Require(retainedRows.Any(row => row.Key.StartsWith(otherKeys, StringComparison.Ordinal)), "test has another actual companion binding on disk");
            Call(shell,"OpenCompanions",Get(Get(Get(shell,"State"),"Layout"),"Viewport")); rest();
            click("profile:"+id+"/"); click("configuration/");
            Require(bindings.TrySet(deletedKeys+"buff",null,null,out _,out _), "actual deletion includes an explicit empty shortcut row");
            NativeQuickItemChecks.Until(() => { rest(); return !bindings.Busy; });
            Require(bindings.Get(deletedKeys+"wormhole")?.MainKey==(int)NativeCompanionHotkeyFeedbackChecks.WormholeKey,
                "retirement includes the persisted one-shot recall row established by native keyboard checks");
            Require(bindings.Get(deletedKeys+"auto-warp")?.MainKey==(int)NativeCompanionHotkeyFeedbackChecks.AutoTeleportKey,
                "retirement includes the persisted automatic teleport row established by native keyboard checks");
            Require(bindings.StoredEntries.Any(row => row.Key == deletedKeys + "buff" && row.Value == ""), "empty row really persisted");
            scroll("request-delete/"); click("request-delete/"); scroll("confirm-delete/"); image("companion-config-delete-confirmation");
            click("profile:"+other+"/");
            Require(GetOptional(window,"deletionTarget")==null && Profile(host,id).Id==id && Profile(host,other).Id==other,
                "switching profiles clears the old confirmation and keeps both real characters");
            click("profile:"+id+"/"); scroll("request-delete/"); click("request-delete/"); scroll("cancel-delete/"); click("cancel-delete/");
            Require(Profile(host,id).Id==id && File.Exists(deletedPath), "cancel preserves the same character archive");

            Require((bool)Call(host,"Join",id),"real reset identity rejoins for active deletion");
            var body=(Player)Call(host,"GetBody",id); int slot=body.whoAmI;
            var oldAction=registry.Find(deletedKeys+"attack");
            var oldAutoTeleport=registry.Find(deletedKeys+"auto-warp");
            var actor=Call(host,"Owned",body);
            var hooks=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionHooks");
            var enter=hooks.GetMethod("Enter",BindingFlags.Static|BindingFlags.NonPublic);
            scroll("request-delete/"); click("request-delete/"); scroll("confirm-delete/");
            body.inventory[0].SetDefaults(3541); body.selectedItemState.Select(0); body.channel=true;
            Projectile controller;
            using ((IDisposable)enter.Invoke(null,new[]{actor,(object)body.Center,false}))
                controller=Main.projectile[Projectile.NewProjectile(new EntitySource_ItemUse(body,body.HeldItem),body.Center,Vector2.UnitX,633,10,0f,slot)];
            Require(controller.active && Call(host,"ProjectileActor",controller)!=null,"real native attached controller owns the selected activation");
            int items = Main.item.Count(item => item.active);
            var infrastructure=AppDomain.CurrentDomain.GetAssemblies().Single(assembly=>assembly.GetName().Name=="JueMingR.Infrastructure");
            var write=AccessTools.Method(infrastructure.GetType("JueMingR.Infrastructure.Storage.AtomicFileDocument"),"Write");
            var patch=new Harmony("JueMingR.Tests.CompanionDeletionHotkey");
            patch.Patch(write,prefix:new HarmonyMethod(typeof(NativeCompanionConfigurationChecks),nameof(FailDeletionHotkey)));
            try
            {
                System.Threading.Interlocked.Exchange(ref failDeletionHotkey,1);
                click("confirm-delete/");
                Require(!body.active && !body.dead && Main.item.Count(item=>item.active)==items && !ReferenceEquals(Main.player[slot],body) &&
                    !controller.active && !oldAction.Invoke(HotkeyContext.SinglePlayer) && !oldAutoTeleport.Invoke(HotkeyContext.SinglePlayer) &&
                    !(bool)Call(host,"Join",id),
                    "confirmed deletion retires the active body, controller, slot and keys with no native death or item drop");
                Require((Guid)Get(window,"selected")==other && Profile(host,other).Id==other,"selected deletion falls through to the remaining real profile");
                Call(window,"Close");
                NativeQuickItemChecks.Until(() => { rest(); return ((IEnumerable)Get(host,"Deletions")).Cast<CompanionDeletion>().Any(d=>d.FilesRemoved&&d.Error!=null); });
                Require(!File.Exists(deletedPath) && !File.Exists(deletedPath+".bak") && File.Exists(deletedPath+".delete") &&
                    bindings.StoredEntries.Any(row=>row.Key.StartsWith(deletedKeys,StringComparison.Ordinal)),
                    "injected shortcut write failure leaves a durable exact retry after the real character files are removed and panel closed");
            }
            finally { System.Threading.Interlocked.Exchange(ref failDeletionHotkey,0); patch.UnpatchAll(patch.Id); }
            var originalOwner=Main.ActivePlayerFileData;
            try
            {
                Main.ActivePlayerFileData=new Terraria.IO.PlayerFileData(Path.Combine(Terraria.Program.SavePath,"delete-another-owner.plr"),false)
                { Player=originalOwner.Player };
                rest();
                Require((Guid)Get(host,"DeletionViewOwner")!=profile.OwnerId,"an actual different character file cannot display or resume the original deletion");
                Call(host,"RetryCompanionDeletion",profile.OwnerId,id);
                Require(((IEnumerable)Get(host,"Deletions")).Cast<CompanionDeletion>().Single().Error!=null,
                    "switching owners preserves the original failure without retargeting it");
            }
            finally { Main.ActivePlayerFileData=originalOwner; rest(); }
            Call(shell,"OpenCompanions",Get(Get(Get(shell,"State"),"Layout"),"Viewport")); rest();
            click("configuration/"); scroll("retry-delete-"+id); image("companion-config-delete-retry"); click("retry-delete-"+id);
            Call(window,"Close");
            bool menu=Main.gameMenu;
            try
            {
                Main.gameMenu=true;
                NativeQuickItemChecks.Until(() => { rest(); return !((IEnumerable)Get(host,"Deletions")).Cast<CompanionDeletion>().Any(); });
            }
            finally { Main.gameMenu=menu; rest(); }
            string savedHotkeys=File.ReadAllText(hotkeysPath);
            Require(!savedHotkeys.Contains(deletedKeys) && retainedRows.SequenceEqual(bindings.StoredEntries) && !File.Exists(deletedPath+".delete"),
                "actual document retry removes the ten toggle rows and the one-shot recall including empty mappings, preserving other bindings");
            Require((bool)Call(host,"Create",profile.Name),"the same name can create a genuinely new character after confirmed deletion");
            Guid replacement=(Guid)Get(host,"LastCreatedId");
            var archive=Get(host,"profiles");
            NativeQuickItemChecks.Until(() => { rest(); return (bool)Call(archive,"IsSaved",replacement); });
            Require(replacement!=id && Profile(host,replacement).OwnerId==profile.OwnerId && !oldAction.Invoke(HotkeyContext.SinglePlayer),
                "fresh identity and old action cannot cross even with the same name");
            // The real WithChallenge sample above leaves its own archived AI
            // after closing the secondary connection in this same fixture.
            var challenge=((IEnumerable)Get(host,"Profiles")).Cast<CompanionProfile>().Single(p=>p.Name=="密码界面原生角色");
            Call(shell,"OpenCompanions",Get(Get(Get(shell,"State"),"Layout"),"Viewport")); rest();
            foreach (Guid target in new[]{challenge.Id,other,replacement})
            {
                click("profile:"+target+"/"); click("configuration/");
                scroll("request-delete/"); click("request-delete/"); scroll("confirm-delete/"); click("confirm-delete/");
                NativeQuickItemChecks.Until(() => { rest(); return !((IEnumerable)Get(host,"Deletions")).Cast<CompanionDeletion>().Any(); });
            }
            Require(!((IEnumerable)Get(host,"Profiles")).Cast<CompanionProfile>().Any() && (Guid)Get(window,"selected")==Guid.Empty,
                "deleting the last offline character clears the actual roster and selection");
            Require((bool)Get(Control(window,"create/"),"Enabled"),"the empty roster keeps the add entry available");
            Require(Main.ActivePlayerFileData.Player==Main.LocalPlayer && Main.LocalPlayer.active,"the actual human body stays intact");
            Require(retainedRows.Where(row=>!row.Key.StartsWith(prefix,StringComparison.Ordinal)).SequenceEqual(bindings.StoredEntries) &&
                bindings.Get(unrelatedAction)?.MainKey==(int)Keys.F13 &&
                File.ReadAllText(hotkeysPath).Contains(unrelatedAction), "the unrelated real feature is still bound after all companion rows are removed");
            Console.WriteLine("PASS native companion deletion: physical cancel/selection/confirmation, online controller and slot retirement without death, real shortcut retry and file cleanup, same-name new ID, final offline roster.");
        }

        private static bool FailDeletionHotkey(object __instance, ref PreferenceWriteResult __result)
        {
            string path=(string)Get(__instance,"path");
            if (!path.EndsWith("hotkeys.json",StringComparison.OrdinalIgnoreCase) ||
                System.Threading.Interlocked.Exchange(ref failDeletionHotkey,0)==0) return true;
            __result = new PreferenceWriteResult(PreferenceWriteStatus.IoFailure, null, "isolated shortcut cleanup failure");
            return false;
        }

        private static void CheckWorldNames(ProbeGraphics graphics, string output, Player body)
        {
            var previousPosition=Main.screenPosition;
            var previousZoom=Main.GameViewMatrix.Zoom;
            int previousMode=Main.netMode;
            bool previousDead=body.dead;
            var nativeDraw=AccessTools.Method(typeof(Main),"DrawPlayerChatBubbles");
            Action draw=()=>nativeDraw.Invoke(Main.instance,null);
            try
            {
                Main.screenPosition=body.Center-new Vector2(420,320);
                Main.GameViewMatrix.Zoom=new Vector2(1.2f);
                var anchor=Main.GetChatDrawPosition(body);
                var withName=graphics.Pixels(draw,Main.UIScaleMatrix);
                Require(withName.Where((pixel,index)=>pixel.A>0 && Math.Abs(index%960-anchor.X)<220 &&
                    index/960<anchor.Y && index/960>anchor.Y-65).Any(),
                    "actual native overhead draw displays the active solo profile near its zoomed body anchor");
                graphics.Image(Path.Combine(output,"companion-world-name.png"),draw,Main.UIScaleMatrix);
                body.dead=true;
                var dead=graphics.Pixels(draw,Main.UIScaleMatrix);
                body.dead=false;
                Main.netMode=1;
                var network=graphics.Pixels(draw,Main.UIScaleMatrix);
                Require(dead.All(pixel=>pixel.A==0) && network.SequenceEqual(dead),
                    "the actual overhead batch draws no product name for the dead body or the multiplayer mode");
                Console.WriteLine("PASS actual native overhead Draw: active solo companion at zoomed anchor, no product label for dead/MP.");
            }
            finally
            {
                body.dead=previousDead;
                Main.netMode=previousMode;
                Main.screenPosition=previousPosition;
                Main.GameViewMatrix.Zoom=previousZoom;
            }
        }
        private static void CheckBankTrash(object window, object host, Player body, Action<string> click,
            Action<Vector2,bool,Keys[],int> frame, Action prepare)
        {
            const int x=60, y=51;
            var savedTiles=new Tile[2,2];
            for(int column=0;column<2;column++)
                for(int row=0;row<2;row++)
                {
                    savedTiles[column,row]=Main.tile[x+column,y+row];
                    var tile=new Tile { type=TileID.Safes, frameX=(short)(18*column), frameY=(short)(18*row) };
                    tile.active(true);Main.tile[x+column,y+row]=tile;
                }
            var owner=Main.LocalPlayer;var savedTrash=owner.trashItem;
            var savedSlot=body.bank2.item[0];var savedPosition=body.position;
            bool noTrash=Terraria.UI.ItemSlot.Options.DisableQuickTrash, ctrl=Terraria.UI.ItemSlot.Options.DisableLeftShiftTrashCan;
            try
            {
                body.Center=new Vector2(x*16,y*16);
                body.bank2.item[0]=new Item();body.bank2.item[0].SetDefaults(ItemID.WoodenBow);
                Terraria.UI.ItemSlot.Options.DisableQuickTrash=false;
                Terraria.UI.ItemSlot.Options.DisableLeftShiftTrashCan=true;
                click("storage-PiggyBank/");
                var slot=NativeToolsUiChecks.Point(Get(Control(window,"slot:False:Safe:0/"),"Rect"));
                frame(slot,false,Array.Empty<Keys>(),0);
                frame(slot,true,new[]{Keys.LeftControl},0);
                frame(slot,false,Array.Empty<Keys>(),0);
                Require(body.bank2.item[0].IsAir&&owner.trashItem.type==ItemID.WoodenBow&&!(bool)Get(host,"HasItemGesture"),
                    "the real modified safe click commits to the owner's trash and never begins ordinary pickup");
                var trashAfter=owner.trashItem;
                body.bank2.item[0]=new Item();body.bank2.item[0].SetDefaults(ItemID.WoodenBow);
                Main.tile[x+1,y+1].active(false);prepare();
                frame(slot,true,new[]{Keys.LeftControl},0);frame(slot,false,Array.Empty<Keys>(),0);
                Require(body.bank2.item[0].type==ItemID.WoodenBow&&ReferenceEquals(owner.trashItem,trashAfter)&&
                    !(bool)Get(host,"HasItemGesture")&&!Controls(window).Cast<object>().Any(c=>((string)Get(c,"Key")).StartsWith("slot:False:Safe:",StringComparison.Ordinal)),
                    "loss of the actual entrance retires the projected bank slot without discarding or picking up");
            }
            finally
            {
                for(int column=0;column<2;column++)
                    for(int row=0;row<2;row++) Main.tile[x+column,y+row]=savedTiles[column,row];
                body.position=savedPosition;body.bank2.item[0]=savedSlot;owner.trashItem=savedTrash;
                Terraria.UI.ItemSlot.Options.DisableQuickTrash=noTrash;
                Terraria.UI.ItemSlot.Options.DisableLeftShiftTrashCan=ctrl;
            }
        }

        private static void CheckHotkeyOwnership(object context, object window, object host, object shell,
            Guid id, Guid other, Action<string> click, Action rest, Action drawPopup)
        {
            var popup=Get(shell,"HotkeyPopup");
            var bindings=(HotkeyBindings)Get(Get(shell,"hotkeys"),"Bindings");
            string prefix="companions.profile."+Profile(host,id).OwnerId.ToString("N")+".";
            string selectedAction=prefix+id.ToString("N")+".attack";
            string otherAction=prefix+other.ToString("N")+".respawn";
            string entry="binding-"+selectedAction+"/";
            click(entry);Require(!(bool)Get(popup,"Visible"),"the first real companion key click does not open capture");
            click(entry);Require((bool)Get(popup,"Visible")&&(bool)Get(window,"Visible")&&
                !(bool)Get(Get(shell,"State"),"Visible")&&(string)Get(popup,"Target")==selectedAction,
                "second click opens the common recorder with the independent companion window visible");
            drawPopup();
            Require(!(bool)Get(shell,"Failed"),"the real shell draws the shared popup above the independent companion window");
            NativeToolsUiChecks.PopupClick(context,popup,"Record");
            NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false,Keys.F14);
            NativeQuickItemChecks.Until(()=>{rest();return !bindings.Busy;});
            Require(bindings.Get(selectedAction)?.MainKey==(int)Keys.F14&&Profile(host,id).Behavior.AttackEnabled,
                "recording the actual chord does not execute it through the parent");
            NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false,Keys.Escape);
            Require(!(bool)Get(popup,"Visible")&&(bool)Get(window,"Visible"),"Escape closes only the companion-owned recorder");
            rest();

            string nurse = prefix + id.ToString("N") + ".nurse";
            string furniture = prefix + id.ToString("N") + ".furniture";
            Require(bindings.Get(nurse) == null && bindings.Get(furniture) == null, "new per-profile service actions have no default chord");
            foreach (var action in new[] { (id: nurse, key: Keys.F16), (id: furniture, key: Keys.F17) })
            {
                Require(HotkeyChord.TryCreate((int)action.key, HotkeyModifiers.None, out var serviceChord, out _) &&
                    bindings.TrySet(action.id, serviceChord, null, out _, out _), "save the selected profile's service chord " + action.key);
                NativeQuickItemChecks.Until(() => { rest(); return !bindings.Busy; });
            }
            click("profile:"+other+"/");
            Require(HotkeyChord.TryCreate((int)Keys.F15,HotkeyModifiers.None,out var chord,out _),"second profile fixture chord");
            Require(bindings.TrySet(otherAction,chord,null,out _,out _),"the distinct selected profile can save its real dynamic action");
            NativeQuickItemChecks.Until(()=>{rest();return !bindings.Busy;});
            Call(window,"Close");rest();
            NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false,Keys.F14);
            rest();
            Require(!Profile(host,id).Behavior.AttackEnabled&&Profile(host,other).Behavior.AttackEnabled&&Profile(host,other).Behavior.AutoRespawn,
                "closed UI dispatches the chord to its archived profile rather than the last selected character");
            NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false,Keys.F15);
            rest();
            Require(!Profile(host,other).Behavior.AutoRespawn&&Profile(host,id).Behavior.AutoRespawn,
                "the second profile's chord takes the correct independent action");
            NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false,Keys.F14);
            rest();
            Require(Profile(host,id).Behavior.AttackEnabled,"gameplay's next physical edge toggles the original profile again");

            NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false,Keys.F16); rest();
            NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false,Keys.F17); rest();
            var changed = Profile(host,id).Behavior;
            var unchanged = Profile(host,other).Behavior;
            Require(!changed.AllowNurse && changed.HealthRecoveryEnabled && changed.FurnitureEnabled &&
                unchanged.AllowNurse && !unchanged.FurnitureEnabled,
                "closed-window nurse and furniture chords apply solely to their bound character while potion recovery stays enabled");
            NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false,Keys.F16); rest();
            NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false,Keys.F17); rest();
            Require(Profile(host,id).Behavior.AllowNurse && !Profile(host,id).Behavior.FurnitureEnabled,
                "second physical edges restore this profile's independent service switches");
            Call(shell,"OpenCompanions",Get(Get(Get(shell,"State"),"Layout"),"Viewport"));
            rest();click("profile:"+id+"/");rest();
        }

        private static void CheckPressIdentity(object window,object host,Guid id,Player body,Matrix matrix,Vector2 screen)
        {
            bool dead=body.dead;int timer=body.respawnTimer,selected=body.selectedItem;var original=Profile(host,id).Behavior;
            Action prepare=()=>Call(window,"Prepare",matrix,screen);
            Action<Vector2,bool> sample=(point,left)=>{NativeToolsUiChecks.Mouse(point,left);Call(window,"ProcessLayoutInput",true,new KeyboardState(),point,matrix,screen,true,0);};
            try {
                body.dead=true;body.respawnTimer=122;prepare();var button=Control(window,"config-Attack/关闭/");var point=NativeToolsUiChecks.Point(Get(button,"Rect"));sample(point,false);sample(point,true);var armed=Get(window,"armedKey");
                body.respawnTimer=61;prepare();Require(Equals(armed,Get(window,"armedKey")),"ordinary countdown refresh retains the same valid toggle operation");
                Require((string)Call(host,"RespawnStatus",id)=="复活倒计时 2 秒","native timer refresh retains the same status outlet");
                var changed=original.Clone();changed.AttackEnabled=!changed.AttackEnabled;Require((bool)Call(host,"SetBehavior",id,changed),"actual preference owner accepts an intervening change");prepare();sample(point,false);Require(Profile(host,id).Behavior.AttackEnabled==changed.AttackEnabled,"an explicit off button never reverses a newer accepted off value");
            }
            finally {body.dead=dead;body.respawnTimer=timer;body.selectedItemState.Select(selected);Call(host,"SetBehavior",id,original);prepare();NativeToolsUiChecks.Mouse(Vector2.Zero,false);}
        }
    }
}
