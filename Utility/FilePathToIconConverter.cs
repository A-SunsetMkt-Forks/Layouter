using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Layouter.Utility
{
    public class FilePathToIconConverter : IValueConverter
    {
        [DllImport("shell32.dll")]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

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

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null || !(value is string filePath))
                return null;

            try
            {
                // 如果是图片文件，直接返回
                string extension = Path.GetExtension(filePath).ToLower();
                if (extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".bmp" || extension == ".gif")
                {
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.UriSource = new Uri(filePath);
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.EndInit();
                            return bmp;
                        }
                        catch
                        {
                            // 如果加载图片失败，则回退到获取系统图标
                        }
                    }
                }

                // 获取系统图标
                SHFILEINFO shfi = new SHFILEINFO();
                IntPtr res = SHGetFileInfo(filePath, 0, ref shfi, (uint)Marshal.SizeOf(shfi), SHGFI_ICON | SHGFI_LARGEICON);

                if (res == IntPtr.Zero || shfi.hIcon == IntPtr.Zero)
                {
                    return null;
                }
                // 转换图标为BitmapSource
                var icon = Icon.FromHandle(shfi.hIcon);
                var bitmap = icon.ToBitmap();
                var source = Imaging.CreateBitmapSourceFromHBitmap(
                    bitmap.GetHbitmap(),
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                // 清理资源
                DestroyIcon(shfi.hIcon);

                return source;
            }
            catch (Exception)
            {
                return null;
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
