using System;
using System.Collections;
using System.Linq;
using JueMingR.Features.Combat;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Combat;
using Terraria;
using Microsoft.Xna.Framework;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatUiChecks
    {
        private static void HiddenPath(object context)
        {
            var host=Get(context,"CombatObservation");var g=new NativeHiddenEntryChecks.Gesture(context);
            var registry=(HotkeyRegistry)Get(Get(g.Shell,"hotkeys"),"Registry");var action=registry.Find("combat.npc-path");
            var settings=(ObservationSettings)Get(host,"Settings");var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");
            int mode=Main.netMode;int connection=Netplay.Connection.State;
            try
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));
                Require(!(bool)Get(host,"PathAccess") && settings.Value.Path && !(bool)Get(host,"Path") && (bool)Get(host,"Enabled"),"Locked preserves true preference and public Aim while suppressing independent Path");
                g.Open(8);Require(!g.Has("ObservationPathOn") && g.Has("ObservationAimOn") && g.Has("ObservationCollisionOn"),"public combat has no Path row");
                g.Frame(940,620);int lockedGeneration=(int)Get(g.Layout,"Generation"),lockedMeasures=(int)Get(g.Layout,"MeasurementCount");long lockedRevision=registry.Revision;
                for(int i=0;i<60;i++)g.Frame(940,620);
                Require((int)Get(g.Layout,"Generation")==lockedGeneration && (int)Get(g.Layout,"MeasurementCount")==lockedMeasures && registry.Revision==lockedRevision,"Locked stable samples do not rebuild layout, metrics or registry");
                Console.WriteLine("HIDDEN STABLE locked frames=60 layoutDelta={0} measureDelta={1} registryDelta={2}",(int)Get(g.Layout,"Generation")-lockedGeneration,(int)Get(g.Layout,"MeasurementCount")-lockedMeasures,registry.Revision-lockedRevision);
                g.Open(1);Require(!g.Has("ObservationPathOn") && !g.Has("EnableJourney"),"Locked Misc has neither hidden row");
                float locked=(float)Get(g.Layout,"ContentHeight");Call(g.Layout,"SetMiscFooterStart",123f);Require((float)Get(g.Layout,"ContentHeight")==117f,"Locked dynamic footer reserves no rows");
                Require(action==null && registry.Find("combat.aim")!=null && registry.Find("combat.collision-display")!=null,"Locked publishes no Path action while ordinary combat actions remain registered");
                long accepted=settings.AcceptedCommandId;Call(host,"Set",1,false);Require(settings.Value.Path && settings.AcceptedCommandId==accepted,"direct field1 cannot bypass qualification or erase preference");
                cache.Demand(2,1,12);Call(host,"Poll");Require(cache.Required==12,"Locked Path cannot release another prediction reader");cache.Release(2);
                g.Reset();for(int i=0;i<5;i++)g.ClickTitle();Call(host,"Poll");
                Require((bool)Get(host,"PathAccess") && settings.Value.Path && (bool)Get(host,"Path") && settings.AcceptedCommandId==accepted,"fifth consumed release restores saved true with no activation bit or toggle command");
                Require((bool)Get(g.State,"ConsumeLeft") && !Main.mouseLeft,"reveal tail cannot reach hidden command");
                action=registry.Find("combat.npc-path");Require(action!=null,"fifth release publishes the original Path action ID");
                CheckCombatProjection(g);CheckHiddenNames(g);
                Main.netMode=1;Netplay.Connection.State=10;
                Require(action.CanConfigure && (bool)Get(host,"Path") && !(bool)Get(Get(context,"Journey"),"CanEnable"),"ordinary multiplayer Path qualification remains independent of Journey singleplayer gate");
                foreach(var geometry in new[]{new[]{960,640,100},new[]{960,360,100},new[]{1440,900,150}})
                {
                    g.Geometry(geometry[0],geometry[1],geometry[2]/100f);g.Open(1);g.Frame(940,620);
                    Require(!g.Has("ObservationPathOn") && g.Has("EnableJourney") && g.Has("SummonMerchant"),"Misc retains Journey and merchant only after real resize/rebuild");
                    Call(g.Layout,"SetMiscFooterStart",123f);Require(!g.Has("ObservationPathOn") && g.Has("EnableJourney"),"dynamic Misc footer also excludes Path");
                    g.Open(8);Require(g.Has("ObservationPathOn") && !g.Has("EnableJourney") && !g.Has("SummonMerchant"),"return to Combat keeps its single Path row");
                    g.Click(g.Find("ObservationPathOff"));NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return settings.Ready;});Require(!settings.Value.Path,"real scrolled Shell OFF reaches stable field1");
                    g.Click(g.Find("ObservationPathOn"));NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return settings.Ready;});Require(settings.Value.Path,"real scrolled Shell ON reaches stable field1");
                    int generation=(int)Get(g.Layout,"Generation"),measure=(int)Get(g.Layout,"MeasurementCount");long revision=registry.Revision;for(int i=0;i<60;i++)g.Frame(940,620);
                    Require(generation==(int)Get(g.Layout,"Generation") && measure==(int)Get(g.Layout,"MeasurementCount") && revision==registry.Revision,"stable hidden Combat does not rebuild layout, remeasure or republish the registry");
                    Console.WriteLine("HIDDEN STABLE qualified geometry={0}x{1}/{2} frames=60 layoutDelta={3} measureDelta={4} registryDelta={5}",geometry[0],geometry[1],geometry[2],(int)Get(g.Layout,"Generation")-generation,(int)Get(g.Layout,"MeasurementCount")-measure,registry.Revision-revision);
                }
                Require(action.Invoke(HotkeyContext.Multiplayer),"same dynamically published Gameplay action executes on ordinary client");NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return settings.Ready;});Require(!settings.Value.Path,"hotkey and F5 use same saved switch");
                Call(host,"Set",1,true);NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return settings.Ready;});
                var controls=Get(Get(g.Shell,"renderer"),"CombatObservationControls");var command=g.Find("ObservationPathOn");Set(host,"pathFailed",true);
                Require((Color)Call(controls,"Selected",Get(command,"Command"))==Color.Goldenrod && Call(controls,"Hint",Get(command,"Command"))!=null && settings.Value.Path && !(bool)Get(host,"Path"),"fault preserves selected saved ON with unavailable reason");Set(host,"pathFailed",false);
                Call(host,"OnSessionEnded");Require((bool)Get(host,"PathAccess") && settings.Value.Path,"world retirement rebuilds content without relocking or clearing preference");
                Console.WriteLine("PASS HIDDEN PATH: original fifth-release chain; Locked absent action/saved preference; Combat classification and same-page qualification reflow; both Misc builders; gold-name semantics/shared hover; real scrolled clicks at three sizes; stable layout/registry; multiplayer action/Journey split; fault and Session preference boundaries.");
            }
            finally{Main.netMode=mode;Netplay.Connection.State=connection;g.Restore();NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
        private static void CheckCombatProjection(NativeHiddenEntryChecks.Gesture g)
        {
            g.Open(8);var renderer=Get(g.Shell,"renderer");
            float width=(float)Get(g.Layout,"screenWidth"),height=(float)Get(g.Layout,"screenHeight"),scale=(float)Get(g.Layout,"uiScale");
            // Reproject the real page in place. The process qualification and
            // saved options stay owned by their existing objects throughout.
            Call(g.Layout,"SetHiddenFeaturesVisible",false);Call(renderer,"Prepare",g.State,width,height,scale);
            Require(!g.Has("ObservationPathOn"),"locked projection removes Path from the existing Combat page");int generation=(int)Get(g.Layout,"Generation");
            Call(g.Layout,"SetHiddenFeaturesVisible",true);Call(renderer,"Prepare",g.State,width,height,scale);
            Require(g.Has("ObservationPathOn") && (int)Get(g.Layout,"Generation")==generation+1,"same-page qualification changes rebuild Combat exactly once");
            Require(g.Elements.Count(e=>Get(e,"Command").ToString()=="ObservationPathOn")==1,"same-page rebuild has one Path row");
        }
        private static void CheckHiddenNames(NativeHiddenEntryChecks.Gesture g)
        {
            var renderer=Get(g.Shell,"renderer");
            var color=renderer.GetType().GetMethod("TextColor",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            Require(color!=null,"built renderer has the metadata color path");
            foreach(int page in new[]{8,1})
            {
                g.Open(page);
                foreach(string label in page==8?new[]{"寻路预测"}:new[]{"旅行菜单","游商测试"})
                {
                    var name=g.Elements.Single(e=>(string)GetOptional(e,"Text")==label);var description=Get(name,"Description");
                    Require(Get(description,"NameStyle").ToString()=="Hidden" && (Color)color.Invoke(null,new[]{name})==Color.Gold,"only the declared hidden name resolves to gold: "+label);
                    var rect=Get(name,"Rect");var view=Get(g.Layout,"Viewport");Call(g.State,"ScrollTo",Math.Max(0,(float)Get(rect,"Bottom")-(float)Get(view,"Height")+4));
                    g.Point(name);g.Sample(false);
                    string hint=(string)Call(renderer,"ResolveHint",g.State,null,false,false,null,null);
                    Require(hint==(string)Get(description,"Text") && Get(g.State,"Command").ToString()=="None","hidden name uses standard hover with no command: "+label);
                    Console.WriteLine("HIDDEN NAME page={0} label={1} style={2} tint={3} hint={4}",page,label,Get(description,"NameStyle"),color.Invoke(null,new[]{name}),hint);
                }
                foreach(var element in g.Elements.Where(e=>Get(e,"Kind").ToString()=="Button" || Get(e,"Kind").ToString()=="Hotkey"))
                    Require(GetOptional(element,"Description")==null && (Color)color.Invoke(null,new[]{element})==Color.White,"gold name metadata does not tint buttons or hotkey slots");
                if(page==8)
                {
                    var ordinary=g.Elements.Single(e=>(string)GetOptional(e,"Text")=="碰撞箱显示");
                    Require(Get(Get(ordinary,"Description"),"NameStyle").ToString()=="Standard" && (Color)color.Invoke(null,new[]{ordinary})==Color.White,"ordinary combat name keeps normal foreground");
                }
            }
        }
        internal static void Run(object context)
        {
            HiddenPath(context);
            var shell=Get(context,"Shell");var registry=(HotkeyRegistry)Get(Get(shell,"hotkeys"),"Registry");
            foreach(string id in new[]{"auto-click","flail","quick-switch","revolver","magic-string","facing","report","goblin"})
                Require(registry.Find("combat."+id)!=null,"complete frozen registry needs combat."+id);
            Require(GetOptional(Get(shell,"renderer"),"CombatControls")!=null && GetOptional(shell,"CombatInterval")!=null,"actual combat UI and interval owner are attached");
            Require(GetOptional(Get(shell,"renderer"),"CombatObservationControls")!=null && GetOptional(shell,"CombatRadius")!=null,"actual shared card, display rows and radius owner are attached");
            foreach(string id in new[]{"combat.collision-display","combat.npc-path"})Require(registry.Find(id)!=null,"independent display binding registered: "+id);
            Require(registry.Find("combat.aim")!=null,"standard independent aim binding is registered");
            var host=Get(context,"CombatObservation");var status=Get(host,"LayerStatus");
            var state=Get(shell,"State");var renderer=Get(shell,"renderer");Call(state,"Navigate",8);Call(state,"RestoreVisible");Call(renderer,"RefreshResources");
            Action prepare=()=>Call(renderer,"Prepare",state,960f,640f,1f);
            Func<object[]> elements=()=>((IEnumerable)Get(Get(state,"Layout"),"Elements")).Cast<object>().ToArray();
            NativeCombatObservationChecks.Save(host,new ObservationOptions());prepare();var closed=elements();float closedHeight=(float)Get(Get(state,"Layout"),"ContentHeight");
            Require(closed.Any(e=>Get(e,"Command").ToString()=="ObservationAimOn") && !closed.Any(e=>Get(e,"Command").ToString()=="ObservationRadius"),"default OFF has standard row and no hidden card/hit targets");
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,false,true,true,true,50,true,true));prepare();
            Require(elements().Count(e=>Get(e,"Command").ToString()=="ObservationRadius")==1 && (float)Get(Get(state,"Layout"),"ContentHeight")>closedHeight,"committed ON expands one card and content height");
            Call(state,"ScrollTo",float.MaxValue);
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,true,true,50,true,false));prepare();
            Require(!elements().Any(e=>Get(e,"Command").ToString()=="ObservationRadius") && (float)Get(state,"Scroll")<=(float)Get(Get(state,"Layout"),"MaxScroll"),"fold removes hit targets and clamps scroll");
            var options=((ObservationSettings)Get(host,"Settings")).Value;
            Require(options.Path && options.Marker && options.ClearLine && options.MouseCenter && options.Dummy && options.Radius==50,"fold retains shared policies, marker and independent path");
            Require(elements().Count(e=>Get(e,"Command").ToString()=="ObservationPathOn")==1 && elements().Any(e=>Get(e,"Command").ToString()=="ObservationCollisionOn"),"qualified Combat keeps one Path row alongside Collision even with Aim folded");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());Call(shell,"CloseAndSubmitPosition");
            Set(host,"LayerStatus",Enum.Parse(status.GetType(),"Unavailable"));
            Require(Call(host,"Unavailable",0)!=null && Call(host,"Unavailable",1)!=null && !(bool)Get(host,"Collision") && !(bool)Get(host,"Path"),"permanent presentation failure cannot masquerade as an effective display");
            Set(host,"LayerStatus",status);
            Console.WriteLine("PASS G11A eight shared hotkeys and actual combat page controls composed.");
        }
    }
}
