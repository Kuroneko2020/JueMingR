using System;
using JueMingR.Platform.Combat;
using JueMingR.TerrariaHost.Guidance;
using JueMingR.TerrariaHost.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed class CombatObservationWorldLayer
    {
        private struct Stroke {internal Vector2 A,B;internal Color Color;internal float Width;}
        private readonly Stroke[] strokes=new Stroke[16384];
        private static readonly Color[] colors={new Color(90,205,250),new Color(145,230,120),new Color(245,185,65),new Color(245,100,120),new Color(215,140,255)};
        private readonly HostCombatObservation host;
        private int count;private Matrix zoom,inverse;private string pathText;private bool legend,limited;
        internal CombatObservationWorldLayer(HostCombatObservation host){this.host=host;}
        internal int StrokeCount {get{return count;}}
        internal void Clear(){count=0;pathText=null;legend=limited=false;}
        internal void Prepare()
        {
            Clear();if(!host.Enabled || !host.CanDraw || !WorldPresentation.CanDraw || Main.GameViewMatrix==null)return;
            zoom=Main.GameViewMatrix.ZoomMatrix;if(zoom.M11<=0 || zoom.M22<=0)return;inverse=Matrix.Invert(zoom);
            if(host.Collision)
            {
                legend=true;limited=host.Geometry.EventOverflow;Samples(host.Geometry.Attacks,0);Samples(host.Geometry.Npcs,1);Samples(host.Geometry.Events,2);
            }
            var path=host.Path?host.Prediction.Cache.Read(0):null;
            if(path==null)return;
            bool approximate=(path.Assumptions&(PredictionAssumption.ApproximateMechanism|PredictionAssumption.RandomRepresentative))!=0;
            var color=approximate?new Color(255,210,110):new Color(235,235,255);
            for(int i=1;i<path.Count;i++)
            {
                var point=path[i];var prior=path[i-1];
                var b=new Vector2(point.Bounds.CenterX,point.Bounds.CenterY);
                if(!point.NewSegment)Line(new Vector2(prior.Bounds.CenterX,prior.Bounds.CenterY),b,color,approximate,1.5f);
                if(point.NewSegment || i%30==0 || i==path.Count-1)
                {Line(b-new Vector2(3,0),b+new Vector2(3,0),color,false,2);Line(b-new Vector2(0,3),b+new Vector2(0,3),color,false,2);}
            }
            pathText=path.Stop==PredictionStop.None?(approximate?"NPC 路径：近似":"NPC 路径：条件预测"):StopText(path.Stop);
            if((path.Assumptions&PredictionAssumption.RandomRepresentative)!=0)pathText+=" · 随机代表路线";
            if((path.Assumptions&PredictionAssumption.NetworkObservation)!=0)pathText+=" · 依据本机网络观察";
            pathText+=" · 假设玩家保持当前位置";
        }
        private static string StopText(PredictionStop reason)
        {
            switch(reason)
            {
                case PredictionStop.UnsupportedMechanism:return "NPC 路径：仅短段近似 · 后续机制未知";
                case PredictionStop.RandomDestination:return "NPC 路径：已截断 · 瞬移目的地尚未确定";
                case PredictionStop.MissingDependency:return "NPC 路径：已截断 · 关联部位缺失";
                case PredictionStop.Slope:return "NPC 路径：已截断 · 斜坡后续反应未建模";
                case PredictionStop.LiquidEffect:return "NPC 路径：已截断 · 特殊液体反应未建模";
                case PredictionStop.Despawn:return "NPC 路径：预计结束或失活";
                case PredictionStop.PhaseBoundary:return "NPC 路径：已截断 · 后续阶段待确认";
                case PredictionStop.BuffTransition:return "NPC 路径：已截断 · 状态变化待确认";
                default:return "NPC 路径：已截断 · 局部信息不足";
            }
        }
        private void Samples(CombatShapeSample[] samples,int kind)
        {
            for(int i=0;i<samples.Length;i++)
            {
                var sample=samples[i];if(sample==null || sample.Tick!=Main.GameUpdateCount || sample.Session!=host.Session)continue;
                if(kind==0 && i<Main.maxProjectiles)
                {var p=Main.projectile[i];if(p==null || !p.active || !ReferenceEquals(p,sample.Token) || (int)p.key!=sample.Identity || p.type!=sample.Type || p.owner!=sample.Owner)continue;}
                else if(kind==0)
                {var p=Main.player[i-Main.maxProjectiles];if(p==null || !p.active || p.dead || !ReferenceEquals(p,sample.Token) || p.itemAnimation<=0)continue;}
                else if(kind==1)
                {var n=Main.npc[i];if(n==null || !n.active || !ReferenceEquals(n,sample.Token) || n.type!=sample.Type || n.generation!=sample.Identity)continue;}
                limited|=sample.Overflow;
                for(int j=0;j<sample.Count;j++)
                {
                    var shape=sample.Shapes[j];var color=colors[shape.Category];
                    if(shape.Line)
                    {
                        var direction=shape.B-shape.A;float length=direction.Length();if(length<.001f)continue;
                        var normal=new Vector2(-direction.Y,direction.X)*(shape.Width*.5f/length);
                        Line(shape.A+normal,shape.B+normal,color,shape.Approximate,1.5f);Line(shape.A-normal,shape.B-normal,color,shape.Approximate,1.5f);
                        Line(shape.A-normal,shape.A+normal,color,shape.Approximate,1.5f);Line(shape.B-normal,shape.B+normal,color,shape.Approximate,1.5f);
                    }
                    else
                    {
                        var c=new Vector2(shape.B.X,shape.A.Y);var d=new Vector2(shape.A.X,shape.B.Y);
                        Line(shape.A,c,color,shape.Approximate,1.5f);Line(c,shape.B,color,shape.Approximate,1.5f);Line(shape.B,d,color,shape.Approximate,1.5f);Line(d,shape.A,color,shape.Approximate,1.5f);
                    }
                }
            }
        }
        private void Line(Vector2 a,Vector2 b,Color color,bool dashed,float width)
        {
            a=GuidanceWorldLayer.Project(a,zoom);b=GuidanceWorldLayer.Project(b,zoom);
            // Clip the actual shape, not the object's origin: an offscreen
            // laser may still cross the entire viewport.
            if(!Clip(ref a,ref b))return;
            float length=Vector2.Distance(a,b);
            if(dashed)
            {for(float d=0;d<length;d+=12)Add(Vector2.Lerp(a,b,d/length),Vector2.Lerp(a,b,Math.Min(length,d+6)/length),color,width);}
            else Add(a,b,color,width);
        }
        private void Add(Vector2 a,Vector2 b,Color color,float width)
        {if(count==strokes.Length){limited=true;return;}strokes[count++]=new Stroke{A=Vector2.Transform(a,inverse),B=Vector2.Transform(b,inverse),Color=color,Width=width*inverse.M11};}
        private static bool Clip(ref Vector2 a,ref Vector2 b)
        {
            if(float.IsNaN(a.X) || float.IsNaN(a.Y) || float.IsNaN(b.X) || float.IsNaN(b.Y))return false;
            var delta=b-a;float lo=0,hi=1;
            if(!Edge(-delta.X,a.X+4,ref lo,ref hi) || !Edge(delta.X,Main.screenWidth+4-a.X,ref lo,ref hi) || !Edge(-delta.Y,a.Y+4,ref lo,ref hi) || !Edge(delta.Y,Main.screenHeight+4-a.Y,ref lo,ref hi))return false;
            b=a+delta*hi;a+=delta*lo;return true;
        }
        private static bool Edge(float p,float q,ref float lo,ref float hi)
        {if(p==0)return q>=0;float r=q/p;if(p<0){if(r>hi)return false;lo=Math.Max(lo,r);}else{if(r<lo)return false;hi=Math.Min(hi,r);}return true;}
        internal bool Draw()
        {
            if(!host.Enabled || !host.CanDraw || !WorldPresentation.CanDraw || Main.spriteBatch==null)return true;
            var batch=Main.spriteBatch;var pixel=TextureAssets.MagicPixel.Value;
            // MagicPixel's asset is larger than one texel; a null source would
            // multiply both dimensions and turn outlines into opaque blocks.
            for(int i=0;i<count;i++){var s=strokes[i];var delta=s.B-s.A;batch.Draw(pixel,s.A,new Rectangle(0,0,1,1),s.Color,(float)Math.Atan2(delta.Y,delta.X),Vector2.Zero,new Vector2(delta.Length(),s.Width),SpriteEffects.None,0);}
            float y=Main.screenHeight-125;
            if(legend)
            {
                Text("碰撞：蓝·玩家攻击  绿·敌怪受击  黄·伤害专属",y,Color.White);y+=22;
                Text("红·不可破坏攻击  紫·可破坏攻击  虚线·近似",y,Color.White);y+=22;
            }
            if(pathText!=null){Text(pathText,y,new Color(235,220,160));y+=22;}
            if(limited)Text("显示数量已达上限，部分区域未绘出",y,Color.Orange);
            return true;
        }
        private void Text(string value,float y,Color color)
        {if(FontAssets.MouseText?.Value!=null)Utils.DrawBorderString(Main.spriteBatch,value,Vector2.Transform(new Vector2(16,Math.Max(16,y)),inverse),color,.72f*inverse.M11);}
    }
}
