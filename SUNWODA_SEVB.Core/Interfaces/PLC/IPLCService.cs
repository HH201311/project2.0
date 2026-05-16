using SUNWODA_SEVB.Core.Models.PLC;
using System.Collections.ObjectModel;

namespace SUNWODA_SEVB.Core.Interfaces.PLC
{
    public interface IPLCService
    {
        IReadOnlyDictionary<string, ConnectInfo> ConnectionStatus { get; }
        IReadOnlyDictionary<int, PLCRWAddress> RWAddresses { get; }
        IReadOnlyDictionary<int, PLCDevice> PLCs { get; }
        /// <summary>
        /// 初始化PLC
        /// </summary>
        Task<bool> InitializeAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 获取PLC对象
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        PLCDevice? GetPLCByName(string name);

        /// <summary>
        /// 写入地址信号
        /// </summary>
        /// <param name="plc"></param>
        /// <param name="address"></param>
        /// <returns></returns>
        Task WriteAddressAsync(PLCDevice plc, PLCRWAddress address);

        /// <summary>
        /// 确保指定 PLC 已连接（尝试重连）
        /// </summary>
        Task<bool> EnsurePLCConnectedAsync(PLCDevice plc, CancellationToken cancellationToken = default);

        /// <summary>
        /// 根据地址 ID 获取关联的 PLC ID
        /// </summary>
        int? GetPLCIdForAddress(int addressId);
    }
}
