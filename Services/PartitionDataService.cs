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

namespace Layouter.Services
{
    public class PartitionDataService
    {
        private static readonly Lazy<PartitionDataService> instance =
            new Lazy<PartitionDataService>(() => new PartitionDataService());

        public static PartitionDataService Instance => instance.Value;

        private string dataDirectory;
        private Dictionary<DesktopManagerWindow, string> windowGuids = new Dictionary<DesktopManagerWindow, string>();

        // 存储哈希码到GUID的映射，用于恢复窗口配置
        private Dictionary<string, string> windowIdMapping = new Dictionary<string, string>();

        private PartitionDataService()
        {
            // 配置保存目录
            dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Layouter");

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
                System.Diagnostics.Debug.WriteLine($"为窗口 {window.GetHashCode()} 创建新GUID: {guid}");
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
                var viewModel = window.DataContext as PartitionViewModel;
                if (viewModel == null)
                {
                    return;
                }
                string windowId = GetWindowId(window);

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
                        IconPath = DesktopIconService.RemoveHiddenPathInIconPath(icon.IconPath),
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
                System.Diagnostics.Debug.WriteLine($"保存分区数据失败: {ex.Message}");
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

            // 保存窗口列表元数据
            SaveWindowsMetadata(windows);
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
                    string windowId = GetWindowId(window);

                    // 确保ID不为空和0
                    if (string.IsNullOrEmpty(windowId) || windowId == "0")
                    {
                        windowId = Guid.NewGuid().ToString();
                        windowGuids[window] = windowId;
                    }

                    metadata.WindowIds.Add(windowId);

                    // 调试信息
                    System.Diagnostics.Debug.WriteLine($"添加窗口ID到元数据: {windowId}");
                }

                string json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                string filePath = Path.Combine(dataDirectory, "windows_metadata.json");
                File.WriteAllText(filePath, json);

                // 调试信息
                System.Diagnostics.Debug.WriteLine($"保存窗口元数据到: {filePath}");
                System.Diagnostics.Debug.WriteLine($"元数据内容: {json}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存窗口元数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存窗口ID映射
        /// </summary>
        private void SaveWindowIdMapping()
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
                    System.Diagnostics.Debug.WriteLine($"保存窗口ID映射: {key} -> {value}");
                }

                string json = JsonSerializer.Serialize(mapping, new JsonSerializerOptions { WriteIndented = true });
                string filePath = Path.Combine(dataDirectory, "window_id_mapping.json");
                File.WriteAllText(filePath, json);

                // 调试信息
                System.Diagnostics.Debug.WriteLine($"保存窗口ID映射到: {filePath}");
                System.Diagnostics.Debug.WriteLine($"映射内容: {json}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存窗口ID映射失败: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"加载窗口ID映射失败: {ex.Message}");
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

                string metadataPath = Path.Combine(dataDirectory, "windows_metadata.json");
                if (!File.Exists(metadataPath))
                {
                    return;
                }
                string json = File.ReadAllText(metadataPath);
                var metadata = JsonSerializer.Deserialize<WindowsMetadataDto>(json);

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
                        System.Diagnostics.Debug.WriteLine("跳过无效的窗口ID: " + (windowId ?? "null"));
                        continue;
                    }

                    // 检查是否已存在使用此ID的窗口
                    bool idAlreadyUsed = windowGuids.Values.Contains(windowId);
                    if (idAlreadyUsed)
                    {
                        System.Diagnostics.Debug.WriteLine($"ID已被使用，跳过: {windowId}");
                        continue;
                    }

                    // 创建新窗口并分配唯一ID
                    var window = new DesktopManagerWindow();
                    windowGuids[window] = windowId; // 关联窗口和ID

                    // 加载对应的分区数据
                    string partitionPath = Path.Combine(dataDirectory, $"partition_{windowId}.json");
                    if (File.Exists(partitionPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"为窗口 {window.GetHashCode()} 加载配置: {windowId}");
                        LoadPartitionData(window, windowId);
                        window.Show();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"未找到窗口 {windowId} 的配置，使用新ID");
                        // 如果找不到配置，使用新GUID
                        windowGuids[window] = Guid.NewGuid().ToString();
                        window.Show();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"恢复窗口失败: {ex.Message}");

                // 如果恢复失败，创建一个新窗口（确保使用新GUID）
                var window = new DesktopManagerWindow();
                windowGuids[window] = Guid.NewGuid().ToString();
                window.Show();
            }
        }

        /// <summary>
        /// 加载分区数据
        /// </summary>
        /// <param name="window"></param>
        /// <param name="specificWindowId"></param>
        public void LoadPartitionData(DesktopManagerWindow window, string specificWindowId = null)
        {
            try
            {
                string windowId = specificWindowId ?? GetWindowId(window);
                string filePath = Path.Combine(dataDirectory, $"partition_{windowId}.json");

                System.Diagnostics.Debug.WriteLine($"尝试加载分区数据: {filePath}");

                // 只有在明确指定ID或找到对应配置文件时才加载该文件
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var partitionData = JsonSerializer.Deserialize<PartitionDataDto>(json);

                    if (partitionData == null)
                    {
                        System.Diagnostics.Debug.WriteLine("配置文件内容为空或格式无效");
                        return;
                    }

                    // 更新窗口位置和大小
                    window.Left = partitionData.WindowPosition.Left;
                    window.Top = partitionData.WindowPosition.Top;
                    window.Width = partitionData.WindowPosition.Width;
                    window.Height = partitionData.WindowPosition.Height;

                    // 更新ViewModel数据
                    var viewModel = window.DataContext as PartitionViewModel;
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
                                IconPath = DesktopIconService.GetAvailableIconPath(iconData.IconPath),
                                Position = new Point(iconData.Position.X, iconData.Position.Y),
                                Size = new Size(iconData.Size.Width, iconData.Size.Height)
                            };

                            viewModel.AddIcon(icon);
                        }

                        System.Diagnostics.Debug.WriteLine($"成功加载分区 '{viewModel.Name}' 数据，包含 {viewModel.Icons.Count} 个图标");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"未找到配置文件: {filePath}");

                    // 如果是没有指定ID的情况，不要尝试加载其他窗口的配置
                    if (specificWindowId == null)
                    {
                        System.Diagnostics.Debug.WriteLine("使用新窗口默认设置");
                        // 这是一个新窗口，确保它有一个唯一的GUID
                        windowGuids[window] = Guid.NewGuid().ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载分区数据失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        #region 数据传输对象

        public class PartitionDataDto
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public WindowPositionDto WindowPosition { get; set; }
            public List<IconDataDto> Icons { get; set; }
        }

        public class WindowPositionDto
        {
            public double Left { get; set; }
            public double Top { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
        }

        public class IconDataDto
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string IconPath { get; set; }
            public PointDto Position { get; set; }
            public SizeDto Size { get; set; }
        }

        public class PointDto
        {
            public double X { get; set; }
            public double Y { get; set; }
        }

        public class SizeDto
        {
            public double Width { get; set; }
            public double Height { get; set; }
        }

        public class WindowsMetadataDto
        {
            public List<string> WindowIds { get; set; }
        }

        #endregion
    }
}
