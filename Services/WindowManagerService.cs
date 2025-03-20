using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using Layouter.Views;

namespace Layouter.Services
{
    public class WindowManagerService
    {
        private static readonly Lazy<WindowManagerService> instance = new Lazy<WindowManagerService>(() => new WindowManagerService());
        public static WindowManagerService Instance => instance.Value;

        private List<DesktopManagerWindow> managedWindows = new List<DesktopManagerWindow>();

        private const double WindowMargin = 5;

        private WindowManagerService() { }

        public void RegisterWindow(DesktopManagerWindow window)
        {
            if (!managedWindows.Contains(window))
            {
                managedWindows.Add(window);

                // 初始新窗口位置，确保与其他窗口对齐而不是居中
                PositionNewWindow(window);

                // 监听窗口位置变化
                window.LocationChanged += Window_LocationChanged;
            }
        }

        public DesktopManagerWindow GetWindowByHandle(IntPtr handle)
        {
            return managedWindows.FirstOrDefault(w => new WindowInteropHelper(w).Handle == handle);
        }

        public List<DesktopManagerWindow> GetAllWindows()
        {
            return new List<DesktopManagerWindow>(managedWindows);
        }

        public void UnregisterWindow(DesktopManagerWindow window)
        {
            if (managedWindows.Contains(window))
            {
                window.LocationChanged -= Window_LocationChanged;
                managedWindows.Remove(window);
            }
        }

        public void ArrangeWindows()
        {
            var visibleWindows = managedWindows.Where(w => w.IsVisible).ToList();
            if (visibleWindows.Count == 0)
            {
                return;
            }
            var workArea = SystemParameters.WorkArea;
            double currentX = WindowMargin;
            double currentY = WindowMargin;
            double rowHeight = 0;

            foreach (var window in visibleWindows)
            {
                // 如果窗口超出屏幕宽度，则换行
                if (currentX + window.Width + WindowMargin > workArea.Width)
                {
                    currentX = WindowMargin;
                    currentY += rowHeight + WindowMargin;
                    rowHeight = 0;
                }

                // 设置窗口位置
                window.Left = currentX;
                window.Top = currentY;

                // 更新位置
                currentX += window.Width + WindowMargin;
                rowHeight = Math.Max(rowHeight, window.Height);

                // 保存分区数据
                PartitionDataService.Instance.SavePartitionData(window);
            }
        }

        private void PositionNewWindow(DesktopManagerWindow newWindow)
        {
            var visibleWindows = managedWindows
                .Where(w => w != newWindow && w.IsVisible)
                .ToList();

            if (visibleWindows.Count == 0)
            {
                // 如果没有其他可见窗口，则放在工作区左上角
                newWindow.Left = WindowMargin;
                newWindow.Top = WindowMargin;
                return;
            }

            // 计算新窗口应该放置的位置
            var workArea = SystemParameters.WorkArea;
            double maxRight = 0;
            double minTop = double.MaxValue;
            double maxBottom = 0;

            foreach (var window in visibleWindows)
            {
                maxRight = Math.Max(maxRight, window.Left + window.Width);
                minTop = Math.Min(minTop, window.Top);
                maxBottom = Math.Max(maxBottom, window.Top + window.Height);
            }

            // 尝试放在最右侧
            if (maxRight + newWindow.Width + WindowMargin <= workArea.Width)
            {
                newWindow.Left = maxRight + WindowMargin;
                newWindow.Top = minTop;
            }
            else
            {
                // 否则尝试放在下一行
                newWindow.Left = WindowMargin;
                newWindow.Top = maxBottom + WindowMargin;
            }
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            // 如果需要实现实时窗口对齐，可以在这里处理
        }

        public DesktopManagerWindow GetWindowAt(Point screenPoint)
        {
            return managedWindows.FirstOrDefault(w => w.IsVisible && new Rect(w.Left, w.Top, w.Width, w.Height).Contains(screenPoint));
        }

        /// <summary>
        /// 从配置中删除指定标题的分区窗口
        /// </summary>
        /// <param name="windowId">窗口的唯一标识符</param>
        public void RemovePartitionWindow(DesktopManagerWindow window)
        {
            if (window == null || string.IsNullOrEmpty(window.WindowId))
            {
                return;
            }
            try
            {
                // 删除分区并更新配置
                PartitionDataService.Instance.RemoveWindow(window.WindowId);

                UnregisterWindow(window);
                window.Close();
                Log.Information($"已关闭分区窗口 {window.WindowId}");
            }
            catch (Exception ex)
            {
                Log.Information($"删除分区配置时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 对齐所有分区窗口
        /// </summary>
        public void ArrangeAllPartitionWindows()
        {
            var windows = GetAllWindows();
            if (windows.Count == 0)
            {
                return;
            }
            // 获取屏幕工作区尺寸
            double screenWidth = SystemParameters.WorkArea.Width;
            double screenHeight = SystemParameters.WorkArea.Height;

            // 计算每个窗口的理想尺寸
            int numWindows = windows.Count;
            int numCols = (int)Math.Ceiling(Math.Sqrt(numWindows));
            int numRows = (int)Math.Ceiling((double)numWindows / numCols);

            double windowWidth = screenWidth / numCols;
            double windowHeight = screenHeight / numRows;

            // 分配窗口位置
            for (int i = 0; i < windows.Count; i++)
            {
                int row = i / numCols;
                int col = i % numCols;

                // 计算窗口位置
                double left = col * windowWidth;
                double top = row * windowHeight;

                // 设置窗口位置和大小
                windows[i].Left = left;
                windows[i].Top = top;
                windows[i].Width = windowWidth;
                windows[i].Height = windowHeight;
            }
        }
    }
}
