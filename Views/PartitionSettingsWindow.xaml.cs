using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Layouter.Models;
using Layouter.Services;
using Layouter.ViewModels;
using Microsoft.Win32;

namespace Layouter.Views
{
    public partial class PartitionSettingsWindow : Window
    {
        private DesktopManagerViewModel _viewModel;
        private bool _isGlobalSettings;
        
        // 保存原始设置，用于取消或重置
        private SolidColorBrush _originalTitleForeground;
        private SolidColorBrush _originalTitleBackground;
        private FontFamily _originalTitleFont;
        private HorizontalAlignment _originalTitleAlignment;
        private SolidColorBrush _originalContentBackground;
        private double _originalOpacity;
        private IconSize _originalIconSize;

        public PartitionSettingsWindow(DesktopManagerViewModel viewModel, bool isGlobalSettings = false)
        {
            InitializeComponent();
            
            _viewModel = viewModel;
            _isGlobalSettings = isGlobalSettings;
            
            // 保存原始设置
            _originalTitleForeground = _viewModel.TitleForeground.Clone();
            _originalTitleBackground = _viewModel.TitleBackground.Clone();
            _originalTitleFont = _viewModel.TitleFont;
            _originalTitleAlignment = _viewModel.TitleAlignment;
            _originalContentBackground = _viewModel.ContentBackground.Clone();
            _originalOpacity = _viewModel.Opacity;
            _originalIconSize = _viewModel.IconSize;
            
            // 初始化UI
            InitializeUI();
            
            // 设置全局设置选项的可见性
            ApplyToAllCheckBox.Visibility = isGlobalSettings ? Visibility.Visible : Visibility.Collapsed;
            ApplyToAllCheckBox.IsChecked = isGlobalSettings;
            
            // 设置窗口标题
            this.Title = isGlobalSettings ? "全局分区设置" : "分区设置";
        }
        
        private void InitializeUI()
        {
            // 设置颜色预览
            TitleForegroundPreview.Fill = _viewModel.TitleForeground.Clone();
            TitleBackgroundPreview.Fill = _viewModel.TitleBackground.Clone();
            ContentBackgroundPreview.Fill = _viewModel.ContentBackground.Clone();
            
            // 设置字体下拉框
            foreach (var font in Fonts.SystemFontFamilies)
            {
                if (font.Source == _viewModel.TitleFont.Source)
                {
                    TitleFontComboBox.SelectedItem = font;
                    break;
                }
            }
            
            // 设置对齐方式
            switch (_viewModel.TitleAlignment)
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
            OpacitySlider.Value = _viewModel.Opacity;
            
            // 设置图标大小
            switch (_viewModel.IconSize)
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
            var currentColor = ((SolidColorBrush)TitleBackgroundPreview.Fill).Color;
            colorDialog.Color = System.Drawing.Color.FromArgb(currentColor.A, currentColor.R, currentColor.G, currentColor.B);
            
            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var newColor = Color.FromArgb(colorDialog.Color.A, colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B);
                TitleBackgroundPreview.Fill = new SolidColorBrush(newColor);
            }
        }
        
        private void ContentBackgroundButton_Click(object sender, RoutedEventArgs e)
        {
            var colorDialog = new ColorDialog();
            var currentColor = ((SolidColorBrush)ContentBackgroundPreview.Fill).Color;
            colorDialog.Color = System.Drawing.Color.FromArgb(currentColor.A, currentColor.R, currentColor.G, currentColor.B);
            
            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var newColor = Color.FromArgb(colorDialog.Color.A, colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B);
                ContentBackgroundPreview.Fill = new SolidColorBrush(newColor);
            }
        }
        
        private void TitleFontComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 预览更新
        }
        
        private void TitleAlignmentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 预览更新
        }
        
        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // 预览更新
            PreviewBorder.Opacity = OpacitySlider.Value;
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
            TitleAlignmentComboBox.SelectedIndex = 0; // 左对齐
            ContentBackgroundPreview.Fill = new SolidColorBrush(Colors.WhiteSmoke);
            OpacitySlider.Value = 0.95;
            IconSizeComboBox.SelectedIndex = 1; // 中等大小
        }
        
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // 应用设置到ViewModel
            _viewModel.TitleForeground = ((SolidColorBrush)TitleForegroundPreview.Fill).Clone();
            _viewModel.TitleBackground = ((SolidColorBrush)TitleBackgroundPreview.Fill).Clone();
            _viewModel.TitleFont = (FontFamily)TitleFontComboBox.SelectedItem;
            
            // 设置对齐方式
            switch (TitleAlignmentComboBox.SelectedIndex)
            {
                case 0:
                    _viewModel.TitleAlignment = HorizontalAlignment.Left;
                    break;
                case 1:
                    _viewModel.TitleAlignment = HorizontalAlignment.Center;
                    break;
                case 2:
                    _viewModel.TitleAlignment = HorizontalAlignment.Right;
                    break;
            }
            
            _viewModel.ContentBackground = ((SolidColorBrush)ContentBackgroundPreview.Fill).Clone();
            _viewModel.Opacity = OpacitySlider.Value;
            
            // 设置图标大小
            switch (IconSizeComboBox.SelectedIndex)
            {
                case 0:
                    _viewModel.IconSize = IconSize.Small;
                    break;
                case 1:
                    _viewModel.IconSize = IconSize.Medium;
                    break;
                case 2:
                    _viewModel.IconSize = IconSize.Large;
                    break;
            }
            
            // 如果是全局设置且选择了应用到所有窗口
            if (_isGlobalSettings && ApplyToAllCheckBox.IsChecked == true)
            {
                // 应用设置到所有分区窗口
                WindowManagerService.Instance.ApplySettingsToAllWindows(_viewModel);
            }
            
            // 保存设置到配置文件
            PartitionSettingsService.Instance.SaveSettings(_viewModel, _isGlobalSettings);
            
            DialogResult = true;
            Close();
        }
        
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // 恢复原始设置
            _viewModel.TitleForeground = _originalTitleForeground;
            _viewModel.TitleBackground = _originalTitleBackground;
            _viewModel.TitleFont = _originalTitleFont;
            _viewModel.TitleAlignment = _originalTitleAlignment;
            _viewModel.ContentBackground = _originalContentBackground;
            _viewModel.Opacity = _originalOpacity;
            _viewModel.IconSize = _originalIconSize;
            
            DialogResult = false;
            Close();
        }
    }
}