using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Drawing;

namespace Layouter.Services
{
    public class SystemFolderService
    {
        public static List<SystemFolderInfo> GetSystemFolders()
        {
            var folders = new List<SystemFolderInfo>
            {
                new SystemFolderInfo
                {
                    Name = "此电脑",
                    Path = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
                    IconPath = "shell32.dll,0"
                },
                new SystemFolderInfo
                {
                    Name = "控制面板",
                    Path = "::{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}",
                    IconPath = "shell32.dll,21"
                },
                new SystemFolderInfo
                {
                    Name = "网络",
                    Path = "::{208D2C60-3AEA-1069-A2D7-08002B30309D}",
                    IconPath = "shell32.dll,17"
                },
                new SystemFolderInfo
                {
                    Name = "回收站",
                    Path = "::{645FF040-5081-101B-9F08-00AA002F954E}",
                    IconPath = "shell32.dll,31"
                },
                new SystemFolderInfo
                {
                    Name = "文档",
                    Path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    IconPath = "shell32.dll,126"
                }
                // 可以添加更多系统文件夹
            };
            
            return folders;
        }
        
        public static ImageSource GetSystemIconAsImageSource(string iconPath)
        {
            try
            {
                string[] parts = iconPath.Split(',');
                string file = parts[0];
                int index = parts.Length > 1 ? int.Parse(parts[1]) : 0;
                
                IntPtr hIcon = ExtractIcon(IntPtr.Zero, file, index);
                if (hIcon == IntPtr.Zero)
                    return null;
                
                ImageSource imageSource = Imaging.CreateBitmapSourceFromHIcon(
                    hIcon, 
                    System.Windows.Int32Rect.Empty, 
                    BitmapSizeOptions.FromEmptyOptions());
                
                DestroyIcon(hIcon);
                return imageSource;
            }
            catch
            {
                return null;
            }
        }
        
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
    
    public class SystemFolderInfo
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string IconPath { get; set; }
    }
} 