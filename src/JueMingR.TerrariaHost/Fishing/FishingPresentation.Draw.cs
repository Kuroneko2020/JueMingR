using System;
using System.Collections.Generic;
using JueMingR.Features.Fishing;
using JueMingR.TerrariaHost.F5;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.Fishing
{
    internal sealed partial class FishingPresentation
    {
        private readonly Dictionary<FishKey,Texture2D> icons=new Dictionary<FishKey,Texture2D>();
        private long iconProjection=-1;
        private void PrepareIcons()
        {
            if(iconProjection!=projection){icons.Clear();iconProjection=projection;}
            foreach(var p in Parts)
            {
                if(!p.Fish.HasValue)continue;var fish=p.Fish.Value;Texture2D previous;
                if(fish.Kind==FishKind.Item?fish.Id>=TextureAssets.Item.Length:fish.Id>=TextureAssets.Npc.Length)continue;
                var current=fish.Kind==FishKind.Item?TextureAssets.Item[fish.Id]?.Value:TextureAssets.Npc[fish.Id]?.Value;
                if(icons.TryGetValue(fish,out previous) && ReferenceEquals(current,previous) && previous!=null && !previous.IsDisposed)continue;
                if(fish.Kind==FishKind.Item && fish.Id<TextureAssets.Item.Length){Main.instance.LoadItem(fish.Id);icons[fish]=TextureAssets.Item[fish.Id]?.Value;}
                else if(fish.Kind==FishKind.Npc && fish.Id<TextureAssets.Npc.Length){Main.instance.LoadNPC(fish.Id);icons[fish]=TextureAssets.Npc[fish.Id]?.Value;}
            }
        }
        internal void Draw(Action<F5Rect> keyboard)
        {
            if(!ready)return;
            renderer.Pass(matrix,view,()=>{foreach(var p in Parts)if(p.Region==0)DrawPart(p,keyboard);});
            if(listRect.Width>0 && listRect.Height>0)renderer.Pass(matrix,listRect,()=>{foreach(var p in Parts)if(p.Region==1 && p.Command!=Command.Remove)DrawPart(p,keyboard);foreach(var p in Parts)if(p.Region==1 && p.Command==Command.Remove)DrawPart(p,keyboard);});
        }
        internal void DrawPopup(Action<F5Rect> keyboard)
        {
            if(!ready || !Visible)return;
            renderer.Pass(matrix,popupRect,()=>{renderer.Panel(popupRect);foreach(var p in Parts)if(p.Region==2)DrawPart(p,keyboard);});
            renderer.Pass(matrix,popupBody,()=>{foreach(var p in Parts)if(p.Region==3)DrawPart(p,keyboard);});
        }
        private void DrawPart(Part p,Action<F5Rect> keyboard)
        {
            var e=p.Element;bool hover=e.Rect.Contains(pointer.X,pointer.Y) && (!Visible || p.Region>=2);
            // Presets are content rows, not nested cards. Only the whole apply
            // target lights on hover; icons do not imply separate commands.
            if(p.Command==Command.ApplyPreset){if(hover && p.Enabled)renderer.ItemButton(e.Rect,true,true);return;}
            if(p.Command==Command.DeletePreset){if(hover)renderer.ItemButton(e.Rect,p.Enabled,true);renderer.Cross(new F5Rect(e.Rect.X+(e.Rect.Width-22)/2,e.Rect.Y+(e.Rect.Height-22)/2,22,22),p.Enabled);return;}
            if(p.Command==Command.Close && overlay==Overlay.Presets)
            {if(hover)renderer.ItemButton(e.Rect,p.Enabled,true);F5ControlRenderer.Text(Main.spriteBatch,FontAssets.MouseText.Value,p.Label,hover?Color.White:Color.LightSteelBlue);return;}
            // Keep the forgiving hit target; the glyph itself is a small square
            // rather than two diagonals stretched to the full card height.
            if(p.Command==Command.Remove){renderer.Cross(new F5Rect(e.Rect.X+(e.Rect.Width-22)/2,e.Rect.Y+(e.Rect.Height-22)/2,22,22),p.Enabled);return;}
            if(p.Command==Command.Hotkey){keyboard?.Invoke(e.Rect);return;}
            if(e.Kind==F5ElementKind.Panel){renderer.Panel(e.Rect);return;}
            if(e.Kind==F5ElementKind.Divider){renderer.Divider(e.Rect);return;}
            if(e.Kind!=F5ElementKind.Button && e.Kind!=F5ElementKind.Field){if(p.Ink.HasValue)F5ControlRenderer.Text(Main.spriteBatch,FontAssets.MouseText.Value,e,p.Ink.Value);else renderer.Label(e);return;}
            if(p.Command==Command.Field || p.Command==Command.RenameField)
            {
                renderer.ItemButton(e.Rect,p.Enabled,hover);
                bool editingThis=Editor!=null && (p.Command==Command.Field || editing==Edit.Rename);
                if(editingThis)
                {
                    if(editView.SelectionRight>editView.SelectionLeft)Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value,new Rectangle((int)(e.Rect.X+6+editView.SelectionLeft),(int)e.Rect.Y+4,(int)(editView.SelectionRight-editView.SelectionLeft),(int)e.Rect.Height-8),Color.CornflowerBlue);
                    var label=new F5Element(F5ElementKind.Text,new F5Rect(e.Rect.X+6,e.Rect.Y+(e.Rect.Height-editView.Size.Height)/2,editView.Size.Width,editView.Size.Height),editView.Text,editView.Size,.7f,F5Command.None);renderer.Label(label);
                    Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value,new Rectangle((int)(e.Rect.X+6+editView.Caret),(int)e.Rect.Y+5,1,(int)e.Rect.Height-10),Color.White);
                    Main.instance.SetIMEPanelAnchor(new Vector2(e.Rect.X+6+editView.Caret,e.Rect.Bottom+32),0);
                }
                else if(p.Label!=null)renderer.Label(p.Label);
                return;
            }
            if(p.Fish.HasValue || p.Label!=null)
            {
                if(!p.PlainIcon)renderer.ItemButton(e.Rect,p.Enabled,hover);Texture2D icon;
                if(p.Fish.HasValue && icons.TryGetValue(p.Fish.Value,out icon) && icon!=null && !icon.IsDisposed)
                {
                    var fish=p.Fish.Value;var rect=new F5Rect(e.Rect.X+3,e.Rect.Y+3,30,e.Rect.Height-6);
                    if(fish.Kind==FishKind.Item)renderer.PreparedItem(fish.Id,icon,rect);
                    else
                    {
                        int frames=Math.Max(1,Main.npcFrameCount[fish.Id]);var frame=new Rectangle(0,0,icon.Width,icon.Height/frames);float scale=Math.Min(1,Math.Min(rect.Width/frame.Width,rect.Height/frame.Height));
                        Main.spriteBatch.Draw(icon,new Vector2(rect.X+rect.Width/2,rect.Y+rect.Height/2),frame,Color.White,0,new Vector2(frame.Width/2f,frame.Height/2f),scale,SpriteEffects.None,0);
                    }
                }
                if(p.Label!=null)renderer.Label(p.Label);if(p.Selected)renderer.Selection(e.Rect);
            }
            else renderer.Button(e,p.Selected,p.Enabled,p.Command==Command.Feature && p.Value==0,hover);
        }
        internal string Hint(float x,float y,out F5Rect rect,F5Rect? clip=null)
        {
            rect=default(F5Rect);if(!ready || dirty)return null;
            if(Visible && !popupRect.Contains(x,y))return null;
            foreach(var p in Parts)
            {
                if(p.Hint==null && p.Command!=Command.Hotkey)continue;
                if(Visible?p.Region<2:p.Region>=2)continue;
                var region=p.Region==0?view:p.Region==1?listRect:p.Region==2?popupRect:popupBody;
                if(clip.HasValue && !Visible)region=F5HintLayout.Intersect(region,clip.Value);
                var hit=p.Element.Description!=null?p.Element.HintRect:p.Element.Rect;
                if(!region.Contains(x,y) || !hit.Contains(x,y))continue;
                rect=F5HintLayout.Intersect(region,hit);
                if(p.Command==Command.Hotkey)return "双击设置快捷键";
                return p.Hint;
            }
            return null;
        }
    }
}
