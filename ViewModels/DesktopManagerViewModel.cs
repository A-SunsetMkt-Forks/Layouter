using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Layouter.Models;
using System.Windows.Media;

namespace Layouter.ViewModels
{
    public partial class DesktopManagerViewModel : ObservableObject
    {
        private static readonly string[] DefaultIconPaths = new[]
        {
            "/Layouter;component/Resources/Images/folder.png",
            "/Layouter;component/Resources/Images/document.png",
            "/Layouter;component/Resources/Images/image.png",
            "/Layouter;component/Resources/Images/music.png",
            "/Layouter;component/Resources/Images/video.png"
        };

        private static readonly string[] DefaultNames = new[]
        {
            "文件夹", "文档", "图片", "音乐", "视频"
        };

        [ObservableProperty]
        private ObservableCollection<IconPartition> partitions = new();

        [ObservableProperty]
        private DesktopIcon draggingIcon;

        [ObservableProperty]
        private IconPartition sourcePartition;

        [ObservableProperty]
        private Point currentDragPosition;

        public DesktopManagerViewModel()
        {
            // 创建默认分区
            if (Partitions.Count == 0)
            {
                CreateDefaultPartition();
            }
        }

        private void CreateDefaultPartition()
        {
            var partition = new IconPartition
            {
                Name = "默认分区",
                Bounds = new Rect(10, 10, 400, 300),
                AutoArrange = true
            };

            Partitions.Add(partition);

            // 添加一些示例图标
            for (int i = 0; i < 5; i++)
            {
                var icon = new DesktopIcon
                {
                    Name = DefaultNames[i % DefaultNames.Length],
                    Size = new Size(48, 48),
                    IconPath = DefaultIconPaths[i % DefaultIconPaths.Length]
                };

                partition.AddIcon(icon);
            }
        }

        [RelayCommand]
        private void CreatePartition(Rect bounds)
        {
            string name = $"分区_{Partitions.Count + 1}";
            var partition = new IconPartition
            {
                Name = name,
                Bounds = bounds,
                AutoArrange = true
            };

            Partitions.Add(partition);
        }

        [RelayCommand]
        private void DeletePartition(string partitionId)
        {
            var partition = Partitions.FirstOrDefault(p => p.Id == partitionId);
            if (partition != null)
            {
                Partitions.Remove(partition);
            }
        }

        [RelayCommand]
        private void StartDrag(DesktopIcon icon)
        {
            var partition = Partitions.FirstOrDefault(p => p.Icons.Contains(icon));
            if (partition != null)
            {
                DraggingIcon = icon;
                SourcePartition = partition;
                partition.RemoveIcon(icon);
                icon.IsDragging = true;
            }
        }

        [RelayCommand]
        private void DragMove(Point position)
        {
            if (DraggingIcon != null)
            {
                CurrentDragPosition = position;
                DraggingIcon.Position = new Point(
                    position.X - DraggingIcon.Size.Width / 2,
                    position.Y - DraggingIcon.Size.Height / 2);
            }
        }

        [RelayCommand]
        private void EndDrag(Point position)
        {
            if (DraggingIcon == null)
            {
                return;
            }
            bool iconPlaced = false;

            // 查找目标分区
            var targetPartition = Partitions.FirstOrDefault(p =>
                p.ContainsPoint(position) && p.CanFitIcon());

            if (targetPartition != null)
            {
                // 将图标添加到新分区
                iconPlaced = targetPartition.AddIcon(DraggingIcon);
            }

            // 如果未能放置图标，则放回原分区
            if (!iconPlaced && SourcePartition != null)
            {
                SourcePartition.AddIcon(DraggingIcon);
            }

            DraggingIcon.IsDragging = false;
            DraggingIcon = null;
            SourcePartition = null;
        }

        [RelayCommand]
        private void AlignPartitions(double spacing = 10)
        {
            if (!Partitions.Any())
            {
                return;
            }
            var orderedPartitions = Partitions.OrderBy(p => p.Bounds.Top)
                                            .ThenBy(p => p.Bounds.Left)
                                            .ToList();

            double currentY = spacing;
            double maxHeight = 0;

            foreach (var partition in orderedPartitions)
            {
                var bounds = partition.Bounds;
                if (bounds.Left == spacing)
                {
                    currentY += maxHeight + spacing;
                    maxHeight = 0;
                }

                partition.Bounds = new Rect(
                    bounds.Left,
                    currentY,
                    bounds.Width,
                    bounds.Height
                );

                maxHeight = Math.Max(maxHeight, bounds.Height);
            }
        }
    }
}
