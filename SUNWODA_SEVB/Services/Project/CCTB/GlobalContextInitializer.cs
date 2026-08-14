using Microsoft.Extensions.Hosting;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Events;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.BusinessModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core.MessageModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.CCDSocketServer;
using SUNWODA_SEVB.Core.Models.Project.CCTB.RoleModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using SUNWODA_SEVB.Data.Repositories;
using SUNWODA_SEVB.Tool.Extension;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Services.Project.CCTB
{
    /// <summary>
    /// 界面全局上下文服务后台服务
    /// </summary>
    public class GlobalContextInitializer : IGlobalContextInitializer
    {
        private readonly ILoggerService<GlobalContextInitializer> _logger;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IUsersRepository _usersRepository;

        private bool _isInitialized = false;
        private readonly GlobalContextModel _globalContext;

        public GlobalContextInitializer(
            ILoggerService<GlobalContextInitializer> logger,
            IGlobalSettingRepository globalSettingRepository,
            IProjectSettingRepository projectSettingRepository,
            IUsersRepository usersRepository)
        {
            _logger = logger;
            _globalSettingRepository = globalSettingRepository;
            _projectSettingRepository = projectSettingRepository;
            _usersRepository = usersRepository;

            _globalContext = GlobalContextModel.Instance;
        }

        /// <summary>
        /// 初始化全局上下文
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            try
            {
                // 等待数据库初始化完成
                await App.DatabaseInitTask;
            }
            catch (Exception ex)
            {
                // 初始化失败
                _logger.Error($"数据库初始化失败,已退出涂布界面全局上下文初始化服务！", ex);
                return;
            }

            // 检查是否为CCTB项目
            var appStartMainWindow = _globalSettingRepository.GetSettingValue("AppStartMainWindow");
            if (appStartMainWindow != "CCTBMainWindow") return;

            _logger.Info("涂布界面全局上下文初始化服务启动");

            // 初始化全局上下文
            InitializeGlobalContext();

            // 订阅用户状态变更事件
            UserStateChangedEvent.OnUserStateChanged -= OnUserStateChanged;
            UserStateChangedEvent.OnUserStateChanged += OnUserStateChanged;

            CCTBCore.CoatingMessageNotificationEvent -= OnCoatingMessageNotification;
            CCTBCore.CoatingMessageNotificationEvent += OnCoatingMessageNotification;

            CCTBCore.CCDMessageNotificationEvent -= OnCCDMessageNotification;
            CCTBCore.CCDMessageNotificationEvent += OnCCDMessageNotification;

            _isInitialized = true;
            _logger.Info("涂布界面全局上下文初始化服务完成");
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Cleanup()
        {
            if (!_isInitialized) return;

            _logger.Info("正在清理涂布界面全局上下文初始化服务...");

            // 取消事件订阅
            UserStateChangedEvent.OnUserStateChanged -= OnUserStateChanged;
            CCTBCore.CoatingMessageNotificationEvent -= OnCoatingMessageNotification;
            CCTBCore.CCDMessageNotificationEvent -= OnCCDMessageNotification;

            _isInitialized = false;
            _logger.Info("涂布界面全局上下文初始化服务已清理");
        }

        /// <summary>
        /// 初始化全局上下文
        /// </summary>
        private void InitializeGlobalContext()
        {
            var instance = GlobalContextModel.Instance;
            instance.ClosedLoopSwitchMDA = _projectSettingRepository.LoadClosedLoopSwitchProjectParameters(EClosedLoopType.MD, ESideType.A);
            instance.ClosedLoopSwitchTDA = _projectSettingRepository.LoadClosedLoopSwitchProjectParameters(EClosedLoopType.TD, ESideType.A);
            instance.ClosedLoopSwitchCCDA = _projectSettingRepository.LoadClosedLoopSwitchProjectParameters(EClosedLoopType.CCD, ESideType.A);
            instance.ClosedLoopSwitchMDB = _projectSettingRepository.LoadClosedLoopSwitchProjectParameters(EClosedLoopType.MD, ESideType.B);
            instance.ClosedLoopSwitchTDB = _projectSettingRepository.LoadClosedLoopSwitchProjectParameters(EClosedLoopType.TD, ESideType.B);
            instance.ClosedLoopSwitchCCDB = _projectSettingRepository.LoadClosedLoopSwitchProjectParameters(EClosedLoopType.CCD, ESideType.B);

            instance.ProductionInfo.CellModel = _projectSettingRepository.LoadProductModelProjectParameters();
        }

        /// <summary>
        /// 当用户状态更新时触发 - 更新用户信息
        /// </summary>
        /// <param name="userAccount">用户账号</param>
        private void OnUserStateChanged(string userAccount)
        {
            try
            {
                var role = _usersRepository.GetByUserAccount(userAccount);
                if (role == null)
                {
                    _logger.Warn($"未找到用户账号: {userAccount}");
                    return;
                }

                // 1.更新用户信息
                _globalContext.UserAccount = role.UserAccount;
                _globalContext.UserRole = (UserRole)role.RoleId;
                _globalContext.UserName = string.IsNullOrEmpty(role.UserName) ? "*****" : role.UserName;
                _globalContext.UserIcon = GetUserIcon(role.UserAccount, role.UserName);
                _globalContext.UserRoleID = role.RoleId;

                // 2.更新CCTBCore静态属性
                CCTBCore.UserAccount = role.UserAccount;
                CCTBCore.UserName = role.UserName;
                CCTBCore.UserRoleID = role.RoleId;

                _logger.Info($"用户切换成功: {userAccount} -> {role.UserName} (角色: {(UserRole)role.RoleId})");
            }
            catch (Exception ex)
            {
                _logger.Error($"更新用户信息失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 当面密度数据接收时触发
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnCoatingMessageNotification(object? sender, MessageEventArgs<CoatingDataModel> e)
        {
            if (sender is not IBusinessService business) return;
            if (e.Message == null) return;

            // 是否是闭环选择数据
            var isNeed = business.IsNeedCoatingData(e.Tag);
            if (!isNeed) return;

            // 是否是干膜净重数据
            var isNetWeight = e.Message.WeightDataType switch
            {
                EWeightDataType.干膜净重分区数据 => true,
                EWeightDataType.干膜净重毫米数据 => true,
                _ => false
            };

            if (!isNetWeight) return;

            var pi = GlobalContextModel.Instance.ProductionInfo;
            if (e.Message.SideType == ESideType.A)
            {
                pi.NetWeightSpecification_A = e.Message.TargetValueA;
                pi.NetWeightSpecificationLimit_A = e.Message.ToleranceValueA;
                return;
            }
            if (e.Message.SideType == ESideType.B)
            {
                pi.NetWeightSpecification_B = e.Message.TargetValueB;
                pi.NetWeightSpecificationLimit_B = e.Message.ToleranceValueB;
                return;
            }
        }

        /// <summary>
        /// 当T块数据接收时触发
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnCCDMessageNotification(object? sender, MessageEventArgs<Tuple<string, WeightMessageModel>> e)
        {
            if (sender is not IBusinessService business) return;
            if (e.Message == null || e.Message.Item2 == null) return;

            if (e.Message.Item1 != "data_n_dry") return;

            var data = e.Message.Item2;
            var pi = GlobalContextModel.Instance.ProductionInfo;
            if (data.TabSpecification != null)
            {
                var target1 = double.Parse(data.TabSpecification.FirstOrDefault(o => o.PName == "中间极耳目标值")?.PValue ?? "0");
                var upper1 = double.Parse(data.TabSpecification.FirstOrDefault(o => o.PName == "中间极耳上限公差")?.PValue ?? "0");
                var lower1 = double.Parse(data.TabSpecification.FirstOrDefault(o => o.PName == "中间极耳下限公差")?.PValue ?? "0");
                pi.CCDSpecification_BatteryTabMiddle = target1;
                pi.CCDSpecificationLimit_BatteryTabMiddle = upper1;

                var target2 = double.Parse(data.TabSpecification.FirstOrDefault(o => o.PName == "中间极耳目标值")?.PValue ?? "0");
                var upper2 = double.Parse(data.TabSpecification.FirstOrDefault(o => o.PName == "中间极耳上限公差")?.PValue ?? "0");
                var lower2 = double.Parse(data.TabSpecification.FirstOrDefault(o => o.PName == "中间极耳下限公差")?.PValue ?? "0");
                pi.CCDSpecification_BatteryTabSide = target2;
                pi.CCDSpecificationLimit_BatteryTabSide = upper2;
            }

            if (data.CilSpecification != null)
            {
                var target = double.Parse(data.CilSpecification.FirstOrDefault(o => o.PName == "陶瓷目标值")?.PValue ?? "0");
                var upper = double.Parse(data.CilSpecification.FirstOrDefault(o => o.PName == "陶瓷上限公差")?.PValue ?? "0");
                var lower = double.Parse(data.CilSpecification.FirstOrDefault(o => o.PName == "陶瓷下限公差")?.PValue ?? "0");
                pi.CCDSpecification_Ceramic = target;
                pi.CCDSpecificationLimit_Ceramic = upper;
            }

            if (data.CoatingSpecification != null)
            {
                var target = double.Parse(data.CoatingSpecification.FirstOrDefault(o => o.PName == "涂膜目标值")?.PValue ?? "0");
                var upper = double.Parse(data.CoatingSpecification.FirstOrDefault(o => o.PName == "涂膜上限公差")?.PValue ?? "0");
                var lower = double.Parse(data.CoatingSpecification.FirstOrDefault(o => o.PName == "涂膜下限公差")?.PValue ?? "0");
                pi.CCDSpecification_Ceramic = target;
                pi.CCDSpecificationLimit_Ceramic = upper;
            }

            if (data.MisalignSpecification != null)
            {
                var target = double.Parse(data.MisalignSpecification.FirstOrDefault(o => o.PName == "错位目标值")?.PValue ?? "0");
                var upper = double.Parse(data.MisalignSpecification.FirstOrDefault(o => o.PName == "错位上限公差")?.PValue ?? "0");
                var lower = double.Parse(data.MisalignSpecification.FirstOrDefault(o => o.PName == "错位下限公差")?.PValue ?? "0");
                pi.CCDSpecificationLimit_Mismatch = upper;
            }
        }
        #region 私有辅助方法
        /// <summary>
        /// 获取用户图标
        /// </summary>
        private string GetUserIcon(string userAccount, string userName)
        {
            if (string.IsNullOrEmpty(userAccount)) return "*";

            return userAccount switch
            {
                "guest" => "访",
                "engineer" => "工",
                "admin" => "管",
                "sadmin" => "超",
                _ => string.IsNullOrEmpty(userName) ? "*" : userName.Last().ToString(),
            };
        }
        #endregion
    }
}