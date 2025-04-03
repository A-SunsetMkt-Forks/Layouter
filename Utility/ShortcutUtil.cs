using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Diagnostics;

namespace Layouter.Utility
{

    public class ShortCutUtil
    {
        [DllImport("shell32.dll")]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeOf, uint uFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

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
        private const uint SHGFI_LARGEICON = 0x0;
        private const uint SHGFI_SMALLICON = 0x1;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x10;

        /// <summary>
        /// 根据快捷方式路径解析目标路径
        /// </summary>
        /// <param name="shortcutPath">快捷方式路径</param>
        private static string ResolveShortcut(string shortcutPath)
        {
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = Activator.CreateInstance(t);
            var shortcut = shell.CreateShortcut(shortcutPath);
            return shortcut.TargetPath;
        }


        public static BitmapSource GetIconFromShortcut(string shortcutPath)
        {
            BitmapSource source = null;
            try
            {
                if (IsShortcutPath(shortcutPath))
                {
                    string targetPath = ResolveShortcut(shortcutPath);

                    if (string.IsNullOrEmpty(targetPath))
                    {
                        return null;
                    }

                    //获取目标文件的图标
                    SHFILEINFO shfi = new SHFILEINFO();
                    IntPtr res = SHGetFileInfo(targetPath, FILE_ATTRIBUTE_NORMAL, ref shfi, (uint)Marshal.SizeOf(shfi), SHGFI_ICON | SHGFI_LARGEICON);

                    if (res == IntPtr.Zero || shfi.hIcon == IntPtr.Zero)
                    {
                        return null;
                    }

                    // 转换图标为BitmapSource
                    var icon = Icon.FromHandle(shfi.hIcon);
                    var bitmap = icon.ToBitmap();
                    source = Imaging.CreateBitmapSourceFromHBitmap(bitmap.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

                    // 清理资源
                    DestroyIcon(shfi.hIcon);

                }
                else
                {
                    source = ShellUtil.GetIconFromShellPath(shortcutPath, true);
                }

                return source;
            }
            catch (Exception ex)
            {
                return null;
            }
        }


        private static bool IsShortcutPath(string shortcutPath)
        {
            string path = shortcutPath.ToLower();
            if (path.EndsWith(".lnk") || path.EndsWith(".url"))
            {
                return true;
            }
            return false;
        }
    }
}