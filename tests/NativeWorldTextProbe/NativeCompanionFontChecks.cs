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
        private static bool Linear(SamplerState value)
        {return value!=null&&value.Filter==TextureFilter.Linear&&value.AddressU==TextureAddressMode.Clamp&&value.AddressV==TextureAddressMode.Clamp;}
        private static bool Point(SamplerState value)
        {return value!=null&&value.Filter==TextureFilter.Point&&value.AddressU==TextureAddressMode.Clamp&&value.AddressV==TextureAddressMode.Clamp;}
        private static string Sampling(SamplerState value)
        {return value==null?"null":value.Filter+"/"+value.AddressU+"/"+value.AddressV+"/"+value.AddressW;}
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
        private static void Background()
        {Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value,new Rectangle(0,0,960,220),new Color(48,52,117));}
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
            bool filterOnly=Environment.GetEnvironmentVariable("JUEMINGR_FONT_BASELINE_FILTER_ONLY")=="1";
            try
            {
                foreach(float factor in filterOnly?new[]{1.2f}:new[]{1.2f,1.5f})
                {
                    Matrix matrix=Matrix.CreateScale(factor);Call(window,"Prepare",matrix,new Vector2(1280,800));
                    foreach(var parent in filterOnly?new[]{SamplerState.LinearClamp}:new[]{SamplerState.PointClamp,SamplerState.LinearClamp})
                    {
                        stamps.Clear();recording=true;
                        var pixels=graphics.Pixels(()=>{
                            Main.spriteBatch.End();graphics.GraphicsDevice.SamplerStates[0]=parent;
                            Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,parent,DepthStencilState.None,RasterizerState.CullNone,null,matrix);
                            Call(window,"Draw");
                        },matrix,1280,800);recording=false;
                        string tag=factor.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+"-"+(ReferenceEquals(parent,SamplerState.PointClamp)?"parent-point":"parent-linear");
                        Save(graphics,Path.Combine(output,"font-window-"+tag+".png"),pixels,1280,800);
                        var normal=stamps.Where(s=>ReferenceEquals(s.Font,FontAssets.MouseText.Value)).ToArray();
                        foreach(string required in filterOnly ? new[]{"配置","恢复行为设置","攻击","攻击模式","字体检查人物"} :
                            new[]{"配置","恢复行为设置","允许攻击","主要武器","字体检查人物"})
                            Require(normal.Any(s=>s.Text==required),"font trace actually reaches the expected window outlet: "+required);
                        foreach(var stamp in normal)
                        {
                            bool linear=Linear(stamp.Sampler);allLinear&=linear;
                            Console.WriteLine("FONT-TRACE "+tag+" text="+stamp.Text+" glyphScale="+stamp.Scale+" physicalScale="+(stamp.Scale*factor)+" origin="+stamp.Origin+" sampler="+Sampling(stamp.Sampler));
                        }
                    }
                    if(filterOnly){Console.WriteLine("FONT-BASELINE-FILTER observed="+allLinear+" (diagnostic only; no product qualification)");return;}
                    // Compare the actual ItemsRenderer outlet against the
                    // previously accepted private Blueprint Text implementation.
                    // Both use the exact captured glyph origin and native font.
                    foreach(float scale in new[]{.55f,.65f,.7f,.85f})
                    {
                        const string text="跟随中 攻击模式 114514";Stamp sample=null;
                        stamps.Clear();recording=true;
                        var actual=graphics.Pixels(()=>{Background();Call(renderer,"Pass",matrix,Rect(assembly,0,0,700,140),(Action)(()=>Call(renderer,"Text",text,Rect(assembly,24.35f,30.2f,600,60),Color.White,scale)));},matrix,960,220);
                        recording=false;sample=stamps.Single(s=>s.Text==text);
                        var reference=graphics.Pixels(()=>{
                            Background();
                            Main.spriteBatch.End();Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.LinearClamp,DepthStencilState.None,RasterizerState.CullNone,null,matrix);
                            Call(oracle,"Text",Main.spriteBatch,text,Rect(assembly,sample.Origin.X,sample.Origin.Y,0,0),Size(assembly),scale,Color.White,false);
                        },matrix,960,220);
                        int different=Difference(actual,reference);allOracle&=different==0;
                        string tag=factor.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+"-glyph-"+scale.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture);
                        Save(graphics,Path.Combine(output,"font-product-"+tag+".png"),actual,960,220);Save(graphics,Path.Combine(output,"font-blueprint-"+tag+".png"),reference,960,220);
                        Console.WriteLine("FONT-ORACLE "+tag+" same native font/origin/matrix different="+different);
                    }
                }
                NativeCompanionPasswordChecks.WithChallenge(context,_=>CheckEditor(window,graphics,output));
                CheckImagesAndFailure(window,renderer,assembly,graphics);
                Console.WriteLine("FONT-SUMMARY queuedLinear="+allLinear+" existingBlueprintPixels="+allOracle);
                Require(allLinear,"all normal companion text remains in its explicit LinearClamp pass after nested native portraits and image passes");
                Require(allOracle,"small companion glyphs use the existing scale-aware Blueprint outline at identical native font/origin/matrix");
            }
            finally {recording=false;harmony.UnpatchAll(harmony.Id);Call(window,"Close");((IDisposable)oracle).Dispose();NativeToolsUiChecks.Mouse(Vector2.Zero,false);}
        }
        private static void CheckEditor(object window,ProbeGraphics graphics,string output)
        {
            Matrix matrix=Matrix.CreateScale(1.2f);
            Call(window,"SyncPasswordPrompt");
            Call(Get(window,"editor"),"Insert","font-check-114514");
            Call(window,"Prepare",matrix,new Vector2(1280,800));
            var element=Get(window,"editElement");var text=(string)Get(element,"Text");var view=Get(window,"editView");
            var measured=Call(window,"MeasureCached",text,.7f);
            Require((float)Get(Get(element,"TextSize"),"Width")== (float)Get(measured,"Width")&&
                Math.Abs((float)Get(view,"Caret")-(float)Get(measured,"Width"))<.01f,"password glyphs, selection and caret share the scaled-outline measure");
            stamps.Clear();recording=true;
            var pixels=graphics.Pixels(()=>Call(window,"Draw"),matrix,1280,800);recording=false;
            var input=stamps.Single(s=>s.Text==text);
            Require(text.All(c=>c=='•')&&Linear(input.Sampler),"actual masked password outlet draws in the explicit linear text pass");
            Save(graphics,Path.Combine(output,"font-password-1.20.png"),pixels,1280,800);
            Call(window,"CancelPasswordPrompt");
            Console.WriteLine("FONT-EDITOR masked native glyphs / shared measured caret / LinearClamp PASS");
        }
        private static void CheckImagesAndFailure(object window,object renderer,Assembly assembly,ProbeGraphics graphics)
        {
            Matrix matrix=Matrix.CreateScale(1.2f);var rect=Rect(assembly,20,20,40,40);var texture=TextureAssets.Item[8].Value;
            var actual=graphics.Pixels(()=>Call(renderer,"Pass",matrix,Rect(assembly,0,0,200,150),(Action)(()=>Call(window,"PointDraw",(Action)(()=>Call(renderer,"PreparedItem",8,texture,rect))))),matrix,240,180);
            var reference=graphics.Pixels(()=>{
                Main.spriteBatch.End();Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone,null,matrix);
                float fit=Math.Min(1,Math.Min(36f/texture.Width,36f/texture.Height));
                Main.spriteBatch.Draw(texture,new Vector2(40,40),texture.Bounds,Color.White,0,new Vector2(texture.Width/2f,texture.Height/2f),fit,SpriteEffects.None,0);
            },matrix,240,180);
            Require(Difference(actual,reference)==0,"native item pixels keep the same point sampling and icon fit");
            var device=graphics.GraphicsDevice;bool caught=false,restored=false;
            stamps.Clear();recording=true;
            graphics.Pixels(()=>{var outerClip=device.ScissorRectangle;Call(renderer,"Pass",matrix,Rect(assembly,0,0,200,150),(Action)(()=>{
                var activeClip=device.ScissorRectangle;
                try {Call(window,"PointDraw",(Action)(()=>{throw new InvalidOperationException("font-point-sentinel");}));}
                catch(TargetInvocationException e) when(e.InnerException is InvalidOperationException&&e.InnerException.Message=="font-point-sentinel") {caught=true;}
                Require(device.ScissorRectangle==activeClip,"nested point failure preserves its owner's clip");
                Call(renderer,"Text","after image failure",Rect(assembly,8,8,180,40),Color.White,.7f);
                Call(window,"PointDraw",(Action)(()=>Call(window,"DrawStack","1234",rect)));
            }));restored=device.ScissorRectangle==outerClip;},matrix,240,180);recording=false;
            Require(caught&&restored&&stamps.Any(s=>s.Text=="after image failure"&&Linear(s.Sampler))&&
                stamps.Any(s=>s.Text=="1234"&&ReferenceEquals(s.Font,FontAssets.ItemStack.Value)&&Point(s.Sampler)),"exception resumes the declared text pass; native stack glyphs retain their own point pass");
            Console.WriteLine("FONT-IMAGES point icon exact / stack native font / exceptional nested continuation PASS");
        }
    }
}
