using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria.Localization;

namespace JueMingR.TerrariaHost.DeathHistory
{
    internal static class NativeDeathText
    {
        private const int Limit = 262144;
        private static readonly FieldInfo Mode = typeof(NetworkText).GetField("_mode", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo Text = typeof(NetworkText).GetField("_text", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo Substitutions = typeof(NetworkText).GetField("_substitutions", BindingFlags.Instance | BindingFlags.NonPublic);
        internal static void Validate()
        {
            if (Mode == null || !Mode.FieldType.IsEnum || Text?.FieldType != typeof(string) || Substitutions?.FieldType != typeof(NetworkText[]))
                throw new InvalidOperationException("native-death-text-shape-changed");
        }
        internal static string Freeze(NetworkText text)
        { string cause; return Capture(text, out cause); }
        internal static string Capture(NetworkText text, out string directCause)
        {
            directCause = null;
            if (text == null) return null;
            try
            {
                Validate(); int nodes = 0, budget = 0; long length;
                var copy = Copy(text, new HashSet<NetworkText>(), 0, ref nodes, ref budget, out length);
                // Native formatting can replace a failed object's own fields.
                // Only the complete private copy ever reaches that operation.
                string value = copy.ToString();
                if (String.IsNullOrEmpty(value) || value.Length > Limit) return null;
                // Classification is optional. A failed projection must never
                // discard an original sentence that was already frozen safely.
                try { directCause = DirectCause(copy); }
                catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is OverflowException || e is FormatException) { directCause = null; }
                return value;
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is OverflowException || e is FormatException) { return null; }
        }
        private static string DirectCause(NetworkText copy)
        {
            // Native keys/substitutions describe this already-created death.
            // Never parse the translated obituary, regenerate it, or resolve a
            // mutable entity slot later. Literal/custom/unknown text falls back.
            if (Convert.ToInt32(Mode.GetValue(copy)) != 2) return null;
            string key = (string)Text.GetValue(copy), cause = null;
            var parts = (NetworkText[])Substitutions.GetValue(copy);
            if ((key == "DeathSource.NPC" || key == "DeathSource.Projectile") && parts?.Length == 2)
                cause = NamedCause(parts[1], "死于");
            else if (key == "DeathSource.Player" && parts?.Length == 3)
                cause = NamedCause(parts[1], "死于玩家 ");
            else if (parts?.Length == 1)
            {
                if (Series(key, "Fell", 9)) cause = "死于摔落";
                else if (Series(key, "Drowned", 7)) cause = "死于溺水";
                else if (Series(key, "Lava", 5)) cause = "死于岩浆";
                else if (Series(key, "Petrified", 4)) cause = "死于石化";
                else if (Series(key, "Suffocated", 2)) cause = "死于窒息";
                else if (Series(key, "Burned", 4)) cause = "死于燃烧";
                else if (key == "DeathText.Poisoned") cause = "死于中毒";
                else if (Series(key, "Electrocuted", 4)) cause = "死于触电";
                else if (Series(key, "Starved", 3)) cause = "死于饥饿";
                else if (key == "DeathText.Teleport_1" || key == "DeathText.Teleport_2_Male" || key == "DeathText.Teleport_2_Female") cause = "死于混沌状态";
                else if (key == "DeathText.TeamTank") cause = "死于替队友承受伤害";
                else if (key == "DeathText.Spored") cause = "死于叶绿孢子";
                else if (key == "DeathText.Stabbed") cause = "死于刺伤";
                else if (key == "DeathText.TriedToEscape") cause = "死于逃离血肉墙";
                else if (Series(key, "WasLicked", 2)) cause = "死于血肉墙";
                else if (key == "DeathText.Inferno") cause = "死于地狱火";
                else if (key == "DeathText.DiedInTheDark") cause = "死于黑暗";
            }
            else if (parts?.Length == 2)
            {
                if (Series(key, "Space", 5)) cause = "死于越过世界顶部";
                else if (Series(key, "Underground", 5)) cause = "死于坠出世界底部";
                else if (Series(key, "VampireBurningInDaylight", 6)) cause = "死于阳光灼烧";
            }
            return cause != null && cause.Length <= JueMingR.Platform.DeathHistory.DeathFact.MaximumDirectCauseLength ? cause : null;
        }
        private static string NamedCause(NetworkText name, string prefix)
        { string value = name?.ToString(); return String.IsNullOrWhiteSpace(value) ? null : prefix + value; }
        private static bool Series(string key, string name, int last)
        {
            string prefix = "DeathText." + name + "_";
            return key.Length == prefix.Length + 1 && key.StartsWith(prefix, StringComparison.Ordinal) && key[key.Length - 1] >= '1' && key[key.Length - 1] <= '0' + last;
        }
        private static NetworkText Copy(NetworkText source, HashSet<NetworkText> path, int depth, ref int nodes, ref int budget, out long length)
        {
            if (source == null || depth > 16 || ++nodes > 256 || !path.Add(source)) throw new InvalidOperationException("death-text-tree-limit");
            try
            {
                string text = (string)Text.GetValue(source); int mode = Convert.ToInt32(Mode.GetValue(source));
                if (text == null || text.Length > Limit || (budget += text.Length) > Limit || mode < 0 || mode > 2) throw new InvalidOperationException("death-text-source-limit");
                if (mode == 0) { length = text.Length; return NetworkText.FromLiteral(text); }
                var children = (NetworkText[])Substitutions.GetValue(source);
                if (children == null || children.Length > 256) throw new InvalidOperationException("death-text-substitutions-limit");
                var copies = new object[children.Length]; long arguments = 0;
                for (int i = 0; i < children.Length; i++) { long childLength; copies[i] = Copy(children[i], path, depth + 1, ref nodes, ref budget, out childLength); arguments = checked(arguments + childLength); }
                string format = mode == 2 ? Language.GetText(text).Value : text;
                length = UpperBound(format, arguments);
                return mode == 1 ? NetworkText.FromFormattable(text, copies) : NetworkText.FromKey(text, copies);
            }
            finally { path.Remove(source); }
        }
        private static long UpperBound(string format, long arguments)
        {
            if (format == null || format.Length > Limit) throw new InvalidOperationException("death-text-format-limit");
            long length = format.Length;
            for (int i = 0; i < format.Length; i++)
            {
                if (format[i] != '{') continue;
                if (i + 1 < format.Length && format[i + 1] == '{') { i++; continue; }
                length = checked(length + arguments);
                for (int j = i + 1; j < format.Length && format[j] != '}'; j++)
                    if (format[j] == ',')
                    {
                        long width = 0; j++; while (j < format.Length && (format[j] == ' ' || format[j] == '-')) j++;
                        while (j < format.Length && format[j] >= '0' && format[j] <= '9') { width = checked(width * 10 + format[j++] - '0'); if (width > Limit) throw new InvalidOperationException("death-text-alignment-limit"); }
                        length = checked(length + width); break;
                    }
                if (length > Limit) throw new InvalidOperationException("death-text-output-limit");
            }
            return length;
        }
    }
}
