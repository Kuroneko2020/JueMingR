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
        private sealed class Entry { internal Texture2D Texture; internal long Used; }
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
        internal bool Upload(string key, BlueprintPreviewImage image, int expectedGeneration)
        {
            if (device == null || device.IsDisposed || image == null || expectedGeneration != Generation) return false;
            if (Touch(key)) return true;
            var texture = new Texture2D(device, image.Width, image.Height, false, SurfaceFormat.Color);
            try
            {
                uint[] values = image.CopyPixels(); var colors = new Color[values.Length];
                for (int i = 0; i < colors.Length; i++) colors[i] = new Color { PackedValue = values[i] };
                texture.SetData(colors);
            }
            catch { texture.Dispose(); throw; }
            while (entries.Count >= 12) Evict();
            entries.Add(key, new Entry { Texture = texture, Used = ++clock });
            PixelBytes += (long)image.Width * image.Height * 4; Uploads++; return true;
        }
        private void Evict()
        {
            string oldest = null; long used = long.MaxValue;
            foreach (var item in entries) if (item.Value.Used < used) { oldest = item.Key; used = item.Value.Used; }
            var texture = entries[oldest].Texture; PixelBytes -= (long)texture.Width * texture.Height * 4;
            entries.Remove(oldest); texture.Dispose();
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
