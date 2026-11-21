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
    internal static class NativeCompanionFancyResourceChecks
    {
        private sealed class Reference
        {
            private readonly ResourceDrawSettings.TextureGetter panel,fill;
            private readonly bool mana;
            internal readonly int First,Second,Count;
            internal readonly Vector4 Bounds;
            private readonly int starWidth,starHeight;
            internal Reference(FancyClassicPlayerResourcesDisplaySet original,Player player,bool stars)
            {
                mana=stars;Call(original,"PrepareFields",player);
                First=(int)Get(original,"_heartCountRow1");Second=(int)Get(original,"_heartCountRow2");Count=mana?(int)Get(original,"_starCount"):First+Second;
                panel=(ResourceDrawSettings.TextureGetter)Delegate.CreateDelegate(typeof(ResourceDrawSettings.TextureGetter),original,AccessTools.Method(original.GetType(),mana?"StarPanelDrawer":"HeartPanelDrawer"));
                fill=(ResourceDrawSettings.TextureGetter)Delegate.CreateDelegate(typeof(ResourceDrawSettings.TextureGetter),original,AccessTools.Method(original.GetType(),mana?"StarFillingDrawer":"HeartFillingDrawer"));
                var single=((Asset<Texture2D>)Get(original,"_starSingle")).Value;starWidth=single.Width;starHeight=single.Height;
                var bounds=new Vector4(Single.MaxValue,Single.MaxValue,Single.MinValue,Single.MinValue);
                if(mana){for(int i=0;i<Count;i++){var at=Cell(i);Trace(panel,1,i,at,false,ref bounds);Trace(fill,1,i,at+new Vector2(15,16),true,ref bounds);}}
                else for(int row=0;row<2;row++){int count=row==0?First:Second,start=row==0?0:10;Trace(panel,count,start,new Vector2(0,row*28),false,ref bounds);Trace(fill,count,start,new Vector2(15,row*28+15),true,ref bounds);}
                Bounds=bounds;
            }
            private Vector2 Cell(int i) {return new Vector2(i%10*(starWidth+2),i/10*(starHeight+2));}
            private static void Trace(ResourceDrawSettings.TextureGetter get,int count,int start,Vector2 anchor,bool filling,ref Vector4 b)
            {
                for(int i=0;i<count;i++) {
                    Asset<Texture2D> asset;Vector2 offset;float scale;Rectangle? source;get(start+i,start,start+count-1,out asset,out offset,out scale,out source);
                    var rect=source??asset.Value.Bounds;var point=anchor+offset-(filling?new Vector2(rect.Width,rect.Height)/2:Vector2.Zero);
                    // Layout reserves the full original filling frame, independent
                    // of current life/pulse, just as native panel geometry does.
                    b.X=Math.Min(b.X,point.X);b.Y=Math.Min(b.Y,point.Y);b.Z=Math.Max(b.Z,point.X+rect.Width);b.W=Math.Max(b.W,point.Y+rect.Height);
                    anchor.X+=rect.Width+(filling?2:0);
                }
            }
            internal void Draw(Vector2 origin,float width)
            {
                float scale=Math.Min(1,width/(Bounds.Z-Bounds.X));var anchor=origin-new Vector2(Bounds.X,Bounds.Y)*scale;bool hover=false;
                if(mana) {
                    for(int i=0;i<Count;i++)Settings(panel,1,i,anchor+Cell(i)*scale,scale,false).Draw(Main.spriteBatch,ref hover);
                    for(int i=0;i<Count;i++)Settings(fill,1,i,anchor+(Cell(i)+new Vector2(15,16))*scale,scale,true).Draw(Main.spriteBatch,ref hover);
                }
                else {
                    for(int row=0;row<2;row++)Settings(panel,row==0?First:Second,row==0?0:10,anchor+new Vector2(0,row*28)*scale,scale,false).Draw(Main.spriteBatch,ref hover);
                    for(int row=0;row<2;row++)Settings(fill,row==0?First:Second,row==0?0:10,anchor+new Vector2(15,row*28+15)*scale,scale,true).Draw(Main.spriteBatch,ref hover);
                }
            }
            private static ResourceDrawSettings Settings(ResourceDrawSettings.TextureGetter native,int count,int start,Vector2 anchor,float scale,bool filling)
            {
                ResourceDrawSettings.TextureGetter get=delegate(int i,int first,int last,out Asset<Texture2D> asset,out Vector2 offset,out float factor,out Rectangle? source){native(i,first,last,out asset,out offset,out factor,out source);offset*=scale;factor*=scale;};
                return new ResourceDrawSettings{ElementCount=count,ElementIndexOffset=start,TopLeftAnchor=anchor,GetTextureMethod=get,OffsetPerDraw=new Vector2(filling?2*scale:0,0),OffsetPerDrawByTexturePercentile=Vector2.UnitX*scale,OffsetSpriteAnchorByTexturePercentile=filling?new Vector2(.5f):Vector2.Zero};
            }
        }
        internal static void Run(object context,ProbeGraphics graphics,string output)
        {
            graphics.LoadCompanionFancyAssets();var previous=Main.ResourceSetsManager;float pulse=Main.cursorScale;
            var native=new FancyClassicPlayerResourcesDisplaySet("NewWithText","NewWithText","FancyClassic",AssetRequestMode.ImmediateLoad);
            Main.ResourceSetsManager=new PlayerResourceSetsManager();Call(Main.ResourceSetsManager,"SetActiveFrame",native);
            var display=Activator.CreateInstance(Get(context,"Companions").GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.CompanionResourceDisplay",true),true);
            try
            {
                Main.cursorScale=1.0625f;Call(display,"Prepare");
                foreach(string name in new[]{"Heart_Left","Heart_Middle","Heart_Right","Heart_Right_Fancy","Heart_Single_Fancy","Heart_Fill","Heart_Fill_B","Star_A","Star_B","Star_C","Star_Single","Star_Fill"}) {
                    var texture=Main.Assets.Request<Texture2D>("Images/UI/PlayerResourceSets/FancyClassic/"+name,AssetRequestMode.ImmediateLoad).Value;
                    Console.WriteLine("NATIVE-FANCY-TEXTURE "+name+" "+texture.Width+"x"+texture.Height);using(var file=File.Create(Path.Combine(output,"fancy-native-texture-"+name+".png")))texture.SaveAsPng(file,texture.Width,texture.Height);
                }
                var samples=new[]{new Player{statLife=73,statLifeMax=100,statLifeMax2=100,statMana=13,statManaMax=20,statManaMax2=20},new Player{statLife=463,statLifeMax=500,statLifeMax2=600,statMana=371,statManaMax=200,statManaMax2=400},new Player{statLife=463,statLifeMax=425,statLifeMax2=600,statMana=371,statManaMax=200,statManaMax2=400}};
                for(int i=0;i<samples.Length;i++)foreach(bool mana in new[]{false,true}) {
                    var player=samples[i];var stats=new PlayerStatsSnapshot(player);var reference=new Reference(native,player,mana);
                    float width=(reference.Bounds.Z-reference.Bounds.X)*(i==2?.625f:1f);var origin=new Vector2(40.375f,44.125f);var matrix=Matrix.CreateScale(i==2?1.25f:1);
                    Action product=()=>{if(mana)Call(display,"FancyMana",stats.Mana,stats.ManaMax,stats.ManaPerSegment,origin.X,origin.Y,width);else Call(display,"FancyLife",stats.Life,stats.LifeMax,stats.LifePerSegment,stats.LifeFruitCount,origin.X,origin.Y,width);};
                    Action original=()=>reference.Draw(origin,width);
                    var actual=graphics.Pixels(product,matrix,640,360);var expected=graphics.Pixels(original,matrix,640,360);int difference=actual.Zip(expected,(a,b)=>a==b?0:1).Sum();
                    string tag="fancy-"+(i==0?"short-partial":i==1?"gold-two-rows":"mixed-fit125")+(mana?"-horizontal-mana":"-life");
                    graphics.Image(Path.Combine(output,tag+"-product.png"),product,matrix,640,360);graphics.Image(Path.Combine(output,tag+"-native.png"),original,matrix,640,360);
                    Require(expected.Any(c=>c.A>0)&&difference==0,"Fancy actual native panel/filling delegates match product pixels including pulse/ornament and approved horizontal star placement: "+tag+" diff="+difference);
                    var blocks=Call(display,"MeasureBlocks",player,width);float scale=Math.Min(1,width/(reference.Bounds.Z-reference.Bounds.X));
                    Require(Math.Abs((float)Get(blocks,mana?"Mana":"Life")-((reference.Bounds.W-reference.Bounds.Y)*scale+26))<.001f,"Fancy measurement reserves full native panel/filling bounds, label and spacing");
                    Console.WriteLine("NATIVE-FANCY-ORACLE "+tag+" different="+difference+" count="+reference.Count+" first="+reference.First+" second="+reference.Second+" bounds="+reference.Bounds+" scale="+scale+" fruit="+stats.LifeFruitCount);
                }
            }
            finally {Main.ResourceSetsManager=previous;Main.cursorScale=pulse;}
            Console.WriteLine("PASS Fancy original panel/fill anchors, native texture advances/all-panels-first, last ornament/gold/pulse and horizontal mana adaptation; no creation/Journey/pet rerun.");
        }
    }
}
