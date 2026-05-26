using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels
{
    /// <summary>
    /// 分页导航按钮的数据模型
    /// </summary>
    public class PaginationItemModel : ModelBase
    {
        private bool isCurrentPage;
        private int pageNumber;

        /// <summary>
        /// 是否为当前页
        /// </summary>
        public bool IsCurrentPage { get => isCurrentPage; set => SetProperty(ref isCurrentPage, value); }

        /// <summary>
        /// 页码  
        /// </summary>
        public int PageNumber { get => pageNumber; set => SetProperty(ref pageNumber, value); }

        /// <summary>
        /// 构造函数
        /// </summary>
        public PaginationItemModel() { }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="pageNumber"></param>
        /// <param name="isCurrentPage"></param>
        public PaginationItemModel(int pageNumber, bool isCurrentPage = false)
        {
            PageNumber = pageNumber;
            IsCurrentPage = isCurrentPage;
        }
    }
}
