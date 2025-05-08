using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Layouter.Models;
using Microsoft.CodeAnalysis;
using static System.Windows.Win32;

namespace Layouter.Utility
{
    /// <summary>
    /// 提供Windows Shell上下文菜单功能的类
    /// </summary>
    public class ShellContextMenu
    {
        /// <summary>
        /// 为DesktopIcon显示Shell上下文菜单
        /// </summary>
        /// <param name="icon">桌面图标</param>
        /// <param name="point">菜单显示位置</param>
        /// <param name="parentWindow">父窗口</param>
        public static void ShowContextMenuForIcon(DesktopIcon icon, Point point, Window parentWindow)
        {
            if (icon == null || string.IsNullOrEmpty(icon.IconPath))
            {
                MessageBox.Show("无效的图标", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string path = icon.IconPath;

            // 处理特殊图标类型
            // 注意：对于快捷方式，我们应该使用快捷方式文件本身的路径来显示上下文菜单
            // 而不是目标路径，因为用户可能想要对快捷方式本身执行操作
            // 如果需要对目标文件执行操作，用户可以通过快捷方式菜单中的"打开文件位置"选项

            // 处理Shell特殊文件夹类型
            if (icon.IconType == IconType.Shell)
            {
                // 对于Shell特殊文件夹，可能需要特殊处理
                // 目前保留原始路径
                Debug.WriteLine($"Shell类型图标: {path}");
            }

            // 确保路径存在
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                Debug.WriteLine($"路径不存在: {path}");
                // 如果路径不存在，尝试使用原始路径
                path = icon.IconPath;
            }

            Debug.WriteLine($"使用路径显示上下文菜单: {path}");

            // 显示上下文菜单，确保使用正确的窗口和位置
            ShowContextMenu(path, point, parentWindow);
        }


        /// <summary>
        /// 显示指定文件或文件夹的Shell上下文菜单
        /// </summary>
        /// <param name="filePath">文件或文件夹的完整路径</param>
        /// <param name="point">菜单显示位置</param>
        /// <param name="parentWindow">父窗口</param>
        public static void ShowContextMenu(string filePath, Point point, Window parentWindow)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath) && !Directory.Exists(filePath))
            {
                MessageBox.Show("指定的文件或文件夹不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            IntPtr handle = (parentWindow != null) ? new WindowInteropHelper(parentWindow).Handle : IntPtr.Zero;

            IntPtr ptrShellFolder = IntPtr.Zero;    // IShellFolder接口指针
            IntPtr ptrContextMenu = IntPtr.Zero;    // IContextMenu接口指针
            IntPtr ptrMenu = IntPtr.Zero;             // 菜单句柄
            IntPtr[] pidlLasts = new IntPtr[1];     // 存放最后一个项目的PIDL指针的数组

            try
            {
                IntPtr pidl = ILCreateFromPath(filePath);
                if (pidl == IntPtr.Zero)
                {
                    MessageBox.Show("无法获取文件的PIDL", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Guid guidShellFolder = new Guid("000214E6-0000-0000-C000-000000000046");
                Guid guidContextMenu = new Guid("000214E4-0000-0000-C000-000000000046");

                int result = SHBindToParent(pidl, ref guidShellFolder, out ptrShellFolder, out pidlLasts[0]);
                if (result != 0)
                {
                    MessageBox.Show("无法获取文件的Shell文件夹", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                IShellFolder shellFolder = (IShellFolder)Marshal.GetTypedObjectForIUnknown(ptrShellFolder, typeof(IShellFolder));

                //取到文件类型特定的上下文菜单
                int hr = shellFolder.GetUIObjectOf(handle, (uint)1, pidlLasts, ref guidContextMenu, IntPtr.Zero, out ptrContextMenu);
                if (hr != 0 || ptrContextMenu == IntPtr.Zero)
                {
                    MessageBox.Show("无法获取上下文菜单", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                IContextMenu3 contextMenu = (IContextMenu3)Marshal.GetTypedObjectForIUnknown(ptrContextMenu, typeof(IContextMenu3));
                //获取菜单句柄
                ptrMenu = CreatePopupMenu();

                if (ptrMenu != IntPtr.Zero)
                {
                    // 用上下文项填充菜单
                    contextMenu.QueryContextMenu(
                        ptrMenu,
                        0,          // 开始添加菜单项的索引
                        1,          // 最小命令ID
                        0x7FFF,     // 最大命令ID
                        CMF_ITEMMENU | CMF_NORMAL | CMF_ASYNCVERBSTATE | CMF_CANRENAME
                        );

                    // 显示菜单并跟踪选择
                    uint selected = (uint)TrackPopupMenuEx(ptrMenu,
                        TPM_RETURNCMD | TPM_RIGHTBUTTON,
                        (int)point.X,       // X坐标
                        (int)point.Y,       // Y坐标
                        handle,             // 所有者窗口
                        IntPtr.Zero);       // 无扩展参数

                    // 如果用户选择了项目
                    if (selected > 0)
                    {
                        // 准备命令调用结构
                        var invokeInfo = new CMINVOKECOMMANDINFOEX
                        {
                            cbSize = Marshal.SizeOf(typeof(CMINVOKECOMMANDINFOEX)),
                            fMask = 0x00004000 | 0x20000000,  // CMIC_MASK_UNICODE | CMIC_MASK_PTINVOKE
                            hwnd = handle,                    // 所有者窗口
                            lpVerb = (IntPtr)(selected - 1),  // 要调用的命令(基于0)
                            lpVerbW = (IntPtr)(selected - 1), // Unicode命令
                            nShow = 1,                       // SW_SHOWNORMAL
                            ptInvoke = new POINT { X = (int)point.X, Y = (int)point.Y } // 调用点

                        };

                        // 执行选定的命令
                        contextMenu.InvokeCommand(ref invokeInfo);
                    }
                }
            }
            finally
            {
                if (ptrMenu != IntPtr.Zero)
                {
                    DestroyMenu(ptrMenu);
                }
                if (ptrContextMenu != IntPtr.Zero)
                {
                    Marshal.Release(ptrContextMenu);
                }
                if (ptrShellFolder != IntPtr.Zero)
                {
                    Marshal.Release(ptrShellFolder);
                }
            }

        }


        ///// <summary>
        ///// 显示指定文件或文件夹的Shell上下文菜单
        ///// </summary>
        ///// <param name="filePath">文件或文件夹的完整路径</param>
        ///// <param name="point">菜单显示位置</param>
        ///// <param name="parentWindow">父窗口</param>
        //public static void ShowContextMenu(string filePath, Point point, Window parentWindow)
        //{
        //    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath) && !Directory.Exists(filePath))
        //    {
        //        MessageBox.Show("指定的文件或文件夹不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        //        return;
        //    }

        //    try
        //    {
        //        // 获取文件的PIDL
        //        IntPtr pidl = ILCreateFromPath(filePath);
        //        if (pidl == IntPtr.Zero)
        //        {
        //            MessageBox.Show("无法获取文件的PIDL", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        //            return;
        //        }

        //        try
        //        {
        //            // 获取桌面文件夹
        //            IShellFolder desktopFolder;
        //            int hr = SHGetDesktopFolder(out desktopFolder);
        //            if (hr != 0)
        //            {
        //                MessageBox.Show("无法获取桌面文件夹", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        //                return;
        //            }

        //            // 获取文件的父文件夹和相对PIDL
        //            IntPtr folderPidl = IntPtr.Zero;
        //            IntPtr relativePidl = IntPtr.Zero;
        //            IShellFolder folder = desktopFolder;

        //            // 获取文件的父目录路径
        //            string parentDir = Path.GetDirectoryName(filePath);
        //            if (!string.IsNullOrEmpty(parentDir))
        //            {
        //                // 获取父目录的PIDL
        //                IntPtr parentPidl = ILCreateFromPath(parentDir);
        //                if (parentPidl != IntPtr.Zero)
        //                {
        //                    try
        //                    {
        //                        // 获取父目录的IShellFolder接口
        //                        Guid IID_IShellFolder = new Guid("000214E6-0000-0000-C000-000000000046");
        //                        IntPtr pParentFolder;
        //                        hr = desktopFolder.BindToObject(parentPidl, IntPtr.Zero, ref IID_IShellFolder, out pParentFolder);

        //                        if (hr == 0 && pParentFolder != IntPtr.Zero)
        //                        {
        //                            // 使用父目录的IShellFolder接口
        //                            folder = (IShellFolder)Marshal.GetTypedObjectForIUnknown(pParentFolder, typeof(IShellFolder));

        //                            // 获取文件的相对PIDL
        //                            string fileName = Path.GetFileName(filePath);
        //                            uint eaten = 0;
        //                            uint attributes = 0;
        //                            hr = folder.ParseDisplayName(IntPtr.Zero, IntPtr.Zero, fileName, ref eaten, out relativePidl, ref attributes);

        //                            if (hr == 0 && relativePidl != IntPtr.Zero)
        //                            {
        //                                // 使用相对PIDL而不是完整PIDL
        //                                ILFree(pidl);
        //                                pidl = relativePidl;
        //                                relativePidl = IntPtr.Zero;
        //                            }
        //                            else
        //                            {
        //                                // 释放资源
        //                                Marshal.ReleaseComObject(folder);
        //                                folder = desktopFolder;
        //                            }

        //                            Marshal.Release(pParentFolder);
        //                        }
        //                    }
        //                    finally
        //                    {
        //                        if (parentPidl != IntPtr.Zero)
        //                        {
        //                            ILFree(parentPidl);
        //                        }
        //                    }
        //                }
        //            }

        //            // 创建弹出菜单
        //            IntPtr hMenu = CreatePopupMenu();
        //            if (hMenu == IntPtr.Zero)
        //            {
        //                MessageBox.Show("无法创建菜单", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        //                return;
        //            }

        //            try
        //            {
        //                // 准备文件PIDL数组
        //                IntPtr[] apidl = new IntPtr[] { pidl };

        //                // 获取文件的上下文菜单
        //                Guid IID_IContextMenu = new Guid("000214e4-0000-0000-c000-000000000046");
        //                IntPtr pContextMenu;

        //                // 使用文件所在的父文件夹获取上下文菜单，而不是桌面文件夹
        //                // 这样可以确保获取到正确的文件类型特定的上下文菜单
        //                hr = folder.GetUIObjectOf(parentWindow != null ? new WindowInteropHelper(parentWindow).Handle : IntPtr.Zero, 1, apidl, ref IID_IContextMenu, IntPtr.Zero, out pContextMenu);

        //                if (hr != 0 || pContextMenu == IntPtr.Zero)
        //                {
        //                    MessageBox.Show("无法获取上下文菜单", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        //                    return;
        //                }

        //                try
        //                {
        //                    // 查询IContextMenu接口
        //                    IContextMenu contextMenu = (IContextMenu)Marshal.GetTypedObjectForIUnknown(pContextMenu, typeof(IContextMenu));

        //                    // 填充菜单
        //                    contextMenu.QueryContextMenu(hMenu, 0, 1, 0x7FFF, CMF_NORMAL | CMF_EXPLORE);

        //                    // 获取高级接口以处理子菜单
        //                    IContextMenu2 contextMenu2 = null;
        //                    IContextMenu3 contextMenu3 = null;

        //                    try
        //                    {
        //                        contextMenu2 = (IContextMenu2)contextMenu;
        //                    }
        //                    catch { }

        //                    try
        //                    {
        //                        contextMenu3 = (IContextMenu3)contextMenu;
        //                    }
        //                    catch { }

        //                    // 创建消息钩子来处理菜单消息
        //                    MessageHook hook = new MessageHook(contextMenu2, contextMenu3);
        //                    HwndSource hwndSource = null;

        //                    try
        //                    {
        //                        // 安装钩子
        //                        if (parentWindow != null)
        //                        {
        //                            hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(parentWindow).Handle);
        //                            hwndSource.AddHook(hook.HookProc);
        //                        }

        //                        // 显示上下文菜单
        //                        // 确保使用正确的窗口句柄
        //                        IntPtr hwnd = parentWindow != null ? new WindowInteropHelper(parentWindow).Handle : IntPtr.Zero;

        //                        // 获取鼠标在屏幕上的坐标
        //                        // 将窗口客户区坐标转换为屏幕坐标
        //                        POINT clientPt = new POINT { X = (int)point.X, Y = (int)point.Y };
        //                        POINT screenPt = clientPt;

        //                        if (parentWindow != null)
        //                        {
        //                            // 获取窗口位置
        //                            Point windowPos = parentWindow.PointToScreen(new Point(0, 0));
        //                            screenPt.X = (int)(clientPt.X + windowPos.X);
        //                            screenPt.Y = (int)(clientPt.Y + windowPos.Y);
        //                        }

        //                        // 调用TrackPopupMenuEx并正确获取返回的命令ID
        //                        uint command = TrackPopupMenuEx(hMenu, TPM_RETURNCMD | TPM_LEFTALIGN | TPM_RIGHTBUTTON, screenPt.X, screenPt.Y, hwnd, IntPtr.Zero);

        //                        // 执行选择的命令
        //                        if (command > 0)
        //                        {
        //                            CMINVOKECOMMANDINFO invoke = new CMINVOKECOMMANDINFO();
        //                            invoke.cbSize = Marshal.SizeOf(invoke);
        //                            invoke.hwnd = parentWindow != null ? new WindowInteropHelper(parentWindow).Handle : IntPtr.Zero;

        //                            // 使用命令ID直接执行命令
        //                            // 在Windows API中，如果lpVerb是一个数值ID，它必须是一个MAKEINTRESOURCE宏转换的值
        //                            // 命令ID是相对于idCmdFirst的偏移量，我们在QueryContextMenu时使用了1作为idCmdFirst
        //                            // 因此这里需要减去1来获取正确的命令索引
        //                            uint cmdIndex = command - 1;
        //                            Debug.WriteLine($"原始命令ID: {command}, 计算后的命令索引: {cmdIndex}");
        //                            invoke.lpVerb = (IntPtr)((int)cmdIndex & 0x0000FFFF);

        //                            // 尝试使用不同的命令索引计算方式
        //                            // 有些Shell扩展可能使用不同的偏移量计算方式
        //                            uint altCmdIndex = command;  // 不减1，直接使用原始命令ID

        //                            // 尝试获取命令的字符串标识符用于调试
        //                            try
        //                            {
        //                                // 分配内存用于接收命令字符串
        //                                IntPtr pszName = Marshal.AllocCoTaskMem(256);
        //                                try
        //                                {
        //                                    // 初始化内存
        //                                    for (int i = 0; i < 256; i++)
        //                                        Marshal.WriteByte(pszName, i, 0);

        //                                    // 获取命令的字符串标识符
        //                                    // 定义不同的标志位
        //                                    const uint GCS_VERBA = 0x00000000;    // 获取ANSI格式的命令字符串
        //                                    const uint GCS_VERBW = 0x00000004;    // 获取Unicode格式的命令字符串
        //                                    const uint GCS_VALIDATEA = 0x00000001; // 验证ANSI字符串是否存在
        //                                    const uint GCS_VALIDATEW = 0x00000005; // 验证Unicode字符串是否存在
        //                                    const uint GCS_HELPTEXTA = 0x00000002; // 获取ANSI格式的帮助文本
        //                                    const uint GCS_HELPTEXTW = 0x00000006; // 获取Unicode格式的帮助文本

        //                                    // 尝试使用GCS_VERBA获取命令字符串 - 使用原始命令索引
        //                                    int cmdVal = contextMenu.GetCommandString(cmdIndex, GCS_VERBA, IntPtr.Zero, pszName, 256);
        //                                    if (cmdVal == 0)
        //                                    {
        //                                        string verb = Marshal.PtrToStringAnsi(pszName);
        //                                        Debug.WriteLine($"命令 {command} 的ANSI字符串标识符: {verb}");
        //                                    }
        //                                    else
        //                                    {
        //                                        Debug.WriteLine($"无法获取命令 {command} 的ANSI字符串标识符, 错误码: {cmdVal}");

        //                                        // 如果ANSI格式失败，尝试使用GCS_VERBW获取Unicode格式
        //                                        cmdVal = contextMenu.GetCommandString(cmdIndex, GCS_VERBW, IntPtr.Zero, pszName, 256);

        //                                        // 如果使用cmdIndex失败，尝试使用altCmdIndex（不减1的原始命令ID）
        //                                        if (cmdVal != 0)
        //                                        {
        //                                            Debug.WriteLine($"尝试使用原始命令ID: {altCmdIndex}");
        //                                            cmdVal = contextMenu.GetCommandString(altCmdIndex, GCS_VERBA, IntPtr.Zero, pszName, 256);
        //                                            if (cmdVal == 0)
        //                                            {
        //                                                string verb = Marshal.PtrToStringAnsi(pszName);
        //                                                Debug.WriteLine($"使用原始命令ID成功! 命令 {command} 的ANSI字符串标识符: {verb}");
        //                                            }
        //                                            else
        //                                            {
        //                                                cmdVal = contextMenu.GetCommandString(altCmdIndex, GCS_VERBW, IntPtr.Zero, pszName, 256);
        //                                                if (cmdVal == 0)
        //                                                {
        //                                                    string verb = Marshal.PtrToStringUni(pszName);
        //                                                    Debug.WriteLine($"使用原始命令ID成功! 命令 {command} 的Unicode字符串标识符: {verb}");
        //                                                }
        //                                                else
        //                                                {
        //                                                    Debug.WriteLine($"使用原始命令ID也失败，错误码: {cmdVal}");
        //                                                }
        //                                            }
        //                                        }
        //                                        if (cmdVal == 0)
        //                                        {
        //                                            string verb = Marshal.PtrToStringUni(pszName);
        //                                            Debug.WriteLine($"命令 {command} 的Unicode字符串标识符: {verb}");
        //                                        }
        //                                        else
        //                                        {
        //                                            Debug.WriteLine($"无法获取命令 {command} 的Unicode字符串标识符, 错误码: {cmdVal}");

        //                                            // 尝试使用验证标志位
        //                                            cmdVal = contextMenu.GetCommandString(cmdIndex, GCS_VALIDATEW, IntPtr.Zero, pszName, 256);
        //                                            Debug.WriteLine($"验证Unicode字符串结果: {cmdVal}");

        //                                            // 尝试使用帮助文本标志位
        //                                            cmdVal = contextMenu.GetCommandString(cmdIndex, GCS_HELPTEXTW, IntPtr.Zero, pszName, 256);
        //                                            if (cmdVal == 0)
        //                                            {
        //                                                string helpText = Marshal.PtrToStringUni(pszName);
        //                                                Debug.WriteLine($"命令 {command} 的帮助文本: {helpText}");
        //                                            }
        //                                            else
        //                                            {
        //                                                Debug.WriteLine($"无法获取命令 {command} 的帮助文本, 错误码: {cmdVal}");
        //                                            }

        //                                            // 尝试使用不同的标志位组合
        //                                            // 有些Shell扩展可能需要特定的标志位组合
        //                                            const uint GCS_UNICODE = 0x0001;  // 使用Unicode字符串
        //                                            const uint GCS_VERB = 0x0000;     // 获取命令字符串

        //                                            // 尝试使用GCS_VERB | GCS_UNICODE组合
        //                                            cmdVal = contextMenu.GetCommandString(cmdIndex, GCS_VERB | GCS_UNICODE, IntPtr.Zero, pszName, 256);
        //                                            if (cmdVal == 0)
        //                                            {
        //                                                string verb = Marshal.PtrToStringUni(pszName);
        //                                                Debug.WriteLine($"使用GCS_VERB | GCS_UNICODE成功! 命令字符串: {verb}");
        //                                            }
        //                                            else
        //                                            {
        //                                                Debug.WriteLine($"使用GCS_VERB | GCS_UNICODE失败, 错误码: {cmdVal}");

        //                                                // 尝试使用不同的内存分配方式
        //                                                // 有些Shell扩展可能对内存分配有特定要求
        //                                                IntPtr pszNameAlt = Marshal.AllocHGlobal(512);
        //                                                try
        //                                                {
        //                                                    // 初始化内存
        //                                                    for (int i = 0; i < 512; i++)
        //                                                        Marshal.WriteByte(pszNameAlt, i, 0);

        //                                                    // 尝试使用不同的内存和不同的命令索引
        //                                                    for (uint testIndex = 0; testIndex < 5; testIndex++)
        //                                                    {
        //                                                        uint testCmd = command > testIndex ? command - testIndex : 0;
        //                                                        Debug.WriteLine($"尝试命令索引: {testCmd}");

        //                                                        cmdVal = contextMenu.GetCommandString(testCmd, GCS_VERBA, IntPtr.Zero, pszNameAlt, 512);
        //                                                        if (cmdVal == 0)
        //                                                        {
        //                                                            string verb = Marshal.PtrToStringAnsi(pszNameAlt);
        //                                                            Debug.WriteLine($"成功! 命令索引 {testCmd} 的ANSI字符串: {verb}");
        //                                                            break;
        //                                                        }

        //                                                        cmdVal = contextMenu.GetCommandString(testCmd, GCS_VERBW, IntPtr.Zero, pszNameAlt, 512);
        //                                                        if (cmdVal == 0)
        //                                                        {
        //                                                            string verb = Marshal.PtrToStringUni(pszNameAlt);
        //                                                            Debug.WriteLine($"成功! 命令索引 {testCmd} 的Unicode字符串: {verb}");
        //                                                            break;
        //                                                        }
        //                                                    }
        //                                                }
        //                                                finally
        //                                                {
        //                                                    Marshal.FreeHGlobal(pszNameAlt);
        //                                                }
        //                                            }
        //                                        }
        //                                    }
        //                                }
        //                                finally
        //                                {
        //                                    Marshal.FreeCoTaskMem(pszName);
        //                                }
        //                            }
        //                            catch (Exception ex)
        //                            {
        //                                Debug.WriteLine($"获取命令字符串时出错: {ex.Message}");
        //                            }

        //                            Debug.WriteLine($"执行命令ID: {cmdIndex}");

        //                            // 设置显示方式为正常显示
        //                            invoke.nShow = 1; // SW_SHOWNORMAL

        //                            // 设置标志位 - CMIC_MASK_FLAG_NO_UI 可以防止显示错误消息
        //                            invoke.fMask = 0x400; // CMIC_MASK_FLAG_NO_UI

        //                            // 设置工作目录为文件所在目录
        //                            // 这对于打开文件或启动程序很重要
        //                            invoke.lpDirectory = Path.GetDirectoryName(filePath);

        //                            // 不需要额外参数
        //                            invoke.lpParameters = null;

        //                            try
        //                            {
        //                                // 执行命令
        //                                contextMenu.InvokeCommand(ref invoke);
        //                            }
        //                            catch (Exception ex)
        //                            {
        //                                Debug.WriteLine($"执行菜单命令时出错: {ex.Message}");
        //                            }
        //                            finally
        //                            {
        //                                // 不需要释放内存，因为我们使用的是命令ID而不是字符串
        //                            }

        //                        }
        //                    }
        //                    finally
        //                    {
        //                        // 移除钩子
        //                        if (hwndSource != null)
        //                        {
        //                            hwndSource.RemoveHook(hook.HookProc);
        //                        }
        //                    }
        //                }
        //                finally
        //                {
        //                    // 释放COM对象
        //                    if (pContextMenu != IntPtr.Zero)
        //                    {
        //                        Marshal.Release(pContextMenu);
        //                    }

        //                    // 如果folder不是desktopFolder，需要释放
        //                    if (folder != null && folder != desktopFolder)
        //                    {
        //                        try
        //                        {
        //                            Marshal.ReleaseComObject(folder);
        //                        }
        //                        catch (Exception ex)
        //                        {
        //                            Debug.WriteLine($"释放文件夹COM对象时出错: {ex.Message}");
        //                        }
        //                    }
        //                }
        //            }
        //            finally
        //            {
        //                if (hMenu != IntPtr.Zero)
        //                {
        //                    DestroyMenu(hMenu);
        //                }
        //            }
        //        }
        //        finally
        //        {
        //            if (pidl != IntPtr.Zero)
        //            {
        //                ILFree(pidl);
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine($"显示上下文菜单时出错: {ex.Message}");
        //        MessageBox.Show($"显示上下文菜单时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        //    }
        //}


        /// <summary>
        /// 消息钩子类，用于处理上下文菜单的消息
        /// </summary>
        //private class MessageHook
        //{
        //    private readonly IContextMenu2 _contextMenu2;
        //    private readonly IContextMenu3 _contextMenu3;

        //    public MessageHook(IContextMenu2 contextMenu2, IContextMenu3 contextMenu3)
        //    {
        //        _contextMenu2 = contextMenu2;
        //        _contextMenu3 = contextMenu3;
        //    }

        //    public IntPtr HookProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        //    {
        //        switch (msg)
        //        {
        //            case WM_INITMENUPOPUP:
        //            case WM_DRAWITEM:
        //            case WM_MEASUREITEM:
        //                if (_contextMenu3 != null)
        //                {
        //                    //_contextMenu3.HandleMenuMsg2((uint)msg, wParam, lParam, IntPtr.Zero);
        //                    handled = true;
        //                }
        //                else if (_contextMenu2 != null)
        //                {
        //                    _contextMenu2.HandleMenuMsg((uint)msg, wParam, lParam);
        //                    handled = true;
        //                }
        //                break;
        //        }

        //        return IntPtr.Zero;
        //    }
        //}
    }
}