using System;
using JueMingR.Features.Text;

namespace JueMingR.Features.Blueprint
{
    public static class LibraryName
    {
        public const int MaximumElements = 48;
        public const int MaximumUtf8Bytes = 32768;
        public static bool TryValidate(string value, out string error)
        {
            error = null;
            if (String.IsNullOrWhiteSpace(value)) { error = "名称不能为空"; return false; }
            if (Char.IsWhiteSpace(value[0]) || Char.IsWhiteSpace(value[value.Length - 1])) { error = "名称首尾不能有空白"; return false; }
            foreach (char c in value) if (Char.IsControl(c) || c == '\u2028' || c == '\u2029') { error = "名称不能包含换行或控制字符"; return false; }
            try { if (TextElements.Boundaries(value).Length - 1 > MaximumElements) { error = "名称最多48个文字元素"; return false; } }
            catch (ArgumentException) { error = "名称包含无效字符或过长组合字符"; return false; }
            if (System.Text.Encoding.UTF8.GetByteCount(value) > MaximumUtf8Bytes) { error = "名称的组合字符内容过长"; return false; }
            return true;
        }
    }
}
