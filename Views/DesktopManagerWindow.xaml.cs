using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using Layouter.Models;
using Layouter.Services;
using Layouter.Utility;
using Layouter.ViewModels;

namespace Layouter.Views
{
    public partial class DesktopManagerWindow : Window
    {
        // 分区ViewModel
        private PartitionViewModel _viewModel;
        private Point? dragStartPoint = null;
        private bool isDragging = false;
        private DesktopIcon draggedIcon = null;

        public DesktopManagerWindow()
        {
            InitializeComponent();

            // 创建ViewModel并设置为DataContext
            _viewModel = new PartitionViewModel
            {
                Name = $"分区 {DateTime.Now.ToString("HH:mm:ss")}"
            };

            DataContext = _viewModel;

            // 挂载事件
            Loaded += DesktopManagerWindow_Loaded;
            Closing += Window_Closing;
            PreviewDragOver += DesktopManagerWindow_PreviewDragOver;
            Drop += DesktopManagerWindow_Drop;
            MouseRightButtonDown += DesktopManagerWindow_MouseRightButtonDown;

            // 初始化拖拽状态
            isDragging = false;

            // 注册到窗口管理服务
            WindowManagerService.Instance.RegisterWindow(this);
        }

        private void DesktopManagerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 加载分区数据
            LoadPartitionData();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // 保存分区数据
            SavePartitionData();

            // 只隐藏窗口，不关闭应用程序
            e.Cancel = true;
            this.Hide();
        }

        private void DesktopManagerWindow_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var contextMenu = this.FindResource("PartitionContextMenu") as ContextMenu;
            if (contextMenu != null)
            {
                contextMenu.IsOpen = true;
            }
        }

        private void DesktopManagerWindow_PreviewDragOver(object sender, DragEventArgs e)
        {
            // 支持文件拖拽
            if (e.Data.GetDataPresent(DataFormats.FileDrop) ||
                e.Data.GetDataPresent("DraggedIconSource"))
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        private void DesktopManagerWindow_Drop(object sender, DragEventArgs e)
        {
            try
            {
                // 获取拖放的数据对象
                var data = e.Data;
                DesktopIcon newIcon = null;

                // 在拖放结束时获取鼠标坐标 - 相对于窗口
                Point dropPoint = e.GetPosition(this);

                // 放置点坐标 - 以Panel为相对坐标系
                double x = dropPoint.X;
                double y = dropPoint.Y;

                // 检查是否是从桌面拖过来的文件
                if (data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = data.GetData(DataFormats.FileDrop) as string[];

                    if (files != null && files.Length > 0)
                    {
                        string filePath = files[0]; // 取第一个文件

                        // 检查是否已经在这个分区中 - 如果已存在则静默返回，不提示
                        var viewModel = DataContext as PartitionViewModel;
                        if (viewModel.Icons.Any(i => i.IconPath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                        {
                            return; // 不做任何操作，也不显示消息框
                        }

                        // 创建新的图标对象
                        newIcon = new DesktopIcon
                        {
                            Id = Guid.NewGuid().ToString(),
                            Name = Path.GetFileNameWithoutExtension(filePath),
                            IconPath = filePath,
                            Position = new Point(x, y),
                            Size = new Size(64, 64)
                        };

                        // 隐藏桌面上的原图标
                        var desktopIconService = new DesktopIconService();
                        bool hidden = desktopIconService.HideDesktopIcon(filePath);

                        if (!hidden)
                        {
                            System.Diagnostics.Debug.WriteLine($"警告：无法隐藏桌面上的图标，但仍会将其添加到分区：{filePath}");
                        }
                    }
                }
                // 如果是从其他分区拖过来的
                else if (data.GetDataPresent("DraggedIconSource") && data.GetData("DraggedIconSource").ToString() == "Partition")
                {
                    // 获取源分区窗口句柄
                    IntPtr sourceWindowHandle = IntPtr.Zero;
                    if (data.GetDataPresent("SourceWindowHandle"))
                    {
                        sourceWindowHandle = (IntPtr)data.GetData("SourceWindowHandle");
                    }

                    // 当前窗口句柄
                    IntPtr currentWindowHandle = new WindowInteropHelper(this).Handle;

                    // 如果是从其他分区窗口拖过来的
                    if (sourceWindowHandle != IntPtr.Zero && sourceWindowHandle != currentWindowHandle)
                    {
                        string iconId = data.GetData("IconId").ToString();
                        string iconName = data.GetData("IconName").ToString();
                        string iconPath = data.GetData("IconPath").ToString();

                        // 检查是否已经在这个分区中 - 如果已存在则静默返回，不提示
                        var viewModel = DataContext as PartitionViewModel;
                        if (viewModel.Icons.Any(i => i.IconPath.Equals(iconPath, StringComparison.OrdinalIgnoreCase)))
                        {
                            return; // 不做任何操作，也不显示消息框
                        }

                        // 创建新的图标，并设置其位置为拖放点
                        newIcon = new DesktopIcon
                        {
                            Id = iconId,  // 保持相同的ID
                            Name = iconName,
                            IconPath = iconPath,
                            Position = new Point(x, y),
                            Size = new Size(64, 64)
                        };

                        // 通知源窗口图标已被接收，可以从源分区移除
                        foreach (var window in Application.Current.Windows)
                        {
                            if (window is DesktopManagerWindow desktopManager)
                            {
                                IntPtr windowHandle = new WindowInteropHelper(desktopManager).Handle;
                                if (windowHandle == sourceWindowHandle)
                                {
                                    // 找到源窗口，通知其移除图标
                                    desktopManager.HandleIconMovedToOtherPartition(iconId);
                                    break;
                                }
                            }
                        }
                    }
                }

                // 如果成功创建了新图标，添加到当前分区
                if (newIcon != null)
                {
                    var viewModel = DataContext as PartitionViewModel;
                    viewModel?.AddIcon(newIcon);

                    // 保存分区数据
                    SavePartitionData();

                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理拖放时出错: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void SavePartitionData()
        {
            try
            {
                // 使用现有的服务保存分区数据
                PartitionDataService.Instance.SavePartitionData(this);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存分区数据时出错: {ex.Message}");
            }
        }

        private void LoadPartitionData()
        {
            try
            {
                // 使用现有的服务加载分区数据
                PartitionDataService.Instance.LoadPartitionData(this);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载分区数据时出错: {ex.Message}");
            }
        }

        // Win32 API
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        // 处理图标已被拖放到其他分区的通知
        public void HandleIconMovedToOtherPartition(string iconId)
        {
            try
            {
                var viewModel = this.DataContext as PartitionViewModel;
                var icon = viewModel?.GetIconById(iconId);

                if (icon != null)
                {
                    System.Diagnostics.Debug.WriteLine($"收到通知：图标已被移动到其他分区，从当前分区移除: {icon.Name}");

                    // 从当前分区中移除图标
                    viewModel.RemoveIcon(icon);

                    // 保存更改
                    SavePartitionData();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理图标移动通知时出错: {ex.Message}");
            }
        }

        // 在窗口初始化完成后自动开始编辑名称
        public void EnableTitleEditOnFirstLoad()
        {
            if (string.IsNullOrWhiteSpace(TitleTextBlock.Text) || TitleTextBlock.Text == "新分区")
            {
                // 使用Dispatcher延迟执行，确保UI已完全加载
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ShowTitleEditor();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private void ShowTitleEditor()
        {
            try
            {
                // 隐藏标题文本
                TitleTextBlock.Visibility = Visibility.Collapsed;

                // 设置编辑框内容
                TitleEditBox.Text = TitleTextBlock.Text;

                // 显示编辑框
                TitleEditBox.Visibility = Visibility.Visible;

                // 聚焦并全选文本
                TitleEditBox.Focus();
                TitleEditBox.SelectAll();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示标题编辑器时出错: {ex.Message}");
            }
        }

        private void TitleEditBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SaveTitleEdit();
            }
            else if (e.Key == Key.Escape)
            {
                // 取消编辑
                TitleTextBlock.Visibility = Visibility.Visible;
                TitleEditBox.Visibility = Visibility.Collapsed;
            }
        }

        private void TitleEditBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveTitleEdit();
        }

        private void SaveTitleEdit()
        {
            try
            {
                // 保存新标题
                if (!string.IsNullOrWhiteSpace(TitleEditBox.Text))
                {
                    TitleTextBlock.Text = TitleEditBox.Text;

                    // 更新ViewModel中的名称
                    var viewModel = DataContext as PartitionViewModel;
                    if (viewModel != null)
                    {
                        viewModel.Name = TitleEditBox.Text;

                        // 保存到配置
                        SavePartitionData();
                    }
                }

                // 恢复显示文本块
                TitleTextBlock.Visibility = Visibility.Visible;
                TitleEditBox.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存标题编辑时出错: {ex.Message}");
            }
        }

        private void TitleBar_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // 双击标题栏开始编辑名称
            ShowTitleEditor();
        }

        private void TitleTextBlock_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // 检测是否是双击
            if (e.ClickCount == 2)
            {
                ShowTitleEditor();
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            // 显示设置下拉菜单
            SettingsPopup.IsOpen = true;
        }

        private void NewPartition_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 创建新的分区窗口
                var window = new DesktopManagerWindow();
                window.Show();

                // 在新窗口显示后让标题变为可编辑状态
                window.EnableTitleEditOnFirstLoad();

                System.Diagnostics.Debug.WriteLine("已创建新分区窗口");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建新分区窗口时出错: {ex.Message}");
            }
        }

        private void DeletePartition_Click(object sender, RoutedEventArgs e)
        {
            // 关闭设置菜单
            SettingsPopup.IsOpen = false;

            // 确认是否删除
            MessageBoxResult result = MessageBox.Show(
                "确定要删除该分区吗？分区内的所有图标将会恢复到桌面。",
                "删除确认",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.OK)
            {
                // 执行删除操作
                var windowManagerService = WindowManagerService.Instance;
                windowManagerService.RemovePartitionWindow(this.Title);
            }
        }

        private void ChangeBackground_Click(object sender, RoutedEventArgs e)
        {
            // 关闭设置菜单
            SettingsPopup.IsOpen = false;

            // TODO: 实现背景颜色修改功能
            MessageBox.Show("背景颜色修改功能暂未实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AutoArrange_Click(object sender, RoutedEventArgs e)
        {
            // 关闭设置菜单
            SettingsPopup.IsOpen = false;

            // 执行自动排列
            var viewModel = DataContext as PartitionViewModel;
            viewModel?.ArrangeIcons();
        }

        private void AlignPartitions_Click(object sender, RoutedEventArgs e)
        {
            // 关闭设置菜单
            SettingsPopup.IsOpen = false;

            try
            {
                // 获取所有分区窗口
                var windows = Application.Current.Windows.OfType<DesktopManagerWindow>().ToList();
                if (windows.Count <= 1)
                    return;

                // 获取屏幕工作区尺寸
                double screenWidth = SystemParameters.WorkArea.Width;
                double screenHeight = SystemParameters.WorkArea.Height;

                // 计算每个窗口的理想尺寸
                int numWindows = windows.Count;
                int numCols = (int)Math.Ceiling(Math.Sqrt(numWindows));
                int numRows = (int)Math.Ceiling((double)numWindows / numCols);

                double windowWidth = screenWidth / numCols;
                double windowHeight = screenHeight / numRows;

                // 分配窗口位置
                for (int i = 0; i < windows.Count; i++)
                {
                    int row = i / numCols;
                    int col = i % numCols;

                    // 计算窗口位置
                    double left = col * windowWidth;
                    double top = row * windowHeight;

                    // 设置窗口位置和大小
                    windows[i].Left = left;
                    windows[i].Top = top;
                    windows[i].Width = windowWidth;
                    windows[i].Height = windowHeight;

                    // 保存每个窗口的数据
                    windows[i].SavePartitionData();
                }

                System.Diagnostics.Debug.WriteLine("已对齐所有分区窗口");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"对齐分区窗口时出错: {ex.Message}");
            }
        }

        private void ClosePartition_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 支持拖动窗口
            if (e.ClickCount == 1)
            {
                this.DragMove();
            }
        }

        // 图标右键菜单事件
        private void MenuItem_Open(object sender, RoutedEventArgs e)
        {
            try
            {
                // 获取菜单项的Tag，即对应的DesktopIcon对象
                var menuItem = sender as MenuItem;
                if (menuItem?.Tag is DesktopIcon icon)
                {
                    // 使用Process.Start打开文件
                    Process.Start(icon.IconPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuItem_Delete(object sender, RoutedEventArgs e)
        {
            try
            {
                // 获取菜单项的Tag，即对应的DesktopIcon对象
                var menuItem = sender as MenuItem;
                if (menuItem?.Tag is DesktopIcon icon)
                {
                    // 确认是否删除
                    MessageBoxResult result = MessageBox.Show(
                        "确定要从分区中删除此图标吗？",
                        "删除确认",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // 从分区移除图标
                        var viewModel = DataContext as PartitionViewModel;
                        viewModel?.RemoveIcon(icon);

                        // 保存配置
                        SavePartitionData();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除图标时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuItem_Properties(object sender, RoutedEventArgs e)
        {
            try
            {
                // 获取菜单项的Tag，即对应的DesktopIcon对象
                var menuItem = sender as MenuItem;
                if (menuItem?.Tag is DesktopIcon icon)
                {
                    // 使用rundll32.exe打开属性对话框
                    string command = $"rundll32.exe shell32.dll,ShellExec_RunDLL properties \"{icon.IconPath}\"";

                    Process process = new Process();
                    process.StartInfo.FileName = "cmd.exe";
                    process.StartInfo.Arguments = $"/c {command}";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法查看属性: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void IconsContainer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 检查点击的是否是图标
            var element = e.OriginalSource as FrameworkElement;
            while (element != null && !(element.Tag is DesktopIcon))
            {
                element = element.Parent as FrameworkElement;
            }

            if (element != null && element.Tag is DesktopIcon icon)
            {
                dragStartPoint = e.GetPosition(this);
                draggedIcon = icon;
            }
        }

        private void IconsContainer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && draggedIcon != null && !isDragging)
            {
                var currentPos = e.GetPosition(this);
                if (Math.Abs(currentPos.X - dragStartPoint.Value.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(currentPos.Y - dragStartPoint.Value.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    try
                    {
                        // 开始拖拽操作
                        isDragging = true;

                        // 标记图标正在拖拽
                        draggedIcon.IsDragging = true;

                        // 创建拖拽数据 - 使用文件拖拽格式，这样桌面可以接受
                        var dataObject = new DataObject();
                        var originalIcon = draggedIcon;

                        // 获取当前位置和窗口句柄，用于判断拖放结果
                        POINT startPt;
                        GetCursorPos(out startPt);
                        IntPtr startWindowHandle = WindowFromPoint(startPt);

                        // 存储原始图标文件路径
                        string originalFilePath = draggedIcon.IconPath;
                        bool isShortcut = originalFilePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);

                        // 将图标路径添加为可拖放的文件 - 这是关键，桌面只接受FileDrop格式
                        string[] files = new string[] { draggedIcon.IconPath };
                        dataObject.SetData(DataFormats.FileDrop, files);

                        // 同时添加我们自己的自定义数据
                        dataObject.SetData("DraggedIconSource", "Partition");
                        dataObject.SetData("IconId", draggedIcon.Id);
                        dataObject.SetData("IconName", draggedIcon.Name);
                        dataObject.SetData("IconPath", draggedIcon.IconPath);
                        dataObject.SetData("SourceWindowHandle", new WindowInteropHelper(this).Handle);

                        // 开始拖放操作
                        var result = DragDrop.DoDragDrop(this, dataObject, DragDropEffects.Copy | DragDropEffects.Move);

                        // 拖拽结束，获取结束位置
                        POINT endPt;
                        GetCursorPos(out endPt);
                        IntPtr endWindowHandle = WindowFromPoint(endPt);

                        // 获取当前窗口句柄和区域
                        IntPtr currentWindowHandle = new WindowInteropHelper(this).Handle;
                        RECT currentWindowRect;
                        GetWindowRect(currentWindowHandle, out currentWindowRect);

                        // 判断是否拖放到了当前窗口外部
                        bool isOutsideCurrentWindow =
                            endPt.X < currentWindowRect.Left || endPt.X > currentWindowRect.Right ||
                            endPt.Y < currentWindowRect.Top || endPt.Y > currentWindowRect.Bottom;

                        // 检查是否拖放到了别的分区窗口
                        bool isOnOtherPartition = false;
                        foreach (var otherWindow in WindowManagerService.Instance.GetAllWindows())
                        {
                            if (otherWindow == this)
                            {
                                continue;
                            }
                            IntPtr otherHandle = new WindowInteropHelper(otherWindow).Handle;
                            if (endWindowHandle == otherHandle)
                            {
                                isOnOtherPartition = true;
                                break;
                            }
                        }

                        // 如果拖放到了窗口外部，且不是到其他分区，则认为是拖放到桌面
                        if (isOutsideCurrentWindow && !isOnOtherPartition)
                        {
                            System.Diagnostics.Debug.WriteLine("图标可能被拖到桌面或其他应用");

                            // 处理拖放到桌面的逻辑
                            var viewModel = this.DataContext as PartitionViewModel;
                            if (viewModel != null)
                            {
                                // 从分区中移除图标
                                viewModel.RemoveIcon(originalIcon);

                                // 保存更改
                                SavePartitionData();
                            }
                        }

                        // 无论如何都要重置状态
                        isDragging = false;
                        if (draggedIcon != null)
                        {
                            draggedIcon.IsDragging = false;
                            draggedIcon = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"拖放操作异常: {ex.Message}\n{ex.StackTrace}");

                        // 出现异常时重置状态
                        isDragging = false;
                        if (draggedIcon != null)
                        {
                            draggedIcon.IsDragging = false;
                            draggedIcon = null;
                        }
                    }
                }
            }
        }

        private void Icon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // 检查点击的是否是图标
            var element = e.OriginalSource as FrameworkElement;
            while (element != null && !(element.Tag is DesktopIcon))
            {
                element = element.Parent as FrameworkElement;
            }

            if (element != null && element.Tag is DesktopIcon icon)
            {
                // 处理双击事件 - 打开文件或程序
                if (e.ClickCount == 2)
                {
                    OpenFileOrProgram(icon.IconPath);
                    e.Handled = true;
                    return;
                }

                // 处理单击 - 准备拖拽
                dragStartPoint = e.GetPosition(this);
                draggedIcon = icon;
                e.Handled = true;
            }
        }

        private void IconsContainer_Drop(object sender, DragEventArgs e)
        {
            try
            {
                // 获取拖放的数据对象
                var data = e.Data;
                DesktopIcon newIcon = null;

                // 在拖放结束时获取鼠标坐标
                Point dropPoint = e.GetPosition((ItemsControl)sender);

                // 放置点坐标 - 以Panel为相对坐标系
                double x = dropPoint.X;
                double y = dropPoint.Y;

                // 检查是否是从其他来源拖过来的文件
                if (data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = data.GetData(DataFormats.FileDrop) as string[];

                    if (files != null && files.Length > 0)
                    {
                        string filePath = files[0]; // 取第一个文件

                        // 检查是否已经在这个分区中 - 如果已存在则静默返回，不提示
                        var viewModel = DataContext as PartitionViewModel;
                        if (viewModel.Icons.Any(i => i.IconPath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                        {
                            return; // 不做任何操作
                        }

                        // 创建新的图标对象
                        newIcon = new DesktopIcon
                        {
                            Id = Guid.NewGuid().ToString(),
                            Name = Path.GetFileNameWithoutExtension(filePath),
                            IconPath = filePath,
                            Position = new Point(x, y),
                            Size = new Size(64, 64)
                        };
                    }
                }

                // 如果成功创建了新图标，添加到当前分区
                if (newIcon != null)
                {
                    var viewModel = DataContext as PartitionViewModel;
                    viewModel?.AddIcon(newIcon);

                    // 保存分区数据
                    SavePartitionData();

                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理拖放时出错: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // 处理图标从一个分区被移动到另一个分区的情况
        //public void HandleIconMovedToOtherPartition(string iconId)
        //{
        //    try
        //    {
        //        var viewModel = DataContext as PartitionViewModel;
        //        if (viewModel != null)
        //        {
        //            // 查找需要移除的图标
        //            var iconToRemove = viewModel.Icons.FirstOrDefault(i => i.Id == iconId);
        //            if (iconToRemove != null)
        //            {
        //                // 从源分区移除图标
        //                viewModel.RemoveIcon(iconToRemove);

        //                // 保存数据
        //                SavePartitionData();

        //                System.Diagnostics.Debug.WriteLine($"图标已从源分区移除: {iconToRemove.Name}");
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        System.Diagnostics.Debug.WriteLine($"处理图标移动时出错: {ex.Message}");
        //    }
        //}

        private void OpenFileOrProgram(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    // 使用Windows默认处理方式打开文件
                    Win32.ShellExecute(IntPtr.Zero, "open", path, "", Path.GetDirectoryName(path), Win32.SW_SHOWNORMAL);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}