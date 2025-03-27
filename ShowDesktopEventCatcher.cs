using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace System.Windows;

internal class ShowDesktopEventCatcher
{
    #region 拦截事件

    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;

    // 窗口消息常量
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_LBUTTONDOWN = 0x0201;

    // 键盘值常量
    private const int VK_D = 0x44;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;

    // Windows事件常量
    private const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNTHREAD = 0x0001;

    // 钩子处理函数的委托
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
    private delegate void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    // 钩子处理函数实例
    private LowLevelKeyboardProc _keyboardProc;
    private LowLevelMouseProc _mouseProc;
    private WinEventProc _winEventProc;

    // 钩子句柄
    private IntPtr _keyboardHookHandle = IntPtr.Zero;
    private IntPtr _mouseHookHandle = IntPtr.Zero;
    private IntPtr _winEventHookHandle = IntPtr.Zero;

    // 跟踪Win键状态
    private bool _isWinKeyDown = false;

    // 检测显示桌面区域的坐标
    private RECT _showDesktopButtonRect;
    private bool _isShowDesktopButtonRectInitialized = false;

    // Windows API导入
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    // 键盘钩子的结构
    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    // 鼠标钩子的结构
    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    // 点结构
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    // 矩形结构
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public void Initialize()
    {
        // 初始化委托
        _keyboardProc = KeyboardHookCallback;
        _mouseProc = MouseHookCallback;
        _winEventProc = WinEvent2Callback;

        // 窗口关闭时移除钩子
        //Closing += MainWindow_Closing;

        // 初始化显示桌面按钮位置
        InitializeShowDesktopButtonRect();

        // 显示初始状态
        //LogMessage("应用程序已启动，请选择要拦截的操作。");
    }

    private void InitializeShowDesktopButtonRect()
    {
        // 查找任务栏和显示桌面按钮
        IntPtr taskbarHandle = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Shell_TrayWnd", null);
        if (taskbarHandle != IntPtr.Zero)
        {
            // Windows 11中，显示桌面按钮通常在任务栏右下角
            IntPtr desktopToolbar = FindWindowEx(taskbarHandle, IntPtr.Zero, "TrayNotifyWnd", null);
            if (desktopToolbar != IntPtr.Zero)
            {
                IntPtr showDesktopButton = FindWindowEx(desktopToolbar, IntPtr.Zero, "TrayShowDesktopButtonWClass", null);
                if (showDesktopButton != IntPtr.Zero)
                {
                    GetWindowRect(showDesktopButton, out _showDesktopButtonRect);
                    _isShowDesktopButtonRectInitialized = true;
                    //LogMessage($"已定位显示桌面按钮: ({_showDesktopButtonRect.Left},{_showDesktopButtonRect.Top})-({_showDesktopButtonRect.Right},{_showDesktopButtonRect.Bottom})");
                }
                else
                {
                    // 如果找不到具体的按钮，估计位置在任务栏右下角
                    GetWindowRect(taskbarHandle, out RECT taskbarRect);
                    _showDesktopButtonRect.Left = taskbarRect.Right - 10;
                    _showDesktopButtonRect.Top = taskbarRect.Bottom - 10;
                    _showDesktopButtonRect.Right = taskbarRect.Right;
                    _showDesktopButtonRect.Bottom = taskbarRect.Bottom;
                    _isShowDesktopButtonRectInitialized = true;
                    //LogMessage("未找到显示桌面按钮，使用任务栏右下角区域进行拦截");
                }
            }
        }
    }

    // 键盘钩子回调函数
    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
            {
                KBDLLHOOKSTRUCT keyInfo = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                // 检测Win键按下
                if (keyInfo.vkCode == VK_LWIN || keyInfo.vkCode == VK_RWIN)
                {
                    _isWinKeyDown = true;
                }

                // 检测Win+D组合
                if (_isWinKeyDown && keyInfo.vkCode == VK_D)
                {
                    //LogMessage("已拦截Win+D组合键!");
                    return (IntPtr)1; // 阻止事件传递
                }
            }
            else if (wParam == (IntPtr)WM_KEYUP)
            {
                KBDLLHOOKSTRUCT keyInfo = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                // 检测Win键释放
                if (keyInfo.vkCode == VK_LWIN || keyInfo.vkCode == VK_RWIN)
                {
                    _isWinKeyDown = false;
                }
            }
        }

        return CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
    }

    // 鼠标钩子回调函数
    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN && _isShowDesktopButtonRectInitialized)
        {
            MSLLHOOKSTRUCT mouseInfo = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

            // 检查鼠标点击是否在显示桌面按钮区域
            if (mouseInfo.pt.x >= _showDesktopButtonRect.Left && mouseInfo.pt.x <= _showDesktopButtonRect.Right &&
                mouseInfo.pt.y >= _showDesktopButtonRect.Top && mouseInfo.pt.y <= _showDesktopButtonRect.Bottom)
            {
                //LogMessage($"已拦截显示桌面按钮点击! 坐标: ({mouseInfo.pt.x}, {mouseInfo.pt.y})");
                return (IntPtr)1; // 阻止事件传递
            }
        }

        return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
    }

    // Windows事件回调，用于检测显示桌面事件
    private void WinEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (eventType == EVENT_SYSTEM_MINIMIZESTART)
        {
            // 检测多窗口同时最小化情况，通常表示显示桌面
            //Dispatcher.Invoke(() => {
            //    LogMessage("检测到可能的显示桌面事件");
            //});
        }
    }

    // 设置键盘钩子，阻止Win+D
    private void SetKeyboardHook()
    {
        _keyboardHookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc,
            GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName), 0);

        if (_keyboardHookHandle == IntPtr.Zero)
        {
            //LogMessage($"键盘钩子设置失败，错误代码: {Marshal.GetLastWin32Error()}");
        }
        else
        {
            //LogMessage("键盘钩子已设置，将拦截Win+D组合键");
        }
    }

    // 移除键盘钩子
    private void RemoveKeyboardHook()
    {
        if (_keyboardHookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHookHandle);
            _keyboardHookHandle = IntPtr.Zero;
            //LogMessage("键盘钩子已移除，Win+D恢复正常功能");
        }
    }

    // 设置鼠标钩子，阻止显示桌面按钮点击
    private void SetMouseHook()
    {
        _mouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc,
            GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName), 0);

        if (_mouseHookHandle == IntPtr.Zero)
        {
            //LogMessage($"鼠标钩子设置失败，错误代码: {Marshal.GetLastWin32Error()}");
        }
        else
        {
            //LogMessage("鼠标钩子已设置，将拦截显示桌面按钮点击");
        }
    }

    // 移除鼠标钩子
    private void RemoveMouseHook()
    {
        if (_mouseHookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHookHandle);
            _mouseHookHandle = IntPtr.Zero;
            //LogMessage("鼠标钩子已移除，显示桌面按钮恢复正常功能");
        }
    }
    #endregion


    // 定义一个委托用于事件回调
    public delegate void ShowDesktopEventHandler(object sender, EventArgs e);

    // 创建一个显示桌面事件
    public static event ShowDesktopEventHandler OnShowDesktop;

    // Windows API导入
    //[DllImport("user32.dll", SetLastError = true)]
    //private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
    //    WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    //[DllImport("user32.dll")]
    //private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    //[DllImport("user32.dll")]
    //private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    // Windows事件常量
    //private const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
    private const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;
    //private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    //private const uint WINEVENT_SKIPOWNTHREAD = 0x0001;


    // Windows事件回调函数委托
    //private delegate void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
    //    int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    // 事件钩子句柄
    private static IntPtr _hookHandle = IntPtr.Zero;

    // 上次最小化的窗口数量
    private static int _lastMinimizedCount = 0;

    // 主应用程序入口点
    static void Main(string[] args)
    {
        // 注册事件处理程序
        OnShowDesktop += Program_OnShowDesktop;

        // 设置Windows事件钩子
        InstallHook();

        Console.WriteLine("显示桌面事件监听已启动...");
        Console.WriteLine("请尝试以下操作触发事件:");
        Console.WriteLine("1. 点击右下角显示桌面按钮");
        Console.WriteLine("2. 按下 Win+D 组合键");
        Console.WriteLine("3. 按下 Win+M 组合键");
        Console.WriteLine("按ESC键退出程序");

        // 保持程序运行直到按下ESC键
        while (Console.ReadKey(true).Key != ConsoleKey.Escape)
        {
            System.Threading.Thread.Sleep(100);
        }

        // 移除钩子
        RemoveHook();
    }

    // 安装Windows事件钩子
    private static void InstallHook()
    {
        // 创建事件回调函数
        WinEventProc winEventProc = new WinEventProc(WinEvent2Callback);

        // 设置窗口最小化事件的钩子
        _hookHandle = SetWinEventHook(
            EVENT_SYSTEM_MINIMIZESTART,
            EVENT_SYSTEM_MINIMIZEEND,
            IntPtr.Zero,
            winEventProc,
            0,
            0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNTHREAD);

        // 保持回调函数的引用防止被垃圾回收
        GC.KeepAlive(winEventProc);
    }

    // 移除Windows事件钩子
    private static void RemoveHook()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWinEvent(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    // Windows事件回调处理函数
    private static void WinEvent2Callback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // 处理窗口最小化开始事件
        if (eventType == EVENT_SYSTEM_MINIMIZESTART)
        {
            // 计算当前被最小化的窗口数量
            int currentMinimizedCount = CountMinimizedWindows();

            // 检测是否为显示桌面操作 - 当多个窗口在同一时间被最小化时
            if (currentMinimizedCount - _lastMinimizedCount > 2)
            {
                // 通过计时器确认是否为批量最小化操作
                CheckForShowDesktopEvent();
            }

            _lastMinimizedCount = currentMinimizedCount;
        }
    }

    // 检查是否为显示桌面事件
    private static void CheckForShowDesktopEvent()
    {
        // 使用定时器进行短暂延迟
        //Timer timer = new Timer();
        //timer.Interval = 50; // 50毫秒延迟
        //timer.Tick += (sender, e) =>
        //{
        //    timer.Stop();
        //    // 如果短时间内有大量窗口被最小化，触发显示桌面事件
        //    OnShowDesktop?.Invoke(null, EventArgs.Empty);
        //};
        //timer.Start();
    }

    // 计算当前最小化的窗口数量 (简化版实现)
    private static int CountMinimizedWindows()
    {
        int count = 0;

        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                if (process.MainWindowHandle != IntPtr.Zero &&
                    !IsWindowVisible(process.MainWindowHandle))
                {
                    count++;
                }
            }
            catch
            {
                // 忽略无法访问的进程
            }
        }

        return count;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    // 显示桌面事件的处理函数
    private static void Program_OnShowDesktop(object sender, EventArgs e)
    {
        Console.WriteLine($"[{DateTime.Now}] 检测到显示桌面事件!");

        // 这里可以添加您需要的自定义处理逻辑
        // 例如：保存当前工作状态，触发自定义操作等
    }
}
