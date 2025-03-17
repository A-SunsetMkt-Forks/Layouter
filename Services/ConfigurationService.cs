using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using Layouter.Models;

namespace Layouter.Services
{
    public class ConfigurationService
    {
        private readonly string configFilePath;

        public ConfigurationService()
        {
            // 配置文件路径，保存在应用程序数据目录
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Layouter");
                
            // 确保目录存在
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
            
            configFilePath = Path.Combine(appDataPath, "config.json");
        }

        public Configuration LoadConfiguration()
        {
            try
            {
                if (File.Exists(configFilePath))
                {
                    string json = File.ReadAllText(configFilePath);
                    return JsonSerializer.Deserialize<Configuration>(json) ?? new Configuration();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载配置时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"加载配置时出错: {ex.Message}");
            }
            
            return new Configuration();
        }

        public void SaveConfiguration(Configuration config)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(configFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"保存配置时出错: {ex.Message}");
            }
        }

        public void SavePartition(PartitionConfig partition)
        {
            var config = LoadConfiguration();
            
            // 查找是否已存在该分区
            var existingPartition = config.Partitions.FirstOrDefault(p => p.Id == partition.Id);
            
            if (existingPartition != null)
            {
                // 更新现有分区
                int index = config.Partitions.IndexOf(existingPartition);
                config.Partitions[index] = partition;
            }
            else
            {
                // 添加新分区
                config.Partitions.Add(partition);
            }
            
            SaveConfiguration(config);
        }

        public void RemovePartition(string partitionId)
        {
            var config = LoadConfiguration();
            var partition = config.Partitions.FirstOrDefault(p => p.Id == partitionId);
            
            if (partition != null)
            {
                config.Partitions.Remove(partition);
                SaveConfiguration(config);
            }
        }
    }
}
