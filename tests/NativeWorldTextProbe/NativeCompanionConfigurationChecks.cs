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
            var body=(Player)Call(host,"GetBody",id);body.inventory[0].SetDefaults(ItemID.SDMG);body.selectedItemState.Select(0);
            foreach(var npc in Main.npc)npc.active=false;
            var text=Get(window,"textInput");var imeField=text.GetType().GetField("ime",BindingFlags.Instance|BindingFlags.NonPublic);var originalIme=imeField.GetValue(text);
            var originalPassword=(string)Get(host,"ConnectionPassword");var owner=Main.LocalPlayer;string ownerName=owner.name;
            var matrix=Matrix.Identity;var screen=new Vector2(1280,800);var neutral=new Keys[0];bool projected=false;
            Action prepare=()=>Call(window,"Prepare",matrix,screen);
            Action<Vector2,bool,Keys[],int> frame=(point,left,keys,wheel)=>{
                if(!projected)NativeToolsUiChecks.UiFrame(context,point*matrix.M11,left,keys,wheel);
                else {NativeToolsUiChecks.Mouse(point,left);Call(window,"Process",true,new KeyboardState(keys),point,matrix,screen,true,wheel);}
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
            try
            {
                imeField.SetValue(text,new PreviewIme(imeField.FieldType).GetTransparentProxy());owner.name="验证主人";
                var otherBody=(Player)Call(host,"GetBody",other);
                var itemTypes=owner.inventory.Concat(body.inventory).Concat(otherBody.inventory).Where(item=>item!=null&&!item.IsAir).Select(item=>item.type).Distinct().ToArray();
                graphics.LoadItemTextures(itemTypes);Console.WriteLine("CONFIG-ASSETS actual inventory types="+String.Join(",",itemTypes));
                Main.screenWidth=(int)screen.X;Main.screenHeight=(int)screen.Y;PlayerInput.CacheOriginalScreenDimensions();Main.UIScale=1;
                var renderer=Get(shell,"renderer");Call(renderer,"RefreshResources");Call(renderer,"Prepare",Get(shell,"State"),screen.X,screen.Y,1f);
                Set(window,"selected",id);Call(shell,"OpenCompanions",Get(Get(Get(shell,"State"),"Layout"),"Viewport"));rest();rest();click("configuration/");
                Require(Controls(window).Cast<object>().Count(c=>((string)Get(c,"Key")).StartsWith("config-tab:",StringComparison.Ordinal))==4,"actual implemented directories share one measured navigation strip");
                Require(!Controls(window).Cast<object>().Any(c=>((string)Get(c,"Key")).StartsWith("actor-state/",StringComparison.Ordinal)||((string)Get(c,"Key")).StartsWith("team:",StringComparison.Ordinal)),"team and membership actions stay on the backpack page");
                image("companion-config-combat");
                int builds=(int)Get(window,"LayoutBuilds");for(int i=0;i<100;i++)prepare();Require((int)Get(window,"LayoutBuilds")==builds,"stable configuration prepares rebuild no controls");
                click("config-toggle:attack:");Require(!Profile(host,id).Behavior.AttackEnabled&&Profile(host,id).Behavior.Follow,"one toggle edits its real field and preserves neighboring preferences");
                float before=(float)Get(Get(Control(window,"config-choice:target/"),"Rect"),"Y");click("config-choice:mode/");
                Require((float)Get(Get(Control(window,"config-choice:target/"),"Rect"),"Y")>before,"inline options advance following controls rather than overlaying them");image("companion-config-expanded");click("defensive/");
                click("config-choice:target/");click("prefer-self/");Require(Profile(host,id).Behavior.SelfDefense&&!Profile(host,id).Behavior.PreferOwner&&!Profile(host,id).Behavior.AttackEnabled,"actual mode/target edits retain the existing attack preference");
                directory("Behavior");click("config-choice:movement/");click("stay/");Require(!Profile(host,id).Behavior.Follow,"real movement option selects stationary behavior");
                directory("Character");click("config-toggle:respawn:");Require(!Profile(host,id).Behavior.AutoRespawn,"real auto-respawn setting uses the existing behavior owner");
                long beforeConfirmation=Profile(host,id).DataEpoch;click("request-reset/");
                var resetText=((IEnumerable)Get(window,"labels")).Cast<object>().Select(label=>(string)Get(label,"Text")).ToArray();
                Require(resetText.Contains("将清空物品、装备、成长和 AI 设置。")&&resetText.Contains("保留名字、外观、难度和归属。"),"reset preserves both original explanations before confirmation");
                click("cancel-reset/");Require(Profile(host,id).DataEpoch==beforeConfirmation,"wide confirmation cancellation performs no archive reset");
                directory("Connection");click("password/");rest();Require(GetOptional(window,"editor")!=null&&(bool)Get(window,"OwnsTextToken"),"password takes its actual existing text lease after the field is admitted");
                Queue("draft-only");rest();Require((string)Get(Get(window,"editor"),"Text")=="draft-only","native committed character queue edits the actual password draft");click("cancel-password/");Require((string)Get(host,"ConnectionPassword")==originalPassword&&!(bool)Get(window,"OwnsTextToken"),"cancel releases the lease and leaves the Host password unchanged");
                click("password/");rest();Queue("retained-local");rest();click("save-password/");Require((string)Get(host,"ConnectionPassword")=="retained-local","apply commits through the original password outlet");
                click("password/");rest();Require((string)Get(Get(window,"editor"),"Text")=="retained-local","reopening uses the accepted password, not a second configuration copy");Queue("uncommitted");rest();directory("Combat");Require(GetOptional(window,"editor")==null&&(string)Get(host,"ConnectionPassword")=="retained-local","directory switch cancels only the uncommitted editor and returns the text lease");
                long epoch=Profile(host,id).DataEpoch;click("restore-behavior/");var restored=Profile(host,id).Behavior;Require(restored.Follow&&restored.AttackEnabled&&!restored.SelfDefense&&restored.PreferOwner&&restored.AutoRespawn&&Profile(host,id).DataEpoch==epoch&&(string)Get(host,"ConnectionPassword")=="retained-local","global restore resets behavior only, preserving accepted password and character archive identity");
                directory("Connection");click("password/");rest();Queue("another draft");rest();click("profile:"+other+"/");Require(GetOptional(window,"editor")==null&&(Guid)Get(window,"selected")==other&&(string)Get(host,"ConnectionPassword")=="retained-local","object switch retires the previous field without changing the shared connection password");click("profile:"+id+"/");
                click("password/");rest();Queue("closing draft");rest();click("restore-behavior/");Require((string)Get(Get(window,"editor"),"Text")=="closing draft"&&(string)Get(host,"ConnectionPassword")=="retained-local","behavior restore preserves both accepted password and the unrelated uncommitted password edit");
                click("close/");Require(!(bool)Get(window,"Visible")&&GetOptional(window,"editor")==null&&!(bool)Get(window,"OwnsTextToken"),"actual close retires the old field and native text lease");
                Call(shell,"OpenCompanions",Get(Get(Get(shell,"State"),"Layout"),"Viewport"));rest();directory("Combat");
                CheckPressIdentity(window,host,id,body,matrix,screen);
                // Vanilla clamps its real UI scale. Independently exercise this
                // window's explicit narrow-matrix contract with direct window
                // input; this is not an accepted native scale/OS-input claim.
                screen=new Vector2(840,560);matrix=Matrix.CreateScale(1.5f);projected=true;Main.screenWidth=840;Main.screenHeight=560;PlayerInput.CacheOriginalScreenDimensions();Main.UIScale=1;prepare();directory("Character");
                var nameRect=Get(window,"configurationNameRect");
                Require(Terraria.GameContent.FontAssets.MouseText.Value.MeasureString(profileName).X*.85f>(float)Get(nameRect,"Width"),"the existing measured text renderer must ellipsize this actual twenty-character narrow name");
                frame(NativeToolsUiChecks.Point(nameRect),false,neutral,0);var nameHint=Get(window,"hint");Require((bool)Get(nameHint,"Visible")&&String.Concat(((IEnumerable)Get(nameHint,"Lines")).Cast<object>().Select(line=>(string)Get(line,"Text")))==profileName,"narrow name keeps its complete original through the existing hint path");
                graphics.Image(Path.Combine(output,"companion-config-narrow-name.png"),()=>Call(window,"Draw"),matrix,(int)screen.X,(int)screen.Y);
                var header=Get(window,"configurationIdentity");var nav=Get(Control(window,"config-tab:Character/"),"Rect");scrollTo("request-reset/");click("request-reset/");scrollTo("cancel-reset/");
                while((int)Get(window,"bodyOffset")<(int)Get(window,"bodyMaximum"))frame(NativeToolsUiChecks.Point(Get(window,"configurationViewport")),false,neutral,-120);
                Require((int)Get(window,"bodyOffset")>0&&Get(window,"configurationIdentity").Equals(header)&&Get(Control(window,"config-tab:Character/"),"Rect").Equals(nav),"narrow scrolling leaves the identity and directory baselines fixed");
                image("companion-config-narrow-reset");int expanded=(int)Get(window,"bodyOffset");click("cancel-reset/");Require(Profile(host,id).DataEpoch==epoch&&!(bool)Get(window,"confirmingReset")&&(int)Get(window,"bodyOffset")<expanded,"cancelling confirmation clamps the smaller measured content without resetting the profile");
                // Confirm through the actual button on the same real identity.
                scrollTo("request-reset/");click("request-reset/");scrollTo("confirm-reset/");click("confirm-reset/");
                NativeQuickItemChecks.Until(()=>{Call(host,"Update",(ulong)Main.GameUpdateCount);return !(bool)Call(host,"IsJoined",id)&&Profile(host,id).DataEpoch==epoch+1&&(bool)Call(archive,"IsSaved",id);});
                Require(Profile(host,id).Id==id&&Profile(host,id).Name==profileName&&(string)Get(host,"ConnectionPassword")=="retained-local","confirmed reset uses the existing archive owner once and leaves the global connection password alone");
                Console.WriteLine("PASS native configuration: one skin and content flow, measured navigation/inline options, actual field-preserving behavior edits, password queue/lease/cancel/apply/directory-object retirement, restore, reset confirmation and narrow scrolling; no OS IME/FPS or unrelated gameplay claim.");
            }
            finally {Call(window,"Close");imeField.SetValue(text,originalIme);Set(host,"ConnectionPassword",originalPassword);owner.name=ownerName;NativeToolsUiChecks.Mouse(Vector2.Zero,false);}
        }
        private static void CheckPressIdentity(object window,object host,Guid id,Player body,Matrix matrix,Vector2 screen)
        {
            bool dead=body.dead;int timer=body.respawnTimer,selected=body.selectedItem;var original=Profile(host,id).Behavior;
            Action prepare=()=>Call(window,"Prepare",matrix,screen);
            Action<Vector2,bool> sample=(point,left)=>{NativeToolsUiChecks.Mouse(point,left);Call(window,"ProcessLayoutInput",true,new KeyboardState(),point,matrix,screen,true,0);};
            try {
                body.dead=true;body.respawnTimer=122;prepare();var button=Control(window,"config-toggle:attack:");var point=NativeToolsUiChecks.Point(Get(button,"Rect"));sample(point,false);sample(point,true);var armed=Get(window,"armedKey");
                body.respawnTimer=61;prepare();Require(Equals(armed,Get(window,"armedKey")),"ordinary countdown refresh retains the same valid toggle operation");
                int builds=(int)Get(window,"LayoutBuilds");string oldName=body.inventory[selected].Name;body.selectedItemState.Select(selected==0?1:0);prepare();string newName=body.inventory[body.selectedItem].Name;
                var weaponLabel=((IEnumerable)Get(window,"labels")).Cast<object>().ElementAt((int)Get(window,"configurationWeaponLabel"));
                Require(oldName!=newName&&(string)Get(weaponLabel,"Text")==newName&&Equals(armed,Get(window,"armedKey"))&&(int)Get(window,"LayoutBuilds")==builds,"actual automatic weapon-slot change refreshes readonly text without rebuilding configuration or retiring its valid press");
                var changed=original.Clone();changed.AttackEnabled=!changed.AttackEnabled;Require((bool)Call(host,"SetBehavior",id,changed),"actual preference owner accepts an intervening change");prepare();Require(GetOptional(window,"armedKey")==null,"changed toggle target retires the old armed operation");sample(point,false);Require(Profile(host,id).Behavior.AttackEnabled==changed.AttackEnabled,"retired release does not reverse a newer accepted toggle value");
            }
            finally {body.dead=dead;body.respawnTimer=timer;body.selectedItemState.Select(selected);Call(host,"SetBehavior",id,original);prepare();NativeToolsUiChecks.Mouse(Vector2.Zero,false);}
        }
    }
}
