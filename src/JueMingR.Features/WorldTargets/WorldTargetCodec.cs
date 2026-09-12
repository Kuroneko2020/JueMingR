using System;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using JueMingR.Platform.Settings;
using JueMingR.Platform.WorldTargets;

namespace JueMingR.Features.WorldTargets
{
    public sealed class WorldTargetCodec : IPreferenceCodec<WorldTargetSettings>
    {
        private static readonly string[] Names = { "lifeCrystal", "lifeFruit", "manaCrystal", "sleepingDigtoise", "chilletEgg" };
        private static readonly WorldTargetKind[] Kinds = { WorldTargetKind.LifeCrystal, WorldTargetKind.LifeFruit,
            WorldTargetKind.ManaCrystal, WorldTargetKind.SleepingDigtoise, WorldTargetKind.ChilletEgg };
        public WorldTargetSettings Decode(byte[] contents)
        {
            XElement root = PreferenceJson.Read(contents, "JueMingR.WorldTargets", "format", "version",
                "lifeCrystal", "lifeFruit", "manaCrystal", "sleepingDigtoise", "chilletEgg");
            var value = WorldTargetSettings.Default;
            try
            {
                for (int i = 0; i < Names.Length; i++)
                {
                    XElement item = PreferenceJson.Required(root, Names[i], "object");
                    PreferenceJson.ExactFields(item, "enabled", "rgb");
                    XElement flag = PreferenceJson.Required(item, "enabled", "boolean");
                    if (flag.HasElements || flag.Value != "true" && flag.Value != "false") throw PreferenceJson.Invalid();
                    value = value.WithEnabled(Kinds[i], flag.Value == "true").WithColor(Kinds[i],
                        PreferenceJson.Integer(PreferenceJson.Required(item, "rgb", "number")));
                }
                return value;
            }
            catch (ArgumentException) { throw PreferenceJson.Invalid(); }
        }
        public byte[] Encode(WorldTargetSettings value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var json = new StringBuilder("{\"format\":\"JueMingR.WorldTargets\",\"version\":1");
            for (int i = 0; i < Names.Length; i++)
                json.Append(",\"").Append(Names[i]).Append("\":{\"enabled\":").Append(value.Enabled(Kinds[i]) ? "true" : "false")
                    .Append(",\"rgb\":").Append(value.Color(Kinds[i]).ToString(CultureInfo.InvariantCulture)).Append('}');
            return new UTF8Encoding(false, true).GetBytes(json.Append('}').ToString());
        }
    }
}
