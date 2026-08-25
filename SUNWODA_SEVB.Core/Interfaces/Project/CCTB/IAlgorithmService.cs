using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.ArithmeticModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Interfaces.Project.CCTB
{
    public interface IAlgorithmService
    {
        #region 初始化与校验

        /// <summary>
        /// 初始化并校验所有算法（项目启动时调用，失败可弹窗提示）
        /// </summary>
        /// <returns>isSuccess: 是否全部校验通过；message: 成功信息或错误详情</returns>
        Task<(bool isSuccess, string message)> InitializeAlgorithmVerityAsync();

        /// <summary>
        /// 供配置界面调用的单算法校验/刷新
        /// </summary>
        /// <param name="sideType">面类型</param>
        /// <param name="closedLoopType">闭环类型</param>
        /// <param name="algorithmName">算法名称</param>
        /// <returns>isSuccess: 是否校验通过；message: 结果信息</returns>
        Task<(bool isSuccess, string message)> VerifyAlgorithmAsync(ESideType sideType, EAlgorithmType algorithmType, string algorithmName);

        #endregion

        #region 业务调用接口（自动保证最新版本）

        /// <summary>
        /// 执行流量PID算法（自动保证为最新校验通过的版本）
        /// </summary>
        /// <param name="sideType">面类型</param>
        /// <param name="pumpSpeed1">泵速1</param>
        /// <param name="pumpSpeed2">泵速2</param>
        /// <param name="flowData">流量数据CSV字符串</param>
        /// <param name="ma">移动平均窗口</param>
        /// <param name="r">控制参数</param>
        /// <param name="allTrend">总趋势</param>
        /// <param name="trend">趋势</param>
        /// <param name="trendRule">趋势规则</param>
        /// <param name="single">单点控制</param>
        /// <param name="inputData1">扩展输入1</param>
        /// <param name="inputData2">扩展输入2</param>
        /// <returns>流量PID输出模型</returns>
        /// <exception cref="Exception">算法执行异常或版本更新失败</exception>
        FlowPIDOutputModel ExcuteFlowPID(ESideType sideType, double pumpSpeed1, double pumpSpeed2,
            string? flowData, int ma, float r, double allTrend, double trend, int trendRule, float single,
            object? inputData1 = null, object? inputData2 = null);

        /// <summary>
        /// 执行专家PID算法（自动保证为最新校验通过的版本）
        /// </summary>
        /// <param name="sideType">面类型</param>
        /// <param name="pid">PID参数模型</param>
        /// <param name="tarVal">目标值</param>
        /// <param name="curVal1">当前值1</param>
        /// <param name="curVal2">当前值2</param>
        /// <param name="curVal3">当前值3</param>
        /// <param name="pumpSpeed1">泵速1</param>
        /// <param name="pumpSpeed2">泵速2</param>
        /// <param name="flow1">流量1</param>
        /// <param name="flow2">流量2</param>
        /// <param name="fluDataDelta1">流量数据差值1</param>
        /// <param name="fluDataDelta2">流量数据差值2</param>
        /// <param name="jjA">增益参数</param>
        /// <param name="inputData">扩展输入</param>
        /// <returns>专家PID输出模型</returns>
        /// <exception cref="Exception">算法执行异常或版本更新失败</exception>
        ExpertPIDOutputModel ExcuteExpertPID(ESideType sideType, PIDModel pid, double tarVal,
            double curVal1, double curVal2, double curVal3, double pumpSpeed1, double pumpSpeed2,
            double flow1, double flow2, double fluDataDelta1, double fluDataDelta2, float jjA = 0,
            object? inputData = null);

        /// <summary>
        /// 执行TDControl算法（自动保证为最新校验通过的版本）
        /// </summary>
        /// <param name="sideType">面类型</param>
        /// <param name="data">输入数据CSV字符串</param>
        /// <returns>TDControl输出模型</returns>
        /// <exception cref="Exception">算法执行异常或版本更新失败</exception>
        TDControlOutputModel ExcuteTDControl(ESideType sideType, string data);

        /// <summary>
        /// 执行MDTDControl算法（自动保证为最新校验通过的版本）
        /// </summary>
        /// <param name="sideType">面类型</param>
        /// <param name="data">输入数据CSV字符串</param>
        /// <returns>MDTDControl输出模型</returns>
        /// <exception cref="Exception">算法执行异常或版本更新失败</exception>
        MDTDControlOutputModel ExcuteMDTDControl(ESideType sideType, string data);

        /// <summary>
        /// 执行MultiMDTDControl算法（自动保证为最新校验通过的版本）
        /// </summary>
        /// <param name="sideType">面类型</param>
        /// <param name="data">输入数据CSV字符串</param>
        /// <returns>MultiMDTDControl输出模型</returns>
        /// <exception cref="Exception">算法执行异常或版本更新失败</exception>
        MULTIMDTDControlOutputModel ExcuteMULTIMDTDControl(ESideType sideType, string data);

        #endregion
    }
}
