using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Layouter.ViewModels;

namespace Layouter.Models
{

    public class PartitionSettings
    {
        public Color TitleForeground { get; set; }
        public Color TitleBackground { get; set; }
        public string TitleFont { get; set; }
        public double TitleFontSize { get; set; }
        public HorizontalAlignment TitleAlignment { get; set; }
        public double Opacity { get; set; }
        public IconSize IconSize { get; set; }
        public double IconTextSize { get; set; } = 12d;
        public bool IsLocked { get; set; } = false;
    }

    public class GlobalPartitionSettings : PartitionSettings
    {

    }

    public class PartitionVisibilityChangedEventArgs : EventArgs
    {
        public string PartitionId { get; }
        public bool IsVisible { get; }

        public PartitionVisibilityChangedEventArgs(string partitionId, bool isVisible)
        {
            PartitionId = partitionId;
            IsVisible = isVisible;
        }
    }
}
