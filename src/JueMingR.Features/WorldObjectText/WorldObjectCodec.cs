using System;
using System.Globalization;
using System.Text;
using JueMingR.Platform.Settings;
using JueMingR.Platform.WorldObjectText;

namespace JueMingR.Features.WorldObjectText
{
    public sealed class WorldObjectCodec : IPreferenceCodec<WorldObjectSettings>
    {
        private static readonly string[] Names = { "chest", "sign", "tombstone" };
        public WorldObjectSettings Decode(byte[] contents)
        {
            var root = PreferenceJson.Read(contents, "JueMingR.WorldObjectText", "format", "version", "chest", "sign", "tombstone");
            var value = WorldObjectSettings.Default;
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    var item = PreferenceJson.Required(root, Names[i], "object");
                    PreferenceJson.ExactFields(item, "mode", "lastMode", "rgb", "size", "lines", "characters");
                    Func<string, int> number = name => PreferenceJson.Integer(PreferenceJson.Required(item, name, "number"));
                    value = value.With(new WorldObjectStyle((WorldObjectKind)i, (WorldObjectMode)number("mode"),
                        (WorldObjectMode)number("lastMode"), number("rgb"), number("size"), number("lines"), number("characters")));
                }
                return value;
            }
            catch (ArgumentException) { throw PreferenceJson.Invalid(); }
        }
        public byte[] Encode(WorldObjectSettings value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var json = new StringBuilder("{\"format\":\"JueMingR.WorldObjectText\",\"version\":1");
            for (int i = 0; i < 3; i++)
            {
                var style = value.Style((WorldObjectKind)i);
                json.Append(",\"").Append(Names[i]).Append("\":{");
                string[] keys = { "mode", "lastMode", "rgb", "size", "lines", "characters" };
                int[] numbers = { (int)style.Mode, (int)style.LastMode, style.Rgb, style.Size, style.Lines, style.Characters };
                for (int n = 0; n < keys.Length; n++)
                { if (n != 0) json.Append(','); json.Append('"').Append(keys[n]).Append("\":").Append(numbers[n].ToString(CultureInfo.InvariantCulture)); }
                json.Append('}');
            }
            return new UTF8Encoding(false, true).GetBytes(json.Append('}').ToString());
        }
    }
}
