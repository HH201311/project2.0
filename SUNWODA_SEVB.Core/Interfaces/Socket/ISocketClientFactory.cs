using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Interfaces.Socket
{
    /// <summary>
    /// Socket 客户端工厂接口
    /// </summary>
    public interface ISocketClientFactory
    {
        ISocketClient CreateClient();
    }
}
