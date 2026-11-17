using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed partial class CompanionWindow
    {
        private void RefreshDynamicPresentation()
        {
            foreach(var c in controls) if(c.ProfileId!=Guid.Empty)
            { c.Joined=host.IsJoined(c.ProfileId);string state=host.Status(c.ProfileId);if(c.StateText!=state){c.StateText=state;c.Hint=c.Text+"\n"+state;} }
            UpdateLabel(selectedStatusLabel,host.Status(selected));
            UpdateLabel(respawnLabel,host.RespawnStatus(selected));
        }
        private void UpdateLabel(int index,string value)
        {
            if(index<0||index>=labels.Count||labels[index].Text==value)return;
            var old=labels[index];labels[index]=new F5Element(old.Kind,old.Rect,value??"",old.TextSize,old.TextScale,F5Command.None);
        }
        private static void Fill(F5Rect r,Color color)
        {
            if(r.Width<=0||r.Height<=0)return;
            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value,new Vector2(r.X,r.Y),new Rectangle(0,0,1,1),color,0,Vector2.Zero,new Vector2(r.Width,r.Height),SpriteEffects.None,0);
        }
        private float Snap(float logical) {return (float)Math.Round(logical*matrix.M11)/matrix.M11;}
        private void DrawAvatarSelection(F5Rect r)
        {
            float thickness=2/matrix.M11,length=Snap(14),inset=6;
            float cx=Snap(r.X+r.Width/2-length/2);
            Fill(new F5Rect(cx,Snap(r.Y+inset),length,thickness),Color.LightGreen);
        }
        private void DrawJoinedUnderline(F5Rect r)
        {Fill(new F5Rect(Snap(r.X+8),Snap(r.Bottom-3-2/matrix.M11),Snap(r.Width-16),2/matrix.M11),Color.LightGreen);}
        private static void DrawFrame(F5Rect r,Color color)
        {
            // Inset strokes preserve the borrowed rounded corners of each card.
            Fill(new F5Rect(r.X+5,r.Y,r.Width-10,1),color);Fill(new F5Rect(r.X+5,r.Bottom-1,r.Width-10,1),color);
            Fill(new F5Rect(r.X,r.Y+5,1,r.Height-10),color);Fill(new F5Rect(r.Right-1,r.Y+5,1,r.Height-10),color);
            for(int i=0;i<4;i++) {float d=4-i;Fill(new F5Rect(r.X+i+1,r.Y+d,1,1),color);Fill(new F5Rect(r.Right-i-2,r.Y+d,1,1),color);Fill(new F5Rect(r.X+i+1,r.Bottom-d-1,1,1),color);Fill(new F5Rect(r.Right-i-2,r.Bottom-d-1,1,1),color);}
        }
        private void DrawCentered(string text,F5Rect rect,Color color,float scale)
        {
            var font=FontAssets.MouseText.Value;var size=measure(text,scale);
            Utils.DrawBorderStringFourWay(Main.spriteBatch,font,text,rect.X+(rect.Width-size.Width)/2-size.OffsetX,rect.Y+(rect.Height-size.Height)/2-size.OffsetY,color,Color.Black,Vector2.Zero,scale);
        }
        private static void DrawEmptyIcon(int icon,F5Rect rect)
        {
            var texture=TextureAssets.Extra[54].Value;
            var frame=new Rectangle(icon%3*(texture.Width/3),icon/3*(texture.Height/7),texture.Width/3-2,texture.Height/7-2);
            float scale=Math.Min(1,Math.Min((rect.Width-7)/frame.Width,(rect.Height-7)/frame.Height));
            Main.spriteBatch.Draw(texture,new Vector2(rect.X+rect.Width/2,rect.Y+rect.Height/2),frame,Color.White*.4f,0,new Vector2(frame.Width/2f,frame.Height/2f),scale,SpriteEffects.None,0);
        }
        private void DrawStack(string text,F5Rect rect)
        {
            var font=FontAssets.ItemStack.Value;var size=font.MeasureString(text);
            float scale=Math.Min(.8f,Math.Max(.5f,(rect.Width-3)/Math.Max(1,size.X)));
            Vector2 position=Vector2.Transform(new Vector2(rect.X+2,rect.Bottom-3-font.LineSpacing*scale),matrix);
            position=new Vector2((float)Math.Round(position.X),(float)Math.Round(position.Y));
            position=Vector2.Transform(position,Matrix.Invert(matrix));
            // ItemStack has its own readable glyphs and native shadow. Never use
            // the general label helper: it can ellipsize a valid stack count.
            Utils.DrawBorderStringFourWay(Main.spriteBatch,font,text,position.X,position.Y,Color.White,Color.Black,Vector2.Zero,scale);
        }
        private void PointDraw(Action draw)
        {
            var batch=Main.spriteBatch;var d=batch.GraphicsDevice;var blend=d.BlendState;var sampler=d.SamplerStates[0];var depth=d.DepthStencilState;var raster=d.RasterizerState;
            batch.End();
            try {batch.Begin(SpriteSortMode.Deferred,blend,SamplerState.PointClamp,depth,raster,null,matrix);draw();}
            finally {batch.End();batch.Begin(SpriteSortMode.Deferred,blend,sampler,depth,raster,null,matrix);}
        }
    }
}
