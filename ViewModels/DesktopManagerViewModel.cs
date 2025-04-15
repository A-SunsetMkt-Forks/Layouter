using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
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

        private const double IconWidth = 64;
        private const double IconHeight = 64;
        private const double IconSpacing = 10;

        private string name;
        private double opacity = 0.90;
        private string backgroundColor;
        private SolidColorBrush titleForeground = new SolidColorBrush(Colors.White);
        private SolidColorBrush titleBackground = new SolidColorBrush(Colors.DodgerBlue);
        private FontFamily titleFont = new FontFamily("Microsoft YaHei");
        private double titleFontSize = 14.0;
        private HorizontalAlignment titleAlignment = HorizontalAlignment.Left;
        private IconSize iconSize = IconSize.Medium;
        private double iconTextSize = 12.0;


        public DesktopManagerViewModel()
        {
            ArrangeIconsCommand = new RelayCommand(ArrangeIcons);
        }

        public string windowId { get; set; }

        public byte TitleBaseAlpha { get; private set; } = 64;//透明度：64/256

        public string Name
        {
            get => name;
            set => SetProperty(ref name, value);
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
                if (titleBackground != null)
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
            get { return backgroundColor; }
            set
            {
                backgroundColor = value;
                OnPropertyChanged(nameof(BackgroundColor));
            }
        }

        // 命令
        public IRelayCommand ArrangeIconsCommand { get; }


        public void ArrangeIcons()
        {
            double x = IconSpacing;
            double y = IconSpacing;
            double maxHeight = 0;

            foreach (var icon in Icons)
            {
                // 检查是否需要换行
                if (x + IconWidth + IconSpacing > GetPartitionWidth() && x > IconSpacing)
                {
                    x = IconSpacing;
                    y += maxHeight + IconSpacing;
                    maxHeight = 0;
                }

                // 设置图标位置
                icon.Position = new Point(x, y);

                // 更新位置
                x += IconWidth + IconSpacing;
                maxHeight = Math.Max(maxHeight, IconHeight);
            }
        }

        private void UpdateIconSize()
        {
            Size newSize;
            switch (IconSize)
            {
                case IconSize.Small:
                    newSize = new Size(32, 32);
                    break;
                case IconSize.Large:
                    newSize = new Size(64, 64);
                    break;
                case IconSize.Medium:
                default:
                    newSize = new Size(48, 48);
                    break;
            }

            // 更新所有图标的尺寸
            foreach (var icon in Icons)
            {
                icon.Size = newSize;
            }

            // 重新排列图标
            ArrangeIcons();
        }

        private double GetPartitionWidth()
        {
            // 假设窗口宽度为有效区域宽度
            return 400; // 这是默认值，实际使用时应该获取窗口的实际宽度
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
    }

    public enum IconSize
    {
        Small,
        Medium,
        Large
    }

}
