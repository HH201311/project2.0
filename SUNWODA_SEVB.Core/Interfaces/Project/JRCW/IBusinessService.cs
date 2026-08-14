using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SUNWODA_SEVB.Core.Enumerations.Project.JRCW;

namespace SUNWODA_SEVB.Core.Interfaces.Project.JRCW
{
    /// <summary>
    /// 卷绕极耳错位闭环项目的业务服务接口
    /// </summary>
    public interface IBusinessService
    {
        /// <summary>
        /// 业务类型标识
        /// </summary>
        EBusinessType BusinessType { get; }

        /// <summary>
        /// 业务名称
        /// </summary>
        string BusinessName { get; }

        /// <summary>
        /// 是否正在运行
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// 开始业务
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        Task StartAsync(CancellationToken token = default);

        /// <summary>
        /// 停止业务
        /// </summary>
        /// <returns></returns>
        Task StopAsync(CancellationToken token = default);

        /// <summary>
        /// 重启业务
        /// </summary>
        /// <returns></returns>
        Task RestartAsync(CancellationToken token = default);

        /// <summary>
        /// 显示业务参数配置对话框
        /// </summary>
        /// <returns></returns>
        Task ShowBusinessParamConfigDialogAsync();
    }
}