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
using Terraria.Graphics;
using Terraria.Graphics.Light;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Fault the actual native queue after its queued-count publication. All
    // temporary content is owned by this finite initialized-Main test; no
    // owner input incident reproduction or new product diagnostic is involved.
    internal static class NativeViewCacheLifecycleChecks
    {
        private const BindingFlags F=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private static readonly FieldInfo queue=AccessTools.Field(typeof(TileBatch),"_spriteDataQueue"),queued=AccessTools.Field(typeof(TileBatch),"_queuedSpriteCount"),begins=AccessTools.Field(typeof(GraphicsDevice),"spriteBeginCount"),immediate=AccessTools.Field(typeof(GraphicsDevice),"spriteImmediateBeginCount");
        private static TileBatch selected;
        private static Exception injected;
        private static bool armed;
        private static int attempts,secondary;
        private static bool allocationArmed;
        private static int allocationAttempts;
        private static Action reenter;
        private static int reentries;
        private static bool foreignArmed;
        private static Exception foreignError;
        private static Type hookOwner;
        private static void ForeignFailure(){if(foreignArmed){foreignArmed=false;throw foreignError;}}
        private static Exception Reenter(Exception __exception)
        {
            if(reenter!=null&&ReferenceEquals(__exception,injected))
            {
                var callback=reenter;reenter=null;int frames=Main.fpsCount;
                Require((bool)AccessTools.Field(typeof(Main),"_isDrawingOrUpdating").GetValue(Main.instance),"outer failed native Draw still owns its entered guard before final return");
                var marker=AccessTools.Field(hookOwner,"ownedDrawFailure");var bindings=AccessTools.Field(hookOwner,"drawBindings");var prior=marker.GetValue(null);var priorTargets=bindings?.GetValue(null);
                callback();reentries++;
                Require(Main.fpsCount==frames&&(bool)AccessTools.Field(typeof(Main),"_isDrawingOrUpdating").GetValue(Main.instance),"nested original Draw preserves true guard and does not execute another DoDraw");
                Require(ReferenceEquals(prior,marker.GetValue(null))&&(bindings==null||ReferenceEquals(priorTargets,bindings.GetValue(null))),"blocked nested Draw returns the exact outer exception marker and frame-target snapshot");
            }
            return __exception;
        }
        private static WorldSceneLayerTarget preparedOne;
        private static void Allocate()
        {if(allocationArmed&&++allocationAttempts==2){allocationArmed=false;throw new OutOfMemoryException("controlled second real optional world RT allocation");}}
        private static void Allocated(WorldSceneLayerTarget __instance)
        {if(allocationArmed&&allocationAttempts==1)preparedOne=__instance;}
        private static IEnumerable<CodeInstruction> Resize(IEnumerable<CodeInstruction> source)
        {
            int found=0;
            foreach(var item in source)
            {
                var method=item.operand as MethodInfo;
                if(item.opcode==OpCodes.Call&&method!=null&&method.DeclaringType==typeof(Array)&&method.Name=="Resize"&&method.IsGenericMethod&&method.GetGenericArguments()[0].Name=="SpriteData")
                {found++;var load=new CodeInstruction(OpCodes.Ldarg_0);load.labels.AddRange(item.labels);item.labels.Clear();yield return load;yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(NativeViewCacheLifecycleChecks),nameof(AtResize)));}
                yield return item;
            }
            Require(found==1,"cache injection binds one actual native SpriteData Array.Resize");
        }
        private static void AtResize(TileBatch value)
        {
            if(!armed||!ReferenceEquals(value,selected))return;
            armed=false;attempts++;
            int count=(int)queued.GetValue(value),capacity=((Array)queue.GetValue(value)).Length;
            Console.WriteLine("CACHE LIFECYCLE actual pre-Resize queued="+count+" capacity="+capacity+" exception="+injected.GetType().Name);
            Require(count>capacity,"native queue publication precedes controlled cache failure");throw injected;
        }
        private static void Disposed(TileBatch __instance)
        {if(ReferenceEquals(__instance,selected)){secondary++;throw new InvalidOperationException("controlled secondary owned cache TileBatch disposal failure");}}
        private static void Arm(object cache,Exception failure)
        {
            selected=(TileBatch)Get(cache,"tiles");Require(!ReferenceEquals(selected,Main.tileBatch),"cache owns the only corrupted queue");
            var old=(Array)queue.GetValue(selected);queue.SetValue(selected,Array.CreateInstance(old.GetType().GetElementType(),1));
            injected=failure;armed=true;attempts=secondary=0;Call(cache,"Invalidate");
        }
        internal static void Run(Main game,object view,Action draw)
        {
            var cache=Get(view,"Cache");hookOwner=view.GetType().Assembly.GetType("JueMingR.TerrariaHost.View.ViewHooks");Set(cache,"ForceDirect",false);Call(view,"SetUnlockEnabled",true);Main.GameZoomTarget=.3f;draw();draw();
            Allocation(view,cache,draw);
            Main.GameZoomTarget=.5f;draw();draw();Main.GameZoomTarget=.3f;draw();draw();
            var device=game.GraphicsDevice;var sharedTiles=Main.tileBatch;var sharedSprites=Main.spriteBatch;
            var native=new object[3];string[] names={"tileTarget","tile2Target","wallTarget"};for(int i=0;i<3;i++)native[i]=AccessTools.Field(typeof(Main),names[i]).GetValue(null);
            int generation=(int)Get(cache,"Generation");var layers=(WorldSceneLayerTarget[])Get(cache,"layers");var first=layers[0];
            for(int i=0;i<20;i++){Main.GameZoomTarget=i%2==0?.5f:.3f;draw();draw();}
            Main.GameZoomTarget=.75f;draw();Main.GameZoomTarget=.3f;draw();
            Require((int)Get(cache,"Generation")==generation&&ReferenceEquals(((WorldSceneLayerTarget[])Get(cache,"layers"))[0],first),"cache narrowing/re-expansion retains sufficient capacity rather than reallocating");
            Lighting.Mode=LightMode.White;draw();Require((bool)Get(view,"ControlsEnabled"),"White mode retains the independent range owner");Lighting.Mode=LightMode.Color;draw();
            var audit=new Harmony("JueMingR.Tests.View.CacheLifecycle");
            audit.Patch(AccessTools.Method(typeof(TileBatch),"GetNewSpriteBufferSlice"),transpiler:new HarmonyMethod(typeof(NativeViewCacheLifecycleChecks),nameof(Resize)));
            audit.Patch(AccessTools.Method(typeof(TileBatch),"Dispose"),postfix:new HarmonyMethod(typeof(NativeViewCacheLifecycleChecks),nameof(Disposed)));
            var nativeDraw=AccessTools.Method(typeof(Main),"Draw",new[]{typeof(GameTime)});
            var nested=new DynamicMethod("NativeViewNestedDraw",typeof(void),new[]{typeof(Main),typeof(GameTime)},typeof(NativeViewCacheLifecycleChecks),true);var il=nested.GetILGenerator();il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldarg_1);il.Emit(OpCodes.Call,nativeDraw);il.Emit(OpCodes.Ret);
            var originalDraw=(Action<Main,GameTime>)nested.CreateDelegate(typeof(Action<Main,GameTime>));
            var reentryHook=new HarmonyMethod(typeof(NativeViewCacheLifecycleChecks),nameof(Reenter)){priority=Priority.Last};
            audit.Patch(AccessTools.Method(typeof(Main),"DoDraw",new[]{typeof(GameTime)}),prefix:new HarmonyMethod(typeof(NativeViewCacheLifecycleChecks),nameof(ForeignFailure)),finalizer:reentryHook);
            try
            {
                int width=Main.screenWidth,height=Main.screenHeight,p=Main.offScreenRange;var position=Main.screenPosition;var zoom=Main.GameViewMatrix.Zoom;bool direct=Main.drawToScreen;
                ushort begin=(ushort)begins.GetValue(device),imm=(ushort)immediate.GetValue(device);
                sharedTiles.Begin();sharedTiles.Draw(TextureAssets.MagicPixel.Value,Vector2.Zero,new VertexColors(Color.Transparent));int pending=(int)queued.GetValue(sharedTiles);
                using(var foreign=new SpriteBatch(device))
                {
                    foreign.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend);
                    var error=new IOException("controlled unknown original tile queue failure");Arm(cache,error);bool propagated=false;
                    try{Call(cache,"Prepare",Get(view,"Plan"));}catch(TargetInvocationException failure){propagated=ReferenceEquals(failure.InnerException,error);}
                    Require(propagated&&attempts==1&&secondary==1,"cache leaf reports swallowed native error unchanged despite secondary cleanup failure");
                    Require((ushort)begins.GetValue(device)==begin+1&&(ushort)immediate.GetValue(device)==imm,"cache failure returns only its own lease and preserves foreign open SpriteBatch");
                    Require(ReferenceEquals(Main.tileBatch,sharedTiles)&&ReferenceEquals(Main.spriteBatch,sharedSprites)&&(int)queued.GetValue(sharedTiles)==pending,"cache failure preserves exact shared batches and unrelated native pending queue");
                    Require(Main.screenWidth==width&&Main.screenHeight==height&&Main.offScreenRange==p&&Main.screenPosition==position&&Main.GameViewMatrix.Zoom==zoom&&Main.drawToScreen==direct,"cache failure restores complete temporary logical drawing domain");
                    for(int i=0;i<3;i++)Require(ReferenceEquals(AccessTools.Field(typeof(Main),names[i]).GetValue(null),native[i]),"unknown leaf failure returns its actual entered native aliases");
                    foreign.End();
                }
                sharedTiles.End();
                Main.GameZoomTarget=1;for(int i=0;i<3;i++)draw();
                Require((bool)Get(view,"ControlsEnabled"),"unknown cache error is not classified as essential direct failure");
                Main.GameZoomTarget=.3f;draw();draw();bool map=Main.mapEnabled;var mode=Lighting.Mode;float target=Main.GameZoomTarget;
                // Ordinary native Draw can legitimately rebuild its own group.
                // Scope restoration must preserve this new entering identity,
                // not overwrite it with the pre-ordinary group's old objects.
                for(int i=0;i<3;i++)native[i]=AccessTools.Field(typeof(Main),names[i]).GetValue(null);
                Arm(cache,new OutOfMemoryException("controlled optional cache native allocation failure"));Call(cache,"Prepare",Get(view,"Plan"));
                Require(attempts==1&&secondary==1&&!(bool)Get(cache,"Attached")&&Main.GameZoomTarget==target&&Main.mapEnabled==map&&Lighting.Mode==mode,"optional cache OOM returns direct without changing target/light/map or leaking failed group");
                for(int i=0;i<20;i++)draw();
                Require(attempts==1&&Main.GameZoomTarget==target&&Main.GameViewMatrix.RenderZoom.X==.296875f&&(bool)Get(view,"ControlsEnabled"),"same failed qualification is not retried and20 actual direct Draw preserve wide camera");
                for(int i=0;i<3;i++)Require(ReferenceEquals(AccessTools.Field(typeof(Main),names[i]).GetValue(null),native[i]),"all three native aliases restored after normal/exception/fallback routes");
                Require((ushort)begins.GetValue(device)==begin&&(ushort)immediate.GetValue(device)==imm,"no cache batch lease escapes20 subsequent native Draw");
                // Unlike the foreign-lease case above, enter through actual
                // Main.Draw too: its own missing finally must not turn later
                // calls into guard-short-circuited no-ops after our new Fill.
                Call(cache,"Dispose");Main.GameZoomTarget=.3f;draw();draw();
                var actualError=new IOException("controlled unknown cached Fill through actual Main.Draw");Arm(cache,actualError);bool actualPropagated=false;reentries=0;reenter=()=>originalDraw(Main.instance,new GameTime());
                try{draw();}catch(Exception error){while(error is TargetInvocationException)error=error.InnerException;actualPropagated=ReferenceEquals(error,actualError);}
                Require(actualPropagated&&attempts==1&&reentries==1,"actual Main.Draw cached Fill preserves its first error through one real blocked nested Draw");
                Main.GameZoomTarget=1;int frames=Main.fpsCount;for(int i=0;i<3;i++)draw();
                Require(Main.fpsCount==frames+3,"unknown Fill recovery permits three real subsequent native Draw rather than a stuck outer guard");
                var guard=AccessTools.Field(typeof(Main),"_isDrawingOrUpdating");bool priorGuard=(bool)guard.GetValue(Main.instance);foreignError=new IOException("controlled foreign original DoDraw prefix error outside VIEW scopes");foreignArmed=true;bool foreignExact=false;
                try
                {
                    try{draw();}catch(Exception error){while(error is TargetInvocationException)error=error.InnerException;foreignExact=ReferenceEquals(error,foreignError);}
                    Require(foreignExact&&(bool)guard.GetValue(Main.instance),"unrelated native error stays original and VIEW does not alter its native missing-finally guard behavior");
                }
                finally{foreignArmed=false;guard.SetValue(Main.instance,priorGuard);Terraria.Testing.DetailedFPS.End();}
                Console.WriteLine("PASS CACHE OWNERSHIP one real blocked nested Draw preserves outer first error/true guard; unrelated native error is untouched, fixture manually restores its own baseline guard (no product recovery claim).");
                Console.WriteLine("PASS CACHE LIFECYCLE capacity reused; native partial-queue IO propagated with secondary dispose, foreign lease/shared pending preserved;3 ordinary Draw;optional OOM unchanged target/settings then20 direct Draw/no retry.");
            }
            finally{armed=foreignArmed=false;reenter=null;hookOwner=null;audit.UnpatchAll(audit.Id);selected=null;injected=foreignError=null;}
        }
        private static void Allocation(object view,object cache,Action draw)
        {
            // Force a real grow from2035x1195 to2048x1208 on Reach. The first
            // new native RT exists when the second constructor fails; this is
            // an atomic prepare test, not native InitTargets/OOM probing.
            Call(cache,"Dispose");Main.GameZoomTarget=.3f;draw();draw();
            bool map=Main.mapEnabled;var mode=Lighting.Mode;var old=((WorldSceneLayerTarget[])Get(cache,"layers"))[0];
            var audit=new Harmony("JueMingR.Tests.View.CacheAllocation");var constructor=typeof(WorldSceneLayerTarget).GetConstructor(new[]{typeof(GraphicsDevice),typeof(int),typeof(int)});
            audit.Patch(constructor,prefix:new HarmonyMethod(typeof(NativeViewCacheLifecycleChecks),nameof(Allocate)),postfix:new HarmonyMethod(typeof(NativeViewCacheLifecycleChecks),nameof(Allocated)));
            allocationAttempts=0;preparedOne=null;
            try
            {
                Main.GameZoomTarget=.5f;draw();allocationArmed=true;draw();
                Require(allocationAttempts==2&&preparedOne!=null&&preparedOne.Texture.IsDisposed&&old.Texture.IsDisposed,"partial new real RT group and prior optional group are safely retired after second allocation fails");
                Require(Main.GameZoomTarget==.5f&&Main.GameViewMatrix.RenderZoom.X==.5f&&Lighting.Mode==mode&&Main.mapEnabled==map&&(bool)Get(view,"ControlsEnabled"),"optional RT allocation failure preserves actual wide target/settings and direct owner");
                for(int i=0;i<20;i++)draw();Require(allocationAttempts==2,"same RT failure qualification never retries in20 actual subsequent Draw");
                Console.WriteLine("PASS CACHE ALLOCATION real new RT1 allocated/RT2 controlled OOM; partial/new and old disposed, same .5 target/settings,20 direct Draw/no retry, no original InitTargets OOM probe.");
            }
            finally{allocationArmed=false;audit.UnpatchAll(audit.Id);preparedOne=null;}
            Main.GameZoomTarget=.3f;draw();draw();
        }
    }
}
