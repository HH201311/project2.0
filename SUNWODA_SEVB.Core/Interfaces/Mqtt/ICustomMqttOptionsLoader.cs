using HslCommunication.MQTT;
using SUNWODA_SEVB.Core.Models.Mqtt;
using SUNWODA_SEVB.Core.Models.Socket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Interfaces.Mqtt
{
    /// <summary>
    /// MQTT 选项加载器接口
    /// </summary>
    public interface ICustomMqttOptionsLoader
    {
        /// <summary>
        /// 从数据库加载配置
        /// </summary>
        Task<CustomMqttClientOptions> LoadMqttClientOptionsAsync(string moduleName);

        /// <summary>
        /// 向数据库中保存配置
        /// </summary>
        /// <param name="moduleName"></param>
        /// <returns></returns>
        Task<bool> UpdateMqttClientOptionsAsync(string moduleName, CustomMqttClientOptions options);
    }
}
