using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Layouter.Views;
using Hardcodet.Wpf.TaskbarNotification;
using Layouter.ViewModels;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using FluentIcons.Wpf;
using Layouter.Utility;
using System.Collections;

namespace Layouter.Services
{
    public class TrayIconService : IDisposable
    {
        private TaskbarIcon notifyIcon;
        private readonly TrayIconViewModel viewModel;

        public TrayIconService(TrayIconViewModel vm)
        {
            this.viewModel = vm;
        }

        public void Initialize()
        {
            // 从资源中获取托盘图标
            notifyIcon = Application.Current.FindResource("TrayIcon") as TaskbarIcon;

            if (notifyIcon != null)
            {
                // 设置数据上下文
                notifyIcon.DataContext = viewModel;
                notifyIcon.Icon = CreateIconFromFluentIcon();
            }
            else
            {
                MessageBox.Show("无法初始化托盘图标", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private System.Drawing.Icon CreateIconFromFluentIcon()
        {

            var fluentIcon = new FluentIcon()
            {
                Icon = FluentIcons.Common.Icon.AppFolder, // 选择合适的图标
                Foreground = Brushes.Black,
                IconSize = FluentIcons.Common.IconSize.Size16,
                Width = 16,
                Height = 16
            };

            // 渲染FluentIcon到一个RenderTargetBitmap
            fluentIcon.Measure(new Size(16, 16));
            fluentIcon.Arrange(new Rect(0, 0, 16, 16));

            var renderBitmap = new RenderTargetBitmap(16, 16, 96, 96, PixelFormats.Pbgra32);
            renderBitmap.Render(fluentIcon);

            // 将RenderTargetBitmap转换为Icon
            using (MemoryStream stream = new MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
                encoder.Save(stream);
                stream.Position = 0;

                // 使用System.Drawing创建图标
                using (var bitmap = new System.Drawing.Bitmap(stream))
                {
                    return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
                }
            }
        }

        public void Dispose()
        {
            notifyIcon?.Dispose();
        }
    }
}
