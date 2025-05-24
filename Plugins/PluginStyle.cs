using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Text.Json;
using PluginEntry;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using Layouter.Models;

namespace Layouter.Plugins
{
    public class PluginStyle
    {
        public WindowPositionDto WindowPosition { get; set; } = new WindowPositionDto(0, 0, 300, 200);

        /// <summary>
        /// 透明度
        /// </summary>
        public double Opacity { get; set; } = 0.7d;

        /// <summary>
        /// 列表项高度
        /// </summary>
        public double ItemHeight { get; set; } = 20d;

        /// <summary>
        /// 列表项字体大小
        /// </summary>
        public double ItemFontSize { get; set; } = 10.5d;

        /// <summary>
        /// 背景颜色
        /// </summary>
        public Color BackgroundColor { get; set; } = Colors.Black;

        /// <summary>
        /// 前景颜色
        /// </summary>
        public Color ForegroundColor { get; set; } = Colors.White;


        /// <summary>
        /// 列表项颜色
        /// </summary>
        public List<Color> ItemColors { get; set; } = new List<Color>();

        /// <summary>
        /// 底部线高度
        /// </summary>
        public double BottomLineHeight { get; set; } = 1d;

        /// <summary>
        /// 底部线颜色
        /// </summary>
        public Color BottomLineColor { get; set; } = Color.FromRgb(255, 215, 0);

        /// <summary>
        /// 百分比模式
        /// </summary>
        public bool PercentageMode { get; set; } = false;

        /// <summary>
        /// 是否周期执行
        /// </summary>
        public bool CycleExecution { get; set; } = false;

        /// <summary>
        /// 周期执行间隔
        /// </summary>
        public double Inteval { get; set; } = 1;

        public static PluginStyle FromJson(string json)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ColorJsonConverter()); // 注册颜色转换器

            return JsonSerializer.Deserialize<PluginStyle>(json, options);
        }
    }

    /// <summary>
    /// 颜色转换器
    /// 支持如下格式：
    ///     "#FF0000FF"	ARGB 十六进制
    ///     "0000FF"	    RGB 十六进制
    ///     "FF0000FF"	    ARGB 十六进制
    ///     "Red"	        命名颜色
    ///     "255,0,0"	    RGB 格式 
    ///     "128,255,0,0"	ARGB 格式 
    /// </summary>
    public class ColorJsonConverter : JsonConverter<Color>
    {
        public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string input = reader.GetString()?.Trim();

            if (string.IsNullOrWhiteSpace(input))
                return Colors.Transparent;

            try
            {
                // 支持命名颜色和标准格式（如 #FF0000FF）
                if (input.StartsWith("#") || IsNamedColor(input))
                {
                    return (Color)ColorConverter.ConvertFromString(input);
                }

                // 支持无 "#" 的 RGB 或 ARGB 十六进制，如 "FF0000FF" 或 "0000FF"
                if (IsHexColor(input))
                {
                    if (input.Length == 6)
                    {
                        // RGB - assume fully opaque
                        input = "#FF" + input;
                    }
                    else if (input.Length == 8)
                    {
                        input = "#" + input;
                    }

                    return (Color)ColorConverter.ConvertFromString(input);
                }

                // 支持逗号分隔的 RGB 或 ARGB 值，如 "0,30,100" 或 "255,0,0,128"
                var parts = input.Split(',');
                if (parts.Length == 3 || parts.Length == 4)
                {
                    byte a = 255;
                    int index = 0;

                    if (parts.Length == 4)
                    {
                        a = byte.Parse(parts[index++].Trim());
                    }

                    byte r = byte.Parse(parts[index++].Trim());
                    byte g = byte.Parse(parts[index++].Trim());
                    byte b = byte.Parse(parts[index++].Trim());

                    return Color.FromArgb(a, r, g, b);
                }

                throw new FormatException("Unsupported color format: " + input);
            }
            catch (Exception ex)
            {
                throw new JsonException($"Could not parse Color from '{input}': {ex.Message}");
            }
        }

        public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString()); // 输出为 "#AARRGGBB"
        }

        private bool IsNamedColor(string input)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(input);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool IsHexColor(string input)
        {
            return input.Length == 6 || input.Length == 8;
        }
    }


}
