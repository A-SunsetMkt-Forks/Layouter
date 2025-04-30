using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Layouter.Plugins.Models
{
    public class PluginDescriptor
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string PluginClassName { get; set; }

        public string Version { get; set; }

        public string Description { get; set; }

        public string Author { get; set; }

        public int Style { get; set; }

        public bool IsEnabled { get; set; }

        public string CodeFilePath { get; set; }

        public string IconFilePath { get; set; }

        public static PluginDescriptor FromJson(string json)
        {
            return JsonSerializer.Deserialize<PluginDescriptor>(json);
        }

    }

}
