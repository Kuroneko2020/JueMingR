using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent.UI.ResourceSets;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionResourceBarChecks
    {
        private sealed class NativeBar
        {
            internal readonly ResourceDrawSettings.TextureGetter Panel,Fill;
            internal readonly int Count,FillStep;
            internal readonly Vector4 Bounds;
            internal NativeBar(HorizontalBarsPlayerResourcesDisplaySet original,Player player,bool mana)
            {
                Call(original,"PrepareFields",player);
                Count=(int)Get(original,mana?"_mpSegmentsCount":"_hpSegmentsCount");
                FillStep=((Asset<Texture2D>)Get(original,mana?"_mpFill":"_hpFill")).Value.Width;
                Panel=(ResourceDrawSettings.TextureGetter)Delegate.CreateDelegate(typeof(ResourceDrawSettings.TextureGetter),original,AccessTools.Method(original.GetType(),mana?"ManaPanelDrawer":"LifePanelDrawer"));
                Fill=(ResourceDrawSettings.TextureGetter)Delegate.CreateDelegate(typeof(ResourceDrawSettings.TextureGetter),original,AccessTools.Method(original.GetType(),mana?"ManaFillingDrawer":"LifeFillingDrawer"));
                var bounds=new Vector4(Single.MaxValue,Single.MaxValue,Single.MinValue,Single.MinValue);
                Trace(Panel,Count+2,Vector2.Zero,0,true,ref bounds);Trace(Fill,Count,new Vector2(6,6),FillStep,false,ref bounds);Bounds=bounds;
            }
            private static void Trace(ResourceDrawSettings.TextureGetter get,int count,Vector2 anchor,int step,bool panels,ref Vector4 bounds)
            {
                for(int i=0;i<count;i++) {
                    Asset<Texture2D> asset;Vector2 offset;float scale;Rectangle? source;get(i,0,count-1,out asset,out offset,out scale,out source);
                    var rect=source??asset.Value.Bounds;var start=anchor+offset;var end=start+new Vector2(rect.Width,rect.Height)*scale;
                    bounds.X=Math.Min(bounds.X,start.X);bounds.Y=Math.Min(bounds.Y,start.Y);bounds.Z=Math.Max(bounds.Z,end.X);bounds.W=Math.Max(bounds.W,end.Y);
                    anchor+=new Vector2(panels?rect.Width:step,0);
                }
            }
            internal void Draw(Vector2 topLeft,float width)
            {
                float fit=Math.Min(1,width/(Bounds.Z-Bounds.X));Vector2 anchor=topLeft-new Vector2(Bounds.X,Bounds.Y)*fit;
                bool hovered=false;
                Settings(Panel,Count+2,anchor,fit,false).Draw(Main.spriteBatch,ref hovered);
                Settings(Fill,Count,anchor+new Vector2(6,6)*fit,fit,true).Draw(Main.spriteBatch,ref hovered);
            }
            private ResourceDrawSettings Settings(ResourceDrawSettings.TextureGetter original,int count,Vector2 anchor,float fit,bool filling)
            {
                // Only adapt the overall anchor/scale. The real native private
                // delegates supply every ornament offset, texture and crop, and
                // the original public ResourceDrawSettings performs submission.
                ResourceDrawSettings.TextureGetter adapted=delegate(int index,int first,int last,out Asset<Texture2D> asset,out Vector2 offset,out float scale,out Rectangle? source) {
                    original(index,first,last,out asset,out offset,out scale,out source);offset*=fit;scale*=fit;
                };
                return new ResourceDrawSettings{ElementCount=count,TopLeftAnchor=anchor,GetTextureMethod=adapted,OffsetPerDraw=filling?new Vector2(FillStep*fit,0):Vector2.Zero,OffsetPerDrawByTexturePercentile=filling?Vector2.Zero:Vector2.UnitX*fit};
            }
        }
        internal static void Run(object context,ProbeGraphics graphics,string output)
        {
            graphics.LoadCompanionBarAssets();var previous=Main.ResourceSetsManager;
            var original=new HorizontalBarsPlayerResourcesDisplaySet("HorizontalBarsWithFullText","HorizontalBarsWithFullText","HorizontalBars",AssetRequestMode.ImmediateLoad);
            Main.ResourceSetsManager=new PlayerResourceSetsManager();Call(Main.ResourceSetsManager,"SetActiveFrame",original);
            var display=Activator.CreateInstance(Get(context,"Companions").GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.CompanionResourceDisplay",true),true);
            var players=Main.player.ToArray();var mouse=Main.mouseItem;var random=Main.rand;
            try
            {
                foreach(string name in new[]{"HP_Fill","HP_Fill_Honey","MP_Fill","Panel_Left","HP_Panel_Middle","HP_Panel_Right","MP_Panel_Middle","MP_Panel_Right"}) {
                    var texture=Main.Assets.Request<Texture2D>("Images/UI/PlayerResourceSets/HorizontalBars/"+name,AssetRequestMode.ImmediateLoad).Value;
                    Console.WriteLine("NATIVE-BAR-TEXTURE "+name+" "+texture.Width+"x"+texture.Height);
                    using(var file=File.Create(Path.Combine(output,"native-texture-"+name+".png")))texture.SaveAsPng(file,texture.Width,texture.Height);
                }
                Call(display,"Prepare");
                var samples=new[]{new Player{statLife=100,statLifeMax=100,statLifeMax2=100,statMana=20,statManaMax=20,statManaMax2=20},new Player{statLife=463,statLifeMax=500,statLifeMax2=600,statMana=371,statManaMax=200,statManaMax2=400},new Player{statLife=463,statLifeMax=450,statLifeMax2=600,statMana=371,statManaMax=200,statManaMax2=400}};
                for(int sample=0;sample<samples.Length;sample++)foreach(bool mana in new[]{false,true}) {
                    var player=samples[sample];var stats=new PlayerStatsSnapshot(player);var reference=new NativeBar(original,player,mana);
                    if(sample==2&&!mana)Require(stats.LifeFruitCount>0&&stats.LifeFruitCount<reference.Count,"mixed native snapshot must distinguish final gold cells from ordinary HP cells");
                    float width=(reference.Bounds.Z-reference.Bounds.X)*(sample==2?.625f:1f);var matrix=Matrix.CreateScale(sample==2?1.25f:1f);var origin=new Vector2(40.375f,44.125f);
                    Action product=()=>Call(display,"Bar",mana?stats.Mana:stats.Life,mana?stats.ManaMax:stats.LifeMax,mana?stats.ManaPerSegment:stats.LifePerSegment,mana?0:stats.LifeFruitCount,origin.X,origin.Y,width,mana);
                    Action native=()=>reference.Draw(origin,width);
                    var actual=graphics.Pixels(product,matrix,640,360);var expected=graphics.Pixels(native,matrix,640,360);
                    int difference=actual.Zip(expected,(a,b)=>a==b?0:1).Sum();
                    string tag=(sample==0?"ai-short":sample==1?"owner-partial":"owner-mixed-fruit-fit125")+(mana?"-mana":"-life");
                    graphics.Image(Path.Combine(output,tag+"-product.png"),product,matrix,640,360);graphics.Image(Path.Combine(output,tag+"-native.png"),native,matrix,640,360);
                    Require(difference==0,"product bar exactly matches real original panel/filling delegates and ResourceDrawSettings pixels: "+tag+" different="+difference);
                    float height=(float)Call(display,"BarHeight",mana?stats.ManaMax:stats.LifeMax,mana?stats.ManaPerSegment:stats.LifePerSegment,width,mana);
                    float fit=Math.Min(1,width/(reference.Bounds.Z-reference.Bounds.X));
                    Require(Math.Abs(height-((reference.Bounds.W-reference.Bounds.Y)*fit+6))<.001f,"bar layout reserves full native ornament negative-y extent and bottom gap: "+tag);
                    var blocks=Call(display,"MeasureBlocks",player,width);
                    Require(Math.Abs((float)Get(blocks,mana?"Mana":"Life")-(height+20))<.001f,"actual public block measurement includes the value label and complete ornament: "+tag);
                    for(int y=0;y<360;y++)for(int x=0;x<640;x++)if(actual[y*640+x].A>0)Require(y>=origin.Y*matrix.M11-1&&y<=(origin.Y+height-6)*matrix.M11+1,"complete ornament stays after label row and within measured bar extent");
                    Console.WriteLine("NATIVE-BAR-ORACLE "+tag+" different="+difference+" count="+reference.Count+" native-bounds="+reference.Bounds+" outer-scale="+fit+" matrix="+matrix.M11+" measured-height="+height);
                }
                Require(Main.player.SequenceEqual(players)&&ReferenceEquals(Main.mouseItem,mouse)&&ReferenceEquals(Main.rand,random),"resource comparison never borrows human player slots, cursor or random stream");
            }
            finally {Main.ResourceSetsManager=previous;}
            Console.WriteLine("PASS original native horizontal bar panel/endcaps, filling anchor/right-preserving crop/gold last cells, full extent and float uniform scale at short/partial/fractional cases; no creation/Journey/pet rerun.");
        }
    }
}
