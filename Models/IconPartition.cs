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
        private string name;
        private Rect bounds;
        private bool autoArrange = true;
        private Size gridSize = new(64, 64);
        private double padding = 10;
        private ObservableCollection<DesktopIcon> icons = new ObservableCollection<DesktopIcon>();

        public string Id { get; } = Guid.NewGuid().ToString();

        public string Name
        {
            get => name;
            set => SetProperty(ref name, value);
        }

        public Rect Bounds
        {
            get => bounds;
            set
            {
                if (SetProperty(ref bounds, value) && AutoArrange)
                {
                    ArrangeIcons();
                }
            }
        }

        public ObservableCollection<DesktopIcon> Icons
        {
            get => icons;
            private set => SetProperty(ref icons, value);
        }

        public bool AutoArrange
        {
            get => autoArrange;
            set
            {
                if (SetProperty(ref autoArrange, value) && value)
                {
                    ArrangeIcons();
                }
            }
        }

        public Size GridSize
        {
            get => gridSize;
            set => SetProperty(ref gridSize, value);
        }

        public double Padding
        {
            get => padding;
            set => SetProperty(ref padding, value);
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
