using System;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.EntityLabels
{
    public sealed class EntityLabelCodec : IPreferenceCodec<EntityLabelSettings>
    {
        public EntityLabelSettings Decode(byte[] contents)
        {
            XElement root = PreferenceJson.Read(contents, "JueMingR.EntityLabels", "format", "version", "enemyEnabled", "critterEnabled",
                "npcMode", "lastNpcMode", "enemy", "critter", "npc");
            try
            {
                return new EntityLabelSettings(Boolean(root, "enemyEnabled"), Boolean(root, "critterEnabled"),
                    (NpcLabelMode)Number(root, "npcMode"), (NpcLabelMode)Number(root, "lastNpcMode"),
                    Style(root, "enemy"), Style(root, "critter"), Style(root, "npc"));
            }
            catch (ArgumentException) { throw PreferenceJson.Invalid(); }
        }
        public byte[] Encode(EntityLabelSettings value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            // Every interpolated value is a validated integer/boolean; no user
            // text or locale-sensitive float enters this small fixed schema.
            string json = "{\"format\":\"JueMingR.EntityLabels\",\"version\":1,\"enemyEnabled\":" + (value.EnemyEnabled ? "true" : "false") +
                ",\"critterEnabled\":" + (value.CritterEnabled ? "true" : "false") + ",\"npcMode\":" + Integer((int)value.NpcMode) +
                ",\"lastNpcMode\":" + Integer((int)value.LastNpcMode) + ",\"enemy\":" + WriteStyle(value.EnemyStyle) +
                ",\"critter\":" + WriteStyle(value.CritterStyle) + ",\"npc\":" + WriteStyle(value.NpcStyle) + "}";
            return new UTF8Encoding(false, true).GetBytes(json);
        }
        private static EntityLabelStyle Style(XElement root, string name)
        {
            XElement item = PreferenceJson.Required(root, name, "object");
            PreferenceJson.ExactFields(item, "rgb", "nameSize");
            return new EntityLabelStyle(Number(item, "rgb"), Number(item, "nameSize"));
        }
        private static bool Boolean(XElement root, string name)
        {
            XElement item = PreferenceJson.Required(root, name, "boolean");
            if (item.HasElements || item.Value != "true" && item.Value != "false") throw PreferenceJson.Invalid();
            return item.Value == "true";
        }
        private static int Number(XElement root, string name) { return PreferenceJson.Integer(PreferenceJson.Required(root, name, "number")); }
        private static string Integer(int value) { return value.ToString(CultureInfo.InvariantCulture); }
        private static string WriteStyle(EntityLabelStyle style) { return "{\"rgb\":" + Integer(style.Rgb) + ",\"nameSize\":" + Integer(style.NameSize) + "}"; }
    }
}
