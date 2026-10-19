using System.Runtime.InteropServices;

namespace JueMingR.TerrariaHost.View
{
    internal static class ViewCacheMemory
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Status
        {
            internal uint Length,Load;
            internal ulong TotalPhysical,AvailablePhysical,TotalPage,AvailablePage,TotalVirtual,AvailableVirtual,AvailableExtendedVirtual;
        }
        [DllImport("kernel32.dll",SetLastError=true)]
        [return:MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref Status status);
        internal static bool Admit(long newBytes,out ulong availableVirtual)
        {
            var value=new Status{Length=(uint)Marshal.SizeOf(typeof(Status))};
            if(!GlobalMemoryStatusEx(ref value)){availableVirtual=0;return false;}
            availableVirtual=value.AvailableVirtual;
            // This optional group is prepared while the old one is still live.
            // AvailableVirtual already excludes those old allocations. Reserve
            // another64MiB for driver/managed transient work; this is a local
            // cache safety margin, not measured VRAM or a project FPS budget.
            ulong needed=(ulong)newBytes+64UL*1024*1024;
            return value.AvailableVirtual>=needed&&value.AvailablePhysical>=needed;
        }
    }
}
