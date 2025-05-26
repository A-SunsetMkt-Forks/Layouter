using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PluginEntry;

namespace Layouter.Plugins
{
    public class PluginSecurityChecker
    {
        /// <summary>
        /// 危险API列表
        /// </summary>
        private readonly List<string> _forbiddenPatterns = new List<string>
        {
            @"System\.Diagnostics\.Process\.Start",
            @"System\.IO\.File\.Delete",
            @"System\.Net\.WebClient",
            @"System\.Reflection\.Assembly\.Load",
            @"Microsoft\.Win32\.Registry",
            @"System\.Runtime\.InteropServices",
            @"System\.Security\.AccessControl\.FileSystemAccessRule",
            @"Environment\.Exit",
            @"System\.Diagnostics\.EventLog",
            @"new\s+System\.Net\.Sockets\.TcpClient",
            @"System\.Windows\.Forms\.Application\.Exit"
        };

        /// <summary>
        /// 代码静态分析
        /// </summary>
        public bool CheckCode(string sourceCode)
        {
            foreach (var pattern in _forbiddenPatterns)
            {
                if (Regex.IsMatch(sourceCode, pattern))
                {
                    Console.WriteLine($"Security violation: {pattern} detected in plugin code");
                    return false;
                }
            }

            // 检查其他危险模式
            if (Regex.IsMatch(sourceCode, @"using\s+System\.Diagnostics"))
            {
                // 进一步检查有没有使用危险类
                if (Regex.IsMatch(sourceCode, @"Process\.|ProcessStartInfo"))
                {
                    Console.WriteLine("Security violation: Process manipulation detected");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 动态方法安全检查
        /// </summary>
        public bool CheckFunction(dynamic plugin, string functionName)
        {
            try
            {
                // 获取函数实例，假设为动态对象
                var dict = plugin.FunctionDict as Dictionary<string, Func<PluginParameter[], object>>;
                if (dict?.ContainsKey(functionName) != true)
                {
                    return false;
                }
                //Todo: 进一步的排查逻辑
                return true;
            }
            catch
            {
                return false;
            }
        }
    }


    public class SecurityException : Exception
    {
        public SecurityException(string message) : base(message) { }
    }
}
