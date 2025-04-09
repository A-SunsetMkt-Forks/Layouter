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
        private static PartitionSettingsService _instance;
        public static PartitionSettingsService Instance => _instance ?? (_instance = new PartitionSettingsService());
        
        private string _settingsFilePath;
        private string _globalSettingsFilePath;
        
        private PartitionSettingsService()
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Layouter");
            
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
            
            _settingsFilePath = Path.Combine(appDataPath, "partition_settings.json");
            _globalSettingsFilePath = Path.Combine(appDataPath, "global_settings.json");
        }
        
        public void SaveSettings(DesktopManagerViewModel viewModel, bool isGlobal = false)
        {
            try
            {
                var settings = new PartitionSettings
                {
                    TitleForeground = viewModel.TitleForeground.Color,
                    TitleBackground = viewModel.TitleBackground.Color,
                    TitleFont = viewModel.TitleFont.Source,
                    TitleAlignment = viewModel.TitleAlignment,
                    ContentBackground = viewModel.ContentBackground.Color,
                    Opacity = viewModel.Opacity,
                    IconSize = viewModel.IconSize
                };
                
                string json = JsonConvert.SerializeObject(settings, Formatting.Indented, 
                    new JsonConverter[] { new StringEnumConverter() });
                
                File.WriteAllText(isGlobal ? _globalSettingsFilePath : _settingsFilePath, json);
            }
            catch (Exception ex)
            {
                Log.Information($"保存分区设置时出错: {ex.Message}");
            }
        }
        
        public void LoadSettings(DesktopManagerViewModel viewModel, bool isGlobal = false)
        {
            try
            {
                string filePath = isGlobal ? _globalSettingsFilePath : _settingsFilePath;
                
                if (!File.Exists(filePath))
                {
                    return;
                }
                
                string json = File.ReadAllText(filePath);
                var settings = JsonConvert.DeserializeObject<PartitionSettings>(json);
                
                if (settings != null)
                {
                    viewModel.TitleForeground = new SolidColorBrush(settings.TitleForeground);
                    viewModel.TitleBackground = new SolidColorBrush(settings.TitleBackground);
                    viewModel.TitleFont = new FontFamily(settings.TitleFont);
                    viewModel.TitleAlignment = settings.TitleAlignment;
                    viewModel.ContentBackground = new SolidColorBrush(settings.ContentBackground);
                    viewModel.Opacity = settings.Opacity;
                    viewModel.IconSize = settings.IconSize;
                }
            }
            catch (Exception ex)
            {
                Log.Information($"加载分区设置时出错: {ex.Message}");
            }
        }
        
        public PartitionSettings GetGlobalSettings()
        {
            try
            {
                if (!File.Exists(_globalSettingsFilePath))
                {
                    return GetDefaultSettings();
                }
                
                string json = File.ReadAllText(_globalSettingsFilePath);
                var settings = JsonConvert.DeserializeObject<PartitionSettings>(json);
                
                return settings ?? GetDefaultSettings();
            }
            catch (Exception ex)
            {
                Log.Information($"获取全局设置时出错: {ex.Message}");
                return GetDefaultSettings();
            }
        }
        
        private PartitionSettings GetDefaultSettings()
        {
            return new PartitionSettings
            {
                TitleForeground = Colors.White,
                TitleBackground = Colors.DodgerBlue,
                TitleFont = "Microsoft YaHei",
                TitleAlignment = HorizontalAlignment.Left,
                ContentBackground = Colors.WhiteSmoke,
                Opacity = 0.95,
                IconSize = IconSize.Medium
            };
        }
    }
    
    public class PartitionSettings
    {
        public Color TitleForeground { get; set; }
        public Color TitleBackground { get; set; }
        public string TitleFont { get; set; }
        public HorizontalAlignment TitleAlignment { get; set; }
        public Color ContentBackground { get; set; }
        public double Opacity { get; set; }
        public IconSize IconSize { get; set; }
    }
}