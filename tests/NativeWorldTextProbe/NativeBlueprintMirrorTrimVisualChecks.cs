using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Blueprint;
using JueMingR.Platform.WorldTargets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Actual WorldLayer/renderer on the existing hidden XNA device and original
    // XNB assets. Controlled business pointers do not certify OS input or Main.Run.
    internal static class NativeBlueprintMirrorTrimVisualChecks
    {
        private const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static void Run(object context,ProbeGraphics graphics,string output)
        {
            var host=Get(context,"Blueprint");var input=Get(context,"Input");var library=(LibraryState)Get(host,"Library");
            var camera=Main.screenPosition;int sw=Main.screenWidth,sh=Main.screenHeight;var zoom=Main.GameViewMatrix.Zoom;
            float gravity=Main.LocalPlayer.gravDir,ui=Main.UIScale;bool automatic=(bool)Get(host,"AutoBuildEnabled");
            Call(Get(context,"Shell"),"CloseAndSubmitPosition");Call(host,"CancelCaptureOrDraft");Call(host,"ClearProjection");
            ((BlueprintCaptureState)Get(host,"State")).Exit();Call(host,"SetAutoBuild",false);
            Action step=()=>{NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());NativeQuickItemChecks.BeginWorldStep();Call(context,"UpdateRuntime");Call(host,"Poll");};
            try
            {
                using(NativeBlueprintWorldMatchChecks.EmptyWorld(host,512,160))
                {
                    NativeBlueprintWorldMatchChecks.InitializeMetadata();Framing.Initialize();Main.SetupTileMerge();
                    Main.screenWidth=960;Main.screenHeight=640;Main.LocalPlayer.gravDir=1;Main.UIScale=1;
                    Main.GameViewMatrix.SetViewportOverride(new Viewport(0,0,960,640));Main.GameViewMatrix.Zoom=Vector2.One;
                    graphics.LoadTexture("Tile","Images/Tiles_1",1);graphics.LoadTexture("Wall","Images/Wall_7",7);
                    var cells=new[]{new CaptureCell{X=0,Y=0,Layers=CaptureLayer.Tile,TileType=1},new CaptureCell{X=4,Y=2,Layers=CaptureLayer.Tile,TileType=1}};
                    var entry=NativeBlueprintBudgetChecks.Save(library,cells,5,3);
                    var target=NativeBlueprintWorldMatchChecks.Place(host,input,entry,100,40,step);
                    Main.screenPosition=new Vector2(100*16-160,40*16-160);
                    Action draw=()=>{Call(Get(context,"worldTiles"),"BeginTick");Call(Get(host,"World"),"Draw");};
                    NativeQuickItemChecks.Until(()=>{drawTo(graphics,draw);step();return GetOptional(target,"Window")!=null;});
                    var ordinary=graphics.Pixels(draw,Matrix.Identity);
                    Require(Cyan(ordinary[184*960+160]) && Cyan(ordinary[207*960+184]),"fixed original left/bottom edges persist without hover or active operation");
                    Require(WorldGen.PlaceTile(100,40,1,true,true),"original native partial construction");
                    NativeQuickItemChecks.Until(()=>{step();var match=GetOptional(target,"Match");return match!=null && (long)Get(match,"Matches")>0;});
                    var hidden=graphics.Pixels(draw,Matrix.Identity);
                    Require(hidden[165*960+165].A==0 && Cyan(hidden[184*960+160]),"partial real Match hides its Tile while original bbox edge remains");
                    graphics.Image(Path.Combine(output,"mirror-trim-fixed-partial-match-frame.png"),draw,Matrix.Identity);
                    long blocks=library.DebugProjectionStatistics.BlockReads,builds=(long)Get(Get(target,"Renderer"),"BuildCount");
                    for(int i=0;i<4;i++)graphics.Pixels(draw,Matrix.Identity);
                    Require(library.DebugProjectionStatistics.BlockReads==blocks && (long)Get(Get(target,"Renderer"),"BuildCount")==builds,"four stable fixed Draws neither reread source nor rebuild commands");
                    var outline=Get(host,"World").GetType().GetMethod("Outline",F);
                    var clipped=graphics.Pixels(()=>outline.Invoke(null,new object[]{80,30,200,80,new WorldTargetView(100,40,50,40,0),Color.White}),Matrix.Identity);
                    Require(clipped.All(p=>p.A==0),"view fully inside a large bbox does not invent view-border outline edges");
                    Call(host,"ClearProjection");
                    PixelMirror(context,graphics,output);
                    TrimExtension(context,host,input,library,graphics,output,step);
                    TrimGeometryPixels(host,graphics,output);
                    Call(host,"ClearProjection");long emptyBlocks=library.DebugProjectionStatistics.BlockReads;
                    for(int i=0;i<4;i++)Require(graphics.Pixels(draw,Matrix.Identity).All(p=>p.A==0),"last instance has no residual world pixels");
                    Require(library.DebugProjectionStatistics.BlockReads==emptyBlocks,"no instance Draw has no added source I/O");
                }
                Console.WriteLine("PASS Blueprint mirror/trim GPU: persistent real bbox edges including partial native Match, no invented clipped edges, original XNB whole-pixel reflection with furniture offsets/Centered pylon, cumulative separated manual regions and complete-object red trim extension, deduplicated two-layer fill/real object edges, four stable Draws and last-instance zero work. Hidden isolated XNA device only; no normal game/OS input/FPS/multiplayer/owner acceptance.");
            }
            finally
            {
                Call(host,"ClearProjection");Main.screenPosition=camera;Main.screenWidth=sw;Main.screenHeight=sh;
                Main.GameViewMatrix.Zoom=zoom;Main.LocalPlayer.gravDir=gravity;Main.UIScale=ui;Call(host,"SetAutoBuild",automatic);
            }
        }
        private static void PixelMirror(object context,ProbeGraphics graphics,string output)
        {
            var host=Get(context,"Blueprint");var sample=NativeBlueprintProjectionChecks.MixedWindow();
            foreach(int id in sample.Cells.Where(c=>(c.Layers&CaptureLayer.Tile)!=0).Select(c=>(int)c.TileType).Distinct())graphics.LoadTexture("Tile","Images/Tiles_"+id,id);
            graphics.LoadTexture("Extra","Images/Extra_181",181);
            var type=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintProjectionRenderer",true);
            var renderer=Activator.CreateInstance(type,true);
            try
            {
                Call(renderer,"SetWindow",sample);Main.screenPosition=new Vector2(40*16-160,40*16-160);
                Action normal=()=>Call(renderer,"Draw",40,40,12,false),mirrored=()=>Call(renderer,"Draw",40,40,12,true);
                var source=graphics.Pixels(normal,Matrix.Identity);var actual=graphics.Pixels(mirrored,Matrix.Identity);
                int compared=0,equal=0;
                // Independent readback permutation: the literal 12-tile source
                // bbox starts at pixel160, so x and 511-x are opposite samples.
                for(int y=0;y<640;y++)for(int x=0;x<512;x++)
                {var a=source[y*960+x];var b=actual[y*960+511-x];if(a.A!=0 || b.A!=0){compared++;if(a==b)equal++;}}
                Require(compared>500 && equal==compared,"all occupied original XNB pixels reflect about the literal full 12-tile axis, including native offsets and texture-sized centered body; equal="+equal+"/"+compared);
                Require((long)Get(renderer,"BuildCount")==1,"pose draw does not rebuild source command geometry");
                graphics.Image(Path.Combine(output,"mirror-trim-source-pixels.png"),normal,Matrix.Identity);
                graphics.Image(Path.Combine(output,"mirror-trim-reflected-pixels.png"),mirrored,Matrix.Identity);
            }
            finally{((IDisposable)renderer).Dispose();}
        }
        private static void TrimExtension(object context,object host,object input,LibraryState library,ProbeGraphics graphics,string output,Action step)
        {
            Require(TileObject.Place(new TileObject{xCoord=20,yCoord=20,type=79,style=0,alternate=0,random=0}),"original bed raw writer seeds complete reliable capture fixture");
            var entry=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,library,step,20,20,4,2,"红色整件预览");
            var target=NativeBlueprintWorldMatchChecks.Place(host,input,entry,100,40,step);
            Main.screenPosition=new Vector2(100*16-160,40*16-160);graphics.LoadTexture("Tile","Images/Tiles_79",79);
            Action draw=()=>{Call(Get(context,"worldTiles"),"BeginTick");Call(Get(host,"World"),"Draw");};
            NativeQuickItemChecks.Until(()=>{drawTo(graphics,draw);step();return GetOptional(target,"Window")!=null;});
            NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());Call(host,"TrimProjection");Pointer(host,false,100,40);Pointer(host,true,100,40);
            NativeQuickItemChecks.Until(()=>{step();return Get(host,"ProjectionMode").ToString()=="TrimReady";});
            Pointer(host,false,100,40);Pointer(host,true,100,40);Pointer(host,false,100,40);
            NativeQuickItemChecks.Until(()=>{step();return (bool)Get(host,"CanInvokeConfirmTrim");});
            var plan=(BlueprintProjectionTrimPlan)Get(host,"TrimPlan");Require(plan.ObjectBounds.Count==1 && plan.RemovedLayerCount==8,"one selected bed member expands complete 4x2 Tile only in preview");
            var manual=(BlueprintTrimGeometry)Get(host,"TrimManualGeometry");var objects=(BlueprintTrimGeometry)Get(host,"TrimObjectGeometry");
            Require(manual.Contains(0,0) && !manual.Contains(3,1) && objects.Contains(3,1),"manual all-layer selection is independent of the complete bed Tile-only extension");
            var red=graphics.Pixels(draw,Matrix.Identity);
            Require(Red(red[191*960+215]) && Red(red[184*960+223]),"red preview reaches the real bottom/right of the complete bed beyond selected one-cell rectangle");
            Require(((BlueprintProjectionMask)Get(target,"Mask")).RemovedLayerCount==0,"red actual Draw has not committed the preview");
            graphics.Image(Path.Combine(output,"mirror-trim-complete-bed-red-preview.png"),draw,Matrix.Identity);
            Pointer(host,true,103,41);Pointer(host,false,103,41);
            Require(manual.Contains(0,0) && manual.Contains(3,1) && !manual.Contains(2,0),"next completed drag is immediately visible as a separate region while prior geometry stays owned");
            NativeQuickItemChecks.Until(()=>{step();return (bool)Get(host,"CanInvokeConfirmTrim");});
            Require(((BlueprintProjectionTrimPlan)Get(host,"TrimPlan")).RemovedLayerCount==8 && objects.EnumerateVisible(0,0,4,2).Sum(span=>span.RightExclusive-span.Left)==8,"later direct selection inside an expanded object preserves cumulative count and deduplicated Tile preview");
            var cumulative=graphics.Pixels(draw,Matrix.Identity);
            Require(Red(cumulative[164*960+164]) && Red(cumulative[184*960+215]),"both completed red regions remain visible on the actual WorldLayer");
            graphics.Image(Path.Combine(output,"mirror-trim-cumulative-bed-red-preview.png"),draw,Matrix.Identity);
            Call(host,"CancelProjection");var cancelled=graphics.Pixels(draw,Matrix.Identity);
            Require(!Red(cancelled[191*960+215]) && Cyan(cancelled[184*960+223]),"cancel removes red extension and returns unchanged original cyan bbox");
            var cancelledManual=(BlueprintTrimGeometry)GetOptional(host,"TrimManualGeometry");var cancelledObjects=(BlueprintTrimGeometry)GetOptional(host,"TrimObjectGeometry");
            Require((cancelledManual==null || !cancelledManual.HasContent) && (cancelledObjects==null || !cancelledObjects.HasContent),"cancel releases both numeric preview layers from the ended Host operation");
        }
        private static void TrimGeometryPixels(object host,ProbeGraphics graphics,string output)
        {
            var method=Get(host,"World").GetType().GetMethod("DrawTrimGeometry",F);
            var viewport=new WorldTargetView(100,40,20,20,0);
            var large=new BlueprintTrimGeometry(true);large.AddRectangle(new BlueprintProjectionBounds(-100,-100,300,300));
            var clipped=graphics.Pixels(()=>method.Invoke(null,new object[]{large,100,40,viewport,Color.Transparent,Color.White}),Matrix.Identity);
            Require(clipped.All(pixel=>pixel.A==0),"indexed preview edges do not invent viewport outlines when all real object sides are outside");

            var manual=new BlueprintTrimGeometry();manual.AddRectangle(new BlueprintProjectionBounds(0,0,1,1));manual.AddRectangle(new BlueprintProjectionBounds(4,0,1,1));
            var objects=new BlueprintTrimGeometry(true);objects.AddRectangle(new BlueprintProjectionBounds(0,0,3,2));objects.AddRectangle(new BlueprintProjectionBounds(1,0,1,2));
            Action draw=()=>
            {
                method.Invoke(null,new object[]{manual,100,40,viewport,Color.FromNonPremultiplied(255,55,65,65),Color.FromNonPremultiplied(255,65,75,220)});
                method.Invoke(null,new object[]{objects,100,40,viewport,Color.FromNonPremultiplied(255,55,65,90),Color.FromNonPremultiplied(255,75,85,230)});
            };
            var layered=graphics.Pixels(draw,Matrix.Identity);
            Require(Red(layered[168*960+232]) && layered[168*960+216].A==0,"separated completed manual areas leave their literal unselected gap transparent");
            Require(layered[184*960+200].A>layered[168*960+232].A && layered[184*960+176].A>layered[184*960+184].A,"object and manual fills retain distinct weights and nested real object sides remain visible");
            manual.AddRectangle(new BlueprintProjectionBounds(4,0,1,1));objects.AddRectangle(new BlueprintProjectionBounds(0,0,3,2));
            Require(layered.SequenceEqual(graphics.Pixels(draw,Matrix.Identity)),"duplicate rectangles/objects do not brighten any rendered preview pixel");
            graphics.Image(Path.Combine(output,"mirror-trim-layered-indexed-geometry.png"),draw,Matrix.Identity);
        }
        private static void drawTo(ProbeGraphics graphics,Action draw){graphics.Pixels(draw,Matrix.Identity);}
        private static void Pointer(object host,bool held,int x,int y){Call(host,"AcceptWorldPointer",true,held,false,x,y,Main.maxTilesX,Main.maxTilesY);}
        private static bool Cyan(Color value){return value.A>0 && value.B>value.R && value.G>value.R;}
        private static bool Red(Color value){return value.A>0 && value.R>value.G*1.5 && value.R>value.B*1.5;}
    }
}
