using System;
using System.Collections;
using System.IO;
using System.Linq;
using JueMingR.Features.Combat;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;
using static NativeWorldTextProbe.NativeToolsUiChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatVisualChecks
    {
        internal static void Run(object context,ProbeGraphics graphics,string output)
        {
            Directory.CreateDirectory(output);Terraria.Localization.LanguageManager.Instance.SetLanguage("zh-Hans");
            var shell=Get(context,"Shell");var state=Get(shell,"State");var combat=Get(context,"Combat");var renderer=Get(shell,"renderer");var drag=Get(shell,"CombatInterval");
            var settings=(CombatSettings)Get(combat,"Settings");NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return settings.Ready;});
            NativeCombatUiChecks.Run(context);Call(shell,"CloseAndSubmitPosition");
            foreach(var size in new[]{new[]{960,760,100},new[]{960,440,100},new[]{1440,900,150}})
            {
                Main.screenWidth=size[0];Main.screenHeight=size[1];PlayerInput.CacheOriginalScreenDimensions();Main.UIScale=size[2]/100f;
                UiFrame(context,Vector2.Zero,false);UiFrame(context,Vector2.Zero,false,Keys.F5);UiFrame(context,Vector2.Zero,false);Nav(context,8);
                var layout=Get(state,"Layout");var elements=((IEnumerable)Get(layout,"Elements")).Cast<object>().ToArray();
                string[] names={"自动连点","链球连击","光剑快切","完美左轮","省力魔法绳","自动转向","装备提示","自动汇报","哥布林必死"};
                var labels=names.Select(n=>elements.Single(e=>(string)GetOptional(e,"Text")==n)).ToArray();
                Require(labels.Zip(labels.Skip(1),(a,b)=>(float)Get(Get(a,"Rect"),"Y")<(float)Get(Get(b,"Rect"),"Y")).All(x=>x),"nine actual rows retain Legacy relative order");
                var field=elements.Single(e=>Get(e,"Command").ToString()=="CombatInterval");
                Require((float)Get(Get(field,"Rect"),"Width")-66==108,"owner-requested track doubles from 54 to 108 logical pixels");
                Require(Math.Abs(CenterY(field)-CenterY(labels[2]))<.01f,"interval remains vertically centered in quick-switch row");
                Image("page");
                Observation(context,graphics,output,size);
                if(size[1]==760)
                {
                    string[] descriptions={"补全原版不支持连点的物品","长按右键触发连击","按住右键快切快捷栏的光剑","按住左键最大程度发挥左轮威力","装备魔法绳后长按左键实现连点效果","固定方向的武器可以随时转头了","还得是穿渔夫套打boss","boss战结束后自动汇报","rnm 还钱！！"};
                    for(int i=0;i<labels.Length;i++)
                    {
                        UiFrame(context,Position(labels[i]),false);Image("description-"+i);
                        var hint=Get(renderer,"HintLayout");string shown=string.Concat(((IEnumerable)Get(hint,"Lines")).Cast<object>().Select(e=>(string)Get(e,"Text")));
                        Require((bool)Get(hint,"Visible") && shown==descriptions[i],"owner-approved actual hover text for "+names[i]+": "+shown);
                    }
                    foreach(var element in elements.Where(e=>Get(e,"Command").ToString().StartsWith("Combat") && Get(e,"Command").ToString().EndsWith("On")))
                    {
                        string command=Get(element,"Command").ToString();Click(context,Position(element));
                        NativeQuickItemChecks.Until(()=>{UiFrame(context,Vector2.Zero,false);return settings.Ready;});
                        Require(settings.CompletionSucceeded,"real F5 button commits "+command);
                    }
                    Require(settings.Value.EnabledMask==255,"all eight actual buttons update independent persisted settings");
                    NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                    foreach(int value in new[]{0,12,30})
                    {
                        EnsureVisible(field);
                        UiFrame(context,Vector2.Zero,false);var track=Get(drag,"Track");Vector2 at=(new Vector2((float)Get(track,"X")+(float)Get(track,"Width")*value/30,(float)Get(track,"Y")+7))*Main.UIScale;
                        Vector2 pressAt=new Vector2((float)Get(track,"X")+(float)Get(track,"Width")/2,(float)Get(track,"Y")+7)*Main.UIScale;
                        long before=settings.AcceptedCommandId;UiFrame(context,pressAt,true);for(int i=0;i<8;i++)UiFrame(context,at,true);
                        Require((bool)Get(drag,"Captured") && settings.AcceptedCommandId==before,"drag preview never saves on held samples, value="+value+" capture="+Get(drag,"Captured")+" accepted="+settings.AcceptedCommandId+" before="+before);
                        UiFrame(context,at,false);NativeQuickItemChecks.Until(()=>{UiFrame(context,Vector2.Zero,false);return settings.Ready;});
                        Require(settings.Value.SwitchInterval==value && settings.AcceptedCommandId==before+1,"focused release saves exactly once at "+value);
                    }
                    UiFrame(context,Position(field),false);Image("interval-hint");Require((bool)Get(Get(renderer,"HintLayout"),"Visible"),"actual interval field has visible help");
                    var trackNow=Get(drag,"Track");Vector2 middle=new Vector2((float)Get(trackNow,"X")+(float)Get(trackNow,"Width")/2,(float)Get(trackNow,"Y")+7)*Main.UIScale;
                    long accepted=settings.AcceptedCommandId;UiFrame(context,middle,true);UiFrame(context,middle,true,Keys.Escape);UiFrame(context,Vector2.Zero,false,Keys.Escape);
                    Require(!(bool)Get(drag,"Captured") && settings.AcceptedCommandId==accepted && (bool)Get(Get(Get(context,"Input"),"Hotkeys"),"HasSuppressedKeys"),"Escape cancels draft and retains key tail beyond mouse release");
                    UiFrame(context,Vector2.Zero,false);UiFrame(context,middle,true);graphics.SetMouseFont(Terraria.GameContent.FontAssets.ItemStack.Value);UiFrame(context,middle,false);
                    Require(!(bool)Get(drag,"Captured") && settings.AcceptedCommandId==accepted,"font identity change cancels stale geometry before release");graphics.SetMouseFont(graphics.Font);UiFrame(context,Vector2.Zero,false);
                    var hotkey=elements.Single(e=>(string)GetOptional(e,"HotkeyTarget")=="combat.quick-switch");Vector2 keyPoint=Position(hotkey);Click(context,keyPoint);Click(context,keyPoint);
                    var popup=Get(shell,"HotkeyPopup");Require((bool)Get(popup,"Visible") && (string)Get(popup,"Target")=="combat.quick-switch","actual adjacent binding opens its own action");
                    PopupClick(context,popup,"Record");UiFrame(context,Vector2.Zero,false,Keys.LeftControl);UiFrame(context,Vector2.Zero,false,Keys.LeftControl,Keys.F9);UiFrame(context,Vector2.Zero,false);
                    var bindings=(HotkeyBindings)Get(Get(shell,"hotkeys"),"Bindings");NativeQuickItemChecks.Until(()=>{UiFrame(context,Vector2.Zero,false);return !bindings.Busy;});
                    Require(bindings.CompletionSucceeded && bindings.Get("combat.quick-switch").MainKey==(int)Keys.F9,"real shared binding capture/save succeeds");Image("binding");PopupClick(context,popup,"Close");
                }
                var view=Get(layout,"Viewport");Vector2 inside=(Point(view)+new Vector2((float)Get(state,"X"),(float)Get(state,"Y")))*Main.UIScale;
                for(int i=0;i<30 && (float)Get(state,"Scroll")<(float)Get(layout,"MaxScroll");i++)UiFrame(context,inside,false,new Keys[0],-120);
                Image("bottom");var last=elements.Single(e=>Get(e,"Command").ToString()=="CombatGoblinOn");Click(context,Position(last));
                NativeQuickItemChecks.Until(()=>{UiFrame(context,Vector2.Zero,false);return settings.Ready;});Require(settings.Value.Enabled(7),"bottom row remains actionable after real scroll/scale");
                NativeCombatCadenceChecks.Save(combat,new CombatOptions());Call(shell,"CloseAndSubmitPosition");UiFrame(context,Vector2.Zero,false);
                Vector2 Position(object element)
                {EnsureVisible(element);var viewport=Get(Get(state,"Layout"),"Viewport");return (Point(Get(element,"Rect"))+new Vector2((float)Get(state,"X")+(float)Get(viewport,"X"),(float)Get(state,"Y")+(float)Get(viewport,"Y")-(float)Get(state,"Scroll")))*Main.UIScale;}
                void EnsureVisible(object element){ScrollTo(context,state,element);}
                void Image(string name){graphics.Image(Path.Combine(output,"combat-"+name+"-"+size[0]+"-"+size[1]+"-"+size[2]+".png"),()=>Call(shell,"DrawLayer"),Main.UIScaleMatrix,size[0],size[1]);}
            }
            World(context,graphics,output);
            Console.WriteLine("PASS G11A actual F5 rows/buttons, interval release/cancel/geometry, binding capture/save, original fonts and scroll at 100/150 percent.");
        }
        private static float CenterY(object element){var rect=Get(element,"Rect");return (float)Get(rect,"Y")+(float)Get(rect,"Height")/2;}
        private static void World(object context,ProbeGraphics graphics,string output)
        {
            var host=Get(context,"CombatObservation");var layer=Get(host,"World");Set(host,"LayerStatus",Enum.Parse(Get(host,"LayerStatus").GetType(),"Ready"));
            Main.screenWidth=960;Main.screenHeight=640;Main.UIScale=1;PlayerInput.CacheOriginalScreenDimensions();Main.screenPosition=new Vector2(300,300);Main.hideUI=false;Main.mapFullscreen=false;Main.dayTime=false;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(collision:true,path:true));
            foreach(var n in Main.npc)n.active=false;var npc=Main.npc[0];npc.SetDefaults(2);npc.whoAmI=0;npc.active=true;npc.target=0;npc.position=new Vector2(480,440);npc.velocity=new Vector2(2,-1);npc.timeLeft=750;
            Main.LocalPlayer.position=new Vector2(650,640);UiFrame(context,Vector2.Zero,false);NativeCombatObservationChecks.Fresh(context,host);
            var shot=Main.projectile[10];shot.SetDefaults(632);shot.whoAmI=10;shot.active=true;shot.owner=0;shot.friendly=true;shot.damage=10;shot.position=new Vector2(-300,550);shot.velocity=Vector2.UnitX;shot.localAI[1]=1500;shot.scale=1;shot.Damage();
            var cache=(JueMingR.Features.Combat.NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");int steps=cache.Steps;Call(layer,"Prepare");Require((int)Get(layer,"StrokeCount")>10,"actual world preparation keeps offscreen-origin beam and selected future path; strokes="+Get(layer,"StrokeCount")+" canDraw="+Get(host,"CanDraw")+" enabled="+Get(host,"Enabled")+" path="+(cache.Read(0)?.Count.ToString()??"none")+" failed="+Call(host,"Unavailable",0));
            for(int i=0;i<100;i++)graphics.Pixels(()=>Call(layer,"Draw"),Main.GameViewMatrix.ZoomMatrix);
            Require(cache.Steps==steps,"repeated actual draw cannot run NPC prediction");
            graphics.Image(Path.Combine(output,"observation-world.png"),()=>Call(layer,"Draw"),Main.GameViewMatrix.ZoomMatrix);
            Main.LocalPlayer.gravDir=-1;Main.screenPosition+=new Vector2(70,30);Call(layer,"Prepare");graphics.Image(Path.Combine(output,"observation-world-inverted.png"),()=>Call(layer,"Draw"),Main.GameViewMatrix.ZoomMatrix);Main.LocalPlayer.gravDir=1;
            npc.SetDefaults(371);npc.whoAmI=0;npc.active=true;npc.position=new Vector2(700,650);npc.target=0;npc.ai[3]=1;
            int priorMode=Main.netMode;try{Main.netMode=1;NativeCombatObservationChecks.Fresh(context,host);Call(layer,"Prepare");string shown=(string)Get(layer,"pathText");Require(shown.Contains("随机代表路线") && shown.Contains("依据本机网络观察") && shown.Contains("玩家保持当前位置"),"actual rendered path text retains random, network and player assumptions");}finally{Main.netMode=priorMode;}
            NativeCombatObservationChecks.Save(host,new ObservationOptions());Call(layer,"Prepare");Require((int)Get(layer,"StrokeCount")==0,"display off retires prepared geometry");
        }
        private static void ScrollTo(object context,object state,object element)
        {
            var rect=Get(element,"Rect");float y=(float)Get(rect,"Y"),h=(float)Get(rect,"Height");var view=Get(Get(state,"Layout"),"Viewport");
            for(int i=0;i<80;i++)
            {
                float scroll=(float)Get(state,"Scroll"),height=(float)Get(view,"Height");if(y>=scroll && y+h<=scroll+height)return;
                var inside=(Point(view)+new Vector2((float)Get(state,"X"),(float)Get(state,"Y")))*Main.UIScale;
                UiFrame(context,inside,false,new Keys[0],y<scroll?120:-120);
            }
            Require(false,"real wheel must reveal "+Get(element,"Command"));
        }
        private static void Observation(object context,ProbeGraphics graphics,string output,int[] size)
        {
            var shell=Get(context,"Shell");var state=Get(shell,"State");var host=Get(context,"CombatObservation");var settings=(ObservationSettings)Get(host,"Settings");var drag=Get(shell,"CombatRadius");
            NativeQuickItemChecks.Until(()=>{UiFrame(context,Vector2.Zero,false);return settings.Ready;});
            var elements=((IEnumerable)Get(Get(state,"Layout"),"Elements")).Cast<object>().ToArray();
            object Find(string command){return ((IEnumerable)Get(Get(state,"Layout"),"Elements")).Cast<object>().Single(e=>Get(e,"Command").ToString()==command);}
            Vector2 Position(object e){ScrollTo(context,state,e);var v=Get(Get(state,"Layout"),"Viewport");return (Point(Get(e,"Rect"))+new Vector2((float)Get(state,"X")+(float)Get(v,"X"),(float)Get(state,"Y")+(float)Get(v,"Y")-(float)Get(state,"Scroll")))*Main.UIScale;}
            void Apply(string command){Click(context,Position(Find(command)));NativeQuickItemChecks.Until(()=>{UiFrame(context,Vector2.Zero,false);return settings.Ready;});Require(settings.CompletionSucceeded,"actual observation command saved: "+command);}
            Require(elements.Count(e=>(string)GetOptional(e,"Text")=="辅助瞄准设置")==1,"one real shared card");
            var title=elements.Single(e=>(string)GetOptional(e,"Text")=="辅助瞄准设置");var policy=Get(Find("ObservationPolicy"),"Rect");var dummy=Get(Find("ObservationDummy"),"Rect");
            Require(Math.Abs(CenterY(title)-CenterY(Find("ObservationPolicy")))<1 && (float)Get(dummy,"Y")== (float)Get(policy,"Y"),"title and all three cycling controls share one compact row");
            Require(510-(float)Get(dummy,"X")-(float)Get(dummy,"Width")>=(float)Get(dummy,"Width")+8,"first row reserves room for a future fourth real control");
            Require(CenterY(Find("ObservationCollisionOn"))<CenterY(Find("ObservationPathOn")) && CenterY(Find("ObservationPathOn"))<CenterY(Find("CombatAutoClickOn")),"two independent display rows precede existing controls");
            Apply("ObservationPolicy");Apply("ObservationCenter");Apply("ObservationDummy");Require(settings.Value.ClearLine && settings.Value.MouseCenter && settings.Value.Dummy,"shared choices actually persisted");
            var field=Find("ObservationRadius");
            foreach(int value in new[]{0,25,50})
            {
                Position(field);UiFrame(context,Vector2.Zero,false);var track=Get(drag,"Track");
                Vector2 center=new Vector2((float)Get(track,"X")+(float)Get(track,"Width")*.5f,(float)Get(track,"Y")+7)*Main.UIScale;
                Vector2 at=new Vector2((float)Get(track,"X")+(float)Get(track,"Width")*value/50,(float)Get(track,"Y")+7)*Main.UIScale;
                long accepted=settings.AcceptedCommandId;UiFrame(context,center,true);for(int i=0;i<5;i++)UiFrame(context,at,true);
                Require((bool)Get(drag,"Captured") && settings.AcceptedCommandId==accepted,"radius held preview does not write");UiFrame(context,at,false);
                NativeQuickItemChecks.Until(()=>{UiFrame(context,Vector2.Zero,false);return settings.Ready;});Require(settings.Value.Radius==value && settings.AcceptedCommandId==accepted+1,"radius real release saves once");
            }
            Position(field);var current=Get(drag,"Track");var mid=new Vector2((float)Get(current,"X")+(float)Get(current,"Width")*.5f,(float)Get(current,"Y")+7)*Main.UIScale;long before=settings.AcceptedCommandId;
            UiFrame(context,mid,true);UiFrame(context,mid,true,Keys.Escape);UiFrame(context,mid,false);UiFrame(context,mid,false);
            Require(!(bool)Get(drag,"Captured") && settings.AcceptedCommandId==before,"radius Escape cancels without saving");
            UiFrame(context,mid,true);graphics.SetMouseFont(Terraria.GameContent.FontAssets.ItemStack.Value);UiFrame(context,mid,false);Require(settings.AcceptedCommandId==before,"radius font geometry replacement cancels stale press");graphics.SetMouseFont(graphics.Font);UiFrame(context,Vector2.Zero,false);
            Apply("ObservationCenter");Require(settings.Value.Radius==50,"changing center preserves mouse preference");
            Apply("ObservationCollisionOn");Apply("ObservationPathOn");Apply("ObservationCollisionOff");Require(!settings.Value.Collision && settings.Value.Path,"display switches are independent");
            Apply("ObservationPathOff");
            if(size[1]==760)
            {
                var bindings=(HotkeyBindings)Get(Get(shell,"hotkeys"),"Bindings");int k=0;
                foreach(string id in new[]{"combat.collision-display","combat.npc-path"})
                {
                    var key=elements.Single(e=>(string)GetOptional(e,"HotkeyTarget")==id);Vector2 at=Position(key);Click(context,at);Click(context,at);var popup=Get(shell,"HotkeyPopup");Require((string)Get(popup,"Target")==id,"own binding popup target");
                    PopupClick(context,popup,"Record");UiFrame(context,Vector2.Zero,false,Keys.LeftControl);UiFrame(context,Vector2.Zero,false,Keys.LeftControl,k==0?Keys.F7:Keys.F8);UiFrame(context,Vector2.Zero,false);
                    NativeQuickItemChecks.Until(()=>{UiFrame(context,Vector2.Zero,false);return !bindings.Busy;});Require(bindings.CompletionSucceeded && bindings.Get(id).MainKey==(int)(k++==0?Keys.F7:Keys.F8),"independent observation binding saved");PopupClick(context,popup,"Close");
                }
            }
            NativeCombatObservationChecks.Save(host,new ObservationOptions());UiFrame(context,Vector2.Zero,false);Position(Find("ObservationPolicy"));
            Require((string)Get(Find("ObservationPolicy"),"Text")=="最近优先" && (string)Get(Find("ObservationCenter"),"Text")=="玩家中心" && (string)Get(Find("ObservationDummy"),"Text")=="追踪人偶：关","actual cycle labels reflect committed values after reset");
            graphics.Image(Path.Combine(output,"observation-card-"+size[0]+"-"+size[1]+"-"+size[2]+".png"),()=>Call(shell,"DrawLayer"),Main.UIScaleMatrix,size[0],size[1]);
        }
    }
}
