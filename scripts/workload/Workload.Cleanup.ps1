# Explicit collection only. Handle ownership makes preflight apply to the same
# Windows objects through deletion; never recursive path-based Remove-Item.
function Initialize-WorkloadCleanupLease {
    if('JueMingR.WorkloadDeleteLease' -as [type]){return}
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace JueMingR {
 public sealed class WorkloadDeleteLease : IDisposable {
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint mode, uint flags, IntPtr template);
  [DllImport("kernel32.dll", SetLastError=true)]
  static extern bool GetFileInformationByHandle(SafeFileHandle handle, out Info info);
  [DllImport("kernel32.dll", SetLastError=true)]
  static extern bool SetFileInformationByHandle(SafeFileHandle handle, int kind, ref Disposition info, uint size);
  [StructLayout(LayoutKind.Sequential)] struct Info {
   public uint attributes, creationLow, creationHigh, accessLow, accessHigh, writeLow, writeHigh, volume, sizeHigh, sizeLow, links, indexHigh, indexLow;
  }
  [StructLayout(LayoutKind.Sequential)] struct Disposition { public byte delete; }
  readonly SafeFileHandle handle; readonly bool removable;
  public WorkloadDeleteLease(string path, bool directory, bool removable) {
   this.removable=removable;
   handle=CreateFile(path, directory ? (removable ? 0x10081u : 0x81u) : 0x80010000u,
    directory ? 3u : 1u, IntPtr.Zero, 3u, 0x02200000u, IntPtr.Zero);
   if(handle.IsInvalid){int error=Marshal.GetLastWin32Error();handle.Dispose();throw new Win32Exception(error, "Cannot lease cleanup object: "+path);}
   Info info;
   if(!GetFileInformationByHandle(handle,out info) || (info.attributes & 0x400u)!=0 || ((info.attributes & 0x10u)!=0)!=directory){handle.Dispose();throw new InvalidOperationException("Cleanup object changed or is reparse: "+path);}
  }
  public void DeleteOwnedObject() {
   if(!removable)throw new InvalidOperationException("Protected cleanup ancestor");
   var value=new Disposition{delete=1};
   if(!SetFileInformationByHandle(handle,4,ref value,1))throw new Win32Exception(Marshal.GetLastWin32Error(),"Cleanup disposition refused");
  }
  public void Dispose(){handle.Dispose();}
 }
}
'@
}
