using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Guidance;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Settings;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeGuidanceInputChecks
    {
        private const BindingFlags Flags = NativeGuidanceChecks.Flags;
        internal static void Run(object context, object host, string root, string[] old)
        {
            object shell = Get(context, "Shell"), input = Get(context, "Input"), state = Get(shell, "State"), renderer = Get(shell, "renderer");
            Set(input, "gameWindow", (Func<IntPtr>)(() => new IntPtr(1))); Set(input, "foregroundWindow", (Func<IntPtr>)(() => new IntPtr(1)));
            typeof(Main).GetField("_uiScaleMatrix", Flags).SetValue(null, Matrix.Identity);
            typeof(PlayerInput).GetField("_originalScreenWidth", Flags).SetValue(null, 960); typeof(PlayerInput).GetField("_originalScreenHeight", Flags).SetValue(null, 640);
            typeof(PlayerInput).GetField("RawMouseScale", Flags).SetValue(null, Vector2.One);
            PlayerInput.Triggers.Initialize(); Main.blockInput = false; FocusHelper.IsSelectedApplication = true;
            Set(shell, "LayersReady", true); Set(state, "Ready", true); Call(renderer, "RefreshResources");
            Action<int, int, bool, Keys[]> frame = (x, y, left, keys) =>
            {
                Main.LocalPlayer.mouseInterface = Main.mouseText = false; Main.keyState = new KeyboardState(keys);
                PlayerInput.MouseInfo = new MouseState(x,y,0,left ? ButtonState.Pressed : ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);
                PlayerInput.Triggers.Reset(); PlayerInput.Triggers.Current.MouseLeft = left; PlayerInput.Triggers.Update(); Main.mouseLeft = left;
                Call(input,"BeginUpdate"); Call(input,"AfterMapping"); Call(input,"AfterKeyboardRefresh"); Call(shell,"ProcessInput");
                Require(!(bool)Get(shell,"Failed"), "real F5 Guidance input remains healthy");
            };
            var none = new Keys[0]; frame(940,620,false,none); frame(940,620,false,none);
            Action<int,string> click = (page, command) =>
            {
                Call(state,"Navigate",page); Call(state,"RestoreVisible"); Call(renderer,"Prepare",state,960f,640f,1f);
                object layout=Get(state,"Layout"), element=null;
                foreach(object candidate in (IEnumerable)Get(layout,"Elements")) if(Get(candidate,"Command").ToString()==command) element=candidate;
                Require(element!=null,"actual page command exists: "+command);
                object rect=Get(element,"Rect"), view=Get(layout,"Viewport"); Call(state,"ScrollTo",(float)Get(rect,"Y"));
                int x=(int)((float)Get(state,"X")+(float)Get(view,"X")+(float)Get(rect,"X")+(float)Get(rect,"Width")/2);
                int y=(int)((float)Get(state,"Y")+(float)Get(view,"Y")+(float)Get(rect,"Y")-(float)Get(state,"Scroll")+(float)Get(rect,"Height")/2);
                frame(x,y,false,none); frame(x,y,true,none); frame(x,y,true,none); frame(x,y,false,none);
                Require(Get(state,"Command").ToString()==command,"same-element release dispatches "+command);
            };
            var bindings=(HotkeyBindings)Get(Get(shell,"hotkeys"),"Bindings"); NativeGuidanceChecks.Until(()=> {bindings.Poll();return bindings.Loaded;});
            foreach(string id in new[]{"rare-direction.toggle","merchant-direction.toggle","equipment-warning.toggle"}) Require(bindings.Get(id)==null,"new hotkeys default unbound");
            click(2,"EnableRare"); click(2,"EnableMerchant"); click(8,"EnableEquipment");
            Require(((PreferenceSnapshot<GuidancePreferences>)Get(host,"Preferences")).Value.Mask==7,"all three actual page buttons reach sole preference owner");
            click(2,"DisableMerchant");
            var feature=(MerchantTestFeature)Get(host,"MerchantTest"); object port=Get(feature,"port"); int calls=0;
            Set(port,"spawn",(Action)(()=>calls++)); click(1,"SummonMerchant");
            Require(feature.Pending && calls==0,"button submits exactly one intent, never calls vanilla in input/UI");
            Call(context,"UpdateRuntime"); for(int i=0;i<20;i++)Call(context,"UpdateRuntime");
            Require(calls==1 && !(bool)Call(host,"IsEnabled",GuidanceKind.Merchant),"one native attempt and no automatic merchant direction toggle");
            foreach (string cancellation in new[]{"chat","map","camera","closed"})
            {
                click(1,"SummonMerchant"); Require(feature.Pending,"real button queues before native input takeover");
                if(cancellation=="chat") Main.drawingPlayerChat=true;
                if(cancellation=="map") Main.mapFullscreen=true;
                if(cancellation=="camera") PlayerInput.Triggers.Current.ToggleCameraMode=true;
                if(cancellation=="closed") Call(state,"Close");
                Call(context,"UpdateRuntime");
                Require(calls==1&&!feature.Pending&&feature.Result.Outcome==JueMingR.Platform.Operations.GameOperationOutcome.Cancelled,"final outlet cancels pending after "+cancellation);
                Main.drawingPlayerChat=Main.mapFullscreen=false; PlayerInput.Triggers.Current.ToggleCameraMode=false;
                Call(state,"RestoreVisible"); frame(940,620,false,none); Call(context,"UpdateRuntime");
                Require(calls==1&&!feature.Pending,"restored input never replays cancelled request");
            }
            object pageLayout=Get(state,"Layout"); bool risk=false,name=false;
            foreach(object e in (IEnumerable)Get(pageLayout,"Elements"))
            {
                string text=GetOptional(e,"Text") as string; if(text!=null && text.Contains("生成真实旅商")) risk=true;
                var description=GetOptional(e,"Description"); if(description!=null) {name=true;Require(Get(e,"Command").ToString()=="None","name help never executes summon");}
            }
            Require(risk&&name,"single summon row has public name help and always-visible risk");
            int generation=(int)Get(pageLayout,"Generation"), measurements=(int)Get(pageLayout,"MeasurementCount");
            for(int i=0;i<100;i++)Call(renderer,"Prepare",state,960f,640f,1f);
            Require((int)Get(pageLayout,"Generation")==generation&&(int)Get(pageLayout,"MeasurementCount")==measurements,"stable Misc page does not rebuild/remeasure");
            Call(state,"Close"); frame(940,620,false,none); frame(940,620,false,none);
            string[] ids={"rare-direction.toggle","merchant-direction.toggle","equipment-warning.toggle"};
            for(int i=0;i<3;i++)
            {
                HotkeyChord chord; string reason; long commandId; var key=(Keys)((int)Keys.F2+i);
                Require(HotkeyChord.TryParse(key.ToString(),out chord,out reason),"test chord parses");
                Require(bindings.TrySet(ids[i],chord,null,out commandId,out reason),"new toggle uses normal binding save");
                NativeGuidanceChecks.Until(()=> {bindings.Poll();return !bindings.Busy;});
                var kind=(GuidanceKind)i; bool before=(bool)Call(host,"IsEnabled",kind); frame(940,620,false,new[]{key});
                Require((bool)Call(host,"IsEnabled",kind)!=before,"saved physical shortcut reaches same owner: "+ids[i]); frame(940,620,false,none);
            }
            var saved=HotkeyDocument.Decode(File.ReadAllBytes(Path.Combine(root,"JueMingRData","config","hotkeys.json")));
            Require(saved.Entries.Any(e=>e.Key=="future.unknown"&&e.Value=="Z") && old.All(id=>saved.Entries.Any(e=>e.Key==id)),"normal binding save retains prior and future entries");
            NativeGuidanceChecks.Until(()=>((PreferenceSnapshot<GuidancePreferences>)Get(host,"Preferences")).Status==PreferenceStatus.Saved);
            var expected=((PreferenceSnapshot<GuidancePreferences>)Get(host,"Preferences")).Value;
            Require((bool)Call(Get(host,"preferences"),"Stop",750),"first settings owner stops before reload");
            var reloaded=new PreferenceDocument<GuidancePreferences>(new AtomicFileDocument(Path.Combine(root,"JueMingRData","config","features","guidance.json"),65536,true),new GuidancePreferenceCodec(),GuidancePreferences.Default);
            Set(host,"preferences",reloaded); NativeGuidanceChecks.Until(()=>reloaded.Snapshot.IsLoaded);
            Require(reloaded.Snapshot.Value.Equals(expected),"actual async save and new owner reload preserves independent choices");
            Console.WriteLine("PASS: three real F5 pages, name help, visible summon risk, physical release/hotkey dispatch, unknown binding retention and restart save.");
        }
    }
}
