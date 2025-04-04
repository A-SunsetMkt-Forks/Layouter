using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Layouter.Models;
using Layouter.Services;
using Layouter.Utility;
using Layouter.ViewModels;
using static Layouter.Utility.ShellUtil;
using static System.Windows.Win32;
using System.Text;

namespace Layouter.Views
{
    public partial class DesktopManagerWindow : Window
    {
        private DesktopManagerViewModel vm;
        private Point? dragStartPoint = null;
        private bool isDragging = false;
        private DesktopIcon draggedIcon = null;

        private DateTime lastClickTime = DateTime.MinValue;
        private DesktopIcon lastClickedIcon = null;
        private const double DoubleClickTimeThreshold = 500; //毫秒

        public DesktopManagerWindow()
        {
            InitializeComponent();

            // 创建ViewModel并设置为DataContext
            vm = new DesktopManagerViewModel
            {
                Name = $"分区 {DateTime.Now.ToString("HH:mm:ss")}"
            };

            DataContext = vm;

            // 挂载事件
            Loaded += DesktopManagerWindow_Loaded;
            PreviewDragOver += DesktopManagerWindow_PreviewDragOver;
            MouseRightButtonDown += DesktopManagerWindow_MouseRightButtonDown;
            Closing += Window_Closing;

            // 初始化拖拽状态
            isDragging = false;

            // 注册到窗口管理服务
            WindowManagerService.Instance.RegisterWindow(this);
        }

        public string WindowId { get; set; }

        #region 分区窗口事件

        private void DesktopManagerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            IntPtr hwnd = SysUtil.GetWindowHandle(this);

            //DesktopUtil.SetAsDesktopLevelWindow(hwnd);
            DesktopUtil.SetShellViewAsOwnerWindow(hwnd); //worked!

            DesktopUtil.SetAsToolWindow(hwnd);

            // 加载分区数据
            LoadPartitionData();
        }

        private void DesktopManagerWindow_PreviewDragOver(object sender, DragEventArgs e)
        {
            //匹配普通文件
            bool flag = e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent("DraggedIconSource");

            // 检查是否包含Shell项目
            if (!flag)
            {
                var formats = e.Data.GetFormats();
                flag = formats.Contains("Shell IDList Array") ||
                              formats.Contains("CF_HDROP") ||
                              formats.Contains("FileGroupDescriptor");
            }

            // 支持文件拖拽
            if (flag)
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        private void DesktopManagerWindow_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 检查点击位置是否在图标上
            var clickedElement = e.OriginalSource as FrameworkElement;
            while (clickedElement != null && !(clickedElement.Tag is DesktopIcon))
            {
                clickedElement = clickedElement.Parent as FrameworkElement;
            }

            if (clickedElement != null && clickedElement.Tag is DesktopIcon icon)
            {
                var iconCtxMenu = this.TryFindResource("IconContextMenu") as ContextMenu;

                if (iconCtxMenu != null)
                {
                    iconCtxMenu.Tag = icon; //将图标传递给右键菜单项
                    iconCtxMenu.IsOpen = true;
                }
            }
            else
            {
                // 点击空白区域，不弹出图标右键菜单
                var contextMenu = this.TryFindResource("PartitionContextMenu") as ContextMenu;
                if (contextMenu != null)
                {
                    contextMenu.IsOpen = true;
                }
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // 保存分区数据
            SavePartitionData();

            // 只隐藏窗口，不关闭应用程序
            e.Cancel = true;
            this.Hide();
        }

        #endregion

        #region 分区数据管理

        private void SavePartitionData()
        {
            try
            {
                // 使用现有的服务保存分区数据
                PartitionDataService.Instance.SavePartitionData(this);
            }
            catch (Exception ex)
            {
                Log.Information($"保存分区数据时出错: {ex.Message}");
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
                Log.Information($"加载分区数据时出错: {ex.Message}");
            }
        }

        #endregion

        #region 标题栏事件

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

                Log.Information("已创建新分区窗口");
            }
            catch (Exception ex)
            {
                Log.Information($"创建新分区窗口时出错: {ex.Message}");
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
                WindowManagerService.Instance.RemovePartitionWindow(this);
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
            var viewModel = DataContext as DesktopManagerViewModel;
            viewModel?.ArrangeIcons();
        }

        private void AlignPartitions_Click(object sender, RoutedEventArgs e)
        {
            // 关闭设置菜单
            SettingsPopup.IsOpen = false;

            WindowManagerService.Instance.ArrangeWindows();
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

        /// <summary>
        /// 在窗口初始化完成后自动开始编辑名称
        /// </summary>
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
                Log.Information($"显示标题编辑器时出错: {ex.Message}");
            }
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
                    var viewModel = DataContext as DesktopManagerViewModel;
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
                Log.Information($"保存标题编辑时出错: {ex.Message}");
            }
        }

        #endregion

        #region 图标区域事件

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
                        Win32.POINT startPt;
                        Win32.GetCursorPos(out startPt);
                        IntPtr startWindowHandle = Win32.WindowFromPoint(startPt);

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
                        Win32.POINT endPt;
                        Win32.GetCursorPos(out endPt);
                        IntPtr endWindowHandle = Win32.WindowFromPoint(endPt);

                        // 获取当前窗口句柄和区域
                        IntPtr currentWindowHandle = new WindowInteropHelper(this).Handle;
                        Win32.RECT currentWindowRect;
                        Win32.GetWindowRect(currentWindowHandle, out currentWindowRect);

                        // 判断是否拖放到了当前窗口外部
                        bool isOutsideCurrentWindow =
                            endPt.X < currentWindowRect.Left || endPt.X > currentWindowRect.Right ||
                            endPt.Y < currentWindowRect.Top || endPt.Y > currentWindowRect.Bottom;

                        // 检查是否拖放到了别的分区窗口
                        bool isOnOtherPartition = false;
                        DesktopManagerWindow targetWindow = null;
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
                                targetWindow = otherWindow;
                                break;
                            }
                        }

                        if (isOutsideCurrentWindow)
                        {
                            //图标移动到了其他分区窗口
                            if (isOnOtherPartition && targetWindow != null)
                            {
                                Log.Information("图标可被移动到其他窗口");

                                // 在目标分区窗口执行一次图标对齐操作
                                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    var targetViewModel = targetWindow.DataContext as DesktopManagerViewModel;
                                    targetViewModel?.ArrangeIcons();
                                    Log.Information("已在目标分区执行图标对齐");
                                }));
                            }
                            // 不在任何分区窗口,则认为是拖放到桌面
                            else
                            {
                                Log.Information("图标可能被拖到桌面或其他应用");

                                // 恢复桌面图标 - 从隐藏文件夹中复制回桌面
                                var desktopIconService = new DesktopIconService();
                                bool restored = desktopIconService.ShowDesktopIcon(originalFilePath);

                                if (!restored)
                                {
                                    Log.Information($"警告：无法恢复桌面上的图标：{originalFilePath}");
                                }
                            }

                            // 从分区中移除图标
                            var viewModel = this.DataContext as DesktopManagerViewModel;
                            if (viewModel != null)
                            {
                                viewModel.RemoveIcon(originalIcon);
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
                        Log.Information($"拖放操作异常: {ex.Message}\n{ex.StackTrace}");

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

                // 判断是否是从另一个分区拖过来的图标(true:是,false:不是)
                bool flag = data.GetDataPresent("DraggedIconSource") && (data.GetData("DraggedIconSource").ToString() == "Partition");

                // 检查是否是从其他来源拖过来的文件
                if (data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = data.GetData(DataFormats.FileDrop) as string[];

                    if (files != null && files.Length > 0)
                    {
                        string filePath = files[0]; // 取第一个文件

                        // 检查是否已经在这个分区中 - 如果已存在则静默返回，不提示
                        var viewModel = DataContext as DesktopManagerViewModel;
                        if (viewModel.Icons.Any(i => i.IconPath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                        {
                            return;
                        }

                        // 创建新的图标对象
                        newIcon = new DesktopIcon
                        {
                            Id = Guid.NewGuid().ToString(),
                            Name = Path.GetFileNameWithoutExtension(filePath),
                            IconPath = DesktopIconService.Instance.CombineHiddenPathWithIconPath(filePath),
                            Position = new Point(x, y),
                            Size = new Size(64, 64)
                        };

                        if (flag)
                        {
                            //保持图标路径不变
                            newIcon.IconPath = filePath;
                        }
                    }
                }
                else
                {
                    var formats = e.Data.GetFormats();
                    if (formats.Contains("Shell IDList Array") || formats.Contains("CF_HDROP") || formats.Contains("FileGroupDescriptor"))
                    {
                        // 使用COM互操作获取Shell项目
                        var shellItems = ShellUtil.GetShellItemsFromDataObject(e.Data);

                        if (shellItems != null && shellItems.Count > 0)
                        {
                            newIcon = CreateIconFromShellItem(shellItems[0], dropPoint);
                        }
                    }
                }

                // 如果成功创建了新图标，添加到当前分区
                if (newIcon != null)
                {
                    var viewModel = DataContext as DesktopManagerViewModel;
                    viewModel?.AddIcon(newIcon);

                    // 保存分区数据
                    SavePartitionData();

                    // 如果是从桌面拖过来的文件，则隐藏桌面上的原图标
                    if (!flag && data.GetDataPresent(DataFormats.FileDrop))
                    {
                        var files = data.GetData(DataFormats.FileDrop) as string[];
                        if (files != null && files.Length > 0)
                        {
                            string filePath = files[0];

                            //确保图标路径为桌面图标路径
                            filePath = DesktopIconService.Instance.RemoveHiddenPathInIconPath(filePath);
                            // 隐藏桌面上的原图标
                            bool hidden = DesktopIconService.Instance.HideDesktopIcon(filePath);

                            if (!hidden)
                            {
                                Log.Information($"警告：无法隐藏桌面上的图标，但仍会将其添加到分区：{filePath}");
                            }
                            else
                            {
                                Log.Information($"成功隐藏桌面上的图标：{filePath}");
                            }
                        }
                    }

                    // 延迟执行排列，确保新图标已经加入集合
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        viewModel?.ArrangeIcons();
                    }));

                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                Log.Information($"处理拖放时出错: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private DesktopIcon CreateIconFromShellItem(ShellItemInfo item, Point point)
        {
            var viewModel = DataContext as DesktopManagerViewModel;
            if (viewModel.Icons.Any(i => i.IconPath.Equals(item.Path, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            // 创建新的图标对象
            var icon = new DesktopIcon
            {
                Id = Guid.NewGuid().ToString(),
                Name = item.DisplayName,
                IconPath = item.Path,
                Position = point,
                Size = new Size(64, 64),
                IconType = IconType.Shell
            };

            return icon;
        }

        #endregion

        #region 分区图标事件

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

        private void Icon_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isDragging)
            {
                // 如果正在拖动，则不处理双击
                return;
            }

            var element = sender as FrameworkElement;
            if (element?.Tag is DesktopIcon icon)
            {
                DateTime now = DateTime.Now;

                // 判断是否是双击（同一个图标在短时间内点击两次）
                if (lastClickedIcon == icon && (now - lastClickTime).TotalMilliseconds < DoubleClickTimeThreshold)
                {
                    // 双击，打开文件
                    OpenFileOrProgram(icon.IconPath);
                    e.Handled = true;

                    // 重置状态，防止连续多次打开
                    lastClickTime = DateTime.MinValue;
                    lastClickedIcon = null;
                }
                else
                {
                    // 单击，更新状态
                    lastClickTime = now;
                    lastClickedIcon = icon;
                }
            }
        }

        private void MenuItem_Open(object sender, RoutedEventArgs e)
        {
            try
            {
                // 获取菜单项的Tag，即对应的DesktopIcon对象
                var menuItem = sender as MenuItem;
                var contextMenu = menuItem?.Parent as ContextMenu;

                //获取在DesktopManagerWindow_MouseRightButtonDown事件中设置的Tag
                if (contextMenu?.Tag is DesktopIcon icon)
                {
                    // 使用我们的辅助方法打开文件
                    OpenFileOrProgram(icon.IconPath);
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
                var contextMenu = menuItem?.Parent as ContextMenu;

                //获取在DesktopManagerWindow_MouseRightButtonDown事件中设置的Tag
                if (contextMenu?.Tag is DesktopIcon icon)
                {
                    // 确认是否删除
                    MessageBoxResult result = MessageBox.Show(
                        "确定要从分区中删除此图标吗？将会恢复桌面上对应的图标。",
                        "删除确认",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // 恢复桌面图标
                        var desktopIconService = new DesktopIconService();
                        bool restored = desktopIconService.ShowDesktopIcon(icon.IconPath);

                        if (!restored)
                        {
                            Log.Information($"警告：无法恢复桌面上的图标，但仍会从分区中移除：{icon.IconPath}");
                        }

                        // 从分区移除图标
                        var viewModel = DataContext as DesktopManagerViewModel;
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
                var contextMenu = menuItem?.Parent as ContextMenu;

                //获取在DesktopManagerWindow_MouseRightButtonDown事件中设置的Tag
                if (contextMenu?.Tag is DesktopIcon icon)
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

        #endregion

        /// <summary>
        /// 处理图标已被拖放到其他分区的通知
        /// </summary>
        //public void HandleIconMovedToOtherPartition(string iconId)
        //{
        //    try
        //    {
        //        var viewModel = this.DataContext as DesktopManagerViewModel;
        //        var icon = viewModel?.GetIconById(iconId);

        //        if (icon != null)
        //        {
        //            Log.Information($"收到通知：图标已被移动到其他分区，从当前分区移除: {icon.Name}");

        //            // 从当前分区中移除图标
        //            viewModel.RemoveIcon(icon);

        //            // 保存更改
        //            SavePartitionData();
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.Information($"处理图标移动通知时出错: {ex.Message}");
        //    }
        //}

        //private void AddItemToPartition(string path, string displayName = null, ImageSource iconSource = null)
        //{
        //    try
        //    {
        //        //// 检查是否已存在相同路径的项目
        //        //if (PartitionItems.Any(item => item.Path == path))
        //        //{
        //        //    return;
        //        //}

        //        //var partitionItem = new PartitionItemViewModel
        //        //{
        //        //    Path = path,
        //        //    Name = displayName ?? Path.GetFileNameWithoutExtension(path)
        //        //};

        //        //// 如果已提供图标，则使用它
        //        //if (iconSource != null)
        //        //{
        //        //    partitionItem.IconSource = iconSource;
        //        //}
        //        //else
        //        //{
        //        //    // 否则尝试加载图标
        //        //    try
        //        //    {
        //        //        if (File.Exists(path))
        //        //        {
        //        //            if (Path.GetExtension(path).ToLower() == ".lnk")
        //        //            {
        //        //                // 处理快捷方式
        //        //                var shortcutInfo = ShortcutService.ResolveShortcut(path);
        //        //                partitionItem.Path = shortcutInfo.TargetPath;
        //        //                partitionItem.Arguments = shortcutInfo.Arguments;
        //        //                partitionItem.WorkingDirectory = shortcutInfo.WorkingDirectory;

        //        //                // 使用无箭头图标
        //        //                partitionItem.IconSource = ShortcutService.GetIconWithoutShortcutOverlay(shortcutInfo.TargetPath).ToImageSource();
        //        //            }
        //        //            else
        //        //            {
        //        //                // 普通文件
        //        //                partitionItem.IconSource = IconHelper.GetFileIcon(path).ToImageSource();
        //        //            }
        //        //        }
        //        //        else if (Directory.Exists(path))
        //        //        {
        //        //            // 文件夹
        //        //            partitionItem.IconSource = IconHelper.GetFolderIcon(path).ToImageSource();
        //        //        }
        //        //        else if (path.StartsWith("::")) // 特殊Shell项目
        //        //        {
        //        //            // 尝试获取Shell项目图标
        //        //            partitionItem.IconSource = ShellItemHelper.GetShellItemIcon(path).ToImageSource();
        //        //        }
        //        //    }
        //        //    catch (Exception ex)
        //        //    {
        //        //        Log.Error($"加载图标时出错: {ex.Message}");
        //        //    }
        //        //}

        //        //PartitionItems.Add(partitionItem);
        //        //SavePartitionData();
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.Error($"添加项目到分区时出错: {ex.Message}");
        //    }
        //}


        //private void DesktopManagerWindow_Drop(object sender, DragEventArgs e)
        //{
        //    try
        //    {
        //        // 获取拖放的数据对象
        //        var data = e.Data;
        //        DesktopIcon newIcon = null;

        //        // 在拖放结束时获取鼠标坐标 - 相对于窗口
        //        Point dropPoint = e.GetPosition(this);

        //        // 放置点坐标 - 以Panel为相对坐标系
        //        double x = dropPoint.X;
        //        double y = dropPoint.Y;

        //        // 检查是否是从桌面拖过来的文件
        //        if (data.GetDataPresent(DataFormats.FileDrop))
        //        {
        //            var files = data.GetData(DataFormats.FileDrop) as string[];

        //            if (files != null && files.Length > 0)
        //            {
        //                string filePath = files[0]; // 取第一个文件

        //                // 检查是否已经在这个分区中
        //                var viewModel = DataContext as DesktopManagerViewModel;
        //                if (viewModel.Icons.Any(i => i.IconPath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
        //                {
        //                    return; // 不做任何操作，也不显示消息框
        //                }

        //                // 创建新的图标对象
        //                newIcon = new DesktopIcon
        //                {
        //                    Id = Guid.NewGuid().ToString(),
        //                    Name = Path.GetFileNameWithoutExtension(filePath),
        //                    IconPath = filePath,
        //                    Position = new Point(x, y),
        //                    Size = new Size(64, 64)
        //                };
        //            }
        //        }
        //        // 如果是从其他分区拖过来的
        //        else if (data.GetDataPresent("DraggedIconSource") && data.GetData("DraggedIconSource").ToString() == "Partition")
        //        {
        //            // 获取源分区窗口句柄
        //            IntPtr sourceWindowHandle = IntPtr.Zero;
        //            if (data.GetDataPresent("SourceWindowHandle"))
        //            {
        //                sourceWindowHandle = (IntPtr)data.GetData("SourceWindowHandle");
        //            }

        //            // 当前窗口句柄
        //            IntPtr currentWindowHandle = new WindowInteropHelper(this).Handle;

        //            // 如果是从其他分区窗口拖过来的
        //            if (sourceWindowHandle != IntPtr.Zero && sourceWindowHandle != currentWindowHandle)
        //            {
        //                string iconId = data.GetData("IconId").ToString();
        //                string iconName = data.GetData("IconName").ToString();
        //                string iconPath = data.GetData("IconPath").ToString();

        //                // 检查是否已经在这个分区中 - 如果已存在则静默返回，不提示
        //                var viewModel = DataContext as DesktopManagerViewModel;
        //                if (viewModel.Icons.Any(i => i.IconPath.Equals(iconPath, StringComparison.OrdinalIgnoreCase)))
        //                {
        //                    return; // 不做任何操作，也不显示消息框
        //                }

        //                // 创建新的图标，并设置其位置为拖放点
        //                newIcon = new DesktopIcon
        //                {
        //                    Id = iconId,  // 保持相同的ID
        //                    Name = iconName,
        //                    IconPath = iconPath,
        //                    Position = new Point(x, y),
        //                    Size = new Size(64, 64)
        //                };

        //                // 通知源窗口图标已被接收，可以从源分区移除
        //                foreach (var window in Application.Current.Windows)
        //                {
        //                    if (window is DesktopManagerWindow desktopManager)
        //                    {
        //                        IntPtr windowHandle = new WindowInteropHelper(desktopManager).Handle;
        //                        if (windowHandle == sourceWindowHandle)
        //                        {
        //                            // 找到源窗口，通知其移除图标
        //                            desktopManager.HandleIconMovedToOtherPartition(iconId);
        //                            break;
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //        else if (e.Data.GetDataPresent("Shell IDList Array"))
        //        {

        //            // 获取拖放的Shell项目
        //            var shellItems = ShellUtil.GetShellItemsFromDataObject(e.Data);
        //            foreach (var item in shellItems)
        //            {
        //                AddItemToPartition(item.Path, item.DisplayName, item.IconSource);
        //            }
        //        }

        //        // 如果成功创建了新图标，添加到当前分区
        //        if (newIcon != null)
        //        {
        //            var viewModel = DataContext as DesktopManagerViewModel;
        //            viewModel?.AddIcon(newIcon);

        //            // 保存分区数据
        //            SavePartitionData();

        //            // 如果是从另一个分区拖过来的图标，执行一次图标对齐
        //            if (data.GetDataPresent("DraggedIconSource") && data.GetData("DraggedIconSource").ToString() == "Partition")
        //            {
        //                // 延迟执行排列，确保新图标已经加入集合
        //                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        //                {
        //                    viewModel?.ArrangeIcons();
        //                    Log.Information("已在目标分区执行图标对齐");
        //                }));
        //            }

        //            e.Handled = true;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.Information($"处理拖放时出错: {ex.Message}\n{ex.StackTrace}");
        //    }
        //}


    }
}