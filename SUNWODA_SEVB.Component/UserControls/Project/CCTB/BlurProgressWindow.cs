using HandyControl.Controls;
using Microsoft.VisualBasic;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SUNWODA_SEVB.Component.UserControls.Project.CCTB
{
    public class BlurProgressWindow : IDisposable
    {
        private readonly Dispatcher _dispatcher;
        private readonly BlurWindow _blurWindow;
        private readonly ProgressBar _progressBar;
        private readonly TextBlock _progressText;
        private bool _isDisposed;

        public BlurProgressWindow(string title = "处理进度")
        {
            _dispatcher = Application.Current.Dispatcher;

            // 创建毛玻璃窗口
            _blurWindow = new BlurWindow
            {
                Title = title,
                Width = 400,
                Height = 80,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Topmost = true,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false
            };

            // 创建进度条控件
            _progressBar = new ProgressBar
            {
                Height = 20,
                Width = 360,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 10, 0, 0),
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };

            // 创建进度文本
            _progressText = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 10),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Text = "正在初始化..."
            };

            // 创建布局容器
            var grid = new Grid();
            grid.Children.Add(_progressBar);
            grid.Children.Add(_progressText);

            _blurWindow.Content = grid;
        }

        /// <summary>
        /// 显示窗口
        /// </summary>
        public void Show()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(BlurProgressWindow));
            _blurWindow.Show();
        }

        /// <summary>
        /// 异步更新进度
        /// </summary>
        public async Task UpdateProgressAsync(double progress, string? operation = null, string? information = null)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(BlurProgressWindow));

            if (_dispatcher.CheckAccess())
            {
                UpdateProgressInternal(progress, operation, information);
            }
            else
            {
                await _dispatcher.InvokeAsync(() => UpdateProgressInternal(progress, operation, information));
            }
        }

        /// <summary>
        /// 同步更新进度（在UI线程中调用）
        /// </summary>
        public void UpdateProgress(double progress, string? operation = null, string? information = null)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(BlurProgressWindow));

            if (!_dispatcher.CheckAccess())
            {
                throw new InvalidOperationException("必须在UI线程中调用此方法");
            }

            UpdateProgressInternal(progress, operation, information);
        }

        private void UpdateProgressInternal(double progress, string? operation, string? information)
        {
            // 限制进度范围
            progress = Math.Max(_progressBar.Minimum, Math.Min(_progressBar.Maximum, progress));

            // 只有当值变化时才更新，减少UI刷新
            if (Math.Abs(_progressBar.Value - progress) > 0.01)
            {
                _progressBar.Value = progress;
            }

            // 构建显示文本
            var textParts = new List<string>();

            if (!string.IsNullOrEmpty(operation))
            {
                textParts.Add(operation);
            }

            if (!string.IsNullOrEmpty(information))
            {
                textParts.Add(information);
            }

            if (textParts.Count == 0)
            {
                textParts.Add($"进度: {progress:F1}%");
            }
            else
            {
                textParts.Add($"{progress:F1}%");
            }

            _progressText.Text = string.Join(" - ", textParts);
        }

        /// <summary>
        /// 异步关闭窗口
        /// </summary>
        public async Task CloseAsync()
        {
            if (_isDisposed) return;

            if (_dispatcher.CheckAccess())
            {
                CloseInternal();
            }
            else
            {
                await _dispatcher.InvokeAsync(CloseInternal);
            }
        }

        /// <summary>
        /// 同步关闭窗口（在UI线程中调用）
        /// </summary>
        public void Close()
        {
            if (_isDisposed) return;

            if (!_dispatcher.CheckAccess())
            {
                throw new InvalidOperationException("必须在UI线程中调用此方法");
            }

            CloseInternal();
        }

        private void CloseInternal()
        {
            if (_blurWindow.IsVisible)
            {
                _blurWindow.Close();
            }
        }

        /// <summary>
        /// 设置窗口位置
        /// </summary>
        public void SetPosition(double left, double top)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(BlurProgressWindow));

            if (_dispatcher.CheckAccess())
            {
                _blurWindow.Left = left;
                _blurWindow.Top = top;
            }
            else
            {
                _dispatcher.Invoke(() =>
                {
                    _blurWindow.Left = left;
                    _blurWindow.Top = top;
                });
            }
        }

        #region IDisposable Implementation

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    try
                    {
                        if (_dispatcher.CheckAccess())
                        {
                            CloseInternal();
                        }
                        else
                        {
                            // 同步等待关闭，避免资源泄漏
                            _dispatcher.Invoke(CloseInternal);
                        }
                    }
                    catch
                    {
                        // 忽略关闭时的异常
                    }
                }

                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
