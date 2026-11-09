using System;
using JueMingR.Features.Blueprint;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        private static BlueprintProjectionBounds IntersectBuildBounds(BlueprintProjectionBounds a, BlueprintProjectionBounds b)
        {
            int left = Math.Max(a.Left, b.Left), top = Math.Max(a.Top, b.Top);
            return new BlueprintProjectionBounds(left, top, Math.Max(0, Math.Min(a.Right, b.Right) - left),
                Math.Max(0, Math.Min(a.Bottom, b.Bottom) - top));
        }
        private static bool EmptyBuildBounds(BlueprintProjectionBounds value) { return value.Width <= 0 || value.Height <= 0; }

    }
}
