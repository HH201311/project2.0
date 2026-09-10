using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Interfaces;

namespace SUNWODA_SEVB.ViewModels.Pages.AIPredictive
{
    /// <summary>
    /// AIPredictive 项目页面（骨架）：仅完成平台注册与空页展示，业务功能待按规划实现。
    /// 规划与资料索引见 docs/AIPredictive/AIPredictive项目开发规划.md。
    /// </summary>
    [Module("VM_AIPredictivePage", "AI预测分析")]
    public class VM_AIPredictivePage : ViewModelBase
    {
        private readonly ILoggerService<VM_AIPredictivePage> _logger;

        private string? _bindingString;
        private string? _statusText;

        /// <summary>页面标题绑定文本。</summary>
        public string? BindingString
        {
            get => _bindingString;
            set => SetProperty(ref _bindingString, value);
        }

        /// <summary>页面状态提示文本。</summary>
        public string? StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public VM_AIPredictivePage(ILoggerService<VM_AIPredictivePage> logger)
        {
            _logger = logger;
        }

        public override void OnInitialize()
        {
            BindingString = "AI预测分析";
            StatusText = "项目骨架已就绪，业务功能待开发（见 docs/AIPredictive/）。";
            _logger.Info("AIPredictive 页面初始化完成");
            base.OnInitialize();
        }

        public override void OnNavigatedTo(object? parameter)
        {
            // 进入页面时刷新数据；当前骨架无数据源
            base.OnNavigatedTo(parameter);
        }

        public override void OnNavigatedFrom()
        {
            // 离开页面时停止定时器 / 释放订阅；当前骨架无需清理
            base.OnNavigatedFrom();
        }
    }
}
