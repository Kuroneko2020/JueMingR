using System;

namespace JueMingR.Features.Guidance
{
    public struct DirectionPose { public double X, Y, Angle, Radius, Scale; public bool Visible; }
    public static class DirectionProjection
    {
        // Inputs are final physical pixels, after the current Game transform.
        // The single scalar radius preserves a circle even with camera offsets.
        public static DirectionPose Circle(double playerX, double playerY, double targetX, double targetY, double radius)
        {
            double x = targetX - playerX, y = targetY - playerY, length = Math.Sqrt(x * x + y * y);
            if (!RareCreatureDirection.Finite(length) || length < .01 || radius <= 0) return default(DirectionPose);
            radius = Math.Min(radius, length * .5);
            // The 20px arrow's forward half must also stay before a close
            // target; shortening only its center offset would still overshoot.
            return new DirectionPose { Visible = true, X = playerX + x / length * radius, Y = playerY + y / length * radius, Angle = Math.Atan2(y, x), Radius = radius, Scale = Math.Min(1, length / 32) };
        }
        public static DirectionPose Ellipse(double targetX, double targetY, double width, double height)
        {
            double x = targetX - width / 2, y = targetY - height / 2;
            double rx = Math.Max(1, width / 2 - 48), ry = Math.Max(1, height / 2 - 42);
            double d = Math.Sqrt(x * x / (rx * rx) + y * y / (ry * ry));
            if (!RareCreatureDirection.Finite(d) || d < .01) return default(DirectionPose);
            return new DirectionPose { Visible = true, X = width / 2 + x / d, Y = height / 2 + y / d };
        }
        public static bool OnScreen(double x, double y, double width, double height)
        { return x >= -16 && y >= -16 && x <= width + 16 && y <= height + 16; }
        public static int Tiles(double x, double y, double targetX, double targetY)
        { return (int)Math.Min(int.MaxValue, Math.Round(Math.Sqrt(RareCreatureDirection.Distance(x, y, targetX, targetY)) / 16, MidpointRounding.AwayFromZero)); }
    }
}
