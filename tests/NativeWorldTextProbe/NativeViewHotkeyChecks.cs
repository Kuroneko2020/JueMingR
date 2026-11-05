using System;
using System.Collections;
using System.IO;
using System.Linq;
using JueMingR.Platform.Hotkeys;
using JueMingR.Infrastructure.Storage;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeViewHotkeyChecks
    {
        private const string Id = "view-unlock.toggle"; // independent contract expectation
        internal static void Run(object context)
        {
            object shell=Get(context,"Shell"), input=Get(context,"Input"), view=Get(context,"View"), display=Get(context,"ShortFeedback");
            var keys=Get(shell,"hotkeys");var registry=(HotkeyRegistry)Get(keys,"Registry");var bindings=(HotkeyBindings)Get(keys,"Bindings");
            var action=registry.Find(Id);
            Require(action!=null && action.Context==HotkeyContext.Gameplay,"view registered once before registry freeze");
            Require(bindings.Get(Id)==null,"new view action defaults unbound");
            NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return (bool)Get(view,"ControlsEnabled");});
            float zoom=Main.GameZoomTarget;bool original=(bool)Get(view,"UnlockEnabled");
            try
            {
                Main.GameZoomTarget=.5f;
                HotkeyChord chord;string reason;long command;
                Require(HotkeyChord.TryCreate((int)Keys.F14,HotkeyModifiers.LeftControl,out chord,out reason),"view test chord");
                Require(bindings.TrySet(Id,chord,null,out command,out reason),"view binding enters shared reliable storage");
                NativeQuickItemChecks.Until(()=>{bindings.Poll();return !bindings.Busy;});
                Require(bindings.CompletionSucceeded && bindings.Get(Id).Equals(chord),"view binding committed");
                Action<Keys[]> sample=pressed=>{NativeQuickItemChecks.Sample(input,pressed);Call(shell,"ProcessInput");};
                Call(shell,"CloseAndSubmitPosition");sample(new Keys[0]);Call(display,"Clear");
                bool before=(bool)Get(view,"UnlockEnabled");sample(new[]{Keys.LeftControl,Keys.F14});
                Require((bool)Get(view,"UnlockEnabled")!=before && Main.GameZoomTarget==.5f && NativeShortFeedbackChecks.Has(display,"视距解锁 "+(before?"已关闭":"已开启")),"physical view hotkey reaches same owner with exact feedback and retained zoom");
                sample(new[]{Keys.LeftControl,Keys.F14});Require((bool)Get(view,"UnlockEnabled")!=before,"held view chord does not repeat");
                sample(new Keys[0]);sample(new[]{Keys.LeftControl,Keys.F14});sample(new Keys[0]);
                Require((bool)Get(view,"UnlockEnabled")==before && Main.GameZoomTarget==.5f,"reverse permission switch retains zoom");
                Main.drawingPlayerChat=true;sample(new[]{Keys.LeftControl,Keys.F14});Main.drawingPlayerChat=false;sample(new Keys[0]);
                Require((bool)Get(view,"UnlockEnabled")==before,"chat gates view switch");
                // Reopen the real serialized binding with a distinct registry;
                // this reader has no business state and only owns its worker.
                var restoredRegistry=new HotkeyRegistry();restoredRegistry.Register(new HotkeyAction(Id,"视距解锁",HotkeyContext.Gameplay,()=>true,()=>{}));
                string reloadRoot=Path.Combine((string)Get(context,"gameDirectory"),"view-binding-reload");Directory.CreateDirectory(reloadRoot);
                string reloadPath=Path.Combine(reloadRoot,"hotkeys.json");
                File.Copy(Path.Combine((string)Get(context,"gameDirectory"),"JueMingRData","config","hotkeys.json"),reloadPath);
                using(var restored=new HotkeyBindings(restoredRegistry,new AtomicFileDocument(reloadPath,65536,true)))
                {NativeQuickItemChecks.Until(()=>{restored.Poll();return restored.Loaded;});Require(chord.Equals(restored.Get(Id)),"saved view chord restores with a new binding owner");}
                Require(bindings.TrySet(Id,null,null,out command,out reason),"view clear accepted");
                NativeQuickItemChecks.Until(()=>{bindings.Poll();return !bindings.Busy;});
                Require(bindings.Get(Id)==null,"reliably cleared view action inactive");
            }
            finally {Main.drawingPlayerChat=false;Main.GameZoomTarget=zoom;Call(view,"SetUnlockEnabled",original);Call(display,"Clear");}
        }
        internal static void Visual(object context, ProbeGraphics graphics, string output)
        {
            object shell=Get(context,"Shell"),state=Get(shell,"State"),popup=Get(shell,"HotkeyPopup"),view=Get(context,"View");
            var bindings=(HotkeyBindings)Get(Get(shell,"hotkeys"),"Bindings");
            foreach(var size in new[]{new[]{960,640,100},new[]{640,360,100},new[]{960,540,150}})
            {
                NativeToolsUiChecks.Prepare(context,2,size[0],size[1],size[2]/100f);
                NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false);
                object layout=Get(state,"Layout");var icon=((IEnumerable)Get(layout,"Elements")).Cast<object>().Single(e=>(string)GetOptional(e,"HotkeyTarget")==Id);
                var rect=Get(icon,"Rect");var viewport=Get(layout,"Viewport");
                Call(state,"ScrollTo",Math.Max(0,(float)Get(rect,"Bottom")-(float)Get(viewport,"Height")+4));
                Vector2 point=new Vector2((float)Get(state,"X")+(float)Get(viewport,"X")+(float)Get(rect,"X")+5,(float)Get(state,"Y")+(float)Get(viewport,"Y")+(float)Get(rect,"Y")-(float)Get(state,"Scroll")+5)*Main.UIScale;
                bool before=(bool)Get(view,"UnlockEnabled");
                graphics.Image(Path.Combine(output,"view-map-"+size[0]+"-"+size[2]+".png"),()=>Call(shell,"DrawLayer"),Main.UIScaleMatrix,size[0],size[1]);
                NativeToolsUiChecks.Click(context,point);Require(!(bool)Get(popup,"Visible"),"first real icon click does not open recorder");
                NativeToolsUiChecks.Click(context,point);Require((bool)Get(popup,"Visible") && (string)Get(popup,"Target")==Id && (bool)Get(view,"UnlockEnabled")==before,"double click opens shared view recorder without toggling");
                graphics.Image(Path.Combine(output,"view-binding-"+size[0]+"-"+size[2]+".png"),()=>Call(shell,"DrawLayer"),Main.UIScaleMatrix,size[0],size[1]);
                NativeToolsUiChecks.PopupClick(context,popup,"Record");
                NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false,Keys.LeftControl);NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false,Keys.LeftControl,Keys.F14);NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false);
                NativeQuickItemChecks.Until(()=>{NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false);return !bindings.Busy;});
                Require(bindings.Get(Id)!=null && bindings.CompletionSucceeded,"real view recorder saves through public binding owner");
                NativeToolsUiChecks.PopupClick(context,popup,"Clear");NativeQuickItemChecks.Until(()=>{NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false);return !bindings.Busy;});
                Require(bindings.Get(Id)==null,"real clear deactivates view chord");Call(popup,"Close");
                object death=Get(shell,"DeathPopup"),renderer=Get(shell,"renderer");Call(death,"Open",true,2);NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false);
                graphics.Image(Path.Combine(output,"quantity-"+size[0]+"-"+size[2]+".png"),()=>Call(renderer,"DrawDeathPopup",death),Main.UIScaleMatrix,size[0],size[1]);Call(death,"Close");
            }
            Call(shell,"CloseAndSubmitPosition");
        }
    }
}
