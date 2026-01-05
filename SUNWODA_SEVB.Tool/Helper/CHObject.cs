
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Helper
{
    public class CHObject : IDisposable
    {

        public int LineWidth { get; set; }
        public string LineStyle { get; set; }

        /// <summary>
        /// Halcon对象
        /// </summary>
        public HObject HObj { get; set; }

        /// <summary>
        /// 对象颜色
        /// </summary>
        public string Color { get; set; }

        private bool disposed = false;
        public CHObject(HObject hObject)
        {
            HObj = hObject;
            Color = "green";
        }
        public CHObject(CHObject original)
        {
            Color = original.Color;
            HObj = original.HObj.Clone();
        }
        public CHObject(HObject hObject, string color)
        {
            HObj = hObject.Clone();
            Color = color;
        }
        public CHObject(HObject hObject, string color, int lineWidth = 1, string lineStyle = "solid")
        {
            HObj = hObject.Clone();
            Color = color;
            LineWidth = lineWidth;
            LineStyle = lineStyle;
            hObject.Dispose();
        }
        // 深拷贝方法
        public CHObject Clone()
        {
            return new CHObject(this);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // 释放托管资源
                    if (HObj != null)
                    {
                        HObj.Dispose();
                        HObj = null;
                    }
                    Color = null;
                }

                // 释放非托管资源（如果有的话）

                disposed = true;
            }
        }

        ~CHObject()
        {
            Dispose(false);
        }
    }
}
