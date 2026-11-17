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
        private static readonly string[] BarTextures={"HP_Fill","HP_Fill_Honey","MP_Fill","Panel_Left","HP_Panel_Middle","HP_Panel_Right","MP_Panel_Middle","MP_Panel_Right"};
        private static readonly string[] FancyTextures={"Heart_Left","Heart_Middle","Heart_Right","Heart_Right_Fancy","Heart_Single_Fancy","Heart_Fill","Heart_Fill_B","Star_Single","Star_Fill"};
        internal string StyleKey { get { return key; } }
        internal void Prepare()
        {
            key=Main.ResourceSetsManager?.ActiveSetKeyName??"Default";
            if(key=="Default") return;
            string folder=key.StartsWith("HorizontalBars",StringComparison.Ordinal)?"HorizontalBars":"FancyClassic";
            if(!ReferenceEquals(repository,Main.Assets)) {repository=Main.Assets;assets.Clear();preparedFolder=null;}
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
            if(bars)Prepare();
            float health=bars?BarHeight(s.LifeMax,s.LifePerSegment,width,false):(float)Math.Ceiling(Math.Ceiling(s.LifeMax/Math.Max(1,s.LifePerSegment))/10)*RowHeight(width,fancy)+5;
            float mana=bars?BarHeight(s.ManaMax,s.ManaPerSegment,width,true):(float)Math.Ceiling(Math.Ceiling(s.ManaMax/Math.Max(1,s.ManaPerSegment))/10)*RowHeight(width,fancy);
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
            else y+=Icons(stats.Life,stats.LifeMax,stats.LifePerSegment,stats.LifeFruitCount,rect.X,y,rect.Width,false,fancy)+5;
            if(sharedLife>=0)y=rect.Y+sharedLife;
            if(manaText) { renderer.Text(text.ManaText,new F5Rect(rect.X,y,rect.Width,20),Color.White,.6f); y+=20; }
            if(bars) Bar(stats.Mana,stats.ManaMax,stats.ManaPerSegment,0,rect.X,y,rect.Width,true);
            else Icons(stats.Mana,stats.ManaMax,stats.ManaPerSegment,0,rect.X,y,rect.Width,true,fancy);
        }
        private static float RowHeight(float width,bool fancy) {return fancy?34:29;}
        private float Icons(int current,int max,float per,int fruit,float x,float y,float width,bool mana,bool fancy)
        {
            int count=Math.Max(0,(int)Math.Ceiling(max/Math.Max(1,per)));
            if(count==0)return 0;
            const int columns=10;
            float cell=Math.Min(fancy?26:27,width/columns),row=RowHeight(width,fancy);
            if(fancy&&!mana)
            {
                // Vanilla panels form one connected row. Its final ornament
                // overlaps eight pixels; fills share the original 15px anchors.
                float unit=Math.Min(1.1f,(width-10)/Math.Max(1,Texture("FancyClassic","Heart_Left").Width*10f));
                for(int start=0;start<count;start+=columns)
                {
                    int n=Math.Min(columns,count-start);float panelX=x+4*unit,fillX=x+15*unit;
                    for(int j=0;j<n;j++)
                    {
                        int i=start+j;bool last=j==n-1;
                        string name=n==1?"Heart_Single_Fancy":last?(i==count-1?"Heart_Right_Fancy":"Heart_Right"):j==0?"Heart_Left":"Heart_Middle";
                        var panel=Texture("FancyClassic",name);float dx=n==1?-4:last&&i==count-1?-8:0,dy=n==1||last&&i==count-1?-4:0;
                        Main.spriteBatch.Draw(panel,new Vector2(panelX+dx*unit,y+start/columns*row+dy*unit+4),null,Color.White,0,Vector2.Zero,unit,SpriteEffects.None,0);
                        var fill=Texture("FancyClassic",i<fruit?"Heart_Fill_B":"Heart_Fill");
                        float amount=MathHelper.Clamp((current-i*per)/Math.Max(1,per),0,1);
                        if(amount>0)Main.spriteBatch.Draw(fill,new Vector2(fillX,y+start/columns*row+15*unit+4),null,Color.White,0,new Vector2(fill.Width/2f,fill.Height/2f),unit*amount,SpriteEffects.None,0);
                        panelX+=panel.Width*unit;fillX+=(fill.Width+2)*unit;
                    }
                }
            }
            else for(int i=0;i<count;i++)
            {
                float fill=MathHelper.Clamp((current-i*per)/Math.Max(1,per),0,1);
                float px=x+i%columns*cell,py=y+i/columns*row;
                if(fancy)
                {
                    var panel=Texture("FancyClassic","Star_Single");var foreground=Texture("FancyClassic","Star_Fill");
                    float scale=Math.Min(1.1f,cell/panel.Width);
                    Main.spriteBatch.Draw(panel,new Vector2(px,py),null,Color.White,0,Vector2.Zero,scale,SpriteEffects.None,0);
                    if(fill>0)Main.spriteBatch.Draw(foreground,new Vector2(px+panel.Width*scale/2,py+panel.Height*scale/2),null,Color.White,0,new Vector2(foreground.Width/2f,foreground.Height/2f),scale*fill,SpriteEffects.None,0);
                }
                else
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
