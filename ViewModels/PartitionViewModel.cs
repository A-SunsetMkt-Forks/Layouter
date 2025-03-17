using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Layouter.Models;

namespace Layouter.ViewModels
{
    public class PartitionViewModel : ObservableObject
    {
        private string _name;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private ObservableCollection<DesktopIcon> _icons = new ObservableCollection<DesktopIcon>();
        public ObservableCollection<DesktopIcon> Icons => _icons;

        private const double IconWidth = 64;
        private const double IconHeight = 64;
        private const double IconSpacing = 10;

        // 分区背景颜色
        private string _backgroundColor;
        public string BackgroundColor
        {
            get { return _backgroundColor; }
            set
            {
                _backgroundColor = value;
                OnPropertyChanged(nameof(BackgroundColor));
            }
        }

        // 命令
        public IRelayCommand ArrangeIconsCommand { get; }

        public PartitionViewModel()
        {
            ArrangeIconsCommand = new RelayCommand(ArrangeIcons);
        }

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
}
