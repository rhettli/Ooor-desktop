using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace ooor.Controls
{
    /// <summary>
    /// 通过 Shell32 获取系统图标（按扩展名）。常驻缓存避免每次绘图都打 API。
    /// </summary>
    internal static class IconReader
    {
        public enum IconSize { Small = 0x1, Large = 0x0 }

        [DllImport("Shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            ref SHFILEINFO psfi,
            uint cbFileInfo,
            uint uFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

        public static Icon GetFileIcon(string ext, IconSize size)
        {
            if (string.IsNullOrEmpty(ext)) return null;
            if (!ext.StartsWith(".", StringComparison.Ordinal)) ext = "." + ext;
            var fi = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES | (uint)size;
            IntPtr res = SHGetFileInfo(ext, FILE_ATTRIBUTE_NORMAL, ref fi, (uint)Marshal.SizeOf(fi), flags);
            if (res == IntPtr.Zero || fi.hIcon == IntPtr.Zero) return null;
            try
            {
                var icon = (Icon)Icon.FromHandle(fi.hIcon).Clone();
                return icon;
            }
            catch { return null; }
            finally
            {
                if (fi.hIcon != IntPtr.Zero) DestroyIcon(fi.hIcon);
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}