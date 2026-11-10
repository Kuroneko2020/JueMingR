using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using JueMingR.Platform.Blueprint;
using static JueMingR.Infrastructure.Blueprint.WindowsBlueprintDialogInterop;

namespace JueMingR.Infrastructure.Blueprint
{
    public sealed class WindowsBlueprintFileDialog : IBlueprintFileDialog
    {
        private static int processSlot;
        private readonly object gate = new object();
        private BlueprintFileDialogRequest active;
        private BlueprintFileDialogResult result;
        private bool stopping, failed, closeRequested, showing;
        private IntPtr messageWindow;
        private const uint CloseMessage = 0x8001, NameMessage = 0x8002;
        internal bool IsShowing { get { lock (gate) return showing; } }

        public bool TryShow(BlueprintFileDialogRequest request)
        {
            if (request == null) return false;
            uint ownerProcess;
            uint ownerThread = GetWindowThreadProcessId(request.OwnerWindow, out ownerProcess);
            if (ownerThread == 0 || ownerProcess != GetCurrentProcessId() || !IsWindow(request.OwnerWindow)) return false;
            lock (gate)
            {
                if (stopping || failed || active != null || result != null || Interlocked.CompareExchange(ref processSlot, 1, 0) != 0) return false;
                active = request; closeRequested = false;
                try
                {
                    var thread = new Thread(() => Run(request, ownerThread)) { IsBackground = true, Name = "JueMingR.BlueprintDialog" };
                    thread.SetApartmentState(ApartmentState.STA); thread.Start(); return true;
                }
                catch { active = null; Interlocked.Exchange(ref processSlot, 0); failed = true; return false; }
            }
        }
        public bool TryTake(out BlueprintFileDialogResult value)
        {
            lock (gate)
            {
                value = result; if (value == null) return false;
                result = null; active = null; Interlocked.Exchange(ref processSlot, 0); return true;
            }
        }
        public void RequestClose(Guid requestId)
        {
            lock (gate)
            {
                if (active == null || active.RequestId != requestId || result != null) return;
                closeRequested = true;
                if (messageWindow != IntPtr.Zero) PostMessage(messageWindow, CloseMessage, IntPtr.Zero, IntPtr.Zero);
            }
        }
        public void BeginStop()
        {
            lock (gate)
            {
                stopping = true; closeRequested = true;
                if (messageWindow != IntPtr.Zero) PostMessage(messageWindow, CloseMessage, IntPtr.Zero, IntPtr.Zero);
            }
        }
        private bool Closing { get { lock (gate) return closeRequested || stopping; } }

        private void Run(BlueprintFileDialogRequest request, uint ownerThread)
        {
            IFileDialog dialog = null; IShellItem selected = null;
            Events events = null; PumpWindow pump = null; uint cookie = 0; bool advised = false, initialized = false;
            BlueprintFileDialogOutcome outcome = BlueprintFileDialogOutcome.Failed;
            WindowsBlueprintFileTicket ticket = null; string error = "dialog-unavailable"; long counter = request.CounterCandidate;
            try
            {
                int init = CoInitializeEx(IntPtr.Zero, 0x2 | 0x4);
                if (init < 0) Marshal.ThrowExceptionForHR(init);
                initialized = true; // S_OK and S_FALSE each own one matching uninitialize.
                pump = new PumpWindow();
                Guid clsid = request.Kind == BlueprintFileDialogKind.Import
                    ? new Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7") : new Guid("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B");
                dialog = (IFileDialog)Activator.CreateInstance(Type.GetTypeFromCLSID(clsid, true));
                IFileDialog staDialog = dialog;
                events = new Events(request, dialog, () => PostMessage(pump.Handle, NameMessage, IntPtr.Zero, IntPtr.Zero));
                Events staEvents = events;
                pump.Dispatch = message =>
                {
                    try
                    {
                        // This handler executes inside Show's STA modal pump.
                        // RequestClose never calls an RCW from the game thread.
                        if (message == CloseMessage && Closing) staDialog.Close(Cancelled);
                        else if (message == NameMessage && !Closing) staEvents.ContinueName();
                    }
                    catch { staEvents.Fail(); }
                };
                lock (gate) messageWindow = pump.Handle;
                uint options; dialog.GetOptions(out options);
                options |= 0x40 | 0x800 | 0x8 | 0x02000000; // filesystem, path exists, no process CWD mutation, no recent-list path persistence
                options &= ~(0x200u | 0x20u); // one file, no folder picker
                options |= request.Kind == BlueprintFileDialogKind.Import ? 0x1000u : 0x2u | 0x4u | 0x8000u;
                dialog.SetOptions(options);
                Filter[] filters = request.Kind == BlueprintFileDialogKind.Import
                    ? new[] { new Filter { Name = "蓝图 (*.jmbp;*.json)", Pattern = "*.jmbp;*.json" }, new Filter { Name = "JueMingR 蓝图 (*.jmbp)", Pattern = "*.jmbp" }, new Filter { Name = "小助手蓝图 (*.json)", Pattern = "*.json" } }
                    : new[] { new Filter { Name = "JueMingR 蓝图 (*.jmbp)", Pattern = "*.jmbp" } };
                dialog.SetFileTypes((uint)filters.Length, filters);
                dialog.SetFileTypeIndex(1); dialog.SetDefaultExtension("jmbp");
                dialog.SetTitle(request.Kind == BlueprintFileDialogKind.Import ? "导入蓝图" : "导出蓝图");
                if (request.Kind == BlueprintFileDialogKind.Export) dialog.SetFileName(request.SuggestedLeaf);
                dialog.Advise(events, out cookie); advised = true;
                uint pid;
                if (!IsWindow(request.OwnerWindow) || GetWindowThreadProcessId(request.OwnerWindow, out pid) != ownerThread || pid != GetCurrentProcessId())
                    throw new InvalidOperationException("dialog-owner-invalid");
                if (Closing) outcome = BlueprintFileDialogOutcome.Cancelled;
                else
                {
                    lock (gate) showing = true;
                    int show = dialog.Show(request.OwnerWindow);
                    lock (gate) showing = false;
                    if (events.Name != null) counter = events.Name.CounterCandidate;
                    if (Closing || show == Cancelled) outcome = BlueprintFileDialogOutcome.Cancelled;
                    else if (show < 0) { error = "dialog-show-failed"; }
                    else if (events.HasFailed) error = "dialog-selection-failed";
                    else
                    {
                        dialog.GetResult(out selected);
                        string path = Path.GetFullPath(PathOf(selected));
                        string extension = Path.GetExtension(path);
                        if (!string.Equals(extension, ".jmbp", StringComparison.OrdinalIgnoreCase) &&
                            !(request.Kind == BlueprintFileDialogKind.Import && string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))) throw new IOException("dialog-extension");
                        if (request.Kind == BlueprintFileDialogKind.Import) ticket = WindowsBlueprintFileTicket.Source(request.RequestId, path);
                        else ticket = events.Finish(path);
                        outcome = BlueprintFileDialogOutcome.Selected; error = null;
                    }
                }
            }
            catch { error = "dialog-operation-failed"; lock (gate) failed = true; }
            finally
            {
                // Every COM interface and allocated shell string is retired on
                // its creating STA before publishing plain immutable values.
                try { if (advised) dialog.Unadvise(cookie); } catch { error = "dialog-cleanup-failed"; outcome = BlueprintFileDialogOutcome.Failed; }
                try { Release(selected); } catch { error = "dialog-cleanup-failed"; outcome = BlueprintFileDialogOutcome.Failed; }
                try { Release(dialog); } catch { error = "dialog-cleanup-failed"; outcome = BlueprintFileDialogOutcome.Failed; }
                if (events != null) events.Dispose();
                lock (gate) { messageWindow = IntPtr.Zero; showing = false; }
                if (pump != null) pump.Dispose();
                if (initialized) CoUninitialize();
                if (outcome != BlueprintFileDialogOutcome.Selected) { if (ticket != null) ticket.Dispose(); ticket = null; }
                lock (gate) result = new BlueprintFileDialogResult(request, outcome, ticket, counter, outcome == BlueprintFileDialogOutcome.Cancelled ? null : error);
            }
        }

        [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
        private sealed class Events : IFileDialogEvents, IDisposable
        {
            private readonly BlueprintFileDialogRequest request;
            private readonly IFileDialog dialog;
            private readonly Action postName;
            internal readonly BlueprintDialogNameState Name;
            private string folder;
            private bool naming, posted;
            private WindowsBlueprintFileTicket overwrite;
            internal bool HasFailed { get; private set; }
            internal Events(BlueprintFileDialogRequest request, IFileDialog dialog, Action postName)
            {
                this.request = request; this.dialog = dialog; this.postName = postName;
                if (request.Kind == BlueprintFileDialogKind.Export) Name = new BlueprintDialogNameState(request.ExportDate, request.CounterCandidate, request.SuggestedLeaf);
            }
            internal void Fail() { HasFailed = true; try { dialog.Close(Failure); } catch { } }
            private void Observe()
            {
                if (Name == null || naming) return;
                Name.Observe(FileName(dialog));
            }
            private bool Suggest()
            {
                if (Name == null || Name.UserEdited || naming) return true;
                IShellItem item = null;
                try { dialog.GetFolder(out item); folder = Path.GetFullPath(PathOf(item)); }
                finally { Release(item); }
                return ContinueName();
            }
            internal bool ContinueName()
            {
                posted = false;
                if (Name == null || Name.UserEdited || naming || folder == null) return true;
                bool changed;
                bool ready = Name.AdvanceBatch(folder, Exists, out changed);
                if (changed)
                {
                    naming = true;
                    try { dialog.SetFileName(Name.LastSuggestedLeaf); }
                    finally { naming = false; }
                }
                if (!ready && !posted) { posted = true; postName(); }
                return ready && !changed;
            }
            private static bool Exists(string path)
            {
                try { File.GetAttributes(path); return true; }
                catch (FileNotFoundException) { return false; }
                catch (DirectoryNotFoundException) { return false; }
                // Access denied and IO errors are not proof of absence.
            }
            public int OnFileOk(IFileDialog value)
            {
                try { Observe(); return Suggest() ? 0 : 1; }
                catch { Fail(); return 1; }
            }
            public int OnFolderChanging(IFileDialog value, IShellItem item) { Release(item); return 0; }
            public int OnFolderChange(IFileDialog value)
            { try { Observe(); Suggest(); return 0; } catch { Fail(); return Failure; } }
            public int OnSelectionChange(IFileDialog value) { return 0; }
            public int OnShareViolation(IFileDialog value, IShellItem item, out uint response) { response = 0; Release(item); return 0; }
            public int OnTypeChange(IFileDialog value) { return 0; }
            public int OnOverwrite(IFileDialog value, IShellItem item, out uint response)
            {
                response = 2; // refuse until manual mode and fixed evidence are established
                try
                {
                    Observe();
                    if (!Name.UserEdited) { Suggest(); return 0; }
                    if (overwrite != null) overwrite.Dispose();
                    overwrite = WindowsBlueprintFileTicket.CaptureOverwrite(request.RequestId, PathOf(item));
                    response = 0; // SDK FDEOR_DEFAULT: native prompt owns the actual yes/no
                    return 0;
                }
                catch { Fail(); return Failure; }
                finally { Release(item); }
            }
            internal WindowsBlueprintFileTicket Finish(string finalPath)
            {
                // An OnOverwrite callback alone grants nothing. Show must have
                // succeeded and its final path must still name that exact file.
                if (overwrite != null && Name.UserEdited && string.Equals(finalPath, overwrite.NormalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    using (overwrite.OpenConfirmedTarget()) { }
                    var result = overwrite; overwrite = null; return result;
                }
                if (Exists(finalPath)) throw new IOException("dialog-overwrite-not-confirmed");
                if (!Name.UserEdited && !string.Equals(Path.GetFileName(finalPath), Name.LastSuggestedLeaf, StringComparison.OrdinalIgnoreCase)) throw new IOException("dialog-suggested-path-changed");
                return WindowsBlueprintFileTicket.NewTarget(request.RequestId, finalPath);
            }
            public void Dispose() { if (overwrite != null) { overwrite.Dispose(); overwrite = null; } }
        }

        private sealed class PumpWindow : IDisposable
        {
            internal readonly IntPtr Handle;
            internal Action<uint> Dispatch;
            private readonly WindowProc procedure;
            private readonly string className;
            private readonly IntPtr instance;
            internal PumpWindow()
            {
                procedure = WndProc; className = "JMR.Blueprint." + Guid.NewGuid().ToString("N"); instance = GetModuleHandle(null);
                var wc = new WindowClass { Size = (uint)Marshal.SizeOf(typeof(WindowClass)), Procedure = procedure, Instance = instance, ClassName = className };
                if (RegisterClassEx(ref wc) == 0) throw new InvalidOperationException("dialog-pump-register");
                Handle = CreateWindowEx(0, className, "", 0, 0, 0, 0, 0, new IntPtr(-3), IntPtr.Zero, instance, IntPtr.Zero);
                if (Handle == IntPtr.Zero) { UnregisterClass(className, instance); throw new InvalidOperationException("dialog-pump-create"); }
            }
            private IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wparam, IntPtr lparam)
            { if (message == CloseMessage || message == NameMessage) { Dispatch?.Invoke(message); return IntPtr.Zero; } return DefWindowProc(hwnd, message, wparam, lparam); }
            public void Dispose() { DestroyWindow(Handle); UnregisterClass(className, instance); GC.KeepAlive(procedure); }
        }
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wparam, IntPtr lparam);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WindowClass
        {
            internal uint Size, Style; internal WindowProc Procedure; internal int ClassExtra, WindowExtra;
            internal IntPtr Instance, Icon, Cursor, Background;
            [MarshalAs(UnmanagedType.LPWStr)] internal string MenuName;
            [MarshalAs(UnmanagedType.LPWStr)] internal string ClassName;
            internal IntPtr SmallIcon;
        }
        [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved, uint flags);
        [DllImport("ole32.dll")] private static extern void CoUninitialize();
        [DllImport("kernel32.dll")] private static extern uint GetCurrentProcessId();
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint process);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wparam, IntPtr lparam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern ushort RegisterClassEx(ref WindowClass windowClass);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CreateWindowEx(uint style, string className, string title, uint windowStyle, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr DefWindowProc(IntPtr hwnd, uint message, IntPtr wparam, IntPtr lparam);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnregisterClass(string className, IntPtr instance);
    }
}
