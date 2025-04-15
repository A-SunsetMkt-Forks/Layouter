using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Layouter.Models;
using Layouter.ViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Layouter.Services
{
    public class PartitionSettingsService
    {
        private static PartitionSettingsService instance;
        public static PartitionSettingsService Instance => instance ?? (instance = new PartitionSettingsService());

        private string styleFilePath;
        private string globalStyleFilePath;

        private PartitionSettingsService()
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Layouter");

            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }

            styleFilePath = Path.Combine(appDataPath, "partitionStyle.json");
            globalStyleFilePath = Path.Combine(appDataPath, "partitionStyle_global.json");
        }

        public void SaveGlobalSettings(DesktopManagerViewModel viewModel)
        {
            try
            {
                var settings = new GlobalPartitionSettings
                {
                    TitleForeground = viewModel.TitleForeground.Color,
                    TitleBackground = viewModel.TitleBackground.Color,
                    TitleFont = viewModel.TitleFont.Source,
                    TitleFontSize = viewModel.TitleFontSize,
                    TitleAlignment = viewModel.TitleAlignment,
                    Opacity = viewModel.Opacity,
                    IconSize = viewModel.IconSize,
                    IconTextSize = viewModel.IconTextSize
                };

                string json = JsonConvert.SerializeObject(settings, Formatting.Indented,
                    new JsonConverter[] { new StringEnumConverter() });

                File.WriteAllText(globalStyleFilePath, json);
                Log.Information("已保存全局样式配置");
            }
            catch (Exception ex)
            {
                Log.Information($"保存全局样式配置时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存单个窗口的样式配置
        /// </summary>
        public void SaveWindowSettings(DesktopManagerViewModel viewModel)
        {

            if (viewModel == null || string.IsNullOrEmpty(viewModel.windowId))
            {
                return;
            }

            try
            {
                // 读取现有的所有窗口配置
                var allSettings = LoadAllWindowSettings();

                string id = viewModel.windowId;

                // 更新或添加当前窗口的配置
                allSettings[id] = new PartitionSettings
                {
                    TitleForeground = viewModel.TitleForeground.Color,
                    TitleBackground = viewModel.TitleBackground.Color,
                    TitleFont = viewModel.TitleFont.Source,
                    TitleFontSize = viewModel.TitleFontSize,
                    TitleAlignment = viewModel.TitleAlignment,
                    Opacity = viewModel.Opacity,
                    IconSize = viewModel.IconSize,
                    IconTextSize = viewModel.IconTextSize
                };

                // 保存所有窗口配置
                string json = JsonConvert.SerializeObject(allSettings, Formatting.Indented,
                    new JsonConverter[] { new StringEnumConverter() });

                File.WriteAllText(styleFilePath, json);
                Log.Information($"已保存窗口 {id} 的样式配置");
            }
            catch (Exception ex)
            {
                Log.Information($"保存窗口样式配置时出错: {ex.Message}");
            }
        }

        public GlobalPartitionSettings LoadGlobalSettings()
        {
            try
            {
                if (!File.Exists(globalStyleFilePath))
                {
                    // 如果全局配置文件不存在，创建默认配置
                    var defaultSettings = GetDefaultGlobalSettings();
                    SaveGlobalSettings(new DesktopManagerViewModel
                    {
                        TitleForeground = new SolidColorBrush(defaultSettings.TitleForeground),
                        TitleBackground = new SolidColorBrush(defaultSettings.TitleBackground),
                        TitleFont = new FontFamily(defaultSettings.TitleFont),
                        TitleFontSize = defaultSettings.TitleFontSize,
                        TitleAlignment = defaultSettings.TitleAlignment,
                        Opacity = defaultSettings.Opacity,
                        IconSize = defaultSettings.IconSize,
                        IconTextSize = defaultSettings.IconTextSize
                    });

                    return defaultSettings;
                }

                string json = File.ReadAllText(globalStyleFilePath);
                var settings = JsonConvert.DeserializeObject<GlobalPartitionSettings>(json);

                return settings ?? GetDefaultGlobalSettings();
            }
            catch (Exception ex)
            {
                Log.Information($"加载全局样式配置时出错: {ex.Message}");
                return GetDefaultGlobalSettings();
            }
        }

        /// <summary>
        /// 加载单个窗口的样式配置
        /// </summary>
        public PartitionSettings LoadWindowSettings(string windowId)
        {
            try
            {
                // 首先检查是否启用全局样式
                var globalSettings = LoadGlobalSettings();
                if (globalSettings.EnableGlobalStyle)
                {
                    // 如果启用全局样式，直接返回全局样式配置
                    return globalSettings;
                }

                // 否则尝试加载窗口特定的样式配置
                if (!File.Exists(styleFilePath))
                {
                    // 如果个性化配置文件不存在，返回全局配置
                    return globalSettings;
                }

                string json = File.ReadAllText(styleFilePath);
                var allSettings = JsonConvert.DeserializeObject<Dictionary<string, PartitionSettings>>(json);

                // 如果找到窗口特定的配置，返回它
                if (allSettings != null && allSettings.ContainsKey(windowId))
                {
                    return allSettings[windowId];
                }

                // 否则返回全局配置
                return globalSettings;
            }
            catch (Exception ex)
            {
                Log.Information($"加载窗口 {windowId} 样式配置时出错: {ex.Message}");
                return LoadGlobalSettings();
            }
        }

        /// <summary>
        /// 加载所有窗口的样式配置
        /// </summary>
        private Dictionary<string, PartitionSettings> LoadAllWindowSettings()
        {
            try
            {
                if (!File.Exists(styleFilePath))
                {
                    return new Dictionary<string, PartitionSettings>();
                }

                string json = File.ReadAllText(styleFilePath);
                var settings = JsonConvert.DeserializeObject<Dictionary<string, PartitionSettings>>(json);

                return settings ?? new Dictionary<string, PartitionSettings>();
            }
            catch (Exception ex)
            {
                Log.Information($"加载所有窗口样式配置时出错: {ex.Message}");
                return new Dictionary<string, PartitionSettings>();
            }
        }

        /// <summary>
        /// 应用样式配置到ViewModel
        /// </summary>
        public void ApplySettingsToViewModel(string windowId, DesktopManagerViewModel viewModel)
        {
            try
            {
                // 加载适用的样式配置
                var settings = LoadWindowSettings(windowId);

                // 应用样式配置到ViewModel
                viewModel.TitleForeground = new SolidColorBrush(settings.TitleForeground);
                viewModel.TitleBackground = new SolidColorBrush(settings.TitleBackground);
                viewModel.TitleFont = new FontFamily(settings.TitleFont);
                viewModel.TitleAlignment = settings.TitleAlignment;
                viewModel.TitleFontSize = settings.TitleFontSize;
                viewModel.Opacity = settings.Opacity;
                viewModel.IconSize = settings.IconSize;
                viewModel.IconTextSize = settings.IconTextSize;

                Log.Information($"已应用样式配置到窗口 {windowId}");
            }
            catch (Exception ex)
            {
                Log.Information($"应用样式配置到窗口 {windowId} 时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取默认的全局样式配置
        /// </summary>
        private GlobalPartitionSettings GetDefaultGlobalSettings()
        {
            return new GlobalPartitionSettings
            {
                EnableGlobalStyle = true,
                TitleForeground = Colors.White,
                TitleBackground = Colors.DodgerBlue,
                TitleFont = "Microsoft YaHei",
                TitleAlignment = HorizontalAlignment.Left,
                TitleFontSize = 14d,
                Opacity = 0.95,
                IconSize = IconSize.Medium,
                IconTextSize = 12d
            };
        }

        public void SaveSettings(DesktopManagerViewModel viewModel, bool isGlobal = false)
        {
            if (isGlobal)
            {
                SaveGlobalSettings(viewModel);
            }
            else
            {
                SaveWindowSettings(viewModel);
            }
        }

        ///// <summary>
        ///// 兼容旧版本的方法
        ///// </summary>
        //public void LoadSettings(DesktopManagerViewModel viewModel, bool isGlobal = false)
        //{
        //    try
        //    {
        //        if (isGlobal)
        //        {
        //            var settings = LoadGlobalSettings();

        //            viewModel.TitleForeground = new SolidColorBrush(settings.TitleForeground);
        //            viewModel.TitleBackground = new SolidColorBrush(settings.TitleBackground);
        //            viewModel.TitleFont = new FontFamily(settings.TitleFont);
        //            viewModel.TitleAlignment = settings.TitleAlignment;
        //            viewModel.TitleFontSize = settings.TitleFontSize;
        //            viewModel.Opacity = settings.Opacity;
        //            viewModel.IconSize = settings.IconSize;
        //            viewModel.IconTextSize = settings.IconTextSize;
        //        }
        //        else
        //        {
        //            // 由于没有windowId，这里只能加载全局配置
        //            var settings = LoadGlobalSettings();

        //            viewModel.TitleForeground = new SolidColorBrush(settings.TitleForeground);
        //            viewModel.TitleBackground = new SolidColorBrush(settings.TitleBackground);
        //            viewModel.TitleFont = new FontFamily(settings.TitleFont);
        //            viewModel.TitleAlignment = settings.TitleAlignment;
        //            viewModel.TitleFontSize = settings.TitleFontSize;
        //            viewModel.Opacity = settings.Opacity;
        //            viewModel.IconSize = settings.IconSize;
        //            viewModel.IconTextSize = settings.IconTextSize;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.Information($"加载分区设置时出错: {ex.Message}");
        //    }
        //}

        //// 兼容旧版本的方法
        //public PartitionSettings GetDefaultSettings()
        //{
        //    var defaultGlobalSettings = GetDefaultGlobalSettings();
        //    return new PartitionSettings
        //    {
        //        TitleForeground = defaultGlobalSettings.TitleForeground,
        //        TitleBackground = defaultGlobalSettings.TitleBackground,
        //        TitleFont = defaultGlobalSettings.TitleFont,
        //        TitleAlignment = defaultGlobalSettings.TitleAlignment,
        //        TitleFontSize = defaultGlobalSettings.TitleFontSize,
        //        Opacity = defaultGlobalSettings.Opacity,
        //        IconSize = defaultGlobalSettings.IconSize,
        //        IconTextSize = defaultGlobalSettings.IconTextSize
        //    };
        //}


    }

    public class GlobalPartitionSettings : PartitionSettings
    {
        public bool EnableGlobalStyle { get; set; } = true;
    }

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

    }

}