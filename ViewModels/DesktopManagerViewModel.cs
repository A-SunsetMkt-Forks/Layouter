using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentIcons.Common;
using Layouter.Models;

namespace Layouter.ViewModels
{
    public class DesktopManagerViewModel : ObservableObject
    {

        private ObservableCollection<DesktopIcon> icons = new ObservableCollection<DesktopIcon>();
        public ObservableCollection<DesktopIcon> Icons => icons;

        // 锁定状态变更事件
        public event EventHandler<bool> LockStateChanged;

        // 图标间距
        public const double IconSpacing = 10;

        private double IconAreaWidth = 64;
        private double IconAreaHeight = 64;

        private string name;
        private double opacity = 0.90;
        private string backgroundColor = "Transparent";
        private SolidColorBrush titleForeground = new SolidColorBrush(Colors.White);
        private SolidColorBrush titleBackground = new SolidColorBrush(Colors.Black);
        private FontFamily titleFont = new FontFamily("Microsoft YaHei");
        private double titleFontSize = 14.0;
        private HorizontalAlignment titleAlignment = HorizontalAlignment.Left;
        private IconSize iconSize = IconSize.Medium;
        private Size iconSizeValue = new Size(48, 48);
        private double iconTextSize = 12.0;
        private bool isLocked = false;

        private double partitionWidth = 400;
        private double partitionHeight = 300;


        private SolidColorBrush contentBackground = new SolidColorBrush(Colors.Transparent);

        public DesktopManagerViewModel()
        {
            ArrangeIconsCommand = new RelayCommand(ArrangeIcons);

            ToggleLockCommand = new RelayCommand(SwitchLockState);
        }

        public string windowId { get; set; }

        public byte TitleBaseAlpha { get; private set; } = 200;//透明度：200/256

        public string Name
        {
            get => name;
            set => SetProperty(ref name, value);
        }


        public double PartitionWidth
        {
            get => partitionWidth;
            set => SetProperty(ref partitionWidth, value);
        }

        public double PartitionHeight
        {
            get => partitionHeight;
            set => SetProperty(ref partitionHeight, value);
        }

        public SolidColorBrush TitleForeground
        {
            get => titleForeground;
            set => SetProperty(ref titleForeground, value);
        }

        // 标题栏背景色
        public SolidColorBrush TitleBackground
        {
            get => titleBackground;
            set
            {
                Color c = value.Color;
                if (value != null && c.A == 255)
                {
                    byte baseAlpha = TitleBaseAlpha;
                    c.A = baseAlpha;
                }

                SetProperty(ref titleBackground, new SolidColorBrush(c));
            }
        }

        // 标题栏字体
        public FontFamily TitleFont
        {
            get => titleFont;
            set => SetProperty(ref titleFont, value);
        }

        public double TitleFontSize
        {
            get => titleFontSize;
            set => SetProperty(ref titleFontSize, value);
        }

        // 标题栏文本显示位置
        public HorizontalAlignment TitleAlignment
        {
            get => titleAlignment;
            set => SetProperty(ref titleAlignment, value);
        }


        // 分区窗口透明度
        public double Opacity
        {
            get => opacity;
            set => SetProperty(ref opacity, value);
        }

        // 图标大小
        public IconSize IconSize
        {
            get => iconSize;
            set
            {
                if (SetProperty(ref iconSize, value))
                {
                    // 更新图标尺寸
                    UpdateIconSize();
                }
            }
        }

        /// <summary>
        /// 图标文本大小
        /// </summary>
        public double IconTextSize
        {
            get => iconTextSize;
            set => SetProperty(ref iconTextSize, value);
        }


        // 分区背景颜色
        public string BackgroundColor
        {
            get
            {
                return backgroundColor;
            }
            set
            {
                backgroundColor = value;
                OnPropertyChanged(nameof(BackgroundColor));
            }
        }

        public bool IsLocked
        {
            get => isLocked;
            set
            {
                if (SetProperty(ref isLocked, value))
                {
                    // 更新窗口可拖动和可调整大小状态
                    UpdateWindowDraggableState();
                    // 触发锁定状态变更事件
                    LockStateChanged?.Invoke(this, value);
                }
            }
        }

        public SolidColorBrush ContentBackground
        {
            get => contentBackground;
            set => SetProperty(ref contentBackground, value);
        }

        public IRelayCommand ArrangeIconsCommand { get; }

        public ICommand ToggleLockCommand { get; }


        public void ArrangeIcons()
        {
            double x = IconSpacing;
            double y = IconSpacing;
            double maxHeight = 0;

            foreach (var icon in Icons)
            {
                // 检查是否需要换行
                if (x + IconAreaWidth + IconSpacing > PartitionWidth && x > IconSpacing)
                {
                    x = IconSpacing;
                    y += Math.Max(maxHeight + IconSpacing, IconAreaHeight);
                    maxHeight = 0;
                }

                // 设置图标位置
                icon.Position = new Point(x, y);

                // 更新位置
                x += IconAreaWidth + IconSpacing;
                maxHeight = Math.Max(maxHeight, IconAreaHeight);
            }


        }

        public void SwitchLockState()
        {
            IsLocked = !IsLocked;
        }

        private void UpdateWindowDraggableState()
        {
            //Todo: 更新窗口可拖动和可调整大小状态
        }

        private void UpdateIconSize()
        {
            switch (IconSize)
            {
                case IconSize.Small:
                    iconSizeValue = new Size(32, 32);
                    IconAreaWidth = 44;
                    IconAreaHeight = 44;
                    break;
                case IconSize.Large:
                    iconSizeValue = new Size(64, 64);
                    IconAreaWidth = 84;
                    IconAreaHeight = 84;
                    break;
                case IconSize.Medium:
                default:
                    iconSizeValue = new Size(48, 48);
                    IconAreaWidth = 64;
                    IconAreaHeight = 64;
                    break;
            }

            // 更新所有图标的尺寸
            foreach (var icon in Icons)
            {
                icon.Size = iconSizeValue;
            }

            // 重新排列图标
            ArrangeIcons();
        }


        /// <summary>
        /// 获取分区窗口大小
        /// </summary>
        /// <returns>窗口大小</returns>
        public Size GetPartitionSize()
        {
            return new Size(partitionWidth, partitionHeight);
        }

        public void AddIcon(DesktopIcon icon)
        {
            if (!Icons.Any(i => i.Id == icon.Id))
            {
                Icons.Add(icon);
            }
        }

        public void RemoveIcon(DesktopIcon icon)
        {
            if (Icons.Contains(icon))
            {
                Icons.Remove(icon);
            }
        }

        public DesktopIcon GetIconById(string id)
        {
            foreach (var icon in Icons)
            {
                if (icon.Id == id)
                {
                    return icon;
                }
            }
            return null;
        }

        public Size GetIconSize()
        {
            var size = new Size(48, 48);
            switch (IconSize)
            {
                case IconSize.Small:
                    size = new Size(32, 32);
                    break;
                case IconSize.Large:
                    size = new Size(64, 64);
                    break;
                case IconSize.Medium:
                default:
                    size = new Size(48, 48);
                    break;
            }
            return size;
        }
    }

    public enum IconSize
    {
        Small,
        Medium,
        Large
    }

}
