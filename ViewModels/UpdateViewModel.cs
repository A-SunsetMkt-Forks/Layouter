using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Layouter.Models;
using System.Text.Json;

namespace Layouter.ViewModels
{
    public partial class UpdateWindowViewModel : ObservableObject
    {
        private readonly HttpClient httpClient = new HttpClient();
        private readonly string appPath;
        private string tempFilePath;
        private string extractPath;
        private WebClient webClient;
        private int versionFlag = 0;


        public UpdateWindowViewModel(string currentVersion, string updateApiUrl, string releasePageUrl)
        {
            CurrentVersion = currentVersion;
            UpdateApiUrl = updateApiUrl;
            ReleasePageUrl = releasePageUrl;
            appPath = Process.GetCurrentProcess().MainModule.FileName;
        }

        [ObservableProperty]
        private string currentVersion;

        [ObservableProperty]
        private string updateApiUrl;

        [ObservableProperty]
        private string releasePageUrl;

        [ObservableProperty]
        private string statusMessage = "点击检测按钮获取版本信息";

        [ObservableProperty]
        private int downloadProgress;

        [ObservableProperty]
        private string progressMessage = "正在下载...";

        [ObservableProperty]
        private Visibility versionsVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private Visibility progressVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private ObservableCollection<ProductVersion> availableVersions = new ObservableCollection<ProductVersion>();

        [ObservableProperty]
        private ObservableCollection<OptionItem> optionItems = new ObservableCollection<OptionItem>()
        {
            new OptionItem { Name = "框架依赖版本",IsSelected= true },
            new OptionItem { Name = "独立安装版本",IsSelected= false }
        };

        public async Task CheckForUpdatesAsync()
        {
            StatusMessage = "正在检测新版本...";
            VersionsVisibility = Visibility.Collapsed;
            AvailableVersions.Clear();

            try
            {
                // 获取版本信息
                HttpResponseMessage response = await httpClient.GetAsync(UpdateApiUrl);
                if (response.IsSuccessStatusCode)
                {
                    string jsonContent = await response.Content.ReadAsStringAsync();
                    var latestVersion = JsonSerializer.Deserialize<ProductVersion>(jsonContent);

                    // 比较版本
                    Version currentVer = new Version(CurrentVersion);
                    Version newVer = new Version(latestVersion.LatestVersion);

                    if (newVer > currentVer)
                    {
                        AvailableVersions.Add(latestVersion);
                        StatusMessage = "发现新版本";
                        VersionsVisibility = Visibility.Visible;
                    }
                    else
                    {
                        StatusMessage = "您当前使用的已经是最新版本";
                    }
                }
                else
                {
                    StatusMessage = $"检测更新失败: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"检测更新失败: {ex.Message}";
            }
        }

        public void OpenReleasePage()
        {
            Process.Start(new ProcessStartInfo { FileName = ReleasePageUrl, UseShellExecute = true });
        }

        public void DownloadAndInstall(bool useFirstLink = true)
        {
            var versionInfo = AvailableVersions.FirstOrDefault();

            if (versionInfo == null)
            {
                return;
            }

            // 根据用户选择决定使用哪个下载链接
            string downloadUrl = useFirstLink
                ? versionInfo.DownloadLink1
                : versionInfo.DownloadLink2;

            versionFlag = useFirstLink ? 0 : 1;

            if (string.IsNullOrEmpty(downloadUrl))
            {
                StatusMessage = "下载链接无效";
                return;
            }

            ProgressVisibility = Visibility.Visible;
            DownloadProgress = 0;
            ProgressMessage = $"正在下载版本 {versionInfo.LatestVersion}...";

            // 创建临时目录
            string tempDir = Path.Combine(Path.GetTempPath(), "AppUpdater");
            Directory.CreateDirectory(tempDir);
            tempFilePath = Path.Combine(tempDir, $"update_{versionInfo.LatestVersion}.zip");
            extractPath = Path.Combine(tempDir, $"extract_{versionInfo.LatestVersion}");

            if (Directory.Exists(extractPath))
            {
                Directory.Delete(extractPath, true);
            }
            Directory.CreateDirectory(extractPath);

            // 下载更新包
            webClient = new WebClient();
            webClient.DownloadProgressChanged += WebClient_DownloadProgressChanged;
            webClient.DownloadFileCompleted += WebClient_DownloadFileCompleted;
            webClient.DownloadFileAsync(new Uri(downloadUrl), tempFilePath);
        }

        private void WebClient_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            DownloadProgress = e.ProgressPercentage;
        }

        private void WebClient_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                ProgressMessage = $"下载失败: {e.Error.Message}";
                return;
            }

            ProgressMessage = "下载完成，正在解压...";

            try
            {
                // 解压文件
                ZipFile.ExtractToDirectory(tempFilePath, extractPath);
                ProgressMessage = "解压完成，准备安装更新...";

                // 创建重启脚本
                string batchPath = Path.Combine(Path.GetTempPath(), "AppUpdater", "update.bat");
                string appDir = Path.GetDirectoryName(appPath);
                string appExe = Path.GetFileName(appPath);
                string extractAppFolder = $"{extractPath}\\{(versionFlag == 0? Env.FrameworkDependencyVersionFolderName : Env.IndependentVersionFolderName)}"; 

                using (StreamWriter writer = new StreamWriter(batchPath))
                {
                    writer.WriteLine("@echo off");
                    writer.WriteLine("timeout /t 2 /nobreak > NUL"); // 等待2秒确保应用已关闭
                    writer.WriteLine($"echo 正在更新应用...");
                    writer.WriteLine($"xcopy \"{extractAppFolder}\\*\" \"{appDir}\\\" /E /Y /I");
                    writer.WriteLine("if %ERRORLEVEL% NEQ 0 (");
                    writer.WriteLine("  echo 更新失败");
                    writer.WriteLine("  pause");
                    writer.WriteLine("  exit /b %ERRORLEVEL%");
                    writer.WriteLine(")");
                    writer.WriteLine($"echo 更新完成，正在启动应用...");
                    writer.WriteLine($"start \"\" \"{appDir}\\{appExe}\"");
                    writer.WriteLine("del /Q \"%~f0\""); // 自删除批处理文件
                    writer.WriteLine("pause"); // 自删除批处理文件
                }

                // 执行重启脚本
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{batchPath}\"",
                    WindowStyle = ProcessWindowStyle.Normal,
                    CreateNoWindow = true
                };

                Process.Start(psi);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                ProgressMessage = $"更新安装失败: {ex.Message}";
                Log.Error(ProgressMessage);
            }
        }
    }

}
