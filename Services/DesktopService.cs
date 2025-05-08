using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using Layouter.Models;
using Layouter.Utility;

namespace Layouter.Services
{
    public class DesktopService
    {
        private static readonly Lazy<DesktopService> instance = new Lazy<DesktopService>(() => new DesktopService());
        public static DesktopService Instance => instance.Value;

        private WndProcDelegate wndProcDelegate;
        private IntPtr oldWndProc;

        // 标准Windows窗口过程委托
        public delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);


        public void RegisterDesktopDoubleClickEvent()
        {
            try
            {
                HookWindow();
                Log.Information("已注册桌面双击事件");
            }
            catch (Exception ex)
            {
                Log.Information($"注册桌面双击事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 注销桌面双击事件
        /// </summary>
        public void UnregisterDesktopDoubleClickEvent()
        {
            try
            {
                UnhookWindow();
                Log.Information("已注销桌面双击事件");
            }
            catch (Exception ex)
            {
                Log.Information($"注销桌面双击事件失败: {ex.Message}");
            }
        }

        private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                // 处理鼠标双击消息
                if (msg == (int)WindowsMessage.WM_LBUTTONDBLCLK)
                {
                    Log.Information("检测到桌面双击事件");
                    //ToggleDesktopIcons();
                }
            }
            catch (Exception ex)
            {
                Log.Information($"处理窗口消息失败: {ex.Message}");
            }

            return IntPtr.Zero;
        }

        public void HookWindow()
        {
            IntPtr window = DesktopUtil.GetShellViewWindow();
            wndProcDelegate = new WndProcDelegate(WndProc);
            oldWndProc = Win32.SetWindowLongPtr(window, Win32.WindowLongFlags.GWL_WNDPROC, Marshal.GetFunctionPointerForDelegate(wndProcDelegate));
        }

        // 记得在适当时机恢复原窗口过程
        public void UnhookWindow()
        {
            IntPtr window = DesktopUtil.GetShellViewWindow();
            if (oldWndProc != IntPtr.Zero)
            {
                Win32.SetWindowLongPtr(window, Win32.WindowLongFlags.GWL_WNDPROC, oldWndProc);
                oldWndProc = IntPtr.Zero;
            }
        }

    }
}
