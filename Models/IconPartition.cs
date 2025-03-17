using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Layouter.Models
{
    public class IconPartition : ObservableObject
    {
        private string _name;
        private Rect _bounds;
        private bool _autoArrange = true;
        private Size _gridSize = new(64, 64);
        private double _padding = 10;
        private ObservableCollection<DesktopIcon> _icons = new();

        public string Id { get; } = Guid.NewGuid().ToString();

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public Rect Bounds
        {
            get => _bounds;
            set
            {
                if (SetProperty(ref _bounds, value) && AutoArrange)
                {
                    ArrangeIcons();
                }
            }
        }

        public ObservableCollection<DesktopIcon> Icons
        {
            get => _icons;
            private set => SetProperty(ref _icons, value);
        }

        public bool AutoArrange
        {
            get => _autoArrange;
            set
            {
                if (SetProperty(ref _autoArrange, value) && value)
                {
                    ArrangeIcons();
                }
            }
        }

        public Size GridSize
        {
            get => _gridSize;
            set => SetProperty(ref _gridSize, value);
        }

        public double Padding
        {
            get => _padding;
            set => SetProperty(ref _padding, value);
        }

        public IconPartition()
        {
            Icons.CollectionChanged += Icons_CollectionChanged;
        }

        private void Icons_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (AutoArrange)
            {
                ArrangeIcons();
            }
        }

        public bool CanFitIcon()
        {
            int cols = (int)(Bounds.Width / (GridSize.Width + Padding));
            int rows = (int)(Bounds.Height / (GridSize.Height + Padding));
            return Icons.Count < (cols * rows);
        }

        public bool ContainsPoint(Point point)
        {
            return Bounds.Contains(point);
        }

        public bool AddIcon(DesktopIcon icon)
        {
            if (CanFitIcon() && !Icons.Any(i => i.Id == icon.Id))
            {
                Icons.Add(icon);
                return true;
            }
            return false;
        }

        public bool RemoveIcon(DesktopIcon icon)
        {
            var iconToRemove = Icons.FirstOrDefault(i => i.Id == icon.Id);
            if (iconToRemove != null)
            {
                return Icons.Remove(iconToRemove);
            }
            return false;
        }

        public void ArrangeIcons()
        {
            if (Icons.Count == 0)
            {
                return;
            }
            int cols = Math.Max(1, (int)((Bounds.Width - Padding) / (GridSize.Width + Padding)));
            double startX = Bounds.Left + Padding;
            double startY = Bounds.Top + Padding;

            for (int i = 0; i < Icons.Count; i++)
            {
                int row = i / cols;
                int col = i % cols;
                double x = startX + col * (GridSize.Width + Padding);
                double y = startY + row * (GridSize.Height + Padding);
                Icons[i].Position = new Point(x, y);
            }
        }

        public DesktopIcon GetIconAt(Point point)
        {
            foreach (var icon in Icons)
            {
                var iconRect = new Rect(
                    icon.Position.X,
                    icon.Position.Y,
                    icon.Size.Width,
                    icon.Size.Height);

                if (iconRect.Contains(point))
                {
                    return icon;
                }
            }
            return null;
        }
    }
}
