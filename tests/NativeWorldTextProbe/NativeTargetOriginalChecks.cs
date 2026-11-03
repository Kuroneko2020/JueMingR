using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Reuses initialized native Main/real Draw and explicit caller-first JIT.
    // No Targets certificate callback, completion bit or frozen target is set.
    internal static class NativeTargetOriginalChecks
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private static int messages;
        private static string lastMessage;
        private static bool Chat(ChatMessage __0) { messages++; lastMessage=__0.Text; return false; }
        internal static void Run(object context, Action<Keys[]> step, Action draw, Action<int,int,bool> mouse)
        {
            var browser = Get(context,"Browser"); var targets = Get(browser,"Targets"); var shell = Get(context,"Shell");
            var state = Get(shell,"State"); var input = Get(context,"Input"); var native = Get(Get(browser,"Knowledge"),"Native");
            var workspace = Get(Get(browser,"Knowledge"),"Workspace"); var announcements = Get(browser,"Announcements");
            var bindings = (HotkeyBindings)Get(Get(shell,"hotkeys"),"Bindings");
            var blueprint = Get(context,"Blueprint"); var proof = Get(blueprint,"Pointer");
            var capture = (JueMingR.Features.Blueprint.BlueprintCaptureState)Get(blueprint,"State");
            var outlet = new Harmony("JueMingR.Tests.TargetOriginal.Chat");
            var incoming = typeof(ChatCommandProcessor).GetMethod("ProcessIncomingMessage",F);
            Action neutral = () => { step(Array.Empty<Keys>()); step(Array.Empty<Keys>()); };
            Action<Keys> press = key => { step(new[]{key}); Witness("request",targets,input,shell,browser);draw();Witness("draw",targets,input,shell,browser);step(Array.Empty<Keys>());Witness("delivery",targets,input,shell,browser); };
            Action<string,string> bind = (id,text) =>
            {
                HotkeyChord chord; string reason; long command;
                Require(HotkeyChord.TryParse(text,out chord,out reason),"real target shortcut parses");
                Require(bindings.TrySet(id,chord,null,out command,out reason),"isolated target shortcut saves: "+reason);
                NativeQuickItemChecks.Until(()=>{bindings.Poll();return !bindings.Busy;});
            };
            Action close = () => { Call(shell,"CloseAndSubmitPosition"); Main.LocalPlayer.mouseInterface=Main.mouseText=false; };
            Action<int,int,bool> sample = (x,y,down) => { mouse(x,y,down); step(Array.Empty<Keys>()); draw(); };
            try
            {
                var picked=(Delegate)Get(targets,"Picked");var argument=Expression.Parameter(picked.GetType().GetGenericArguments()[0]);
                Action<object> observePicked=value=>
                {
                    Console.WriteLine("TARGET ORIGINAL Picked type="+Get(value,"ItemType")+" stateReady="+Get(state,"Ready"));
                    picked.DynamicInvoke(value);
                    Console.WriteLine("TARGET ORIGINAL Picked exit selected="+Get(workspace,"Selected")+" visible="+Get(state,"Visible")+" page="+Get(state,"Page"));
                };
                Set(targets,"Picked",Expression.Lambda(picked.GetType(),Expression.Invoke(Expression.Constant(observePicked),Expression.Convert(argument,typeof(object))),argument).Compile());
                Require((bool)Get(targets,"Ready"),"real Targets resource hooks installed");
                NativeQuickItemChecks.Until(()=>{Call(context,"UpdateShell");return (bool)Get(state,"Ready");});
                NativeQuickItemChecks.Until(()=>{step(Array.Empty<Keys>());return (bool)Get(blueprint,"ProjectionPersistenceReady");});
                NativeQuickItemChecks.Until(()=>{Call(announcements,"Update");bindings.Poll();return bindings.Loaded&&(bool)Get(announcements,"CanConfigure");});
                bind("item-browser.query","K"); bind("announcement.send","L");
                Call(announcements,"SetEnabled",true); close(); Main.netMode=0; Main.playerInventory=true;Main.UIScale=1f;
                Main.LocalPlayer.inventory[10].SetDefaults(ItemID.DirtBlock); Main.LocalPlayer.inventory[10].stack=3;
                Main.LocalPlayer.inventory[11].TurnToAir();
                mouse((int)(40*Main.UIScale),(int)(88*Main.UIScale),false);neutral();draw();
                var styles = new System.Collections.Generic.HashSet<string>();
                for(int i=0;i<6;i++)
                {
                    styles.Add(Main.ResourceSetsManager.ActiveSetKeyName); Call(workspace,"Navigate",ItemID.Wood,false);
                    close();neutral();press(Keys.K);
                    Require(!(bool)Get(targets,"Busy") && (int)Get(workspace,"Selected")==ItemID.DirtBlock && (bool)Get(state,"Visible"),
                        "caller-first real slot query reaches workspace for resource style "+Main.ResourceSetsManager.ActiveSetKeyName);
                    Main.ResourceSetsManager.CycleResourceSet();
                }
                Require(styles.Count==6,"six native styles cover the three resource originals");
                close();mouse((int)(92*Main.UIScale),(int)(88*Main.UIScale),false);neutral();SeedWorldItem();draw();press(Keys.K);
                Require((int)Get(workspace,"Selected")==ItemID.DirtBlock,"real empty inventory slot blocks world fallthrough");
                // Query-page pick invokes its actual configured user action,
                // then accepts a newly released/pressed real mapped Mouse1.
                close();Main.playerInventory=true;mouse((int)(40*Main.UIScale),(int)(88*Main.UIScale),false);neutral();draw();
                var page=Get(browser,"page"); ((Action)Get(page,"PickRequested"))();
                Call(workspace,"Navigate",ItemID.Wood,false);
                sample(40,88,false);sample(40,88,false);sample(40,88,true);sample(40,88,false);neutral();
                Require(!(bool)Get(targets,"Busy")&&(bool)Get(state,"Visible")&&(int)Get(workspace,"Selected")==ItemID.DirtBlock,"query-page point pick restores page and navigates via actual native slot");
                close();Main.playerInventory=false;mouse(1050,550,false);neutral();draw();
                SeedWorldItem();
                draw(); Require(Main.mouseText,"actual world-item tooltip is present in the native Draw");
                outlet.Patch(incoming,prefix:new HarmonyMethod(typeof(NativeTargetOriginalChecks),nameof(Chat)));
                messages=0;Call(Get(announcements,"cooldown"),"Clear");press(Keys.L);
                Require(messages==1&&!(bool)Get(targets,"Busy")&&lastMessage.Contains("这里有 2 个 "+Lang.GetItemNameValue(ItemID.FallenStar)),"actual announcement shortcut reaches native local chat once with the frozen world-item quantity after real world-tooltip Draw");
                draw();neutral();Require(messages==1,"repeated Draw cannot duplicate announcement");
                foreach(int mode in new[]{0,1,2})
                {
                    close();neutral(); int before=messages;Call(Get(announcements,"cooldown"),"Clear");step(new[]{Keys.L});
                    if(mode!=0)
                    {
                        typeof(NativeBlueprintOriginalChecks).GetField("resourceMode",F).SetValue(null,mode);
                        try { draw(); } catch(Exception) { if(mode!=2) throw; }
                        finally { typeof(NativeBlueprintOriginalChecks).GetField("resourceMode",F).SetValue(null,0); }
                    }
                    for(int tick=0;tick<6;tick++)step(Array.Empty<Keys>());
                    Require(messages==before&&!(bool)Get(targets,"Busy"),"no Draw / skipped resource / throwing resource cannot grant target proof "+mode);
                }
                close();neutral();string style=Main.ResourceSetsManager.ActiveSetKeyName;
                mouse(Main.screenWidth-(style.StartsWith("HorizontalBars",StringComparison.Ordinal)?80:220),style.StartsWith("HorizontalBars",StringComparison.Ordinal)?24:40,false);neutral();SeedWorldItem();draw();
                int blocked=messages;Call(Get(announcements,"cooldown"),"Clear");press(Keys.L);
                Require(messages==blocked,"actual life/mana UI hover blocks announcement into the world");
                // Each owner can leave without removing the other's patches.
                ((IDisposable)proof).Dispose();Main.playerInventory=true;close();Call(workspace,"Navigate",ItemID.Wood,false);mouse(40,88,false);neutral();draw();press(Keys.K);
                Require((int)Get(workspace,"Selected")==ItemID.DirtBlock&&!(bool)Get(targets,"Busy"),"Targets still confirms real Draw after Blueprint proof owner leaves");
                Call(proof,"Install");((IDisposable)targets).Dispose();close();Main.playerInventory=false;
                sample(1050,550,false);sample(1050,550,false);Call(blueprint,"Toggle");
                sample(1050,550,false);sample(1050,550,false);sample(1050,550,true);sample(1130,590,true);sample(1130,590,true);
                Require(capture.Dragging,"actual blueprint capture survives Targets owner removal");
                sample(1130,590,false);sample(1130,590,false);Require(capture.Selection.HasSelection,"blueprint capture commits through remaining real resource hooks");
                Call(blueprint,"Toggle");
                Call(blueprint,"CancelCaptureOrDraft");
                var library=(JueMingR.Features.Blueprint.LibraryState)Get(blueprint,"Library");
                NativeQuickItemChecks.Until(()=>{Call(blueprint,"Poll");return !library.Snapshot.Busy&&!(bool)Get(blueprint,"discardPending");});
                var entry=NativeBlueprintPlacementChecks.Seed(library,3,2,"目标接缝取消代表");
                close();sample(1050,550,false);sample(1050,550,false);
                Require((bool)Call(blueprint,"Place",entry),"actual projection entry starts in the owned fixture; canStart="+Get(input,"CanStartActions")+
                    " controls="+Get(blueprint,"ControlsEnabled")+" library="+(GetOptional(blueprint,"Library")!=null)+
                    " persistence="+Get(blueprint,"ProjectionPersistenceReady")+" captureBlocked="+Get(blueprint,"ProjectionCaptureBlocked")+
                    " editing="+Get(blueprint,"ProjectionEditing")+" delete="+Get(blueprint,"deletePending")+
                    " protected="+entry.IsProtected+" revision="+entry.ContentRevision+" hint="+Get(blueprint,"ProjectionActionHint"));
                NativeQuickItemChecks.Until(()=>{Call(blueprint,"Poll");return Get(blueprint,"ProjectionMode").ToString()=="Following";});
                sample(1050,550,false);sample(1050,550,false);sample(1050,550,true);sample(1050,550,true);sample(1050,550,false);sample(1050,550,false);
                // Pointer confirmation can await the real library receipt.
                // Preserve release and continue normal input/Draw; never invoke
                // a commit callback or supply another confirmation press.
                NativeQuickItemChecks.Until(()=>{sample(1050,550,false);return Get(blueprint,"ProjectionMode").ToString()=="Fixed";});
                Require(Get(blueprint,"ProjectionMode").ToString()=="Fixed","actual Draw-certified pointer fixes the projection");
                Func<object[]> scene=()=>((System.Collections.IEnumerable)Get(blueprint,"ProjectionScene")).Cast<object>().ToArray();
                var original=scene().Single();Call(blueprint,"RemoveProjection");
                Require(Get(blueprint,"ProjectionMode").ToString()=="SelectingRemove","actual edit enters removal selection");
                step(new[]{Keys.W,Keys.Escape});
                Require(Get(blueprint,"ProjectionMode").ToString()=="Fixed"&&scene().Length==1&&ReferenceEquals(scene()[0],original),
                    "real Escape cancels removal, restores Fixed and preserves the original projection");
                step(new[]{Keys.W});step(Array.Empty<Keys>());Call(blueprint,"ClearProjection");
                Console.WriteLine("PASS Targets initialized-native caller-first: six styles, query/announcement/page-pick real business outlets, real slot/empty/UI/world-tooltip, noDraw/skip/exception, Blueprint capture/edit-cancel coexistence and independent removal. Controlled input, SkipAssemblyLoad/manual Runtime; not ordinary startup, OS input, FPS, multiplayer or owner acceptance.");
            }
            finally { outlet.Unpatch(incoming,HarmonyPatchType.All,outlet.Id); typeof(NativeBlueprintOriginalChecks).GetField("resourceMode",F).SetValue(null,0); }
        }
        private static void SeedWorldItem()
        {
            var item=new Item();item.SetDefaults(ItemID.FallenStar);item.stack=2;
            Main.item[0]=new WorldItem(item){position=Main.MouseWorld-new Vector2(8,8),width=16,height=16};
        }
        private static void Witness(string stage,object targets,object input,object shell,object browser)
        {
            Console.WriteLine("TARGET ORIGINAL "+stage+" pending="+Get(targets,"pending")+" completed="+Get(targets,"completed")+
                " slot="+Get(targets,"seenSlot")+" hoverType="+(GetOptional(targets,"hovered")==null?"null":Get(Get(targets,"hovered"),"ItemType"))+
                " uiBoundary="+Get(targets,"uiBoundary")+" resourceBoundary="+Get(targets,"resourceBoundary")+" uiBlocked="+Get(targets,"uiBlocked")+
                " matches="+Call(targets,"Matches")+" focused="+Get(input,"IsFocused")+" canStart="+Get(input,"CanStartActions")+
                " canTarget="+Get(shell,"CanTargetInput")+" canQuery="+Call(browser,"CanQuery")+" status="+Get(Get(browser,"Announcements"),"Status")+
                " stateReady="+Get(Get(shell,"State"),"Ready")+" visible="+Get(Get(shell,"State"),"Visible")+" selected="+Get(Get(Get(browser,"Knowledge"),"Workspace"),"Selected")+
                " cameraSame="+((Vector2)Get(targets,"camera")==Main.screenPosition)+" matrixSame="+((Matrix)Get(targets,"matrix")==Main.UIScaleMatrix)+
                " zoomSame="+((Matrix)Get(targets,"zoom")==Main.GameViewMatrix.ZoomMatrix)+" inventorySame="+((bool)Get(targets,"inventory")==Main.playerInventory)+
                " raw="+Get(targets,"raw")+" sampled="+Terraria.GameInput.PlayerInput.MouseInfo.X+","+Terraria.GameInput.PlayerInput.MouseInfo.Y);
        }
    }
}
