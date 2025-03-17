using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Layouter.Services
{
    public interface IDataService
    {
        Task<List<string>> GetDataAsync();
    }

}
