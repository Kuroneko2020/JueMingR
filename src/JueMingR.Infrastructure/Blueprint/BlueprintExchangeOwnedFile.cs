using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace JueMingR.Infrastructure.Blueprint
{
    // Cleanup acts on a verified file object, never whichever pathname is found
    // after closing the writer. DELETE access is acquired only for this exact
    // operation's CreateNew candidate; unknown or changed objects are retained.
    internal sealed class BlueprintExchangeOwnedFile
    {
        private NativeInfo stamp;
        private bool known;
        internal void Capture(Stream stream)
        {
            var file = stream as FileStream;
            if (file == null || !GetFileInformationByHandle(file.SafeFileHandle, out stamp)) { known = false; return; }
            known = (stamp.Attributes & ((uint)FileAttributes.ReparsePoint | (uint)FileAttributes.Directory)) == 0;
        }
        internal bool Delete(string path, bool writerAlive)
        {
            if (!known) return false;
            using (SafeFileHandle handle = CreateFile(path, 0x80010000, writerAlive ? 0x7u : 0x5u, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero))
            {
                NativeInfo current;
                if (handle.IsInvalid || !GetFileInformationByHandle(handle, out current) || !Same(stamp, current)) return false;
                var disposition = new Disposition { DeleteFile = true };
                return SetFileInformationByHandle(handle, 4, ref disposition, 1);
            }
        }
        private static bool Same(NativeInfo a, NativeInfo b)
        { return a.Volume == b.Volume && a.IndexHigh == b.IndexHigh && a.IndexLow == b.IndexLow && a.SizeHigh == b.SizeHigh && a.SizeLow == b.SizeLow && a.WriteHigh == b.WriteHigh && a.WriteLow == b.WriteLow && (b.Attributes & ((uint)FileAttributes.ReparsePoint | (uint)FileAttributes.Directory)) == 0; }
        [StructLayout(LayoutKind.Sequential)] private struct NativeInfo { internal uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh, Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow; }
        [StructLayout(LayoutKind.Sequential)] private struct Disposition { [MarshalAs(UnmanagedType.U1)] internal bool DeleteFile; }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFile(string path, uint access, uint sharing, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out NativeInfo value);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int kind, ref Disposition value, uint size);
    }
}
