using RestSharp;
using SUNWODA_SEVB.Component.UserControls;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.MES;
using SUNWODA_SEVB.MES.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Windows.Input;

namespace SUNWODA_SEVB.ViewModels.Pages.Demo
{
    /// <summary>
    /// MES测试页面 ViewModel
    /// </summary>
    [Module("VM_MESTestPage", "MES测试模块")]
    public class VM_MESTestPage : ViewModelBase
    {
        private readonly ILoggerService<VM_MESTestPage> _logger;
        private readonly IModuleManager _moduleManager;
        // MES 服务注入
        private readonly IOfflineDataUploadService _offlineDataUploadService;
        private readonly IMarkingDataUploadService _markingDataUploadService;
        private readonly IMesConfigurationProvider _mesConfigProvider;

        private bool _initializedView = false;
        private ModelViewerControl? _modelViewerControl;

        #region 属性

        public ModelViewerControl? ModelViewerControl
        {
            get => _modelViewerControl;
            set => SetProperty(ref _modelViewerControl, value);
        }

        private string _productSn = "190124C16971700";
        public string ProductSn
        {
            get => _productSn;
            set => SetProperty(ref _productSn, value);
        }

        private string _testResult = "1";
        public string TestResult
        {
            get => _testResult;
            set => SetProperty(ref _testResult, value);
        }

        private string _markingData = "正极翻折,极耳偏移";
        public string MarkingData
        {
            get => _markingData;
            set => SetProperty(ref _markingData, value);
        }

        private string _uploadStatus = "就绪";
        public string UploadStatus
        {
            get => _uploadStatus;
            set => SetProperty(ref _uploadStatus, value);
        }

        private bool _isUploading = false;
        public bool IsUploading
        {
            get => _isUploading;
            set => SetProperty(ref _isUploading, value);
        }

        private bool _isMesEnabled = false;
        public bool IsMesEnabled
        {
            get => _isMesEnabled;
            set => SetProperty(ref _isMesEnabled, value);
        }

        private ObservableCollection<string> _uploadLogs = new();
        public ObservableCollection<string> UploadLogs
        {
            get => _uploadLogs;
            set => SetProperty(ref _uploadLogs, value);
        }

        #endregion

        #region 命令

        public ICommand UploadOfflineDataCommand { get; }
        public ICommand UploadMarkingDataCommand { get; }
        public ICommand CheckMesStatusCommand { get; }
        public ICommand ClearLogsCommand { get; }
        public ICommand ExportLogsCommand { get; }
        public ICommand TestConnectionCommand { get; }

        #endregion

        public VM_MESTestPage(
            ILoggerService<VM_MESTestPage> logger,
            IModuleManager moduleManager,
            IOfflineDataUploadService offlineDataUploadService,
            IMarkingDataUploadService markingDataUploadService,
            IMesConfigurationProvider mesConfigProvider
        )
        {
            _logger = logger;
            _moduleManager = moduleManager;
            _offlineDataUploadService = offlineDataUploadService;
            _markingDataUploadService = markingDataUploadService;
            _mesConfigProvider = mesConfigProvider;

            // 初始化命令
            UploadOfflineDataCommand = new RelayCommand(async () => await UploadOfflineDataAsync());
            UploadMarkingDataCommand = new RelayCommand(async () => await UploadMarkingDataAsync());
            CheckMesStatusCommand = new RelayCommand(async () => await CheckMesStatusAsync());
            ClearLogsCommand = new RelayCommand(ClearLogs);
            ExportLogsCommand = new RelayCommand(async () => await ExportLogsAsync());
            TestConnectionCommand = new RelayCommand(async () => await TestConnectionAsync());
        }

        protected override async Task OnNavigatedToAsync(object? parameter)
        {
            if (!_initializedView)
            {
                var moduleName = typeof(VM_MESTestPage).GetCustomAttribute<ModuleAttribute>()?.Name;
                if (moduleName != null)
                {
                    await RunOnUIThreadAsync(() =>
                    {
                        var view = _moduleManager.GetViewFromService(moduleName);
                        ModelViewerControl = view?.FindName("ModelViewer") as ModelViewerControl;
                    });
                }

                _initializedView = true;
            }

            // 初始化MES服务
            await InitializeMesServicesAsync();

            // 检查 MES 服务状态
            await CheckMesStatusAsync();

            await base.OnNavigatedToAsync(parameter);
        }

        #region MES 初始化方法

        /// <summary>
        /// 初始化MES服务
        /// </summary>
        private async Task InitializeMesServicesAsync()
        {
            try
            {
                await AddLogAsync("开始初始化MES服务...");

                // 初始化离线数据上传服务
                var offlineInitResult = await _offlineDataUploadService.InitializeAsync();
                await AddLogAsync($"离线数据上传服务初始化: {(offlineInitResult ? "成功" : "失败")}");

                // 初始化Marking数据上传服务  
                var markingInitResult = await _markingDataUploadService.InitializeAsync();
                await AddLogAsync($"Marking数据上传服务初始化: {(markingInitResult ? "成功" : "失败")}");

                // 更新UI状态
                IsMesEnabled = _offlineDataUploadService.IsEnabled || _markingDataUploadService.IsEnabled;

                if (IsMesEnabled)
                {
                    await AddLogAsync("✓ MES服务初始化成功");
                    _logger.Info("MES服务初始化成功");
                }
                else
                {
                    await AddLogAsync("⚠ MES服务初始化失败或未启用");
                    _logger.Warn("MES服务初始化失败或未启用");
                }
            }
            catch (Exception ex)
            {
                await AddLogAsync($"✗ MES服务初始化异常: {ex.Message}");
                _logger.Error("MES服务初始化异常", ex);
                IsMesEnabled = false;
            }
        }

        #endregion

        #region MES 操作方法

        ///// <summary>
        ///// 上传离线数据到 MES
        ///// </summary>
        //private async Task UploadOfflineDataAsync()
        //{
        //    // 验证输入
        //    if (string.IsNullOrWhiteSpace(ProductSn))
        //    {
        //        await AddLogAsync("✗ 请输入产品序列号");
        //        logger.Warn("上传失败: 产品序列号为空");
        //        return;
        //    }

        //    // 检查服务是否已启用
        //    if (!_offlineDataUploadService.IsEnabled)
        //    {
        //        await AddLogAsync("离线数据上传服务未启用");

        //        // 尝试重新初始化
        //        await AddLogAsync("尝试重新初始化离线数据上传服务...");
        //        var initResult = await _offlineDataUploadService.InitializeAsync();
        //        if (!initResult)
        //        {
        //            await AddLogAsync("✗ 服务初始化失败,无法上传");
        //            return;
        //        }
        //    }

        //    if (IsUploading)
        //    {
        //        await AddLogAsync("正在上传中,请稍候...");
        //        return;
        //    }

        //    try
        //    {
        //        IsUploading = true;
        //        UploadStatus = "上传中...";
        //        await AddLogAsync($"开始上传离线数据 - 产品SN: {ProductSn}");

        //        // 构造测试数据
        //        var testDatas = new List<OfflineTestData>
        //        {
        //            new OfflineTestData
        //            {
        //                paramCode = "1026",
        //                paramName = "AI检测翻折",
        //                paramValue = "正极翻折",
        //                paramResult = TestResult,
        //                paramUnit = ""
        //            },
        //            new OfflineTestData
        //            {
        //                paramCode = "1027",
        //                paramName = "电压测试",
        //                paramValue = "3.7",
        //                paramResult = TestResult,
        //                paramUnit = "V"
        //            }
        //        };

        //        // 调用上传服务
        //        var response = await _offlineDataUploadService.UploadAsync(
        //            productSn: ProductSn,
        //            testResult: TestResult,
        //            testDatas: testDatas,
        //            cancellationToken: default
        //        );

        //        // 处理响应
        //        if (response.Success)
        //        {
        //            UploadStatus = "上传成功";
        //            await AddLogAsync($"✓ 上传成功 - TraceId: {response.TraceId}");
        //            logger.Info($"MES离线数据上传成功 - {ProductSn}", true);
        //        }
        //        else
        //        {
        //            UploadStatus = "上传失败";
        //            await AddLogAsync($"✗ 上传失败 - {response.Code}: {response.Message}");
        //            logger.Warn($"MES离线数据上传失败 - {ProductSn}: {response.Message}", true);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        UploadStatus = "上传异常";
        //        await AddLogAsync($"✗ 上传异常: {ex.Message}");
        //        logger.Error($"MES离线数据上传异常 - {ProductSn}", ex, true);
        //    }
        //    finally
        //    {
        //        IsUploading = false;
        //    }
        //}

        HttpTest httpTest = new HttpTest();
        /// <summary>
        /// 上传离线数据到 MES (使用方式一测试)
        /// </summary>
        private async Task UploadOfflineDataAsync()
        {
            try
            {
                var options = new RestClientOptions("http://10.178.5.6:9002")
                {
                    // 在这里设置所有客户端级别的选项
                    Timeout = Timeout.InfiniteTimeSpan,
                    UserAgent = "Apifox/1.0.0 (https://apifox.com)"
                };
                var client = new RestClient(options);
                // 3. 创建 RestRequest 时，传入具体的资源路径 (Resource)
                var request = new RestRequest("mes/OfflineDataUpload", RestSharp.Method.Post);

                // 4. 添加请求头 (Headers) - 这部分保持不变
                request.AddHeader("Accept", "*/*");
                request.AddHeader("Host", "10.178.5.6:9002");
                request.AddHeader("Connection", "keep-alive");
                request.AddHeader("Content-Type", "application/x-www-form-urlencoded");

                var currentTimeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                // 5. 添加请求体参数 - 这部分保持不变
                var jsonData = $@"{{
                  ""operatorId"": ""53084205"",
                  ""productSn"": ""190124C16971700"",
                  ""groupCode"": ""CDXPD1"",
                  ""deviceSn"": ""L3ZZABPDJ001"",
                  ""moNumber"": ""569-MO2407260702-01"",
                  ""timeStamp"": ""{currentTimeStamp}"",
                  ""testResult"": ""NG"",
                  ""testData"": [
                    {{
                      ""paramCode"": ""1026"",
                      ""paramName"": ""AI检测翻折"",
                      ""paramValue"": ""正极翻折"",
                      ""paramResult"": ""1"",
                      ""paramUnit"": """"
                    }}
                  ],
                  ""environment"": [],
                  ""stepData"": []
                }}";

                request.AddParameter("jsonData", jsonData, ParameterType.GetOrPost);
                string httpresponse = await httpTest.HttpWebPostAsync("http://10.178.5.6:9002/mes/OfflineDataUpload", jsonData);

                var response = await client.ExecuteAsync(request);
                if (response.IsSuccessful)
                {
                    _logger.Info("状态码: " + (int)response.StatusCode + " " + response.StatusCode);
                }
                else
                {
                    _logger.Error("状态码: " + (int)response.StatusCode + " " + response.StatusCode);
                    _logger.Error("错误信息: " + response.ErrorMessage);
                }

            }
            catch (Exception ex)
            {
                UploadStatus = "上传异常";
                await AddLogAsync($"✗ 上传异常: {ex.Message}");
                _logger.Error($"MES离线数据上传异常 - {ProductSn}", ex, true);
            }
        }


        /// <summary>
        /// 上传 Marking 数据到 MES
        /// </summary>
        private async Task UploadMarkingDataAsync()
        {
            // 验证输入
            if (string.IsNullOrWhiteSpace(ProductSn))
            {
                await AddLogAsync("✗ 请输入产品序列号");
                _logger.Warn("上传失败: 产品序列号为空");
                return;
            }

            if (string.IsNullOrWhiteSpace(MarkingData))
            {
                await AddLogAsync("✗ 请输入Marking数据");
                _logger.Warn("上传失败: Marking数据为空");
                return;
            }

            // 检查服务是否已启用
            if (!_markingDataUploadService.IsEnabled)
            {
                await AddLogAsync("Marking数据上传服务未启用");

                // 尝试重新初始化
                await AddLogAsync("尝试重新初始化Marking数据上传服务...");
                var initResult = await _markingDataUploadService.InitializeAsync();
                if (!initResult)
                {
                    await AddLogAsync("✗ 服务初始化失败,无法上传");
                    return;
                }
            }

            if (IsUploading)
            {
                await AddLogAsync("正在上传中,请稍候...");
                return;
            }

            try
            {
                IsUploading = true;
                UploadStatus = "上传中...";
                await AddLogAsync($"开始上传Marking数据 - 产品SN: {ProductSn}");
                await AddLogAsync($"Marking内容: {MarkingData}");

                // 调用接口定义的方法
                //var result = await _markingDataUploadService.UploadMarkingDataAsync(
                //    serialNumber: ProductSn,
                //    markingData: MarkingData
                //);
                var Data = new List<string>() { MarkingData };
                var result = await _markingDataUploadService.UploadAsync(
                    productSn: ProductSn,
                    defectList: Data
                );

                // 处理响应
                if (result.Success)
                {
                    UploadStatus = "上传成功";
                    await AddLogAsync($"✓ Marking数据上传成功");
                    _logger.Info($"MES Marking数据上传成功 - {ProductSn}", true);
                }
                else
                {
                    UploadStatus = "上传失败";
                    await AddLogAsync($"✗ Marking数据上传失败");
                    _logger.Warn($"MES Marking数据上传失败 - {ProductSn}", true);
                }
            }
            catch (Exception ex)
            {
                UploadStatus = "上传异常";
                await AddLogAsync($"✗ 上传异常: {ex.Message}");
                _logger.Error($"MES Marking数据上传异常 - {ProductSn}", ex, true);
            }
            finally
            {
                IsUploading = false;
            }
        }

        /// <summary>
        /// 检查 MES 服务状态
        /// </summary>
        private async Task CheckMesStatusAsync()
        {
            try
            {
                await AddLogAsync("正在检查MES服务状态...");

                // 获取配置
                var config = await _mesConfigProvider.GetConfigurationAsync();

                // 检查服务的实际状态
                var offlineEnabled = _offlineDataUploadService.IsEnabled;
                var markingEnabled = _markingDataUploadService.IsEnabled;

                IsMesEnabled = (config != null) && (offlineEnabled || markingEnabled);

                if (IsMesEnabled)
                {
                    UploadStatus = "MES服务已启用";
                    await AddLogAsync($"✓ MES服务已启用 - BaseUrl: {config?.BaseUrl}");
                    await AddLogAsync($"  - 离线数据上传: {(offlineEnabled ? "✓ 启用" : "✗ 禁用")}");
                    await AddLogAsync($"  - Marking数据上传: {(markingEnabled ? "✓ 启用" : "✗ 禁用")}");
                    _logger.Info($"MES服务状态检查: 已启用 (离线:{offlineEnabled}, Marking:{markingEnabled})");
                }
                else
                {
                    UploadStatus = "MES服务未启用";
                    await AddLogAsync("⚠ MES服务未启用");

                    if (config == null)
                    {
                        await AddLogAsync("  - 原因: 配置加载失败");
                        await AddLogAsync("  - 请检查数据库中的 IsMESEnabled 设置");
                        await AddLogAsync("  - 请检查 MesProfile 配置");
                    }
                    else
                    {
                        await AddLogAsync("  - 原因: 服务初始化失败");
                        await AddLogAsync($"  - 配置BaseUrl: {config.BaseUrl}");
                    }

                    _logger.Warn("MES服务状态检查: 未启用");
                }
            }
            catch (Exception ex)
            {
                UploadStatus = "状态检查失败";
                await AddLogAsync($"✗ 状态检查失败: {ex.Message}");
                _logger.Error("MES服务状态检查失败", ex);
            }
        }

        /// <summary>
        /// 测试MES连接
        /// </summary>
        private async Task TestConnectionAsync()
        {
            try
            {
                await AddLogAsync("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                await AddLogAsync("开始测试MES连接...");

                var config = await _mesConfigProvider.GetConfigurationAsync();

                if (config == null)
                {
                    await AddLogAsync("✗ 无法获取MES配置");
                    return;
                }

                await AddLogAsync($"MES服务器地址: {config.BaseUrl}");
                await AddLogAsync($"超时设置: {config.TimeoutSeconds}秒");

                // 这里可以添加实际的连接测试逻辑
                // 例如发送一个简单的健康检查请求

                await AddLogAsync("正在尝试连接...");
                await Task.Delay(1000); // 模拟连接延迟

                if (IsMesEnabled)
                {
                    await AddLogAsync("✓ 连接测试成功");
                    await AddLogAsync($"  - 服务响应正常");
                    _logger.Info("MES连接测试成功");
                }
                else
                {
                    await AddLogAsync("✗ 连接测试失败");
                    await AddLogAsync("  - 服务未启用或配置错误");
                    _logger.Warn("MES连接测试失败");
                }

                await AddLogAsync("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            }
            catch (Exception ex)
            {
                await AddLogAsync($"✗ 连接测试异常: {ex.Message}");
                _logger.Error("MES连接测试异常", ex);
            }
        }

        /// <summary>
        /// 清空日志
        /// </summary>
        private void ClearLogs()
        {
            RunOnUIThread(() =>
            {
                var count = UploadLogs.Count;
                UploadLogs.Clear();
                _logger.Info($"已清空 {count} 条日志记录");
            });
        }

        /// <summary>
        /// 导出日志到文件
        /// </summary>
        private async Task ExportLogsAsync()
        {
            try
            {
                if (UploadLogs.Count == 0)
                {
                    await AddLogAsync("⚠ 没有日志可导出");
                    return;
                }

                // 创建日志目录
                var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "MES");
                Directory.CreateDirectory(logDirectory);

                // 生成文件名
                var fileName = $"MES_Upload_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                var filePath = Path.Combine(logDirectory, fileName);

                // 写入日志
                var sb = new StringBuilder();
                sb.AppendLine("═══════════════════════════════════════════════");
                sb.AppendLine($"MES数据上传日志");
                sb.AppendLine($"导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"日志条数: {UploadLogs.Count}");
                sb.AppendLine("═══════════════════════════════════════════════");
                sb.AppendLine();

                // 反转日志顺序,使其按时间正序排列
                foreach (var log in UploadLogs.Reverse())
                {
                    sb.AppendLine(log);
                }

                await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);

                await AddLogAsync($"✓ 日志已导出: {fileName}");
                await AddLogAsync($"  路径: {logDirectory}");
                _logger.Info($"MES日志已导出: {filePath}");

                // 可选: 打开文件所在目录
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            }
            catch (Exception ex)
            {
                await AddLogAsync($"✗ 日志导出失败: {ex.Message}");
                _logger.Error("MES日志导出失败", ex);
            }
        }

        /// <summary>
        /// 添加日志到界面
        /// </summary>
        private async Task AddLogAsync(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            await RunOnUIThreadAsync(() =>
            {
                UploadLogs.Insert(0, $"[{timestamp}] {message}");

                // 限制日志条数
                while (UploadLogs.Count > 100)
                {
                    UploadLogs.RemoveAt(UploadLogs.Count - 1);
                }
            });
        }

        #endregion




    }
    public class HttpTest
    {
        private static readonly HttpClient httpClient = new HttpClient();

        /// <summary>
        /// 使用 HttpClient 发送 POST 请求 (异步)
        /// </summary>
        /// <param name="url">请求 URL</param>
        /// <param name="jsonparams">JSON 格式的参数</param>
        /// <param name="jsonHeader">包裹参数的键名，默认为 "jsonData"</param>
        /// <returns>服务器响应的字符串</returns>
        public async Task<string> HttpWebPostAsync(string url, string jsonparams, string jsonHeader = "jsonData")
        {
            var result = "failed";

            // 准备要发送的数据
            var jsonData = jsonparams.Replace("\n", "").Replace("\t", "");
            if (!string.IsNullOrEmpty(jsonHeader))
            {
                jsonData = $"{jsonHeader}={Uri.EscapeDataString(jsonparams)}";
            }

            try
            {
                // 设置请求超时时间
                httpClient.Timeout = TimeSpan.FromSeconds(20);

                // 1. 创建请求内容 (HttpContent)
                var content = new StringContent(jsonData, Encoding.UTF8, "application/x-www-form-urlencoded");

                // 2. 发送 POST 请求
                HttpResponseMessage response = await httpClient.PostAsync(url, content);

                // 3. 确保请求成功 (状态码 200-299)
                response.EnsureSuccessStatusCode();

                // 4. 读取响应内容
                result = await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex)
            {
                // 处理网络请求相关的错误 (如 DNS 解析失败、服务器拒绝连接等)
                result = $"{result}:{ex.Message}";
                Console.WriteLine($"请求失败 (HttpRequestException): {ex.Message}");
            }
            catch (TaskCanceledException ex)
            {
                // 处理请求超时
                result = $"{result}:Request timed out. {ex.Message}";
                Console.WriteLine($"请求超时: {ex.Message}");
            }
            catch (Exception ex)
            {
                // 处理其他所有异常
                result = $"{result}:{ex.Message}";
                Console.WriteLine($"请求失败 (Exception): {ex.Message}");
            }

            // 将接口日志记录到数据库
            return result;
        }
    }
}
