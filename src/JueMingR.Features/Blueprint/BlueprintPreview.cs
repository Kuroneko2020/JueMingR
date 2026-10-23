using System;

namespace JueMingR.Features.Blueprint
{
    [Flags]
    public enum BlueprintPreviewLayer : byte
    {
        None = 0, Wall = 1, Wire = 2, Actuator = 4, Liquid = 8, Tile = 16, Object = 32
    }

    // This is an immutable, finite presentation result, never an asset body.
    // Packed pixels use XNA Color's 0xAABBGGRR order without referencing XNA.
    public sealed class BlueprintPreviewImage
    {
        private readonly uint[] pixels;
        internal BlueprintPreviewImage(int width, int height, uint[] pixels, long processed)
        { Width = width; Height = height; this.pixels = pixels; ProcessedCells = processed; }
        public int Width { get; }
        public int Height { get; }
        public long ProcessedCells { get; }
        public uint[] CopyPixels() { return (uint[])pixels.Clone(); }
        public uint GetPixel(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) throw new ArgumentOutOfRangeException();
            return pixels[y * Width + x];
        }
    }

    // A worker feeds every meaningful cell once. Memory depends on the finite
    // output, not source bounds or cell count. OR aggregation is independent of
    // chunk order and retains remote sparse content instead of sampling a prefix.
    public sealed class BlueprintPreviewBuilder
    {
        public const int RendererVersion = 1;
        public const int MaximumWidth = 512, MaximumHeight = 128;
        private readonly int sourceWidth, sourceHeight, width, height;
        private byte[] layers;
        private long processed;
        public BlueprintPreviewBuilder(int contentWidth, int contentHeight, int maximumWidth, int maximumHeight)
        {
            if (contentWidth <= 0 || contentHeight <= 0 || maximumWidth <= 0 || maximumHeight <= 0 ||
                maximumWidth > MaximumWidth || maximumHeight > MaximumHeight) throw new ArgumentOutOfRangeException();
            sourceWidth = contentWidth; sourceHeight = contentHeight;
            if ((long)contentWidth * maximumHeight >= (long)contentHeight * maximumWidth)
            {
                width = maximumWidth;
                height = Math.Max(1, (int)(((long)contentHeight * width + contentWidth - 1) / contentWidth));
            }
            else
            {
                height = maximumHeight;
                width = Math.Max(1, (int)(((long)contentWidth * height + contentHeight - 1) / contentHeight));
            }
            layers = new byte[width * height];
        }
        public void Add(int x, int y, BlueprintPreviewLayer value)
        {
            if (layers == null) throw new InvalidOperationException("preview-already-completed");
            if (x < 0 || y < 0 || x >= sourceWidth || y >= sourceHeight || ((int)value & ~63) != 0)
                throw new ArgumentOutOfRangeException();
            if (value == BlueprintPreviewLayer.None) return;
            processed = checked(processed + 1);
            int left = (int)((long)x * width / sourceWidth), top = (int)((long)y * height / sourceHeight);
            int right = (int)(((long)(x + 1) * width + sourceWidth - 1) / sourceWidth);
            int bottom = (int)(((long)(y + 1) * height + sourceHeight - 1) / sourceHeight);
            for (int py = top; py < bottom; py++)
                for (int px = left; px < right; px++) layers[py * width + px] |= (byte)value;
        }
        public BlueprintPreviewImage Complete()
        {
            if (layers == null) throw new InvalidOperationException("preview-already-completed");
            var pixels = new uint[layers.Length];
            for (int i = 0; i < layers.Length; i++) pixels[i] = Color((BlueprintPreviewLayer)layers[i]);
            layers = null;
            return new BlueprintPreviewImage(width, height, pixels, processed);
        }
        private static uint Color(BlueprintPreviewLayer value)
        {
            // When several semantic layers share a pixel, a fixed priority
            // chooses the visible summary. No source enumeration order wins.
            if ((value & BlueprintPreviewLayer.Object) != 0) return Pack(136, 196, 126);
            if ((value & BlueprintPreviewLayer.Tile) != 0) return Pack(178, 188, 156);
            if ((value & BlueprintPreviewLayer.Liquid) != 0) return Pack(96, 154, 218);
            if ((value & BlueprintPreviewLayer.Actuator) != 0) return Pack(196, 164, 84);
            if ((value & BlueprintPreviewLayer.Wire) != 0) return Pack(218, 94, 92);
            if ((value & BlueprintPreviewLayer.Wall) != 0) return Pack(96, 134, 188);
            return 0;
        }
        private static uint Pack(uint red, uint green, uint blue) { return red | green << 8 | blue << 16 | 220u << 24; }
    }
}
