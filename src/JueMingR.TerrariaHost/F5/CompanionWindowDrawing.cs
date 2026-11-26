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
            var body=host.GetBody(selected);
            foreach(var c in controls) {
                if (c.SwitchLoadout >= 0)
                {
                    var player = c.Owner ? Main.LocalPlayer : body;
                    c.Selected = player != null && player.CurrentLoadoutIndex == c.SwitchLoadout;
                    c.Enabled = player != null && !player.dead && (c.Owner || selectedTransfer) &&
                        Main.mouseItem?.IsAir != false && !host.HasItemGesture;
                }
                if(c.ActorState)
                {
                    bool recall = host.IsJoined(selected);
                    c.Enabled = host.CanChangeCompanionPresence(selected, recall, out string reason);
                    c.Hint = reason ?? (recall ? "退出当前世界并保存人物" : "加入当前世界");
                }
                if (c.ManualRecall)
                {
                    c.Enabled = host.QueryManualRecall(selected, out string reason);
                    c.Hint = reason ?? "把AI队友传送到玩家，消耗虫洞药水";
                }
                if(c.TeamIcon>=0)c.Selected=body!=null&&body.team==c.TeamIcon;
            }
            UpdateLabel(selectedStatusLabel,host.Status(selected));
            UpdateLabel(respawnLabel,host.RespawnStatus(selected));
        }
        private void DrawActorControl(Control c)
        {
            bool hover=c.Rect.Contains(pointer.X,pointer.Y);
            if(c.TeamIcon>=0) {
                var texture=TextureAssets.Pvp[1]?.Value;var highlight=TextureAssets.Pvp[2]?.Value;if(texture==null)return;
                var frame=new Rectangle(c.TeamIcon*(texture.Width/6),0,texture.Width/6-2,texture.Height);
                if((hover||c.Selected)&&highlight!=null)Main.spriteBatch.Draw(highlight,new Vector2(c.Rect.X-2,c.Rect.Y-2),Color.White);
                Main.spriteBatch.Draw(texture,new Vector2(c.Rect.X,c.Rect.Y),frame,Color.White,0,Vector2.Zero,1,SpriteEffects.None,0);
            }
            else {
                renderer.Button(c.Element,false,c.Enabled,false,hover,hover&&armedKey==c.Key);
            }
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
            F5ControlRenderer.DrawScaledOutline(Main.spriteBatch,font,text,new Vector2(rect.X+(rect.Width-size.Width)/2-size.OffsetX,rect.Y+(rect.Height-size.Height)/2-size.OffsetY),color,scale);
        }
        private static void DrawEmptyIcon(int icon,F5Rect rect)
        {
            var texture=TextureAssets.Extra[54].Value;
            var frame=new Rectangle(icon%3*(texture.Width/3),icon/3*(texture.Height/7),texture.Width/3-2,texture.Height/7-2);
            float scale=Math.Min(1,Math.Min((rect.Width-7)/frame.Width,(rect.Height-7)/frame.Height));
            Main.spriteBatch.Draw(texture,new Vector2(rect.X+rect.Width/2,rect.Y+rect.Height/2),frame,Color.White*.4f,0,new Vector2(frame.Width/2f,frame.Height/2f),scale,SpriteEffects.None,0);
        }
        private static void DrawFavoriteBackground(Control c)
        {
            var resource=c.Area==JueMingR.Platform.Companions.CompanionInventoryArea.VoidVault?TextureAssets.InventoryBack19:TextureAssets.InventoryBack10;
            var texture=resource?.Value;
            if(texture!=null) Main.spriteBatch.Draw(texture,new Rectangle((int)c.Rect.X,(int)c.Rect.Y,(int)c.Rect.Width,(int)c.Rect.Height),Color.White);
            else DrawFrame(c.Rect,Color.Gold);
        }
        private static void DrawLoadoutMark(Control c)
        {
            var texture=TextureAssets.Extra[c.LoadoutIcon]?.Value;
            if(texture!=null) Main.spriteBatch.Draw(texture,new Vector2(c.Rect.X,c.Rect.Y),null,Color.White*.7f,0,Vector2.Zero,
                c.Rect.Width/52f,SpriteEffects.None,0);
        }
        private void DrawStack(string text,F5Rect rect)
        {
            var font=FontAssets.ItemStack.Value;var size=font.MeasureString(text);
            float scale=Math.Min(.8f,Math.Max(.5f,(rect.Width-3)/Math.Max(1,size.X)));
            Vector2 position=Vector2.Transform(new Vector2(rect.X+2,rect.Bottom-3-font.LineSpacing*scale),matrix);
            position=new Vector2((float)Math.Round(position.X),(float)Math.Round(position.Y));
            position=Vector2.Transform(position,Matrix.Invert(matrix));
            // Keep native ItemStack glyphs, point sampling and this window's
            // scale-sized outline. The general label helper may ellipsize a
            // valid count and must not be used for inventory quantities.
            F5ControlRenderer.DrawScaledOutline(Main.spriteBatch,font,text,position,Color.White,scale);
        }
        private void PointDraw(Action draw)
        {
            renderer.PointImages(draw);
        }
    }
}
