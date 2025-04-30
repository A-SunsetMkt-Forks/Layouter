using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Layouter.Plugins.Models
{
    public class ParameterInfo
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public Type ParameterType { get; set; }
        public object DefaultValue { get; set; }
        public bool IsRequired { get; set; } = true;
    }
}
