using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.WMSocketClient
{
    /// <summary>
    /// GetData数据模型
    /// </summary>
    public class GetDataModel
    {
        /// <summary>
        /// 机架数量
        /// </summary>
        public float RackCount { get; set; }

        /// <summary>
        /// 测量数据报警状态（0表示无报警，1表示有报警）
        /// </summary>
        public float MeasureAlarmStatus { get; set; }

        /// <summary>
        /// 其余报警状态（0表示无报警，1表示有报警）
        /// </summary>
        public float OtherAlarmStatus { get; set; }

        /// <summary>
        /// 软件可工作状态（0表示没有正常测量，1表示在正常测量）
        /// </summary>
        public float WorkStatus { get; set; }

        /// <summary>
        /// 当前分区数
        /// </summary>
        public float CurrentPartitionCount { get; set; }

        /// <summary>
        /// 空气AD值数组（索引0对应1#探头，索引1对应2#探头，依此类推）
        /// </summary>
        public float[]? AirADValues { get; set; }

        /// <summary>
        /// 样本AD值数组（索引0对应1#探头，索引1对应2#探头，依此类推）
        /// </summary>
        public float[]? SampleADValues { get; set; }

        /// <summary>
        /// 当前扫描趟数数组（索引0对应1#探头，索引1对应2#探头，依此类推）
        /// </summary>
        public float[]? PassCounts { get; set; }

        /// <summary>
        /// 当前扫描类型数组（0为未扫描，1为同步扫描，2为自由扫描）
        /// </summary>
        public float[]? ScanTypes { get; set; }

        /// <summary>
        /// 当前机器状态数组（状态码由测量软件定义）
        /// </summary>
        public float[]? MachineStatus { get; set; }

        /// <summary>
        /// 行程起点数组（系统设置中的）
        /// </summary>
        public float[]? StrokeStart { get; set; }

        /// <summary>
        /// 行程终点数组（系统设置中的）
        /// </summary>
        public float[]? StrokeEnd { get; set; }

        /// <summary>
        /// 扫描方向数组（0为正向，1为反向）
        /// </summary>
        public float[]? ScanDirections { get; set; }

        /// <summary>
        /// 测量结果异常报警数组（0表示无报警，1表示有报警）
        /// </summary>
        public float[]? MeasureAbnormalAlarm { get; set; }

        /// <summary>
        /// 放射源开启状态数组（0为关闭，1为开启）
        /// </summary>
        public float[]? SourceStatus { get; set; }

        /// <summary>
        /// 扫描速度（系统设置中的）
        /// </summary>
        public float ScanSpeed { get; set; }

        /// <summary>
        /// 涂布速度（系统设置中其他设置下的"涂布速度"）
        /// </summary>
        public float CoatingSpeed { get; set; }

        /// <summary>
        /// 膜片宽度（系统设置中的）
        /// </summary>
        public float MembraneWidth { get; set; }

        /// <summary>
        /// 静态解析方法
        /// </summary>
        /// <param name="data">原始字节数据</param>
        /// <param name="rackCount">探头数量（1-5）</param>
        /// <returns>解析后的设备状态数据对象</returns>
        /// <exception cref="ArgumentException">当数据为空或探头数量不在1-5之间时抛出</exception>
        public static GetDataModel Parse(byte[] data, float rackCount)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("数据不能为空");

            int rackCountInt = (int)rackCount;
            if (rackCountInt < 1 || rackCountInt > 5)
                throw new ArgumentException("探头数量必须在1-5之间");

            int offset = 0;
            var result = new GetDataModel
            {
                RackCount = rackCount
            };

            // 解析固定字段
            result.MeasureAlarmStatus = BitConverter.ToSingle(data, offset);
            offset += 4;
            result.OtherAlarmStatus = BitConverter.ToSingle(data, offset);
            offset += 4;
            result.WorkStatus = BitConverter.ToSingle(data, offset);
            offset += 4;
            result.CurrentPartitionCount = BitConverter.ToSingle(data, offset);
            offset += 4;

            // 解析空气AD值
            result.AirADValues = new float[rackCountInt];
            for (int i = 0; i < rackCountInt; i++)
            {
                result.AirADValues[i] = BitConverter.ToSingle(data, offset);
                offset += 4;
            }

            // 解析样本AD值
            result.SampleADValues = new float[rackCountInt];
            for (int i = 0; i < rackCountInt; i++)
            {
                result.SampleADValues[i] = BitConverter.ToSingle(data, offset);
                offset += 4;
            }

            // 解析扫描趟数
            result.PassCounts = new float[rackCountInt];
            for (int i = 0; i < rackCountInt; i++)
            {
                result.PassCounts[i] = BitConverter.ToSingle(data, offset);
                offset += 4;
            }

            // 解析扫描类型
            result.ScanTypes = new float[rackCountInt];
            for (int i = 0; i < rackCountInt; i++)
            {
                result.ScanTypes[i] = data[offset++];
            }

            // 解析机器状态
            result.MachineStatus = new float[rackCountInt];
            for (int i = 0; i < rackCountInt; i++)
            {
                result.MachineStatus[i] = BitConverter.ToSingle(data, offset);
                offset += 4;
            }

            // 解析扫描速度
            result.ScanSpeed = BitConverter.ToSingle(data, offset);
            offset += 4;

            // 解析行程起/终点
            result.StrokeStart = new float[rackCountInt];
            result.StrokeEnd = new float[rackCountInt];
            for (int i = 0; i < rackCountInt; i++)
            {
                result.StrokeStart[i] = BitConverter.ToSingle(data, offset);
                offset += 4;
                result.StrokeEnd[i] = BitConverter.ToSingle(data, offset);
                offset += 4;
            }

            // 解析扫描方向
            result.ScanDirections = new float[rackCountInt];
            for (int i = 0; i < rackCountInt; i++)
            {
                result.ScanDirections[i] = data[offset++];
            }

            // 解析测量结果异常报警
            result.MeasureAbnormalAlarm = new float[rackCountInt];
            for (int i = 0; i < rackCountInt; i++)
            {
                result.MeasureAbnormalAlarm[i] = data[offset++];
            }

            // 解析涂布速度
            result.CoatingSpeed = BitConverter.ToSingle(data, offset);
            offset += 4;

            // 解析膜片宽度
            result.MembraneWidth = BitConverter.ToSingle(data, offset);
            offset += 4;

            // 解析放射源开启状态
            result.SourceStatus = new float[rackCountInt];
            for (int i = 0; i < rackCountInt; i++)
            {
                result.SourceStatus[i] = data[offset++];
            }

            return result;
        }

        #region GetData解析
        //1、测量数据报警状态（0表示无报警，1表示有报警）（30 - 34是这个信号的细分，此信号仍然要提供）
        //2、其余报警状态（0表示无报警，1表示有报警）
        //3、软件可工作状态（0表示没有正常测量，1表示在正常测量（停机自动停止扫描的情况下，一走带即可继续测量的情况，也算是正常测量））
        //4、当前分区数
        //5、1#空气AD值
        //6、2#空气AD值（不能提供时给0）
        //7、3#空气AD值（不能提供时给0）
        //8、4#空气AD值（不能提供时给0）
        //9、5#空气AD值（不能提供时给0）
        //10、1#样本AD值
        //11、2#样本AD值（不能提供时给0）
        //12、3#样本AD值（不能提供时给0）
        //13、4#样本AD值（不能提供时给0）
        //14、5#样本AD值（不能提供时给0）
        //15、1#当前扫描趟数
        //16、2#当前扫描趟数（不能提供时给0）
        //17、3#当前扫描趟数（不能提供时给0）
        //18、4#当前扫描趟数（不能提供时给0）
        //19、5#当前扫描趟数（不能提供时给0）
        //20、1#当前扫描类型（0为未扫描，1为同步扫描，2为自由扫描）
        //21、2#当前扫描类型（不能提供时给0）
        //22、3#当前扫描类型（不能提供时给0）
        //23、4#当前扫描类型（不能提供时给0）
        //24、5#当前扫描类型（不能提供时给0）
        //25、1#当前机器状态（此状态码是测量软件给我的，每个状态码表示一种状态）
        //26、2#当前机器状态（不能提供时给0）
        //27、3#当前机器状态（不能提供时给0）
        //28、4#当前机器状态（不能提供时给0）
        //29、5#当前机器状态（不能提供时给0）
        //30、扫描速度（系统设置中的）（不能提供时给0）
        //31、1#行程起点（系统设置中的）（不能提供时给0）
        //32、1#行程终点（系统设置中的）（不能提供时给0）
        //33、2#行程起点（系统设置中的）（不能提供时给0）
        //34、2#行程终点（系统设置中的）（不能提供时给0）
        //35、3#行程起点（系统设置中的）（不能提供时给0）
        //36、3#行程终点（系统设置中的）（不能提供时给0）
        //37、4#行程起点（系统设置中的）（不能提供时给0）
        //38、4#行程终点（系统设置中的）（不能提供时给0）
        //39、5#行程起点（系统设置中的）（不能提供时给0）
        //40、5#行程终点（系统设置中的）（不能提供时给0）
        //41、1#扫描方向（扫描趟数对应的扫描方向，0为正向，1为反向）（不能提供时给0）
        //42、2#扫描方向（扫描趟数对应的扫描方向，0为正向，1为反向）（不能提供时给0）
        //43、3#扫描方向（扫描趟数对应的扫描方向，0为正向，1为反向）（不能提供时给0）
        //44、4#扫描方向（扫描趟数对应的扫描方向，0为正向，1为反向）（不能提供时给0）
        //45、5#扫描方向（扫描趟数对应的扫描方向，0为正向，1为反向）（不能提供时给0）
        //46、1#测量结果异常报警（0表示无报警，1表示有报警）（不能提供时给0）
        //47、2#测量结果异常报警（0表示无报警，1表示有报警）（不能提供时给0）
        //48、3#测量结果异常报警（0表示无报警，1表示有报警）（不能提供时给0）
        //49、4#测量结果异常报警（0表示无报警，1表示有报警）（不能提供时给0）
        //50、5#测量结果异常报警（0表示无报警，1表示有报警）（不能提供时给0）
        //51、系统设置中，其他设置下的“涂布速度”（不能提供时给0）
        //52、系统设置中，“膜片宽度”（不能提供时给0）
        //53、1#放射源开启状态（0为关闭，1为开启）（不能提供时给0）
        //54、2#放射源开启状态（0为关闭，1为开启）（不能提供时给0）
        //55、3#放射源开启状态（0为关闭，1为开启）（不能提供时给0）
        //56、4#放射源开启状态（0为关闭，1为开启）（不能提供时给0）
        //57、5#放射源开启状态（0为关闭，1为开启）（不能提供时给0）
        #endregion
    }
}