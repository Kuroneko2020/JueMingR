using System;
using System.Globalization;
using JueMingR.Platform.Information;

namespace JueMingR.Features.Information
{
    public sealed class InformationText
    {
        public string Text { get; private set; }
        public long Version { get; private set; }
        public void Publish(string text) { if (String.Equals(Text, text, StringComparison.Ordinal)) return; Text = text; Version++; }
        public void Clear() { Publish(null); }
        internal static string Status(string name, InformationAvailability availability, string condition)
        {
            return name + "：" + (availability == InformationAvailability.ConditionUnmet ? condition :
                availability == InformationAvailability.Waiting ? "等待可靠数据" : "暂不可用");
        }
        internal static bool Finite(float? value) { return value.HasValue && !Single.IsNaN(value.Value) && !Single.IsInfinity(value.Value); }
        internal static double Rounded(double value) { return Math.Abs(value) < 0.0005 ? 0 : Math.Round(value, 3, MidpointRounding.AwayFromZero); }
        internal static string Number(double value) { return Rounded(value).ToString("0.###", CultureInfo.InvariantCulture); }
        internal static string Signed(double value) { double rounded = Rounded(value); return (rounded > 0 ? "+" : "") + rounded.ToString("0.###", CultureInfo.InvariantCulture); }
    }
}
