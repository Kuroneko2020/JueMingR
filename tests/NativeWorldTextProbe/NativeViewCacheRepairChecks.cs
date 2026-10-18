using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics;
using Terraria.Graphics.Light;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // R6/R7/R8 finite counterexamples use the original initialized Main and
    // actual Draw. The same test assembly may load the preserved c439 Host.
    internal static class NativeViewCacheRepairChecks
    {
        private static object cache;
        private static SpriteBatch caller;
        private static bool armed,persistent;
        private static Exception injected,secondary,optional;
        private static int attempts,tiles;
        private static SpriteBatch foreign;
        private static Texture2D pixel;
        private static void Optional(OutOfMemoryException __0){optional=__0;}
        private static void Tiles(bool __runOriginal){if(__runOriginal)tiles++;}
        private static void Submit(SpriteBatch __instance)
        {
            if(!armed)return;
            if(caller==null)
            {
                if(!(bool)Get(cache,"Attached")||(bool)Get(cache,"Rendering")||!ReferenceEquals(__instance,Main.spriteBatch))return;
                if(!new StackTrace().GetFrames().Any(f=>f.GetMethod().Name=="Composite"&&f.GetMethod().DeclaringType?.FullName=="JueMingR.TerrariaHost.View.ViewTileCache"))return;
                caller=__instance;
                foreign.Begin();foreign.Draw(pixel,Vector2.Zero,Color.White);
            }
            if(!ReferenceEquals(caller,__instance))return;
            attempts++;if(!persistent)armed=false;
            Console.WriteLine("R6 actual caller End/SetRenderState attempt="+attempts+" queued="+Get(__instance,"spriteQueueCount")+" exception="+(attempts==1?injected:secondary).GetType().Name);
            throw attempts==1?injected:secondary;
        }
        internal static void Caller(Main game,object view,Action draw)
        {
            cache=Get(view,"Cache");var audit=new Harmony("JueMingR.Tests.View.R6Caller");
            audit.Patch(AccessTools.Method(typeof(SpriteBatch),"SetRenderState"),prefix:new HarmonyMethod(typeof(NativeViewCacheRepairChecks),nameof(Submit)));
            audit.Patch(AccessTools.Method(typeof(Main),"DrawTiles"),postfix:new HarmonyMethod(typeof(NativeViewCacheRepairChecks),nameof(Tiles)));
            audit.Patch(view.GetType().Assembly.GetType("JueMingR.TerrariaHost.View.ViewOptionalCacheFailure").GetConstructor(BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(OutOfMemoryException)},null),prefix:new HarmonyMethod(typeof(NativeViewCacheRepairChecks),nameof(Optional)));
            var begins=AccessTools.Field(typeof(GraphicsDevice),"spriteBeginCount");
            try
            {
                foreach(bool repeat in new[]{false,true})foreach(bool oom in new[]{false,true})
                {
                    Call(cache,"Dispose");Main.GameZoomTarget=.5f;Call(view,"SetUnlockEnabled",true);for(int i=0;i<3;i++)draw();
                    Require(Get(Get(view,"Plan"),"Route").ToString()=="TileCache","R6 reaches real cached route");
                    var shared=Main.spriteBatch;var sharedTiles=Main.tileBatch;var bindings=game.GraphicsDevice.GetRenderTargets();
                    var ownedField=cache.GetType().GetField("worldSprites",BindingFlags.Instance|BindingFlags.NonPublic);
                    if(ownedField!=null){var stable=(SpriteBatch)ownedField.GetValue(cache);draw();Require(ReferenceEquals(stable,ownedField.GetValue(cache))&&!stable.IsDisposed&&ReferenceEquals(shared,Main.spriteBatch),"R6 stable owned world batch returns shared identity before UI/end and is reused on next healthy Draw");}
                    // A separate foreign deferred lease/pending sprite must not
                    // be disposed, flushed or cleared by our failed caller.
                    using(foreign=new SpriteBatch(game.GraphicsDevice))
                    using(pixel=new Texture2D(game.GraphicsDevice,1,1))
                    {
                        pixel.SetData(new[]{Color.White});
                        ushort entry=(ushort)((ushort)begins.GetValue(game.GraphicsDevice)+1);int pending=1;
                        injected=oom?(Exception)new OutOfMemoryException("R6 caller submit primary"):new IOException("R6 caller submit primary");secondary=new IOException("R6 persistent caller submit secondary");optional=null;caller=null;persistent=repeat;attempts=0;armed=true;
                        bool exact=false,escaped=false;
                        try{draw();}catch(Exception error){escaped=true;while(error is TargetInvocationException)error=error.InnerException;exact=ReferenceEquals(error,injected);}
                        armed=false;
                        Console.WriteLine("R6 RESULT repeat="+repeat+" oom="+oom+" attempts="+attempts+" lease="+entry+"->"+begins.GetValue(game.GraphicsDevice)+" external="+escaped);
                        Require(attempts>0&&(oom?!escaped&&ReferenceEquals(optional,injected):exact),"R6 first caller exception preserved; optional OOM internal control never escapes");
                        Require(ReferenceEquals(shared,Main.spriteBatch)&&ReferenceEquals(sharedTiles,Main.tileBatch)&&(ushort)begins.GetValue(game.GraphicsDevice)==entry&&(int)Get(foreign,"spriteQueueCount")==pending&&(bool)Get(foreign,"inBeginEndPair")&&!foreign.IsDisposed,"R6 caller scope returns shared identities/exact foreign lease and pending queue without cleanup retry dependency");
                        Require(game.GraphicsDevice.GetRenderTargets().SequenceEqual(bindings),"R6 render-target bindings returned");
                        if(!ReferenceEquals(caller,shared))Require(caller.IsDisposed&&!shared.IsDisposed,"R6 only the owned failed caller is retired, not the original shared batch");
                        foreign.End();
                    }
                    if(!oom)Main.GameZoomTarget=1;
                    int frames=Main.fpsCount;tiles=0;int n=oom?20:3;for(int i=0;i<n;i++)draw();
                    Require(Main.fpsCount==frames+n&&tiles>=n,"R6 following3/20 real native DrawTiles execute");
                    if(oom)Require(Main.GameZoomTarget==.5f&&Get(Get(view,"Plan"),"Route").ToString()=="Direct"&&(bool)Get(view,"ControlsEnabled"),"R6 optional caller failure preserves target/owner and stays Direct");
                    Console.WriteLine("PASS R6 caller="+(repeat?"persistent+secondary":"single")+" "+injected.GetType().Name+" first object/foreign pending/leases/RT/shared; subsequent true Draw="+n);
                }
            }
            finally{armed=false;caller=null;cache=null;audit.UnpatchAll(audit.Id);}
        }
        internal static void Dirty(Main game,object view,Action draw,string output)
        {
            var c=Get(view,"Cache");Call(c,"Dispose");var origin=Main.LocalPlayer.position;var tile=Main.tile[315,591];var saved=(Tile)tile.Clone();
            try
            {
                Main.GameZoomTarget=.5f;Call(view,"SetUnlockEnabled",true);Set(c,"ForceDirect",false);for(int i=0;i<4;i++)draw();
                foreach(bool requested in new[]{true,false})
                {
                    Main.LocalPlayer.position=origin;Main.renderCount=0;Main.renderNow=false;draw();draw();
                    var layer=((WorldSceneLayerTarget[])Get(c,"layers"))[0];var before=new Color[layer.Texture.Width*layer.Texture.Height];layer.Texture.GetData(before);int refresh=((int[])Get(c,"Refreshes"))[0];
                    tile.active(!tile.active());tile.type=Terraria.ID.TileID.Stone;WorldGen.TileFrame(315,591);
                    Main.LocalPlayer.position=origin+new Vector2(1,1);Main.renderCount=requested?0:3;Main.renderNow=requested;draw();
                    Require(Get(Get(view,"Plan"),"Route").ToString()=="Direct","R7 dirty native refresh occurs on fractional-phase Direct");
                    Main.LocalPlayer.position=origin;Main.renderCount=0;Main.renderNow=false;draw();draw();
                    layer=((WorldSceneLayerTarget[])Get(c,"layers"))[0];var after=new Color[before.Length];layer.Texture.GetData(after);int changed=before.Where((v,i)=>v!=after[i]).Count();
                    Console.WriteLine("R7 dirty="+(requested?"renderNow":"native-group3")+" refresh="+refresh+"->"+((int[])Get(c,"Refreshes"))[0]+" pixels="+changed+" route="+Get(Get(view,"Plan"),"Route"));
                    using(var file=File.Create(Path.Combine(output,"r7-"+(requested?"renderNow":"group3")+"-returned-cache.png")))layer.Texture.SaveAsPng(file,layer.Texture.Width,layer.Texture.Height);
                    Require(Get(Get(view,"Plan"),"Route").ToString()=="TileCache"&&((int[])Get(c,"Refreshes"))[0]>refresh&&changed>0,"R7 dirty persists across Direct and returning non-group3 phase; real edited pixels refreshed");
                }
                Console.WriteLine("PASS R7 native renderNow/group3 retained as content invalidity across fractional Direct; no new refresh clock.");
            }
            finally{Main.LocalPlayer.position=origin;tile.CopyFrom(saved);WorldGen.TileFrame(315,591);Main.renderNow=true;Main.renderCount=0;draw();}
        }
        private static int Subscriptions(GraphicsDevice device,object target)
        {
            int count=0;for(Type type=device.GetType();type!=null;type=type.BaseType)
            foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly))
                if(typeof(Delegate).IsAssignableFrom(field.FieldType)){var value=field.GetValue(device) as Delegate;if(value!=null)count+=value.GetInvocationList().Count(d=>ReferenceEquals(d.Target,target));}
            return count;
        }
        internal static void Motion(object view,Action draw,string output)
        {
            var c=Get(view,"Cache");var origin=Main.LocalPlayer.position;
            try
            {
                foreach(float target in new[]{.5f,.3f})foreach(bool direct in new[]{true,false,true,false})
                {
                    Main.LocalPlayer.position=origin;Main.GameZoomTarget=target;Set(c,"ForceDirect",direct);Main.renderNow=true;for(int i=0;i<6;i++)draw();
                    var rows=new System.Collections.Generic.List<string>{"draw_wall_ms,native_renderCount,route,refresh0"};var times=new double[32];int cached=0;var clock=new[]{3,0,1,2};long allocated=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                    for(int i=0;i<32;i++)
                    {
                        Main.LocalPlayer.position+=new Vector2(1,1);Main.renderCount=clock[i%4];Main.renderNow=false;var watch=Stopwatch.StartNew();draw();watch.Stop();times[i]=watch.Elapsed.TotalMilliseconds;
                        string route=Get(Get(view,"Plan"),"Route").ToString();if(route=="TileCache")cached++;
                        rows.Add(times[i].ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+clock[i%4]+","+route+","+((int[])Get(c,"Refreshes"))[0]);
                    }
                    string prefix="r7-motion-"+target.ToString(System.Globalization.CultureInfo.InvariantCulture)+"-"+(direct?"direct":"auto");int pass=Directory.GetFiles(output,prefix+"*.csv").Length+1;File.WriteAllLines(Path.Combine(output,prefix+pass+".csv"),rows);
                    Console.WriteLine("R7 MOTION target="+target+" override="+(direct?"direct":"auto")+" pass="+pass+" controlled-native-groups=3/0/1/2 n=32 mean="+times.Average()+" max="+times.Max()+" allocated="+(AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-allocated)+" cached="+cached+" direct="+(32-cached));
                }
                Console.WriteLine("PASS R7 representative motion costs retain all samples including native-group3/fractional phase coupling; fixture-controlled original group values, no product clock/full native Update/FPS claim.");
            }
            finally{Main.LocalPlayer.position=origin;Main.renderCount=0;Main.renderNow=true;Set(c,"ForceDirect",false);}
        }
        internal static void Exit(Main game,object view,Action draw,Action<int,int> resize)
        {
            resize(2560,1080);Main.GameZoomTarget=.9f;Call(view,"SetUnlockEnabled",true);for(int i=0;i<4;i++)draw();
            var n=Get(view,"Native");var a=Get(n,"tiles");var b=(SpriteBatch)Get(n,"sprites");var inside=(SpriteBatch)Get(a,"_spriteBatch");int subscriptions=Subscriptions(game.GraphicsDevice,n);int generation=(int)Get(n,"BatchGeneration");
            Func<string,object> field=name=>n.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(n);
            var vertex=(GraphicsResource)Get(a,"_vertexBuffer");var index=(GraphicsResource)Get(a,"_indexBuffer");
            Require(a!=null&&b!=null&&subscriptions>=2,"R8 real expanded Native owns batches and device subscriptions");
            Call(view,"SetUnlockEnabled",false);draw();Require(ReferenceEquals(a,Get(n,"tiles"))&&(int)Get(n,"BatchGeneration")==generation,"R8 OFF-Retained Native preserves reusable batches");
            Main.GameZoomTarget=.75f;draw();Main.GameZoomTarget=.9f;draw();Require(ReferenceEquals(a,Get(n,"tiles")),"R8 Native/Direct transition does not hot recreate Native batches");
            Main.GameZoomTarget=1;int frames=Main.fpsCount;for(int i=0;i<3;i++)draw();
            Console.WriteLine("R8 normal subscriptions="+subscriptions+"->"+Subscriptions(game.GraphicsDevice,n)+" device-null="+(field("device")==null));
            Require(field("tiles")==null&&field("sprites")==null&&field("device")==null&&b.IsDisposed&&inside.IsDisposed&&(vertex==null||vertex.IsDisposed)&&(index==null||index.IsDisposed)&&Subscriptions(game.GraphicsDevice,n)==0&&Main.fpsCount==frames+3,"R8 ordinary context retires Native private objects/subscriptions then3 real native Draw");
            Console.WriteLine("PASS R8 expanded Native->OFF-Retained/Direct stable->ordinary disposed/subscriptions0; no global native/wave owner release.");
        }
    }
}
