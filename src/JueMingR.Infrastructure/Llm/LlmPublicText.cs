using System;
using System.Collections.Generic;
using System.Text;

namespace JueMingR.Infrastructure.Llm
{
    internal sealed class LlmPublicText
    {
        private readonly string[] secrets;
        private string pending = "";

        public LlmPublicText(string[] secrets) { this.secrets = secrets; }
        public string Next(string text) => Redact(pending + text, false, out pending);
        public string Final(string text) => Redact(text, true, out _);
        public string Flush() => Redact(pending, true, out pending);

        private string Redact(string text, bool final, out string remainder)
        {
            remainder = "";
            if (secrets.Length == 0 || string.IsNullOrEmpty(text)) return text ?? "";
            var result = new StringBuilder(text.Length);
            for (int index = 0; index < text.Length; index++)
            {
                bool replaced = false;
                foreach (string secret in secrets)
                {
                    int remaining = text.Length - index;
                    int count = Math.Min(secret.Length, remaining);
                    if (string.CompareOrdinal(text, index, secret, 0, count) != 0) continue;
                    if (remaining >= secret.Length)
                    {
                        result.Append("[已隐藏]");
                        index += secret.Length - 1;
                        replaced = true;
                        break;
                    }
                    if (!final)
                    {
                        remainder = text.Substring(index);
                        return result.ToString();
                    }
                }
                if (!replaced) result.Append(text[index]);
            }
            return result.ToString();
        }
    }
}
