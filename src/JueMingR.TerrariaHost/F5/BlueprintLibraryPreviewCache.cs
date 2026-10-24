using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace JueMingR.TerrariaHost.F5
{
    // The graphics thread exclusively owns these derived textures. Content
    // identity excludes names/camera/page position; device generation retires
    // pending uploads as well as cached resources. At most twelve finite images.
    internal sealed class BlueprintLibraryPreviewCache : IDisposable
    {
        internal const int MaximumCount = 12;
        internal const long MaximumPixelBytes = 8L * 1024 * 1024;
        private sealed class Entry { internal Texture2D Texture; internal CaptureSummary Summary; internal long Used; }
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private GraphicsDevice device;
        private long clock;
        internal int Generation { get; private set; }
        internal int Count { get { return entries.Count; } }
        internal long PixelBytes { get; private set; }
        internal long Uploads { get; private set; }
        internal void Observe(GraphicsDevice current)
        {
            if (ReferenceEquals(device, current)) return;
            Dispose(); device = current;
            if (device != null) { device.DeviceResetting += Reset; device.Disposing += Reset; }
        }
        internal Texture2D Get(string key)
        {
            Entry entry;
            return key != null && entries.TryGetValue(key, out entry) && !entry.Texture.IsDisposed ? entry.Texture : null;
        }
        internal bool Touch(string key)
        {
            Entry entry;
            if (key == null || !entries.TryGetValue(key, out entry) || entry.Texture.IsDisposed) return false;
            entry.Used = ++clock; return true;
        }
        internal CaptureSummary Summary(string key)
        {
            Entry entry;
            return key != null && entries.TryGetValue(key, out entry) && !entry.Texture.IsDisposed ? entry.Summary : null;
        }
        internal bool Upload(string key, BlueprintPreviewImage image, CaptureSummary summary, int expectedGeneration)
        {
            if (device == null || device.IsDisposed || image == null || key == null || expectedGeneration != Generation) return false;
            if (Touch(key)) return true;
            long bytes = (long)image.Width * image.Height * 4;
            if (bytes > MaximumPixelBytes) return false;
            // One draft handle and current size may retain exclusion variants
            // for A-B-A revisits. Historical resize images/previous handles are
            // retired; the accepted replacement occupies one pending slot.
            if (key.StartsWith("d:", StringComparison.Ordinal))
            {
                var retired = new List<string>();
                foreach (var item in entries) if (item.Key.StartsWith("d:", StringComparison.Ordinal) &&
                    (item.Key.Substring(0, 34) != key.Substring(0, 34) || SizeIdentity(item.Key) != SizeIdentity(key))) retired.Add(item.Key);
                foreach (string old in retired) Remove(old);
            }
            while (entries.Count >= MaximumCount || PixelBytes + bytes > MaximumPixelBytes) Evict();
            var texture = new Texture2D(device, image.Width, image.Height, false, SurfaceFormat.Color);
            try
            {
                uint[] values = image.CopyPixels(); var colors = new Color[values.Length];
                for (int i = 0; i < colors.Length; i++) colors[i] = new Color { PackedValue = values[i] };
                texture.SetData(colors);
            }
            catch { texture.Dispose(); throw; }
            // The finite image and its derived bounds/count/missing facts have
            // one identity and one eviction lifetime, including cache revisits.
            entries.Add(key, new Entry { Texture = texture, Summary = summary, Used = ++clock });
            PixelBytes += bytes; Uploads++; return true;
        }
        private static string SizeIdentity(string key)
        {
            int start = key.Length;
            for (int i = 0; i < 4; i++) start = key.LastIndexOf(':', start - 1);
            return key.Substring(start);
        }
        private void Evict()
        {
            string oldest = null; long used = long.MaxValue;
            foreach (var item in entries) if (item.Value.Used < used) { oldest = item.Key; used = item.Value.Used; }
            Remove(oldest);
        }
        private void Remove(string key)
        {
            var texture = entries[key].Texture; PixelBytes -= (long)texture.Width * texture.Height * 4;
            entries.Remove(key); texture.Dispose();
        }
        private void Reset(object sender, EventArgs args)
        {
            foreach (var entry in entries.Values) entry.Texture.Dispose();
            entries.Clear(); PixelBytes = 0; Generation++;
        }
        public void Dispose()
        {
            if (device != null) { device.DeviceResetting -= Reset; device.Disposing -= Reset; }
            device = null; Reset(null, EventArgs.Empty);
        }
    }
}
