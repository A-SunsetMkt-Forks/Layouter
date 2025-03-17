using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Layouter.Services;
using Layouter.Views;
using Layouter.ViewModels;

namespace Layouter.ViewModels
{
    public class TrayIconViewModel : ObservableObject
    {
        public ICommand OpenFeature1Command { get; }
        public ICommand ExitCommand { get; }

        public TrayIconViewModel()
        {
            OpenFeature1Command = new RelayCommand(OpenFeature1);
            ExitCommand = new RelayCommand(Exit);
        }

        private void OpenFeature1()
        {
            // 创建一个新的分区窗口
            var window = new DesktopManagerWindow();
            
            PartitionDataService.Instance.LoadPartitionData(window, Guid.NewGuid().ToString());
            window.Show();

            // 重新排列窗口
            WindowManagerService.Instance.ArrangeWindows();
        }

        private void Exit()
        {
            Application.Current.Shutdown();
        }
    }
}
