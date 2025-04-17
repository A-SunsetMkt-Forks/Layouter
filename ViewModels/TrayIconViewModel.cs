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
using Hardcodet.Wpf.TaskbarNotification;

namespace Layouter.ViewModels
{
    public class TrayIconViewModel : ObservableObject
    {
        private TaskbarIcon _trayIcon;

        public ICommand NewWindowCommand { get; }
        public ICommand WindowSettingCommand { get; }
        public ICommand ExitCommand { get; }

        public TrayIconViewModel()
        {
            NewWindowCommand = new RelayCommand(OpenNewWindow);
            WindowSettingCommand = new RelayCommand(SettingWindow);
            ExitCommand = new RelayCommand(Exit);
        }

        public void SetTrayIcon(TaskbarIcon trayIcon)
        {
            _trayIcon = trayIcon;

            // 初始化托盘菜单
            InitializeTrayMenu();
        }

        private void InitializeTrayMenu()
        {
            try
            {
                // 初始化托盘菜单管理器
                TrayMenuService.Instance.Initialize(this);

                if (_trayIcon != null)
                {
                    _trayIcon.ContextMenu = TrayMenuService.Instance.GetTrayMenu();
                }
            }
            catch (Exception ex)
            {
                Log.Information($"初始化托盘菜单时出错: {ex.Message}");
            }
        }

        private void OpenNewWindow()
        {
            try
            {
                // 创建新的分区窗口
                var window = new DesktopManagerWindow();
                PartitionDataService.Instance.ShowWindow(window);

                Task.Delay(100).Wait();
                // 在新窗口显示后让标题变为可编辑状态
                window.EnableTitleEditOnFirstLoad();

                Log.Information("已创建新分区窗口");
            }
            catch (Exception ex)
            {
                Log.Information($"创建新分区窗口时出错: {ex.Message}");
            }
        }

        private void SettingWindow()
        {
            var settingsWindow = new PartitionManagerWindow();
            settingsWindow.ShowDialog();
        }

        private void Exit()
        {
            Application.Current.Shutdown();
        }

    }
}
