using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace JueMingR.ArchitectureTests
{
    internal static class AnnouncementChecks
    {
        internal static void Check(List<string> failures)
        {
            Type type = Assembly.Load("JueMingR.Features").GetType("JueMingR.Features.Announcements.SafeChatText");
            if (type == null) { failures.Add("G04 bounded safe announcement text missing"); return; }
            Func<string, int, string> clean = (text, length) => (string)type.GetMethod("CleanName").Invoke(null, new object[] { text, length });
            if (clean(" /help\r\n[i:1]玩家", 80).Contains("/") || clean("[c/ff0000:坏]好", 80).Contains("[")) failures.Add("G04 names cannot inject commands or tags");
            if (clean("Á😀B", 2) != "Á😀") failures.Add("G04 name limits preserve complete Unicode text elements");
            string message = (string)type.GetMethod("Build").Invoke(null, new object[] { new[] { "甲", new string('中', 600), "乙" }, 100 });
            if (Encoding.UTF8.GetByteCount(message) > 100 || !message.Contains("甲") || message.Contains("中")) failures.Add("G04 final encoded budget must retain only complete entries including color wrapper");
            if (!message.Contains("省略")) failures.Add("G04 omitted complete entries must be visible within the final byte budget");
            string complete = (string)type.GetMethod("Build").Invoke(null, new object[] { new[] { "甲", "乙" }, 100 });
            if (complete.Contains("省略")) failures.Add("G04 complete announcements must not claim omission");
            if (complete != "[c/FFD966:这里有 甲，乙]") failures.Add("G04 normal announcements retain the shared Legacy sentence and separator");
            string multiple = (string)type.GetMethod("BuildItems").Invoke(null, new object[] { new[] { "8 个 木材", "11 个 火把" }, 100 });
            if (multiple != "[c/FFD966:这里有 8 个 木材和11 个 火把]") failures.Add("G04 distinct dropped-item groups retain both exact quantities joined with 和");
            string notice = (string)type.GetMethod("BuildNotice").Invoke(null, new object[] { new[] { "这里看不见东西" }, 100 });
            if (notice != "[c/FFD966:这里看不见东西]") failures.Add("G04 complete notices must not gain a duplicate normal sentence prefix");
            string icon = (string)type.GetMethod("Build").Invoke(null, new object[] { new[] { "泥土块 [i:2]" }, 100 });
            if (icon != "[c/FFD966:这里有 泥土块 ][i:2]") failures.Add("G04 icons must be siblings of color text, with no literal empty color tag");
            Type clock = type.Assembly.GetType("JueMingR.Features.Announcements.AnnouncementCooldown");
            object cooldown = Activator.CreateInstance(clock);
            Func<long, bool, bool> take = (now, air) => (bool)clock.GetMethod("TryTake").Invoke(cooldown, new object[] { now, air });
            if (!take(1000, true) || take(1001, false) || take(1500, true) || !take(1500, false) || take(1501, true) || take(1999, false) || !take(2000, false) || !take(3000, true)) failures.Add("G04 every announcement requires 500ms, while air has its own 2000ms gate");
            if (take(1, false) || take(300, false) || !take(501, false)) failures.Add("G04 clock rollback must rearm without replaying old action");
        }
    }
}
