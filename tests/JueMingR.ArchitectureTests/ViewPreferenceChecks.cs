using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using JueMingR.Features.View;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class ViewPreferenceChecks
    {
        internal static void Check(List<string> failures)
        {
            foreach (Action test in new Action[] { Format, Storage })
                try { test(); } catch (Exception e) { failures.Add("View preference: " + e.Message); }
        }
        private static void Format()
        {
            var codec = new ViewPreferenceCodec();
            Require(!codec.Decode(codec.Encode(false)) && codec.Decode(codec.Encode(true)), "boolean round trips");
            Reject(codec, "{\"format\":\"JueMingR.View\",\"version\":1,\"enabled\":\"true\"}", PreferenceStatus.Invalid);
            Reject(codec, "{\"format\":\"JueMingR.View\",\"version\":2,\"enabled\":true}", PreferenceStatus.UnsupportedVersion);
            Reject(codec, "{\"format\":\"JueMingR.View\",\"version\":1,\"enabled\":true,\"zoom\":0.5}", PreferenceStatus.UnknownFields);
            Reject(codec, "{\"format\":\"JueMingR.View\",\"version\":1}", PreferenceStatus.Invalid);
            Reject(codec, "{\"format\":\"JueMingR.View\",\"version\":1,\"enabled\":false,\"enabled\":true}", PreferenceStatus.Invalid);
        }
        private static void Storage()
        {
            string root = Path.Combine(Path.GetTempPath(), "JueMingR-ViewPreference-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "view-unlock.json"); bool stopped = true;
            try
            {
                WithDocument(path, setting =>
                {
                    Require(!setting.Snapshot.Value && setting.Snapshot.Status == PreferenceStatus.Missing, "missing preference defaults off");
                    Require(!File.Exists(path) && !setting.Set(false), "default selection does not generate a JSON file");
                    Require(setting.Set(true) && setting.Snapshot.Value, "accepted selection applies in memory");
                }, ref stopped);
                Require(new ViewPreferenceCodec().Decode(File.ReadAllBytes(path)), "accepted boolean is saved on stop");
                WithDocument(path, setting => Require(setting.Snapshot.Value, "new owner restores only enabled preference"), ref stopped);
                const string future = "{\"format\":\"JueMingR.View\",\"version\":2,\"enabled\":true,\"zoom\":0.25}";
                File.WriteAllText(path, future, new UTF8Encoding(false));
                WithDocument(path, setting =>
                {
                    Require(!setting.Snapshot.Value && setting.Snapshot.IsProtected, "unsupported document retains default and protects source");
                    Require(setting.Set(true) && setting.Snapshot.Value, "protected preference can still change for this process");
                }, ref stopped);
                Require(File.ReadAllText(path) == future, "unsupported source bytes are not rewritten");
            }
            finally
            {
                // Never delete a root still owned by an unjoined I/O worker.
                if (stopped) Directory.Delete(root, true);
                else Console.WriteLine("View preference worker still owns isolated test root: " + root);
            }
        }
        private static void WithDocument(string path, Action<PreferenceDocument<bool>> test, ref bool stopped)
        {
            var setting = new PreferenceDocument<bool>(new AtomicFileDocument(path, 65536, true), new ViewPreferenceCodec(), false);
            stopped = false;
            try
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                while (!setting.Snapshot.IsLoaded && timer.ElapsedMilliseconds < 3000) Thread.Sleep(5);
                Require(setting.Snapshot.IsLoaded, "isolated document load completed");
                test(setting);
            }
            finally { stopped = setting.Stop(3000); }
            Require(stopped, "isolated document worker stopped");
        }
        private static void Reject(ViewPreferenceCodec codec, string json, PreferenceStatus status)
        {
            try { codec.Decode(Encoding.UTF8.GetBytes(json)); }
            catch (PreferenceFormatException e) { Require(e.Status == status, "specific protected format cause"); return; }
            throw new InvalidOperationException("invalid preference accepted");
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
    }
}
