using System;
using System.Globalization;
using JueMingR.Platform.Information;

namespace JueMingR.Features.Information
{
    public sealed class AnglerSummary
    {
        private AnglerObservation previous;
        private bool hasPrevious;
        public InformationText Content { get; private set; } = new InformationText();
#if DEBUG
        public int TextBuilds { get; private set; }
#endif
        public void Update(AnglerObservation value)
        {
            if (hasPrevious && previous.Availability == value.Availability && previous.ItemType == value.ItemType &&
                previous.Completed == value.Completed && previous.SubmittedToday == value.SubmittedToday &&
                String.Equals(previous.Name, value.Name, StringComparison.Ordinal) && String.Equals(previous.Location, value.Location, StringComparison.Ordinal)) return;
            previous = value; hasPrevious = true;
#if DEBUG
            TextBuilds++;
#endif
            // The three personal/world facts have separate readiness. A missing
            // quest never substitutes index zero or erases a known count.
            if (value.Availability == InformationAvailability.ConditionUnmet)
            { Content.Publish(InformationText.Status("渔夫任务", value.Availability, "需要在此世界解救过渔夫，或当前有渔夫")); return; }
            string missing = value.Availability == InformationAvailability.Unavailable ? "暂不可用" : value.Availability == InformationAvailability.Waiting ? "等待同步" : "未知";
            Content.Publish("渔夫任务：" + (value.ItemType.HasValue && !String.IsNullOrEmpty(value.Name) ? value.Name : missing) +
                "；地点：" + (String.IsNullOrEmpty(value.Location) ? "未知" : value.Location) +
                "\n累计完成：" + (value.Completed.HasValue && value.Completed >= 0 ? value.Completed.Value.ToString(CultureInfo.InvariantCulture) : "未知") +
                "；今日：" + (!value.SubmittedToday.HasValue ? missing : value.SubmittedToday.Value ? "已提交" : "未提交"));
        }
        public void Clear() { hasPrevious = false; Content.Clear(); }
    }
}
