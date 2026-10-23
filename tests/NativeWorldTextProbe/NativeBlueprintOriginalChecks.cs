using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Terraria.ID;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Initialized native-Main specialist: input/window/focus are controlled,
    // preload is skipped and Update is simplified by NativeViewOriginalChecks.
    // Native input/mapping, UI layers and the production editor execute.
    // No pointer certificate callback or success flag is invoked or assigned.
    internal static class NativeBlueprintOriginalChecks
    {
        private static int resourceReturns, resourceErrors, wrapperReturns, resourceMode;
        internal static void PrepareCallerBeforeHost()
        {
            // Explicitly exercise the reachable caller-first preparation order
            // before late host patches. These observers count real
            // native returns/errors only; they never supply a pointer certificate.
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            var caller = typeof(Main).GetMethod("DrawMouseOver", flags);
            RuntimeHelpers.PrepareMethod(caller.MethodHandle);
            var observer = new Harmony("JueMingR.Tests.Blueprint.ResourceObservation");
            foreach (var type in new[] {
                typeof(Terraria.GameContent.UI.ResourceSets.ClassicPlayerResourcesDisplaySet),
                typeof(Terraria.GameContent.UI.ResourceSets.FancyClassicPlayerResourcesDisplaySet),
                typeof(Terraria.GameContent.UI.ResourceSets.HorizontalBarsPlayerResourcesDisplaySet) })
                observer.Patch(type.GetMethod("TryToHover", flags),
                    prefix: new HarmonyMethod(typeof(NativeBlueprintOriginalChecks), nameof(ResourceBefore)),
                    postfix: new HarmonyMethod(typeof(NativeBlueprintOriginalChecks), nameof(ResourceReturned)),
                    finalizer: new HarmonyMethod(typeof(NativeBlueprintOriginalChecks), nameof(ResourceFailed)));
            observer.Patch(typeof(Terraria.GameContent.UI.ResourceSets.PlayerResourceSetsManager).GetMethod("TryToHoverOverResources", flags),
                postfix: new HarmonyMethod(typeof(NativeBlueprintOriginalChecks), nameof(WrapperReturned)));
            Console.WriteLine("BLUEPRINT caller DrawMouseOver prepared before host installation; resource observers do not grant business proof");
        }
        private static bool ResourceBefore()
        {
            if (resourceMode == 2) throw new InvalidOperationException("BLUEPRINT isolated resource-seam exception");
            return resourceMode != 1;
        }
        private static void ResourceReturned(bool __runOriginal) { if (__runOriginal) resourceReturns++; }
        private static Exception ResourceFailed(Exception __exception) { if (__exception != null) resourceErrors++; return __exception; }
        private static void WrapperReturned(bool __runOriginal) { if (__runOriginal) wrapperReturns++; }

        internal static void Run(Main game, object context, Action inputStep, Action draw, Func<RenderTarget2D> canvas, string output, Action<int,int,bool> mouse)
        {
            var host=Get(context,"Blueprint"); var input=Get(context,"Input"); var shell=Get(context,"Shell");
            var state=(BlueprintCaptureState)Get(host,"State");var p=Main.LocalPlayer;
            Require(GetOptional(host,"SetupError")==null,"blueprint hooks installed in initialized original Main");
            Action<int,int,bool> sample=(x,y,down)=>{mouse(x,y,down);inputStep();draw();};
            Action neutral=()=>{sample(1050,550,false);sample(1050,550,false);};
            Action drag=()=>{neutral();sample(1050,550,true);sample(1130,590,true);sample(1130,590,true);};
            Action release=()=>{sample(1130,590,false);sample(1130,590,false);};
            Func<string> selected=()=>String.Join(";",state.Selection.EnumerateVisible(0,0,Main.maxTilesX,Main.maxTilesY).Select(s=>s.Y+":"+s.Left+":"+s.RightExclusive));
            Action<string> image=name=>{using(var stream=File.Create(Path.Combine(output,name+".png")))canvas().SaveAsPng(stream,Main.screenWidth,Main.screenHeight);};
            try
            {
                Call(Get(context,"Shell"),"CloseAndSubmitPosition"); Main.playerInventory=false;
                Main.GameZoomTarget=1f; Main.LocalPlayer.mouseInterface=false;Main.mouseText=false;
                mouse(1050,550,true);inputStep();Call(host,"Toggle");draw();
                sample(1130,590,true);Require(!state.Dragging&&!state.Selection.HasSelection,"startup click cannot become first stroke");
                sample(1050,550,false);
                sample(1050,550,false);
                Console.WriteLine("BLUEPRINT native resource returns="+resourceReturns+" exceptions="+resourceErrors+" manager-wrapper returns="+wrapperReturns);
                Require(resourceReturns > 0 && resourceErrors == 0, "prepared native caller executes resource originals successfully before editor assertion");
                // A read-only pointer must survive a legal empty outer Update.
                // Read actual production-layer pixels while no draft/selection
                // can hide a missing pointer behind a different blue rectangle.
                Require(!state.Dragging && !state.Selection.HasSelection && !state.NeedsNeutral, "neutral world input arms the sole editor before pointer-only pixel regression");
                Require(PointerPixels(game, host) > 0, "current mapped pointer reaches actual world-layer pixels");
                long pointerMapping = (long)Get(input, "Mapping");
                Call(input, "BeginUpdate"); Call(context, "UpdateRuntime"); draw();
                Require(!(bool)Get(input,"CanStartActions") && (bool)Get(host,"CanRetainEdit") && (bool)Get(Get(host,"Pointer"),"HasPoint"), "legal empty update keeps a valid retained point with no action permission");
                Require(PointerPixels(game, host) > 0, "empty outer Update must not hide actual blue pointer pixels");
                draw(); draw();
                Require(PointerPixels(game, host) > 0 && (long)Get(input,"Mapping") == pointerMapping && !state.Dragging && !state.Selection.HasSelection && !state.NeedsNeutral,
                    "repeated Draw retains actual pointer pixels without new input, fake release, drag or XOR");
                sample(1050,550,true);
                sample(1130,590,true);
                sample(1130,590,true);
                bool preview=state.Dragging;
                image("blueprint-held-preview");
                sample(1130,590,false);
                sample(1130,590,false);
                Require(preview,"actual original UI certifies held world drag and visible preview before release");
                Require(state.Selection.HasSelection&&!state.Dragging,"actual input and UI release commits one XOR");
                string first=selected();draw();draw();neutral();
                Require(selected()==first,"repeat Draw and ordinary Draw intervals do not repeat XOR or clear committed selection");
                drag();release();Require(!state.Selection.HasSelection,"same rectangle selected twice cancels by XOR");
                // All six native styles share exactly three implementation
                // families. Cycle real UI state in this owned process only;
                // do not assign hover flags or invoke proof callbacks.
                var styles = new System.Collections.Generic.HashSet<string>();
                for (int style = 0; style < 6; style++)
                {
                    string key=Main.ResourceSetsManager.ActiveSetKeyName;styles.Add(key);
                    drag();Require(state.Dragging,"prepared caller certifies held world drag for resource style "+key);
                    release();Require(state.Selection.HasSelection,"resource style release commits "+key);
                    string stable=selected();neutral();
                    int hx=key.StartsWith("HorizontalBars",StringComparison.Ordinal)?Main.screenWidth-80:Main.screenWidth-220;
                    int hy=key.StartsWith("HorizontalBars",StringComparison.Ordinal)?24:40;
                    sample(hx,hy,false);sample(hx,hy,true);sample(hx,hy,true);
                    Require(Main.mouseText&&!state.Dragging&&selected()==stable,"actual resource tooltip owns pointer without world drag "+key);
                    sample(1130,590,true);sample(1130,590,true);
                    Require(!state.Dragging&&selected()==stable,"resource held gesture cannot become world press "+key);
                    neutral();drag();release();Require(!state.Selection.HasSelection,"resource style preserves XOR repeat "+key);
                    Console.WriteLine("BLUEPRINT resource style PASS "+key+" world drag/release/XOR and actual resource tooltip/held crossing");
                    Main.ResourceSetsManager.CycleResourceSet();
                }
                Require(styles.Count==6,"all six original resource variants covered across three implementations");
                foreach (int rejected in new[]{1,2})
                {
                    drag();Require(state.Dragging,"resource failure counterexample starts with real draft");
                    int errorsBefore=resourceErrors;resourceMode=rejected;
                    try { sample(1130,590,true);sample(1130,590,true); }
                    finally { resourceMode=0; }
                    Require(!state.Dragging&&!state.Selection.HasSelection&&state.NeedsNeutral,
                        "skipped/throwing resource original cannot certify or commit "+rejected);
                    Require(rejected==1?resourceErrors==errorsBefore:resourceErrors>errorsBefore,
                        "resource rejection distinguishes skip from swallowed exception "+rejected);
                    Console.WriteLine("BLUEPRINT resource "+(rejected==1?"skip":"swallowed exception")+" PASS fail closed without commit");
                    neutral();
                }
                var worldItem=Main.item[0];var oldItemPosition=worldItem.position;
                try
                {
                    neutral();
                    var worldPoint=Vector2.Transform(new Vector2(1050,550)*PlayerInput.RawMouseScale,Matrix.Invert(Main.GameViewMatrix.ZoomMatrix))+Main.screenPosition;
                    worldItem.Bottom=worldPoint+new Vector2(0,6);
                    sample(1050,550,false);sample(1050,550,false);
                    Require(Main.mouseText&&!state.NeedsNeutral,"actual dropped-item world tooltip does not own UI certificate");
                    sample(1050,550,true);sample(1050,550,true);
                    Require(Main.mouseText&&state.Dragging,"world hover after resources remains selectable despite final mouseText");
                    sample(1050,550,false);sample(1050,550,false);
                    Require(state.Selection.HasSelection,"world hover release commits once");
                    neutral();sample(1050,550,true);sample(1050,550,true);sample(1050,550,false);sample(1050,550,false);
                    Require(!state.Selection.HasSelection,"world hover repeat XOR clears once");
                    Console.WriteLine("BLUEPRINT actual dropped-item world hover PASS selectable with final mouseText");
                }
                finally { worldItem.position=oldItemPosition; }
                sample(1050,550,false);
                sample(1050,550,false);
                mouse(1050,550,true);inputStep();
                long mapping=(long)Get(input,"Mapping");
                Call(input,"BeginUpdate");Call(context,"UpdateRuntime");
                Require((long)Get(input,"Mapping")==mapping&&!(bool)Get(input,"CanStartActions")&&(bool)Get(input,"CanRetainIntent"),"empty outer Update retains focus without new mapping permission");
                draw();
                sample(1130,590,true);
                sample(1130,590,true);
                Require(state.Dragging,"empty outer Update between native input and Draw must retain the held draft");
                mouse(1160,600,true);inputStep();inputStep();
                Require(state.Dragging,"two mapped updates without Draw retain existing held draft");
                draw();sample(1130,590,true);release();
                Require(state.Selection.HasSelection,"certified release after scheduling gaps commits once");first=selected();
                mouse(1130,590,true);inputStep(); // no Draw for the new press
                sample(1160,600,true);sample(1160,600,true);
                Require(!state.Dragging,"uncertified skipped press is not replayed from a later held sample");
                release();Require(selected()==first,"skipped press preserves committed coordinates");
                drag();mouse(1130,590,false);inputStep();inputStep();draw();neutral();
                Require(!state.Dragging&&selected()==first,"unobserved release cancels draft instead of replaying late commit");

                drag();Set(input,"foregroundWindow",(Func<IntPtr>)(()=>IntPtr.Zero));inputStep();draw();
                Require(!state.Dragging&&state.NeedsNeutral&&selected()==first,"focus rejection cancels only draft");
                Set(input,"foregroundWindow",(Func<IntPtr>)(()=>new IntPtr(1)));neutral();neutral();
                drag();Require(state.Dragging,"fresh neutral and press recover after focus loss");
                Main.playerInventory=true;sample(1050,550,true);
                Require(!state.Dragging&&selected()==first,"explicit native inventory cancels draft and preserves committed set");
                // The real DrawInventory -> ItemSlot.Handle consumer must still
                // pick up a slot item while world capture is active.
                Main.mouseItem=new Item();p.inventory[10].SetDefaults(ItemID.DirtBlock);p.inventory[10].stack=3;
                int sx=(int)(40*Main.UIScale),sy=(int)(88*Main.UIScale);
                sample(sx,sy,false);sample(sx,sy,true);
                Require(Main.mouseItem.type==ItemID.DirtBlock&&Main.mouseItem.stack==3&&p.inventory[10].IsAir,"actual native inventory slot click stays usable during capture");
                sample(sx,sy,false);sample(1050,550,true);Main.playerInventory=false;sample(1050,550,true);
                Require(!state.Dragging&&selected()==first,"UI carried-item gesture cannot become world drag outside inventory");
                Main.mouseItem=new Item();neutral();

                p.inventory[0].SetDefaults(ItemID.CopperShortsword);p.selectedItemState.Select(0);p.selectedItemState.Update();
                sample(1050,550,true);PlayerInput.Triggers.Current.CopyInto(p);
                int before=p.itemAnimation;p.ItemCheck();
                Require(!p.controlUseItem&&!p.controlUseTile&&p.itemAnimation==before,"capture blocks held ItemCheck attack initiation");
                Call(host,"Toggle");neutral();sample(1050,550,true);PlayerInput.Triggers.Current.CopyInto(p);p.ItemCheck();
                Require(p.itemAnimation>before,"exit and fresh press restore native attack");
                release();p.itemAnimation=p.itemTime=0;

                p.inventory[0].SetDefaults(ItemID.Gel);Call(host,"SetToolbar",true);neutral();draw();
                var toolbar=Get(host,"Toolbar");var layout=Get(toolbar,"Layout");
                Action<int,bool> button=(index,down)=>
                {
                    var panel=Get(layout,"Panel");var b=((System.Collections.IEnumerable)Get(layout,"Buttons")).Cast<object>().ElementAt(index);var rect=Get(b,"Rect");
                    sample((int)(((float)Get(panel,"X")+(float)Get(rect,"X")+(float)Get(rect,"Width")/2)*Main.UIScale),
                        (int)(((float)Get(panel,"Y")+(float)Get(rect,"Y")+(float)Get(rect,"Height")/2)*Main.UIScale),down);
                };
                button(0,false);button(0,true);button(0,false);
                Require(state.Active&&!state.Dragging&&!state.Selection.HasSelection,"actual toolbar capture button executes without world selection");
                sample(1050,550,true);Require(!state.Dragging,"startup gesture is not reinterpreted outside bar");neutral();
                Terraria.Localization.LanguageManager.Instance.SetLanguage("zh-Hans");
                foreach(float scale in new[]{1f,1.5f})
                {
                    Main.UIScale=scale;neutral();draw();button(0,false);draw();
                    Require((bool)Get(Get(toolbar,"hint"),"Visible"),"actual disabled-save hint draws at UI "+scale);
                    image("blueprint-hint-ui-"+scale.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                NativeBlueprintPlacementOriginalChecks.Run(context, sample, draw);
                NativeBlueprintProjectionOriginalChecks.Run(game, context, draw, canvas, output);
                Console.WriteLine("PASS BLUEPRINT initialized-native specialist: explicit caller-first JIT, six resource variants/three originals, actual resource vs world hover, skip/exception fail closed, actual pointer pixels across empty Update/repeat Draw, startup barrier, held preview, once-only XOR/cancel, Draw gaps/no replay, focus/UI recovery, actual inventory slot/bar button, attack block/exit restore, native hint screenshots UI100/150. SkipAssemblyLoad, simplified Update, manual Runtime and controlled focus/window/input; not complete ordinary load/preload, OS input, multiplayer, FPS or owner acceptance.");
            }
            finally { resourceMode=0;Set(input,"foregroundWindow",(Func<IntPtr>)(()=>new IntPtr(1)));state.Exit();mouse(1050,550,false);Main.playerInventory=false;Main.mouseItem=new Item(); }
        }

        private static int PointerPixels(Main game, object host)
        {
            var device = game.GraphicsDevice;
            var targets = device.GetRenderTargets();
            using (var target = new RenderTarget2D(device, Main.screenWidth, Main.screenHeight))
            {
                try
                {
                    device.SetRenderTarget(target); device.Clear(Color.Transparent);
                    Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                        DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.ZoomMatrix);
                    try { Call(Get(host,"World"),"Draw"); }
                    finally { Main.spriteBatch.End(); }
                    device.SetRenderTarget(null);
                    var pixels = new Color[target.Width * target.Height]; target.GetData(pixels);
                    return pixels.Count(c => c.A > 0 && c.B > c.R && c.B > c.G);
                }
                finally { device.SetRenderTargets(targets); }
            }
        }
    }
}
