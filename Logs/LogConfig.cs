using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Layouter.Utility;
using Serilog.Events;
using Serilog;

namespace Layouter.Logs
{
    public class LogConfig
    {
        public static void Init()
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!Directory.Exists(logPath))
            {
                Directory.CreateDirectory(logPath);
            }

            string logTemplate = "{NewLine}Date:{Timestamp:yyyy-MM-dd HH:mm:ss.fff}   LogLevel：{Level}{NewLine}{Message}{NewLine}" + new string('-', 50) + "{NewLine}";

            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()//记录相关上下文信息 
                .MinimumLevel.Debug()//最小记录级别
                                     //.MinimumLevel.Override("Microsoft", LogEventLevel.Information)//对其他日志进行重写
                .WriteTo.File($"{logPath}\\log_.log", restrictedToMinimumLevel: LogEventLevel.Information, rollingInterval: RollingInterval.Day)
                .Enrich.WithProperty("Group", "Default") //请设置LoggerView的LoggerGroupName分组名称和此处一致
                .CreateLogger();

        }

    }

}
