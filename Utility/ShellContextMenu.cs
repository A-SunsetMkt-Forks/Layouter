using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Shapes;
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
        public static bool ShowContextMenuForIcon(DesktopIcon icon, Point point, Window parentWindow)
        {
            if (icon == null || string.IsNullOrEmpty(icon.IconPath))
            {
                Log.Error("无效的图标");
                return false;
            }

            bool isSpecialObj = false;
            string path = icon.IconPath;

            try
            {
                // 确保路径存在
                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    // 判断是否是系统图标
                    var systemIconType = ShellUtil.GetSystemIconType(path);

                    if (systemIconType != null)
                    {
                        // 获取系统图标路径
                        switch (systemIconType.Value)
                        {
                            case SystemIconType.UserFiles:
                                path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                                break;
                            default:
                                path = icon.IconPath;
                                isSpecialObj = true;
                                break;
                        }

                    }
                    //非桌面图标,作为其他Windows Shell对象处理
                    else
                    {
                        isSpecialObj = true;
                    }
                }

                // 将菜单显示位置转换为屏幕坐标
                Point windowPos = parentWindow.PointToScreen(new Point(0, 0));
                point.X = point.X + windowPos.X;
                point.Y = point.Y + windowPos.Y;

                // 显示上下文菜单，确保使用正确的窗口和位置
                return ShowContextMenu(path, point, parentWindow, isSpecialObj);
            }
            catch
            {
                Log.Error("图标路径不存在");
                return false;
            }
        }

        /// <summary>
        /// 显示指定文件或文件夹的Shell上下文菜单
        /// </summary>
        /// <param name="filePath">文件或文件夹的完整路径</param>
        /// <param name="point">菜单显示位置</param>
        /// <param name="parentWindow">父窗口</param>
        /// <param name="isSpecialObj">是否特殊桌面图标</param>
        public static bool ShowContextMenu(string filePath, Point point, Window parentWindow, bool isSpecialObj = false)
        {
            if (!isSpecialObj && (string.IsNullOrEmpty(filePath) || !File.Exists(filePath) && !Directory.Exists(filePath)))
            {
                Log.Error("指定的文件或文件夹不存在");
                return false;
            }

            IntPtr handle = (parentWindow != null) ? new WindowInteropHelper(parentWindow).Handle : IntPtr.Zero;

            IntPtr ptrShellFolder = IntPtr.Zero;    // IShellFolder接口指针
            IntPtr ptrContextMenu = IntPtr.Zero;    // IContextMenu接口指针
            IntPtr ptrMenu = IntPtr.Zero;             // 菜单句柄
            IntPtr[] pidlLasts = new IntPtr[1];     // 存放最后一个项目的PIDL指针的数组
            uint psfgaoOut = 0;

            try
            {
                IntPtr pidl = IntPtr.Zero;
                if (isSpecialObj)
                {
                    // 对于CLSID路径，使用SHParseDisplayName获取PIDL
                    SHParseDisplayName(filePath, IntPtr.Zero, out pidl, 0, out psfgaoOut);
                }
                else
                {
                    pidl = ILCreateFromPath(filePath);
                }

                if (pidl == IntPtr.Zero)
                {
                    uint attrs;
                    int hr2 = SHParseDisplayName(filePath, IntPtr.Zero, out pidl, 0, out attrs);
                    if (hr2 != 0 || pidl == IntPtr.Zero)
                    {
                        Log.Error("无法获取文件的PIDL");
                        return false;
                    }
                }

                Guid guidShellFolder = new Guid("000214E6-0000-0000-C000-000000000046");
                Guid guidContextMenu = new Guid("000214E4-0000-0000-C000-000000000046");

                int result = SHBindToParent(pidl, ref guidShellFolder, out ptrShellFolder, out pidlLasts[0]);
                if (result != 0)
                {
                    Log.Error("无法获取文件的Shell文件夹");
                    return false;
                }
                IShellFolder shellFolder = (IShellFolder)Marshal.GetTypedObjectForIUnknown(ptrShellFolder, typeof(IShellFolder));

                //取到文件类型特定的上下文菜单
                int hr = shellFolder.GetUIObjectOf(handle, (uint)1, pidlLasts, ref guidContextMenu, IntPtr.Zero, out ptrContextMenu);
                if (hr != 0 || ptrContextMenu == IntPtr.Zero)
                {
                    Log.Error("无法获取上下文菜单");
                    return false;
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
                            nShow = 1,                        // SW_SHOWNORMAL
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

            return true;
        }

    }
}