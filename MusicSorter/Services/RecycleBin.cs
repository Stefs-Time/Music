using System.IO;
using System.Runtime.InteropServices;

namespace MusicSorter.Services;

/// <summary>
/// Thin wrapper around shell32 SHFileOperation so we can send a file to the Recycle
/// Bin (recoverable) instead of permanently deleting it.
///
/// We allocate <c>pFrom</c> as a raw <see cref="IntPtr"/> pointing at a manually
/// constructed double-null-terminated UTF-16 string. The default LPWStr marshaller
/// is documented as "null-terminated" without guaranteeing what happens to embedded
/// nulls, so we sidestep it entirely.
/// </summary>
public static class RecycleBin
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint   wFunc;
        public IntPtr pFrom;
        public IntPtr pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public IntPtr lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern int SHFileOperationW(ref SHFILEOPSTRUCT FileOp);

    private const uint   FO_DELETE          = 0x0003;
    private const ushort FOF_ALLOWUNDO      = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_NOERRORUI      = 0x0400;
    private const ushort FOF_SILENT         = 0x0004;

    /// <summary>Send <paramref name="path"/> to Recycle Bin. Returns true on success.</summary>
    public static bool TrySendToBin(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;

        // SHFileOperation expects a buffer terminated by *two* consecutive null
        // chars (the second null marks "end of list"). Build it manually.
        var pFrom = Marshal.StringToHGlobalUni(path + '\0');
        try
        {
            var op = new SHFILEOPSTRUCT
            {
                wFunc  = FO_DELETE,
                pFrom  = pFrom,
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_SILENT
            };
            return SHFileOperationW(ref op) == 0 && !op.fAnyOperationsAborted;
        }
        finally
        {
            Marshal.FreeHGlobal(pFrom);
        }
    }

    /// <summary>Permanent delete (no recycle bin).</summary>
    public static bool TryHardDelete(string path)
    {
        try
        {
            if (File.Exists(path)) { File.Delete(path); return true; }
        }
        catch { }
        return false;
    }

    public static bool Remove(string path, bool recycle)
        => recycle ? TrySendToBin(path) : TryHardDelete(path);
}
