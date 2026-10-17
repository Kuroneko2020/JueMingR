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
        private CombatShapeSample projectedEvent;
        private static readonly Color[] colors={new Color(90,205,250),new Color(145,230,120),new Color(245,185,65),new Color(245,100,120),new Color(215,140,255)};
        private readonly HostCombatObservation host;
        internal readonly CombatTargetMarker Marker;
        // Retained inert identity for existing read-only probes. The withdrawn
        // red display has no prepare/project/draw or resource polling entry.
        internal readonly CombatImpactMarker Impact;
        private int strokeLimit=16384;
        private int count,eventStart,eventEnd;private long presentation;private bool eventsDrawn;private Matrix zoom,inverse;private NpcTrajectory preparedPath;
        internal CombatObservationWorldLayer(HostCombatObservation host){this.host=host;Marker=new CombatTargetMarker(host);Impact=new CombatImpactMarker(host);}
        internal int StrokeCount {get{return count;}}
        internal bool Failed {get;private set;}
        private object failedPixel,failedBatch;
        // Presentation owns its latch, never prediction/operation state. Only
        // an explicit retry, replaced borrowed resources or a new session
        // clears it; clearing a frame does not. Never read Asset.Value to retry.
        internal void DisplayFailed(){Clear();Failed=true;failedPixel=TextureAssets.MagicPixel;failedBatch=Main.spriteBatch;}
        internal void Recover(){Clear();Failed=false;failedPixel=failedBatch=null;Impact.Reset();}
        internal void PollResources()
        {
            if(Failed && (!ReferenceEquals(failedPixel,TextureAssets.MagicPixel) || !ReferenceEquals(failedBatch,Main.spriteBatch)))Recover();
            Marker.PollResources();
        }
        internal void Clear(){count=eventStart=eventEnd=0;eventsDrawn=false;preparedPath=null;Marker.Clear();}
        internal void Prepare()
        {
            if(Failed){Clear();return;}
            try{PrepareCore();}
            catch(Exception error)
            {
                // Ordinary catch/rethrow preserves fatal propagation and the
                // existing Harmony observation seam (its emitter lacks filters).
                if(error is OutOfMemoryException || error is AccessViolationException)throw;
                DisplayFailed();
            }
        }
        private void PrepareCore()
        {
            Prediction.AimLightTrace.Presentation("prepare-enter",null,0,0,false);
#if JMR_AIM_LIGHT
            try
            {
#endif
            Clear();if(!host.Enabled || !host.CanDraw || !WorldPresentation.CanDraw || Main.GameViewMatrix==null){Prediction.AimLightTrace.Presentation(!host.Enabled?"prepare-disabled":!host.CanDraw?"prepare-host-gate":!WorldPresentation.CanDraw?"prepare-world-gate":"prepare-no-matrix",null,0,0,false);return;}
            Marker.Capture();
            if(host.Collision)host.Geometry.PrepareEvents();
            var path=host.DisplayPath;
            Prediction.AimLightTrace.Presentation("cache-consume",path,0,eventEnd,false);
            if(path==null)
            {
                Prediction.AimLightTrace.Presentation(host.Path?"prepare-cache-empty":"prepare-path-gate",null,0,eventEnd,false);return;
            }
            preparedPath=path;
            Prediction.AimLightTrace.Presentation("prepared",path,count-eventEnd,eventEnd,false);
#if JMR_AIM_LIGHT
            }
            catch(Exception error){Prediction.AimLightTrace.Fault("prepare",error,(long)Main.GameUpdateCount);throw;}
#endif
        }
        private void ProjectPresentation()
        {
            // Only bounded geometry from completed observations is traversed.
            // The final camera decides visibility before the physical-stroke
            // cap, so offscreen facts cannot starve later visible strokes.
            count=eventStart=eventEnd=0;eventsDrawn=false;
            strokeLimit=host.Path?strokes.Length/2:strokes.Length;
            if(host.Collision)
            {Samples(host.Geometry.Attacks,0);Samples(host.Geometry.Npcs,1);Samples(host.Geometry.Bodies,3);eventStart=count;presentation++;Samples(host.Geometry.Events,2);eventEnd=count;}
            projectedEvent=null;strokeLimit=strokes.Length;var path=preparedPath;
            if(path==null)return;
            if(!host.Path || !host.Selection.HasTarget || !host.Selection.Target.Equals(path.Identity) || !ReferenceEquals(host.DisplayPath,path) || !CombatSelection.Valid(path.Identity,host.Session))return;
            bool approximate=(path.Assumptions&(PredictionAssumption.ApproximateMechanism|PredictionAssumption.RandomRepresentative))!=0;
            var color=approximate?new Color(255,210,110):new Color(235,235,255);
            for(int i=host.DisplayAge+1;i<path.Count;i++)
            {
                var point=path[i];var prior=path[i-1];var b=new Vector2(point.Bounds.CenterX,point.Bounds.CenterY);
                if(!point.NewSegment)Line(new Vector2(prior.Bounds.CenterX,prior.Bounds.CenterY),b,color,approximate,1.5f);
                // Milestones refer to absolute simulation time, not a new
                // publication's array origin. Equivalent rolling samples must
                // not move the same future marker every refresh.
                if(point.NewSegment || (path.SampleTick+point.TickOffset)%30==0 || i==path.Count-1)
                {Line(b-new Vector2(3,0),b+new Vector2(3,0),color,false,2);Line(b-new Vector2(0,3),b+new Vector2(0,3),color,false,2);}
            }
        }
        private void Samples(CombatShapeSample[] samples,int kind)
        {
            for(int i=0;i<samples.Length;i++)
            {
                var sample=samples[i];if(sample==null || sample.Session!=host.Session)continue;
                if(kind==2){if(i>=host.Geometry.EventCount || sample.Presented || sample.Retired)continue;}
                else if(sample.Tick!=Main.GameUpdateCount)continue;
                if(kind==0 && i<Main.maxProjectiles)
                {var p=Main.projectile[i];if(p==null || !p.active || !ReferenceEquals(p,sample.Token) || (int)p.key!=sample.Identity || p.type!=sample.Type || p.owner!=sample.Owner)continue;}
                else if(kind==0)
                {var p=Main.player[i-Main.maxProjectiles];if(p==null || !p.active || p.dead || !ReferenceEquals(p,sample.Token) || p.itemAnimation<=0)continue;}
                else if(kind==1)
                {var n=Main.npc[i];if(n==null || !n.active || !ReferenceEquals(n,sample.Token) || n.type!=sample.Type || n.generation!=sample.Identity)continue;}
                else if(kind==3)
                {var p=Main.player[i];if(p==null || !p.active || p.dead || !ReferenceEquals(p,sample.Token))continue;}
                projectedEvent=kind==2?sample:null;
                for(int j=0;j<sample.Count;j++)
                {
                    if(count==strokeLimit){return;}
                    var shape=sample.Shapes[j];var color=colors[shape.Category];
                    bool dashed=shape.Approximate || shape.Condition!=0 || shape.Kind==3 || shape.Kind==4 || shape.HasBounds;
                    if(shape.Kind==5)
                    {
                        // Native lightning consists of discrete point boxes.
                        // Connecting them would invent damage across gaps.
                        var pairBounds=sample.PointBounds;pairBounds.Inflate(sample.PointSize.X/2,sample.PointSize.Y/2);
                        if(!Visible(pairBounds.TopLeft(),pairBounds.BottomRight()))continue;
                        for(int k=0;k<sample.PointCount;k++)
                        {if(count==strokeLimit){return;}var p=sample.Points[k].ToPoint();var box=new Rectangle(p.X-sample.PointSize.X/2,p.Y-sample.PointSize.Y/2,sample.PointSize.X/2*2+1,sample.PointSize.Y/2*2+1);box=Rectangle.Intersect(box,pairBounds);if(box.Width>0 && box.Height>0 && Visible(box.TopLeft(),box.BottomRight()))Box(box.TopLeft(),box.BottomRight(),color,dashed);}
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
                // A projection can stage a receipt, never commit retirement.
                // Reaching this end below the budget proves every shape was
                // considered under the final camera, including crossing lines.
                if(kind==2 && count<strokeLimit)sample.Opportunity=presentation;
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
        {if(!Finite(a) || !Finite(b))return;if(count==strokeLimit){return;}if(projectedEvent!=null)projectedEvent.Presentation=presentation;strokes[count++]=new Stroke{A=Vector2.Transform(a,inverse),B=Vector2.Transform(b,inverse),Color=color,Width=width*inverse.M11};}
        private static bool Finite(Vector2 p){return !float.IsNaN(p.X) && !float.IsNaN(p.Y) && !float.IsInfinity(p.X) && !float.IsInfinity(p.Y);}
        internal static bool Clip(ref Vector2 a,ref Vector2 b)
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
            if(Failed)return true;
            Prediction.AimLightTrace.Presentation("draw-enter",null,0,0,false);
#if JMR_AIM_LIGHT
            int pathIssued=0,pathCompleted=0,otherIssued=0,otherCompleted=0;
            try
            {
#endif
            if(host.Enabled && host.Collision)host.Geometry.PrepareEventsForDraw();
            if(!host.Enabled || !host.CanDraw || !WorldPresentation.CanDraw || Main.spriteBatch==null){Prediction.AimLightTrace.Presentation(!host.Enabled?"draw-disabled":!host.CanDraw?"draw-host-gate":!WorldPresentation.CanDraw?"draw-world-gate":"draw-no-batch",null,0,0,false);return true;}
            if(Main.GameViewMatrix==null)return true;zoom=Main.GameViewMatrix.ZoomMatrix;if(zoom.M11<=0 || zoom.M22<=0)return true;inverse=Matrix.Invert(zoom);
            ProjectPresentation();
            var batch=Main.spriteBatch;var pixel=TextureAssets.MagicPixel.Value;
            Marker.Project(zoom,inverse);Marker.Draw(batch);
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
            Prediction.AimLightTrace.Presentation("draw-complete",null,count-eventEnd,eventEnd,false);
            return true;
#if JMR_AIM_LIGHT
            }
            catch(Exception error){Prediction.AimLightTrace.Fault("draw",error,(long)Main.GameUpdateCount);throw;}
            finally{Prediction.AimLightTrace.DrawCounts(pathIssued,pathCompleted,otherIssued,otherCompleted);}
#endif
        }
    }
}
