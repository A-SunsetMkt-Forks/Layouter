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
using System.Net.NetworkInformation;
using Window = System.Windows.Window;

namespace Layouter.Views
{
    public partial class DesktopManagerWindow : Window
    {
        private DesktopManagerViewModel vm;
        private Point? dragStartPoint = null;
        private bool isDragging = false;
        private DesktopIcon draggedIcon = null;
        private bool isMouseOver = false;
        private bool isResizing = false; // 是否正在调整大小
        private double originalOpacity = 1.0; //窗口默认透明度
        private bool isAdjustingSize = false; // 是否正在调整窗口大小

        private DateTime lastClickTime = DateTime.MinValue;
        private DesktopIcon lastClickedIcon = null;
        private const double DoubleClickTimeThreshold = 500; //毫秒

        private bool isContainerClapsed = false;//图标容器是否折叠
        private double windowHeight = 0;

        public DesktopManagerWindow()
        {
            InitializeComponent();
            WindowId = Guid.NewGuid().ToString();

            // 创建ViewModel并设置为DataContext
            vm = new DesktopManagerViewModel
            {
                Name = "新分区"
            };

            DataContext = vm;

            // 挂载事件
            Loaded += DesktopManagerWindow_Loaded;
            PreviewDragOver += DesktopManagerWindow_PreviewDragOver;
            MouseRightButtonDown += DesktopManagerWindow_MouseRightButtonDown;
            Closing += Window_Closing;

            // 窗口大小调整事件
            SizeChanged += Window_SizeChanged;
            // 窗口状态变化事件
            //StateChanged += Window_StateChanged;

            vm.LockStateChanged += (s, e) =>
            {
                UpdateLockState(vm.IsLocked);
            };

            // 初始化拖拽状态
            isDragging = false;

            // 注册到窗口管理服务
            WindowManagerService.Instance.RegisterWindow(this);

            windowHeight = this.Height;

            // 添加窗口消息钩子
            SourceInitialized += DesktopManagerWindow_SourceInitialized;
        }

        public DesktopManagerWindow(string windowId) : this()
        {
            WindowId = windowId;
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

            // 设置分区样式
            ApplyStyleSettings();

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
                #region 自定义右键菜单

                var iconCtxMenu = this.TryFindResource("IconContextMenu") as ContextMenu;

                if (iconCtxMenu != null)
                {
                    iconCtxMenu.Tag = icon; //将图标传递给右键菜单项
                    iconCtxMenu.IsOpen = true;
                }

                #endregion
            }
            else
            {
                // 点击空白区域，不弹出图标右键菜单
                var contextMenu = this.TryFindResource("PartitionContextMenu") as System.Windows.Controls.ContextMenu;
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

        private void DesktopManagerWindow_SourceInitialized(object sender, EventArgs e)
        {
            // 获取窗口句柄
            IntPtr hwnd = new WindowInteropHelper(this).Handle;

            // 添加钩子处理窗口消息
            HwndSource source = HwndSource.FromHwnd(hwnd);
            source.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            //判断当前窗口是否正常调整大小 (替代Window_SizeChanged判定)
            if (msg == (int)WindowsMessage.WM_ENTERSIZEMOVE)
            {
                isResizing = true;
                UpdateWindowOpacity();
            }
            else if (msg == (int)WindowsMessage.WM_EXITSIZEMOVE)
            {
                if (isResizing && Mouse.LeftButton != MouseButtonState.Pressed)
                {
                    isResizing = false;
                    UpdateWindowOpacity();
                }
            }

            return IntPtr.Zero;
        }



        #endregion

        #region 分区数据管理

        private void SavePartitionData()
        {
            try
            {
                // 使用现有的服务保存分区数据
                PartitionDataService.Instance.SavePartitionData(this);

                //保存分区样式
                PartitionSettingsService.Instance.SaveWindowSettings(vm);
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
                PartitionDataService.Instance.LoadPartitionData(this, this.WindowId);
            }
            catch (Exception ex)
            {
                Log.Information($"加载分区数据时出错: {ex.Message}");
            }
        }

        #endregion

        #region 窗口事件

        private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            isMouseOver = true;
            UpdateWindowOpacity();
        }

        /// <summary>
        /// 鼠标离开窗口事件
        /// </summary>
        private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            isMouseOver = false;
            UpdateWindowOpacity();
        }

        /// <summary>
        /// 窗口大小变化事件
        /// </summary>
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            vm.PartitionWidth = this.Width;
            vm.PartitionHeight = this.Height;

            // 调整窗口尺寸
            if (!isAdjustingSize)
            {
                //AdjustWindowSizeToIconGrid();
            }

            // 使用延迟重置调整状态
            Task.Delay(500).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {

                    (DataContext as DesktopManagerViewModel)?.ArrangeIcons();

                    UpdateWindowOpacity();

                    // 重置调整标志
                    isAdjustingSize = false;
                });
            });
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            UpdateWindowOpacity();
        }

        private void UpdateWindowOpacity()
        {
            if (vm != null)
            {
                originalOpacity = vm.Opacity;

                if (isMouseOver || IsActive || isResizing)
                {
                    vm.ContentBackground = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
                    this.Opacity = 1.0;
                }
                else
                {
                    vm.ContentBackground = new SolidColorBrush(Colors.Transparent);
                    this.Opacity = originalOpacity * 0.7;
                }
            }
        }

        /// <summary>
        /// 按图标大小的整倍数调整窗口尺寸
        /// </summary>
        //private void AdjustWindowSizeToIconGrid()
        //{
        //    if (vm == null)
        //    {
        //        return;
        //    }

        //    // 获取当前图标大小
        //    double iconSize = 0;

        //    switch (vm.IconSize)
        //    {
        //        case IconSize.Small:
        //            iconSize = 32;
        //            break;
        //        case IconSize.Medium:
        //            iconSize = 48;
        //            break;
        //        case IconSize.Large:
        //            iconSize = 64;
        //            break;
        //        default:
        //            iconSize = 48;
        //            break;
        //    }

        //    // 考虑图标间距
        //    var iconSpacing = DesktopManagerViewModel.IconSpacing;
        //    double gridSize = iconSize + iconSpacing;

        //    // 计算内容区域的宽高（减去标题栏高度）
        //    double contentWidth = this.Width;
        //    double contentHeight = this.Height - TitleBar.ActualHeight;

        //    // 计算整倍数的宽高
        //    int columnsCount = Math.Max(1, (int)Math.Ceiling(contentWidth / gridSize));
        //    int rowsCount = Math.Max(1, (int)Math.Ceiling(contentHeight / gridSize));

        //    // 调整窗口大小为整倍数
        //    double newWidth = columnsCount * gridSize + iconSpacing;
        //    double newHeight = rowsCount * gridSize + TitleBar.ActualHeight + iconSpacing;

        //    // 如果大小有变化，则调整窗口大小
        //    if (Math.Abs(this.Width - newWidth) > 1 || Math.Abs(this.Height - newHeight) > 1)
        //    {
        //        isAdjustingSize = true;

        //        this.Width = newWidth;
        //        this.Height = newHeight;
        //    }
        //}

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
                PartitionDataService.Instance.ShowWindow(window);

                Task.Delay(100).Wait();
                // 在新窗口显示后让标题变为可编辑状态
                window.EnableTitleEditOnFirstLoad();

                Log.Information("已创建新分区窗口");
            }
            catch (Exception ex)
            {
                Log.Information($"创建新分区窗口时出错: {ex.Message}");
            }
        }


        private void PartitionSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsPopup.IsOpen = false;

            try
            {
                var settingsWindow = new PartitionSettingsWindow(vm, false);
                settingsWindow.Owner = this;
                settingsWindow.ShowDialog();

                // 如果设置已保存，更新UI
                if (settingsWindow.DialogResult == true)
                {
                    // 保存分区数据
                    SavePartitionData();
                }
            }
            catch (Exception ex)
            {
                Log.Information($"打开分区设置窗口时出错: {ex.Message}");
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


        private void LockButton_Click(object sender, RoutedEventArgs e)
        {
            vm.IsLocked = !vm.IsLocked;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 支持拖动窗口
            if (e.ClickCount == 1 && Mouse.LeftButton == MouseButtonState.Pressed)
            {
                if (!vm.IsLocked)
                {
                    this.DragMove();
                }
            }
        }

        private void DoubleClickHandler(object sender, MouseButtonEventArgs e)
        {
            SwitchContainerClapsedState();
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

        private void SwitchContainerClapsedState()
        {
            if (isContainerClapsed)
            {
                //展开
                IconsContainer.Visibility = Visibility.Visible;
                this.Height = windowHeight;
                isContainerClapsed = false;
            }
            else
            {
                //折叠
                IconsContainer.Visibility = Visibility.Collapsed;
                this.Height = TitleBar.Height;
                isContainerClapsed = true;
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
                                bool restored = DesktopIconService.Instance.ShowDesktopIcon(originalFilePath);

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

                var viewModel = DataContext as DesktopManagerViewModel;
                var iconSize = viewModel?.GetIconSize() ?? new Size(48, 48);

                // 判断是否是从另一个分区拖过来的图标(true:是,false:不是)
                bool flag = data.GetDataPresent("DraggedIconSource") && (data.GetData("DraggedIconSource").ToString() == "Partition");

                // 检查是否是从其他来源拖过来的文件
                if (data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = data.GetData(DataFormats.FileDrop) as string[];

                    if (files != null && files.Length > 0)
                    {
                        // 创建一个图标列表，用于存储所有拖放的图标
                        List<DesktopIcon> newIcons = new List<DesktopIcon>();

                        // 处理所有拖放的文件
                        foreach (string filePath in files)
                        {
                            // 检查是否已经在这个分区中 - 如果已存在则跳过此文件
                            if (viewModel.Icons.Any(i => i.IconPath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                            {
                                continue;
                            }

                            // 为每个文件创建新的图标对象
                            DesktopIcon icon = new DesktopIcon
                            {
                                Id = Guid.NewGuid().ToString(),
                                Position = new Point(x, y),
                                Size = iconSize,
                                TextSize = viewModel.IconTextSize,
                                IconPath = DesktopIconService.Instance.CombineHiddenPathWithIconPath(filePath)
                            };

                            // 根据文件类型设置图标属性
                            if (filePath.Equals(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)))
                            {
                                string userProfile = ShellUtil.GetDisplayCurrentUserName();
                                icon.Name = userProfile;
                                icon.IconPath = ShellUtil.MapSpecialFolderByName(userProfile);
                                icon.IconType = IconType.Shell;
                            }
                            else if (ShortCutUtil.IsShortcutPath(filePath))
                            {
                                icon.Name = Path.GetFileNameWithoutExtension(filePath);
                                icon.IconType = IconType.Shortcut;
                            }
                            else if (Directory.Exists(filePath))
                            {
                                icon.Name = new DirectoryInfo(filePath).Name;
                                icon.IconType = IconType.Folder;
                            }
                            else if (File.Exists(filePath))
                            {
                                icon.Name = new FileInfo(filePath).Name;
                                icon.IconType = IconType.File;
                            }
                            else
                            {
                                icon.Name = ShellUtil.GetSpecialFolderDisplayName(filePath);
                                icon.IconType = IconType.Shell;
                            }

                            if (flag)
                            {
                                //保持图标路径不变
                                icon.IconPath = filePath;
                            }

                            // 将图标添加到列表中
                            newIcons.Add(icon);

                            // 稍微偏移下一个图标的位置，避免完全重叠
                            x += 20;
                            y += 20;
                        }

                        // 如果有图标被添加，则使用第一个图标作为newIcon（用于后续处理）
                        if (newIcons.Count > 0)
                        {
                            newIcon = newIcons[0];

                            // 将所有图标添加到视图模型中
                            foreach (var icon in newIcons)
                            {
                                viewModel?.AddIcon(icon);
                            }
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
                            // 创建一个图标列表，用于存储所有拖放的Shell项目
                            List<DesktopIcon> shellIcons = new List<DesktopIcon>();

                            // 处理所有拖放的Shell项目
                            foreach (var shellItem in shellItems)
                            {
                                // 为每个Shell项目创建图标
                                DesktopIcon icon = CreateIconFromShellItem(shellItem, dropPoint, iconSize);

                                if (icon != null)
                                {
                                    // 将图标添加到列表和视图模型中
                                    shellIcons.Add(icon);
                                    viewModel?.AddIcon(icon);

                                    // 稍微偏移下一个图标的位置，避免完全重叠
                                    dropPoint.X += 20;
                                    dropPoint.Y += 20;
                                }
                            }

                            // 如果有图标被添加，则使用第一个图标作为newIcon（用于后续处理）
                            if (shellIcons.Count > 0)
                            {
                                newIcon = shellIcons[0];
                            }
                        }
                    }
                }

                // 如果成功创建了新图标，保存分区数据
                if (newIcon != null)
                {
                    // 注意：图标已经在前面的代码中添加到视图模型中，这里不需要再次添加

                    // 保存分区数据
                    SavePartitionData();

                    // 如果是从桌面拖过来的文件，则隐藏桌面上的原图标
                    if (!flag && data.GetDataPresent(DataFormats.FileDrop))
                    {
                        var files = data.GetData(DataFormats.FileDrop) as string[];
                        if (files != null && files.Length > 0)
                        {
                            // 处理所有拖放的文件
                            foreach (string filePath in files)
                            {
                                // 跳过特殊Shell类型的图标
                                bool isShellType = viewModel.Icons.Any(i => i.IconPath.Equals(filePath, StringComparison.OrdinalIgnoreCase) && i.IconType == IconType.Shell);
                                if (isShellType)
                                {
                                    continue;
                                }

                                //确保图标路径为桌面图标路径
                                string desktopPath = DesktopIconService.Instance.RemoveHiddenPathInIconPath(filePath);
                                // 隐藏桌面上的原图标
                                bool hidden = DesktopIconService.Instance.HideDesktopIcon(desktopPath);

                                if (!hidden)
                                {
                                    Log.Information($"警告：无法隐藏桌面上的图标，但仍会将其添加到分区：{desktopPath}");
                                }
                                else
                                {
                                    Log.Information($"成功隐藏桌面上的图标：{desktopPath}");
                                }
                            }
                        }
                    }
                    // 处理Shell类型的图标
                    foreach (var icon in viewModel.Icons.Where(i => i.IconType == IconType.Shell))
                    {
                        var filePath = DesktopIconService.Instance.RemoveHiddenPathInIconPath(icon.IconPath);
                        // 隐藏桌面上的原图标
                        DesktopIconService.Instance.HideDesktopIcon(filePath);
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

        private DesktopIcon CreateIconFromShellItem(ShellItemInfo item, Point point, Size iconSize)
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
                Size = iconSize,
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
                var contextMenu = menuItem?.Parent as System.Windows.Controls.ContextMenu;

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
                var contextMenu = menuItem?.Parent as System.Windows.Controls.ContextMenu;

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
                var contextMenu = menuItem?.Parent as System.Windows.Controls.ContextMenu;

                //获取在DesktopManagerWindow_MouseRightButtonDown事件中设置的Tag
                if (contextMenu?.Tag is DesktopIcon icon)
                {

                    ShellUtil.ShowProperties(icon.IconPath);
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
                else if (!ShortCutUtil.IsShortcutPath(path))
                {
                    ShellUtil.OpenSpecialFolder(path);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region 分区窗口样式

        private void ApplyStyleSettings()
        {
            try
            {
                // 使用PartitionSettingsService应用样式配置
                PartitionSettingsService.Instance.ApplySettingsToViewModel(vm);

                // 更新图标大小
                UpdateIconSizes();

                // 更新锁定状态
                UpdateLockState(vm.IsLocked);
            }
            catch (Exception ex)
            {
                Log.Information($"应用样式配置时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新图标大小
        /// </summary>
        public void UpdateIconSizes()
        {
            try
            {
                Size iconSize;
                switch (vm.IconSize)
                {
                    case IconSize.Small:
                        iconSize = new Size(32, 32);
                        break;
                    case IconSize.Large:
                        iconSize = new Size(64, 64);
                        break;
                    case IconSize.Medium:
                    default:
                        iconSize = new Size(48, 48);
                        break;
                }

                // 更新所有图标的尺寸
                foreach (var icon in vm.Icons)
                {
                    icon.Size = iconSize;
                    icon.TextSize = vm.IconTextSize;
                }

                // 重新排列图标
                vm.ArrangeIcons();
            }
            catch (Exception ex)
            {
                Log.Information($"更新图标大小时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新锁定状态
        /// </summary>
        private void UpdateLockState(bool isLocked)
        {
            LockIcon.Symbol = isLocked ? FluentIcons.Common.Symbol.LockClosed : FluentIcons.Common.Symbol.LockOpen;
            LockButton.ToolTip = isLocked ? "已锁定" : "未锁定";

            // 更新窗口可拖动和可调整大小状态
            if (isLocked)
            {
                ResizeMode = ResizeMode.NoResize;
            }
            else
            {
                ResizeMode = ResizeMode.CanResizeWithGrip;
            }
        }

        #endregion

        #region 数据同步

        public void Sync(DesktopManagerWindow window)
        {
            if (window == null)
            {
                return;
            }

            vm = window.DataContext as DesktopManagerViewModel;
            if (vm != null)
            {
                vm.windowId = window.WindowId;
                DataContext = vm;
            }

            // 应用样式设置
            ApplyStyleSettings();
        }

        #endregion

    }
}