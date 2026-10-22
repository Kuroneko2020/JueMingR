using System.Text;
using System.Xml.Linq;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Movement
{
    public sealed class MovementPreferenceCodec : IPreferenceCodec<MovementOptions>
    {
        private static readonly string[] permissionNames = { "doubleJump", "rocketBoots", "carpet", "wings", "horseshoe", "umbrella", "grapple", "flyingMount", "djinnLegs", "safeMount", "teleportRod", "gravityGlobe" };
        public MovementOptions Decode(byte[] bytes)
        {
            var root = PreferenceJson.ReadOptional(bytes, "JueMingR.Movement", "smartFallProtection", "format", "version", "simulatedJump", "continuousDash", "teleportCorrection", "dashMode");
            var mode = PreferenceJson.Required(root, "dashMode", "string");
            if (mode.HasElements || mode.Value != "hold" && mode.Value != "doubleTap") throw PreferenceJson.Invalid();
            var protection = new SmartFallProtectionOptions();
            if (root.Element("smartFallProtection") != null)
            {
                var node = PreferenceJson.Required(root, "smartFallProtection", "object");
                var fields = new string[permissionNames.Length + 1]; fields[0] = "enabled"; permissionNames.CopyTo(fields, 1);
                PreferenceJson.ExactFields(node, fields);
                protection = protection.WithEnabled(Boolean(node, "enabled"));
                for (int i = 0; i < permissionNames.Length; i++) protection = protection.WithPermission(i, Boolean(node, permissionNames[i]));
            }
            return new MovementOptions(Boolean(root, "simulatedJump"), Boolean(root, "continuousDash"), Boolean(root, "teleportCorrection"), mode.Value == "hold" ? ContinuousDashMode.Hold : ContinuousDashMode.DoubleTap, protection);
        }
        private static bool Boolean(XElement root, string name)
        { var value = PreferenceJson.Required(root, name, "boolean"); if (value.HasElements || value.Value != "true" && value.Value != "false") throw PreferenceJson.Invalid(); return value.Value == "true"; }
        public byte[] Encode(MovementOptions value)
        {
            var text = new StringBuilder("{\"format\":\"JueMingR.Movement\",\"version\":1,\"simulatedJump\":").Append(Bool(value.SimulatedJumpEnabled))
                .Append(",\"continuousDash\":").Append(Bool(value.ContinuousDashEnabled)).Append(",\"teleportCorrection\":").Append(Bool(value.TeleportCorrectionEnabled))
                .Append(",\"dashMode\":\"").Append(value.DashMode == ContinuousDashMode.Hold ? "hold" : "doubleTap").Append("\",\"smartFallProtection\":{\"enabled\":").Append(Bool(value.SmartFallProtection.Enabled));
            for (int i = 0; i < permissionNames.Length; i++) text.Append(",\"").Append(permissionNames[i]).Append("\":").Append(Bool(value.SmartFallProtection.Allows(i)));
            return new UTF8Encoding(false, true).GetBytes(text.Append("}}\n").ToString());
        }
        private static string Bool(bool value) { return value ? "true" : "false"; }
    }
}
