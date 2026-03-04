using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.Core.MessageModels
{
    /// <summary>
    /// 消息类
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class MessageEventArgs<T> : EventArgs where T : class
    {
        public T? Message { get; set; }
    }
}
