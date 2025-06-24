using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Text.Json;
using Layouter.Utility;
using Path = System.IO.Path;
using PluginEntry;

namespace Layouter.Plugins.Views
{
    public partial class FloatingWindow : Window
    {
        private string pluginId;
        private static Dictionary<string, FloatingWindow> openWindows = new Dictionary<string, FloatingWindow>();
        private PluginStyle style;
        private bool isAnimationPlaying = true;
        private Storyboard gradientAnimation;
        private bool isAutoHideEnabled = true;
        private bool isHiding = false;
        private double edgeThreshold = 5; // 边缘检测阈值
        private double showWidth; // 显示时的宽度
        private double showHeight; // 显示时的高度
        private bool isCircleShape = true; // 是否为圆形
        private DispatcherTimer edgeCheckTimer;
        private DispatcherTimer autoHideTimer;

        public event EventHandler<SettingSavedEventArgs> OnSettingSaved;

        #region 构造函数和初始化

        public FloatingWindow()
        {
            InitializeComponent();
            showWidth = Width;
            showHeight = Height;

            // 初始化渐变动画
            gradientAnimation = (Storyboard)FindResource("GradientAnimation");
            gradientAnimation.Begin();

            // 初始化边缘检测定时器
            edgeCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            edgeCheckTimer.Tick += EdgeCheckTimer_Tick;
            edgeCheckTimer.Start();

            // 初始化自动隐藏定时器
            autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            autoHideTimer.Tick += AutoHideTimer_Tick;
        }

        public FloatingWindow(PluginDescriptor pd) : this()
        {
            pluginId = pd.Id;
            string key = pd.Key;

            // 从插件描述文件中获取Name值作为窗口标题
            try
            {
                string pluginsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Utility.Env.AppName, "Plugins");
                string pluginPath = Path.Combine(pluginsDirectory, $"{key}.plug");

                if (File.Exists(pluginPath))
                {
                    string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                    Directory.CreateDirectory(tempDir);

                    try
                    {
                        ZipFile.ExtractToDirectory(pluginPath, tempDir);
                        string pluginJsonPath = Path.Combine(tempDir, key, "plugin.json");

                        if (File.Exists(pluginJsonPath))
                        {
                            string json = File.ReadAllText(pluginJsonPath);
                            using (JsonDocument doc = JsonDocument.Parse(json))
                            {
                                if (doc.RootElement.TryGetProperty("Name", out JsonElement nameElement))
                                {
                                    this.Title = nameElement.GetString();
                                }

                                // 尝试加载图标
                                string iconPath = Path.Combine(tempDir, key, "icon.json");
                                if (File.Exists(iconPath))
                                {
                                    LoadPluginIcon(iconPath);
                                }
                            }
                        }
                    }
                    finally
                    {
                        if (Directory.Exists(tempDir))
                        {
                            Directory.Delete(tempDir, true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Create FloatingWindow Error: {ex.Message}");
            }
        }

        private void LoadPluginIcon(string iconPath)
        {
            try
            {
                string json = File.ReadAllText(iconPath);
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("MainIcon", out JsonElement iconElement))
                    {
                        string iconFile = iconElement.GetString();
                        string fullPath = Path.Combine(Path.GetDirectoryName(iconPath), iconFile);

                        if (File.Exists(fullPath))
                        {
                            BitmapImage bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                            bitmap.EndInit();

                            IconImage.Source = bitmap;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Load plugin icon error: {ex.Message}");
            }
        }

        #endregion

        #region 单例模式实现

        public static FloatingWindow GetInstance(PluginDescriptor pd)
        {
            if (openWindows.TryGetValue(pd.Id, out var window))
            {
                if (window.IsLoaded && window.Visibility == Visibility.Visible)
                {
                    window.Activate();
                    return window;
                }
                else
                {
                    openWindows.Remove(pd.Id);
                }
            }

            var newWindow = new FloatingWindow(pd);
            openWindows[pd.Id] = newWindow;
            return newWindow;
        }

        #endregion

        #region 属性

        public PluginStyle Style
        {
            get { return style; }
            set
            {
                style = value;
                ApplyStyle();
            }
        }

        public string Key { get; set; }

        public DispatcherTimer RefreshInfoTimer { get; set; }

        #endregion

        #region 样式和外观

        private void ApplyStyle()
        {
            if (style == null)
            {
                return;
            }
            // 应用窗口位置和大小
            Width = style.WindowPosition.Width;
            Height = style.WindowPosition.Height;
            Left = style.WindowPosition.Left;
            Top = style.WindowPosition.Top;
            Opacity = style.Opacity;

            // 保存显示时的尺寸
            showWidth = Width;
            showHeight = Height;

            // 应用渐变色
            if (GradientStop1 != null && GradientStop2 != null)
            {
                // 如果ItemColors包含至少两种颜色，使用它们作为渐变色
                if (style.ItemColors != null && style.ItemColors.Count >= 2)
                {
                    GradientStop1.Color = style.ItemColors[0];
                    GradientStop2.Color = style.ItemColors[1];
                }
                else
                {
                    // 否则使用默认颜色
                    GradientStop1.Color = Color.FromRgb(63, 81, 181); // #3F51B5
                    GradientStop2.Color = Color.FromRgb(0, 188, 212);  // #00BCD4
                }
            }

            // 设置形状
            isCircleShape = style.ShapeType == ShapeType.Round;
            MainBorder.CornerRadius = isCircleShape ? new CornerRadius(Width / 2) : new CornerRadius(8);
        }

        #endregion

        #region 事件处理

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            // 停止动画
            if (isAnimationPlaying)
            {
                gradientAnimation.Pause();
            }

            // 如果窗口正在隐藏状态，显示它
            if (isHiding)
            {
                ShowWindow();
            }

            // 显示悬停菜单
            IconImage.Visibility = Visibility.Collapsed;
            HoverMenu.Visibility = Visibility.Visible;
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            // 恢复动画
            if (isAnimationPlaying)
            {
                gradientAnimation.Resume();
            }

            // 隐藏悬停菜单
            IconImage.Visibility = Visibility.Visible;
            HoverMenu.Visibility = Visibility.Collapsed;

            // 如果启用了自动隐藏，启动定时器
            if (isAutoHideEnabled && IsNearScreenEdge())
            {
                autoHideTimer.Start();
            }
        }

        private void MainBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 拖动窗口
            DragMove();
        }

        private void MainBorder_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 显示右键菜单
            ShowContextMenu();
        }

        private void EdgeCheckTimer_Tick(object sender, EventArgs e)
        {
            // 检查窗口是否靠近屏幕边缘
            if (isAutoHideEnabled && !isHiding && !IsMouseOver && IsNearScreenEdge())
            {
                HideWindow();
            }
        }

        private void AutoHideTimer_Tick(object sender, EventArgs e)
        {
            // 自动隐藏定时器触发
            autoHideTimer.Stop();
            if (isAutoHideEnabled && !IsMouseOver && IsNearScreenEdge())
            {
                HideWindow();
            }
        }

        #endregion

        #region 菜单和功能

        public void AddMenuItem(string name, string iconPath, Action action)
        {
            Button button = new Button
            {
                Width = 24,
                Height = 24,
                Margin = new Thickness(2),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                ToolTip = name
            };

            // 添加图标
            if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
            {
                try
                {
                    Image image = new Image
                    {
                        Source = new BitmapImage(new Uri(iconPath, UriKind.Absolute)),
                        Width = 16,
                        Height = 16
                    };
                    button.Content = image;
                }
                catch
                {
                    TextBlock text = new TextBlock
                    {
                        Text = name.Length > 0 ? name[0].ToString() : "?",
                        Foreground = Brushes.White,
                        FontSize = 12,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    button.Content = text;
                }
            }
            else
            {
                TextBlock text = new TextBlock
                {
                    Text = name.Length > 0 ? name[0].ToString() : "?",
                    Foreground = Brushes.White,
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                button.Content = text;
            }

            button.Click += (s, e) => action?.Invoke();
            HoverMenu.Children.Add(button);
        }

        public void ClearMenuItems()
        {
            HoverMenu.Children.Clear();
        }

        private void ShowContextMenu()
        {
            ContextMenu menu = new ContextMenu();

            // 切换形状选项
            MenuItem shapeItem = new MenuItem { Header = isCircleShape ? "切换为方形" : "切换为圆形" };
            shapeItem.Click += (s, e) => ToggleShape();
            menu.Items.Add(shapeItem);

            // 切换动画选项
            MenuItem animationItem = new MenuItem { Header = isAnimationPlaying ? "停止动画" : "开始动画" };
            animationItem.Click += (s, e) => ToggleAnimation();
            menu.Items.Add(animationItem);

            // 自动隐藏选项
            MenuItem autoHideItem = new MenuItem { Header = isAutoHideEnabled ? "禁用自动隐藏" : "启用自动隐藏" };
            autoHideItem.Click += (s, e) => ToggleAutoHide();
            menu.Items.Add(autoHideItem);

            // 设置选项
            MenuItem settingsItem = new MenuItem { Header = "设置" };
            settingsItem.Click += (s, e) => ShowSettings();
            menu.Items.Add(settingsItem);

            // 分隔符
            menu.Items.Add(new Separator());

            // 关闭选项
            MenuItem closeItem = new MenuItem { Header = "关闭" };
            closeItem.Click += (s, e) => Close();
            menu.Items.Add(closeItem);

            menu.IsOpen = true;
        }

        private void ToggleShape()
        {
            isCircleShape = !isCircleShape;

            if (isCircleShape)
            {
                // 切换为圆形
                double size = Math.Max(Width, Height);
                Width = size;
                Height = size;
                MainBorder.CornerRadius = new CornerRadius(size / 2);
            }
            else
            {
                // 切换为方形
                MainBorder.CornerRadius = new CornerRadius(8);
                // 可以调整宽高比例
                if (Width == Height)
                {
                    Width = Width * 1.5;
                }
            }

            // 更新显示尺寸
            showWidth = Width;
            showHeight = Height;

            // 保存设置
            SaveSettings();
        }

        private void ToggleAnimation()
        {
            isAnimationPlaying = !isAnimationPlaying;

            if (isAnimationPlaying)
            {
                gradientAnimation.Begin();
            }
            else
            {
                gradientAnimation.Stop();
            }

            // 保存设置
            SaveSettings();
        }

        private void ToggleAutoHide()
        {
            isAutoHideEnabled = !isAutoHideEnabled;

            if (!isAutoHideEnabled && isHiding)
            {
                ShowWindow();
            }

            // 保存设置
            SaveSettings();
        }

        private void ShowSettings()
        {
            // 创建设置窗口
            Window settingsWindow = new Window
            {
                Title = "悬浮窗设置",
                Width = 300,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize
            };

            // 创建设置面板
            StackPanel panel = new StackPanel { Margin = new Thickness(10) };

            // 添加设置项
            AddColorPicker(panel, "渐变色1", GradientStop1.Color, color =>
            {
                GradientStop1.Color = color;
                SaveSettings();
            });

            AddColorPicker(panel, "渐变色2", GradientStop2.Color, color =>
            {
                GradientStop2.Color = color;
                SaveSettings();
            });

            AddSlider(panel, "透明度", Opacity, 0.1, 1.0, value =>
            {
                Opacity = value;
                SaveSettings();
            });

            AddCheckBox(panel, "启用动画", isAnimationPlaying, value =>
            {
                isAnimationPlaying = value;
                if (isAnimationPlaying)
                    gradientAnimation.Begin();
                else
                    gradientAnimation.Stop();
                SaveSettings();
            });

            AddCheckBox(panel, "启用自动隐藏", isAutoHideEnabled, value =>
            {
                isAutoHideEnabled = value;
                SaveSettings();
            });

            // 添加确定按钮
            Button okButton = new Button
            {
                Content = "确定",
                Margin = new Thickness(0, 20, 0, 0),
                Padding = new Thickness(20, 5, 20, 5),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            okButton.Click += (s, e) => settingsWindow.Close();
            panel.Children.Add(okButton);

            settingsWindow.Content = panel;
            settingsWindow.ShowDialog();
        }

        private void AddColorPicker(StackPanel panel, string label, Color initialColor, Action<Color> onColorChanged)
        {
            // 简单的颜色选择器实现
            StackPanel itemPanel = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };
            itemPanel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 5) });

            StackPanel colorPanel = new StackPanel { Orientation = Orientation.Horizontal };

            // 预定义的颜色选项
            Color[] colors = new Color[] {
                Colors.Red, Colors.Green, Colors.Blue, Colors.Yellow, Colors.Purple,
                Colors.Orange, Colors.Pink, Colors.Cyan, Colors.Magenta, Colors.Lime
            };

            foreach (var color in colors)
            {
                Border colorBorder = new Border
                {
                    Width = 20,
                    Height = 20,
                    Margin = new Thickness(2),
                    Background = new SolidColorBrush(color),
                    BorderThickness = new Thickness(1),
                    BorderBrush = Brushes.Black
                };

                colorBorder.MouseLeftButtonDown += (s, e) =>
                {
                    onColorChanged(color);
                };

                colorPanel.Children.Add(colorBorder);
            }

            itemPanel.Children.Add(colorPanel);
            panel.Children.Add(itemPanel);
        }

        private void AddSlider(StackPanel panel, string label, double initialValue, double min, double max, Action<double> onValueChanged)
        {
            StackPanel itemPanel = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };
            itemPanel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 5) });

            Slider slider = new Slider
            {
                Minimum = min,
                Maximum = max,
                Value = initialValue,
                IsSnapToTickEnabled = true,
                TickFrequency = 0.1
            };

            TextBlock valueText = new TextBlock { Text = initialValue.ToString("F1"), Margin = new Thickness(5, 0, 0, 0) };

            slider.ValueChanged += (s, e) =>
            {
                double value = Math.Round(e.NewValue, 1);
                valueText.Text = value.ToString("F1");
                onValueChanged(value);
            };

            StackPanel sliderPanel = new StackPanel { Orientation = Orientation.Horizontal };
            sliderPanel.Children.Add(slider);
            sliderPanel.Children.Add(valueText);

            itemPanel.Children.Add(sliderPanel);
            panel.Children.Add(itemPanel);
        }

        private void AddCheckBox(StackPanel panel, string label, bool initialValue, Action<bool> onValueChanged)
        {
            CheckBox checkBox = new CheckBox
            {
                Content = label,
                IsChecked = initialValue,
                Margin = new Thickness(0, 5, 0, 5)
            };

            checkBox.Checked += (s, e) => onValueChanged(true);
            checkBox.Unchecked += (s, e) => onValueChanged(false);

            panel.Children.Add(checkBox);
        }

        #endregion

        #region 边缘隐藏功能

        private bool IsNearScreenEdge()
        {
            // 检查窗口是否靠近屏幕边缘
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            return (Left <= edgeThreshold) || // 左边缘
                   (Top <= edgeThreshold) || // 上边缘
                   (Left + Width >= screenWidth - edgeThreshold) || // 右边缘
                   (Top + Height >= screenHeight - edgeThreshold); // 下边缘
        }

        private void HideWindow()
        {
            if (isHiding) return;

            isHiding = true;
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            // 确定窗口靠近哪个边缘
            if (Left <= edgeThreshold) // 左边缘
            {
                Width = 10;
                Left = 0;
            }
            else if (Top <= edgeThreshold) // 上边缘
            {
                Height = 10;
                Top = 0;
            }
            else if (Left + Width >= screenWidth - edgeThreshold) // 右边缘
            {
                Width = 10;
                Left = screenWidth - Width;
            }
            else if (Top + Height >= screenHeight - edgeThreshold) // 下边缘
            {
                Height = 10;
                Top = screenHeight - Height;
            }

            // 降低透明度
            Opacity = 0.5;
        }

        private void ShowWindow()
        {
            if (!isHiding) return;

            isHiding = false;
            Width = showWidth;
            Height = showHeight;
            Opacity = style?.Opacity ?? 0.7;

            // 确保窗口完全可见
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            if (Left + Width > screenWidth)
            {
                Left = screenWidth - Width;
            }

            if (Top + Height > screenHeight)
            {
                Top = screenHeight - Height;
            }
        }

        #endregion

        #region 设置保存

        private void SaveSettings()
        {
            if (style == null) style = new PluginStyle();

            // 更新样式对象
            style.WindowPosition = new Models.WindowPositionDto(Left, Top, Width, Height);
            style.Opacity = Opacity;

            // 更新颜色列表
            if (style.ItemColors == null) style.ItemColors = new List<Color>();
            if (style.ItemColors.Count < 2)
            {
                style.ItemColors.Clear();
                style.ItemColors.Add(GradientStop1.Color);
                style.ItemColors.Add(GradientStop2.Color);
            }
            else
            {
                style.ItemColors[0] = GradientStop1.Color;
                style.ItemColors[1] = GradientStop2.Color;
            }

            // 触发设置保存事件
            OnSettingSaved?.Invoke(this, new SettingSavedEventArgs(style));
        }

        #endregion
    }

}