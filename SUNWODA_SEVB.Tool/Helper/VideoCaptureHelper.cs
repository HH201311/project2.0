using OpenCvSharp;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Tool.Extension;
using System;
using System.Globalization;
using System.IO;
using System.Windows;
using Size = OpenCvSharp.Size;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// 数据类型转换助手
    /// </summary>
    public class VideoCaptureHelper
    {
        private string? name;
        private bool isRelease;
        private string rtspUrl;
        // 自动重加重连
        private bool isReconnecting;

        /// <summary>
        /// 名称
        /// </summary>
        public string? Name { get { return name; } }

        /// <summary>
        /// RTSP地址
        /// </summary>
        public string RtspUrl { get { return rtspUrl; } }

        public VideoCapture? videoCapture;

        private readonly ILoggerService _logger;

        private CancellationTokenSource? _ctsConnect;

        private readonly object lockObj = new();

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name"></param>
        /// <param name="rtspUrl"></param>
        /// <param name="isReconnect"></param>
        public VideoCaptureHelper(ILoggerService logger, string? name, string rtspUrl)
        {
            _logger = logger;
            this.name = name;
            this.rtspUrl = rtspUrl;
        }

        /// <summary>
        /// 连接
        /// </summary>
        public void Connect()
        {
            // 已连接，则退出
            if ((videoCapture?.IsOpened() ?? false)) return;
            // 正在重连则退出
            if (isReconnecting) return;
            isReconnecting = true;
            // 重置标识
            isRelease = false;
            _ctsConnect = new CancellationTokenSource();
            // 重新连接线程
            var t = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        // 1、优雅退出
                        if (_ctsConnect.IsCancellationRequested)
                        {
                            _logger.Info($"相机[{Name} connectStrings:{rtspUrl}]已退出重连任务！");
                            break;
                        }

                        // 2、已连接则退出
                        if (videoCapture?.IsOpened() ?? false) continue;

                        // 3、相机连接
                        videoCapture?.Dispose();
                        videoCapture = new VideoCapture(rtspUrl);
                        // 优化缓冲区和超时设置
                        videoCapture.Set(VideoCaptureProperties.BufferSize, 1);
                        _logger.Info($"相机[{Name} connectStrings:{rtspUrl}]连接[{videoCapture?.IsOpened() ?? false}]");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"相机[{Name} connectStrings:{rtspUrl}]重连异常！", ex);
                    }
                    finally
                    {
                        Thread.Sleep(5000);
                    }
                }
                // 退出重连任务后，重置重连标识
                isReconnecting = false;
            });
            t.IsBackground = true;
            t.Start();
        }

        /// <summary>
        /// 断开
        /// </summary>
        public void DisConnect()
        {
            StopReading();
            ExecuteFrame = null;
            videoCapture?.Dispose();
            videoCapture = null;
        }

        /// <summary>
        /// 摄像头是否打开
        /// </summary>
        public bool IsOpened { get { return videoCapture?.IsOpened() ?? false; } }             

        #region 视频流操作
        private CancellationTokenSource? cts = new CancellationTokenSource();
        /// <summary>
        /// 当视频流读取时操作
        /// </summary>
        /// <param name="frame"></param>
        public delegate void OnReadingFrame(Mat frame);
        /// <summary>
        /// 当视频流读取时操作-建议异步
        /// </summary>
        public event OnReadingFrame? ExecuteFrame;

        /// <summary>
        /// 开始读视频流
        /// </summary>
        public void StartReading()
        {
            lock (lockObj)             
            {
                cts = new CancellationTokenSource();
                VideoReadingAsync(ExecuteFrame, cts.Token);
            }            
        }

        /// <summary>
        /// 停止读视频流
        /// </summary>
        public void StopReading()
        {
            cts?.Cancel();
            cts = null;
        }
        #endregion

        /// <summary>
        /// 视频流读取
        /// </summary>
        /// <param name="action"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async void VideoReadingAsync(OnReadingFrame? executeFreame, CancellationToken ct)
        {
            var t = Task.Factory.StartNew(async (obj) =>
            {
                var _action = obj as OnReadingFrame;
                using (var frame = new Mat())
                {
                    while (!isRelease)
                    {
                        try
                        {
                            // 若取消则退出
                            if (ct.IsCancellationRequested) break;

                            // 摄像头未打开
                            if ((videoCapture?.IsDisposed ?? true) || !(videoCapture?.IsOpened() ?? false))
                            {
                                _logger.Error($"相机[{Name} connectStrings:{rtspUrl}]打开失败，等待重连！");
                                await Task.Delay(5000);
                                continue;
                            }
                            // 读取视频帧
                            if (!videoCapture.Read(frame) || frame.Empty())
                            {
                                _logger.Error($"相机[{Name} connectStrings:{rtspUrl}]读取视频帧失败！");
                                continue;
                            }
                            // 执行委托
                            _action?.Invoke(frame.Clone());
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex.Message);
                        }
                        finally
                        {
                            await Task.Delay(1);
                        }
                    }
                }
            }, executeFreame, ct);
            await t;
        }

        /// <summary>
        /// 视频流读取
        /// </summary>
        /// <param name="action"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async void VideoReadingAsync(Action<Mat> action, CancellationToken ct)
        {
            var t = Task.Factory.StartNew(async (obj) =>
            {
                try
                {
                    var _action = obj as Action<Mat>;
                    using (var frame = new Mat())
                    {
                        while (!isRelease)
                        {
                            // 若取消则退出
                            if (ct.IsCancellationRequested) break;
                            if ((videoCapture?.IsDisposed ?? true) || !(videoCapture?.IsOpened() ?? false))
                            {
                                _logger.Error($"相机[{Name} connectStrings:{rtspUrl}]打开失败，等待重连！");
                                await Task.Delay(1000);
                                continue;
                            }
                            // 读取视频帧
                            if (!videoCapture.Read(frame) || frame.Empty())
                            {
                                _logger.Error($"相机[{Name} connectStrings:{rtspUrl}]读取视频帧失败！");
                                continue;
                            }
                            // 执行委托
                            _action?.Invoke(frame.Clone());
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex.Message);
                    throw;
                }
            }, action, ct);
            await t;
        }        

        /// <summary>
        /// 释放
        /// </summary>
        public void Dispose()
        {
            this.isRelease = true;
            ExecuteFrame = null;
            videoCapture?.Dispose();
            videoCapture = null;
        }
    }
}
