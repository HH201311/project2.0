using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Component.Project.CCTB
{
    /// <summary>
    /// 自定义下拉框绑定数据元使用的模型类
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ComboboxItemModel<T> : ModelBase
    {
        private T? itemValue;
        private string? displayName;

        public T? ItemValue { get => itemValue; set => SetProperty(ref itemValue, value); }
        public string? DisplayName { get => displayName; set => SetProperty(ref displayName, value); }
    }
}
