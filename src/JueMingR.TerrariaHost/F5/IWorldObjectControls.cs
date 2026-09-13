using JueMingR.Features.WorldObjectText;
using JueMingR.Platform.WorldObjectText;

namespace JueMingR.TerrariaHost.F5
{
    // The existing F5/style consumers need intent and commands, not tile reads,
    // native chat resources or record-storage ownership.
    internal interface IWorldObjectControls
    {
        WorldObjectSettings Settings { get; }
        bool CanConfigure { get; }
        bool ControlsEnabled { get; }
        string PreferenceMessage { get; }
        bool SetMode(WorldObjectKind kind, WorldObjectMode mode);
        bool SetColor(WorldObjectKind kind, int rgb);
        bool ResetColor(WorldObjectKind kind);
        bool SetSize(WorldObjectKind kind, int size);
        bool SetLimits(WorldObjectKind kind, int lines, int characters);
    }
}
