using HandyControl.Tools.Extension;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using SUNWODA_SEVB.Tool.Helper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using static SUNWODA_SEVB.ViewModels.Dialogs.CCTB.VM_TBDataConfigDialog;

namespace SUNWODA_SEVB.ViewModels.Dialogs.CCTB
{
    public class VM_TBDataConfigDialog : DialogViewModelBase, IDialogResultable<bool>
    {
        public bool Result { get; set; } = false;
        public Action? CloseAction { get; set; }

        public ICommand CloseCommand =>
            new RelayCommand(() =>
            {
                Result = false;
                CloseAction?.Invoke();
            });

        /// <summary>
        /// 选择
        /// </summary>
        public ICommand ParamSelectedCommand => new RelayCommand(ParamSelected);

        public List<EParamOption> ParamOptions { get; private set; } = [];
        private void ParamSelected(object? arg)
        {
            if (!(arg is ParamOptionModel paramOptionModel)) return;

        }

        /// <summary>
        /// 确认
        /// </summary>
        public ICommand ConfirmCommand => new RelayCommand(Confirm);

        /// <summary>
        /// 确认
        /// </summary>
        private void Confirm()
        {
            var selectedParamOptions = ParamOptionModels.Where(o => o.IsSelected).Select(o => o.ParamOption);
            ParamOptions.AddRange(selectedParamOptions);
            ParamOptions.Sort();
            // 保存数据库
            _ = RunInBackgroundAsync(RefreshParamrersAsync);
            Result = true;
            CloseAction?.Invoke();
        }

        /// <summary>
        /// 取消
        /// </summary>
        public ICommand CancleCommand => new RelayCommand(Cancle);

        private void Cancle()
        {
            Result = false;
            CloseAction?.Invoke();
        }

        public VM_TBDataConfigDialog(ESideType sideType, IProjectSettingRepository projectSettingRepository)
        {
            _sideType = sideType;
            _projectSettingRepository = projectSettingRepository;
            _ = RunInBackgroundAsync(InitializeParametersAsync);
        }

        private ESideType _sideType;
        private IProjectSettingRepository _projectSettingRepository;

        /// <summary>
        /// 初始化数据库参数
        /// </summary>
        /// <returns></returns>
        private async Task InitializeParametersAsync()
        {
            var paramName = $"TBDataParamOptions{_sideType}";
            if (!_projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, CCTBCore.BelongToVM, "", DataTypeConverter.TypeToString(typeof(EParamOption[])), 2)
                    {
                        Remark = "涂布闭环面密度实时参数显示配置"
                    }
                );
            }

            var tbDataParamOptions = _projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM)?.Value?.Split(',')?.ToList();
            var paramOptions = new List<EParamOption>();
            if (tbDataParamOptions != null && tbDataParamOptions.Count > 0)
            {
                tbDataParamOptions.ForEach(o =>
                {
                    if (Enum.TryParse(o, out EParamOption paramOption))
                        paramOptions.Add(paramOption);
                });
            }
            await InitilizeParamOptionModels(paramOptions);
        }

        /// <summary>
        /// 初始化界面控件
        /// </summary>
        /// <param name="paramOptions"></param>
        /// <returns></returns>
        private async Task InitilizeParamOptionModels(List<EParamOption> paramOptions)
        {
            var eParamOptions = Enum.GetValues(typeof(EParamOption));
            foreach (EParamOption eParamOption in eParamOptions)
            {
                var paramOptionModel = new ParamOptionModel();
                paramOptionModel.ParamOption = eParamOption;
                paramOptionModel.DisplayName = eParamOption.ToString();
                var isSelected = (paramOptions != null && paramOptions.Contains(eParamOption));
                paramOptionModel.IsSelected = isSelected;
                await RunOnUIThreadAsync(() => ParamOptionModels.Add(paramOptionModel));
            }
        }

        /// <summary>
        /// 保存到数据库
        /// </summary>
        /// <returns></returns>
        private async Task RefreshParamrersAsync()
        {
            var paramName = $"TBDataParamOptions{_sideType}";
            var tbDataParamOptions = _projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (tbDataParamOptions == null) return;
            tbDataParamOptions.Value = string.Join(',', ParamOptionModels.Where(o => o.IsSelected).Select(o => o.ParamOption));
            var re = await _projectSettingRepository.UpdateAsync(tbDataParamOptions);
            return;
        }

        /// <summary>
        /// 绑定到界面
        /// </summary>
        public ObservableCollection<ParamOptionModel> ParamOptionModels { get; set; } = [];
    }
}
