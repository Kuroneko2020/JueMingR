using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Utilities;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatWorkerAssetChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        internal static void Initialize()
        {
            foreach(var array in new[]{TextureAssets.Npc,TextureAssets.Gore})
                for(int i=0;i<array.Length;i++)array[i]=Asset("fixture-unloaded-"+i);
        }
        private static Asset<Texture2D> Asset(string name)
        {return (Asset<Texture2D>)typeof(Asset<Texture2D>).GetConstructor(Flags,null,new[]{typeof(string)},null).Invoke(new object[]{name});}
        private static Asset<Texture2D> Loaded(string name,int width,int height)
        {
            // The independent oracle uses the real, unpatched XNA width/height
            // getters (plain fields in this locked runtime), with no device,
            // native pointer, content load, or texture constructor. The helper
            // receives only values and must not depend on this fixture object.
            var texture=(Texture2D)FormatterServices.GetUninitializedObject(typeof(Texture2D));GC.SuppressFinalize(texture);
            typeof(Texture2D).GetField("_width",Flags).SetValue(texture,width);
            typeof(Texture2D).GetField("_height",Flags).SetValue(texture,height);
            var asset=Asset(name);
            typeof(Asset<Texture2D>).GetField("<Value>k__BackingField",Flags).SetValue(asset,texture);
            typeof(Asset<Texture2D>).GetField("<State>k__BackingField",Flags).SetValue(asset,AssetState.Loaded);
            if(texture.GraphicsDevice!=null)throw new InvalidOperationException("Metadata fixture unexpectedly has a graphics device.");
            return asset;
        }
        internal static void Run(Assembly host,string layout,string output)
        {
            NativeCombatWorkerChecks.Scene(false);
            var asset=Loaded("fixture-harpy-metadata",48,48*Main.npcFrameCount[48]);
            TextureAssets.Npc[48]=asset;Main.dedServ=false;
            Main.npc[0].SetDefaults(48);Main.npc[0].active=true;Main.npc[0].whoAmI=0;Main.npc[0].position=new Vector2(480,480);Main.npc[0].velocity=new Vector2(2,-1);Main.npc[0].target=0;
            if(Main.npc[0].frame.Height!=48 || !asset.IsLoaded)throw new InvalidOperationException("Invalid CPU-only client metadata fixture.");
            var capture=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true).GetMethod("Capture",Flags);
            byte[] snapshot=(byte[])capture.Invoke(null,new object[]{new[]{0},0,1000L,120}),future;
            using(var child=NativeCombatWorkerChecks.Start(layout))
            {
                var errors=child.StandardError.ReadToEndAsync();future=NativeCombatWorkerChecks.AcquireFrozen(host,child,new[]{0},new int[0],0).Future;
                NativeCombatWorkerChecks.Exit(child,"client-frame helper exits");
                File.WriteAllText(Path.Combine(output,"client-frame-worker.log"),errors.Result);
            }
            if(BitConverter.ToInt32(future,0)!=NativeCombatWorkerChecks.ExpectedProtocol)throw new InvalidOperationException("Client texture metadata and native frame result were refused.");
            NativeCombatWorkerChecks.Compare(future,0,output,"client-harpy-frame");
            bool animated=false;for(int step=1;step<=120;step++)animated|=BitConverter.ToInt32(future,20+step*NativeCombatWorkerChecks.PointBytes+45)!=0;
            if(!animated)throw new InvalidOperationException("Client fixture did not exercise animation during its frozen future.");
            Console.WriteLine("PASS client FindFrame frozen 120 updates with real CPU-only original texture metadata; no graphics device.");
            Main.dedServ=true;
            GoldLight(host,layout,output);
        }
        private static void GoldLight(Assembly host,string layout,string output)
        {
            Lighting.Mode=Terraria.Graphics.Light.LightMode.Color;Lighting.GlobalBrightness=1;
            var engine=typeof(Lighting).GetField("NewEngine",Flags).GetValue(null);var engineType=engine.GetType();
            var map=(Terraria.Graphics.Light.LightMap)engineType.GetField("_activeLightMap",Flags).GetValue(engine);var area=engineType.GetField("_activeProcessedArea",Flags);object prior=area.GetValue(engine);
            using(var child=NativeCombatWorkerChecks.Start(layout))
            {
                var errors=child.StandardError.ReadToEndAsync();
                try
                {
                    foreach(int brightness in new[]{0,21,255})
                    {
                        NativeCombatWorkerChecks.Scene(false);NPC.ClearAll();Main.LocalPlayer.immune=true;Main.LocalPlayer.immuneTime=100000;
                        map.SetSize(120,120);area.SetValue(engine,new Rectangle(0,0,120,120));
                        for(int x=0;x<120;x++)for(int y=0;y<120;y++)map[x,y]=new Vector3(brightness/255f);
                        int slot=NPC.NewNPC(new EntitySource_DebugCommand(),550,800,Terraria.ID.NPCID.GoldBunny,Start:1);
                        for(int i=0;i<100;i++)NativeCombatSegmentedPredictionChecks.Advance();
                        if(!Main.npc[slot].active || Main.npc[slot].friendly)throw new InvalidOperationException("Natural gold critter reaches its vulnerable stage.");
                        var scene=NativeCombatWorkerChecks.AcquireFrozen(host,child,new[]{slot},new int[0],slot);
                        NativeCombatWorkerChecks.Compare(scene.Future,slot,output,"gold-light-"+brightness);
                    }
                    Console.WriteLine("PASS actual original dark / dim RNG-consuming / bright gold-critter lighting branch, independent frozen120 and no omitted dust RNG");
                }
                finally{area.SetValue(engine,prior);NativeCombatWorkerChecks.Exit(child,"gold light helper EOF");File.WriteAllText(Path.Combine(output,"gold-light-worker.log"),errors.Result);}
            }
        }
        internal static void GoreMetadata(Assembly host)
        {
            var metadata=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeAssetSnapshot",true);
            var effect=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeEffectBoundary",true);
            var oldNpc=(Asset<Texture2D>[])TextureAssets.Npc.Clone();var oldGore=(Asset<Texture2D>[])TextureAssets.Gore.Clone();
            try
            {
                foreach(bool loaded in new[]{false,true})
                {
                    for(int i=411;i<=430;i++)TextureAssets.Gore[i]=loaded?Loaded("fixture-gore-"+i,24,32):Asset("fixture-gore-unloaded-"+i);
                    string[] expected={GoreResult(600),GoreResult(0)};byte[] snapshot;
                    using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
                    {metadata.GetMethod("WriteFixture",Flags).Invoke(null,new object[]{writer});snapshot=stream.ToArray();}
                    // First freeze unpatched original outcomes, then install the
                    // product metadata bridge and compare both state and RNG.
                    using(var boundary=(IDisposable)Activator.CreateInstance(effect,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{true},null))
                    {
                        using(var reader=new BinaryReader(new MemoryStream(snapshot)))metadata.GetMethod("Read",Flags).Invoke(null,new object[]{reader});
                        for(int i=0;i<2;i++)if(GoreResult(i==0?600:0)!=expected[i])throw new InvalidOperationException("Gore metadata/RNG differs from original, loaded="+loaded+" case="+i);
                    }
                    Array.Copy(oldNpc,TextureAssets.Npc,oldNpc.Length);Array.Copy(oldGore,TextureAssets.Gore,oldGore.Length);
                }
            }
            finally{Array.Copy(oldNpc,TextureAssets.Npc,oldNpc.Length);Array.Copy(oldGore,TextureAssets.Gore,oldGore.Length);Gore.goreTime=600;}
            Console.WriteLine("PASS loaded/unloaded Gore metadata, source rectangles and native RNG for normal/zero goreTime.");
        }
        private static string GoreResult(int time)
        {
            Main.rand=new UnifiedRandom(123);Gore.goreTime=time;
            for(int i=0;i<Main.gore.Length;i++)Main.gore[i]=new Gore();
            var gore=Main.gore[Gore.NewGore(new Vector2(500,500),new Vector2(2,3),411,1.5f)];
            gore.Frame=new Terraria.DataStructures.SpriteFrame(2,4){CurrentColumn=1,CurrentRow=2};
            return gore.position+"|"+gore.velocity+"|"+gore.type+"|"+gore.timeLeft+"|"+gore.Width+"|"+gore.Height+"|"+gore.AABBRectangle+"|"+NativeCombatWorkerChecks.RandomStamp();
        }
    }
}
