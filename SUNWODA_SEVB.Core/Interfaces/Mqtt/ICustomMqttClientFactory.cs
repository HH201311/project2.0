using HslCommunication.MQTT;
using SUNWODA_SEVB.Core.Interfaces.Socket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Interfaces.Mqtt
{
    public interface ICustomMqttClientFactory
    {
        ICustomMqttClient CreateClient();
    }
}
