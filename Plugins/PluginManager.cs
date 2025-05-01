using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Layouter.Plugins
{
    public class PluginManager
    {
        private readonly PluginLoader loader;
        private readonly Dictionary<string, LoadedPlugin> plugins = new Dictionary<string, LoadedPlugin>();
        private readonly string pluginsDirectory;

        public IReadOnlyDictionary<string, LoadedPlugin> Plugins => plugins;

        public PluginManager(string pluginsDirectory)
        {
            this.pluginsDirectory = pluginsDirectory;
        }

        public void LoadPlugins()
        {
           
        }

    }

    public class PluginLoadedEventArgs : EventArgs
    {
        public LoadedPlugin Plugin { get; }

        public PluginLoadedEventArgs(LoadedPlugin plugin)
        {
            Plugin = plugin;
        }
    }

}
