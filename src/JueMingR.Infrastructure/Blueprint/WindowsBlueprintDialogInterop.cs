using System;
using System.Runtime.InteropServices;

namespace JueMingR.Infrastructure.Blueprint
{
    // Flattened IModalWindow + IFileDialog vtable, in SDK declaration order.
    // SDK: Windows Kits/10/Include/10.0.26100.0/um/ShObjIdl_core.h.
    // Both Common Item Dialog coclasses expose this exact base IID. No derived
    // Open/Save-only method is called, so no incomplete derived vtable is cast.
    internal static class WindowsBlueprintDialogInterop
    {
        internal const int Cancelled = unchecked((int)0x800704C7), Failure = unchecked((int)0x80004005);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct Filter { [MarshalAs(UnmanagedType.LPWStr)] internal string Name; [MarshalAs(UnmanagedType.LPWStr)] internal string Pattern; }
        [ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IFileDialog
        {
            [PreserveSig] int Show(IntPtr owner);
            void SetFileTypes(uint count, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] Filter[] filters);
            void SetFileTypeIndex(uint index);
            void GetFileTypeIndex(out uint index);
            void Advise(IFileDialogEvents events, out uint cookie);
            void Unadvise(uint cookie);
            void SetOptions(uint options);
            void GetOptions(out uint options);
            void SetDefaultFolder(IShellItem item);
            void SetFolder(IShellItem item);
            void GetFolder(out IShellItem item);
            void GetCurrentSelection(out IShellItem item);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetFileName(out IntPtr name);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            void GetResult(out IShellItem item);
            void AddPlace(IShellItem item, uint location);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
            void Close(int result);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr filter);
        }
        [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IShellItem
        {
            void BindToHandler(IntPtr context, ref Guid handler, ref Guid iid, out IntPtr value);
            void GetParent(out IShellItem parent);
            void GetDisplayName(uint kind, out IntPtr name);
            void GetAttributes(uint mask, out uint attributes);
            void Compare(IShellItem other, uint hint, out int order);
        }
        [ComVisible(true), Guid("973510db-7d7f-452b-8975-74a85828d354"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IFileDialogEvents
        {
            [PreserveSig] int OnFileOk(IFileDialog dialog);
            [PreserveSig] int OnFolderChanging(IFileDialog dialog, IShellItem folder);
            [PreserveSig] int OnFolderChange(IFileDialog dialog);
            [PreserveSig] int OnSelectionChange(IFileDialog dialog);
            [PreserveSig] int OnShareViolation(IFileDialog dialog, IShellItem item, out uint response);
            [PreserveSig] int OnTypeChange(IFileDialog dialog);
            [PreserveSig] int OnOverwrite(IFileDialog dialog, IShellItem item, out uint response);
        }
        internal static string FileName(IFileDialog dialog)
        {
            IntPtr value = IntPtr.Zero;
            try { dialog.GetFileName(out value); return Marshal.PtrToStringUni(value); }
            finally { if (value != IntPtr.Zero) Marshal.FreeCoTaskMem(value); }
        }
        internal static string PathOf(IShellItem item)
        {
            IntPtr value = IntPtr.Zero;
            try { item.GetDisplayName(0x80058000, out value); return Marshal.PtrToStringUni(value); }
            finally { if (value != IntPtr.Zero) Marshal.FreeCoTaskMem(value); }
        }
        internal static void Release(object value) { if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
    }
}
