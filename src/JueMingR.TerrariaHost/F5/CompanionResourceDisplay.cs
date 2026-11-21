using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.ResourceSets;
using JueMingR.TerrariaHost.Items;

namespace JueMingR.TerrariaHost.F5
{
    // Selection belongs to the vanilla HUD. Only positions are adapted here;
    // effective values always come from the specified authoritative player.
    internal sealed class CompanionResourceDisplay
    {
        private readonly Dictionary<string, Asset<Texture2D>> assets = new Dictionary<string, Asset<Texture2D>>();
        private sealed class Numbers
        {
            internal int Life=Int32.MinValue,LifeMax,Mana=Int32.MinValue,ManaMax;
            internal string LifeText,ManaText;
        }
        private readonly Dictionary<Player,Numbers> numbers=new Dictionary<Player,Numbers>();
        internal void ClearNumbers() {numbers.Clear();}
        private Numbers TextValues(Player player,PlayerStatsSnapshot stats)
        {
            Numbers value;if(!numbers.TryGetValue(player,out value)) {if(numbers.Count>=2)numbers.Clear();numbers[player]=value=new Numbers();}
            if(value.Life!=stats.Life||value.LifeMax!=stats.LifeMax) {value.Life=stats.Life;value.LifeMax=stats.LifeMax;value.LifeText="生命 "+stats.Life+" / "+stats.LifeMax;}
            if(value.Mana!=stats.Mana||value.ManaMax!=stats.ManaMax) {value.Mana=stats.Mana;value.ManaMax=stats.ManaMax;value.ManaText="魔力 "+stats.Mana+" / "+stats.ManaMax;}
            return value;
        }
        private string key,preparedFolder;
        private IAssetRepository repository;
        private HeartGeometry? heartsA,heartsB;
        private static readonly string[] BarTextures={"HP_Fill","HP_Fill_Honey","MP_Fill","Panel_Left","HP_Panel_Middle","HP_Panel_Right","MP_Panel_Middle","MP_Panel_Right"};
        private static readonly string[] FancyTextures={"Heart_Left","Heart_Middle","Heart_Right","Heart_Right_Fancy","Heart_Single_Fancy","Heart_Fill","Heart_Fill_B","Star_Single","Star_Fill"};
        internal string StyleKey { get { return key; } }
        internal void Prepare()
        {
            key=Main.ResourceSetsManager?.ActiveSetKeyName??"Default";
            if(key=="Default") return;
            string folder=key.StartsWith("HorizontalBars",StringComparison.Ordinal)?"HorizontalBars":"FancyClassic";
            if(!ReferenceEquals(repository,Main.Assets)) {repository=Main.Assets;assets.Clear();preparedFolder=null;heartsA=heartsB=null;}
            if(preparedFolder==folder) return; preparedFolder=folder;
            string[] names=folder=="HorizontalBars"?BarTextures:FancyTextures;
            foreach(string name in names) { string path="Images/UI/PlayerResourceSets/"+folder+"/"+name; if(!assets.ContainsKey(path)) assets.Add(path,Main.Assets.Request<Texture2D>(path,AssetRequestMode.ImmediateLoad)); }
        }
        private Texture2D Texture(string folder,string name) { return assets["Images/UI/PlayerResourceSets/"+folder+"/"+name].Value; }
        internal struct Blocks
        {internal float Life,Mana;}
        internal Blocks MeasureBlocks(Player player,float width)
        {
            var s=new PlayerStatsSnapshot(player);string k=Main.ResourceSetsManager?.ActiveSetKeyName??"Default";
            bool bars=k.StartsWith("HorizontalBars",StringComparison.Ordinal),fancy=k=="New"||k=="NewWithText";
            bool lifeText=k=="Default"||k=="NewWithText"||k=="HorizontalBarsWithText"||k=="HorizontalBarsWithFullText";
            bool manaText=k=="Default"||k=="NewWithText"||k=="HorizontalBarsWithFullText";
            if(bars||fancy)Prepare();
            float health=bars?BarHeight(s.LifeMax,s.LifePerSegment,width,false):fancy?FancyLifeHeight(s.LifeMax,s.LifePerSegment,s.LifeFruitCount,width):(float)Math.Ceiling(Math.Ceiling(s.LifeMax/Math.Max(1,s.LifePerSegment))/10)*RowHeight(width,false)+5;
            float mana=bars?BarHeight(s.ManaMax,s.ManaPerSegment,width,true):fancy?FancyManaHeight(s.ManaMax,s.ManaPerSegment,width):(float)Math.Ceiling(Math.Ceiling(s.ManaMax/Math.Max(1,s.ManaPerSegment))/10)*RowHeight(width,false);
            return new Blocks{Life=health+(lifeText?20:0),Mana=mana+(manaText?20:0)};
        }
        internal float MeasureHeight(Player player,float width) {var blocks=MeasureBlocks(player,width);return blocks.Life+blocks.Mana;}
        internal void Draw(ItemsRenderer renderer,Player player,F5Rect rect,float sharedLife=-1)
        {
            var stats=new PlayerStatsSnapshot(player); var text=TextValues(player,stats);
            bool bars=key!=null&&key.StartsWith("HorizontalBars",StringComparison.Ordinal), fancy=key=="New"||key=="NewWithText";
            bool lifeText=key=="Default"||key=="NewWithText"||key=="HorizontalBarsWithText"||key=="HorizontalBarsWithFullText";
            bool manaText=key=="Default"||key=="NewWithText"||key=="HorizontalBarsWithFullText";
            float y=rect.Y;
            if(lifeText) { renderer.Text(text.LifeText,new F5Rect(rect.X,y,rect.Width,20),Color.White,.6f); y+=20; }
            if(bars) { Bar(stats.Life,stats.LifeMax,stats.LifePerSegment,stats.LifeFruitCount,rect.X,y,rect.Width,false); y+=BarHeight(stats.LifeMax,stats.LifePerSegment,rect.Width,false); }
            else y+=Icons(stats.Life,stats.LifeMax,stats.LifePerSegment,stats.LifeFruitCount,rect.X,y,rect.Width,false,fancy)+(fancy?0:5);
            if(sharedLife>=0)y=rect.Y+sharedLife;
            if(manaText) { renderer.Text(text.ManaText,new F5Rect(rect.X,y,rect.Width,20),Color.White,.6f); y+=20; }
            if(bars) Bar(stats.Mana,stats.ManaMax,stats.ManaPerSegment,0,rect.X,y,rect.Width,true);
            else Icons(stats.Mana,stats.ManaMax,stats.ManaPerSegment,0,rect.X,y,rect.Width,true,fancy);
        }
        private static float RowHeight(float width,bool fancy) {return fancy?34:29;}
        private float Icons(int current,int max,float per,int fruit,float x,float y,float width,bool mana,bool fancy)
        {
            if(fancy)return mana?FancyMana(current,max,per,x,y,width):FancyLife(current,max,per,fruit,x,y,width);
            int count=Math.Max(0,(int)Math.Ceiling(max/Math.Max(1,per)));
            if(count==0)return 0;
            const int columns=10;
            float cell=Math.Min(fancy?26:27,width/columns),row=RowHeight(width,fancy);
            for(int i=0;i<count;i++)
            {
                float fill=MathHelper.Clamp((current-i*per)/Math.Max(1,per),0,1);
                float px=x+i%columns*cell,py=y+i/columns*row;
                {
                    var texture=mana?TextureAssets.Mana.Value:i<fruit?TextureAssets.Heart2.Value:TextureAssets.Heart.Value;
                    int brightness=(int)(30+225*fill);float pulse=.75f+fill*.25f;
                    if(fill>0&&fill<1||current==(i+1)*per)pulse+=Main.cursorScale-1;
                    float scale=Math.Min(1.1f,(cell-1)/texture.Width)*pulse;
                    Main.spriteBatch.Draw(texture,new Vector2(px+cell/2,py+row/2),null,new Color(brightness,brightness,brightness,(int)(brightness*.9f)),0,new Vector2(texture.Width/2f,texture.Height/2f),scale,SpriteEffects.None,0);
                }
            }
            return (float)Math.Ceiling((double)count/columns)*row;
        }
        private struct HeartGeometry
        {internal int First,Second,Maximum,Fruit;internal float Left,Top,Height,Scale,Per,Available;}
        private Texture2D HeartPanel(int index,int first,int last,int total,out Vector2 offset)
        {
            offset=Vector2.Zero;string name="Heart_Left";
            if(index==first&&index==last){name="Heart_Single_Fancy";offset=new Vector2(-4,-4);}
            else if(index==last&&index==total-1){name="Heart_Right_Fancy";offset=new Vector2(-8,-4);}
            else if(index==last)name="Heart_Right";else if(index!=first)name="Heart_Middle";
            return Texture("FancyClassic",name);
        }
        private HeartGeometry Hearts(int maximum,float per,int fruit,float width)
        {
            if(heartsA.HasValue&&HeartKey(heartsA.Value,maximum,per,fruit,width))return heartsA.Value;
            if(heartsB.HasValue&&HeartKey(heartsB.Value,maximum,per,fruit,width))return heartsB.Value;
            int first=Utils.Clamp((int)(maximum/Math.Max(1,per)),0,10),second=Utils.Clamp((int)((maximum-200)/Math.Max(1,per)),0,10),total=first+second;
            if(total==0)return new HeartGeometry{Scale=1};
            float left=Single.MaxValue,top=Single.MaxValue,right=Single.MinValue,bottom=Single.MinValue;
            for(int row=0;row<2;row++) {
                int count=row==0?first:second,start=row==0?0:10;float advance=0,fillAdvance=15;
                for(int i=0;i<count;i++) {
                    Vector2 offset;var panel=HeartPanel(start+i,start,start+count-1,total,out offset);
                    left=Math.Min(left,advance+offset.X);top=Math.Min(top,row*28+offset.Y);right=Math.Max(right,advance+offset.X+panel.Width);bottom=Math.Max(bottom,row*28+offset.Y+panel.Height);advance+=panel.Width;
                    var fill=Texture("FancyClassic",start+i<fruit?"Heart_Fill_B":"Heart_Fill");
                    left=Math.Min(left,fillAdvance-fill.Width/2f);top=Math.Min(top,row*28+15-fill.Height/2f);right=Math.Max(right,fillAdvance+fill.Width/2f);bottom=Math.Max(bottom,row*28+15+fill.Height/2f);fillAdvance+=fill.Width+2;
                }
            }
            var geometry=new HeartGeometry{First=first,Second=second,Maximum=maximum,Fruit=fruit,Per=per,Available=width,Left=left,Top=top,Height=bottom-top,Scale=Math.Min(1,width/Math.Max(1,right-left))};
            heartsB=heartsA;heartsA=geometry;return geometry;
        }
        private static bool HeartKey(HeartGeometry g,int maximum,float per,int fruit,float width)
        {return g.Maximum==maximum&&g.Per==per&&g.Fruit==fruit&&g.Available==width;}
        private float FancyLifeHeight(int maximum,float per,int fruit,float width)
        {var g=Hearts(maximum,per,fruit,width);return g.Height*g.Scale+6;}
        private float FancyLife(int current,int maximum,float per,int fruit,float x,float y,float width)
        {
            var g=Hearts(maximum,per,fruit,width);int total=g.First+g.Second;var anchor=new Vector2(x-g.Left*g.Scale,y-g.Top*g.Scale);
            // Native order is all panels, then all fillings. Both sets share
            // the same row anchor; fill centers are exactly panel anchor+(15,15).
            // An end ornament's overlap must not paint over an earlier filling.
            for(int row=0;row<2;row++) {
                int count=row==0?g.First:g.Second,start=row==0?0:10;float advance=0;
                for(int i=0;i<count;i++){Vector2 offset;var panel=HeartPanel(start+i,start,start+count-1,total,out offset);DrawBarPart(panel,anchor,new Vector2(advance,row*28)+offset,null,g.Scale);advance+=panel.Width;}
            }
            for(int row=0;row<2;row++) {
                int count=row==0?g.First:g.Second,start=row==0?0:10;float advance=15;
                for(int i=0;i<count;i++) {
                    int index=start+i;var fill=Texture("FancyClassic",index<fruit?"Heart_Fill_B":"Heart_Fill");
                    float amount=Utils.GetLerpValue(per*index,per*(index+1),current,true);if(index==(int)(current/Math.Max(1,per))&&amount>0)amount+=Main.cursorScale-1;
                    Main.spriteBatch.Draw(fill,anchor+new Vector2(advance,row*28+15)*g.Scale,null,Color.White,0,new Vector2(fill.Width/2f,fill.Height/2f),g.Scale*amount,SpriteEffects.None,0);advance+=fill.Width+2;
                }
            }
            return g.Height*g.Scale+6;
        }
        private float FancyManaHeight(int maximum,float per,float width)
        {
            int count=Math.Max(0,(int)(maximum/Math.Max(1,per)));if(count==0)return 0;
            var panel=Texture("FancyClassic","Star_Single");int columns=Math.Min(10,count);float scale=Math.Min(1,width/Math.Max(1,columns*(panel.Width+2)-2));
            return ((float)Math.Ceiling(count/10f)*(panel.Height+2)-2)*scale+6;
        }
        private float FancyMana(int current,int maximum,float per,float x,float y,float width)
        {
            int count=Math.Max(0,(int)(maximum/Math.Max(1,per)));if(count==0)return 0;
            var panel=Texture("FancyClassic","Star_Single");var fill=Texture("FancyClassic","Star_Fill");
            float scale=Math.Min(1,width/Math.Max(1,Math.Min(10,count)*(panel.Width+2)-2));
            for(int i=0;i<count;i++)DrawBarPart(panel,new Vector2(x,y),new Vector2(i%10*(panel.Width+2),i/10*(panel.Height+2)),null,scale);
            for(int i=0;i<count;i++) {
                float amount=Utils.GetLerpValue(per*i,per*(i+1),current,true);if(i==(int)(current/Math.Max(1,per))&&amount>0)amount+=Main.cursorScale-1;
                Main.spriteBatch.Draw(fill,new Vector2(x,y)+new Vector2(i%10*(panel.Width+2)+15,i/10*(panel.Height+2)+16)*scale,null,Color.White,0,new Vector2(fill.Width/2f,fill.Height/2f),scale*amount,SpriteEffects.None,0);
            }
            return FancyManaHeight(maximum,per,width);
        }
        private struct BarGeometry
        {internal int Count;internal float Left,Top,Height,Scale;}
        private BarGeometry Geometry(int maximum,float per,float width,bool mana)
        {
            int count=Math.Max(0,(int)(maximum/Math.Max(1,per)));
            var fill=Texture("HorizontalBars",mana?"MP_Fill":"HP_Fill");
            var middle=Texture("HorizontalBars",mana?"MP_Panel_Middle":"HP_Panel_Middle");
            var left=Texture("HorizontalBars","Panel_Left"); var right=Texture("HorizontalBars",mana?"MP_Panel_Right":"HP_Panel_Right");
            float capX=left.Width+count*middle.Width-16,capY=mana?-6:-10;
            float minX=Math.Min(0,capX),minY=Math.Min(0,capY);
            float maxX=Math.Max(left.Width+count*middle.Width,capX+right.Width);
            float maxY=Math.Max(Math.Max(left.Height,middle.Height),capY+right.Height);
            if(count>0){maxX=Math.Max(maxX,6+count*fill.Width);maxY=Math.Max(maxY,6+fill.Height);}
            return new BarGeometry{Count=count,Left=minX,Top=minY,Height=maxY-minY,Scale=Math.Min(1,width/Math.Max(1,maxX-minX))};
        }
        private float BarHeight(int maximum,float per,float width,bool mana)
        {var g=Geometry(maximum,per,width,mana);return g.Height*g.Scale+6;}
        private void Bar(int current,int maximum,float per,int fruit,float x,float y,float width,bool mana)
        {
            var g=Geometry(maximum,per,width,mana);var anchor=new Vector2(x-g.Left*g.Scale,y-g.Top*g.Scale);
            var left=Texture("HorizontalBars","Panel_Left");var middle=Texture("HorizontalBars",mana?"MP_Panel_Middle":"HP_Panel_Middle");var right=Texture("HorizontalBars",mana?"MP_Panel_Right":"HP_Panel_Right");
            // Native panels are one strip: advance by panel texture widths and
            // overlap/lift the end ornament. Reserve its full negative-y extent
            // above in Geometry, so it cannot cover the preceding value label.
            DrawBarPart(left,anchor,Vector2.Zero,null,g.Scale);
            for(int i=0;i<g.Count;i++)DrawBarPart(middle,anchor,new Vector2(left.Width+i*middle.Width,0),null,g.Scale);
            DrawBarPart(right,anchor,new Vector2(left.Width+g.Count*middle.Width-16,mana?-6:-10),null,g.Scale);
            // Match native FillBarByValues: drain from the left, retain filling
            // next to the ornament, and assign fruit-gold to the final HP cells.
            float missing=maximum>0?1-current/(float)maximum:1;
            int fillStep=Texture("HorizontalBars",mana?"MP_Fill":"HP_Fill").Width;
            for(int i=0;i<g.Count;i++) {
                var fill=Texture("HorizontalBars",mana?"MP_Fill":i>=g.Count-fruit?"HP_Fill_Honey":"HP_Fill");
                float segment=1f/g.Count,amount=1-Utils.GetLerpValue(segment*i,segment*(i+1),missing,true);
                int crop=(int)(fill.Width*(1-amount));if(crop>=fill.Width)continue;
                DrawBarPart(fill,anchor,new Vector2(6+i*fillStep+crop,6),new Rectangle(crop,0,fill.Width-crop,fill.Height),g.Scale);
            }
        }
        private static void DrawBarPart(Texture2D texture,Vector2 anchor,Vector2 offset,Rectangle? source,float scale)
        {Main.spriteBatch.Draw(texture,anchor+offset*scale,source,Color.White,0,Vector2.Zero,scale,SpriteEffects.None,0);}
    }
}
