#if JMR_TARGET_GESTURE_DIAGNOSTIC
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using Terraria;

namespace JueMingR.TerrariaHost.ItemBrowser
{
    // One-use observation only. No callback here can grant target proof, call a
    // business admission predicate, prepare a native caller, or alter input.
    internal static class TargetGestureTrace
    {
        private struct Row { internal long Time, Tick, Mapping, Pass; internal ulong Game; internal int Request, Kind, Point, State, Reasons, Detail; }
        private static readonly object gate = new object();
        private static readonly Row[] rows = new Row[512];
        private static int count, request, kind;
        private static long deadline, pass;
        private static bool initialized, active, exported, requestActive, claimed;
        private static string output, identity, reason;
        private static string errorType, errorStage;
        private static int errorPoint, errorHResult;
        private static string metadataErrorType, metadataErrorStage;
        private static int metadataErrorHResult, metadataErrors;
        private static Timer timer;
        internal static bool Active { get { lock (gate) return active; } }
        internal static bool RequestActive { get { lock (gate) return active && requestActive; } }

        internal static void Install(int stage)
        {
            string phase = "install.preload-state";
            try
            {
                bool skip = Main.SkipAssemblyLoad, loaded = Terraria.Program.LoadedEverything;
                lock (gate)
                {
                    if (!initialized)
                    {
                        initialized = true;
                        phase = "install.arm";
                        string directory = Path.Combine(Path.GetDirectoryName(typeof(Main).Assembly.Location), "JueMingRData", "diagnostics", "target-gesture");
                        string arm = Path.Combine(directory, "armed");
                        if (!File.Exists(arm)) return;
                        Guid run;
                        if (!Guid.TryParse(File.ReadAllText(arm).Trim(), out run)) return;
                        output = Path.Combine(directory, run.ToString("N") + ".log");
                        // The durable claim survives ordinary restart. Reusing
                        // the same explicit arm never starts another recording.
                        phase = "install.claim";
                        using (new FileStream(Path.Combine(directory, run.ToString("N") + ".claimed"), FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
                        claimed = true;
                        phase = "install.identity";
                        identity = "host=" + typeof(TargetGestureTrace).Assembly.ManifestModule.ModuleVersionId +
                            " hostVersion=" + typeof(TargetGestureTrace).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion +
                            " native=" + typeof(Main).Module.ModuleVersionId + " bits=" + (IntPtr.Size * 8) + " clr=" + Environment.Version +
                            " resourceExceptions=unobserved diagnosticInitialization=gate-read-claim-metadata-before-target-patch\n";
                        active = true; deadline = Stopwatch.GetTimestamp() + 600L * Stopwatch.Frequency;
                        phase = "install.timer";
                        AppDomain.CurrentDomain.ProcessExit += Exit;
                        timer = new Timer(_ => Stop("time-limit"), null, 600000, Timeout.Infinite);
                    }
                }
                Record(1, 0, 0, 0, 0, stage | (skip ? 4 : 0) | (loaded ? 8 : 0));
            }
            catch (Exception error) { Fail(error, 1, phase); }
        }
        private static string PatchOwners(Type type, string name)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var method in methods)
                if (method.Name == name)
                {
                    var patches = Harmony.GetPatchInfo(method);
                    return "owners=" + type.FullName + "." + name + ":" + (patches == null ? "none" : string.Join("|", patches.Owners)) + "\n";
                }
            return "owners=" + type.FullName + "." + name + ":missing\n";
        }
        private static void AppendOwners(Type type, string name, string phase)
        {
            try { identity += PatchOwners(type, name); }
            catch (Exception error)
            {
                // Harmony's optional patch introspection must not erase the
                // business event stream. Keep the first fixed-stage failure;
                // there are only six one-time metadata entries, no retry loop.
                metadataErrors++;
                if (metadataErrorType == null)
                {
                    metadataErrorType = error.GetType().FullName;
                    metadataErrorHResult = error.HResult; metadataErrorStage = phase;
                }
            }
        }

        internal static void Request(int requestKind, long tick, long mapping, long expires)
        {
            string phase = "request.admission";
            try
            {
                lock (gate)
                {
                    if (!active) return;
                    long now = Stopwatch.GetTimestamp();
                    // Timer delivery can be late. A real request must never
                    // revive an expired recorder or extend its installation
                    // limit; the first-request window can only shorten it.
                    if (now >= deadline) { Stop("time-limit"); return; }
                    if (request >= 2) { Stop("two-requests"); return; }
                    request++; kind = requestKind; requestActive = true;
                    if (request == 1) deadline = Math.Min(deadline, now + 120L * Stopwatch.Frequency);
                    // Keep proof that the real business Request arrived even
                    // if optional Harmony metadata subsequently fails.
                    phase = "request.row";
                    Record(2, tick, mapping, 0, 0, (int)(expires - tick));
                    if (!active) return;
                    if (request == 1)
                    {
                        // Blueprint installs later than Targets. Snapshot the
                        // final owner set once at the first real request, not
                        // midway through Runtime construction.
                        AppendOwners(typeof(Main), "DrawInterface", "request.owners.DrawInterface");
                        AppendOwners(typeof(Main), "DrawInterface_39_MouseOver", "request.owners.MouseOver");
                        AppendOwners(typeof(Terraria.GameContent.UI.ResourceSets.PlayerResourceSetsManager), "TryToHoverOverResources", "request.owners.Manager");
                        AppendOwners(typeof(Terraria.GameContent.UI.ResourceSets.ClassicPlayerResourcesDisplaySet), "TryToHover", "request.owners.Classic");
                        AppendOwners(typeof(Terraria.GameContent.UI.ResourceSets.FancyClassicPlayerResourcesDisplaySet), "TryToHover", "request.owners.Fancy");
                        AppendOwners(typeof(Terraria.GameContent.UI.ResourceSets.HorizontalBarsPlayerResourcesDisplaySet), "TryToHover", "request.owners.Horizontal");
                        phase = "request.timer";
                        long remaining = deadline - Stopwatch.GetTimestamp();
                        if (remaining <= 0) { Stop("time-limit"); return; }
                        timer.Change((int)Math.Ceiling(remaining * 1000.0 / Stopwatch.Frequency), Timeout.Infinite);
                    }
                }
            }
            catch (Exception error) { Fail(error, 2, phase); }
        }

        internal static void Record(int point, long tick, long mapping, int state, int reasons, int detail)
        {
            try
            {
                bool stop = false;
                lock (gate)
                {
                    if (!active) return;
                    if (Stopwatch.GetTimestamp() >= deadline) { Stop("time-limit"); return; }
                    // Resource callbacks arrive through Blueprint's existing
                    // postfix before its business early-outs. They are events,
                    // never a certificate supplied to the target owner.
                    if (point != 1 && !requestActive) return;
                    if (point == 3) pass++;
                    rows[count] = new Row { Time = Stopwatch.GetTimestamp(), Tick = tick, Mapping = mapping, Game = Main.GameUpdateCount,
                        Request = request, Kind = kind, Pass = pass, Point = point, State = state, Reasons = reasons, Detail = detail };
                    count++;
                    // Every native Draw boundary is explicit in the stream;
                    // resource rows are correlated with that nearest boundary.
                    if (point == 10 || point == 11) requestActive = false;
                    // The owner card has two actions. Export on their second
                    // terminal event instead of depending on a late ProcessExit
                    // handler behind other components' bounded shutdown work.
                    stop = count == rows.Length || !requestActive && request == 2;
                }
                if (stop) Stop(count == rows.Length ? "512-rows" : "two-requests");
            }
            catch (Exception error) { Fail(error, point, "record"); }
        }
        internal static void Resource(bool original, int implementation)
        { Record(6, 0, 0, 0, 0, implementation | (original ? 16 : 0)); }
        internal static void Fail(Exception error, int point, string phase)
        {
            try
            {
                lock (gate)
                {
                    if (!claimed || exported) return;
                    errorType = error.GetType().FullName; errorHResult = error.HResult; errorPoint = point; errorStage = phase;
                    // A failed observer retires sampling but must preserve its
                    // bounded prefix. Stop owns the single export attempt;
                    // export failure never recursively calls this method.
                    Stop("observer-failure");
                }
            }
            catch { lock (gate) { active = false; try { timer?.Dispose(); } catch { } } }
        }
        private static void Exit(object sender, EventArgs args) { Stop("process-exit"); }
        private static void Stop(string why)
        {
            try
            {
                lock (gate)
                {
                    if (!claimed || exported) return;
                    active = false; exported = true; reason = why; timer?.Dispose();
                    // A single bounded export, including on normal process
                    // exit. The capacity-ending callback can do this one I/O;
                    // sampling itself owns no per-frame strings or file writes.
                    var text = new StringBuilder(identity);
                    text.AppendLine("stop=" + reason + " rows=" + count.ToString(CultureInfo.InvariantCulture));
                    if (errorType != null) text.AppendLine("observerErrorType=" + errorType + " hresult=" + errorHResult.ToString(CultureInfo.InvariantCulture) +
                        " point=" + errorPoint.ToString(CultureInfo.InvariantCulture) + " stage=" + errorStage);
                    if (metadataErrorType != null) text.AppendLine("metadataErrors=" + metadataErrors.ToString(CultureInfo.InvariantCulture) +
                        " firstType=" + metadataErrorType + " hresult=" + metadataErrorHResult.ToString(CultureInfo.InvariantCulture) + " stage=" + metadataErrorStage);
                    text.AppendLine("points=1-install,2-request,3-begin,4-mouseover,5-target-resource,6-independent-resource,7-end,8-finalizer,9-update,10-delivery,11-cancel,12-input,13-admission,14-placement");
                    text.AppendLine("state=pending:0..2,picking:4,completed:8,inPass:16,slot:32,uiBlocked:64,uiBoundary:128,resourceBoundary:256,preparing:512");
                    text.AppendLine("reasons=focus:1,player:2,world:4,tile:8,connection:16,raw:32,screen:64,uiMatrix:128,zoom:256,mouseScale:512,camera:1024,gravity:2048,inventory:4096,chest:8192,noPending:16384");
                    text.AppendLine("resourceDetail=implementation:1-classic/2-fancy/3-horizontal,original:16;updateDetail=expired:1,menu:2,focus:4,session:8;deliveryDetail=valid:1,target:2,placement:4");
                    text.AppendLine("requestDetail=expiresMinusTick;placementDetail=0-notReady/1-resolveRejected/2-resolved/3-exception;inputDetail=canStart:1,escape:2,right:4,rawPick:8");
                    text.AppendLine("time,request,kind,pass,point,tick,mapping,game,state,reasons,detail");
                    for (int i = 0; i < count; i++)
                    {
                        var r = rows[i];
                        text.Append(r.Time).Append(',').Append(r.Request).Append(',').Append(r.Kind).Append(',').Append(r.Pass).Append(',').Append(r.Point).Append(',')
                            .Append(r.Tick).Append(',').Append(r.Mapping).Append(',').Append(r.Game).Append(',').Append(r.State).Append(',')
                            .Append(r.Reasons).Append(',').Append(r.Detail).AppendLine();
                    }
                    using (var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(text.ToString());
                }
            }
            // The export attempt was claimed before touching the file. Never
            // retry or recursively export a failing export, even on shutdown.
            catch { lock (gate) { active = false; try { timer?.Dispose(); } catch { } } }
        }
    }
}
#endif
