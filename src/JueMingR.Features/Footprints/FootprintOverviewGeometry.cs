using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using JueMingR.Platform.Footprints;

namespace JueMingR.Features.Footprints
{
    // A derived immutable page. The tree keeps all recorded leaves: geometric
    // chords are only selected after their entire time interval has happened.
    internal sealed class FootprintOverviewGeometry
    {
        internal struct Node
        {
            internal float X1, Y1, X2, Y2, Error;
            internal long Start, End, Segment, First, Last;
            internal int Left, Right;
        }
        private struct Work { internal int Node, First, End; }
        internal readonly Node[] Nodes;
        internal readonly int[] Roots;
        internal readonly float MinX, MinY, MaxX, MaxY;
        internal readonly long End, LastSequence;
        internal readonly byte[] SourceIdentity;
        internal long Bytes { get { return 128L + Nodes.Length * 88L + Roots.Length * 4L; } }
        private FootprintOverviewGeometry(Node[] nodes, int[] roots, float minX, float minY, float maxX, float maxY, long end, long last, byte[] sourceIdentity)
        { Nodes = nodes; Roots = roots; MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY; End = end; LastSequence = last; SourceIdentity = sourceIdentity; }

        internal static FootprintOverviewGeometry Build(FootprintSample[] samples)
        {
            var nodes = new List<Node>(); var roots = new List<int>();
            float minX = Single.PositiveInfinity, minY = Single.PositiveInfinity, maxX = Single.NegativeInfinity, maxY = Single.NegativeInfinity;
            for (int first = 0; first < samples.Length;)
            {
                if (!samples[first].HasPosition) { first++; continue; }
                int end = first + 1;
                while (end < samples.Length && samples[end].Follows(samples[end - 1])) end++;
                int root = nodes.Count; nodes.Add(new Node()); roots.Add(root);
                var work = new Stack<Work>(); work.Push(new Work { Node = root, First = first, End = end });
                while (work.Count != 0)
                {
                    Work part = work.Pop(); FootprintSample a = samples[part.First], b = samples[part.End - 1];
                    var node = new Node { X1 = a.FromX, Y1 = a.FromY, X2 = b.X, Y2 = b.Y, Start = a.Start, End = b.End,
                        Segment = a.Segment, First = a.Sequence, Last = b.Sequence, Left = -1, Right = -1 };
                    if (part.End - part.First > 1)
                    {
                        double largest = -1; int split = (part.First + part.End) / 2;
                        for (int i = part.First; i < part.End - 1; i++)
                        {
                            double distance = DistanceSquared(samples[i].X, samples[i].Y, a.FromX, a.FromY, b.X, b.Y);
                            if (distance > largest) { largest = distance; split = i + 1; }
                        }
                        // Balanced collinear runs prevent a long straight history
                        // from degenerating into a linear-depth selection tree.
                        if (largest <= 0) split = (part.First + part.End) / 2;
                        node.Error = (float)Math.Sqrt(Math.Max(0, largest));
                        node.Left = nodes.Count; nodes.Add(new Node()); node.Right = nodes.Count; nodes.Add(new Node());
                        work.Push(new Work { Node = node.Right, First = split, End = part.End });
                        work.Push(new Work { Node = node.Left, First = part.First, End = split });
                    }
                    nodes[part.Node] = node;
                }
                first = end;
            }
            for (int i = 0; i < samples.Length; i++)
            {
                var point = samples[i]; if (!point.HasPosition) continue;
                minX = Math.Min(minX, Math.Min(point.FromX, point.X)); minY = Math.Min(minY, Math.Min(point.FromY, point.Y));
                maxX = Math.Max(maxX, Math.Max(point.FromX, point.X)); maxY = Math.Max(maxY, Math.Max(point.FromY, point.Y));
            }
            return new FootprintOverviewGeometry(nodes.ToArray(), roots.ToArray(), minX, minY, maxX, maxY,
                samples.Length == 0 ? 0 : samples[samples.Length - 1].End, samples.Length == 0 ? 0 : samples[samples.Length - 1].Sequence, Fingerprint(samples));
        }

        internal void Select(long until, float epsilon, Action<FootprintOverviewEdge> emit)
        {
            var work = new Stack<int>(); for (int i = Roots.Length - 1; i >= 0; i--) work.Push(Roots[i]);
            while (work.Count != 0)
            {
                Node node = Nodes[work.Pop()]; if (node.Start > until) continue;
                // A closed moving loop still owns visible coverage, even when
                // its low-zoom chord would be a point. Pure stays remain empty.
                bool closedMovement = node.X1 == node.X2 && node.Y1 == node.Y2 && node.Error > 0;
                if (node.Left >= 0 && (node.End > until || node.Error > epsilon || closedMovement))
                { work.Push(node.Right); work.Push(node.Left); continue; }
                float x = node.X2, y = node.Y2; long end = node.End;
                if (end > until)
                {
                    // Only original time-aware leaves may interpolate. A DP
                    // chord has a geometric bound, not a timing guarantee.
                    double fraction = node.End == node.Start ? 1 : (double)(until - node.Start) / (node.End - node.Start);
                    x = (float)(node.X1 + (node.X2 - node.X1) * fraction); y = (float)(node.Y1 + (node.Y2 - node.Y1) * fraction); end = until;
                }
                if (node.X1 != x || node.Y1 != y) emit(new FootprintOverviewEdge(node.X1, node.Y1, x, y, node.Start, end, node.Segment, node.First, node.Last));
            }
        }

        internal byte[] Encode(string generation, long number)
        {
            byte[] body;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(0x324F4D4A); writer.Write(2); writer.Write(generation); writer.Write(number); writer.Write(SourceIdentity);
                writer.Write(MinX); writer.Write(MinY); writer.Write(MaxX); writer.Write(MaxY); writer.Write(End); writer.Write(LastSequence);
                writer.Write(Roots.Length); foreach (int root in Roots) writer.Write(root);
                writer.Write(Nodes.Length);
                foreach (Node node in Nodes)
                {
                    writer.Write(node.X1); writer.Write(node.Y1); writer.Write(node.X2); writer.Write(node.Y2); writer.Write(node.Error);
                    writer.Write(node.Start); writer.Write(node.End); writer.Write(node.Segment); writer.Write(node.First); writer.Write(node.Last);
                    writer.Write(node.Left); writer.Write(node.Right);
                }
                writer.Flush(); body = stream.ToArray();
            }
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(body), result = new byte[body.Length + hash.Length];
                Buffer.BlockCopy(body, 0, result, 0, body.Length); Buffer.BlockCopy(hash, 0, result, body.Length, hash.Length); return result;
            }
        }

        internal static FootprintOverviewGeometry Decode(byte[] bytes, string generation, long number)
        {
            if (bytes == null || bytes.Length < 80 || bytes.Length > 65536) throw Invalid();
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes, 0, bytes.Length - 32);
                for (int i = 0; i < hash.Length; i++) if (hash[i] != bytes[bytes.Length - 32 + i]) throw Invalid();
            }
            using (var stream = new MemoryStream(bytes, 0, bytes.Length - 32, false))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadInt32() != 0x324F4D4A || reader.ReadInt32() != 2 || reader.ReadString() != generation || reader.ReadInt64() != number) throw Invalid();
                byte[] sourceIdentity = reader.ReadBytes(32); if (sourceIdentity.Length != 32) throw Invalid();
                float minX = reader.ReadSingle(), minY = reader.ReadSingle(), maxX = reader.ReadSingle(), maxY = reader.ReadSingle();
                long end = reader.ReadInt64(), last = reader.ReadInt64();
                int count = reader.ReadInt32(); if (count < 0 || count > 256) throw Invalid();
                var roots = new int[count]; for (int i = 0; i < count; i++) roots[i] = reader.ReadInt32();
                count = reader.ReadInt32(); if (count < 0 || count > 512) throw Invalid();
                var nodes = new Node[count];
                for (int i = 0; i < count; i++)
                {
                    var node = new Node { X1 = reader.ReadSingle(), Y1 = reader.ReadSingle(), X2 = reader.ReadSingle(), Y2 = reader.ReadSingle(), Error = reader.ReadSingle(),
                        Start = reader.ReadInt64(), End = reader.ReadInt64(), Segment = reader.ReadInt64(), First = reader.ReadInt64(), Last = reader.ReadInt64(), Left = reader.ReadInt32(), Right = reader.ReadInt32() };
                    if (!FootprintSample.Finite(node.X1) || !FootprintSample.Finite(node.Y1) || !FootprintSample.Finite(node.X2) || !FootprintSample.Finite(node.Y2) ||
                        !FootprintSample.Finite(node.Error) || node.Error < 0 || node.Start < 0 || node.End < node.Start || node.First <= 0 || node.Last < node.First ||
                        (node.Left == -1 ? node.Right != -1 : node.Left <= i || node.Right <= i || node.Left >= count || node.Right >= count)) throw Invalid();
                    nodes[i] = node;
                }
                foreach (int root in roots) if (root < 0 || root >= count) throw Invalid();
                if (stream.Position != stream.Length || end < 0 || last <= 0 || last != (number + 1) * 256) throw Invalid();
                return new FootprintOverviewGeometry(nodes, roots, minX, minY, maxX, maxY, end, last, sourceIdentity);
            }
        }

        private static InvalidDataException Invalid() { return new InvalidDataException("footprint-overview-cache"); }
        internal bool MatchesSource(FootprintSample[] samples)
        {
            byte[] identity = Fingerprint(samples); for (int i = 0; i < identity.Length; i++) if (identity[i] != SourceIdentity[i]) return false; return true;
        }
        private static byte[] Fingerprint(FootprintSample[] samples)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            using (var sha = SHA256.Create())
            {
                foreach (var sample in samples)
                {
                    writer.Write(sample.Sequence); writer.Write(sample.Start); writer.Write(sample.End); writer.Write(sample.Segment);
                    writer.Write(sample.FromX); writer.Write(sample.FromY); writer.Write(sample.X); writer.Write(sample.Y); writer.Write((byte)sample.Position);
                }
                writer.Flush(); return sha.ComputeHash(stream.ToArray());
            }
        }
        private static double DistanceSquared(float x, float y, float x1, float y1, float x2, float y2)
        {
            double dx = x2 - x1, dy = y2 - y1, length = dx * dx + dy * dy;
            double fraction = length == 0 ? 0 : Math.Max(0, Math.Min(1, ((x - x1) * dx + (y - y1) * dy) / length));
            double ex = x - x1 - fraction * dx, ey = y - y1 - fraction * dy; return ex * ex + ey * ey;
        }
    }
}
