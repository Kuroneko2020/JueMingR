using System;
using System.Collections.Generic;

namespace JueMingR.Features.Blueprint
{
    // Frozen numeric broad phase for one qualified Sources sequence. It owns
    // no cells, world state, material eligibility or file lifetime. The worker
    // transfers its sole node array only after every leaf/parent is complete.
    public sealed class BlueprintProjectionGeometryDirectory
    {
        public readonly struct Node
        {
            internal Node(BlueprintProjectionBounds bounds,int left,int right,int group)
            { Bounds=bounds;Left=left;Right=right;Group=group; }
            public BlueprintProjectionBounds Bounds { get; }
            public int Left { get; }
            public int Right { get; }
            public int Group { get; }
        }
        private readonly Node[] nodes;
        private BlueprintProjectionGeometryDirectory(Node[] nodes,IReadOnlyList<BlueprintProjectionSourceRange> sources)
        { this.nodes=nodes;Sources=sources; }
        public IReadOnlyList<BlueprintProjectionSourceRange> Sources { get; }
        public int NodeCount { get { return nodes.Length; } }
        public long Bytes { get { return 72L+nodes.LongLength*32L; } }
        public Node GetNode(int index) { return nodes[index]; }

        internal sealed class Builder
        {
            private readonly IReadOnlyList<BlueprintProjectionSourceRange> ranges;
            private Node[] nodes;
            private int nextLeaf,nextParent;
            internal Builder(IReadOnlyList<BlueprintProjectionSourceRange> ranges)
            {
                this.ranges=ranges??throw new ArgumentNullException(nameof(ranges));
                nodes=new Node[checked(Math.Max(0,ranges.Count*2-1))];nextParent=ranges.Count-2;
            }
            internal bool Ready { get { return nextLeaf==ranges.Count && nextParent<0; } }
            internal long Bytes { get { return 64L+(nodes==null?0:24L+nodes.LongLength*32L); } }
            // Physical ordinals retain the original balanced heap layout,
            // including non-powers of two. One leaf or union is one work step;
            // there is no sort, synchronous completion or second node buffer.
            internal int Advance(int budget)
            {
                if(nodes==null)throw new InvalidOperationException("blueprint-directory-already-frozen");
                int steps=0;
                while(steps<budget && !Ready)
                {
                    if(nextLeaf<ranges.Count)
                    {
                        int group=nextLeaf++;
                        nodes[ranges.Count-1+group]=new Node(ranges[group].Bounds,-1,-1,group);
                    }
                    else
                    {
                        int parent=nextParent--,left=parent*2+1,right=left+1;
                        var a=nodes[left].Bounds;var b=nodes[right].Bounds;
                        int x=Math.Min(a.Left,b.Left),y=Math.Min(a.Top,b.Top);
                        nodes[parent]=new Node(new BlueprintProjectionBounds(x,y,Math.Max(a.Right,b.Right)-x,Math.Max(a.Bottom,b.Bottom)-y),left,right,-1);
                    }
                    steps++;
                }
                return steps;
            }
            internal BlueprintProjectionGeometryDirectory Freeze(IReadOnlyList<BlueprintProjectionSourceRange> sources)
            {
                if(!Ready || nodes==null || sources==null || sources.Count!=ranges.Count)
                    throw new InvalidOperationException("blueprint-directory-not-complete");
                var result=new BlueprintProjectionGeometryDirectory(nodes,sources);nodes=null;return result;
            }
        }
    }
}
