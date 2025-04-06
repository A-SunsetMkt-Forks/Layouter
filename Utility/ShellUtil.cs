using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.IO;
using static System.Windows.Win32;
using System.Diagnostics;
using FluentIcons.Common.Internals;
using System.Security.Principal;

namespace Layouter.Utility
{
    #region COM Interop

    [ComImport]
    [Guid("00000000-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IUnknown
    {
        void QueryInterface(ref Guid riid, out IntPtr ppvObject);
        void AddRef();
        void Release();
    }

    //[ComImport]
    //[Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    //[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    //public interface IShellItem
    //{
    //    void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
    //    void GetParent(out IShellItem ppsi);
    //    int GetDisplayName(uint sigdnName, out IntPtr ppszName);
    //    void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
    //    int Compare(IShellItem psi, uint hint, out int piOrder);
    //}

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItem
    {
        [PreserveSig]
        int BindToHandler(IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);

        [PreserveSig]
        int GetParent(out IShellItem ppsi);

        [PreserveSig]
        int GetDisplayName([In] uint sigdnName, out IntPtr ppszName);

        [PreserveSig]
        int GetAttributes([In] uint sfgaoMask, out uint psfgaoAttribs);

        [PreserveSig]
        int Compare([In] IShellItem psi, [In] uint hint, out int piOrder);
    }


    [ComImport]
    [Guid("B63EA76D-1F85-456F-A19C-48159EFA858B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItemArray
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppvOut);
        void GetPropertyStore(int flags, ref Guid riid, out IntPtr ppv);
        void GetPropertyDescriptionList(IntPtr keyType, ref Guid riid, out IntPtr ppv);
        void GetAttributes(int AttribFlags, uint sfgaoMask, out uint psfgaoAttribs);
        uint GetCount();
        void GetItemAt(uint dwIndex, out IShellItem ppsi);
        void EnumItems(out IntPtr ppenumShellItems);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    #endregion

    public class ShellItemInfo
    {
        public string Path { get; set; }
        public string DisplayName { get; set; }
        public ImageSource IconSource { get; set; }
    }

    public class ShellUtil
    {
        private const uint SIGDN_FILESYSPATH = 0x80058000;
        private const uint SIGDN_NORMALDISPLAY = 0x00000000;
        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_SMALLICON = 0x000000001;
        private const uint SHGFI_PIDL = 0x000000008;
        private const int CSIDL_PROFILE = 0x28; // UserProfile文件夹

        private static Dictionary<string, string> specialFolders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "此电脑", "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}" },
            { "计算机", "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}" },
            { "控制面板", "::{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}" },
            { "回收站", "::{645FF040-5081-101B-9F08-00AA002F954E}" },
            { "网络", "::{208D2C60-3AEA-1069-A2D7-08002B30309D}" },
            { "桌面", "::{B4BFCC3A-DB2C-424C-B29F-7FE9909CFC64}" },
            { "文档", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) },
            { "我的文档", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) },
            { "图片", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) },
            { "我的图片", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) },
            { "音乐", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) },
            { "我的音乐", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) },
            { "视频", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos) },
            { "我的视频", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos) },
            { "下载", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads") },
            { "我的下载", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads") },
            { "收藏夹", Environment.GetFolderPath(Environment.SpecialFolder.Favorites) },
            { "程序", Environment.GetFolderPath(Environment.SpecialFolder.Programs) },
            { "开始菜单", Environment.GetFolderPath(Environment.SpecialFolder.StartMenu) },
            { "应用数据", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) },
            { "本地应用数据", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) },
            { "临时文件", Path.GetTempPath() },
            { GetDisplayCurrentUserName(),Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) }
        };



        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern int SHCreateShellItemArrayFromDataObject(System.Runtime.InteropServices.ComTypes.IDataObject pdo, ref Guid riid, out IShellItemArray ppv);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHGetNameFromIDList(IntPtr pidl, uint sigdnName, out IntPtr ppszName);

        //[DllImport("shell32.dll")]
        //private static extern IntPtr SHGetNameFromIDList(IntPtr pidl, int sigdnName);

        [DllImport("shell32.dll")]
        private static extern int SHGetFolderLocation(IntPtr hwndOwner, int nFolder, IntPtr hToken, uint dwReserved, out IntPtr ppidl);

        [DllImport("shell32.dll")]
        private static extern IntPtr ILCreateFromPath([MarshalAs(UnmanagedType.LPWStr)] string pszPath);

        [DllImport("shell32.dll")]
        private static extern void ILFree(IntPtr pidl);

        [DllImport("shell32.dll")]
        private static extern int SHGetIDListFromObject(IntPtr punk, out IntPtr ppidl);

        [DllImport("shell32.dll")]
        private static extern int SHGetFileInfo(IntPtr pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHParseDisplayName([MarshalAs(UnmanagedType.LPWStr)] string pszName, IntPtr pbc, out IntPtr ppidl, uint sfgaoIn, out uint psfgaoOut);

        [DllImport("ole32.dll")]
        private static extern void CoTaskMemFree(IntPtr pv);

        public static System.Runtime.InteropServices.ComTypes.IDataObject GetComDataObject(System.Windows.IDataObject wpfDataObject)
        {
            // 获取IUnknown接口
            IntPtr pUnk = Marshal.GetIUnknownForObject(wpfDataObject);

            try
            {
                // IDataObject的COM接口ID
                Guid iidIDataObject = new Guid("0000010E-0000-0000-C000-000000000046");

                IntPtr pDataObject;
                Marshal.QueryInterface(pUnk, ref iidIDataObject, out pDataObject);

                try
                {
                    // 将指针转换为COM IDataObject
                    return (System.Runtime.InteropServices.ComTypes.IDataObject)Marshal.GetObjectForIUnknown(pDataObject);
                }
                finally
                {
                    Marshal.Release(pDataObject);
                }
            }
            finally
            {
                Marshal.Release(pUnk);
            }
        }

        public static List<ShellItemInfo> GetShellItemsFromDataObject(IDataObject dataObject)
        {
            var result = new List<ShellItemInfo>();

            // 获取COM数据对象
            //var dataObjectCom = Marshal.GetIUnknownForObject(dataObject);
            var dataObjectCom = GetComDataObject(dataObject);

            try
            {
                // 创建Shell项目数组
                var shellItemArrayGuid = new Guid("B63EA76D-1F85-456F-A19C-48159EFA858B");
                int hr = SHCreateShellItemArrayFromDataObject(dataObjectCom, ref shellItemArrayGuid, out IShellItemArray shellItemArray);

                if (hr != 0)
                {
                    return result;
                }
                // 获取项目数量
                uint count = shellItemArray.GetCount();

                // 遍历所有项目
                for (uint i = 0; i < count; i++)
                {
                    shellItemArray.GetItemAt(i, out IShellItem shellItem);

                    if (shellItem != null)
                    {

                        // 获取路径
                        IntPtr pszPath = IntPtr.Zero;
                        shellItem.GetDisplayName(SIGDN_FILESYSPATH, out pszPath);
                        string path = Marshal.PtrToStringUni(pszPath);
                        Marshal.FreeCoTaskMem(pszPath);

                        // 获取显示名称
                        shellItem.GetDisplayName(SIGDN_NORMALDISPLAY, out IntPtr pszDisplayName);
                        string displayName = Marshal.PtrToStringUni(pszDisplayName);
                        Marshal.FreeCoTaskMem(pszDisplayName);

                        // 获取图标
                        ImageSource iconSource = null;

                        // 获取PIDL
                        int hrPidl = SHGetIDListFromObject(Marshal.GetIUnknownForObject(shellItem), out IntPtr pidl);
                        if (hrPidl == 0 && pidl != IntPtr.Zero)
                        {
                            SHFILEINFO shfi = new SHFILEINFO();
                            if (SHGetFileInfo(pidl, 0, ref shfi, (uint)Marshal.SizeOf(shfi), SHGFI_ICON | SHGFI_SMALLICON | SHGFI_PIDL) != 0)
                            {
                                if (shfi.hIcon != IntPtr.Zero)
                                {
                                    iconSource = Imaging.CreateBitmapSourceFromHIcon(shfi.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                                    DestroyIcon(shfi.hIcon);
                                }
                            }
                            ILFree(pidl);
                        }

                        // 特殊处理：如果路径为空但有显示名称，可能是特殊Shell项目
                        if (string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(displayName))
                        {
                            // 尝试根据显示名称映射到已知的特殊文件夹
                            path = MapSpecialFolderByName(displayName);
                        }

                        // 添加到结果列表
                        if (!string.IsNullOrEmpty(path) || !string.IsNullOrEmpty(displayName))
                        {
                            result.Add(new ShellItemInfo
                            {
                                Path = path,
                                DisplayName = displayName,
                                IconSource = iconSource
                            });
                        }

                        Marshal.ReleaseComObject(shellItem);
                    }
                }

                Marshal.ReleaseComObject(shellItemArray);
            }
            catch (Exception ex)
            {
                Log.Error($"处理Shell项目时出错: {ex.Message}");
            }

            return result;
        }

        public static BitmapSource GetIconFromShellPath(string path, bool largeIcon = false)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }
            BitmapSource iconSource = null;
            IntPtr pidl = IntPtr.Zero;

            try
            {
                uint flags = largeIcon ? SHGFI_ICON | SHGFI_PIDL | SHGFI_LARGEICON : SHGFI_ICON | SHGFI_PIDL | SHGFI_SMALLICON;

                // 对于常规文件路径和CLSID路径的处理方式不同
                if (path.StartsWith("::"))
                {
                    // 对于CLSID路径，使用SHParseDisplayName获取PIDL
                    uint sfgao;
                    int hr = (int)SHParseDisplayName(path, IntPtr.Zero, out pidl, 0, out sfgao);

                    if (hr != 0 || pidl == IntPtr.Zero)
                    {
                        return null;
                    }
                }
                else
                {
                    // 对于普通文件路径，可以直接使用SHGetFileInfo
                    SHFILEINFO shfi = new SHFILEINFO();
                    IntPtr npidl = ILCreateFromPath(path);
                    SHGetFileInfo(npidl, 0, ref shfi, (uint)Marshal.SizeOf(shfi), SHGFI_ICON | (largeIcon ? SHGFI_LARGEICON : SHGFI_SMALLICON));

                    if (shfi.hIcon != IntPtr.Zero)
                    {
                        iconSource = Imaging.CreateBitmapSourceFromHIcon(shfi.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        DestroyIcon(shfi.hIcon);
                        return iconSource;
                    }

                    return null;
                }

                // 使用PIDL获取图标
                if (pidl != IntPtr.Zero)
                {
                    SHFILEINFO shfi = new SHFILEINFO();
                    IntPtr result = SHGetFileInfo(pidl, 0, ref shfi, (uint)Marshal.SizeOf(shfi), flags);

                    if (result != IntPtr.Zero && shfi.hIcon != IntPtr.Zero)
                    {
                        iconSource = Imaging.CreateBitmapSourceFromHIcon(shfi.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

                        DestroyIcon(shfi.hIcon);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"获取图标时出错: {ex.Message}");
            }
            finally
            {
                // 释放PIDL
                if (pidl != IntPtr.Zero)
                {
                    ILFree(pidl);
                }
            }

            return iconSource;
        }

        //public static ImageSource GetShellItemIcon(string path)
        //{
        //    if (string.IsNullOrEmpty(path))
        //    {
        //        return null;
        //    }
        //    IntPtr pidl = ILCreateFromPath(path);
        //    if (pidl == IntPtr.Zero)
        //    {
        //        return null;
        //    }
        //    try
        //    {
        //        SHFILEINFO shfi = new SHFILEINFO();
        //        if (SHGetFileInfo(pidl, 0, ref shfi, (uint)Marshal.SizeOf(shfi), SHGFI_ICON | SHGFI_SMALLICON | SHGFI_PIDL) != 0)
        //        {
        //            if (shfi.hIcon != IntPtr.Zero)
        //            {
        //                ImageSource iconSource = Imaging.CreateBitmapSourceFromHIcon(shfi.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        //                DestroyIcon(shfi.hIcon);
        //                return iconSource;
        //            }
        //        }
        //        return null;
        //    }
        //    finally
        //    {
        //        ILFree(pidl);
        //    }
        //}

        /// <summary>
        /// 映射常见的特殊文件夹名称到CLSID
        /// </summary>
        public static string MapSpecialFolderByName(string displayName)
        {
            if (specialFolders.TryGetValue(displayName, out string path))
            {
                return path;
            }
            return null;
        }

        public static string GetSpecialFolderDisplayName(string path)
        {
            return specialFolders.FirstOrDefault(x => x.Value == path).Key;
        }


        /// <summary>
        /// 获取当前Windows用户的全名(本地用户名)
        /// </summary>
        private static string GetUserFullName()
        {
            try
            {
                // 获取当前Windows用户标识
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    // 创建主体对象
                    WindowsPrincipal principal = new WindowsPrincipal(identity);

                    // 返回用户名
                    return identity.Name.Split('\\')[1]; // 通常格式为 "DOMAIN\username"
                }
            }
            catch
            {
                // 如果出错，回退到简单的用户名
                return Environment.UserName;
            }
        }

        /// <summary>
        /// 从Windows API获取当前用户的显示名称
        /// </summary>
        public static string GetDisplayCurrentUserName()
        {
            using (var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT * FROM Win32_UserAccount WHERE Domain='" +
                Environment.UserDomainName + "' AND Name='" +
                Environment.UserName + "'"))
            {
                foreach (var o in searcher.Get())
                {
                    var user = (System.Management.ManagementObject)o;
                    return user["FullName"]?.ToString() ?? Environment.UserName;
                }
            }
            return Environment.UserName;
        }

        public static void OpenSpecialFolder(string path)
        {
            try
            {
                ProcessStartInfo psi;

                // 处理特殊文件夹标识符（以::开头的路径）
                if (path.StartsWith("::"))
                {
                    psi = new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = path,
                        UseShellExecute = true
                    };
                }
                else
                {
                    // 普通文件夹路径
                    psi = new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    };
                }

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Log.Error($"无法打开目录: {path}. \r\n原因: {ex.Message}");
            }
        }
    }
}
