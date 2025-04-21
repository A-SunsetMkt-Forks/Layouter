using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Layouter.Models;
using Layouter.Services;
using Layouter.ViewModels;
using Microsoft.Win32;
using ColorDialog = System.Windows.Forms.ColorDialog;

namespace Layouter.Views
{
    public partial class PartitionSettingsWindow : Window
    {
        private DesktopManagerViewModel vm;
        private bool isGlobalSettings;

        // 保存原始设置，用于取消或重置
        private SolidColorBrush originalTitleForeground;
        private SolidColorBrush originalTitleBackground;
        private FontFamily originalTitleFont;
        private HorizontalAlignment originalTitleAlignment;
        private SolidColorBrush originalContentBackground;
        private double originalTitleFontSize;
        private double originalOpacity;
        private IconSize originalIconSize;
        private double originalIconTextSize;

        public PartitionSettingsWindow(DesktopManagerViewModel viewModel, bool isGlobalSettings = false)
        {
            InitializeComponent();

            vm = viewModel;
            this.isGlobalSettings = isGlobalSettings;

            // 保存原始设置
            originalTitleForeground = vm.TitleForeground.Clone();
            originalTitleBackground = vm.TitleBackground.Clone();
            originalTitleFont = vm.TitleFont;
            originalTitleAlignment = vm.TitleAlignment;
            originalTitleFontSize = vm.TitleFontSize;
            originalOpacity = vm.Opacity;
            originalIconSize = vm.IconSize;
            originalIconTextSize = vm.IconTextSize;

            // 初始化UI
            InitializeUI();

            // 设置窗口标题
            this.Title = isGlobalSettings ? "全局分区设置" : "分区设置";
        }

        private void InitializeUI()
        {
            // 设置颜色预览
            TitleForegroundPreview.Fill = vm.TitleForeground.Clone();
            TitleBackgroundPreview.Fill = vm.TitleBackground.Clone();
            TitleFontSizeSlider.Value = vm.TitleFontSize;
            TitleFontSizeText.Text = vm.TitleFontSize.ToString("F1");
            TextSizeSlider.Value = vm.IconTextSize;

            // 设置字体下拉框
            foreach (var font in Fonts.SystemFontFamilies)
            {
                if (font.Source == vm.TitleFont.Source)
                {
                    TitleFontComboBox.SelectedItem = font;
                    break;
                }
            }

            // 设置对齐方式
            switch (vm.TitleAlignment)
            {
                case HorizontalAlignment.Left:
                    TitleAlignmentComboBox.SelectedIndex = 0;
                    break;
                case HorizontalAlignment.Center:
                    TitleAlignmentComboBox.SelectedIndex = 1;
                    break;
                case HorizontalAlignment.Right:
                    TitleAlignmentComboBox.SelectedIndex = 2;
                    break;
            }

            // 设置透明度
            OpacitySlider.Value = vm.Opacity;

            // 设置图标大小
            switch (vm.IconSize)
            {
                case IconSize.Small:
                    IconSizeComboBox.SelectedIndex = 0;
                    break;
                case IconSize.Medium:
                    IconSizeComboBox.SelectedIndex = 1;
                    break;
                case IconSize.Large:
                    IconSizeComboBox.SelectedIndex = 2;
                    break;
            }

        }

        private void TitleForegroundButton_Click(object sender, RoutedEventArgs e)
        {
            var colorDialog = new ColorDialog();
            var currentColor = ((SolidColorBrush)TitleForegroundPreview.Fill).Color;
            colorDialog.Color = System.Drawing.Color.FromArgb(currentColor.A, currentColor.R, currentColor.G, currentColor.B);

            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var newColor = Color.FromArgb(colorDialog.Color.A, colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B);
                TitleForegroundPreview.Fill = new SolidColorBrush(newColor);
            }
        }

        private void TitleBackgroundButton_Click(object sender, RoutedEventArgs e)
        {
            var colorDialog = new ColorDialog();
            colorDialog.Color = System.Drawing.Color.FromArgb(
                ((SolidColorBrush)TitleBackgroundPreview.Fill).Color.R,
                ((SolidColorBrush)TitleBackgroundPreview.Fill).Color.G,
                ((SolidColorBrush)TitleBackgroundPreview.Fill).Color.B);

            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // 创建带透明度的颜色
                Color c = Color.FromRgb(colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B);

                // 应用基础透明度
                c.A = vm.TitleBaseAlpha;

                TitleBackgroundPreview.Fill = new SolidColorBrush(c);
            }

        }


        private void TitleFontComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void TitleAlignmentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (PreviewBorder == null)
            {
                return;
            }
            PreviewBorder.Opacity = OpacitySlider.Value;
        }

        private void TitleFontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TitleFontSizeText != null)
            {
                TitleFontSizeText.Text = e.NewValue.ToString("F1");
            }
        }

        private void IconSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 预览更新
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            // 重置为默认值
            TitleForegroundPreview.Fill = new SolidColorBrush(Colors.White);
            TitleBackgroundPreview.Fill = new SolidColorBrush(Colors.DodgerBlue);
            TitleFontComboBox.SelectedItem = new FontFamily("Microsoft YaHei");
            TitleFontSizeSlider.Value = 14d;
            TitleAlignmentComboBox.SelectedIndex = 0; // 左对齐
            OpacitySlider.Value = 0.90;
            IconSizeComboBox.SelectedIndex = 1; // 中等大小

        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // 应用设置到ViewModel
            vm.TitleForeground = ((SolidColorBrush)TitleForegroundPreview.Fill).Clone();
            vm.TitleBackground = ((SolidColorBrush)TitleBackgroundPreview.Fill).Clone();
            vm.TitleFont = (FontFamily)TitleFontComboBox.SelectedItem;

            // 设置对齐方式
            switch (TitleAlignmentComboBox.SelectedIndex)
            {
                case 0:
                    vm.TitleAlignment = HorizontalAlignment.Left;
                    break;
                case 1:
                    vm.TitleAlignment = HorizontalAlignment.Center;
                    break;
                case 2:
                    vm.TitleAlignment = HorizontalAlignment.Right;
                    break;
            }

            vm.TitleFontSize = TitleFontSizeSlider.Value;
            vm.Opacity = OpacitySlider.Value;

            // 设置图标大小
            switch (IconSizeComboBox.SelectedIndex)
            {
                case 0:
                    vm.IconSize = IconSize.Small;
                    break;
                case 1:
                    vm.IconSize = IconSize.Medium;
                    break;
                case 2:
                    vm.IconSize = IconSize.Large;
                    break;
            }

            //设置图标文字大小
            vm.IconTextSize = TextSizeSlider.Value;
            foreach (var icon in vm.Icons)
            {
                icon.TextSize = vm.IconTextSize;
            }
            
            if (!string.IsNullOrEmpty(vm.windowId))
            {
                // 保存设置到配置文件
                PartitionSettingsService.Instance.SaveSettings(vm, isGlobalSettings);
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // 恢复原始设置
            vm.TitleForeground = originalTitleForeground;
            vm.TitleBackground = originalTitleBackground;
            vm.TitleFont = originalTitleFont;
            vm.TitleAlignment = originalTitleAlignment;
            vm.TitleFontSize = originalTitleFontSize;
            vm.Opacity = originalOpacity;
            vm.IconSize = originalIconSize;
            vm.IconTextSize = originalIconTextSize;

            DialogResult = false;
            Close();
        }

    }
}