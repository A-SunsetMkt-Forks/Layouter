using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using Layouter.Models;
using Layouter.Utility;
using Layouter.ViewModels;
using Layouter.Views;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Layouter.Services
{
    public class PartitionDataService
    {
        private static readonly Lazy<PartitionDataService> instance = new Lazy<PartitionDataService>(() => new PartitionDataService());
        public static PartitionDataService Instance => instance.Value;

        private string dataDirectory;
        private Dictionary<DesktopManagerWindow, string> windowGuids = new Dictionary<DesktopManagerWindow, string>();

        // 存储哈希码到GUID的映射，用于恢复窗口配置
        private Dictionary<string, string> windowIdMapping = new Dictionary<string, string>();

        private PartitionDataService()
        {
            // 配置保存目录
            dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Env.AppName);

            // 确保目录存在
            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }
        }

        /// <summary>
        /// 获取窗口的唯一标识符
        /// </summary>
        /// <param name="window"></param>
        /// <returns></returns>
        private string GetWindowId(DesktopManagerWindow window)
        {
            if (!windowGuids.TryGetValue(window, out string guid))
            {
                // 使用新的GUID
                guid = Guid.NewGuid().ToString();
                windowGuids[window] = guid;

                // 调试信息
                Log.Information($"为窗口 {window.GetHashCode()} 创建新GUID: {guid}");
            }
            return guid;
        }

        /// <summary>
        /// 保存单个分区数据
        /// </summary>
        /// <param name="window"></param>
        public void SavePartitionData(DesktopManagerWindow window)
        {
            try
            {
                var viewModel = window.DataContext as DesktopManagerViewModel;
                if (viewModel == null)
                {
                    return;
                }
                string windowId = window.WindowId ?? GetWindowId(window);

                // 创建要保存的DTO
                var partitionData = new PartitionDataDto
                {
                    Id = windowId,
                    Name = viewModel.Name,
                    WindowPosition = new WindowPositionDto
                    {
                        Left = window.Left,
                        Top = window.Top,
                        Width = window.Width,
                        Height = window.Height
                    },
                    Icons = new List<IconDataDto>()
                };

                // 添加所有图标数据
                foreach (var icon in viewModel.Icons)
                {
                    partitionData.Icons.Add(new IconDataDto
                    {
                        Id = icon.Id,
                        Name = icon.Name,
                        IconPath = DesktopIconService.Instance.RemoveHiddenPathInIconPath(icon.IconPath),
                        Position = new PointDto { X = icon.Position.X, Y = icon.Position.Y },
                        Size = new SizeDto { Width = icon.Size.Width, Height = icon.Size.Height }
                    });
                }

                // 序列化并保存
                string json = JsonSerializer.Serialize(partitionData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                string filePath = Path.Combine(dataDirectory, $"partition_{windowId}.json");
                File.WriteAllText(filePath, json);

                // 保存窗口ID到应用程序数据目录
                SaveWindowIdMapping();
            }
            catch (Exception ex)
            {
                // 记录错误但不抛出异常
                Log.Information($"保存分区数据失败: {ex.Message}");
            }
        }

        // 保存所有分区的数据
        public void SaveAllPartitions()
        {
            var windows = WindowManagerService.Instance.GetAllWindows();
            foreach (var window in windows)
            {
                SavePartitionData(window);
            }
        }

        /// <summary>
        /// /// <summary>
        /// 保存窗口元数据
        /// </summary>
        /// </summary>
        /// <param name="windowId">新窗口Id</param>
        private void SaveMetadata(string windowId)
        {
            try
            {
                var metadata = GetMetadata();
                if (metadata == null)
                {
                    metadata = new WindowsMetadataDto
                    {
                        WindowIds = new List<string>()
                    };
                }
                if (!metadata.WindowIds.Contains(windowId))
                {
                    metadata.WindowIds.Add(windowId);
                }

                string json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                string filePath = Path.Combine(dataDirectory, "windows_metadata.json");
                File.WriteAllText(filePath, json);

                //通用配置文件中添加窗口的可见性设置
                GeneralSettingsService.Instance.SetPartitionVisibility(windowId, true);

                // 调试信息
                Log.Information($"保存窗口元数据到: {filePath}");
                Log.Information($"元数据内容: {json}");
            }
            catch (Exception ex)
            {
                Log.Information($"保存窗口元数据失败: {ex.Message}");
            }
        }


        /// <summary>
        /// 保存窗口元数据（用于跟踪已打开的窗口）
        /// </summary>
        /// <param name="windows"></param>
        private void SaveWindowsMetadata(List<DesktopManagerWindow> windows)
        {
            try
            {
                var metadata = new WindowsMetadataDto
                {
                    WindowIds = new List<string>()
                };

                foreach (var window in windows)
                {
                    string windowId = window.WindowId ?? GetWindowId(window);

                    // 确保ID不为空和0
                    if (string.IsNullOrEmpty(windowId) || windowId == "0")
                    {
                        windowId = Guid.NewGuid().ToString();
                        windowGuids[window] = windowId;
                    }

                    metadata.WindowIds.Add(windowId);

                    // 调试信息
                    Log.Information($"添加窗口ID到元数据: {windowId}");
                }

                string json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                string filePath = Path.Combine(dataDirectory, "windows_metadata.json");
                File.WriteAllText(filePath, json);

                // 调试信息
                Log.Information($"保存窗口元数据到: {filePath}");
                Log.Information($"元数据内容: {json}");
            }
            catch (Exception ex)
            {
                Log.Information($"保存窗口元数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存窗口ID映射
        /// </summary>
        public void SaveWindowIdMapping()
        {
            try
            {
                var mapping = new Dictionary<string, string>();

                foreach (var pair in windowGuids)
                {
                    // 使用窗口哈希码作为键，GUID作为值
                    string key = pair.Key.GetHashCode().ToString();
                    string value = pair.Value;

                    // 确保值不为空和0
                    if (string.IsNullOrEmpty(value) || value == "0")
                    {
                        value = Guid.NewGuid().ToString();
                        windowGuids[pair.Key] = value;
                    }

                    mapping[key] = value;

                    // 调试信息
                    Log.Information($"保存窗口ID映射: {key} -> {value}");
                }

                string json = JsonSerializer.Serialize(mapping, new JsonSerializerOptions { WriteIndented = true });
                string filePath = Path.Combine(dataDirectory, "window_id_mapping.json");
                File.WriteAllText(filePath, json);

                // 调试信息
                Log.Information($"保存窗口ID映射到: {filePath}");
                Log.Information($"映射内容: {json}");
            }
            catch (Exception ex)
            {
                Log.Information($"保存窗口ID映射失败: {ex.Message}");
            }
        }

        public void RemoveWindowMapping(DesktopManagerWindow window)
        {
            if (windowGuids.Remove(window))
            {
                SaveWindowIdMapping();
            }
        }

        /// <summary>
        /// 加载窗口ID映射
        /// </summary>
        private void LoadWindowIdMapping()
        {
            try
            {
                string filePath = Path.Combine(dataDirectory, "window_id_mapping.json");
                if (!File.Exists(filePath))
                {
                    return;
                }
                string json = File.ReadAllText(filePath);
                var mapping = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                if (mapping == null)
                {
                    return;
                }
                // 这里只能加载映射，无法立即将其应用到窗口
                // 因为窗口实例会在后续创建
                // 所以我们在这里保存映射，然后在窗口加载时使用
                windowIdMapping = mapping;
            }
            catch (Exception ex)
            {
                Log.Information($"加载窗口ID映射失败: {ex.Message}");
            }
        }


        // 尝试为新创建的窗口恢复之前的GUID
        public void AssociateWindowWithPreviousId(DesktopManagerWindow window)
        {
            string hashCode = window.GetHashCode().ToString();

            if (windowIdMapping.TryGetValue(hashCode, out string guid))
            {
                windowGuids[window] = guid;
            }
        }

        /// <summary>
        /// 恢复上次会话的所有窗口
        /// </summary>
        public void RestoreWindows()
        {
            try
            {
                // 首先加载窗口ID映射
                LoadWindowIdMapping();

                var metadata = GetMetadata();

                if (metadata == null || metadata.WindowIds == null || metadata.WindowIds.Count == 0)
                {
                    return;
                }
                // 创建所有保存的窗口
                foreach (var windowId in metadata.WindowIds)
                {
                    // 验证GUID是否有效
                    if (string.IsNullOrEmpty(windowId) || windowId == "0")
                    {
                        Log.Information("跳过无效的窗口ID: " + (windowId ?? "null"));
                        continue;
                    }

                    //检查窗口是否配置为不可见
                    bool isVisible = GeneralSettingsService.Instance.GetPartitionVisibility(windowId);
                    if (!isVisible)
                    {
                        Log.Information($"跳过不可见的窗口: {windowId}");
                        continue;
                    }

                    // 检查是否已存在使用此ID的窗口
                    bool idAlreadyUsed = windowGuids.Values.Contains(windowId);
                    if (idAlreadyUsed)
                    {
                        Log.Information($"ID已被使用，跳过: {windowId}");
                        continue;
                    }

                    // 创建并显示窗口
                    ShowWindow(windowId);
                }
            }
            catch (Exception ex)
            {
                Log.Information($"恢复窗口失败: {ex.Message}");

                // 如果恢复失败，创建一个新窗口（确保使用新GUID）
                var window = new DesktopManagerWindow();
                windowGuids[window] = Guid.NewGuid().ToString();
                window.Show();
            }
        }

        /// <summary>
        /// 恢复窗口显示
        /// </summary>
        /// <param name="windowId"></param>
        public void RestoreWindow(string windowId)
        {
            try
            {
                // 首先加载窗口ID映射
                LoadWindowIdMapping();

                var metadata = GetMetadata();

                if (string.IsNullOrEmpty(windowId))
                {
                    return;
                }

                // 检查是否已存在使用此ID的窗口
                bool idAlreadyUsed = windowGuids.Values.Contains(windowId);
                if (idAlreadyUsed)
                {
                    Log.Information($"窗口已存在，跳过: {windowId}");
                    return;
                }
                // 创建并显示窗口
                ShowWindow(windowId);
            }
            catch (Exception ex)
            {
                Log.Information($"恢复窗口失败: {ex.Message}");

                // 如果恢复失败，创建一个新窗口（确保使用新GUID）
                var window = new DesktopManagerWindow();
                windowGuids[window] = Guid.NewGuid().ToString();
                window.Show();
            }
        }


        public void RemoveWindow(string windowId)
        {
            var metadata = GetMetadata();
            metadata.WindowIds.Remove(windowId);

            string json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            string filePath = Path.Combine(dataDirectory, "windows_metadata.json");
            File.WriteAllText(filePath, json);

            //更新通用配置文件中窗口的可见性设置
            GeneralSettingsService.Instance.RemovePartitionVisibility(windowId);
        }

        /// <summary>
        /// 加载分区数据
        /// </summary>
        public void LoadPartitionData(DesktopManagerWindow window, string specificWindowId = null)
        {
            try
            {
                string windowId = specificWindowId ?? GetWindowId(window);
                window.WindowId = windowId;

                string filePath = Path.Combine(dataDirectory, $"partition_{windowId}.json");

                Log.Information($"尝试加载分区数据: {filePath}");

                // 只有在明确指定ID或找到对应配置文件时才加载该文件
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var partitionData = JsonSerializer.Deserialize<PartitionDataDto>(json);

                    if (partitionData == null)
                    {
                        Log.Information("配置文件内容为空或格式无效");
                        return;
                    }

                    // 更新窗口位置和大小
                    window.Left = partitionData.WindowPosition.Left;
                    window.Top = partitionData.WindowPosition.Top;
                    window.Width = partitionData.WindowPosition.Width;
                    window.Height = partitionData.WindowPosition.Height;

                    // 更新ViewModel数据
                    var viewModel = window.DataContext as DesktopManagerViewModel;
                    if (viewModel != null)
                    {
                        viewModel.Name = partitionData.Name;
                        viewModel.Icons.Clear();

                        // 添加所有图标
                        foreach (var iconData in partitionData.Icons)
                        {
                            var icon = new DesktopIcon
                            {
                                Id = iconData.Id,
                                Name = iconData.Name,
                                IconPath = DesktopIconService.Instance.GetAvailableIconPath(iconData.IconPath),
                                Position = new Point(iconData.Position.X, iconData.Position.Y),
                                Size = new Size(iconData.Size.Width, iconData.Size.Height)
                            };

                            viewModel.AddIcon(icon);
                        }

                        Log.Information($"成功加载分区 '{viewModel.Name}' 数据，包含 {viewModel.Icons.Count} 个图标");
                    }
                }
                else
                {
                    Log.Information($"未找到配置文件: {filePath}");

                    if (specificWindowId == null)
                    {
                        Log.Information("使用新窗口默认设置");
                        windowGuids[window] = string.IsNullOrEmpty(windowId) ? Guid.NewGuid().ToString() : windowId;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Information($"加载分区数据失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public PartitionDataDto GetPartitionData(string windowId)
        {
            string filePath = Path.Combine(dataDirectory, $"partition_{windowId}.json");

            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<PartitionDataDto>(json);
            }
            return null;
        }

        public WindowsMetadataDto GetMetadata()
        {
            string metadataPath = Path.Combine(dataDirectory, "windows_metadata.json");
            if (!File.Exists(metadataPath))
            {
                return null;
            }
            string json = File.ReadAllText(metadataPath);
            var metadata = JsonSerializer.Deserialize<WindowsMetadataDto>(json);

            return metadata;
        }

        /// <summary>
        /// 显示窗口
        /// (适合显示已保存的窗口)
        /// </summary>
        /// <param name="windowId"></param>
        public void ShowWindow(string windowId)
        {
            // 创建新窗口并分配唯一ID
            var window = new DesktopManagerWindow();
            windowGuids[window] = windowId; // 关联窗口和ID

            // 加载对应的分区数据
            string partitionPath = Path.Combine(dataDirectory, $"partition_{windowId}.json");
            if (File.Exists(partitionPath))
            {
                Log.Information($"为窗口 {window.GetHashCode()} 加载配置: {windowId}");
                LoadPartitionData(window, windowId);
                window.Sync(window);
                window.Show();
            }
            else
            {
                Log.Information($"未找到窗口 {windowId} 的配置，使用新ID");
                // 如果找不到配置，使用新GUID
                windowGuids[window] = Guid.NewGuid().ToString();
                window.Show();
            }
        }

        /// <summary>
        /// 显示窗口
        /// （适合显示新创建的窗口）
        /// </summary>
        public void ShowWindow(DesktopManagerWindow window)
        {
            string windowId = window.WindowId ?? Guid.NewGuid().ToString();
            windowGuids[window] = windowId;

            // 加载对应的分区数据
            string partitionPath = Path.Combine(dataDirectory, $"partition_{windowId}.json");
            if (File.Exists(partitionPath))
            {
                Log.Information($"为窗口 {window.GetHashCode()} 加载配置: {windowId}");
                LoadPartitionData(window, windowId);
                window.Sync(window);
                window.Show();
            }
            //新窗口
            else
            {
                SaveMetadata(windowId);
                window.Sync(window);
                window.Show();
            }
        }

    }

}
