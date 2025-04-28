using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Layouter.Plugins.Models
{
    public class IconDescriptor
    {
        private Dictionary<string, string> _iconPaths = new Dictionary<string, string>();

        public IReadOnlyDictionary<string, string> IconPaths => _iconPaths;

        public static IconDescriptor FromJson(string json)
        {
            var descriptor = new IconDescriptor();
            descriptor._iconPaths = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return descriptor;
        }
    }

}
