using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Layouter.Models
{
    /// <summary>
    /// 通用设置类
    /// </summary>
    public class GeneralSettings
    {
        /// <summary>
        /// 是否开机自启动
        /// </summary>
        public bool AutoStartEnabled { get; set; } = false;

        /// <summary>
        /// 是否启用全局样式
        /// </summary>
        public bool EnableGlobalStyle { get; set; } = false;

        /// <summary>
        /// 分区窗口显示状态字典
        /// </summary>
        public Dictionary<string, bool> PartitionVisibility { get; set; } = new Dictionary<string, bool>();
    }
}
