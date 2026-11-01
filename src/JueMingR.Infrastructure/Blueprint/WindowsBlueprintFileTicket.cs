using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using JueMingR.Platform.Blueprint;

namespace JueMingR.Infrastructure.Blueprint
{
    public sealed class WindowsBlueprintFileTicket : IBlueprintExchangeTicket
    {
        public Guid RequestId { get; private set; }
        internal readonly string NormalizedPath;
        internal readonly BlueprintTargetDisposition Disposition;
        internal readonly bool IsImport;
        private readonly Stamp confirmed;
        private bool disposed;
        private WindowsBlueprintFileTicket(Guid requestId, string path, bool import, BlueprintTargetDisposition disposition, Stamp stamp)
        { RequestId = requestId; NormalizedPath = Path.GetFullPath(path); IsImport = import; Disposition = disposition; confirmed = stamp; }
        internal static WindowsBlueprintFileTicket Source(Guid requestId, string path)
        { return new WindowsBlueprintFileTicket(requestId, path, true, BlueprintTargetDisposition.AutoCreateNew, default(Stamp)); }
        internal static WindowsBlueprintFileTicket NewTarget(Guid requestId, string path)
        { return new WindowsBlueprintFileTicket(requestId, path, false, BlueprintTargetDisposition.AutoCreateNew, default(Stamp)); }
        internal static WindowsBlueprintFileTicket CaptureOverwrite(Guid requestId, string path)
        {
            using (var stream = Open(path))
                return new WindowsBlueprintFileTicket(requestId, path, false, BlueprintTargetDisposition.ConfirmedReplace, ReadStamp(stream));
        }
        internal FileStream OpenConfirmedTarget()
        {
            if (disposed || IsImport || Disposition != BlueprintTargetDisposition.ConfirmedReplace) throw new IOException("overwrite-not-authorized");
            FileStream stream = Open(NormalizedPath);
            try { if (!confirmed.Equals(ReadStamp(stream))) throw new IOException("overwrite-target-changed"); return stream; }
            catch { stream.Dispose(); throw; }
        }
        private static FileStream Open(string path)
        {
            if ((File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) throw new IOException("overwrite-target-kind");
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, FileOptions.SequentialScan);
        }
        // Only this fixed Win32 metadata is sampled on the dialog STA. Body hash,
        // expected identity and all large reads belong to the external IO lane.
        private static Stamp ReadStamp(FileStream stream)
        {
            NativeInfo info;
            if (!GetFileInformationByHandle(stream.SafeFileHandle, out info)) throw new IOException("overwrite-identity-unavailable");
            if ((info.Attributes & ((uint)FileAttributes.Directory | (uint)FileAttributes.ReparsePoint)) != 0) throw new IOException("overwrite-target-kind");
            return new Stamp { Volume = info.Volume, IndexHigh = info.IndexHigh, IndexLow = info.IndexLow,
                LengthHigh = info.SizeHigh, LengthLow = info.SizeLow, WriteHigh = info.WriteHigh, WriteLow = info.WriteLow };
        }
        public void Dispose() { disposed = true; }
        private struct Stamp : IEquatable<Stamp>
        {
            internal uint Volume, IndexHigh, IndexLow, LengthHigh, LengthLow, WriteHigh, WriteLow;
            public bool Equals(Stamp other)
            { return Volume == other.Volume && IndexHigh == other.IndexHigh && IndexLow == other.IndexLow && LengthHigh == other.LengthHigh && LengthLow == other.LengthLow && WriteHigh == other.WriteHigh && WriteLow == other.WriteLow; }
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeInfo
        {
            internal uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh,
                Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out NativeInfo information);
    }
}
