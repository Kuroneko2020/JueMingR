using System;
using System.Text;
using JueMingR.Platform.Settings;
namespace JueMingR.Features.Onboarding
{
    public sealed class OnboardingMarkerCodec
    {
        public const int MaximumBytes = 1024;
        private readonly string character;
        public OnboardingMarkerCodec(string character)
        { if (!ValidKey(character)) throw new ArgumentException("Invalid character key."); this.character = character; }
        public static bool ValidKey(string value)
        { if (value == null || value.Length != 64) return false; foreach (char c in value) if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) return false; return true; }
        public bool Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length > MaximumBytes) throw PreferenceJson.Invalid();
            var root = PreferenceJson.Read(bytes, "JueMingR.Onboarding", "format", "version", "character", "seen");
            if (PreferenceJson.Required(root, "character", "string").Value != character || PreferenceJson.Required(root, "seen", "boolean").Value != "true") throw PreferenceJson.Invalid();
            return true;
        }
        public byte[] Encode(bool seen)
        {
            if (!seen) throw new ArgumentException("Only an actually presented marker can be saved.");
            return new UTF8Encoding(false, true).GetBytes("{\"format\":\"JueMingR.Onboarding\",\"version\":1,\"character\":\"" + character + "\",\"seen\":true}\n");
        }
    }
}
