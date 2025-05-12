using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Layouter.Plugins
{
    public class PluginSettingsService
    {
        private static PluginSettingsService instance;
        public static PluginSettingsService Instance => instance ?? (instance = new PluginSettingsService());

        private readonly string settingsFolder;

        private PluginSettingsService()
        {
            // 创建插件设置目录
            settingsFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Layouter",
                "Plugins");

            if (!Directory.Exists(settingsFolder))
            {
                Directory.CreateDirectory(settingsFolder);
            }
        }

        /// <summary>
        /// 保存插件设置
        /// </summary>
        /// <typeparam name="T">设置类型</typeparam>
        /// <param name="pluginId">插件ID</param>
        /// <param name="settings">设置对象</param>
        /// <returns>是否保存成功</returns>
        public bool SaveSettings<T>(string pluginId, T settings)
        {
            try
            {
                string filePath = GetSettingsFilePath(pluginId);
                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(filePath, json);

                Log.Information($"已保存插件 {pluginId} 的设置");
                return true;
            }
            catch (Exception ex)
            {
                Log.Information($"保存插件 {pluginId} 设置时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 加载插件设置
        /// </summary>
        /// <typeparam name="T">设置类型</typeparam>
        /// <param name="pluginId">插件ID</param>
        /// <param name="defaultSettings">默认设置</param>
        /// <returns>设置对象</returns>
        public T LoadSettings<T>(string pluginId, T defaultSettings = default)
        {
            try
            {
                string filePath = GetSettingsFilePath(pluginId);

                if (!File.Exists(filePath))
                {
                    // 如果设置文件不存在，保存默认设置
                    if (defaultSettings != null)
                    {
                        SaveSettings(pluginId, defaultSettings);
                    }

                    return defaultSettings;
                }

                string json = File.ReadAllText(filePath);
                var settings = JsonConvert.DeserializeObject<T>(json);

                return settings != null ? settings : defaultSettings;
            }
            catch (Exception ex)
            {
                Log.Information($"加载插件 {pluginId} 设置时出错: {ex.Message}");
                return defaultSettings;
            }
        }

        /// <summary>
        /// 删除插件设置
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        /// <returns>是否删除成功</returns>
        public bool DeleteSettings(string pluginId)
        {
            try
            {
                string filePath = GetSettingsFilePath(pluginId);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Log.Information($"已删除插件 {pluginId} 的设置");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Log.Information($"删除插件 {pluginId} 设置时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取插件设置文件路径
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        /// <returns>设置文件路径</returns>
        private string GetSettingsFilePath(string pluginId)
        {
            // 使用插件ID作为文件名
            return Path.Combine(settingsFolder, $"{pluginId}.json");
        }
    }
}
