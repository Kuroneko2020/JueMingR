using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;

namespace JueMingR.TerrariaHost.F5
{
    // Native head frames include transparent full-body padding. Cache their
    // actual alpha bounds once per borrowed texture/frame, then transform those
    // bounds through the original DrawData. Stable portraits never read GPU data.
    internal sealed class CompanionHeadLayout
    {
        private static readonly ConditionalWeakTable<Texture2D,Dictionary<Rectangle,Rectangle>> alpha=new ConditionalWeakTable<Texture2D,Dictionary<Rectangle,Rectangle>>();
        private readonly List<DrawData> data=new List<DrawData>();
        private readonly List<int> dust=new List<int>(),gore=new List<int>();
        private int revision=-1,generation=-1;
        private Vector2 size,center;
        private float scale=1;
        internal int Builds {get;private set;}
        internal void Clear() {data.Clear();dust.Clear();gore.Clear();revision=generation=-1;size=Vector2.Zero;}
        internal void Prepare(Player player,int appearance,int skin,Vector2 dimensions)
        {
            bool current=revision==appearance&&generation==skin&&size==dimensions;
            if(current) {foreach(var layer in data)if(layer.texture==null||layer.texture.IsDisposed){current=false;break;}}
            if(current)return;
            Build(player,1);var bounds=Bounds();
            scale=Math.Min(1.65f,Math.Min((dimensions.X-12)/Math.Max(1,bounds.Z),(dimensions.Y-12)/Math.Max(1,bounds.W)));
            Build(player,scale);bounds=Bounds();center=new Vector2(bounds.X+bounds.Z/2,bounds.Y+bounds.W/2);
            revision=appearance;generation=skin;size=dimensions;Builds++;
        }
        internal float Scale {get{return scale;}}
        internal Vector2 Anchor(F5Rect rect) {return new Vector2(rect.X+rect.Width/2,rect.Y+rect.Height/2)-center;}
        private void Build(Player player,float factor)
        {
            data.Clear();dust.Clear();gore.Clear();var state=new PlayerDrawHeadSet();
            state.BoringSetup(player,data,dust,gore,0,0,1,factor);
            PlayerDrawHeadLayers.DrawPlayer_00_BackHelmet(ref state);
            PlayerDrawHeadLayers.DrawPlayer_01_FaceSkin(ref state);
            PlayerDrawHeadLayers.DrawPlayer_02_DrawArmorWithFullHair(ref state);
            PlayerDrawHeadLayers.DrawPlayer_03_HelmetHair(ref state);
            PlayerDrawHeadLayers.DrawPlayer_04_HatsWithFullHair(ref state);
            PlayerDrawHeadLayers.DrawPlayer_05_TallHats(ref state);
            PlayerDrawHeadLayers.DrawPlayer_06_NormalHats(ref state);
            PlayerDrawHeadLayers.DrawPlayer_07_JustHair(ref state);
            PlayerDrawHeadLayers.DrawPlayer_08_FaceAcc(ref state);
        }
        private static Rectangle Opaque(Texture2D texture,Rectangle frame)
        {
            var frames=alpha.GetValue(texture,key=>new Dictionary<Rectangle,Rectangle>());Rectangle found;
            if(frames.TryGetValue(frame,out found))return found;
            var pixels=new Color[frame.Width*frame.Height];texture.GetData(0,frame,pixels,0,pixels.Length);
            int left=frame.Width,right=-1,top=frame.Height,bottom=-1;
            for(int y=0;y<frame.Height;y++)for(int x=0;x<frame.Width;x++)if(pixels[y*frame.Width+x].A>16){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
            found=right<left?Rectangle.Empty:new Rectangle(left,top,right-left+1,bottom-top+1);
            if(frames.Count>=32)frames.Clear();frames.Add(frame,found);return found;
        }
        private Vector4 Bounds()
        {
            var bounds=EmptyBounds;foreach(var layer in data)AddBounds(ref bounds,layer);
            return bounds.X==Single.MaxValue?new Vector4(-16,-16,32,32):new Vector4(bounds.X,bounds.Y,bounds.Z-bounds.X,bounds.W-bounds.Y);
        }
        internal static Vector4 EmptyBounds {get{return new Vector4(Single.MaxValue,Single.MaxValue,Single.MinValue,Single.MinValue);}}
        // The showcase consumes the same native DrawData as heads. Keep one
        // bounded weak texture/frame alpha cache, not per-frame GPU readbacks.
        internal static void AddBounds(ref Vector4 bounds,DrawData layer)
        {
            if(layer.texture==null||layer.color.A==0)return;
            var frame=layer.sourceRect??layer.texture.Bounds;var opaque=Opaque(layer.texture,frame);if(opaque.IsEmpty)return;
            for(int corner=0;corner<4;corner++) {
                var p=new Vector2(corner%2==0?opaque.Left:opaque.Right,corner<2?opaque.Top:opaque.Bottom);
                if((layer.effect&SpriteEffects.FlipHorizontally)!=0)p.X=frame.Width-p.X;
                if((layer.effect&SpriteEffects.FlipVertically)!=0)p.Y=frame.Height-p.Y;
                p=(p-layer.origin)*layer.scale;
                float cos=(float)Math.Cos(layer.rotation),sin=(float)Math.Sin(layer.rotation);
                p=new Vector2(p.X*cos-p.Y*sin,p.X*sin+p.Y*cos)+layer.position;
                bounds.X=Math.Min(bounds.X,p.X);bounds.Y=Math.Min(bounds.Y,p.Y);bounds.Z=Math.Max(bounds.Z,p.X);bounds.W=Math.Max(bounds.W,p.Y);
            }
        }
    }
}
