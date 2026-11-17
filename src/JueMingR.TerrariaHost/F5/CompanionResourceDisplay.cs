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
        private static readonly string[] FancyTextures={"Heart_Single_Fancy","Heart_Fill","Heart_Fill_B","Star_Single","Star_Fill"};
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
        internal static float MeasureHeight(Player player,float width)
        {
            var s=new PlayerStatsSnapshot(player); string k=Main.ResourceSetsManager?.ActiveSetKeyName??"Default";
            bool bars=k.StartsWith("HorizontalBars",StringComparison.Ordinal),fancy=k=="New"||k=="NewWithText";
            bool lifeText=k=="Default"||k=="NewWithText"||k=="HorizontalBarsWithText"||k=="HorizontalBarsWithFullText";
            bool manaText=k=="Default"||k=="NewWithText"||k=="HorizontalBarsWithFullText";
            int columns=Math.Max(1,(int)(width/(fancy?25:23)));
            float health=bars?34:(float)Math.Ceiling(Math.Ceiling(s.LifeMax/Math.Max(1,s.LifePerSegment))/columns)*24+5;
            float mana=bars?34:(float)Math.Ceiling(Math.Ceiling(s.ManaMax/Math.Max(1,s.ManaPerSegment))/columns)*24;
            return health+mana+(lifeText?25:0)+(manaText?25:0);
        }
        internal void Draw(ItemsRenderer renderer,Player player,F5Rect rect)
        {
            var stats=new PlayerStatsSnapshot(player); var text=TextValues(player,stats);
            bool bars=key!=null&&key.StartsWith("HorizontalBars",StringComparison.Ordinal), fancy=key=="New"||key=="NewWithText";
            bool lifeText=key=="Default"||key=="NewWithText"||key=="HorizontalBarsWithText"||key=="HorizontalBarsWithFullText";
            bool manaText=key=="Default"||key=="NewWithText"||key=="HorizontalBarsWithFullText";
            float y=rect.Y;
            if(lifeText) { renderer.Text(text.LifeText,new F5Rect(rect.X,y,rect.Width,24),Color.White,.6f); y+=25; }
            if(bars) { Bar(stats.Life,stats.LifeMax,stats.LifePerSegment,stats.LifeFruitCount,rect.X,y,rect.Width,false); y+=34; }
            else y+=Icons(stats.Life,stats.LifeMax,stats.LifePerSegment,stats.LifeFruitCount,rect.X,y,rect.Width,false,fancy)+5;
            if(manaText) { renderer.Text(text.ManaText,new F5Rect(rect.X,y,rect.Width,24),Color.White,.6f); y+=25; }
            if(bars) Bar(stats.Mana,stats.ManaMax,stats.ManaPerSegment,0,rect.X,y,rect.Width,true);
            else Icons(stats.Mana,stats.ManaMax,stats.ManaPerSegment,0,rect.X,y,rect.Width,true,fancy);
        }
        private float Icons(int current,int max,float per,int fruit,float x,float y,float width,bool mana,bool fancy)
        {
            int count=Math.Max(0,(int)Math.Ceiling(max/Math.Max(1,per)));
            if(count==0) return 0;
            Texture2D background=fancy?Texture("FancyClassic",mana?"Star_Single":"Heart_Single_Fancy"):mana?TextureAssets.Mana.Value:TextureAssets.Heart.Value;
            float desired=fancy?25:23;
            int columns=Math.Max(1,(int)(width/desired));
            // Real segment count is never truncated. Additional stars stay
            // horizontal within each row, and wrap inside this local rectangle.
            float cell=Math.Min(desired,width/Math.Min(columns,count));
            for(int i=0;i<count;i++)
            {
                float fill=MathHelper.Clamp((current-i*per)/Math.Max(1,per),0,1);
                var r=new F5Rect(x+i%columns*cell,y+i/columns*24,cell-1,23);
                if(fancy)
                {
                    DrawTexture(background,r,Color.White,1);
                    var foreground=Texture("FancyClassic",mana?"Star_Fill":i<fruit?"Heart_Fill_B":"Heart_Fill");
                    DrawTexture(foreground,new F5Rect(r.X+3,r.Y+3,r.Width-6,r.Height-6),Color.White,fill);
                }
                else
                {
                    var texture=mana?TextureAssets.Mana.Value:i<fruit?TextureAssets.Heart2.Value:TextureAssets.Heart.Value;
                    int brightness=(int)(30+225*fill); float pulse=.75f+fill*.25f;
                    if(fill>0&&fill<1||current==(i+1)*per) pulse+=Main.cursorScale-1;
                    float w=r.Width*pulse,h=r.Height*pulse;
                    DrawTexture(texture,new F5Rect(r.X+(r.Width-w)/2,r.Y+(r.Height-h)/2,w,h),new Color(brightness,brightness,brightness,(int)(brightness*.9f)),1);
                }
            }
            return (float)Math.Ceiling((double)count/columns)*24;
        }
        private void Bar(int current,int maximum,float per,int fruit,float x,float y,float width,bool mana)
        {
            int count=Math.Max(1,(int)Math.Ceiling(maximum/Math.Max(1,per)));
            var fill=Texture("HorizontalBars",mana?"MP_Fill":"HP_Fill");
            var middle=Texture("HorizontalBars",mana?"MP_Panel_Middle":"HP_Panel_Middle");
            var left=Texture("HorizontalBars","Panel_Left"); var right=Texture("HorizontalBars",mana?"MP_Panel_Right":"HP_Panel_Right");
            float natural=count*fill.Width+left.Width+right.Width;
            float scale=Math.Min(1,width/Math.Max(1,natural));
            float step=fill.Width*scale;
            DrawTexture(left,new F5Rect(x,y,left.Width*scale,middle.Height*scale),Color.White,1); x+=left.Width*scale;
            for(int i=0;i<count;i++)
            {
                var r=new F5Rect(x+i*step,y,step,middle.Height*scale); DrawTexture(middle,r,Color.White,1);
                var f=mana?fill:i<fruit?Texture("HorizontalBars","HP_Fill_Honey"):fill;
                DrawTexture(f,new F5Rect(r.X,r.Y+Math.Max(0,(middle.Height-fill.Height)*scale/2),step,fill.Height*scale),Color.White,MathHelper.Clamp((current-i*per)/Math.Max(1,per),0,1));
            }
            DrawTexture(right,new F5Rect(x+count*step,y,right.Width*scale,right.Height*scale),Color.White,1);
        }
        private static void DrawTexture(Texture2D texture,F5Rect rect,Color color,float fill)
        {
            if(texture==null||texture.IsDisposed||fill<=0||rect.Width<=0||rect.Height<=0) return;
            int cropped=Math.Max(1,(int)Math.Round(texture.Width*fill));
            Main.spriteBatch.Draw(texture,new Rectangle((int)rect.X,(int)rect.Y,(int)(rect.Width*fill),(int)rect.Height),new Rectangle(0,0,cropped,texture.Height),color);
        }
    }
}
