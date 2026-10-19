using System.Text;
using System.Xml.Linq;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Movement
{
    public sealed class MovementPreferenceCodec : IPreferenceCodec<MovementOptions>
    {
        public MovementOptions Decode(byte[] bytes)
        {
            var root = PreferenceJson.Read(bytes, "JueMingR.Movement", "format", "version", "simulatedJump", "continuousDash", "teleportCorrection", "dashMode");
            var mode = PreferenceJson.Required(root, "dashMode", "string");
            if (mode.HasElements || mode.Value != "hold" && mode.Value != "doubleTap") throw PreferenceJson.Invalid();
            return new MovementOptions(Boolean(root, "simulatedJump"), Boolean(root, "continuousDash"), Boolean(root, "teleportCorrection"), mode.Value == "hold" ? ContinuousDashMode.Hold : ContinuousDashMode.DoubleTap);
        }
        private static bool Boolean(XElement root, string name)
        { var value = PreferenceJson.Required(root, name, "boolean"); if (value.HasElements || value.Value != "true" && value.Value != "false") throw PreferenceJson.Invalid(); return value.Value == "true"; }
        public byte[] Encode(MovementOptions value)
        { return new UTF8Encoding(false, true).GetBytes("{\"format\":\"JueMingR.Movement\",\"version\":1,\"simulatedJump\":" + Bool(value.SimulatedJumpEnabled) + ",\"continuousDash\":" + Bool(value.ContinuousDashEnabled) + ",\"teleportCorrection\":" + Bool(value.TeleportCorrectionEnabled) + ",\"dashMode\":\"" + (value.DashMode == ContinuousDashMode.Hold ? "hold" : "doubleTap") + "\"}\n"); }
        private static string Bool(bool value) { return value ? "true" : "false"; }
    }
}
