using System;
using System.Collections.Generic;
using System.IO;
using JueMingR.Platform.DeathHistory;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.DeathHistory
{
    // Death-only rank index: leaves contain token/header references, never
    // reasons. No mutable sibling links: COW touches only the insertion path.
    internal sealed class DeathTimeIndex
    {
        private sealed class Link { internal string Key, Page; internal long Count; }
        private sealed class Node { internal int Level; internal Link[] Links; internal long Count; }
        private readonly IDeathArchiveFiles files;
        private readonly Func<bool> cancelled;
        private readonly Dictionary<string, Node> cache = new Dictionary<string, Node>(StringComparer.Ordinal);
        private readonly Queue<string> order = new Queue<string>();
        internal DeathTimeIndex(IDeathArchiveFiles files, Func<bool> cancelled) { this.files = files; this.cancelled = cancelled; }
        private void Check() { if (cancelled()) throw new OperationCanceledException(); }
        private Node Read(string page)
        {
            Check(); Node node; if (cache.TryGetValue(page, out node)) return node;
            using (var reader = DeathArchiveCodec.Reader(files.ReadPage(page), 3))
            {
                int level = reader.ReadInt32(), length = reader.ReadInt32();
                if (level < 0 || level > 12 || length < 1 || length > (level == 0 ? 64 : 32)) throw PreferenceJson.Invalid();
                node = new Node { Level = level, Links = new Link[length] }; string previous = null;
                for (int i = 0; i < length; i++)
                {
                    var link = new Link { Key = DeathArchiveCodec.String(reader, 52), Page = DeathArchiveCodec.String(reader, 64), Count = reader.ReadInt64() };
                    DeathEventId.Time(link.Key, TimeSpan.Zero);
                    if (!DeathArchiveCodec.PageId(link.Page) || link.Count < 1 || level == 0 && link.Count != 1 || previous != null && String.CompareOrdinal(previous, link.Key) >= 0) throw PreferenceJson.Invalid();
                    node.Count = checked(node.Count + link.Count); node.Links[i] = link; previous = link.Key;
                }
                DeathArchiveCodec.End(reader);
            }
            Cache(page, node); return node;
        }
        private void Cache(string id, Node node)
        { if (cache.ContainsKey(id)) return; if (cache.Count == 128) cache.Remove(order.Dequeue()); cache.Add(id, node); order.Enqueue(id); }
        private Link Save(int level, Link[] links)
        {
            Check(); var node = new Node { Level = level, Links = links };
            using (var stream = new MemoryStream()) using (var writer = DeathArchiveCodec.Writer(stream, 3))
            {
                writer.Write(level); writer.Write(links.Length);
                foreach (Link link in links) { DeathArchiveCodec.String(writer, link.Key); DeathArchiveCodec.String(writer, link.Page); writer.Write(link.Count); node.Count = checked(node.Count + link.Count); }
                writer.Flush(); string page = files.CreatePage(stream.ToArray()); Cache(page, node);
                return new Link { Key = links[0].Key, Page = page, Count = node.Count };
            }
        }
        internal string Find(string root, string key)
        {
            if (root == "") return null; Node node = Read(root); int depth = 0;
            while (true)
            {
                if (++depth > 13) throw PreferenceJson.Invalid();
                int at = Before(node.Links, key); if (at < 0) return null; Link link = node.Links[at];
                if (node.Level == 0) return link.Key == key ? link.Page : null;
                node = Child(node, link);
            }
        }
        private Node Child(Node parent, Link link)
        { Node child = Read(link.Page); if (child.Level != parent.Level - 1 || child.Count != link.Count || child.Links[0].Key != link.Key) throw PreferenceJson.Invalid(); return child; }
        private static int Before(Link[] links, string key)
        { int lo = 0, hi = links.Length; while (lo < hi) { int mid = lo + (hi - lo) / 2; if (String.CompareOrdinal(links[mid].Key, key) <= 0) lo = mid + 1; else hi = mid; } return lo - 1; }
        internal string Insert(string root, string key, string header)
        {
            var entry = new Link { Key = key, Page = header, Count = 1 };
            if (root == "") return Save(0, new[] { entry }).Page;
            Node old = Read(root); Link[] roots = Insert(old, entry);
            if (roots.Length == 1) return roots[0].Page;
            if (old.Level >= 12) throw new InvalidOperationException("death-index-capacity");
            return Save(old.Level + 1, roots).Page;
        }
        private Link[] Insert(Node node, Link entry)
        {
            int at = Before(node.Links, entry.Key); var values = new List<Link>(node.Links);
            if (node.Level == 0)
            { if (at >= 0 && node.Links[at].Key == entry.Key) throw new InvalidOperationException("duplicate-death-index-key"); values.Insert(at + 1, entry); }
            else
            { at = Math.Max(0, at); Link[] replacement = Insert(Child(node, node.Links[at]), entry); values.RemoveAt(at); values.InsertRange(at, replacement); }
            int capacity = node.Level == 0 ? 64 : 32;
            if (values.Count <= capacity) return new[] { Save(node.Level, values.ToArray()) };
            int half = values.Count / 2;
            return new[] { Save(node.Level, values.GetRange(0, half).ToArray()), Save(node.Level, values.GetRange(half, values.Count - half).ToArray()) };
        }
        internal string[] Page(string root, long skip, int take)
        { var values = new List<string>(take); if (root != "") Collect(Read(root), ref skip, take, values); return values.ToArray(); }
        internal long Rank(string root, string key)
        {
            if (root == "") return 0; Node node = Read(root); long rank = 0;
            while (true)
            {
                int at = Before(node.Links, key); if (at < 0) return rank;
                for (int i = 0; i < at; i++) rank = checked(rank + node.Links[i].Count);
                if (node.Level == 0) return rank + (String.CompareOrdinal(node.Links[at].Key, key) < 0 ? 1 : 0);
                node = Child(node, node.Links[at]);
            }
        }
        private void Collect(Node node, ref long skip, int take, List<string> result)
        {
            foreach (Link link in node.Links)
            {
                if (result.Count == take) break; if (skip >= link.Count) { skip -= link.Count; continue; }
                if (node.Level == 0) result.Add(link.Page); else Collect(Child(node, link), ref skip, take, result);
            }
        }
        internal void Validate(string root, long count)
        { if (root == "") { if (count != 0) throw PreferenceJson.Invalid(); return; } Node node = Read(root); if (node.Count != count) throw PreferenceJson.Invalid(); Validate(node, null); }
        private void Validate(Node node, string upper)
        {
            for (int i = 0; i < node.Links.Length; i++)
            {
                Check(); Link link = node.Links[i]; if (upper != null && String.CompareOrdinal(link.Key, upper) >= 0) throw PreferenceJson.Invalid();
                if (node.Level != 0) Validate(Child(node, link), i + 1 < node.Links.Length ? node.Links[i + 1].Key : upper);
            }
        }
    }
}
