using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using JueMingR.Infrastructure.Blueprint;
using JueMingR.Platform.Blueprint;

namespace JueMingR.ArchitectureTests
{
    internal static class BlueprintDialogChecks
    {
        private const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        internal static void Check(IList<string> failures)
        {
            try { Names(); Tickets(); Abi(); }
            catch (Exception error) { failures.Add("BlueprintDialog: " + error); }
        }
        internal static void NativeSmoke(IList<string> failures)
        {
            try
            {
                Require(IntPtr.Size == 4 && typeof(object).Assembly.GetName().Name == "mscorlib", "COM smoke requires real Framework x86");
                // This hidden native owner belongs only to this fixture process.
                // Its thread pumps messages while the adapter's STA enters Show.
                // No Terraria window, real input, UI Automation or game data.
                IntPtr owner = CreateWindowEx(0, "STATIC", "JMR isolated file dialog", 0, 0, 0, 16, 16, IntPtr.Zero, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);
                Require(owner != IntPtr.Zero, "isolated process owns a real nonzero HWND");
                var adapter = new WindowsBlueprintFileDialog();
                try
                {
                    foreach (BlueprintFileDialogKind kind in new[] { BlueprintFileDialogKind.Import, BlueprintFileDialogKind.Export })
                    {
                        Guid request = Guid.NewGuid();
                        Require(adapter.TryShow(new BlueprintFileDialogRequest(request, kind, owner, 71, new DateTime(2026, 11, 1), 1, "JM1101001.jmbp")), "native " + kind + " accepts one isolated request");
                        Require(!adapter.TryShow(new BlueprintFileDialogRequest(Guid.NewGuid(), kind, owner, 71, new DateTime(2026, 11, 1), 2, "JM1101002.jmbp")), "single global native slot rejects another window");
                        bool shown = false, closeSent = false;
                        BlueprintFileDialogResult receipt = null;
                        DateTime deadline = DateTime.UtcNow.AddSeconds(12);
                        while (DateTime.UtcNow < deadline)
                        {
                            NativeMessage message;
                            while (PeekMessage(out message, IntPtr.Zero, 0, 0, 1)) { TranslateMessage(ref message); DispatchMessage(ref message); }
                            if ((bool)adapter.GetType().GetProperty("IsShowing", F).GetValue(adapter, null)) shown = true;
                            if (shown && !closeSent) { closeSent = true; adapter.RequestClose(request); }
                            if (adapter.TryTake(out receipt)) break;
                            Thread.Sleep(2);
                        }
                        Require(shown && closeSent && receipt != null && receipt.RequestId == request && receipt.SessionGeneration == 71 &&
                            receipt.Outcome == BlueprintFileDialogOutcome.Cancelled && receipt.Ticket == null && receipt.ErrorCode == null,
                            "real " + kind + " coclass/vtable/events/Show/STA message Close returns exact Cancelled HRESULT and no path ticket");
                    }
                }
                finally { adapter.BeginStop(); DestroyWindow(owner); }
                Console.WriteLine("PASS Common Item Dialog real Windows Framework x86 Open+Save activation, events advise/unadvise, owned Show, STA-message Close and Cancelled result; no physical selection/overwrite/OS focus/gameplay claim.");
            }
            catch (Exception error) { failures.Add("BlueprintDialog NativeSmoke: " + error); }
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static object Call(object value, string name, params object[] args)
        { return value.GetType().GetMethod(name, F).Invoke(value, args); }
        private static object Get(object value, string name) { return value.GetType().GetProperty(name, F).GetValue(value, null); }
        private static void Names()
        {
            var type = typeof(WindowsBlueprintFileDialog).Assembly.GetType("JueMingR.Infrastructure.Blueprint.BlueprintDialogNameState", true);
            var state = Activator.CreateInstance(type, F, null, new object[] { new DateTime(2026, 11, 1), 999L, "JM1101999.jmbp" }, null);
            object[] batch = { "A", (Func<string, bool>)(p => Path.GetFileName(p) == "JM1101999.jmbp"), false };
            Require((bool)Call(state, "AdvanceBatch", batch) && (bool)batch[2], "existing 999 advances one real collision");
            Require((string)Get(state, "LastSuggestedLeaf") == "JM11011000.jmbp" && (long)Get(state, "CounterCandidate") == 1000, "literal 1000 keeps all digits");
            batch = new object[] { "B", (Func<string, bool>)(p => false), false };
            Require((bool)Call(state, "AdvanceBatch", batch) && !(bool)batch[2] && (long)Get(state, "CounterCandidate") == 1000, "changing directory never rewinds candidate");
            Call(state, "Observe", "手工.jmbp"); Call(state, "Observe", "JM11011000.jmbp");
            Require((bool)Get(state, "UserEdited"), "observed manual edit remains sticky after editing back");
            int probes = 0;
            batch = new object[] { "A", (Func<string, bool>)(p => { probes++; return true; }), false };
            Require(!(bool)Call(state, "AdvanceBatch", batch) && probes == 16, "one collision callback has exactly sixteen probes before yielding");
            var frozen = (DateTime)type.GetField("ExportDate", F).GetValue(state);
            Require(frozen == new DateTime(2026, 11, 1), "date remains the original full local date");
        }
        private static void Tickets()
        {
            string directory = Path.Combine(Path.GetTempPath(), "JMR-dialog-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "中文 原件.jmbp");
            try
            {
                File.WriteAllBytes(path, new byte[] { 3, 5, 7 });
                Type type = typeof(WindowsBlueprintFileTicket);
                var ticket = (WindowsBlueprintFileTicket)type.GetMethod("CaptureOverwrite", F).Invoke(null, new object[] { Guid.NewGuid(), path });
                using ((FileStream)Call(ticket, "OpenConfirmedTarget")) { }
                File.WriteAllBytes(path, new byte[] { 3, 5, 7, 9 });
                bool rejected = false;
                try { using ((FileStream)Call(ticket, "OpenConfirmedTarget")) { } }
                catch (TargetInvocationException error) { rejected = error.InnerException is IOException; }
                Require(rejected, "confirmation cannot transfer to a changed length/object"); ticket.Dispose();
                ticket = (WindowsBlueprintFileTicket)type.GetMethod("CaptureOverwrite", F).Invoke(null, new object[] { Guid.NewGuid(), path });
                string other = Path.Combine(directory, "other"); File.WriteAllBytes(other, new byte[] { 3, 5, 7, 9 });
                File.SetLastWriteTimeUtc(other, File.GetLastWriteTimeUtc(path)); File.Delete(path); File.Move(other, path);
                rejected = false;
                try { using ((FileStream)Call(ticket, "OpenConfirmedTarget")) { } }
                catch (TargetInvocationException error) { rejected = error.InnerException is IOException; }
                Require(rejected, "same length and time cannot transfer native consent to a different file-id"); ticket.Dispose();
            }
            finally { File.Delete(path); File.Delete(Path.Combine(directory, "other")); Directory.Delete(directory); }
        }
        private static void Abi()
        {
            var assembly = typeof(WindowsBlueprintFileDialog).Assembly;
            var dialog = assembly.GetType("JueMingR.Infrastructure.Blueprint.WindowsBlueprintDialogInterop+IFileDialog", true);
            string[] expected = { "Show", "SetFileTypes", "SetFileTypeIndex", "GetFileTypeIndex", "Advise", "Unadvise", "SetOptions", "GetOptions", "SetDefaultFolder", "SetFolder", "GetFolder", "GetCurrentSelection", "SetFileName", "GetFileName", "SetTitle", "SetOkButtonLabel", "SetFileNameLabel", "GetResult", "AddPlace", "SetDefaultExtension", "Close", "SetClientGuid", "ClearClientData", "SetFilter" };
            var methods = dialog.GetMethods(); Array.Sort(methods, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
            Require(dialog.GUID == new Guid("42f85136-db7e-439c-85f1-e4075d135fc8") && methods.Length == expected.Length, "official base IID and full flattened vtable length");
            for (int i = 0; i < expected.Length; i++) Require(methods[i].Name == expected[i], "SDK slot " + i + " is " + expected[i]);
            Require((methods[0].MethodImplementationFlags & MethodImplAttributes.PreserveSig) != 0 && methods[0].ReturnType == typeof(int), "Show preserves HRESULT cancellation");
            var adapter = new WindowsBlueprintFileDialog(); adapter.BeginStop();
            Require(!adapter.TryShow(new BlueprintFileDialogRequest(Guid.NewGuid(), BlueprintFileDialogKind.Import, new IntPtr(1), 1, default(DateTime), 0, null)), "stop refuses new windows without allocating a thread");
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMessage
        {
            internal IntPtr Window; internal uint Message; internal UIntPtr WParam; internal IntPtr LParam;
            internal uint Time; internal int X, Y; internal uint Private;
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CreateWindowEx(uint style, string className, string title, uint windowStyle, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(IntPtr window);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PeekMessage(out NativeMessage message, IntPtr window, uint min, uint max, uint remove);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool TranslateMessage(ref NativeMessage message);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr DispatchMessage(ref NativeMessage message);
    }
}
