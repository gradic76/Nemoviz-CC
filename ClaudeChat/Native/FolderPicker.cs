using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ClaudeChat.Native;

/// <summary>
/// The standard Explorer "Select Folder" dialog (IFileOpenDialog with FOS_PICKFOLDERS):
/// navigation tree on the left, address bar, folder list. It opens in the parent of the
/// current folder with the current folder already typed in, so a project and its
/// neighbours are visible at once.
/// </summary>
internal static class FolderPicker
{
    public static string? Pick(IntPtr owner, string title, string? current)
    {
        IFileOpenDialog? dialog = null;
        try
        {
            dialog = (IFileOpenDialog)new FileOpenDialogRcw();
            dialog.GetOptions(out uint options);
            dialog.SetOptions(options | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST | FOS_NOCHANGEDIR);
            dialog.SetTitle(title);

            if (!string.IsNullOrEmpty(current) && Directory.Exists(current))
            {
                string full = Path.GetFullPath(current).TrimEnd('\\');
                string? parent = Path.GetDirectoryName(full);
                if (parent != null && TryItem(parent, out var parentItem))
                {
                    dialog.SetFolder(parentItem!);
                    dialog.SetFileName(Path.GetFileName(full));
                }
                else if (TryItem(full, out var item))
                {
                    dialog.SetFolder(item!);
                }
            }

            int hr = dialog.Show(owner);
            if (hr != 0) return null; // cancelled (0x800704C7) or failed

            dialog.GetResult(out IShellItem result);
            result.GetDisplayName(SIGDN_FILESYSPATH, out IntPtr pszPath);
            try { return Marshal.PtrToStringUni(pszPath); }
            finally { Marshal.FreeCoTaskMem(pszPath); }
        }
        catch (COMException) { return null; }
        finally
        {
            if (dialog != null) Marshal.ReleaseComObject(dialog);
        }
    }

    private static bool TryItem(string path, out IShellItem? item)
    {
        var iid = typeof(IShellItem).GUID;
        int hr = SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out item);
        return hr == 0 && item != null;
    }

    private const uint FOS_NOCHANGEDIR = 0x00000008;
    private const uint FOS_PICKFOLDERS = 0x00000020;
    private const uint FOS_FORCEFILESYSTEM = 0x00000040;
    private const uint FOS_PATHMUSTEXIST = 0x00000800;
    private const uint SIGDN_FILESYSPATH = 0x80058000;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr bindCtx, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItem? item);

    [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
    private class FileOpenDialogRcw { }

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(uint sigdnName, out IntPtr ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    // Vtable order matters: IModalWindow.Show, then IFileDialog, then IFileOpenDialog.
    [ComImport, Guid("D57C7288-D4AD-4768-BE02-9D969532D960"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig] int Show(IntPtr parent);
        void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
        void SetFileTypeIndex(uint iFileType);
        void GetFileTypeIndex(out uint piFileType);
        void Advise(IntPtr pfde, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(uint fos);
        void GetOptions(out uint pfos);
        void SetDefaultFolder(IShellItem psi);
        void SetFolder(IShellItem psi);
        void GetFolder(out IShellItem ppsi);
        void GetCurrentSelection(out IShellItem ppsi);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult(out IShellItem ppsi);
        void AddPlace(IShellItem psi, int fdap);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void Close(int hr);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr pFilter);
        void GetResults(out IntPtr ppenum);
        void GetSelectedItems(out IntPtr ppsai);
    }
}
