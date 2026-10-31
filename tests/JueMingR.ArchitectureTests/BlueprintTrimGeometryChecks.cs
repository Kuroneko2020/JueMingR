using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.Blueprint;

namespace JueMingR.ArchitectureTests
{
    internal static class BlueprintTrimGeometryChecks
    {
        internal static void Check(IList<string> failures)
        {
            Run(failures,"hand-listed union deltas and holes",UnionAndHoles);
            Run(failures,"separate Tile extension and source pose",LayersAndPose);
            Run(failures,"visible candidates and real edges",VisibleWork);
            Run(failures,"pending ownership and cleanup",Ownership);
        }
        private static void Run(IList<string> failures,string name,Action action)
        { try { action(); } catch(Exception error) { failures.Add("BlueprintTrimGeometry "+name+": "+error); } }
        private static void Require(bool condition,string message)
        { if(!condition)throw new InvalidOperationException(message); }
        private static string Key(BlueprintSpan span)
        { return span.Y+":"+span.Left+":"+span.RightExclusive; }
        private static string[] Spans(IEnumerable<BlueprintSpan> spans)
        { return spans.Select(Key).OrderBy(value=>value,StringComparer.Ordinal).ToArray(); }
        private static void Expect(IEnumerable<BlueprintSpan> actual,params string[] expected)
        { Require(Spans(actual).SequenceEqual(expected.OrderBy(value=>value,StringComparer.Ordinal)),"independent row spans: "+string.Join(",",expected)); }

        private static void UnionAndHoles()
        {
            var manual=new BlueprintTrimGeometry();
            var first=manual.AddRectangle(new BlueprintProjectionBounds(1,1,3,2));
            Expect(first,"1:1:4","2:1:4");
            Expect(manual.AddRectangle(new BlueprintProjectionBounds(3,2,3,2)),"2:4:6","3:3:6");
            Expect(manual.EnumerateSpans(),"1:1:4","2:1:6","3:3:6");
            Require(manual.AddRectangle(new BlueprintProjectionBounds(1,1,3,2)).Count==0,"repeated rectangle is idempotent union, not XOR");
            Expect(first,"1:1:4","2:1:4");
            Require(manual.AddRectangle(new BlueprintProjectionBounds(30,30,0,4)).Count==0 && manual.Contains(1,1),"empty later frame retains prior selection");
            manual.AddRectangle(new BlueprintProjectionBounds(10,1,1,1));
            Require(!manual.Contains(7,1) && manual.Contains(10,1),"separate rectangles retain the unselected gap");
            Require(manual.AddRectangle(BlueprintProjectionView.InclusiveRectangle(-8,-8,-1,-1,12,8)).Count==0,"outside endpoints do not invent a clamped tile");

            manual.Clear();
            manual.AddRectangle(new BlueprintProjectionBounds(0,0,6,1));
            manual.AddRectangle(new BlueprintProjectionBounds(0,4,6,1));
            manual.AddRectangle(new BlueprintProjectionBounds(0,1,1,3));
            manual.AddRectangle(new BlueprintProjectionBounds(5,1,1,3));
            Expect(manual.EnumerateSpans(),"0:0:6","1:0:1","1:5:6","2:0:1","2:5:6","3:0:1","3:5:6","4:0:6");
            // Literal occupancy is the independent oracle. It knows no row
            // merging or boundary-index implementation and retains the hole.
            ExpectEdges(manual,new[]{"######","#....#","#....#","#....#","######"});
            manual.AddRectangle(new BlueprintProjectionBounds(1,1,2,3));
            ExpectEdges(manual,new[]{"######","###..#","###..#","###..#","######"});
            manual.AddRectangle(new BlueprintProjectionBounds(3,1,2,3));
            ExpectEdges(manual,new[]{"######","######","######","######","######"});
        }

        private static void LayersAndPose()
        {
            var manual=new BlueprintTrimGeometry();var objects=new BlueprintTrimGeometry(true);
            manual.AddRectangle(new BlueprintProjectionBounds(0,0,1,1));
            objects.AddRectangle(new BlueprintProjectionBounds(0,0,4,2));
            Expect(manual.AddRectangle(new BlueprintProjectionBounds(3,0,1,1)),"0:3:4");
            Require(!manual.Contains(2,0) && objects.Contains(2,0),"Tile-only expansion cannot swallow a later all-layer wall/wire selection");
            var originalEdges=EdgeUnits(objects.EnumerateVisibleEdges(-1,-1,6,4));
            Require(objects.AddRectangle(new BlueprintProjectionBounds(0,0,4,2)).Count==0 && originalEdges.SetEquals(EdgeUnits(objects.EnumerateVisibleEdges(-1,-1,6,4))),"duplicate object footprint neither duplicates fill nor adds brighter same-side edges");
            objects.AddRectangle(new BlueprintProjectionBounds(1,0,1,2));
            var nestedEdges=EdgeUnits(objects.EnumerateVisibleEdges(-1,-1,6,4));
            Require(nestedEdges.Contains("Left:1:0") && nestedEdges.Contains("Right:2:1"),"verified object real internal sides survive union fill coverage");

            var pose=new BlueprintProjectionView(new CaptureSummary{Left=30,Top=40,Width=7,Height=4},true,null);
            var display=new BlueprintTrimGeometry();var source=new BlueprintTrimGeometry();
            var a=display.AddRectangle(new BlueprintProjectionBounds(0,1,2,1));
            var b=display.AddRectangle(new BlueprintProjectionBounds(4,0,1,1));
            foreach(BlueprintSpan span in a.Concat(b))
            {
                var converted=pose.SourceBounds(new BlueprintProjectionBounds(span.Left,span.Y,span.RightExclusive-span.Left,1));
                source.AddSpans(new[]{new BlueprintSpan(converted.Top,converted.Left,converted.Right)});
            }
            Expect(source.EnumerateSpans(),"0:2:3","1:5:7");
            Require(!source.Contains(30,40) && !source.Contains(4,0),"source geometry is normalized once, without saved offset or a second reflection");
        }

        private static void VisibleWork()
        {
            var objects=new BlueprintTrimGeometry(true);
            objects.AddRectangle(new BlueprintProjectionBounds(1,1,3,3));
            string[] fill=Spans(objects.EnumerateVisible(0,0,6,6));
            var edges=EdgeUnits(objects.EnumerateVisibleEdges(0,0,6,6));
            for(int i=0;i<2048;i++)
            {
                objects.AddRectangle(new BlueprintProjectionBounds(1000+i*4,1,1,3));
                objects.AddRectangle(new BlueprintProjectionBounds(1,1000+i*4,3,1));
            }
            Require(fill.SequenceEqual(Spans(objects.EnumerateVisible(0,0,6,6))) && edges.SetEquals(EdgeUnits(objects.EnumerateVisibleEdges(0,0,6,6))),"thousands of offscreen object footprints preserve exactly the fixed viewport output");
#if DEBUG
            Require(objects.DebugVisibleRows==6 && objects.DebugVisibleSpans==3 && objects.DebugVisibleSearchComparisons<=39,"fill queries only viewport rows, then logarithmic first overlap");
            Require(objects.DebugVisibleEdgeRows==13 && objects.DebugVisibleEdges==8 && objects.DebugVisibleEdgeSearchComparisons<=112,"real-edge queries only viewport lines/candidates, not historical objects");
#else
            Require(typeof(BlueprintTrimGeometry).GetProperty("DebugVisibleRows")==null,"normal Release excludes workload probes");
#endif
            Expect(objects.EnumerateVisible(1000,1,1001,4),"1:1000:1001","2:1000:1001","3:1000:1001");
            var huge=new BlueprintTrimGeometry(true);huge.AddRectangle(new BlueprintProjectionBounds(0,0,1000,1000));
            Require(!huge.EnumerateVisibleEdges(200,200,210,210).Any(),"viewport wholly inside real bounds creates no artificial clipped sides");
            var border=new BlueprintTrimGeometry(true);border.AddRectangle(new BlueprintProjectionBounds(0,0,10,10));
            var atBoundary=border.EnumerateVisibleEdges(0,0,10,10).ToArray();
            Require(atBoundary.Any(edge=>edge.Side==BlueprintTrimEdgeSide.Bottom && edge.Fixed==10) && atBoundary.Any(edge=>edge.Side==BlueprintTrimEdgeSide.Right && edge.Fixed==10),"real bottom/right at viewport edge still emit inside-boundary ink");
        }

        private static void Ownership()
        {
            var manual=new BlueprintTrimGeometry();var pending=new BlueprintTrimGeometry();
            pending.AddSpans(manual.AddRectangle(new BlueprintProjectionBounds(1,1,2,1)));
            var offered=pending;
            // A busy offer leaves this exact container owned by Host. After
            // acceptance Host replaces it; only the accepted reader keeps it.
            Require(ReferenceEquals(offered,pending) && offered.Contains(1,1),"offering a reference does not consume pending");
            pending=new BlueprintTrimGeometry();pending.AddSpans(manual.AddRectangle(new BlueprintProjectionBounds(8,1,1,1)));
            Expect(offered.EnumerateSpans(),"1:1:3");Expect(pending.EnumerateSpans(),"1:8:9");
            pending.Clear();manual.Clear();
            Expect(offered.EnumerateSpans(),"1:1:3");
            offered.Clear();
            Require(!manual.HasContent && !pending.HasContent && !offered.HasContent && !offered.EnumerateVisibleEdges(0,0,20,20).Any(),"operation owner cleanup removes all fill/edge indexes without aliasing another batch");
#if DEBUG
            Require(manual.DebugStoredRows==0 && pending.DebugStoredRows==0 && offered.DebugStoredRows==0,"cleanup retires numeric row ownership");
#endif
        }

        private static HashSet<string> EdgeUnits(IEnumerable<BlueprintTrimEdge> edges)
        {
            var units=new HashSet<string>(StringComparer.Ordinal);
            foreach(BlueprintTrimEdge edge in edges)
                for(int value=edge.Start;value<edge.EndExclusive;value++)
                    Require(units.Add(edge.Side+":"+edge.Fixed+":"+value),"same directed edge unit is emitted once");
            return units;
        }
        private static void ExpectEdges(BlueprintTrimGeometry geometry,string[] cells)
        {
            var expected=new HashSet<string>(StringComparer.Ordinal);int height=cells.Length,width=cells[0].Length;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(cells[y][x]=='#')
            {
                if(y==0 || cells[y-1][x]!='#')expected.Add("Top:"+y+":"+x);
                if(y==height-1 || cells[y+1][x]!='#')expected.Add("Bottom:"+(y+1)+":"+x);
                if(x==0 || cells[y][x-1]!='#')expected.Add("Left:"+x+":"+y);
                if(x==width-1 || cells[y][x+1]!='#')expected.Add("Right:"+(x+1)+":"+y);
            }
            Require(expected.SetEquals(EdgeUnits(geometry.EnumerateVisibleEdges(-1,-1,width+1,height+1))),"all union/hole boundaries agree with independent literal tile occupancy");
        }
    }
}
