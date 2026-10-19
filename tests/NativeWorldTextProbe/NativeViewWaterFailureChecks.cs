using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.Graphics;
using Terraria.Graphics.Effects;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeViewWaterFailureChecks
    {
        private const BindingFlags F=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
        private static TileBatch selected;
        private static Exception injected;
        private static bool armed;
        private static SpriteBatch foreignFailureLease;
        private static bool foreignOpened;
        private static object ownWater;
        private static void BeforeMask()
        {if(armed && injected is IOException && foreignFailureLease!=null && !foreignOpened && ownWater!=null && (bool)Get(ownWater,"Active") && !(bool)Get(ownWater,"RenderingMask")){foreignFailureLease.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend);foreignOpened=true;}}
        private static int attempts,secondary;
        private static readonly FieldInfo deviceBegins=typeof(GraphicsDevice).GetField("spriteBeginCount",F),deviceImmediate=typeof(GraphicsDevice).GetField("spriteImmediateBeginCount",F);
        private static readonly FieldInfo queue=typeof(TileBatch).GetField("_spriteDataQueue",F),queued=typeof(TileBatch).GetField("_queuedSpriteCount",F);
        private static IEnumerable<CodeInstruction> BeforeRealResize(IEnumerable<CodeInstruction> source)
        {
            int count=0;
            foreach(var instruction in source)
            {
                var method=instruction.operand as MethodInfo;
                if(instruction.opcode==OpCodes.Call && method!=null && method.DeclaringType==typeof(Array) && method.Name=="Resize" && method.IsGenericMethod && method.GetGenericArguments()[0].Name=="SpriteData")
                {
                    count++;var first=new CodeInstruction(OpCodes.Ldarg_0);first.labels.AddRange(instruction.labels);instruction.labels.Clear();yield return first;
                    yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(NativeViewWaterFailureChecks),nameof(AtResize)));
                }
                yield return instruction;
            }
            Require(count==1,"one real native SpriteData resize in GetNewSpriteBufferSlice");
        }
        private static void AtResize(TileBatch value)
        {
            if(!armed || !ReferenceEquals(value,selected))return;
            armed=false;attempts++;
            int length=((Array)queue.GetValue(value)).Length,n=(int)queued.GetValue(value);
            Console.WriteLine("VIEW WATER FAILURE native pre-Resize queued="+n+" capacity="+length+" exception="+injected.GetType().Name);
            Require(n>length,"real original partial queue publication precedes resize failure");
            throw injected;
        }
        private static void Disposed(TileBatch __instance)
        {if(ReferenceEquals(__instance,selected)){secondary++;throw new InvalidOperationException("Controlled secondary error after actual owned TileBatch disposal");}}
        private static void Arm(object water,Exception error)
        {
            selected=(TileBatch)Get(water,"maskTiles");Require(!ReferenceEquals(selected,Main.tileBatch),"fault only targets VIEW-owned native batch");
            var old=(Array)queue.GetValue(selected);queue.SetValue(selected,Array.CreateInstance(old.GetType().GetElementType(),1));
            injected=error;armed=true;attempts=secondary=0;
        }
        internal static void Run(object view,Action draw)
        {
            var audit=new Harmony("JueMingR.Tests.View.WaterFailure");
            var originalTiles=Main.tileBatch;var originalSprites=Main.spriteBatch;
            var filter=Filters.Scene["WaterDistortion"];bool active=filter.IsActive();float opacity=filter.Opacity;int quality=Main.WaveQuality;
            audit.Patch(typeof(TileBatch).GetMethod("GetNewSpriteBufferSlice",F),transpiler:new HarmonyMethod(typeof(NativeViewWaterFailureChecks),nameof(BeforeRealResize)));
            audit.Patch(typeof(TileBatch).GetMethod("Dispose"),postfix:new HarmonyMethod(typeof(NativeViewWaterFailureChecks),nameof(Disposed)));
            try
            {
                // The preceding sampling scope legitimately fades its filter.
                // This failure belongs to the physical-mask consumer path;
                // establish that real consumer rather than requiring a mask
                // which the no-consumer optimization must correctly omit.
                Main.WaveQuality=3;Filters.Scene.Activate("WaterDistortion",Main.LocalPlayer.Center);filter.Opacity=1;
                Filters.Scene.Update(new GameTime(TimeSpan.Zero,TimeSpan.Zero));
                Call(view,"SetUnlockEnabled",true);Main.GameZoomTarget=.5f;draw();
                var water=Get(view,"Water");ownWater=water;
                var device=Main.instance.GraphicsDevice;
                ushort originalBegin=(ushort)deviceBegins.GetValue(device),originalImmediate=(ushort)deviceImmediate.GetValue(device);
                // A normal leaf may be called inside another module's deferred
                // batch. Verify its lease and exact global references survive.
                using(var foreign=new SpriteBatch(device))
                {
                    foreign.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend);
                    var render=(Action)Delegate.CreateDelegate(typeof(Action),water,water.GetType().GetMethod("RenderMask",F));render();
                    Require((ushort)deviceBegins.GetValue(device)==originalBegin+1 && (ushort)deviceImmediate.GetValue(device)==originalImmediate && ReferenceEquals(Main.tileBatch,originalTiles)&&ReferenceEquals(Main.spriteBatch,originalSprites),"normal mask scope preserves an unrelated already-open deferred sprite lease and exact shared references");
                    foreign.Draw(TextureAssets.MagicPixel.Value,Vector2.Zero,Color.Transparent);foreign.End();
                }
                originalTiles.Begin();originalTiles.Draw(TextureAssets.MagicPixel.Value,Vector2.Zero,new VertexColors(Color.Transparent));
                int pending=(int)queued.GetValue(originalTiles);Require(pending>0,"shared original batch has controlled unrelated pending work");
                foreignFailureLease=new SpriteBatch(device);
                audit.Patch(typeof(TileDrawing).GetMethod("GetScreenDrawArea",F),prefix:new HarmonyMethod(typeof(NativeViewWaterFailureChecks),nameof(BeforeMask)));
                var unknown=new IOException("Controlled unknown native liquid error at real partial queue publication");Arm(water,unknown);
                var raw=(Action<GameTime>)Delegate.CreateDelegate(typeof(Action<GameTime>),Main.instance,typeof(Main).GetMethod("DoDraw",F));
                bool propagated=false;
                try{raw(new GameTime());}catch(IOException error){propagated=ReferenceEquals(error,unknown);}
                Require(propagated && attempts==1 && secondary==1,"unknown original error survives both VIEW scope and outer DoDraw unchanged, including a second cleanup error");
                Require(ReferenceEquals(Main.tileBatch,originalTiles)&&ReferenceEquals(Main.spriteBatch,originalSprites)&&(int)queued.GetValue(originalTiles)==pending,"exception restores exact shared batch references and leaves unrelated original pending queue unchanged");
                Require(foreignOpened && (ushort)deviceBegins.GetValue(device)==originalBegin+1 && (ushort)deviceImmediate.GetValue(device)==originalImmediate,"unknown failure preserves the unrelated already-open entry lease while returning only owned Begin counts");
                foreignFailureLease.Draw(TextureAssets.MagicPixel.Value,Vector2.Zero,Color.Transparent);foreignFailureLease.End();foreignOpened=false;
                Require(GetOptional(water,"maskTiles")==null && GetOptional(water,"maskSprites")==null,"corrupt owned queues are retired, never flushed as recovery");
                originalTiles.End();Main.GameZoomTarget=1;int count=Main.fpsCount;for(int i=0;i<3;i++)draw();
                Require(Main.fpsCount==count+3&&(bool)Get(view,"ControlsEnabled") && Main.GameViewMatrix.RenderZoom.X==1,"unknown original error is not relabeled as VIEW resource failure; following ordinary Draw really executes");
                Console.WriteLine("PASS VIEW WATER unknown native partial queue error propagated exactly with secondary cleanup error, original pending work preserved; following3 real ordinary Draw.");

                Main.GameZoomTarget=.5f;draw();Arm(water,new OutOfMemoryException("Controlled actual native liquid queue resize allocation failure"));
                draw();
                Require((ushort)deviceBegins.GetValue(device)==originalBegin && (ushort)deviceImmediate.GetValue(device)==originalImmediate,"owned OOM returns its device Begin lease despite secondary cleanup error");
                Require(attempts==1 && secondary==1 && ReferenceEquals(Main.tileBatch,originalTiles)&&ReferenceEquals(Main.spriteBatch,originalSprites)&&(int)queued.GetValue(originalTiles)==0,"owned resize OOM is contained without damaged queue or reference escaping into original batch");
                var messages=new List<string>();Call(view,"TakeFeedback",(Action<string>)messages.Add);Call(view,"TakeFeedback",(Action<string>)messages.Add);
                Require(messages.Count==1 && !(bool)Get(view,"ControlsEnabled") && Main.GameZoomTarget>=1,"water resource failure reports once and selects safe ordinary state");
                count=Main.fpsCount;for(int i=0;i<20;i++)draw();Call(view,"TakeFeedback",(Action<string>)messages.Add);
                Require(Main.fpsCount==count+20&&Main.GameViewMatrix.RenderZoom.X==1 && (int)queued.GetValue(originalTiles)==0 && attempts==1 && messages.Count==1 && !(bool)typeof(Main).GetField("_isDrawingOrUpdating",F).GetValue(Main.instance),"next20 actual native ordinary Draw complete without pending-queue Begin failure, retry or feedback loop");
                Console.WriteLine("PASS VIEW WATER OOM after real queued increment/before Array.Resize, secondary disposal error preserved primary; exact original batches intact, next20 ordinary Draw, attempt1/message1.");
            }
            finally{armed=false;audit.UnpatchAll(audit.Id);if(foreignOpened){foreignFailureLease.End();foreignOpened=false;}foreignFailureLease?.Dispose();foreignFailureLease=null;ownWater=null;selected=null;injected=null;Main.WaveQuality=quality;if(active)Filters.Scene.Activate("WaterDistortion",Main.LocalPlayer.Center);else Filters.Scene.Deactivate("WaterDistortion");filter.Opacity=opacity;}
        }
    }
}
