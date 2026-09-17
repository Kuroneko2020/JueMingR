using System;
using System.Text;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Announcements
{
    public sealed class AnnouncementSettings
    {
        public bool Enabled { get; }
        public AnnouncementSettings(bool enabled) { Enabled = enabled; }
    }
    public sealed class AnnouncementCodec : IPreferenceCodec<AnnouncementSettings>
    {
        public AnnouncementSettings Decode(byte[] bytes)
        {
            var root = PreferenceJson.Read(bytes, "JueMingR.Announcements", "format", "version", "enabled");
            var value = PreferenceJson.Required(root, "enabled", "boolean");
            if (value.HasElements || value.Value != "true" && value.Value != "false") throw PreferenceJson.Invalid();
            return new AnnouncementSettings(value.Value == "true");
        }
        public byte[] Encode(AnnouncementSettings value)
        { if (value == null) throw new ArgumentNullException(nameof(value)); return Encoding.UTF8.GetBytes("{\"format\":\"JueMingR.Announcements\",\"version\":1,\"enabled\":" + (value.Enabled ? "true" : "false") + "}"); }
    }
}
