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
        private int count,eventStart,eventEnd;private long presentation;private bool eventsDrawn;private Matrix zoom,inverse;private string pathText;private bool legend,limited;
        internal CombatObservationWorldLayer(HostCombatObservation host){this.host=host;}
        internal int StrokeCount {get{return count;}}
        internal void Clear(){count=eventStart=eventEnd=0;eventsDrawn=false;pathText=null;legend=limited=false;}
        internal void Prepare()
        {
            Prediction.AimLightTrace.Presentation("prepare-enter",null,0,0,false);
#if JMR_AIM_LIGHT
            try
            {
#endif
            Clear();if(!host.Enabled || !host.CanDraw || !WorldPresentation.CanDraw || Main.GameViewMatrix==null){Prediction.AimLightTrace.Presentation(!host.Enabled?"prepare-disabled":!host.CanDraw?"prepare-host-gate":!WorldPresentation.CanDraw?"prepare-world-gate":"prepare-no-matrix",null,0,0,false);return;}
            zoom=Main.GameViewMatrix.ZoomMatrix;if(zoom.M11<=0 || zoom.M22<=0){Prediction.AimLightTrace.Presentation("prepare-invalid-zoom",null,0,0,false);return;}inverse=Matrix.Invert(zoom);
            if(host.Collision)
            {
                host.Geometry.PrepareEvents();legend=true;limited=host.Geometry.EventOverflow;Samples(host.Geometry.Attacks,0);Samples(host.Geometry.Npcs,1);Samples(host.Geometry.Bodies,3);
                eventStart=count;presentation++;Samples(host.Geometry.Events,2);eventEnd=count;
            }
            var path=host.Path?host.Prediction.Cache.Read(0):null;
            Prediction.AimLightTrace.Presentation("cache-consume",path,0,eventEnd,false);
            if(path==null){Prediction.AimLightTrace.Presentation(host.Path?"prepare-cache-empty":"prepare-path-gate",null,0,eventEnd,false);return;}
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
            if(path.Strategy==PredictionStrategy.SegmentedTrend)
                pathText+=path.Quality==PredictionQuality.LimitedObservation?" · 运动观察较少":" · 依据近期移动，远端仅供参考";
            else pathText+=(path.Assumptions&PredictionAssumption.HeldPlayerControls)!=0?" · 假设玩家延续当前输入":" · 假设玩家保持当前位置";
            Prediction.AimLightTrace.Presentation("prepared",path,count-eventEnd,eventEnd,pathText!=null);
#if JMR_AIM_LIGHT
            }
            catch(Exception error){Prediction.AimLightTrace.Fault("prepare",error,(long)Main.GameUpdateCount);throw;}
#endif
        }
        private static string StopText(PredictionStop reason)
        {
            switch(reason)
            {
                case PredictionStop.UnsupportedMechanism:return "NPC 路径：仅短段近似 · 后续机制未知";
                case PredictionStop.RandomDestination:return "NPC 路径：已截断 · 瞬移目的地尚未确定";
                case PredictionStop.RandomDecision:return "NPC 路径：已截断 · 随机行为尚未确定";
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
                var sample=samples[i];if(sample==null || sample.Session!=host.Session)continue;
                if(kind==2){if(i>=host.Geometry.EventCount || sample.Presented || unchecked(Main.GameUpdateCount-sample.Tick)>4)continue;sample.Presentation=presentation;}
                else if(sample.Tick!=Main.GameUpdateCount)continue;
                if(kind==0 && i<Main.maxProjectiles)
                {var p=Main.projectile[i];if(p==null || !p.active || !ReferenceEquals(p,sample.Token) || (int)p.key!=sample.Identity || p.type!=sample.Type || p.owner!=sample.Owner)continue;}
                else if(kind==0)
                {var p=Main.player[i-Main.maxProjectiles];if(p==null || !p.active || p.dead || !ReferenceEquals(p,sample.Token) || p.itemAnimation<=0)continue;}
                else if(kind==1)
                {var n=Main.npc[i];if(n==null || !n.active || !ReferenceEquals(n,sample.Token) || n.type!=sample.Type || n.generation!=sample.Identity)continue;}
                else if(kind==3)
                {var p=Main.player[i];if(p==null || !p.active || p.dead || !ReferenceEquals(p,sample.Token))continue;}
                limited|=sample.Overflow;
                for(int j=0;j<sample.Count;j++)
                {
                    if(count==strokes.Length){limited=true;return;}
                    var shape=sample.Shapes[j];var color=colors[shape.Category];
                    bool dashed=shape.Approximate || shape.Condition!=0 || shape.Kind==3 || shape.Kind==4 || shape.HasBounds;
                    if(shape.Kind==5)
                    {
                        // Native lightning consists of discrete point boxes.
                        // Connecting them would invent damage across gaps.
                        var pairBounds=sample.PointBounds;pairBounds.Inflate(sample.PointSize.X/2,sample.PointSize.Y/2);
                        if(!Visible(pairBounds.TopLeft(),pairBounds.BottomRight()))continue;
                        for(int k=0;k<sample.PointCount;k++)
                        {if(count==strokes.Length){limited=true;return;}var p=sample.Points[k].ToPoint();var box=new Rectangle(p.X-sample.PointSize.X/2,p.Y-sample.PointSize.Y/2,sample.PointSize.X/2*2+1,sample.PointSize.Y/2*2+1);box=Rectangle.Intersect(box,pairBounds);if(box.Width>0 && box.Height>0 && Visible(box.TopLeft(),box.BottomRight()))Box(box.TopLeft(),box.BottomRight(),color,dashed);}
                        continue;
                    }
                    if(shape.Kind==7)
                    {
                        float angle=(float)Math.Atan2(shape.B.Y-shape.A.Y,shape.B.X-shape.A.X);var normal=new Vector2(-(float)Math.Sin(angle),(float)Math.Cos(angle))*shape.Width;
                        Line(shape.A+normal,shape.B+normal,color,dashed,1.5f);Line(shape.A-normal,shape.B-normal,color,dashed,1.5f);
                        for(int cap=0;cap<2;cap++)
                        {var center=cap==0?shape.A:shape.B;float start=angle+(cap==0?(float)Math.PI/2:-(float)Math.PI/2),phase=0;var prior=center+start.ToRotationVector2()*shape.Width;for(int k=1;k<=24;k++){var next=center+(start+(float)Math.PI*k/24).ToRotationVector2()*shape.Width;Line(prior,next,color,dashed,1.5f,ref phase);prior=next;}}
                        continue;
                    }
                    if(shape.Kind!=0)
                    {
                        bool sector=shape.Kind==3 || shape.Kind==4;
                        float first=sector?shape.B.X-shape.B.Y:0,last=sector?shape.B.X+shape.B.Y:(float)Math.PI*2;
                        int segments=sector?24:64;Vector2 prior=CurvePoint(shape,first);float dashPhase=0;
                        if(sector)Line(shape.A,prior,color,dashed,1.5f,ref dashPhase);
                        for(int k=1;k<=segments;k++){var next=CurvePoint(shape,first+(last-first)*k/segments);Line(prior,next,color,dashed,1.5f,ref dashPhase);prior=next;}
                        if(sector)Line(prior,shape.A,color,dashed,1.5f,ref dashPhase);
                        continue;
                    }
                    if(shape.Line)
                    {
                        var direction=shape.B-shape.A;float length=direction.Length();if(length<.001f)continue;
                        var normal=new Vector2(-direction.Y,direction.X)*(shape.Width*.5f/length);
                        Line(shape.A+normal,shape.B+normal,color,dashed,1.5f);Line(shape.A-normal,shape.B-normal,color,dashed,1.5f);
                        Line(shape.A-normal,shape.A+normal,color,dashed,1.5f);Line(shape.B-normal,shape.B+normal,color,dashed,1.5f);
                    }
                    else
                    {
                        Box(shape.A,shape.B,color,dashed);
                    }
                }
            }
        }
        private static Vector2 CurvePoint(CombatShape shape,float angle)
        {
            float y=(float)Math.Sin(angle),ratio=shape.Kind==2?(y>0?shape.B.Y:shape.B.X):1;
            return shape.A+new Vector2((float)Math.Cos(angle),y*ratio)*shape.Width;
        }
        private bool Visible(Vector2 a,Vector2 b)
        {a=GuidanceWorldLayer.Project(a,zoom);b=GuidanceWorldLayer.Project(b,zoom);return Math.Max(a.X,b.X)>=-4 && Math.Min(a.X,b.X)<=Main.screenWidth+4 && Math.Max(a.Y,b.Y)>=-4 && Math.Min(a.Y,b.Y)<=Main.screenHeight+4;}
        private void Box(Vector2 a,Vector2 b,Color color,bool dashed)
        {var c=new Vector2(b.X,a.Y);var d=new Vector2(a.X,b.Y);float phase=0;Line(a,c,color,dashed,1.5f,ref phase);Line(c,b,color,dashed,1.5f,ref phase);Line(b,d,color,dashed,1.5f,ref phase);Line(d,a,color,dashed,1.5f,ref phase);}
        private void Line(Vector2 a,Vector2 b,Color color,bool dashed,float width)
        {float phase=0;Line(a,b,color,dashed,width,ref phase);}
        private void Line(Vector2 a,Vector2 b,Color color,bool dashed,float width,ref float phase)
        {
            a=GuidanceWorldLayer.Project(a,zoom);b=GuidanceWorldLayer.Project(b,zoom);
            var original=a;float prior=phase;phase+=Vector2.Distance(a,b);
            // Clip the actual shape, not the object's origin: an offscreen
            // laser may still cross the entire viewport.
            if(!Clip(ref a,ref b))return;
            float length=Vector2.Distance(a,b);
            // A segment touching only a clip corner has no drawable length.
            // Its dash phase is already accounted for above; never divide by 0.
            if(length<=.0001f || float.IsNaN(length) || float.IsInfinity(length))return;
            if(dashed)
            {float start=(prior+Vector2.Distance(original,a))%12;for(float d=-start;d<length;d+=12)if(d+6>0)Add(Vector2.Lerp(a,b,Math.Max(0,d)/length),Vector2.Lerp(a,b,Math.Min(length,d+6)/length),color,width);}
            else Add(a,b,color,width);
        }
        private void Add(Vector2 a,Vector2 b,Color color,float width)
        {if(!Finite(a) || !Finite(b))return;if(count==strokes.Length){limited=true;return;}strokes[count++]=new Stroke{A=Vector2.Transform(a,inverse),B=Vector2.Transform(b,inverse),Color=color,Width=width*inverse.M11};}
        private static bool Finite(Vector2 p){return !float.IsNaN(p.X) && !float.IsNaN(p.Y) && !float.IsInfinity(p.X) && !float.IsInfinity(p.Y);}
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
            Prediction.AimLightTrace.Presentation("draw-enter",null,0,0,false);
#if JMR_AIM_LIGHT
            int pathIssued=0,pathCompleted=0,otherIssued=0,otherCompleted=0;
            try
            {
#endif
            if(!host.Enabled || !host.CanDraw || !WorldPresentation.CanDraw || Main.spriteBatch==null){Prediction.AimLightTrace.Presentation(!host.Enabled?"draw-disabled":!host.CanDraw?"draw-host-gate":!WorldPresentation.CanDraw?"draw-world-gate":"draw-no-batch",null,0,0,false);return true;}
            var batch=Main.spriteBatch;var pixel=TextureAssets.MagicPixel.Value;
            // MagicPixel's asset is larger than one texel; a null source would
            // multiply both dimensions and turn outlines into opaque blocks.
            for(int i=0;i<count;i++)
            {
                if(eventsDrawn && i>=eventStart && i<eventEnd)continue;var s=strokes[i];var delta=s.B-s.A;
#if JMR_AIM_LIGHT
                if(i>=eventEnd)pathIssued++;else otherIssued++;
#endif
                batch.Draw(pixel,s.A,new Rectangle(0,0,1,1),s.Color,(float)Math.Atan2(delta.Y,delta.X),Vector2.Zero,new Vector2(delta.Length(),s.Width),SpriteEffects.None,0);
#if JMR_AIM_LIGHT
                if(i>=eventEnd)pathCompleted++;else otherCompleted++;
#endif
            }
            if(!eventsDrawn && host.Collision){host.Geometry.PresentedEvents(presentation);eventsDrawn=true;}
            float y=Main.screenHeight-125;
            if(legend)
            {
                Text("碰撞：蓝·玩家攻击  绿·敌怪受击  黄·伤害专属",y,Color.White);y+=22;
                Text("红·不可破坏攻击  紫·可破坏攻击  虚线·有条件或近似",y,Color.White);y+=22;
            }
            if(pathText!=null){Text(pathText,y,new Color(235,220,160));y+=22;}
            if(limited)Text("显示数量已达上限，部分区域未绘出",y,Color.Orange);
            Prediction.AimLightTrace.Presentation("draw-complete",null,count-eventEnd,eventEnd,pathText!=null);
            return true;
#if JMR_AIM_LIGHT
            }
            catch(Exception error){Prediction.AimLightTrace.Fault("draw",error,(long)Main.GameUpdateCount);throw;}
            finally{Prediction.AimLightTrace.DrawCounts(pathIssued,pathCompleted,otherIssued,otherCompleted);}
#endif
        }
        private void Text(string value,float y,Color color)
        {if(FontAssets.MouseText?.Value!=null){Prediction.AimLightTrace.Presentation("text-issued",null,0,0,ReferenceEquals(value,pathText));Utils.DrawBorderString(Main.spriteBatch,value,Vector2.Transform(new Vector2(16,Math.Max(16,y)),inverse),color,.72f*inverse.M11);Prediction.AimLightTrace.Presentation("text-completed",null,0,0,ReferenceEquals(value,pathText));}else Prediction.AimLightTrace.Presentation("text-no-font",null,0,0,ReferenceEquals(value,pathText));}
    }
}
