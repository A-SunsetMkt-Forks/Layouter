using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Layouter.Services
{
    public class DataService : IDataService
    {
        public async Task<List<string>> GetDataAsync()
        {
            await Task.Delay(500);

            return new List<string>
            {
                "数据项1",
                "数据项2",
                "数据项3"
            };
        }
    }

}
