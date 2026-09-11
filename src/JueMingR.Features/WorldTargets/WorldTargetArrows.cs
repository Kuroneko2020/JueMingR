using System;

namespace JueMingR.Features.WorldTargets
{
    public struct WorldTargetAnimation
    {
        public readonly double Angle;
        public readonly float Bob;
        internal readonly double Cos, Sin;
        public WorldTargetAnimation(long ticks, float gravity)
        {
            // Integer modulo before conversion keeps long sessions precise. The
            // mirrored world uses the opposite world orbit for screen-clockwise.
            Angle = (ticks % 70000000L) * (Math.PI * 2 / 70000000L) * gravity;
            Bob = (float)Math.Sin((ticks % 25000000L) * (Math.PI * 2 / 25000000L)) * 3 * gravity;
            Cos = Math.Cos(Angle); Sin = Math.Sin(Angle);
        }
    }
    public struct ArrowPose { public float X, Y, DirectionX, DirectionY, Length; }
    public static class WorldTargetArrows
    {
        // Largest supported origin-to-draw edge: turtle center offset 19 plus
        // sqrt(56^2+46^2)/2 + 32 = 87.25 world pixels. Keep discovery/retention
        // wide enough for visible arrows; resolver neighbor reads stay separate.
        public const int ObservationPadding = 88;
        public static ArrowPose At(WorldTarget target, WorldTargetAnimation animation, int index)
        {
            if (index < 0 || index > 2) throw new ArgumentOutOfRangeException(nameof(index));
            float length = target.Width > 40 || target.Height > 36 ? 21 : 18;
            float radius = (float)Math.Sqrt(target.Width * target.Width + target.Height * target.Height) / 2 + length / 2 + 7;
            // The three unit directions share one time sample/trig calculation
            // across all objects; rotating by 120 degrees needs only products.
            const double rootThreeOverTwo = .86602540378443864676;
            double cos = index == 0 ? animation.Cos : -.5 * animation.Cos + (index == 1 ? -1 : 1) * rootThreeOverTwo * animation.Sin;
            double sin = index == 0 ? animation.Sin : -.5 * animation.Sin + (index == 1 ? 1 : -1) * rootThreeOverTwo * animation.Cos;
            float x = target.CenterX + (float)cos * radius;
            float y = target.CenterY + (float)sin * radius + animation.Bob;
            float dx = target.CenterX - x, dy = target.CenterY - y, distance = (float)Math.Sqrt(dx * dx + dy * dy);
            // Bob moves the arrow, not its target; recompute inward direction.
            return new ArrowPose { X = x, Y = y, DirectionX = dx / distance, DirectionY = dy / distance, Length = length };
        }
        public static float Extent(WorldTarget target)
        { return (float)Math.Sqrt(target.Width * target.Width + target.Height * target.Height) / 2 + 21 + 11; }
    }
}
