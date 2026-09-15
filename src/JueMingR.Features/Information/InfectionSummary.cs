using JueMingR.Platform.Information;

namespace JueMingR.Features.Information
{
    public sealed class InfectionSummary
    {
        private InfectionObservation previous;
        private bool hasPrevious;
        public InformationText Content { get; private set; } = new InformationText();
#if DEBUG
        public int TextBuilds { get; private set; }
#endif
        public void Update(InfectionObservation value)
        {
            if (hasPrevious && previous.Availability == value.Availability && previous.Hallow == value.Hallow &&
                previous.Corruption == value.Corruption && previous.Crimson == value.Crimson) return;
            previous = value; hasPrevious = true;
#if DEBUG
            TextBuilds++;
#endif
            Content.Publish(value.Availability != InformationAvailability.Ready ? InformationText.Status("世界感染", value.Availability, "需要世界中有树妖") :
                "世界感染：神圣 " + Percentage(value.Hallow) + "，腐化 " + Percentage(value.Corruption) + "，猩红 " + Percentage(value.Crimson));
        }
        public void Clear() { hasPrevious = false; Content.Clear(); }
        private static string Percentage(int? value) { return value.HasValue && value >= 0 && value <= 100 ? value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%" : "未知"; }
    }
}
