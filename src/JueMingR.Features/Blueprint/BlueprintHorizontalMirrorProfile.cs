using System.Collections.Generic;

namespace JueMingR.Features.Blueprint
{
    // The game-thread Host copies native layout facts once. Every operation on
    // the published profile is value-only and safe for the library worker;
    // neither Terraria metadata nor world arrays may be read through this port.
    public interface IBlueprintHorizontalMirrorProfile
    {
        long Revision { get; }
        bool Ready { get; }
        bool TryMirrorCell(CaptureCell source, int templateWidth, out CaptureCell target, out string reason);
        bool TryNormalizeObject(CaptureObject source, IReadOnlyList<CaptureCell> sourceMembers,
            out CaptureObject target, out string reason);

        // Members are one complete source row-major object. The returned cells
        // are display row-major: transport offsets and source ordinals remain
        // owned by the original stream, rather than by this reordered view.
        bool TryMirrorObject(CaptureObject source, IReadOnlyList<CaptureCell> sourceMembers,
            int templateWidth, out CaptureObject target, out CaptureCell[] targetMembers, out string reason);
    }
}
