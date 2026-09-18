namespace Terraria.Testing
{
    public static class DebugOptions { public static bool enableDebugCommands; }
}
namespace Terraria.GameInput
{
    public static partial class PlayerInput { public static bool InBuildingMode; }
    public class LockOnHelper { public static bool ForceUsability; }
}
namespace Terraria { public partial class Main { public static string blockKey = "None"; } }
namespace Terraria.UI { public static class IngameFancyUI { public static bool CanCover() { return Main.inFancyUI; } } }
namespace Terraria.UI.Gamepad { public static class UILinkPointNavigator { public static bool Available; } }
namespace Terraria.Social
{
    public enum SocialMode { None, Steam, WeGame }
    public static class SocialAPI { public static SocialMode Mode { get; set; } }
}
