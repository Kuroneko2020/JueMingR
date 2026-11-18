using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // This scope records the unchanged product first. Instrumentation observes
    // the batch's requested state, not GraphicsDevice's deferred, stale state.
    internal static class NativeCompanionFontChecks
    {
        private sealed class Stamp
        {
            internal string Text; internal Vector2 Origin; internal float Scale;
            internal SamplerState Sampler; internal DynamicSpriteFont Font;
        }
        private static readonly List<Stamp> stamps=new List<Stamp>();
        private static SamplerState requested;
        private static bool recording;
        private static void Begin(SpriteBatch __instance,SamplerState __2)
        {if(ReferenceEquals(__instance,Main.spriteBatch))requested=__2??SamplerState.LinearClamp;}
        private static void Text(SpriteBatch __0,DynamicSpriteFont __1,string __2,float __3,float __4,float __8)
        {if(recording&&ReferenceEquals(__0,Main.spriteBatch))stamps.Add(new Stamp{Text=__2,Origin=new Vector2(__3,__4),Scale=__8,Sampler=requested,Font=__1});}
        // The optional new primitive uses the same observation semantics; no
        // product branch is replaced by the oracle or by this test hook.
        private static void Scaled(SpriteBatch __0,DynamicSpriteFont __1,string __2,Vector2 __3,Color __4,float __5)
        {if(recording&&ReferenceEquals(__0,Main.spriteBatch))stamps.Add(new Stamp{Text=__2,Origin=__3,Scale=__5,Sampler=requested,Font=__1});}
        private static object Rect(Assembly a,float x,float y,float w,float h)
        {return Activator.CreateInstance(a.GetType("JueMingR.TerrariaHost.F5.F5Rect"),BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{x,y,w,h},null);}
        private static object Size(Assembly a)
        {return Activator.CreateInstance(a.GetType("JueMingR.TerrariaHost.F5.F5Size"),BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{0f,0f,0f,0f},null);}
        private static void Save(ProbeGraphics graphics,string path,Color[] pixels,int width,int height)
        {using(var texture=new Texture2D(graphics.GraphicsDevice,width,height)){texture.SetData(pixels);using(var file=File.Create(path))texture.SaveAsPng(file,width,height);}}
        private static int Difference(Color[] a,Color[] b)
        {int count=0;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])count++;return count;}
        private static IEnumerable Controls(object window){return (IEnumerable)Get(window,"controls");}
        private static object Control(object window,string prefix)
        {return Controls(window).Cast<object>().Single(c=>((string)Get(c,"Key")).StartsWith(prefix,StringComparison.Ordinal));}
        internal static void Run(object context,ProbeGraphics graphics,string output)
        {
            NativeCombatAimChecks.Initialize();Main.spawnTileX=60;Main.spawnTileY=58;Main.leftWorld=Main.topWorld=0;Main.rightWorld=Main.maxTilesX*16;Main.bottomWorld=Main.maxTilesY*16;
            for(int x=2;x<118;x++){Main.tile[x,60].active(true);Main.tile[x,60].type=TileID.Stone;}
            new Harmony("JueMingR.Tests.QuickItemOutlets").Unpatch(AccessTools.Method(typeof(Player),"Spawn"),HarmonyPatchType.Prefix,"JueMingR.Tests.QuickItemOutlets");
            NativeCompanionCoreLifecycleChecks.PrepareHuman();graphics.LoadCompanionAssets();
            var host=Get(context,"Companions");var archive=Get(host,"profiles");var shell=Get(context,"Shell");var window=Get(shell,"CompanionUi");var assembly=window.GetType().Assembly;
            NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return (bool)Get(archive,"Ready");});
            Require((bool)Call(host,"Create","字体检查人物"),"font sample uses a real profile");Guid id=(Guid)Get(host,"LastCreatedId");
            NativeQuickItemChecks.Until(()=>{Call(host,"Update",(ulong)Main.GameUpdateCount);return (bool)Call(archive,"IsSaved",id);});
            var body=(Player)Call(host,"GetBody",id);graphics.LoadItemTextures(Main.LocalPlayer.inventory.Concat(body.inventory).Where(i=>i!=null&&!i.IsAir).Select(i=>i.type).Distinct());
            Main.screenWidth=1280;Main.screenHeight=800;PlayerInput.CacheOriginalScreenDimensions();Main.UIScale=1;
            var shellRenderer=Get(shell,"renderer");Call(shellRenderer,"RefreshResources");Call(shellRenderer,"Prepare",Get(shell,"State"),1280f,800f,1f);
            Set(window,"selected",id);Call(shell,"OpenCompanions",Get(Get(Get(shell,"State"),"Layout"),"Viewport"));
            NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false);NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false);
            NativeToolsUiChecks.Click(context,NativeToolsUiChecks.Point(Get(Control(window,"configuration/"),"Rect")));
            var renderer=Get(window,"renderer");var oracle=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.F5.BlueprintLibraryRenderer"),true);Call(oracle,"Refresh");
            var harmony=new Harmony("JueMingR.Tests.CompanionFont");var flags=BindingFlags.Static|BindingFlags.NonPublic;
            var begin=typeof(SpriteBatch).GetMethods().Single(m=>m.Name=="Begin"&&m.GetParameters().Length==7);
            harmony.Patch(begin,prefix:new HarmonyMethod(typeof(NativeCompanionFontChecks).GetMethod("Begin",flags)));
            harmony.Patch(AccessTools.Method(typeof(Utils),"DrawBorderStringFourWay"),prefix:new HarmonyMethod(typeof(NativeCompanionFontChecks).GetMethod("Text",flags)));
            var primitive=assembly.GetType("JueMingR.TerrariaHost.F5.F5ControlRenderer").GetMethod("DrawScaledOutline",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
            if(primitive!=null)harmony.Patch(primitive,prefix:new HarmonyMethod(typeof(NativeCompanionFontChecks).GetMethod("Scaled",flags)));
            bool allLinear=true,allOracle=true;
            try
            {
                foreach(float factor in new[]{1.2f,1.5f})
                {
                    Matrix matrix=Matrix.CreateScale(factor);Call(window,"Prepare",matrix,new Vector2(1280,800));
                    foreach(var parent in new[]{SamplerState.PointClamp,SamplerState.LinearClamp})
                    {
                        stamps.Clear();recording=true;
                        var pixels=graphics.Pixels(()=>{
                            Main.spriteBatch.End();graphics.GraphicsDevice.SamplerStates[0]=parent;
                            Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,parent,DepthStencilState.None,RasterizerState.CullNone,null,matrix);
                            Call(window,"Draw");
                        },matrix,1280,800);recording=false;
                        string tag=factor.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+"-"+(ReferenceEquals(parent,SamplerState.PointClamp)?"parent-point":"parent-linear");
                        Save(graphics,Path.Combine(output,"font-window-"+tag+".png"),pixels,1280,800);
                        foreach(var stamp in stamps.Where(s=>ReferenceEquals(s.Font,FontAssets.MouseText.Value)))
                        {
                            bool linear=ReferenceEquals(stamp.Sampler,SamplerState.LinearClamp);allLinear&=linear;
                            Console.WriteLine("FONT-TRACE "+tag+" text="+stamp.Text+" glyphScale="+stamp.Scale+" physicalScale="+(stamp.Scale*factor)+" origin="+stamp.Origin+" sampler="+(linear?"LinearClamp":"OTHER"));
                        }
                    }
                    // Compare the actual ItemsRenderer outlet against the
                    // previously accepted private Blueprint Text implementation.
                    // Both use the exact captured glyph origin and native font.
                    foreach(float scale in new[]{.55f,.65f,.7f,.85f})
                    {
                        const string text="跟随中 攻击模式 114514";Stamp sample=null;
                        stamps.Clear();recording=true;
                        var actual=graphics.Pixels(()=>Call(renderer,"Pass",matrix,Rect(assembly,0,0,700,140),(Action)(()=>Call(renderer,"Text",text,Rect(assembly,24.35f,30.2f,600,60),Color.White,scale))),matrix,960,220);
                        recording=false;sample=stamps.Single(s=>s.Text==text);
                        var reference=graphics.Pixels(()=>{
                            Main.spriteBatch.End();Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.LinearClamp,DepthStencilState.None,RasterizerState.CullNone,null,matrix);
                            Call(oracle,"Text",Main.spriteBatch,text,Rect(assembly,sample.Origin.X,sample.Origin.Y,0,0),Size(assembly),scale,Color.White,false);
                        },matrix,960,220);
                        int different=Difference(actual,reference);allOracle&=different==0;
                        string tag=factor.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+"-glyph-"+scale.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture);
                        Save(graphics,Path.Combine(output,"font-product-"+tag+".png"),actual,960,220);Save(graphics,Path.Combine(output,"font-blueprint-"+tag+".png"),reference,960,220);
                        Console.WriteLine("FONT-ORACLE "+tag+" same native font/origin/matrix different="+different);
                    }
                }
                Console.WriteLine("FONT-SUMMARY queuedLinear="+allLinear+" existingBlueprintPixels="+allOracle);
                Require(allLinear,"all normal companion text remains in its explicit LinearClamp pass after nested native portraits and image passes");
                Require(allOracle,"small companion glyphs use the existing scale-aware Blueprint outline at identical native font/origin/matrix");
            }
            finally {recording=false;harmony.UnpatchAll(harmony.Id);Call(window,"Close");((IDisposable)oracle).Dispose();NativeToolsUiChecks.Mouse(Vector2.Zero,false);}
        }
    }
}

