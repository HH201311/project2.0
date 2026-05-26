using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels
{
    /// <summary>
    /// MessageBoxButton的样式参数
    /// </summary>
    public class MessageBoxButtonModel : ModelBase
    {
        private Visibility visibility = Visibility.Collapsed;
        private object? content;
        private Brush? background;
        private ICommand? command;

        /// <summary>
        /// 显示
        /// </summary>
        public Visibility Visibility { get => visibility; set => SetProperty(ref visibility, value); }

        /// <summary>
        /// 显示内容
        /// </summary>
        public object? Content { get => content; set => SetProperty(ref content, value); }

        /// <summary>
        /// 背景色
        /// </summary>
        public Brush? Background { get => background; set => SetProperty(ref background, value); }

        /// <summary>
        /// 命令
        /// </summary>
        public ICommand? Command { get => command; set => SetProperty(ref command, value); }
    }
}
