using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using HarmonyLib;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
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
        private static CompanionProfile Profile(object host,Guid id)
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
            Action<string> directory=section=>click("config-tab:"+section+"/");
            Action<string> image=name=>{rest();graphics.Image(Path.Combine(output,name+".png"),()=>Call(window,"Draw"),matrix,(int)screen.X,(int)screen.Y);};
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
                graphics.LoadItemTextures(itemTypes);Console.WriteLine("CONFIG-ASSETS actual inventory types="+String.Join(",",itemTypes));
                Main.screenWidth=(int)screen.X;Main.screenHeight=(int)screen.Y;PlayerInput.CacheOriginalScreenDimensions();Main.UIScale=1;
                var renderer=Get(shell,"renderer");Call(renderer,"RefreshResources");Call(renderer,"Prepare",Get(shell,"State"),screen.X,screen.Y,1f);
                Set(window,"selected",id);Call(shell,"OpenCompanions",Get(Get(Get(shell,"State"),"Layout"),"Viewport"));rest();rest();click("configuration/");
                Require(Controls(window).Cast<object>().Count(c=>((string)Get(c,"Key")).StartsWith("config-tab:",StringComparison.Ordinal))==5,"five actual categories share one measured navigation strip");
                Require(!Controls(window).Cast<object>().Any(c=>((string)Get(c,"Key")).StartsWith("actor-state/",StringComparison.Ordinal)||((string)Get(c,"Key")).StartsWith("team:",StringComparison.Ordinal)),"team and membership actions stay on the backpack page");
                Require(!Controls(window).Cast<object>().Any(c=>((string)Get(c,"Key")).StartsWith("config-tab:Connection",StringComparison.Ordinal)),"configuration has no always-present password outlet");
                image("companion-config-behavior");
                int builds=(int)Get(window,"LayoutBuilds");for(int i=0;i<100;i++)prepare();Require((int)Get(window,"LayoutBuilds")==builds,"stable configuration prepares rebuild no controls");
                click("config-toggle:attack:");Require(!Profile(host,id).Behavior.AttackEnabled&&Profile(host,id).Behavior.Follow,"one toggle edits its real field and preserves neighboring preferences");
                click("config-choice:movement/");click("stay/");Require(!Profile(host,id).Behavior.Follow,"real movement option preserves stationary behavior");
                click("config-toggle:respawn:");Require(!Profile(host,id).Behavior.AutoRespawn,"real auto-respawn setting uses the existing behavior owner");
                directory("Healing");
                float before=(float)Get(Get(Control(window,"config-toggle:nurse:"),"Rect"),"Y");
                click("config-choice:heal-mode/");
                Require((float)Get(Get(Control(window,"config-toggle:nurse:"),"Rect"),"Y")>before,"mode choices advance following controls in the measured layout");
                image("companion-config-heal-modes");click("heal-fast/");
                click("config-toggle:nurse:");
                Require(Profile(host,id).Behavior.HealthRecoveryMode==CompanionHealthRecoveryMode.Fast&&!Profile(host,id).Behavior.AllowNurse,
                    "recovery mode and nurse are independent persisted settings");
                click("supply-list:Life/");scrollTo("supply:Life:"+ItemID.HealingPotion+":");
                click("supply:Life:"+ItemID.HealingPotion+":");
                Require(Profile(host,id).Behavior.ExcludedHealingTypes.Contains(ItemID.HealingPotion),"unchecking a defined life potion saves its explicit override even while absent");
                directory("Mana");click("config-toggle:mana:");Require(!Profile(host,id).Behavior.ManaRecoveryEnabled,"mana has its own switch");
                Require(!Controls(window).Cast<object>().Any(c=>((string)Get(c,"Key")).StartsWith("config-choice:",StringComparison.Ordinal)),"mana introduces no mode");
                directory("Consumables");click("supply-list:Growth/");scrollTo("supply:Growth:"+ItemID.LifeCrystal+":");
                click("supply:Growth:"+ItemID.LifeCrystal+":");
                Require(Profile(host,id).Behavior.AllowedGrowthTypes.SequenceEqual(new[]{(int)ItemID.LifeCrystal}) &&
                    ((IEnumerable)Get(window,"labels")).Cast<object>().Any(label=>(string)Get(label,"Text")=="缺货"),"growth can be permitted without possessing the material");
                body.statLifeMax=400;Call(host,"CaptureActorState",Call(host,"Owned",body),null);prepare();
                var loanRows=((IEnumerable)Get(window,"labels")).Cast<object>().Select(label=>(string)Get(label,"Text")).ToArray();
                Require(loanRows.Contains("已完成"),"the complete status is independent of the lasting check mark");body.statLifeMax=100;
                image("companion-config-growth");
                directory("Character");
                long beforeConfirmation=Profile(host,id).DataEpoch;click("request-reset/");
                var resetText=((IEnumerable)Get(window,"labels")).Cast<object>().Select(label=>(string)Get(label,"Text")).ToArray();
                Require(resetText.Contains("将清空物品、装备、成长和 AI 设置。")&&resetText.Contains("保留名字、外观、难度和归属。"),"reset preserves both original explanations before confirmation");
                click("cancel-reset/");Require(Profile(host,id).DataEpoch==beforeConfirmation,"wide confirmation cancellation performs no archive reset");
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
                Call(host,"CaptureActorState",Call(host,"Owned",body),null);
                Require(Profile(host,id).Revision>beforeSave&&GetOptional(window,"weaponDraft")!=null,"ordinary background revision leaves the active draft intact");
                frame(done,false,neutral,0);
                Require(Profile(host,id).Behavior.MainWeapons.Select(w=>w.SlotHint).SequenceEqual(new[]{10,29}) &&
                    ReferenceEquals(gun,body.inventory[10])&&ReferenceEquals(bow,body.inventory[29])&&body.selectedItem==held&&GetOptional(window,"weaponDraft")==null,
                    "release after a background save commits precisely the two source items and leaves the active hand alone");
                click("weapon-select/");click(secondSlot);click("weapon-cancel/");
                Require(Profile(host,id).Behavior.MainWeapons.Length==2&&GetOptional(window,"weaponDraft")==null,"cancel keeps the previously saved pair");
                click("weapon-select/");click("profile:"+other+"/");
                Require(GetOptional(window,"weaponDraft")==null&&(Guid)Get(window,"selected")==other,"changing the selected character retires the whole draft");click("profile:"+id+"/");
                click("configuration/");click("restore-behavior/");var restored=Profile(host,id).Behavior;
                Require(restored.Follow&&restored.AttackEnabled&&restored.AutoRespawn&&restored.MainWeapons.Length==0&&restored.AllowedGrowthTypes.Length==0&&
                    restored.HealthRecoveryMode==CompanionHealthRecoveryMode.Smart&&restored.ManaRecoveryEnabled&&restored.AllowNurse&&Profile(host,id).DataEpoch==epoch,
                    "restore resets the full configuration while preserving character archive identity");
                directory("Behavior");
                CheckPressIdentity(window,host,id,body,matrix,screen);
                // Vanilla clamps its real UI scale. Independently exercise this
                // window's explicit narrow-matrix contract with direct window
                // input; this is not an accepted native scale/OS-input claim.
                screen=new Vector2(840,560);matrix=Matrix.CreateScale(1.5f);projected=true;Main.screenWidth=840;Main.screenHeight=560;PlayerInput.CacheOriginalScreenDimensions();Main.UIScale=1;prepare();directory("Character");
                var nameRect=Get(window,"configurationNameRect");
                Require(Terraria.GameContent.FontAssets.MouseText.Value.MeasureString(profileName).X*.85f>(float)Get(nameRect,"Width"),"the existing measured text renderer must ellipsize this actual twenty-character narrow name");
                frame(NativeToolsUiChecks.Point(nameRect),false,neutral,0);var nameHint=Get(window,"hint");Require((bool)Get(nameHint,"Visible")&&String.Concat(((IEnumerable)Get(nameHint,"Lines")).Cast<object>().Select(line=>(string)Get(line,"Text")))==profileName,"narrow name keeps its complete original through the existing hint path");
                graphics.Image(Path.Combine(output,"companion-config-narrow-name.png"),()=>Call(window,"Draw"),matrix,(int)screen.X,(int)screen.Y);
                directory("Consumables");scrollTo("supply-list:Buff/");click("supply-list:Buff/");prepare();
                var header=Get(window,"configurationIdentity");var nav=Get(Control(window,"config-tab:Consumables/"),"Rect");
                int scrollBudget=150;
                while((int)Get(window,"bodyOffset")<(int)Get(window,"bodyMaximum")&&--scrollBudget>0)frame(NativeToolsUiChecks.Point(Get(window,"configurationViewport")),false,neutral,-120);
                Require((int)Get(window,"bodyOffset")>0&&Get(window,"configurationIdentity").Equals(header)&&Get(Control(window,"config-tab:Consumables/"),"Rect").Equals(nav),"narrow scrolling leaves the identity and directory baselines fixed");
                image("companion-config-narrow-buffs");
                directory("Character");scrollTo("request-reset/");click("request-reset/");scrollTo("cancel-reset/");
                image("companion-config-narrow-reset");float expanded=(float)Get(Get(window,"panel"),"Height");click("cancel-reset/");Require(Profile(host,id).DataEpoch==epoch&&!(bool)Get(window,"confirmingReset")&&
                    (float)Get(Get(window,"panel"),"Height")<expanded&&(int)Get(window,"bodyOffset")<=(int)Get(window,"bodyMaximum"),"cancelling confirmation shrinks measured content without resetting the profile");
                // Confirm through the actual button on the same real identity.
                scrollTo("request-reset/");click("request-reset/");scrollTo("confirm-reset/");click("confirm-reset/");
                NativeQuickItemChecks.Until(()=>{Call(host,"Update",(ulong)Main.GameUpdateCount);return !(bool)Call(host,"IsJoined",id)&&Profile(host,id).DataEpoch==epoch+1&&(bool)Call(archive,"IsSaved",id);});
                Require(Profile(host,id).Id==id&&Profile(host,id).Name==profileName,"confirmed reset uses the existing archive owner once");
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
            }
            finally {Call(window,"Close");imeField.SetValue(text,originalIme);owner.name=ownerName;NativeToolsUiChecks.Mouse(Vector2.Zero,false);}
        }
        private static void CheckPressIdentity(object window,object host,Guid id,Player body,Matrix matrix,Vector2 screen)
        {
            bool dead=body.dead;int timer=body.respawnTimer,selected=body.selectedItem;var original=Profile(host,id).Behavior;
            Action prepare=()=>Call(window,"Prepare",matrix,screen);
            Action<Vector2,bool> sample=(point,left)=>{NativeToolsUiChecks.Mouse(point,left);Call(window,"ProcessLayoutInput",true,new KeyboardState(),point,matrix,screen,true,0);};
            try {
                body.dead=true;body.respawnTimer=122;prepare();var button=Control(window,"config-toggle:attack:");var point=NativeToolsUiChecks.Point(Get(button,"Rect"));sample(point,false);sample(point,true);var armed=Get(window,"armedKey");
                body.respawnTimer=61;prepare();Require(Equals(armed,Get(window,"armedKey")),"ordinary countdown refresh retains the same valid toggle operation");
                Require((string)Call(host,"RespawnStatus",id)=="复活倒计时 2 秒","native timer refresh retains the same status outlet");
                var changed=original.Clone();changed.AttackEnabled=!changed.AttackEnabled;Require((bool)Call(host,"SetBehavior",id,changed),"actual preference owner accepts an intervening change");prepare();Require(GetOptional(window,"armedKey")==null,"changed toggle target retires the old armed operation");sample(point,false);Require(Profile(host,id).Behavior.AttackEnabled==changed.AttackEnabled,"retired release does not reverse a newer accepted toggle value");
            }
            finally {body.dead=dead;body.respawnTimer=timer;body.selectedItemState.Select(selected);Call(host,"SetBehavior",id,original);prepare();NativeToolsUiChecks.Mouse(Vector2.Zero,false);}
        }
    }
}
