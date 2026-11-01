using System.Globalization;
using System.Text;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Blueprint
{
    public sealed class BlueprintPreferenceCodec : IPreferenceCodec<BlueprintPreferences>
    {
        public BlueprintPreferences Decode(byte[] contents)
        {
            // These additive v1 preferences may be absent independently. Reading
            // old bytes never requests a save; unknown fields/versions stay protected.
            var root = PreferenceJson.ReadOptionals(contents, "JueMingR.Blueprint",
                new[] { "autoBuild", "sameTypeReplacement", "replacementCategories" }, "format", "version", "gelToolbar");
            string value = PreferenceJson.Required(root, "gelToolbar", "boolean").Value;
            if (value != "true" && value != "false") throw PreferenceJson.Invalid();
            bool autoBuild = false;
            if (root.Element("autoBuild") != null)
            {
                string build = PreferenceJson.Required(root, "autoBuild", "boolean").Value;
                if (build != "true" && build != "false") throw PreferenceJson.Invalid();
                autoBuild = build == "true";
            }
            bool replacement = false;
            if (root.Element("sameTypeReplacement") != null)
            {
                string enabled = PreferenceJson.Required(root, "sameTypeReplacement", "boolean").Value;
                if (enabled != "true" && enabled != "false") throw PreferenceJson.Invalid();
                replacement = enabled == "true";
            }
            int categories = BlueprintPreferences.AllReplacementCategories;
            if (root.Element("replacementCategories") != null)
            {
                categories = PreferenceJson.Integer(PreferenceJson.Required(root, "replacementCategories", "number"));
                if (categories < 0 || categories > BlueprintPreferences.AllReplacementCategories) throw PreferenceJson.Invalid();
            }
            return new BlueprintPreferences(value == "true", autoBuild, replacement, categories);
        }
        public byte[] Encode(BlueprintPreferences value)
        { return new UTF8Encoding(false, true).GetBytes("{\"format\":\"JueMingR.Blueprint\",\"version\":1,\"gelToolbar\":" + (value.GelToolbar ? "true" : "false") +
            ",\"autoBuild\":" + (value.AutoBuild ? "true" : "false") + ",\"sameTypeReplacement\":" + (value.SameTypeReplacement ? "true" : "false") +
            ",\"replacementCategories\":" + value.ReplacementCategories.ToString(CultureInfo.InvariantCulture) + "}\n"); }
    }
}
