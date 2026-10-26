using System.Text;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Blueprint
{
    public sealed class BlueprintPreferenceCodec : IPreferenceCodec<BlueprintPreferences>
    {
        public BlueprintPreferences Decode(byte[] contents)
        {
            // autoBuild is an additive v1 preference. Old v1 bytes remain a
            // valid input; unknown fields/versions retain the shared write lock.
            var root = PreferenceJson.ReadOptional(contents, "JueMingR.Blueprint", "autoBuild", "format", "version", "gelToolbar");
            string value = PreferenceJson.Required(root, "gelToolbar", "boolean").Value;
            if (value != "true" && value != "false") throw PreferenceJson.Invalid();
            bool autoBuild = false;
            if (root.Element("autoBuild") != null)
            {
                string build = PreferenceJson.Required(root, "autoBuild", "boolean").Value;
                if (build != "true" && build != "false") throw PreferenceJson.Invalid();
                autoBuild = build == "true";
            }
            return new BlueprintPreferences(value == "true", autoBuild);
        }
        public byte[] Encode(BlueprintPreferences value)
        { return new UTF8Encoding(false, true).GetBytes("{\"format\":\"JueMingR.Blueprint\",\"version\":1,\"gelToolbar\":" + (value.GelToolbar ? "true" : "false") + ",\"autoBuild\":" + (value.AutoBuild ? "true" : "false") + "}\n"); }
    }
}
