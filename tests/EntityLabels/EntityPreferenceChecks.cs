using System;
using System.IO;
using System.Reflection;
using System.Threading;
using JueMingR.Features.EntityLabels;
using JueMingR.Platform.Runtime;
using JueMingR.Platform.Settings;
using JueMingR.TerrariaHost.EntityLabels;

namespace Terraria
{
    internal static class EntityPreferenceChecks
    {
        internal static void Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "JueMingR-EntityPreferences-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            bool allStopped = true;
            try
            {
                string path = Path.Combine(root, "JueMingRData", "config", "features", "entity-labels.json");
                var runtime = new SingleFeatureRuntime(new Probe(), new Idle());
                var host = new HostEntityLabels(root, runtime); var document = Document(host);
                try
                {
                    Wait(() => host.Preferences.IsLoaded); host.PollPreferences();
                    Check(!File.Exists(path), "first read does not create or pre-enable settings");
                    host.SetNpcMode(NpcLabelMode.Type); host.Toggle(EntityLabelKind.Npc);
                    host.SetColor(EntityLabelKind.Enemy, 0x123456); host.StepSize(EntityLabelKind.Enemy, 1);
                    host.SetColor(EntityLabelKind.Critter, 0x112233);
                    Wait(() => host.Preferences.Status == PreferenceStatus.Saved);
                    Check(host.Preferences.Value.NpcMode == NpcLabelMode.Off && host.Preferences.Value.LastNpcMode == NpcLabelMode.Type &&
                        host.Preferences.Value.EnemyStyle.Rgb == 0x123456 && host.Preferences.Value.EnemyStyle.NameSize == 100 && host.Preferences.Value.CritterStyle.Rgb == 0x112233,
                        "real command consumer coalesces independent revisions");
                    var decoded = new EntityLabelCodec().Decode(File.ReadAllBytes(path));
                    Check(decoded.Equals(host.Preferences.Value), "actual disk has newest submitted document");
                    File.WriteAllText(path, "external edit"); host.SetColor(EntityLabelKind.Enemy, 0x654321);
                    Wait(() => host.Preferences.Status == PreferenceStatus.Conflict);
                    Check(File.ReadAllText(path) == "external edit" && host.Preferences.Value.EnemyStyle.Rgb == 0x654321, "external conflict protects bytes while current memory choice stays truthful");
                    int messages = 0; host.TakeFeedback(_ => messages++); host.TakeFeedback(_ => messages++);
                    Check(messages == 1 && host.PreferenceMessage != null, "closed popup still has one finite failure notification");
                }
                finally { allStopped &= document.Stop(3000); Check(allStopped, "stop before releasing isolated root"); }
                File.WriteAllBytes(path, new EntityLabelCodec().Encode(EntityLabelSettings.Default.WithNpcMode(NpcLabelMode.Type).WithNpcMode(NpcLabelMode.Off)));
                var reopened = new HostEntityLabels(root, runtime); var second = Document(reopened);
                try { Wait(() => reopened.Preferences.IsLoaded); reopened.Toggle(EntityLabelKind.Npc); Check(reopened.Preferences.Value.NpcMode == NpcLabelMode.Type, "persisted function mode survives new owner"); }
                finally { allStopped &= second.Stop(3000); Check(allStopped, "reopened worker stopped"); }
            }
            finally
            {
                string full = Path.GetFullPath(root);
                Check(Path.GetDirectoryName(full).Equals(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileName(full).StartsWith("JueMingR-EntityPreferences-", StringComparison.Ordinal), "exact isolated root");
                if (allStopped) Directory.Delete(full, true);
            }
            Console.WriteLine("PASS: entity commands, isolated atomic file revisions, conflict feedback and restored NPC mode.");
        }
        private static PreferenceDocument<EntityLabelSettings> Document(HostEntityLabels host)
        { return (PreferenceDocument<EntityLabelSettings>)typeof(HostEntityLabels).GetField("preferences", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host); }
        private static void Wait(Func<bool> test) { Check(SpinWait.SpinUntil(test, 3000), "background preference completion"); }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private sealed class Probe : IGameSessionProbe { public bool IsSessionActive { get { return true; } } }
        private sealed class Idle : IRuntimeFeature
        { public bool Enabled { get { return false; } } public void OnSessionStarted() { } public void OnSessionEnded() { } public void Update(ulong tick) { } public void FailClosed() { } }
    }
}
