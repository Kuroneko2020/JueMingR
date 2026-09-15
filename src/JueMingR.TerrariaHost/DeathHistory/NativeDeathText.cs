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
        {
            if (text == null) return null;
            try
            {
                Validate(); int nodes = 0, budget = 0; long length;
                var copy = Copy(text, new HashSet<NetworkText>(), 0, ref nodes, ref budget, out length);
                // Native formatting can replace a failed object's own fields.
                // Only the complete private copy ever reaches that operation.
                string value = copy.ToString(); return String.IsNullOrEmpty(value) || value.Length > Limit ? null : value;
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is OverflowException || e is FormatException) { return null; }
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
