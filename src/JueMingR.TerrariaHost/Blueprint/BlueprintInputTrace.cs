#if JMR_BLUEPRINT_INPUT_TRACE
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Blueprint
{
    // One ordinary-start investigation, compiled out of ordinary candidates.
    // The single-use file arms observation only; no input/proof/state is changed.
    internal sealed class BlueprintInputTrace
    {
        private struct Row
        {
            internal long Sequence, Frame, Mapping;
            internal int Point, Data, Input, State, Proof, Matches, RawX, RawY, RawButtons, Ui, CellX, CellY, Retain,
                PreviewLeft, PreviewTop, PreviewRight, PreviewBottom;
        }
        private readonly HostBlueprint host;
        private readonly object gate = new object();
        private readonly object writeGate = new object();
        private readonly Row[] rows;
        private readonly long[] arrivals;
        private readonly string output, identity;
        private int count, prelude, firstUnexpected = -1;
        private long sequence, deadline;
        private bool armed, started, attempted;
        private volatile bool stopped;
        private Timer timer;
        private string reason;

        internal BlueprintInputTrace(HostBlueprint host, string gameDirectory)
        {
            this.host = host;
            try
            {
                string root = Path.Combine(gameDirectory, "JueMingRData", "diagnostics", "blueprint-input");
                string token = Path.Combine(root, "armed.once");
                if (!File.Exists(token) || File.ReadAllText(token) != "JMR_BLUEPRINT_INPUT_TRACE_V1") return;
                // Atomic one-use claim occurs before sampling; a later ordinary
                // restart cannot silently run the recorder again, even on crash.
                string id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Process.GetCurrentProcess().Id;
                File.Move(token, Path.Combine(root, "claimed-" + id + ".txt"));
                rows = new Row[32768]; arrivals = new long[24];
                output = Path.Combine(root, "trace-" + id + ".log");
                Assembly assembly = typeof(BlueprintInputTrace).Assembly;
                identity = "host-path=" + assembly.Location + "\nhost-mvid=" + assembly.ManifestModule.ModuleVersionId +
                    "\nhost-build=" + assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion +
                    "\ngame-mvid=" + typeof(Main).Assembly.ManifestModule.ModuleVersionId + "\nframe-skip=" + Main.FrameSkipMode;
                armed = true;
                AppDomain.CurrentDomain.ProcessExit += Exit;
            }
            catch { stopped = true; }
        }

        // Points: 0 outer update; 1 enter-before; 2 enter-after; 3 mapping;
        // 4 input-before; 5 input-after; 6 capture; 7 begin-entry; 8 boundary-entry;
        // 9 resources-entry; 10 end-entry; 11 consume-decision; 12 begin-adopt;
        // 13 boundary-adopt; 14 resources-adopt; 15 end-adopt; 16 world-draw;
        // 17 interrupt; 18 cancel; 19 exit; 20 draw-output; 21 consume-after;
        // 22 native MouseOver boundary returned (not a certificate producer).
        internal void Record(int point, int data = 0)
        {
            if (!armed || stopped) return;
            try
            {
                var input = host.Input; var state = host.State; var pointer = host.Pointer;
                var mouse = PlayerInput.MouseInfo;
                var row = new Row { Point = point, Data = data, Frame = input.Frame, Mapping = input.Mapping,
                    Input = input.BlueprintDiagnosticFlags,
                    State = (state.Active ? 1 : 0) | (state.NeedsNeutral ? 2 : 0) | (state.Dragging ? 4 : 0) | (state.Selection.HasSelection ? 8 : 0),
                    Proof = pointer.DiagnosticFlags, Matches = pointer.DiagnosticMatches,
                    RawX = mouse.X, RawY = mouse.Y, RawButtons = (mouse.LeftButton == ButtonState.Pressed ? 1 : 0) | (mouse.RightButton == ButtonState.Pressed ? 2 : 0),
                    Ui = (host.NativeUiBusy ? 1 : 0) | (Main.LocalPlayer.mouseInterface ? 2 : 0) | (Main.mouseText ? 4 : 0) |
                        (host.Shell?.State.Visible == true ? 8 : 0) | (host.CanEdit ? 16 : 0) | (host.CanRetainEdit ? 32 : 0) |
                        (Main.LocalPlayer.spectating >= 0 ? 64 : 0),
                    CellX = pointer.CellX, CellY = pointer.CellY, Retain = host.DiagnosticRetainReasons,
                    PreviewLeft = state.Preview.Left, PreviewTop = state.Preview.Top,
                    PreviewRight = state.Preview.RightExclusive, PreviewBottom = state.Preview.BottomExclusive };
                bool stop = false;
                lock (gate)
                {
                    if (stopped) return;
                    row.Sequence = ++sequence; arrivals[point]++;
                    if (!started && point != 1)
                    {
                        // Preserve bounded pre-entry context without spending
                        // the recording window while the owner loads a world.
                        rows[prelude++ % 128] = row; return;
                    }
                    if (!started)
                    {
                        int n = Math.Min(prelude, 128);
                        var ordered = new Row[n];
                        for (int i = 0; i < n; i++) ordered[i] = rows[(prelude - n + i) % 128];
                        Array.Copy(ordered, rows, n); count = n; started = true;
                        deadline = Stopwatch.GetTimestamp() + 30 * Stopwatch.Frequency;
                        timer = new Timer(_ => Stop("30-seconds"), null, 30000, Timeout.Infinite);
                    }
                    if (count < rows.Length) rows[count++] = row;
                    // Caller marks only a failed native certificate/geometry or
                    // untrusted input, never expected Enter/Exit/UI/neutral wait.
                    if (data < 0 && firstUnexpected < 0 && (row.Retain & (4 | 16 | 32 | 64 | 128 | 256 | 512 | 1024)) == 0)
                        firstUnexpected = count - 1;
                    stop = count == rows.Length || point == 19 || Stopwatch.GetTimestamp() >= deadline;
                }
                if (stop) Stop(point == 19 ? "capture-exit" : count == rows.Length ? "capacity" : "30-seconds");
            }
            catch { Stop("observer-failure"); }
        }

        private void Exit(object sender, EventArgs args) { Stop("process-exit", true); }
        private void Stop(string why, bool exiting = false)
        {
            lock (gate)
            {
                if (!armed || !started) { stopped = true; return; }
                if (!stopped) { stopped = true; reason = why; timer?.Dispose(); }
            }
            if (exiting) Export(true); else ThreadPool.QueueUserWorkItem(_ => Export());
        }
        private void Export(bool exiting = false)
        {
            bool acquired = false;
            try
            {
                // A normal exit may race the timer's one export. Wait only a
                // bounded interval for that writer; crashes/forced termination
                // and slow or failed storage can still leave incomplete data.
                acquired = Monitor.TryEnter(writeGate, exiting ? 250 : 0);
                if (!acquired) return;
                lock (gate) { if (attempted) return; attempted = true; }
                var text = new StringBuilder(4 * 1024 * 1024);
                text.AppendLine("JMR_BLUEPRINT_INPUT_TRACE_V1; behavior baseline; one-use; stop=" + reason);
                text.AppendLine(identity); text.AppendLine("first-unexpected-row=" + firstUnexpected);
                text.AppendLine("input bits:focused=1,sampleFocused=2,nativePermission=4,mapped=8,finalized=16,rearming=32,quarantine=64,canStart=128,retain=256");
                text.AppendLine("state bits:active=1,needsNeutral=2,dragging=4,selected=8");
                text.AppendLine("proof bits:pending=1,completed=2,inPass=4,boundary=8,resources=16,ui=32,left=64,right=128,drawn=256,freshLeft=512,hasPoint=1024");
                text.AppendLine("matches bits:pending=1,retain=2,mapping=4,raw=8,screen=16,rawScale=32,uiMatrix=64,zoom=128,gravity=256; all=511");
                text.AppendLine("ui bits:nativeBusy=1,mouseInterface=2,mouseText=4,shell=8,canEdit=16,retain=32,spectating=64; raw is native MouseInfo, trusted only with sampleFocused/nativePermission");
                text.AppendLine("retain rejection bits:session=1,inputRetain=2,nativeUi=4,paused=8,dead=16,ghost=32,shellVisible=64,shellPointer=128,targetGesture=256,mapTrigger=512,cameraTrigger=1024");
                text.AppendLine("points:0=outer,1=enterBefore,2=enterAfter,3=mapping,4=inputBefore,5=inputAfter,6=capture,7=beginEntry,8=boundaryEntry,9=resourcesEntry,10=endEntry,11=consumeDecision,12=beginAdopt,13=boundaryAdopt,14=resourcesAdopt,15=endAdopt,16=worldDraw,17=interrupt,18=cancel,19=exit,20=drawOutput,21=consumeAfter,22=boundaryReturned");
                text.AppendLine("data:entry Begin/Boundary/Resources/End/Returned=runOriginal; consume=valid1+certified2+ui4+drawn8+left16+fresh32 (negative encodes -bits-1); interrupt -1=mapping-trust,-2=input-gate,-3=runtime-retain,-4=geometry,-5=world-point,-6=begin-match,-7=end-certificate,-8=UI-exception; expected interrupt 1=input-UI,2=runtime-UI/dead,9=session-start,10=session-end,11=fail-closed,12=dispose; Enter/Exit are expected");
                for (int i = 0; i < arrivals.Length; i++) text.AppendLine("arrival," + i + "," + arrivals[i]);
                text.AppendLine("sequence,frame,mapping,point,data,input,state,proof,matches,rawX,rawY,rawButtons,ui,cellX,cellY,retainRejections,previewLeft,previewTop,previewRight,previewBottom");
                for (int i = 0; i < count; i++)
                {
                    Row r = rows[i];
                    text.AppendLine(r.Sequence + "," + r.Frame + "," + r.Mapping + "," + r.Point + "," + r.Data + "," + r.Input + "," + r.State + "," + r.Proof + "," + r.Matches + "," + r.RawX + "," + r.RawY + "," + r.RawButtons + "," + r.Ui + "," + r.CellX + "," + r.CellY + "," + r.Retain + "," + r.PreviewLeft + "," + r.PreviewTop + "," + r.PreviewRight + "," + r.PreviewBottom);
                }
                byte[] bytes = Encoding.UTF8.GetBytes(text.ToString());
                if (bytes.Length > 8 * 1024 * 1024) return;
                using (var file = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) file.Write(bytes, 0, bytes.Length);
            }
            catch { /* Evidence failure cannot change the editor or game. */ }
            finally { if (acquired) Monitor.Exit(writeGate); }
        }
    }
}
#endif
