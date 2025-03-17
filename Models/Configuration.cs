using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Layouter.Models
{
    public class Configuration
    {
        public List<PartitionConfig> Partitions { get; set; } = new List<PartitionConfig>();
    }

    public class PartitionConfig
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public List<IconConfig> Icons { get; set; } = new List<IconConfig>();
    }

    public class IconConfig
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string IconPath { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }
}
