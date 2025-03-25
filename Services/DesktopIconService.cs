using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Layouter.Models;
using Layouter.Utility;

namespace Layouter.Services
{
    public class DesktopIconService
    {
        private static readonly Lazy<DesktopIconService> instance = new Lazy<DesktopIconService>(() => new DesktopIconService());
        public static DesktopIconService Instance => instance.Value;

        // 获取桌面路径
        public string GetDesktopPath()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }

        // 获取桌面上的所有图标
        public List<DesktopIcon> GetDesktopIcons()
        {
            List<DesktopIcon> icons = new List<DesktopIcon>();
            string desktopPath = GetDesktopPath();

            try
            {
                foreach (string file in Directory.GetFiles(desktopPath))
                {
                    // 排除隐藏文件
                    if ((File.GetAttributes(file) & FileAttributes.Hidden) != 0)
                    {
                        continue;
                    }
                    string fileName = Path.GetFileName(file);
                    icons.Add(new DesktopIcon
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        IconPath = file,
                        Position = new Point(0, 0), // 默认位置
                        Size = new Size(64, 64)     // 默认大小
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Information($"获取桌面图标时出错: {ex.Message}");
            }

            return icons;
        }

        /// <summary>
        /// 刷新桌面
        /// </summary>
        public void RefreshDesktop()
        {
            try
            {
                // 获取桌面窗口和视图
                IntPtr hDesktop = Win32.FindWindow("Progman", "Program Manager"); 
                IntPtr hWorkerW = IntPtr.Zero;
                IntPtr hDefView = Win32.FindWindowEx(hDesktop, IntPtr.Zero, "SHELLDLL_DefView", null);

                if (hDefView == IntPtr.Zero)
                {
                    // 如果没有找到默认视图，尝试通过WorkerW查找
                    while (hDefView == IntPtr.Zero)
                    {
                        hWorkerW = Win32.FindWindowEx(IntPtr.Zero, hWorkerW, "WorkerW", null);
                        if (hWorkerW == IntPtr.Zero) break;
                        hDefView = Win32.FindWindowEx(hWorkerW, IntPtr.Zero, "SHELLDLL_DefView", null);
                    }
                }

                if (hDefView != IntPtr.Zero)
                {
                    // 通知Shell刷新桌面
                    IntPtr hListView = Win32.FindWindowEx(hDefView, IntPtr.Zero, "SysListView32", "FolderView");
                    if (hListView != IntPtr.Zero)
                    {
                        const int WM_COMMAND = 0x0111;
                        const int REFRESH_COMMAND = 0x7103;

                        // 使用 SendMessage 代替 HandleMessage
                        Win32.SendMessage(hListView, WM_COMMAND, (IntPtr)REFRESH_COMMAND, IntPtr.Zero);
                    }
                }

                // 使用SHChangeNotify通知系统更改
                Win32.SHChangeNotify(Win32.SHCNE_CREATE, Win32.SHCNF_IDLIST | Win32.SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Log.Information($"刷新桌面时出错: {ex.Message}");
            }
        }



        /// <summary>
        /// 创建桌面快捷方式
        /// </summary>
        /// <param name="targetPath"></param>
        /// <param name="shortcutName"></param>
        /// <returns></returns>
        public bool CreateShortcutOnDesktop(string targetPath, string shortcutName)
        {
            try
            {
                string desktopPath = GetDesktopPath();
                string shortcutPath = Path.Combine(desktopPath, $"{shortcutName}.lnk");

                if (File.Exists(shortcutPath))
                {
                    // 快捷方式已存在
                    return false;
                }

                Type shellLinkType = Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"));
                var link = (Win32.IShellLink)Activator.CreateInstance(shellLinkType);

                link.SetPath(targetPath);
                link.SetDescription($"Shortcut to {Path.GetFileName(targetPath)}");

                // 设置图标
                link.SetIconLocation(targetPath, 0);

                // 获取IPersistFile接口保存文件
                var file = (Win32.IPersistFile)link;
                file.Save(shortcutPath, true);

                // 刷新桌面
                RefreshDesktop();

                return true;
            }
            catch (Exception ex)
            {
                Log.Information($"创建桌面快捷方式时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 根据DesktopIcon对象创建桌面快捷方式
        /// </summary>
        /// <param name="icon"></param>
        /// <returns></returns>
        public bool CreateShortcutOnDesktop(DesktopIcon icon)
        {
            try
            {
                if (icon == null || string.IsNullOrEmpty(icon.IconPath))
                {
                    Log.Information("创建快捷方式失败：图标或路径为空");
                    return false;
                }

                // 获取文件名和目标路径
                string desktopPath = GetDesktopPath();
                string iconFileName = Path.GetFileName(icon.IconPath);
                string shortcutName = Path.GetFileNameWithoutExtension(icon.IconPath);
                string shortcutPath = Path.Combine(desktopPath, $"{shortcutName}.lnk");
                string targetPath = icon.IconPath;

                // 如果源是快捷方式，直接复制到桌面
                bool isSourceShortcut = icon.IconPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);
                if (isSourceShortcut)
                {
                    Log.Information($"源是快捷方式，直接复制到桌面: {icon.IconPath}");
                    return CopyFileToDesktop(icon.IconPath);
                }

                // 检查是否已存在同名快捷方式
                if (File.Exists(shortcutPath))
                {
                    int counter = 1;
                    string baseShortcutName = shortcutName;

                    // 生成不重复的快捷方式名称
                    do
                    {
                        shortcutName = $"{baseShortcutName} ({counter})";
                        shortcutPath = Path.Combine(desktopPath, $"{shortcutName}.lnk");
                        counter++;
                    } while (File.Exists(shortcutPath));
                }

                Log.Information($"创建快捷方式：{shortcutPath} -> {targetPath}");

                // 创建快捷方式
                Type shellLinkType = Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"));
                var link = (Win32.IShellLink)Activator.CreateInstance(shellLinkType);

                link.SetPath(targetPath);
                link.SetDescription($"Shortcut to {Path.GetFileName(targetPath)}");

                // 设置图标
                link.SetIconLocation(targetPath, 0);

                // 获取IPersistFile接口保存文件
                var file = (Win32.IPersistFile)link;
                file.Save(shortcutPath, true);

                // 刷新桌面
                RefreshDesktop();

                Log.Information("快捷方式创建成功");
                return true;
            }
            catch (Exception ex)
            {
                Log.Information($"创建桌面快捷方式时出错: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 复制文件到桌面
        /// </summary>
        /// <param name="sourceFilePath"></param>
        /// <param name="newFileName"></param>
        /// <returns></returns>
        public bool CopyFileToDesktop(string sourceFilePath, string newFileName = null)
        {
            try
            {
                string fileName = newFileName ?? Path.GetFileName(sourceFilePath);
                string desktopPath = GetDesktopPath();
                string targetPath = Path.Combine(desktopPath, fileName);

                // 如果目标文件存在，则使用新名称
                if (File.Exists(targetPath))
                {
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    string extension = Path.GetExtension(fileName);
                    int counter = 1;

                    do
                    {
                        fileName = $"{fileNameWithoutExt} ({counter}){extension}";
                        targetPath = Path.Combine(desktopPath, fileName);
                        counter++;
                    } while (File.Exists(targetPath));
                }

                // 尝试复制文件 - 如果源文件存在则复制，否则只返回目标路径
                if (File.Exists(sourceFilePath))
                {
                    File.Copy(sourceFilePath, targetPath);
                    Log.Information($"已将文件复制到桌面: {targetPath}");
                }
                else
                {
                    Log.Information($"源文件不存在，无法复制: {sourceFilePath}");
                    return false;
                }

                // 刷新桌面以显示新文件
                RefreshDesktop();

                return true;
            }
            catch (Exception ex)
            {
                Log.Information($"复制文件到桌面时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从分区拖回到桌面
        /// </summary>
        /// <param name="icon"></param>
        /// <returns></returns>
        public bool RestoreIconToDesktop(DesktopIcon icon)
        {
            try
            {
                if (icon == null || string.IsNullOrEmpty(icon.IconPath))
                {
                    return false;
                }

                // 检查文件是否存在
                if (!File.Exists(icon.IconPath))
                {
                    return false;
                }

                // 检查文件是否已经在桌面上
                string desktopPath = GetDesktopPath();
                string fileName = Path.GetFileName(icon.IconPath);
                string desktopFilePath = Path.Combine(desktopPath, fileName);

                if (string.Equals(icon.IconPath, desktopFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    // 文件已经在桌面上，不需要额外操作
                    return true;
                }

                // 如果是快捷方式，则创建快捷方式
                if (Path.GetExtension(icon.IconPath).ToLower() == ".lnk")
                {
                    // 获取快捷方式指向的文件
                    Type shellLinkType = Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"));
                    var link = (Win32.IShellLink)Activator.CreateInstance(shellLinkType);
                    ((Win32.IPersistFile)link).Load(icon.IconPath, 0);

                    System.Text.StringBuilder targetPath = new System.Text.StringBuilder(260);
                    link.GetPath(targetPath, targetPath.Capacity, out IntPtr _, 0);

                    // 在桌面上创建新的快捷方式
                    return CreateShortcutOnDesktop(targetPath.ToString(), icon.Name);
                }
                else
                {
                    // 否则复制文件到桌面
                    return CopyFileToDesktop(icon.IconPath);
                }
            }
            catch (Exception ex)
            {
                Log.Information($"将图标恢复到桌面时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 隐藏桌面上的文件图标 - 使用增强版隐藏方法
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public bool HideDesktopIcon(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    Log.Information($"无法隐藏不存在的文件: {filePath}");
                    return false;
                }

                // 创建特殊的隐藏文件夹来存储隐藏的图标
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string hiddenFolderPath = Path.Combine(desktopPath, Env.HiddenFolderName);

                // 确保隐藏文件夹存在
                if (!Directory.Exists(hiddenFolderPath))
                {
                    // 创建隐藏文件夹
                    Directory.CreateDirectory(hiddenFolderPath);

                    // 设置文件夹为隐藏+系统属性，这样即使在"显示隐藏文件"开启时也不太容易被看到
                    File.SetAttributes(hiddenFolderPath, FileAttributes.Hidden | FileAttributes.System | FileAttributes.Directory);
                }

                // 获取文件名和扩展名
                string fileName = Path.GetFileName(filePath);

                // 目标路径（隐藏文件夹中的文件路径）
                string targetPath = Path.Combine(hiddenFolderPath, fileName);

                // 创建记录文件位置的元数据文件，用于稍后恢复
                string metadataPath = Path.Combine(hiddenFolderPath, $"{fileName}.meta");

                // 如果目标文件已存在，先尝试删除
                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }

                // 复制文件到隐藏文件夹
                File.Copy(filePath, targetPath);

                // 记录原始文件路径到元数据文件
                File.WriteAllText(metadataPath, filePath);

                // 删除原始文件
                File.Delete(filePath);

                Log.Information($"成功隐藏桌面图标: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Information($"隐藏桌面图标时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 显示桌面上的文件图标 - 配合增强版隐藏方法
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public bool ShowDesktopIcon(string filePath)
        {
            try
            {
                // 确保文件路径有效
                if (string.IsNullOrEmpty(filePath))
                {
                    Log.Information("无法显示空路径的图标");
                    return false;
                }

                // 获取文件名
                string fileName = Path.GetFileName(filePath);

                // 获取隐藏文件夹路径
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string hiddenFolderPath = Path.Combine(desktopPath, Env.HiddenFolderName);

                // 检查隐藏文件夹是否存在
                if (!Directory.Exists(hiddenFolderPath))
                {
                    Log.Information("隐藏文件夹不存在，无法恢复图标");
                    return false;
                }

                // 隐藏文件夹中的文件路径
                string hiddenFilePath = Path.Combine(hiddenFolderPath, fileName);
                string metadataPath = Path.Combine(hiddenFolderPath, $"{fileName}.meta");

                // 检查文件是否存在于隐藏文件夹中
                if (!File.Exists(hiddenFilePath))
                {
                    Log.Information($"隐藏文件夹中不存在此文件: {fileName}");
                    return false;
                }

                // 检查原始路径是否存在于元数据文件中
                string originalPath = filePath;
                if (File.Exists(metadataPath))
                {
                    // 从元数据读取原始路径
                    originalPath = File.ReadAllText(metadataPath);
                }

                // 如果目标位置已存在文件，我们不会覆盖它
                if (File.Exists(originalPath))
                {
                    Log.Information($"目标位置已存在文件: {originalPath}，无法恢复");
                    return false;
                }

                // 复制文件回原位置
                File.Copy(hiddenFilePath, originalPath);

                // 删除隐藏文件和元数据
                File.Delete(hiddenFilePath);
                if (File.Exists(metadataPath))
                {
                    File.Delete(metadataPath);
                }

                // 清理空的隐藏文件夹
                if (Directory.GetFiles(hiddenFolderPath).Length == 0)
                {
                    try
                    {
                        Directory.Delete(hiddenFolderPath);
                    }
                    catch
                    {
                        // 忽略清理隐藏文件夹时的错误
                    }
                }

                Log.Information($"成功显示桌面图标: {originalPath}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Information($"显示桌面图标时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 根据DesktopIcon对象隐藏或显示桌面图标
        /// </summary>
        /// <param name="icon"></param>
        /// <param name="hide"></param>
        /// <returns></returns>
        public bool ToggleDesktopIconVisibility(DesktopIcon icon, bool hide)
        {
            if (icon == null || string.IsNullOrEmpty(icon.IconPath))
            {
                return false;
            }

            return hide ? HideDesktopIcon(icon.IconPath) : ShowDesktopIcon(icon.IconPath);
        }

        /// <summary>
        /// 移除路径中的隐藏目录标识
        /// </summary>
        public string RemoveHiddenPathInIconPath(string iconPath)
        {
            if (iconPath.Contains(Env.HiddenFolderName))
            {
                return iconPath.Replace($"{Env.HiddenFolderName}{Path.DirectorySeparatorChar}", "");
            }
            return iconPath;
        }

        /// <summary>
        /// 为路径添加隐藏目录标识
        /// </summary>
        public string CombineHiddenPathWithIconPath(string filePath)
        {
            if (filePath.Contains(Env.HiddenFolderName))
            {
                return filePath;
            }
            else
            {
                var fi = new FileInfo(filePath);
                return filePath.Replace(fi.Name, $"{Env.HiddenFolderName}{Path.DirectorySeparatorChar}{fi.Name}");

            }
        }

        /// <summary>
        /// 获取可用的图标路径
        /// </summary>
        /// <param name="iconPath"></param>
        /// <returns></returns>
        public string GetAvailableIconPath(string iconPath)
        {
            string purePath = string.Empty;
            string hiddenPath = string.Empty;

            if (iconPath.Contains(Env.HiddenFolderName))
            {
                purePath = RemoveHiddenPathInIconPath(iconPath);
                hiddenPath = iconPath;
            }
            else
            {
                purePath = iconPath;
                hiddenPath = CombineHiddenPathWithIconPath(iconPath);
            }

            if (File.Exists(purePath))
            {
                return purePath;
            }
            else if (File.Exists(hiddenPath))
            {
                return hiddenPath;
            }
            return iconPath;
        }
    }
}
