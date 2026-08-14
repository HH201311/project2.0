using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Python.Runtime;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Interfaces.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces.Socket;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.ArithmeticModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
using SUNWODA_SEVB.ViewModels.Pages.CCTB;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Services.Project.CCTB
{
    /// <summary>
    /// CCTB项目算法服务类（DI注册为单例）
    /// </summary>
    public class AlgorithmService : IAlgorithmService
    {
        private readonly ILoggerService<AlgorithmService> _logger;
        private readonly IPLCService _plcService;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;
        private readonly ISocketServerManager _socketServerManager;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="plcService"></param>
        /// <param name="globalSettingRepository"></param>
        /// <param name="plcConfigRepository"></param>
        /// <param name="webApiClient"></param>
        /// <param name="projectSettingRepository"></param>
        /// <param name="moduleManager"></param>
        /// <param name="socketServerManager"></param>
        public AlgorithmService(
            ILoggerService<AlgorithmService> logger,
            IPLCService plcService,
            IGlobalSettingRepository globalSettingRepository,
            IPLCConfigRepository plcConfigRepository,
            IWebApiClient webApiClient,
            IProjectSettingRepository projectSettingRepository,
            IModuleManager moduleManager,
            ISocketServerManager socketServerManager
        )
        {
            _logger = logger;
            _plcService = plcService;
            _globalSettingRepository = globalSettingRepository;
            _plcConfigRepository = plcConfigRepository;
            _webApiClient = webApiClient;
            _projectSettingRepository = projectSettingRepository;
            _moduleManager = moduleManager;
            _socketServerManager = socketServerManager;
        }


        #region 配置参数
        private string? pythonPydRootPath;
        /// <summary>
        /// 并发控制锁，每个算法类型一个锁
        /// </summary>
        private readonly ConcurrentDictionary<(ESideType, EAlgorithmType), SemaphoreSlim> updateLocks = new();

        /// <summary>
        /// Python算法pyd文件根目录（由全局配置的Python安装目录衍生而来，路径为{Python安装目录}\..\Dlls\TBPyds）
        /// </summary>
        public string PythonPydRootPath => pythonPydRootPath ?? throw new InvalidOperationException("Python 环境未初始化，请先调用 InitializePythonConfig");

        /// <summary>
        /// 算法缓存字典-已进行算法验证
        /// </summary>
        public readonly ConcurrentDictionary<(ESideType, EAlgorithmType), AlgorithmCacheModel> AlgorithmCache = new();
        #endregion

        #region 初始化与校验
        /// <summary>
        /// 项目启动时全面校验所有启用的算法，失败则弹窗提示
        /// </summary>
        public async Task<(bool isSuccess, string message)> InitializeAlgorithmVerityAsync()
        {
            InitializePythonConfig();
            CleanAllPydFiles();

            // 加载所有启用的 WebUrl
            var webUrls = _projectSettingRepository.LoadAlgorithmWebUrlsProjectParameters().Where(u => u.IsEnable).ToList();
            if (webUrls.Count == 0)
            {
                var msg = "没有启用的算法服务地址，已退出算法校验！";
                _logger.Warn(msg);
                return (false, msg);
            }

            var verityResult = true;
            var verityMessage = new StringBuilder();
            var sideTypes = new[] { ESideType.A, ESideType.B };
            var closedLoopTypes = new[] { EClosedLoopType.MD, EClosedLoopType.TD, EClosedLoopType.MDTD };

            foreach (var sideType in sideTypes)
            {
                foreach (var closedLoopType in closedLoopTypes)
                {
                    var algoModel = _projectSettingRepository.LoadAlgorithmProjectParameters(closedLoopType, sideType);
                    if (!algoModel.IsEnable)
                    {
                        _logger.Info($"[{sideType}面{closedLoopType}算法未启用，已跳过验证！");
                        continue;
                    }

                    var algorithmType = AlgorithmModel.GetEAlgorithmType(closedLoopType, algoModel.AlgorithmName);
                    var (ok, msg) = await EnsureAlgorithmLatestAsync(sideType, algorithmType, algoModel.AlgorithmName, forceUpdate: true);
                    verityResult &= ok;
                    if (!ok) verityMessage.AppendLine($"[{sideType}面{algorithmType}算法验证失败：{msg}");

                    if (closedLoopType.Equals(EClosedLoopType.MD))
                    {
                        algorithmType = AlgorithmModel.GetEAlgorithmType(closedLoopType, algoModel.AlgorithmName, 1);
                        (ok, msg) = await EnsureAlgorithmLatestAsync(sideType, algorithmType, algoModel.AlgorithmName, forceUpdate: true);
                        verityResult &= ok;
                        if (!ok) verityMessage.AppendLine($"[{sideType}面{algorithmType}算法验证失败：{msg}");
                    }
                }
            }

            if (!verityResult)
            {
                var errMsg = $"算法验证存在异常：\r\n{verityMessage}";
                _logger.Error(errMsg);
                return (verityResult, errMsg);
            }

            _logger.Info("算法验证全部通过！");
            return (verityResult, "Success");
        }

        /// <summary>
        /// 供配置界面调用的单算法校验
        /// </summary>
        public async Task<(bool isSuccess, string message)> VerifyAlgorithmAsync(ESideType sideType, EAlgorithmType algorithmType, string algorithmName)
        {
            var (ok, msg) = await EnsureAlgorithmLatestAsync(sideType, algorithmType, algorithmName, forceUpdate: true);
            return (ok, msg);
        }

        private void InitializePythonConfig()
        {
            if (pythonPydRootPath != null) return;
            var init = _globalSettingRepository.LoadIsInitPythonEnvironmentGlobalParameters();
            if (!init) throw new Exception("未开启 Python 环境初始化开关！");
            var pythonDir = _globalSettingRepository.LoadPythonInstallDirGlobalParameters();
            if (string.IsNullOrEmpty(pythonDir) || !Directory.Exists(pythonDir))
                throw new Exception($"Python 安装目录无效：{pythonDir}");
            if (!PythonConfigHelper.IsInitConfig)
                throw new Exception("Python 环境未初始化！");

            // 这个搜索路径在 PythonConfigHelper 初始化时是默认添加的
            pythonPydRootPath = Path.Combine(pythonDir, "..", "Dlls");
        }

        /// <summary>
        /// 清空所有已存在的 pyd 文件（仅在启动校验时使用）
        /// </summary>
        private void CleanAllPydFiles()
        {
            if (!Directory.Exists(PythonPydRootPath)) return;

            foreach (var file in Directory.EnumerateFiles(PythonPydRootPath, "*.pyd", SearchOption.AllDirectories))
            {
                try { File.Delete(file); } catch { /* 忽略删除失败 */ }
            }
            // 删除空目录
            foreach (var dir in Directory.GetDirectories(PythonPydRootPath))
            {
                try { Directory.Delete(dir, true); } catch { /* 忽略 */ }
            }
        }
        #endregion

        #region 算法版本确保与更新（核心）
        /// <summary>
        /// 确保指定算法为最新版本（从 Web 下载并验证）
        /// forceUpdate: true 时会忽略缓存版本强制重新下载校验
        /// </summary>
        private async Task<(bool success, string message)> EnsureAlgorithmLatestAsync(ESideType sideType, EAlgorithmType algorithmType, string algorithmName, bool forceUpdate = false)
        {
            var key = (sideType, algorithmType);
            var semaphore = updateLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            try
            {
                AlgorithmCache.TryGetValue(key, out var cache);

                // 如果不强制更新，先尝试用缓存中记录的 WebUrl 快速检查版本
                if (!forceUpdate && cache != null && cache.WebUrl != null)
                {
                    var (verOk, verMsg, latestVersion) = await TryGetVersionAsync(cache.WebUrl, algorithmName);
                    if (verOk && latestVersion == cache.Version && !string.IsNullOrEmpty(cache.Version))
                    {
                        // 版本未变，无需更新
                        return (true, "版本已是最新");
                    }
                }

                // 需要下载更新：遍历所有启用的 WebUrl，直到成功
                var webUrls = _projectSettingRepository.LoadAlgorithmWebUrlsProjectParameters().Where(u => u.IsEnable).ToList();
                if (webUrls.Count == 0) return (false, "没有可用的算法服务地址");

                List<string> errors = new();
                foreach (var url in webUrls)
                {
                    var (dlOk, dlMsg) = await TryDownloadAndVerifyAsync(sideType, algorithmType, algorithmName, url);
                    if (dlOk) return (true, "更新成功");
                    errors.Add($"[{url.Name}]: {dlMsg}");
                }

                // 所有地址都失败
                return (false, string.Join("\r\n", errors));
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// 尝试从单个 WebUrl 获取版本、下载 pyd、验证并更新缓存
        /// </summary>
        private async Task<(bool success, string message)> TryDownloadAndVerifyAsync(ESideType sideType, EAlgorithmType algorithmType, string algorithmName, AlgorithmWebUrlModel webUrl)
        {
            if (!webUrl.IsEnable || string.IsNullOrEmpty(webUrl.BaseUrl))
                return (false, "WebUrl 未启用或地址为空");

            if (string.IsNullOrEmpty(algorithmName)) return (false, "算法名称为空");

            // 1. 获取版本
            var (verOk, verMsg, version) = await TryGetVersionAsync(webUrl, algorithmName);
            if (!verOk) return (false, verMsg);

            // 2. 下载加密 pyd 文件
            var xmlPublicKey = RasHelper.ToXmlPublicKey(AlgorithmModel.PublicKey);
            var signature = RasHelper.RSAEncrypt(xmlPublicKey, AlgorithmModel.AppKey);
            var fileResp = await _webApiClient.DownloadDllAsync(webUrl.BaseUrl, AlgorithmModel.AppKey, signature, algorithmName, AlgorithmModel.ExecuteType);
            if (fileResp?.DataStr is not byte[] fileBuffer || fileBuffer.Length == 0)
                return (false, $"下载 pyd 文件失败：{fileResp?.Message}");

            // 3. 保存到版本隔离目录
            var uniqueId = Guid.NewGuid().ToString("N");
            var modelPath = Path.Combine(PythonPydRootPath, "TBPyds", $"{algorithmType}_{sideType}", $"{version}_{uniqueId}");
            var pydFilePath = Path.Combine(modelPath, $"{algorithmName}.pyd");
            if (!Directory.Exists(modelPath)) Directory.CreateDirectory(modelPath);
            await File.WriteAllBytesAsync(pydFilePath, fileBuffer);

            // 4. 构造 Python 验证代码并执行
            var pydImportPath = $"TBPyds.{algorithmType}_{sideType}.{version}_{uniqueId}.{algorithmName}"; // 将路径转换为 Python 模块导入格式
            var pythonCode = GetAlgorithmCode(algorithmType, pydImportPath);

            var (verifyOk, verifyMsg) = AlgorithmVerify(algorithmType, algorithmName, pythonCode);
            if (!verifyOk)
            {
                // 验证失败，删除刚下载的文件
                try { File.Delete(pydFilePath); } catch { /* ignore */ }
                return (false, $"算法验证失败：{verifyMsg}");
            }

            // 5. 更新缓存
            var cache = new AlgorithmCacheModel
            {
                WebUrl = webUrl,
                Version = version,
                ModlePath = modelPath,
                AlgorithmName = algorithmName,
                PythonCode = pythonCode
            };

            AlgorithmCache.AddOrUpdate((sideType, algorithmType), cache, (_, _) => cache);
            _logger.Info($"算法 [{sideType}/{algorithmType}] 更新成功，版本 {version}，地址 {webUrl.BaseUrl}");
            return (true, "更新成功");
        }

        /// <summary>
        /// 获取远程算法版本
        /// </summary>
        private async Task<(bool success, string message, string version)> TryGetVersionAsync(AlgorithmWebUrlModel webUrl, string algoName)
        {
            try
            {
                if (string.IsNullOrEmpty(webUrl.BaseUrl)) return (false, "WebUrl 地址为空", string.Empty);

                var ver = await _webApiClient.GetFileVersionAsync(webUrl.BaseUrl, algoName);
                if (ver == null || !ver.Success || string.IsNullOrEmpty(ver.Version))
                    return (false, $"获取版本失败：{ver?.Message}", string.Empty);

                if (string.IsNullOrEmpty(ver.Version))
                    return (false, "获取版本失败：版本号为空", ver.Version);

                return (true, string.Empty, ver.Version);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, string.Empty);
            }
        }
        #endregion

        #region 算法执行部分
        #region 算法验证方法（每个算法类型一个方法，保持与原先一致的测试数据和逻辑，仅供校验使用）
        /// <summary>
        /// 涂布Python算法校验
        /// </summary>
        /// <param name="algorithmType"></param>
        /// <param name="algorithmName"></param>
        /// <param name="algorithmCode"></param>
        /// <param name="Message"></param>
        /// <returns></returns>
        private (bool success, string message) AlgorithmVerify(EAlgorithmType algorithmType, string algorithmName, string algorithmCode)
        {
            try
            {
                switch (algorithmType)
                {
                    case EAlgorithmType.流量PID:
                        ExcuteFlowPIDInline(algorithmName, algorithmCode); break;
                    case EAlgorithmType.专家PID:
                        ExcuteExpertPIDInline(algorithmName, algorithmCode); break;
                    case EAlgorithmType.TDControl:
                        ExcuteTDControlInline(algorithmName, algorithmCode); break;
                    case EAlgorithmType.MDTDControl:
                        ExcuteMDTDControlInline(algorithmName, algorithmCode); break;
                    case EAlgorithmType.MULTIMDTDControl:
                        ExcuteMULTIMDTDControlInline(algorithmName, algorithmCode); break;
                    default: return (false, "未实现的算法类型");
                }
                return (true, "Success");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// 执行流量PID算法（内部校验方法）
        /// </summary>
        /// <param name="algorithmName"></param>
        /// <param name="pythonCode"></param>
        /// <returns></returns>
        private FlowPIDOutputModel ExcuteFlowPIDInline(string algorithmName, string pythonCode)
        {
            var algoType = EAlgorithmType.流量PID;
            var data = "flu_top,flu_bottom,range,target,weight,pump_top,pump_bottom" +
                "\r\n134.6527778,127.5,1.2075,189.8,189.907,44.74,43.56" +
                "\r\n134.5972222,127.3888889,1.8302,189.8,189.952,44.74,43.56" +
                "\r\n134.5125,127.25,1.5172,189.8,189.945,44.74,43.56" +
                "\r\n134.65,127.3375,1.8941,189.8,189.946,44.74,43.56" +
                "\r\n134.6111111,127.3888889,1.5686,189.8,189.945,44.74,43.56" +
                "\r\n134.625,127.6388889,1.5039,189.8,189.96,44.74,43.56" +
                "\r\n134.6944444,127.6111111,1.5243,189.8,190,44.74,43.56" +
                "\r\n134.7638889,127.4444444,1.6989,189.8,190.012,44.74,43.56" +
                "\r\n134.7361111,127.3333333,1.5889,189.8,189.993,44.74,43.56";
            var input = new FlowPIDInputModel
            {
                PumpSpeed1 = 20,
                PumpSpeed2 = 21,
                FlowData = data,
                MA = 3,
                R = 3,
                AllTrend = 0.25,
                Trend = 0.14,
                TrendRule = 5,
                Single = 0,
                InputData1 = null,
                InputData2 = null
            };
            var output = RunPython(algoType, algorithmName, pythonCode, input, ExecuteFlowPIDWorker);
            return output;
        }

        /// <summary>
        /// 执行专家PID算法（内部校验方法）
        /// </summary>
        /// <param name="algorithmName"></param>
        /// <param name="pythonCode"></param>
        /// <returns></returns>
        private ExpertPIDOutputModel ExcuteExpertPIDInline(string algorithmName, string pythonCode)
        {
            var algoType = EAlgorithmType.专家PID;
            var pid = new PIDModel() { T = 0.05f, P = 0.005f, I = 0.001f, D = 0.001f, };
            var inout = new ExpertPIDInputModel
            {
                PID = pid,
                TargetValue = 301,
                CurrentValue1 = 302,
                CurrentValue2 = 303,
                CurrentValue3 = 304,
                PumpSpeed1 = 20,
                PumpSpeed2 = 21,
                FlowValue1 = 0,
                FlowValue2 = 0,
                FluDataDelta1 = 0,
                FluDataDelta2 = 0,
                JJA = 1,
                InputData = null
            };
            var output = RunPython(algoType, algorithmName, pythonCode, inout, ExecuteExpertPIDWorker);
            return output;
        }

        /// <summary>
        /// 执行涂布控制算法（内部校验方法）
        /// </summary>
        /// <param name="algorithmName"></param>
        /// <param name="pythonCode"></param>
        /// <returns></returns>
        private TDControlOutputModel ExcuteTDControlInline(string algorithmName, string pythonCode)
        {
            var algoType = EAlgorithmType.TDControl;
            var data = "时间,重量1,重量2,重量3,重量4,重量5,重量6,重量7,重量8,重量9,重量10,重量11,重量12,重量13,重量14,重量15,重量16,重量17,重量18,重量19,重量20,重量21,重量22,重量23,重量24,重量25," +
                      "T块调机模式,T块[1]-实时位置,T块[2]-实时位置,T块[3]-实时位置,T块[4]-实时位置,T块[5]-实时位置,T块[6]-实时位置,T块[7]-实时位置,T块[8]-实时位置,T块[9]-实时位置,T块[10]-实时位置,T块[11]-实时位置,T块[12]-实时位置,T块[13]-实时位置,T块[14]-实时位置,T块[15]-实时位置,T块[16]-实时位置,T块[17]-实时位置,T块[18]-实时位置,T块[19]-实时位置,T块[20]-实时位置,T块[21]-实时位置,T块[22]-实时位置,T块[23]-实时位置,T块[24]-实时位置,T块[25]-实时位置," +
                      "T块[1]-报警标识,T块[2]-报警标识,T块[3]-报警标识,T块[4]-报警标识,T块[5]-报警标识,T块[6]-报警标识,T块[7]-报警标识,T块[8]-报警标识,T块[9]-报警标识,T块[10]-报警标识,T块[11]-报警标识,T块[12]-报警标识,T块[13]-报警标识,T块[14]-报警标识,T块[15]-报警标识,T块[16]-报警标识,T块[17]-报警标识,T块[18]-报警标识,T块[19]-报警标识,T块[20]-报警标识,T块[21]-报警标识,T块[22]-报警标识,T块[23]-报警标识,T块[24]-报警标识,T块[25]-报警标识," +
                      "T块[1]-上限值,T块[2]-上限值,T块[3]-上限值,T块[4]-上限值,T块[5]-上限值,T块[6]-上限值,T块[7]-上限值,T块[8]-上限值,T块[9]-上限值,T块[10]-上限值,T块[11]-上限值,T块[12]-上限值,T块[13]-上限值,T块[14]-上限值,T块[15]-上限值,T块[16]-上限值,T块[17]-上限值,T块[18]-上限值,T块[19]-上限值,T块[20]-上限值,T块[21]-上限值,T块[22]-上限值,T块[23]-上限值,T块[24]-上限值,T块[25]-上限值," +
                      "T块[1]-下限值,T块[2]-下限值,T块[3]-下限值,T块[4]-下限值,T块[5]-下限值,T块[6]-下限值,T块[7]-下限值,T块[8]-下限值,T块[9]-下限值,T块[10]-下限值,T块[11]-下限值,T块[12]-下限值,T块[13]-下限值,T块[14]-下限值,T块[15]-下限值,T块[16]-下限值,T块[17]-下限值,T块[18]-下限值,T块[19]-下限值,T块[20]-下限值,T块[21]-下限值,T块[22]-下限值,T块[23]-下限值,T块[24]-下限值,T块[25]-下限值" +
                      "T块[1]-使能,T块[2]-使能,T块[3]-使能,T块[4]-使能,T块[5]-使能,T块[6]-使能,T块[7]-使能,T块[8]-使能,T块[9]-使能,T块[10]-使能,T块[11]-使能,T块[12]-使能,T块[13]-使能,T块[14]-使能,T块[15]-使能,T块[16]-使能,T块[17]-使能,T块[18]-使能,T块[19]-使能,T块[20]-使能,T块[21]-使能,T块[22]-使能,T块[23]-使能,T块[24]-使能,T块[25]-使能," +
                      "\r\n2024/8/30 11:42:45,0,698.21,696.532,699.996,698.877,700.078,700.268,700.713,701.509,702.343,703.169,0,0,0,698.128,700.343,701.539,701.2,702.276,700.519,702.51,701.585,700.009,699.294,0,1,0,1043,410,1317,1375,1028,1050,1197,857,620,980,0,0,10,1305,766,894,635,731,647,1154,955,1006,663,0,[],[],[20321,20323],[20423],[],[],[],[],[],[],[],[],[],[],[21523],[],[],[],[],[],[],[],[],[],[],0,476,637,455,427,440,394,434,431,319,347,0,17,0,315,0,323,67,167,178,288,213,351,183,0,395,1949,2000,1939,1952,1970,1920,1722,1961,1575,1652,285,270,353,1720,1425,1724,1572,1689,1595,1808,1729,1704,1539,360,1,1,1,1,1,1,1,1,1,1,1,0,0,1,1,1,1,1,1,1,1,1,1,1,1" +
                      "\r\n2024/8/30 11:42:39,0,699.175,698.676,699.841,700.053,699.338,700.084,699.493,699.942,700.469,702.459,0,0,0,700.396,701.892,701.807,701.425,701.602,700.231,701.254,699.73,699.429,700.291,0,1,0,1043,411,1317,1376,1028,1051,1197,857,621,980,0,0,10,1306,766,894,635,731,646,1154,954,1005,663,0,[],[],[20321,20323],[20423],[],[],[],[],[],[],[],[],[],[],[21523],[],[],[],[],[],[],[],[],[],[],0,476,637,455,427,440,394,434,431,319,347,0,17,0,315,0,323,67,167,178,288,213,351,183,0,395,1949,2000,1939,1952,1970,1920,1722,1961,1575,1652,285,270,353,1720,1425,1724,1572,1689,1595,1808,1729,1704,1539,360,1,1,1,1,1,1,1,1,1,1,1,0,0,1,1,1,1,1,1,1,1,1,1,1,1" +
                      "\r\n2024/8/30 11:42:33,0,699.916,699.621,701.308,700.997,699.481,700.283,699.291,698.261,698.997,702.221,0,0,0,701.887,703.129,703.393,702.023,701.251,699.295,700.013,699.236,699.38,700.257,0,1,0,1044,410,1317,1375,1028,1050,1198,857,620,979,0,0,10,1305,766,895,635,730,647,1155,954,1005,663,0,[],[],[20321,20323],[20423],[],[],[],[],[],[],[],[],[],[],[21523],[],[],[],[],[],[],[],[],[],[],0,476,637,455,427,440,394,434,431,319,347,0,17,0,315,0,323,67,167,178,288,213,351,183,0,395,1949,2000,1939,1952,1970,1920,1722,1961,1575,1652,285,270,353,1720,1425,1724,1572,1689,1595,1808,1729,1704,1539,360,1,1,1,1,1,1,1,1,1,1,1,0,0,1,1,1,1,1,1,1,1,1,1,1,1" +
                      "\r\n2024/8/30 11:42:27,0,698.986,698.259,701.678,701.498,701.218,700.962,700.641,699.055,699.687,701.491,0,0,0,700.329,702.149,704.384,703.922,703.133,700.749,700.195,699.521,699.23,699.51,0,1,0,1044,411,1317,1375,1028,1050,1197,857,620,979,0,0,9,1305,766,895,635,730,646,1155,954,1006,663,0,[],[],[20321,20323],[20423],[],[],[],[],[],[],[],[],[],[],[21523],[],[],[],[],[],[],[],[],[],[],0,476,637,455,427,440,394,434,431,319,347,0,17,0,315,0,323,67,167,178,288,213,351,183,0,395,1949,2000,1939,1952,1970,1920,1722,1961,1575,1652,285,270,353,1720,1425,1724,1572,1689,1595,1808,1729,1704,1539,360,1,1,1,1,1,1,1,1,1,1,1,0,0,1,1,1,1,1,1,1,1,1,1,1,1";
            var input = new TDControlInputModel { Data = data };
            var output = RunPython(algoType, algorithmName, pythonCode, input, ExecuteTDControlWorker);
            return output;
        }

        /// <summary>
        /// 执行MDTDControl算法（内部校验方法）
        /// </summary>
        /// <param name="algorithmName"></param>
        /// <param name="pythonCode"></param>
        /// <returns></returns>
        private MDTDControlOutputModel ExcuteMDTDControlInline(string algorithmName, string pythonCode)
        {
            var algoType = EAlgorithmType.MDTDControl;
            var data = "目标值,公差,泵速,T块调机模式,重量1,重量2,重量3,重量4,重量5,重量6,重量7,重量8,重量9,重量10,重量11,重量12,重量13,重量14,重量15,重量16,重量17,重量18,重量19,重量20,重量21,重量22,重量23,重量24,重量25," +
                    "T块[1]-实时位置,T块[2]-实时位置,T块[3]-实时位置,T块[4]-实时位置,T块[5]-实时位置,T块[6]-实时位置,T块[7]-实时位置,T块[8]-实时位置,T块[9]-实时位置,T块[10]-实时位置,T块[11]-实时位置,T块[12]-实时位置,T块[13]-实时位置,T块[14]-实时位置,T块[15]-实时位置,T块[16]-实时位置,T块[17]-实时位置,T块[18]-实时位置,T块[19]-实时位置,T块[20]-实时位置,T块[21]-实时位置,T块[22]-实时位置,T块[23]-实时位置,T块[24]-实时位置,T块[25]-实时位置," +
                    "T块[1]-上限值,T块[2]-上限值,T块[3]-上限值,T块[4]-上限值,T块[5]-上限值,T块[6]-上限值,T块[7]-上限值,T块[8]-上限值,T块[9]-上限值,T块[10]-上限值,T块[11]-上限值,T块[12]-上限值,T块[13]-上限值,T块[14]-上限值,T块[15]-上限值,T块[16]-上限值,T块[17]-上限值,T块[18]-上限值,T块[19]-上限值,T块[20]-上限值,T块[21]-上限值,T块[22]-上限值,T块[23]-上限值,T块[24]-上限值,T块[25]-上限值," +
                    "T块[1]-下限值,T块[2]-下限值,T块[3]-下限值,T块[4]-下限值,T块[5]-下限值,T块[6]-下限值,T块[7]-下限值,T块[8]-下限值,T块[9]-下限值,T块[10]-下限值,T块[11]-下限值,T块[12]-下限值,T块[13]-下限值,T块[14]-下限值,T块[15]-下限值,T块[16]-下限值,T块[17]-下限值,T块[18]-下限值,T块[19]-下限值,T块[20]-下限值,T块[21]-下限值,T块[22]-下限值,T块[23]-下限值,T块[24]-下限值,T块[25]-下限值," +
                    "T块[1]-使能,T块[2]-使能,T块[3]-使能,T块[4]-使能,T块[5]-使能,T块[6]-使能,T块[7]-使能,T块[8]-使能,T块[9]-使能,T块[10]-使能,T块[11]-使能,T块[12]-使能,T块[13]-使能,T块[14]-使能,T块[15]-使能,T块[16]-使能,T块[17]-使能,T块[18]-使能,T块[19]-使能,T块[20]-使能,T块[21]-使能,T块[22]-使能,T块[23]-使能,T块[24]-使能,T块[25]-使能," +
                    "T块[1]-报警标识,T块[2]-报警标识,T块[3]-报警标识,T块[4]-报警标识,T块[5]-报警标识,T块[6]-报警标识,T块[7]-报警标识,T块[8]-报警标识,T块[9]-报警标识,T块[10]-报警标识,T块[11]-报警标识,T块[12]-报警标识,T块[13]-报警标识,T块[14]-报警标识,T块[15]-报警标识,T块[16]-报警标识,T块[17]-报警标识,T块[18]-报警标识,T块[19]-报警标识,T块[20]-报警标识,T块[21]-报警标识,T块[22]-报警标识,T块[23]-报警标识,T块[24]-报警标识,T块[25]-报警标识" +
                    "\r\n376,4,20,1,0,372.931,375.365,376.064,375.772,375.416,375.935,377.227,376.711,375.169,375.272,0,0,0,376.119,375.559,375.275,376.282,376.695,376.563,376.057,376.645,376.553,376.916,0,0,101,573,1020,420,454,609,651,650,512,646,36,0,166,877,551,850,676,775,867,739,605,731,355,7,0,0,342,724,154,218,344,372,434,236,287,0,5,0,355,253,450,372,437,458,332,268,330,187,0,334,1586,1837,2000,1310,1441,1573,1516,1519,1397,1520,81,649,330,1523,1518,1903,1890,1914,1826,1712,2000,1605,1712,344,1,1,1,1,1,1,1,1,1,1,1,0,0,1,1,1,1,1,1,1,1,1,1,1,1,[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]" +
                    "\r\n376,4,20,1,0,373.62,375.78,375.642,375.357,375.676,376.181,376.869,375.974,375.918,374.743,0,0,0,375.286,376.079,375.497,375.793,376.076,376.502,376.333,376.267,375.871,376.985,0,0,101,572,1020,420,455,609,651,649,511,646,37,0,166,877,551,849,676,775,867,739,605,731,355,6,0,0,342,724,154,218,344,372,434,236,287,0,5,0,355,253,450,372,437,458,332,268,330,187,0,334,1586,1837,2000,1310,1441,1573,1516,1519,1397,1520,81,649,330,1523,1518,1903,1890,1914,1826,1712,2000,1605,1712,344,1,1,1,1,1,1,1,1,1,1,1,0,0,1,1,1,1,1,1,1,1,1,1,1,1,[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]" +
                    "\r\n376,4,20,1,0,373.595,376.059,376.213,376.075,376.251,376.684,376.441,375.91,376.124,374.494,0,0,0,374.945,376.55,376.141,376.35,376.07,376.843,376.387,376.409,376.366,376.411,0,0,101,572,1021,420,454,610,650,649,512,647,37,0,166,876,551,850,675,774,867,739,605,732,355,6,0,0,342,724,154,218,344,372,434,236,287,0,5,0,355,253,450,372,437,458,332,268,330,187,0,334,1586,1837,2000,1310,1441,1573,1516,1519,1397,1520,81,649,330,1523,1518,1903,1890,1914,1826,1712,2000,1605,1712,344,1,1,1,1,1,1,1,1,1,1,1,0,0,1,1,1,1,1,1,1,1,1,1,1,1,[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]" +
                    "\r\n376,4,20,1,0,373.536,376.394,376.349,376.873,376.368,375.928,376.049,376.751,376.276,374.462,0,0,0,375.898,376.607,375.686,376.299,375.776,375.785,376.377,376.593,376.971,376.488,0,0,101,572,1020,420,455,610,650,649,511,647,37,0,166,877,551,849,676,774,867,739,605,732,356,6,0,0,342,724,154,218,344,372,434,236,287,0,5,0,355,253,450,372,437,458,332,268,330,187,0,334,1586,1837,2000,1310,1441,1573,1516,1519,1397,1520,81,649,330,1523,1518,1903,1890,1914,1826,1712,2000,1605,1712,344,1,1,1,1,1,1,1,1,1,1,1,0,0,1,1,1,1,1,1,1,1,1,1,1,1,[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]" +
                    "\r\n376,4,20,1,0,373.5,376.837,376.484,376.809,375.256,375.005,375.557,376.414,376.352,374.017,0,0,0,376.253,376.72,375.539,375.767,375.304,376.049,376.739,377.103,375.962,376.091,0,0,101,573,1020,421,455,609,650,649,512,646,37,0,166,876,551,849,675,775,867,740,605,731,355,7,0,0,342,724,154,218,344,372,434,236,287,0,5,0,355,253,450,372,437,458,332,268,330,187,0,334,1586,1837,2000,1310,1441,1573,1516,1519,1397,1520,81,649,330,1523,1518,1903,1890,1914,1826,1712,2000,1605,1712,344,1,1,1,1,1,1,1,1,1,1,1,0,0,1,1,1,1,1,1,1,1,1,1,1,1,[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]" +
                    "\r\n376,4,20,1,0,372.691,376.577,377.5,376.134,375.958,375.508,375.66,376.352,376.051,373.897,0,0,0,376.002,377.36,376.363,376.073,376.087,376.747,376.371,376.631,376.817,376.768,0,0,101,572,1021,420,455,609,650,650,512,646,36,0,166,876,550,850,676,774,867,739,606,731,355,7,0,0,342,724,154,218,344,372,434,236,287,0,5,0,355,253,450,372,437,458,332,268,330,187,0,334,1586,1837,2000,1310,1441,1573,1516,1519,1397,1520,81,649,330,1523,1518,1903,1890,1914,1826,1712,2000,1605,1712,344,1,1,1,1,1,1,1,1,1,1,1,0,0,1,1,1,1,1,1,1,1,1,1,1,1,[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]" +
                    "\r\n376,4,20,1,0,373.371,376.66,376.119,375.512,376.293,375.612,375.551,376.816,375.345,374.293,0,0,0,375.976,377.538,376.571,376.607,375.978,376.182,376.44,375.637,377.549,376.862,0,0,101,573,1019,419,451,609,650,649,511,646,35,0,166,875,550,849,677,775,869,743,608,734,359,7,0,0,342,724,154,218,344,372,434,236,287,0,5,0,355,253,450,372,437,458,332,268,330,187,0,334,1586,1837,2000,1310,1441,1573,1516,1519,1397,1520,81,649,330,1523,1518,1903,1890,1914,1826,1712,2000,1605,1712,344,1,1,1,1,1,1,1,1,1,1,1,0,0,1,1,1,1,1,1,1,1,1,1,1,1,[],[20221],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]";
            var input = new MDTDControlInputModel { Data = data };
            var output = RunPython(algoType, algorithmName, pythonCode, input, ExecuteMDTDControlWorker);
            return output;
        }

        /// <summary>
        /// 执行MULTIMDTDControl算法（内部校验方法）
        /// </summary>
        /// <param name="algorithmName"></param>
        /// <param name="pythonCode"></param>
        /// <returns></returns>
        private MULTIMDTDControlOutputModel ExcuteMULTIMDTDControlInline(string algorithmName, string pythonCode)
        {
            var algoType = EAlgorithmType.MULTIMDTDControl;
            var data = "时间,趟数,目标值,公差,泵速0,浆料泵启动标识0,泵速1,浆料泵启动标识1,泵速2,浆料泵启动标识2,泵速3,浆料泵启动标识3,T块调机模式,重量1,重量2,重量3,重量4,重量5,重量6,重量7,重量8,重量9,重量10,重量11,重量12,重量13,重量14,重量15,重量16,重量17,重量18,重量19,重量20,重量21,重量22,重量23,重量24,重量25,重量26,重量27,重量28,重量29,重量30,重量31,重量32,重量33,重量34,重量35,重量36,重量37,重量38,重量39,重量40,重量41,重量42," +
                      "T块[1]-实时位置,T块[2]-实时位置,T块[3]-实时位置,T块[4]-实时位置,T块[5]-实时位置,T块[6]-实时位置,T块[7]-实时位置,T块[8]-实时位置,T块[9]-实时位置,T块[10]-实时位置,T块[11]-实时位置,T块[12]-实时位置,T块[13]-实时位置,T块[14]-实时位置,T块[15]-实时位置,T块[16]-实时位置,T块[17]-实时位置,T块[18]-实时位置,T块[19]-实时位置,T块[20]-实时位置,T块[21]-实时位置,T块[22]-实时位置,T块[23]-实时位置,T块[24]-实时位置,T块[25]-实时位置,T块[26]-实时位置,T块[27]-实时位置,T块[28]-实时位置,T块[29]-实时位置,T块[30]-实时位置,T块[31]-实时位置,T块[32]-实时位置,T块[33]-实时位置,T块[34]-实时位置,T块[35]-实时位置,T块[36]-实时位置,T块[37]-实时位置,T块[38]-实时位置,T块[39]-实时位置,T块[40]-实时位置,T块[41]-实时位置,T块[42]-实时位置,T块[43]-实时位置,T块[44]-实时位置,T块[45]-实时位置,T块[46]-实时位置,T块[47]-实时位置,T块[48]-实时位置,T块[49]-实时位置,T块[50]-实时位置,T块[51]-实时位置,T块[52]-实时位置,T块[53]-实时位置,T块[54]-实时位置," +
                      "T块[1]-上限值,T块[2]-上限值,T块[3]-上限值,T块[4]-上限值,T块[5]-上限值,T块[6]-上限值,T块[7]-上限值,T块[8]-上限值,T块[9]-上限值,T块[10]-上限值,T块[11]-上限值,T块[12]-上限值,T块[13]-上限值,T块[14]-上限值,T块[15]-上限值,T块[16]-上限值,T块[17]-上限值,T块[18]-上限值,T块[19]-上限值,T块[20]-上限值,T块[21]-上限值,T块[22]-上限值,T块[23]-上限值,T块[24]-上限值,T块[25]-上限值,T块[26]-上限值,T块[27]-上限值,T块[28]-上限值,T块[29]-上限值,T块[30]-上限值,T块[31]-上限值,T块[32]-上限值,T块[33]-上限值,T块[34]-上限值,T块[35]-上限值,T块[36]-上限值,T块[37]-上限值,T块[38]-上限值,T块[39]-上限值,T块[40]-上限值,T块[41]-上限值,T块[42]-上限值,T块[43]-上限值,T块[44]-上限值,T块[45]-上限值,T块[46]-上限值,T块[47]-上限值,T块[48]-上限值,T块[49]-上限值,T块[50]-上限值,T块[51]-上限值,T块[52]-上限值," +
                      "T块[1]-下限值,T块[2]-下限值,T块[3]-下限值,T块[4]-下限值,T块[5]-下限值,T块[6]-下限值,T块[7]-下限值,T块[8]-下限值,T块[9]-下限值,T块[10]-下限值,T块[11]-下限值,T块[12]-下限值,T块[13]-下限值,T块[14]-下限值,T块[15]-下限值,T块[16]-下限值,T块[17]-下限值,T块[18]-下限值,T块[19]-下限值,T块[20]-下限值,T块[21]-下限值,T块[22]-下限值,T块[23]-下限值,T块[24]-下限值,T块[25]-下限值,T块[26]-下限值,T块[27]-下限值,T块[28]-下限值,T块[29]-下限值,T块[30]-下限值,T块[31]-下限值,T块[32]-下限值,T块[33]-下限值,T块[34]-下限值,T块[35]-下限值,T块[36]-下限值,T块[37]-下限值,T块[38]-下限值,T块[39]-下限值,T块[40]-下限值,T块[41]-下限值,T块[42]-下限值,T块[43]-下限值,T块[44]-下限值,T块[45]-下限值,T块[46]-下限值,T块[47]-下限值,T块[48]-下限值,T块[49]-下限值,T块[50]-下限值,T块[51]-下限值,T块[52]-下限值," +
                      "T块[1]-使能,T块[2]-使能,T块[3]-使能,T块[4]-使能,T块[5]-使能,T块[6]-使能,T块[7]-使能,T块[8]-使能,T块[9]-使能,T块[10]-使能,T块[11]-使能,T块[12]-使能,T块[13]-使能,T块[14]-使能,T块[15]-使能,T块[16]-使能,T块[17]-使能,T块[18]-使能,T块[19]-使能,T块[20]-使能,T块[21]-使能,T块[22]-使能,T块[23]-使能,T块[24]-使能,T块[25]-使能,T块[26]-使能,T块[27]-使能,T块[28]-使能,T块[29]-使能,T块[30]-使能,T块[31]-使能,T块[32]-使能,T块[33]-使能,T块[34]-使能,T块[35]-使能,T块[36]-使能,T块[37]-使能,T块[38]-使能,T块[39]-使能,T块[40]-使能,T块[41]-使能,T块[42]-使能,T块[43]-使能,T块[44]-使能,T块[45]-使能,T块[46]-使能,T块[47]-使能,T块[48]-使能,T块[49]-使能,T块[50]-使能,T块[51]-使能,T块[52]-使能," +
                      "T块[1]-报警标识,T块[2]-报警标识,T块[3]-报警标识,T块[4]-报警标识,T块[5]-报警标识,T块[6]-报警标识,T块[7]-报警标识,T块[8]-报警标识,T块[9]-报警标识,T块[10]-报警标识,T块[11]-报警标识,T块[12]-报警标识,T块[13]-报警标识,T块[14]-报警标识,T块[15]-报警标识,T块[16]-报警标识,T块[17]-报警标识,T块[18]-报警标识,T块[19]-报警标识,T块[20]-报警标识,T块[21]-报警标识,T块[22]-报警标识,T块[23]-报警标识,T块[24]-报警标识,T块[25]-报警标识,T块[26]-报警标识,T块[27]-报警标识,T块[28]-报警标识,T块[29]-报警标识,T块[30]-报警标识,T块[31]-报警标识,T块[32]-报警标识,T块[33]-报警标识,T块[34]-报警标识,T块[35]-报警标识,T块[36]-报警标识,T块[37]-报警标识,T块[38]-报警标识,T块[39]-报警标识,T块[40]-报警标识,T块[41]-报警标识,T块[42]-报警标识,T块[43]-报警标识,T块[44]-报警标识,T块[45]-报警标识,T块[46]-报警标识,T块[47]-报警标识,T块[48]-报警标识,T块[49]-报警标识,T块[50]-报警标识,T块[51]-报警标识,T块[52]-报警标识" +
                      "\r\n2025-06-18 01:31:49,6332,354.3,3.6,1,46.41,1,46.42,-1,46.43,0,46.44,4,0,0,0,350.7343,353.7544,354.5932,354.2084,354.5324,354.4376,0,0,0,0,354.6538,355.6228,354.5168,353.6692,353.79,353.6928,0,0,0,0,354.9013,354.9472,354.848,354.5796,354.0516,353.5324,0,0,0,0,354.96,354.2944,354.3932,354.812,353.9532,354.4596,0,0,0,1,2,0,3,670,124,443,282,406,0,0,0,1,325,405,314,385,337,347,0,-1,1,-38,334,330,347,376,273,152,4,0,0,1,-38,334,330,347,376,273,0,4,0,0,-38,334,330,347,376,273,152,0,4,0,0,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1,1,1,1,1,1,1,1,1,0,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]" +
                      "\r\n2025-06-18 01:31:57,6333,354.3,3.6,1,46.41,1,46.42,-1,46.43,0,46.44,4,0,0,0,350.4621,353.736,353.85,354.698,354.2276,354.1116,0,0,0,0,353.579,355.6964,355.7024,355.2364,355.1216,353.6884,0,0,0,0,353.7822,353.6564,353.0104,353.7224,354.0644,355.0432,0,0,0,0,354.355,353.6084,353.8252,354.126,354.0184,353.5716,0,0,0,1,2,0,3,669,124,443,282,406,0,0,0,2,325,405,314,385,337,347,1,-1,1,-38,334,330,347,376,274,152,4,0,0,1,-38,334,330,347,376,274,0,4,0,0,-38,334,330,347,376,274,152,0,4,0,0,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1,1,1,1,1,1,1,1,1,0,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]" +
                      "\r\n2025-06-18 01:32:06,6334,354.3,3.6,1,46.41,1,46.42,-1,46.43,0,46.44,4,0,0,0,350.8329,353.9324,354.5896,354.7308,354.178,354.142,0,0,0,0,353.5827,355.716,356.0712,356.3712,355.9844,354.4828,0,0,0,0,355.856,353.4808,353.9108,354.2944,355.754,355.1692,0,0,0,0,354.1324,353.4464,354.5084,354.418,353.802,354.1216,0,0,0,1,2,0,3,669,124,443,282,406,0,0,0,1,325,405,313,385,337,347,0,-1,1,-38,334,330,347,376,273,152,4,0,0,1,-38,334,330,347,376,273,0,4,0,0,-38,334,330,347,376,273,152,0,4,0,0,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1,1,1,1,1,1,1,1,1,0,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]" +
                      "\r\n2025-06-18 01:32:14,6335,354.3,3.6,1,46.41,1,46.42,-1,46.43,0,46.44,4,0,0,0,351.1487,354.2356,354.0708,353.824,354.8016,354.3,0,0,0,0,354.5391,355.612,354.8264,353.64,354.2436,354.2752,0,0,0,0,355.1367,355.1212,354.898,353.3852,353.7072,353.1448,0,0,0,0,354.7809,354.1616,353.1596,354.5188,353.808,354.3248,0,0,0,1,2,0,3,670,124,443,282,406,0,0,0,1,325,405,313,385,337,347,0,-1,1,-38,334,330,347,376,273,152,4,0,0,1,-38,334,330,347,376,273,0,4,0,0,-38,334,330,347,376,273,152,0,4,0,0,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1,1,1,1,1,1,1,1,1,0,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]" +
                      "\r\n2025-06-18 01:32:28,6336,354.3,3.6,1,46.41,1,46.42,-1,46.43,0,46.44,4,0,0,0,350.9093,353.6724,354.302,354.7648,354.4648,354.6548,0,0,0,0,354.8977,356.2012,355.2144,353.6104,353.6292,353.7696,0,0,0,0,355.4005,355.2408,354.2768,353.6236,352.906,353.0524,0,0,0,0,354.215,354.6808,354.1468,354.2208,353.6828,354.0812,0,0,0,1,2,0,3,670,124,443,282,406,0,0,0,1,325,405,314,385,337,347,0,-1,1,-38,334,330,347,376,273,152,4,0,0,1,-38,334,330,347,376,273,0,4,0,0,-38,334,330,347,376,273,152,0,4,0,0,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1,1,1,1,1,1,1,1,1,0,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]" +
                      "\r\n2025-06-18 01:32:44,6337,354.3,3.6,1,46.41,1,46.42,-1,46.43,0,46.44,4,0,0,0,350.4521,353.7188,354.298,354.508,354.8096,354.3292,0,0,0,0,353.479,355.7556,355.5136,355.5592,355.4652,354.0384,0,0,0,0,353.9165,353.8592,354.3468,354.5372,354.5076,354.1112,0,0,0,0,354.009,354.406,353.4832,355.092,354.2224,354.3856,0,0,0,1,2,0,6,669,124,444,281,406,0,0,0,1,325,407,314,385,337,348,1,-1,1,-38,334,330,347,376,273,152,4,0,0,1,-38,334,330,347,376,273,0,4,0,0,-38,334,330,347,376,273,152,0,4,0,0,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1,1,1,1,1,1,1,1,1,0,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]" +
                      "\r\n2025-06-18 01:32:53,6338,354.3,3.6,1,46.41,1,46.42,-1,46.43,0,46.44,4,0,0,0,349.9886,354.0836,354.3388,354.2464,354.28,353.958,0,0,0,0,352.8443,355.1572,354.8992,355.1584,355.3448,354.0664,0,0,0,0,353.7,352.9328,353.4172,353.1792,354.4396,355.5012,0,0,0,0,353.2936,354.3244,353.5924,354.9792,354.6996,354.9212,0,0,0,1,2,0,7,667,123,444,281,406,0,0,0,1,325,406,314,385,336,348,0,-1,1,-38,334,330,347,376,273,152,4,0,0,1,-38,334,330,347,376,273,0,4,0,0,-38,334,330,347,376,273,152,0,4,0,0,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,-300,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1100,1,1,1,1,1,1,1,1,1,0,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,[],[20221],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],[]";
            var input = new MULTIMDTDControlInputModel { Data = data };
            var output = RunPython(algoType, algorithmName, pythonCode, input, ExecuteMULTIMDTDControlWorker);
            return output;
        }
        #endregion

        #region 业务调用接口（每次调用自动检查并确保最新版本）
        /// <summary>
        /// 执行流量PID算法
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="pumpSpeed1"></param>
        /// <param name="pumpSpeed2"></param>
        /// <param name="flowData"></param>
        /// <param name="ma"></param>
        /// <param name="r"></param>
        /// <param name="allTrend"></param>
        /// <param name="trend"></param>
        /// <param name="trendRule"></param>
        /// <param name="single"></param>
        /// <param name="inputData1"></param>
        /// <param name="inputData2"></param>
        /// <returns></returns>
        public FlowPIDOutputModel ExcuteFlowPID(ESideType sideType, double pumpSpeed1, double pumpSpeed2,
            string? flowData, int ma, float r, double allTrend, double trend, int trendRule, float single,
            object? inputData1 = null, object? inputData2 = null)
        {
            var algoType = EAlgorithmType.流量PID;
            var cache = EnsureLatestSync(sideType, algoType);
            var input = new FlowPIDInputModel
            {
                PumpSpeed1 = pumpSpeed1,
                PumpSpeed2 = pumpSpeed2,
                FlowData = flowData,
                MA = ma,
                R = r,
                AllTrend = allTrend,
                Trend = trend,
                TrendRule = trendRule,
                Single = single,
                InputData1 = inputData1,
                InputData2 = inputData2
            };
            var output = RunPython(algoType, cache.AlgorithmName, cache.PythonCode, input, ExecuteFlowPIDWorker);
            return output;
        }

        /// <summary>
        /// 执行专家PID算法
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="pid"></param>
        /// <param name="tarVal"></param>
        /// <param name="curVal1"></param>
        /// <param name="curVal2"></param>
        /// <param name="curVal3"></param>
        /// <param name="pumpSpeed1"></param>
        /// <param name="pumpSpeed2"></param>
        /// <param name="flow1"></param>
        /// <param name="flow2"></param>
        /// <param name="fluDataDelta1"></param>
        /// <param name="fluDataDelta2"></param>
        /// <param name="jjA"></param>
        /// <param name="inputData"></param>
        /// <returns></returns>
        public ExpertPIDOutputModel ExcuteExpertPID(ESideType sideType, PIDModel pid, double tarVal,
            double curVal1, double curVal2, double curVal3, double pumpSpeed1, double pumpSpeed2,
            double flow1, double flow2, double fluDataDelta1, double fluDataDelta2, float jjA = 0,
            object? inputData = null)
        {
            var algoType = EAlgorithmType.专家PID;
            var cache = EnsureLatestSync(sideType, algoType);
            var inout = new ExpertPIDInputModel
            {
                PID = pid,
                TargetValue = tarVal,
                CurrentValue1 = curVal1,
                CurrentValue2 = curVal2,
                CurrentValue3 = curVal3,
                PumpSpeed1 = pumpSpeed1,
                PumpSpeed2 = pumpSpeed2,
                FlowValue1 = flow1,
                FlowValue2 = flow2,
                FluDataDelta1 = fluDataDelta1,
                FluDataDelta2 = fluDataDelta2,
                JJA = jjA,
                InputData = inputData
            };
            var output = RunPython(algoType, cache.AlgorithmName, cache.PythonCode, inout, ExecuteExpertPIDWorker);
            return output;
        }

        /// <summary>
        /// 执行TDControl算法
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public TDControlOutputModel ExcuteTDControl(ESideType sideType, string data)
        {
            var algoType = EAlgorithmType.TDControl;
            var cache = EnsureLatestSync(sideType, algoType);
            var input = new TDControlInputModel { Data = data };
            var output = RunPython(algoType, cache.AlgorithmName, cache.PythonCode, input, ExecuteTDControlWorker);
            return output;
        }

        /// <summary>
        /// 执行MDTDControl算法
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public MDTDControlOutputModel ExcuteMDTDControl(ESideType sideType, string data)
        {
            var algoType = EAlgorithmType.MDTDControl;
            var cache = EnsureLatestSync(sideType, algoType);
            var input = new MDTDControlInputModel { Data = data };
            var output = RunPython(algoType, cache.AlgorithmName, cache.PythonCode, input, ExecuteMDTDControlWorker);
            return output;
        }

        /// <summary>
        /// 执行MultiMDTDControl算法
        /// </summary>
        /// <param name="sideType"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public MULTIMDTDControlOutputModel ExcuteMULTIMDTDControl(ESideType sideType, string data)
        {
            var algoType = EAlgorithmType.MULTIMDTDControl;
            var cache = EnsureLatestSync(sideType, algoType);
            var input = new MULTIMDTDControlInputModel { Data = data };
            var output = RunPython(algoType, cache.AlgorithmName, cache.PythonCode, input, ExecuteMULTIMDTDControlWorker);
            return output;
        }
        #endregion

        #region 内部同步辅助
        private AlgorithmCacheModel EnsureLatestSync(ESideType sideType, EAlgorithmType algoType)
        {
            var algoModel = _projectSettingRepository.LoadAlgorithmProjectParameters(GetClosedLoopType(algoType), sideType);
            if (algoModel == null) throw new InvalidOperationException($"找不到算法配置 {sideType}/{algoType}");

            // 同步等待异步更新
            var task = EnsureAlgorithmLatestAsync(sideType, algoType, algoModel.AlgorithmName, forceUpdate: false);
            task.Wait(); // 调用线程应为后台线程，不会死锁
            if (!task.Result.success) throw new Exception($"算法 [{sideType}/{algoType}] 更新失败：{task.Result.message}");

            if (!AlgorithmCache.TryGetValue((sideType, algoType), out var cache)) throw new Exception($"算法缓存丢失 [{sideType}/{algoType}]");
            return cache;
        }

        private TOut RunPython<TIn, TOut>(EAlgorithmType algoType, string algorithmName, string pythonCode, TIn input, Func<TIn, string, string, TOut> workerFunc)
        {
            var sw = Stopwatch.StartNew();
            using var mr = new ManualResetEvent(false);
            PyResultModel<TIn, TOut> pyResult = new() { PyInPut = input };
            var args = new object[] { mr, algorithmName, pythonCode, pyResult, workerFunc };
            PythonConfigHelper.AddAction((ExecutePythonWrapper<TIn, TOut>, args));
            mr.WaitOne();
            sw.Stop();

            if (pyResult.IsSuccess)
            {
                _logger.Info($"'{algoType}算法 [{algorithmName}]' 执行完毕 [耗时:{sw.ElapsedMilliseconds}ms]");
                return pyResult.Result ?? Activator.CreateInstance<TOut>();
            }

            throw new Exception(
                $"'{algoType}算法 [{algorithmName}]' 执行异常 [耗时:{sw.ElapsedMilliseconds}ms]: [{pyResult.Message}]\r\n" +
                $"入参 [{JsonConvert.SerializeObject(pyResult.PyInPut)}]\r\n出参 [{pyResult.PyOutPut}]");
        }

        /// <summary>
        /// 通用 Python 执行入口
        /// </summary>
        /// <typeparam name="TIn"></typeparam>
        /// <typeparam name="TOut"></typeparam>
        /// <param name="args"></param>
        /// <exception cref="ArgumentException"></exception>
        private void ExecutePythonWrapper<TIn, TOut>(object[] args)
        {
            if (args.Length < 5) throw new ArgumentException("参数数量错误");
            var mr = (ManualResetEvent)args[0];
            var name = (string)args[1];
            var code = (string)args[2];
            var pyRes = (PyResultModel<TIn, TOut>)args[3];
            var worker = (Func<TIn, string, string, TOut>)args[4];

            try
            {
                pyRes.Result = worker(pyRes.PyInPut!, code, name);
                pyRes.IsSuccess = true;
                pyRes.Message = "Success";
            }
            catch (Exception ex)
            {
                pyRes.IsSuccess = false;
                pyRes.Message = ex.Message;
            }
            finally
            {
                mr.Set();
            }
        }
        #endregion

        #region 各类算法 Worker 实现
        /// <summary>
        /// 执行流量PID算法
        /// </summary>
        /// <param name="input"></param>
        /// <param name="code"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        private FlowPIDOutputModel ExecuteFlowPIDWorker(FlowPIDInputModel input, string code, string name)
        {
            using var gs = Py.GIL();
            dynamic pm = PyModule.FromString($"pyCode-{name}", code);
            dynamic re = pm.algorithm_run(input.FlowData, input.MA, input.R, input.AllTrend, input.Trend,
                input.TrendRule, input.Single, input.InputData1, input.InputData2);
            var tup = PyTuple.AsTuple(re);
            return new FlowPIDOutputModel
            {
                BAdjustDescription = tup[0].As<string>(),
                BOffset1 = tup[1].As<float>(),
                BOffset2 = tup[2].As<float>()
            };
        }

        /// <summary>
        /// 执行专家PID算法
        /// </summary>
        /// <param name="input"></param>
        /// <param name="code"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        private ExpertPIDOutputModel ExecuteExpertPIDWorker(ExpertPIDInputModel input, string code, string name)
        {
            using var gs = Py.GIL();
            dynamic pm = PyModule.FromString($"pyCode-{name}", code);
            dynamic re = pm.algorithm_run(input.TargetValue, input.CurrentValue1, input.CurrentValue2, input.CurrentValue3,
                input.PID.T, input.PID.P, input.PID.I, input.PID.D,
                input.PumpSpeed1, input.PumpSpeed2, input.FlowValue1, input.FlowValue2,
                input.JJA, input.FluDataDelta1, input.FluDataDelta2, input.InputData);
            var tup = PyTuple.AsTuple(re);
            return new ExpertPIDOutputModel
            {
                BAdjustDescription = tup[0].As<string>(),
                BOffset1 = tup[1].As<float>(),
                BOffset2 = tup[2].As<float>()
            };
        }

        /// <summary>
        /// 执行 TDControl 算法
        /// </summary>
        /// <param name="input"></param>
        /// <param name="code"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        private TDControlOutputModel ExecuteTDControlWorker(TDControlInputModel input, string code, string name)
        {
            using var gs = Py.GIL();
            dynamic pm = PyModule.FromString($"pyCode-{name}", code);
            dynamic re = pm.algorithm_run(input.Data);
            var tup = PyTuple.AsTuple(re);
            var dict = new PyDict(tup[2]);
            var offsetDict = dict.ToDictionary(o => o.As<string>(), o => dict[o].As<PyList>()[0].As<int>());
            return new TDControlOutputModel
            {
                TAdjustMode = tup[0].As<int>(),
                TAdjustDescription = tup[1].As<string>(),
                TOffset = offsetDict
            };
        }

        /// <summary>
        /// 执行 MDTDControl 算法
        /// </summary>
        /// <param name="input"></param>
        /// <param name="code"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        private MDTDControlOutputModel ExecuteMDTDControlWorker(MDTDControlInputModel input, string code, string name)
        {
            using var gs = Py.GIL();
            dynamic pm = PyModule.FromString($"pyCode-{name}", code);
            dynamic re = pm.algorithm_run(input.Data);
            var tup = PyTuple.AsTuple(re);
            var dict = new PyDict(tup[3]);
            var offsetDict = dict.ToDictionary(o => o.As<string>(), o => dict[o].As<PyList>()[0].As<int>());
            return new MDTDControlOutputModel
            {
                AdjustMode = tup[0].As<int>(),
                AdjustDescription = tup[1].As<string>(),
                BOffset = tup[2].As<float>(),
                TOffset = offsetDict
            };
        }

        /// <summary>
        /// 执行 MultiMDTDControl 算法
        /// </summary>
        /// <param name="input"></param>
        /// <param name="code"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        private MULTIMDTDControlOutputModel ExecuteMULTIMDTDControlWorker(MULTIMDTDControlInputModel input, string code, string name)
        {
            using var gs = Py.GIL();
            dynamic pm = PyModule.FromString($"pyCode-{name}", code);
            dynamic re = pm.algorithm_run(input.Data);
            var tup = PyTuple.AsTuple(re);

            var tupB = PyTuple.AsTuple(tup[0]);
            var dictBDesc = new PyDict(tupB[1]);
            var bDesc = dictBDesc.ToDictionary(o => o.As<string>(), o => dictBDesc[o].As<PyList>()[0].As<string>());
            var dictBOff = new PyDict(tupB[2]);
            var bOff = dictBOff.ToDictionary(o => o.As<string>(), o => dictBOff[o].As<PyList>()[0].As<float>());

            var tupT = PyTuple.AsTuple(tup[1]);
            var dictTDesc = new PyDict(tupT[1]);
            var tDesc = dictTDesc.ToDictionary(o => o.As<string>(), o => dictTDesc[o].As<PyList>()[0].As<string>());
            var dictTOff = new PyDict(tupT[2]);
            //var tOff = dictTOff.ToDictionary(o => o.As<string>(), o => dictTOff[o].As<int>());
            var tOff = dictTOff.ToDictionary(o => o.As<string>(), o => int.Parse(dictTOff[o].ToString()?.Trim('[', ']') ?? "0"));

            return new MULTIMDTDControlOutputModel
            {
                BAdjustMode = tupB[0].As<int>(),
                BAdjustDescription = bDesc,
                BAdjustOffset = bOff,
                TAdjustMode = tupT[0].As<int>(),
                TAdjustDescription = tDesc,
                TAdjustOffset = tOff
            };
        }
        #endregion

        /// <summary>
        /// 获取加密算法公开python程序
        /// </summary>
        /// <param name="algorithmType"></param>
        /// <param name="algorithmName"></param>
        /// <returns></returns>
        private string GetAlgorithmCode(EAlgorithmType algorithmType, string pydImportPath)
        {
            switch (algorithmType)
            {
                case EAlgorithmType.流量PID:
                    {
                        return $@"# -*- coding: utf-8 -*-
import io
import pandas as pd
import re
from {pydImportPath} import FluMDControl

def algorithm_run(target, cur_val, cur_val_1, cur_val_2, dt, p, i, d, pump_top, pump_bottom, flu_top, flu_bottom, single, flu_top_delta, flu_bottom_delta, input_1):
    dff = FluMDControl(target, cur_val, cur_val_1, cur_val_2, dt, p, i, d, pump_top, pump_bottom, flu_top, flu_bottom, single, flu_top_delta, flu_bottom_delta, input_1)
    pump_top, pump_bottom = dff.final_output()
    return pump_top, pump_bottom";
                    }
                case EAlgorithmType.专家PID:
                    {
                        return $@"# -*- coding: utf-8 -*-
import io
import pandas as pd
import re
from {pydImportPath} import MDControl

def algorithm_run(target, cur_val, cur_val_1, cur_val_2, dt, p, i, d, pump_top, pump_bottom, flu_top, flu_bottom, single, flu_top_delta, flu_bottom_delta, input_1):
    dff = MDControl(target, cur_val, cur_val_1, cur_val_2, dt, p, i, d, pump_top, pump_bottom, flu_top, flu_bottom, single, flu_top_delta, flu_bottom_delta, input_1)
    pump_top, pump_bottom = dff.final_output()
    return pump_top, pump_bottom";
                    }
                case EAlgorithmType.TDControl:
                    {
                        return $@"# -*- coding: utf-8 -*-
import io
import pandas as pd
import re
from {pydImportPath} import TDControl

def algorithm_run(data_str):
    data = re.sub(r'\[(.*?)\]', lambda x: x.group(0).replace(',', ' '), data_str)
    data = io.StringIO(data)
    data = pd.read_csv(data)

    dff = TDControl(data)
    td_result = dff.final_output()
    return td_result";
                    }
                case EAlgorithmType.MDTDControl:
                    {
                        return $@"# -*- coding: utf-8 -*-
import io
import pandas as pd
import re
from {pydImportPath} import MDTDControl

def algorithm_run(data_str):
    data = re.sub(r'\[(.*?)\]', lambda x: x.group(0).replace(',', ' '), data_str)
    data = io.StringIO(data)
    data = pd.read_csv(data)

    dff = MDTDControl(data)
    mdtd_result = dff.final_output()
    return mdtd_result";
                    }
                case EAlgorithmType.MULTIMDTDControl:
                    {
                        return $@"# -*- coding: utf-8 -*-
import io
import pandas as pd
import re
from {pydImportPath} import MultiMDTDControl

def algorithm_run(data_str):  # 多腔调用代码
    data = re.sub(r'\[(.*?)\]', lambda x: x.group(0).replace(',', ' '), data_str)
    data = io.StringIO(data)
    data = pd.read_csv(data)

    dff = MultiMDTDControl(data)
    md_result, td_result = dff.final_output()
    return md_result, td_result";
                    }
                default: return string.Empty;
            }
        }

        /// <summary>
        /// 辅助方法：从 AlgorithmType 反查 ClosedLoopType（根据项目实际映射）
        /// </summary>
        /// <param name="algoType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private static EClosedLoopType GetClosedLoopType(EAlgorithmType algoType) => algoType switch
        {
            EAlgorithmType.流量PID => EClosedLoopType.MD,
            EAlgorithmType.专家PID => EClosedLoopType.MD,
            EAlgorithmType.TDControl => EClosedLoopType.TD,
            EAlgorithmType.MDTDControl => EClosedLoopType.MDTD,
            EAlgorithmType.MULTIMDTDControl => EClosedLoopType.MDTD,
            _ => throw new ArgumentOutOfRangeException(nameof(algoType))
        };
        #endregion
    }
}
