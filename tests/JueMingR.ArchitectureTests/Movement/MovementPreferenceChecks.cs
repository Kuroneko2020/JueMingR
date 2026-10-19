using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using JueMingR.Features.Movement;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class MovementPreferenceChecks
    {
        internal static void Check(List<string> failures)
        { try { Format(); Storage(); } catch (Exception error) { failures.Add("Movement preference: " + error); } }
        private static void Format()
        {
            var codec = new MovementPreferenceCodec(); var initial = new MovementOptions();
            Require(!initial.SimulatedJumpEnabled && !initial.ContinuousDashEnabled && !initial.TeleportCorrectionEnabled && initial.DashMode == ContinuousDashMode.DoubleTap, "first defaults all off/double tap");
            for (int bits = 0; bits < 8; bits++) foreach (ContinuousDashMode mode in new[] { ContinuousDashMode.Hold, ContinuousDashMode.DoubleTap })
            { var value = new MovementOptions((bits & 1) != 0, (bits & 2) != 0, (bits & 4) != 0, mode); Require(codec.Decode(codec.Encode(value)).Equals(value), "independent settings round trip"); }
            string valid = Encoding.UTF8.GetString(codec.Encode(initial));
            Reject(codec, valid.Replace("\"doubleTap\"", "\"off\""), PreferenceStatus.Invalid);
            Reject(codec, valid.Replace("\"version\":1", "\"version\":2"), PreferenceStatus.UnsupportedVersion);
            Reject(codec, valid.Replace("\"simulatedJump\":false,", ""), PreferenceStatus.Invalid);
            Reject(codec, valid.Replace("\"dashMode\":", "\"unknown\":true,\"dashMode\":"), PreferenceStatus.UnknownFields);
            Reject(codec, valid.Replace("\"continuousDash\":false", "\"continuousDash\":false,\"continuousDash\":true"), PreferenceStatus.Invalid);
        }
        private static void Storage()
        {
            string root = Path.Combine(Path.GetTempPath(), "JueMingR-movement-preferences-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            string path = Path.Combine(root, "movement.json"); bool stopped = true;
            try
            {
                With(path, document =>
                {
                    Require(!File.Exists(path), "missing defaults do not write a document");
                    Require(document.Set(document.Snapshot.Value.WithMode(ContinuousDashMode.Hold)), "mode changes while dash disabled");
                    Require(!document.Snapshot.Value.ContinuousDashEnabled, "mode change does not enable dash");
                }, ref stopped);
                With(path, document =>
                {
                    Require(document.Snapshot.Value.DashMode == ContinuousDashMode.Hold && !document.Snapshot.Value.ContinuousDashEnabled, "disabled mode persists across owner restart");
                    document.Set(document.Snapshot.Value.WithEnabled(1, true));
                    Require(document.Snapshot.Value.DashMode == ContinuousDashMode.Hold, "reenable keeps current mode");
                }, ref stopped);
                string bad = "{\"format\":\"JueMingR.Movement\",\"version\":99}"; File.WriteAllText(path, bad, new UTF8Encoding(false));
                With(path, document => { Require(document.Snapshot.IsProtected, "future version protected"); document.Set(new MovementOptions(true)); }, ref stopped);
                Require(File.ReadAllText(path) == bad, "protected source not overwritten by in-memory choice");
            }
            finally { if (stopped) Directory.Delete(root, true); }
        }
        private static void With(string path, Action<PreferenceDocument<MovementOptions>> test, ref bool stopped)
        {
            var document = new PreferenceDocument<MovementOptions>(new AtomicFileDocument(path, 65536, true), new MovementPreferenceCodec(), new MovementOptions()); stopped = false;
            try { var timer = System.Diagnostics.Stopwatch.StartNew(); while (!document.Snapshot.IsLoaded && timer.ElapsedMilliseconds < 3000) Thread.Sleep(5); Require(document.Snapshot.IsLoaded, "load bounded"); test(document); }
            finally { stopped = document.Stop(3000); }
            Require(stopped, "worker released isolated root");
        }
        private static void Reject(MovementPreferenceCodec codec, string text, PreferenceStatus expected)
        { try { codec.Decode(Encoding.UTF8.GetBytes(text)); } catch (PreferenceFormatException error) { Require(error.Status == expected, "specific protected cause"); return; } throw new InvalidOperationException("invalid accepted"); }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
