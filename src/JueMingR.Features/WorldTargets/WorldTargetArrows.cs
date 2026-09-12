using System;

namespace JueMingR.Features.WorldTargets
{
    public struct WorldTargetAnimation
    {
        public readonly double Angle;
        public readonly float Bob;
        internal readonly double Cos, Sin, BobCos, BobSin;
        internal readonly float Gravity;
        public WorldTargetAnimation(long ticks, float gravity)
        {
            // Integer modulo before conversion keeps long sessions precise. The
            // mirrored world uses the opposite world orbit for screen-clockwise.
            Angle = (ticks % 70000000L) * (Math.PI * 2 / 70000000L) * gravity;
            double bobAngle = (ticks % 27000000L) * (Math.PI * 2 / 27000000L);
            BobSin = Math.Sin(bobAngle); BobCos = Math.Cos(bobAngle); Gravity = gravity;
            Bob = (float)BobSin * 6 * gravity;
            Cos = Math.Cos(Angle); Sin = Math.Sin(Angle);
        }
    }
    public struct ArrowPose { public float X, Y, DirectionX, DirectionY, Length; }
    public static class WorldTargetArrows
    {
        // Largest supported origin-to-draw edge: turtle center offset 19 plus
        // sqrt(56^2+46^2)/2 + 36 = 91.25 world pixels. Keep discovery/retention
        // wide enough for visible arrows; resolver neighbor reads stay separate.
        public const int ObservationPadding = 92;
        // Fixed phase coefficients keep every draw allocation-free and share
        // the time trig across objects. Even 2-tile-adjacent origins differ;
        // list order, rescans, colors and visibility never reset their phase.
        private static readonly double[] PhaseCos = {
            1, .923879532511287, .707106781186548, .38268343236509,
            0, -.38268343236509, -.707106781186548, -.923879532511287,
            -1, -.923879532511287, -.707106781186548, -.38268343236509,
            0, .38268343236509, .707106781186548, .923879532511287 };
        public static float Bob(WorldTarget target, WorldTargetAnimation animation)
        {
            int phase = unchecked(target.TileX * 3 + target.TileY * 5 + (int)target.Kind * 7) & 15;
            return (float)(animation.BobSin * PhaseCos[phase] + animation.BobCos * PhaseCos[(phase + 12) & 15]) * 6 * animation.Gravity;
        }
        public static ArrowPose At(WorldTarget target, WorldTargetAnimation animation, int index)
        {
            if (index < 0 || index > 2) throw new ArgumentOutOfRangeException(nameof(index));
            float length = target.Width > 40 || target.Height > 36 ? 23 : 20;
            // Keep the chosen orbit independent of glyph proportions. Display
            // bounds include transparent corners; radial collision correction
            // would change the owner's vertical bob and established spacing.
            float radius = (float)Math.Sqrt(target.Width * target.Width + target.Height * target.Height) / 2 + 12;
            int phase = unchecked(target.TileX + target.TileY * 3 + (int)target.Kind * 5) & 15;
            double pc = PhaseCos[phase], ps = PhaseCos[(phase + 12) & 15] * animation.Gravity;
            double orbitCos = animation.Cos * pc - animation.Sin * ps;
            double orbitSin = animation.Sin * pc + animation.Cos * ps;
            // The three unit directions share one time sample/trig calculation
            // across all objects; rotating by 120 degrees needs only products.
            double rootThreeOverTwo = .86602540378443864676 * animation.Gravity;
            double cos = index == 0 ? orbitCos : -.5 * orbitCos + (index == 1 ? -1 : 1) * rootThreeOverTwo * orbitSin;
            double sin = index == 0 ? orbitSin : -.5 * orbitSin + (index == 1 ? 1 : -1) * rootThreeOverTwo * orbitCos;
            float x = target.CenterX + (float)cos * radius;
            float y = target.CenterY + (float)sin * radius + Bob(target, animation);
            float dx = target.CenterX - x, dy = target.CenterY - y, distance = (float)Math.Sqrt(dx * dx + dy * dy);
            // Bob moves the arrow, not its target; recompute inward direction.
            return new ArrowPose { X = x, Y = y, DirectionX = dx / distance, DirectionY = dy / distance, Length = length };
        }
        public static float Extent(WorldTarget target)
        { return (float)Math.Sqrt(target.Width * target.Width + target.Height * target.Height) / 2 + 36; }
    }
}
