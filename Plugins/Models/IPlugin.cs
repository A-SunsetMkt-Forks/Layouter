using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Layouter.Plugins
{
    //public interface IPlugin
    //{
    //    string Name { get; }
    //    void Load();
    //    IReadOnlyDictionary<string, Action<object>> GetFunctions();
    //    void Run(string functionKey, object arg);
    //}

    public interface IPlugin
    {
        string Name { get; }
        void Load();
        IReadOnlyDictionary<string, Func<dynamic, object>> GetFunctions();
        object Run(string functionKey, dynamic parameters);

        /// <summary>
        /// 获取参数描述
        /// </summary>
        Dictionary<string, List<ParameterInfo>> GetParameterDescriptions();
    }


}
