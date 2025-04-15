using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentIcons.Wpf;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using FluentIcons.Common;

namespace Layouter.Utility
{
    public class IconUtil
    {
        public static System.Drawing.Icon CreateIconFromFluentIcon(Icon icon, Brush foreground, IconSize size = IconSize.Size16)
        {
            double w = Convert.ToDouble(size);
            double h = Convert.ToDouble(size);

            var fluentIcon = new FluentIcon()
            {
                Icon = icon,
                Foreground = foreground,
                IconSize = size,
                Width = w,
                Height = h
            };

            // 渲染FluentIcon到一个RenderTargetBitmap
            fluentIcon.Measure(new Size(w, h));
            fluentIcon.Arrange(new Rect(0, 0, w, h));

            var renderBitmap = new RenderTargetBitmap((int)size, (int)size, 96, 96, PixelFormats.Pbgra32);
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


        public static SymbolIcon CreateMenuItemIcon(Symbol symbol, Color color)
        {
            var icon = new SymbolIcon
            {
                Symbol = symbol,
                FontSize = 12,
                Foreground = new SolidColorBrush(color),
                Width = 16,
                Height = 16
            };

            return icon;
        }

    }
}
