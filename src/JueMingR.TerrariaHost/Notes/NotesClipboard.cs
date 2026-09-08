using System;
using System.Runtime.InteropServices;
using JueMingR.Features.Notes;

namespace JueMingR.TerrariaHost.Notes
{
    internal interface INotesClipboard
    {
        bool TryCopy(string text);
        bool TryPaste(out string text);
    }
    // Open once and fail promptly. ReLogic's wrapper swallows errors, which cannot
    // establish the success required before a destructive whole-draft cut.
    internal sealed class NotesClipboard : INotesClipboard
    {
        private readonly Func<IntPtr> window;
        internal NotesClipboard(Func<IntPtr> window) { this.window = window; }
        public bool TryCopy(string text)
        {
            IntPtr memory = GlobalAlloc(2, new UIntPtr((uint)((text.Length + 1) * 2)));
            if (memory == IntPtr.Zero) return false;
            bool opened = false;
            try
            {
                IntPtr pointer = GlobalLock(memory); if (pointer == IntPtr.Zero) return false;
                try { char[] value = (text + "\0").ToCharArray(); Marshal.Copy(value, 0, pointer, value.Length); }
                finally { GlobalUnlock(memory); }
                opened = OpenClipboard(window());
                if (!opened || !EmptyClipboard()) return false;
                if (SetClipboardData(13, memory) == IntPtr.Zero) return false;
                // Windows owns the successful movable allocation from this point.
                memory = IntPtr.Zero; return true;
            }
            finally { if (opened) CloseClipboard(); if (memory != IntPtr.Zero) GlobalFree(memory); }
        }
        public bool TryPaste(out string text)
        {
            text = null; if (!OpenClipboard(window())) return false;
            try
            {
                if (!IsClipboardFormatAvailable(13)) return false;
                IntPtr memory = GetClipboardData(13); if (memory == IntPtr.Zero) return false;
                ulong bytes = GlobalSize(memory).ToUInt64();
                if (bytes < 2 || bytes > (Note.MaximumBodyUnits + 1L) * 2 || bytes % 2 != 0) return false;
                IntPtr pointer = GlobalLock(memory); if (pointer == IntPtr.Zero) return false;
                try
                {
                    string value = Marshal.PtrToStringUni(pointer, (int)(bytes / 2)); int end = value.IndexOf('\0');
                    if (end < 0) return false; text = value.Substring(0, end); return true;
                }
                finally { GlobalUnlock(memory); }
            }
            finally { CloseClipboard(); }
        }
        [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr owner);
        [DllImport("user32.dll")] private static extern bool CloseClipboard();
        [DllImport("user32.dll")] private static extern bool EmptyClipboard();
        [DllImport("user32.dll")] private static extern bool IsClipboardFormatAvailable(uint format);
        [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint format);
        [DllImport("user32.dll")] private static extern IntPtr SetClipboardData(uint format, IntPtr memory);
        [DllImport("kernel32.dll")] private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
        [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr memory);
        [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr memory);
        [DllImport("kernel32.dll")] private static extern IntPtr GlobalFree(IntPtr memory);
        [DllImport("kernel32.dll")] private static extern UIntPtr GlobalSize(IntPtr memory);
    }
}
