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
using Layouter.Models;

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
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;

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


        public static BitmapSource GetSuitableIcon(string filePath)
        {
            BitmapSource source = null;
            try
            {
                if (IsShortcutPath(filePath))
                {
                    string targetPath = ResolveShortcut(filePath);

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
                else if (Directory.Exists(filePath))
                {
                    source = GetIconFromFolder(filePath);
                }
                else if (File.Exists(filePath))
                {
                    source = GetIconFromFile(filePath);
                }
                else
                {
                    source = ShellUtil.GetIconFromShellPath(filePath, true);
                }

                return source;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public static BitmapSource GetIconFromFile(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    return GetDefaultIcon();
                }

                SHFILEINFO shfi = new SHFILEINFO();
                uint flags = SHGFI_ICON | SHGFI_LARGEICON;

                // 直接获取文件图标
                if (File.Exists(filePath))
                {
                    IntPtr hImgSmall = SHGetFileInfo(filePath, 0, ref shfi, (uint)Marshal.SizeOf(shfi), flags);
                }
                // 如果文件不存在，则根据扩展名获取图标
                else
                {
                    string ext = Path.GetExtension(filePath);
                    if (!string.IsNullOrEmpty(ext))
                    {
                        string tempFileName = "temp" + ext;
                        IntPtr hImgSmall = SHGetFileInfo(tempFileName, FILE_ATTRIBUTE_NORMAL, ref shfi, (uint)Marshal.SizeOf(shfi), flags | SHGFI_USEFILEATTRIBUTES);
                    }
                    else
                    {
                        return GetDefaultIcon();
                    }
                }

                if (shfi.hIcon == IntPtr.Zero)
                {
                    return GetDefaultIcon();
                }

                using (Icon icon = Icon.FromHandle(shfi.hIcon))
                {
                    BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

                    // 释放图标句柄
                    DestroyIcon(shfi.hIcon);

                    return bitmapSource;
                }
            }
            catch (Exception ex)
            {
                Log.Information($"获取文件图标失败: {ex.Message}");
                return GetDefaultIcon();
            }
        }

        /// <summary>
        /// 获取文件夹图标
        /// </summary>
        public static BitmapSource GetIconFromFolder(string folderPath)
        {
            try
            {
                SHFILEINFO shfi = new SHFILEINFO();
                uint flags = SHGFI_ICON | SHGFI_LARGEICON;

                // 直接获取目录图标
                if (Directory.Exists(folderPath))
                {
                    IntPtr hImgSmall = SHGetFileInfo(folderPath, 0, ref shfi, (uint)Marshal.SizeOf(shfi), flags);
                }
                // 目录不存在则获取通用目录图标
                else
                {
                    IntPtr hImgSmall = SHGetFileInfo("", FILE_ATTRIBUTE_DIRECTORY, ref shfi, (uint)Marshal.SizeOf(shfi), flags | SHGFI_USEFILEATTRIBUTES);
                }

                if (shfi.hIcon == IntPtr.Zero)
                {
                    return GetDefaultIcon();
                }

                using (Icon icon = Icon.FromHandle(shfi.hIcon))
                {
                    BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

                    // 释放图标句柄
                    DestroyIcon(shfi.hIcon);

                    return bitmapSource;
                }
            }
            catch (Exception ex)
            {
                Log.Information($"获取文件夹图标失败: {ex.Message}");
                return GetDefaultIcon();
            }
        }

        /// <summary>
        /// 获取系统图标
        /// </summary>
        public static BitmapSource GetSystemIcon(string path)
        {
            try
            {
                SHFILEINFO shfi = new SHFILEINFO();
                IntPtr hImgSmall = SHGetFileInfo(path, FILE_ATTRIBUTE_NORMAL, ref shfi, (uint)Marshal.SizeOf(shfi), SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES);

                if (shfi.hIcon == IntPtr.Zero)
                {
                    return GetDefaultIcon();
                }

                using (Icon icon = Icon.FromHandle(shfi.hIcon))
                {
                    BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

                    DestroyIcon(shfi.hIcon);

                    return bitmapSource;
                }
            }
            catch (Exception ex)
            {
                Log.Information($"获取系统图标失败: {ex.Message}");
                return GetDefaultIcon();
            }
        }

        /// <summary>
        /// 获取默认图标
        /// </summary>
        public static BitmapSource GetDefaultIcon()
        {
            try
            {
                // 获取系统默认的未知文件类型图标
                SHFILEINFO shfi = new SHFILEINFO();
                IntPtr hImgSmall = SHGetFileInfo("unknown", FILE_ATTRIBUTE_NORMAL, ref shfi, (uint)Marshal.SizeOf(shfi), SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES);

                if (shfi.hIcon == IntPtr.Zero)
                {
                    // 如果获取失败，尝试获取系统文件图标
                    string systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    string defaultIconPath = Path.Combine(systemDir, "shell32.dll");

                    // 使用 shell32.dll 中的默认图标
                    hImgSmall = SHGetFileInfo(defaultIconPath, 0, ref shfi, (uint)Marshal.SizeOf(shfi), SHGFI_ICON | SHGFI_LARGEICON);

                    if (shfi.hIcon == IntPtr.Zero)
                    {
                        return null;
                    }
                }

                using (Icon icon = Icon.FromHandle(shfi.hIcon))
                {
                    BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

                    DestroyIcon(shfi.hIcon);

                    return bitmapSource;
                }
            }
            catch (Exception ex)
            {
                Log.Information($"获取默认图标失败: {ex.Message}");
                return null;
            }
        }

        public static bool IsShortcutPath(string shortcutPath)
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