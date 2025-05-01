using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Layouter.Plugins.Models;

namespace Layouter.Plugins
{
    
    public class LoadedPlugin
    {
        public PluginDescriptor Descriptor { get; }

        public dynamic Plugin { get; }

        public IconDescriptor IconDescriptor { get; }

        public Dictionary<string, List<PluginParameter>> ParameterDescriptions { get; }

        public LoadedPlugin(PluginDescriptor descriptor, dynamic plugin, IconDescriptor iconDescriptor, Dictionary<string, List<PluginParameter>> parameterDescriptions = null)
        {
            Descriptor = descriptor;
            Plugin = plugin;
            IconDescriptor = iconDescriptor;
            ParameterDescriptions = parameterDescriptions ?? new Dictionary<string, List<PluginParameter>>();
        }
    }

}
