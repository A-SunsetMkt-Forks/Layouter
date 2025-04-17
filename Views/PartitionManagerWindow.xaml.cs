using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Layouter.Services;
using Layouter.ViewModels;

namespace Layouter.Views
{
    /// <summary>
    /// PartitionManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class PartitionManagerWindow : Window
    {
        private PartitionManagerViewModel vm;

        public PartitionManagerWindow()
        {
            InitializeComponent();

            vm = new PartitionManagerViewModel();
            DataContext = vm;

            // 加载分区数据
            vm.LoadPartitions();
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            vm.UpdateSelectedPartition();
        }

        private void EditPartition_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var partition = (PartitionItemViewModel)button.DataContext;

            vm.EditPartition(partition);
        }

        private void ToggleVisibility_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var partition = (PartitionItemViewModel)button.DataContext;

            vm.TogglePartitionVisibility(partition);
        }

        private void DeletePartition_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var partition = (PartitionItemViewModel)button.DataContext;

            if (MessageBox.Show($"确定要删除分区 \"{partition.Title}\" 吗？此操作不可恢复。", "确认删除",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                vm.DeletePartition(partition);
            }
        }

        //private void SelectAll_Click(object sender, RoutedEventArgs e)
        //{
        //    vm.SelectAll();
        //}

        //private void DeselectAll_Click(object sender, RoutedEventArgs e)
        //{
        //    vm.DeselectAll();
        //}

        //private void ShowSelected_Click(object sender, RoutedEventArgs e)
        //{
        //    vm.ShowSelected();
        //}

        //private void HideSelected_Click(object sender, RoutedEventArgs e)
        //{
        //    vm.HideSelected();
        //}

        private void ToggleSelectAll_Click(object sender, RoutedEventArgs e)
        {
            vm.ToggleSelectAll();
        }

        private void ToggleVisibilitySelected_Click(object sender, RoutedEventArgs e)
        {
            vm.ToggleVisibilitySelected();
        }

        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            int count = vm.Partitions.Count(p => p.IsSelected);

            if (count == 0)
            {
                MessageBox.Show("请先选择要删除的分区。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"确定要删除选中的 {count} 个分区吗？此操作不可恢复。", "确认删除",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                vm.DeleteSelected();
            }
        }

        private void EditGlobalStyle_Click(object sender, RoutedEventArgs e)
        {
            vm.EditGlobalStyle();
        }

    }
}
