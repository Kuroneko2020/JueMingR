namespace Terraria.Testing
{
    public static class DebugOptions { public static bool enableDebugCommands; }
}
namespace Terraria.GameInput
{
    public class LockOnHelper { public static bool ForceUsability; }
}
namespace Terraria.Social
{
    public enum SocialMode { None, Steam, WeGame }
    public static class SocialAPI { public static SocialMode Mode { get; set; } }
}
