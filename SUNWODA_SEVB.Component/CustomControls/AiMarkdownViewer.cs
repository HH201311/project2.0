using SUNWODA_SEVB.Core.Common.Commands;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SUNWODA_SEVB.Component.CustomControls
{
    /// <summary>
    /// AiMarkdownViewer
    /// </summary>
    public class AiMarkdownViewer : Control
    {
        static AiMarkdownViewer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AiMarkdownViewer),
                new FrameworkPropertyMetadata(typeof(AiMarkdownViewer)));
        }

        #region 依赖属性

        public static readonly DependencyProperty MarkdownTextProperty =
            DependencyProperty.Register("MarkdownText", typeof(string), typeof(AiMarkdownViewer),
                new PropertyMetadata(string.Empty, OnMarkdownTextChanged));

        public static readonly DependencyProperty IsStreamingProperty =
            DependencyProperty.Register("IsStreaming", typeof(bool), typeof(AiMarkdownViewer),
                new PropertyMetadata(false));

        public static readonly DependencyProperty ShowThinkingProperty =
            DependencyProperty.Register("ShowThinking", typeof(bool), typeof(AiMarkdownViewer),
                new PropertyMetadata(true, OnShowThinkingChanged));

        public static readonly DependencyProperty ThinkingContentProperty =
            DependencyProperty.Register("ThinkingContent", typeof(string), typeof(AiMarkdownViewer),
                new PropertyMetadata(string.Empty, OnThinkingContentChanged));

        public static readonly DependencyProperty StreamingSpeedProperty =
            DependencyProperty.Register("StreamingSpeed", typeof(int), typeof(AiMarkdownViewer),
                new PropertyMetadata(30));

        public static readonly DependencyProperty IsThinkingExpandedProperty =
            DependencyProperty.Register("IsThinkingExpanded", typeof(bool), typeof(AiMarkdownViewer),
                new PropertyMetadata(true));

        public static readonly DependencyProperty HasThinkingContentProperty =
            DependencyProperty.Register("HasThinkingContent", typeof(bool), typeof(AiMarkdownViewer),
                new PropertyMetadata(false));

        #endregion

        #region 属性

        public string MarkdownText
        {
            get { return (string)GetValue(MarkdownTextProperty); }
            set { SetValue(MarkdownTextProperty, value); }
        }

        public bool IsStreaming
        {
            get { return (bool)GetValue(IsStreamingProperty); }
            set { SetValue(IsStreamingProperty, value); }
        }

        public bool ShowThinking
        {
            get { return (bool)GetValue(ShowThinkingProperty); }
            set { SetValue(ShowThinkingProperty, value); }
        }

        public string ThinkingContent
        {
            get { return (string)GetValue(ThinkingContentProperty); }
            set { SetValue(ThinkingContentProperty, value); }
        }

        public int StreamingSpeed
        {
            get { return (int)GetValue(StreamingSpeedProperty); }
            set { SetValue(StreamingSpeedProperty, value); }
        }

        public bool IsThinkingExpanded
        {
            get { return (bool)GetValue(IsThinkingExpandedProperty); }
            set { SetValue(IsThinkingExpandedProperty, value); }
        }

        public bool HasThinkingContent
        {
            get { return (bool)GetValue(HasThinkingContentProperty); }
            set { SetValue(HasThinkingContentProperty, value); }
        }

        #endregion

        #region 私有字段

        private FlowDocumentScrollViewer? _documentViewer;
        private ScrollViewer? _documentScrollViewer;
        private Border? _thinkingContainer;
        private TextBlock? _thinkingTextBlock;
        private Button? _thinkingToggleButton;
        private ScrollViewer? _thinkingScrollViewer;
        private DispatcherTimer? _streamTimer;
        private Queue<string>? _streamQueue;
        private StringBuilder? _currentContent;
        private FlowDocument? _document;
        private DispatcherTimer? _updateTimer;
        private bool _pendingUpdate = false;
        private StringBuilder? _pendingContent;

        private Border? _mainBorder;

        #endregion

        #region 命令

        public ICommand ToggleThinkingCommand { get; private set; }

        #endregion

        public AiMarkdownViewer()
        {
            ToggleThinkingCommand = new RelayCommand(() =>
            {
                IsThinkingExpanded = !IsThinkingExpanded;
            });
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _mainBorder = GetTemplateChild("PART_MainBorder") as Border;
            _documentViewer = GetTemplateChild("PART_DocumentViewer") as FlowDocumentScrollViewer;
            _thinkingContainer = GetTemplateChild("PART_ThinkingContainer") as Border;
            _thinkingTextBlock = GetTemplateChild("PART_ThinkingText") as TextBlock;
            //_thinkingProgress = GetTemplateChild("PART_ThinkingProgress") as ProgressBar;
            _thinkingToggleButton = GetTemplateChild("PART_ThinkingToggleButton") as Button;
            _thinkingScrollViewer = GetTemplateChild("PART_ThinkingScrollViewer") as ScrollViewer;

            if (_thinkingToggleButton != null)
            {
                _thinkingToggleButton.Command = ToggleThinkingCommand;
            }

            // 获取FlowDocumentScrollViewer内部的ScrollViewer
            if (_documentViewer != null)
            {
                _documentViewer.Loaded += (s, e) =>
                {
                    _documentScrollViewer = GetDescendantByType(_documentViewer, typeof(ScrollViewer)) as ScrollViewer;
                };
            }

            InitializeStreamTimer();
            InitializeUpdateTimer();
            UpdateDocument();

            // 初始化时更新思考容器的显示状态
            UpdateThinkingVisibility();
        }

        // 获取指定类型的子元素
        private static Visual? GetDescendantByType(Visual element, Type type)
        {
            if (element == null) return null;

            if (element.GetType() == type) return element;

            Visual? foundElement = null;
            int childrenCount = VisualTreeHelper.GetChildrenCount(element);

            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(element, i) as Visual;
                if (child == null) continue;
                foundElement = GetDescendantByType(child, type);
                if (foundElement != null)
                    break;
            }

            return foundElement;
        }

        private void InitializeStreamTimer()
        {
            _streamTimer = new DispatcherTimer();
            _streamTimer.Interval = TimeSpan.FromMilliseconds(StreamingSpeed);
            _streamTimer.Tick += OnStreamTick;
            _streamQueue = new Queue<string>();
            _currentContent = new StringBuilder();
            _pendingContent = new StringBuilder();
        }

        private void InitializeUpdateTimer()
        {
            _updateTimer = new DispatcherTimer();
            _updateTimer.Interval = TimeSpan.FromMilliseconds(100);
            _updateTimer.Tick += OnUpdateTick;
        }

        private static void OnMarkdownTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as AiMarkdownViewer;
            control?.HandleMarkdownChanged(e.NewValue as string);
        }

        private static void OnThinkingContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as AiMarkdownViewer;
            control?.HandleThinkingChanged(e.NewValue as string);
        }

        private static void OnShowThinkingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as AiMarkdownViewer;
            control?.UpdateThinkingVisibility();
        }

        private void HandleMarkdownChanged(string? newMarkdown)
        {
            if (IsStreaming)
            {
                StartStreaming(newMarkdown);
            }
            else
            {
                // 清空并重新设置内容
                _currentContent?.Clear();

                if (!string.IsNullOrEmpty(newMarkdown))
                {
                    _currentContent?.Append(newMarkdown);
                }

                // 立即更新文档
                UpdateDocument();
            }
        }

        private void HandleThinkingChanged(string? thinking)
        {
            // 更新HasThinkingContent属性
            HasThinkingContent = !string.IsNullOrEmpty(thinking);

            if (_thinkingTextBlock != null)
            {
                _thinkingTextBlock.Text = thinking;

                // 自动滚动到最新的思考内容
                if (_thinkingScrollViewer != null && !string.IsNullOrEmpty(thinking))
                {
                    _thinkingScrollViewer.ScrollToBottom();
                }
            }

            // 更新思考容器的显示状态
            UpdateThinkingVisibility();
        }

        private void UpdateThinkingVisibility()
        {
            if (_thinkingContainer == null) return;

            // 只有当ShowThinking为true且有思考内容时才显示
            bool shouldShow = ShowThinking && HasThinkingContent;

            if (shouldShow)
            {
                AnimateThinkingContainer(true);
            }
            else
            {
                AnimateThinkingContainer(false);
            }
        }

        #region 流式输出优化

        public void StartStreaming(string? content)
        {
            _streamQueue?.Clear();
            _currentContent?.Clear();
            _pendingContent?.Clear();

            if (!string.IsNullOrEmpty(content))
            {
                _streamQueue?.Enqueue(content);
            }

            _streamTimer?.Start();
            _updateTimer?.Start();
            IsStreaming = true;
        }

        public void AppendStreamContent(string content)
        {
            if (!string.IsNullOrEmpty(content))
            {
                _streamQueue?.Enqueue(content);

                if (!(_streamTimer?.IsEnabled ?? false))
                {
                    _streamTimer?.Start();
                    _updateTimer?.Start();
                }
            }
        }

        public void StopStreaming()
        {
            _streamTimer?.Stop();
            _updateTimer?.Stop();
            IsStreaming = false;

            while (_streamQueue?.Count > 0)
            {
                _currentContent?.Append(_streamQueue.Dequeue());
            }

            if (_pendingContent?.Length > 0)
            {
                _currentContent?.Append(_pendingContent.ToString());
                _pendingContent.Clear();
            }

            UpdateDocument();
            ScrollToBottom();
        }

        private void OnStreamTick(object? sender, EventArgs e)
        {
            if (_streamQueue?.Count > 0)
            {
                var content = _streamQueue.Dequeue();
                _pendingContent?.Append(content);
                _pendingUpdate = true;
            }
            else
            {
                _streamTimer?.Stop();
                if (!_pendingUpdate)
                {
                    _updateTimer?.Stop();
                    IsStreaming = false;
                }
            }
        }

        private void OnUpdateTick(object? sender, EventArgs e)
        {
            if (_pendingUpdate && _pendingContent?.Length > 0)
            {
                _currentContent?.Append(_pendingContent.ToString());
                _pendingContent.Clear();
                _pendingUpdate = false;
                UpdateDocument();
                ScrollToBottom();
            }
        }

        #endregion

        #region 自动滚动

        private void ScrollToBottom()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                if (_documentScrollViewer == null && _documentViewer != null)
                {
                    _documentScrollViewer = GetDescendantByType(_documentViewer, typeof(ScrollViewer)) as ScrollViewer;
                }

                _documentScrollViewer?.ScrollToEnd();
            }));
        }

        #endregion

        #region Markdown渲染优化

        private void UpdateDocument()
        {
            if (_documentViewer == null) return;

            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                _document = new FlowDocument();
                _document.FontFamily = new FontFamily("Segoe UI");
                _document.FontSize = 14;
                _document.PagePadding = new Thickness(0);

                var markdown = _currentContent?.ToString();
                if (string.IsNullOrEmpty(markdown))
                {
                    _documentViewer.Document = _document;
                    return;
                }

                ParseMarkdown(markdown);
                _documentViewer.Document = _document;
            }));
        }

        private void ParseMarkdown(string markdown)
        {
            var lines = markdown.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var currentParagraph = new Paragraph();
            bool inCodeBlock = false;
            var codeBlock = new StringBuilder();
            string codeLanguage = "";

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (line.StartsWith("```"))
                {
                    if (!inCodeBlock)
                    {
                        inCodeBlock = true;
                        codeLanguage = line.Substring(3).Trim();
                        codeBlock.Clear();

                        if (currentParagraph.Inlines.Count > 0)
                        {
                            _document?.Blocks.Add(currentParagraph);
                            currentParagraph = new Paragraph();
                        }
                    }
                    else
                    {
                        inCodeBlock = false;
                        AddCodeBlock(codeBlock.ToString(), codeLanguage);
                    }
                    continue;
                }

                if (inCodeBlock)
                {
                    if (codeBlock.Length > 0) codeBlock.AppendLine();
                    codeBlock.Append(line);
                    continue;
                }

                if (line.StartsWith("#"))
                {
                    if (currentParagraph.Inlines.Count > 0)
                    {
                        _document?.Blocks.Add(currentParagraph);
                        currentParagraph = new Paragraph();
                    }

                    var level = line.TakeWhile(c => c == '#').Count();
                    var headerText = line.Substring(level).Trim();
                    AddHeader(headerText, level);
                }
                else if (Regex.IsMatch(line, @"^\s*[-*+]\s+"))
                {
                    if (currentParagraph.Inlines.Count > 0)
                    {
                        _document?.Blocks.Add(currentParagraph);
                        currentParagraph = new Paragraph();
                    }

                    var listItem = Regex.Replace(line, @"^\s*[-*+]\s+", "• ");
                    var listParagraph = new Paragraph(new Run(listItem));
                    listParagraph.Margin = new Thickness(20, 2, 0, 2);
                    _document?.Blocks.Add(listParagraph);
                }
                else if (line.StartsWith(">"))
                {
                    if (currentParagraph.Inlines.Count > 0)
                    {
                        _document?.Blocks.Add(currentParagraph);
                        currentParagraph = new Paragraph();
                    }

                    var quoteText = line.Substring(1).Trim();
                    AddQuote(quoteText);
                }
                else if (string.IsNullOrWhiteSpace(line))
                {
                    if (currentParagraph.Inlines.Count > 0)
                    {
                        _document?.Blocks.Add(currentParagraph);
                        currentParagraph = new Paragraph();
                    }
                }
                else
                {
                    ParseInlineMarkdown(line, currentParagraph);
                    if (i < lines.Length - 1 && !string.IsNullOrWhiteSpace(lines[i + 1]))
                    {
                        currentParagraph.Inlines.Add(new LineBreak());
                    }
                }
            }

            if (currentParagraph.Inlines.Count > 0)
            {
                _document?.Blocks.Add(currentParagraph);
            }
        }

        private void ParseInlineMarkdown(string text, Paragraph paragraph)
        {
            var pattern = @"(\*\*(.+?)\*\*)|(\*(.+?)\*)|(`(.+?)`)|(\[(.+?)\]\((.+?)\))";
            var matches = Regex.Matches(text, pattern);

            int lastIndex = 0;
            foreach (Match match in matches)
            {
                if (match.Index > lastIndex)
                {
                    paragraph.Inlines.Add(new Run(text.Substring(lastIndex, match.Index - lastIndex)));
                }

                if (match.Groups[1].Success)
                {
                    var bold = new Bold(new Run(match.Groups[2].Value));
                    paragraph.Inlines.Add(bold);
                }
                else if (match.Groups[3].Success)
                {
                    var italic = new Italic(new Run(match.Groups[4].Value));
                    paragraph.Inlines.Add(italic);
                }
                else if (match.Groups[5].Success)
                {
                    var code = new Run(match.Groups[6].Value);
                    code.Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                    code.FontFamily = new FontFamily("Consolas");
                    paragraph.Inlines.Add(code);
                }
                else if (match.Groups[7].Success)
                {
                    var hyperlink = new Hyperlink(new Run(match.Groups[8].Value));
                    hyperlink.NavigateUri = new Uri(match.Groups[9].Value, UriKind.RelativeOrAbsolute);
                    paragraph.Inlines.Add(hyperlink);
                }

                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < text.Length)
            {
                paragraph.Inlines.Add(new Run(text.Substring(lastIndex)));
            }
        }

        private void AddHeader(string text, int level)
        {
            var paragraph = new Paragraph(new Run(text));
            paragraph.FontSize = 28 - (level - 1) * 4;
            paragraph.FontWeight = FontWeights.Bold;
            paragraph.Margin = new Thickness(0, 10, 0, 5);
            _document?.Blocks.Add(paragraph);
        }

        private void AddCodeBlock(string code, string language)
        {
            var section = new Section();
            section.Background = new SolidColorBrush(Color.FromRgb(245, 245, 245));
            section.Padding = new Thickness(10);
            section.Margin = new Thickness(0, 5, 0, 5);

            if (!string.IsNullOrEmpty(language))
            {
                var langParagraph = new Paragraph(new Run(language));
                langParagraph.FontSize = 12;
                langParagraph.Foreground = new SolidColorBrush(Colors.Gray);
                section.Blocks.Add(langParagraph);
            }

            var codeParagraph = new Paragraph(new Run(code));
            codeParagraph.FontFamily = new FontFamily("Consolas");
            codeParagraph.FontSize = 13;
            section.Blocks.Add(codeParagraph);

            _document?.Blocks.Add(section);
        }

        private void AddQuote(string text)
        {
            var section = new Section();
            section.BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200));
            section.BorderThickness = new Thickness(4, 0, 0, 0);
            section.Padding = new Thickness(10, 5, 5, 5);
            section.Margin = new Thickness(10, 5, 0, 5);

            var paragraph = new Paragraph(new Run(text));
            paragraph.FontStyle = FontStyles.Italic;
            paragraph.Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100));
            section.Blocks.Add(paragraph);

            _document?.Blocks.Add(section);
        }

        #endregion

        #region 深度思考动画

        private void AnimateThinkingContainer(bool show)
        {
            if (_thinkingContainer == null) return;

            var animation = new DoubleAnimation
            {
                To = show ? 1.0 : 0.0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            _thinkingContainer.BeginAnimation(OpacityProperty, animation);
            _thinkingContainer.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        public void ClearMarkdownContent()
        {
            // 停止所有计时器
            _streamTimer?.Stop();
            _updateTimer?.Stop();

            // 清空所有缓冲区
            _currentContent?.Clear();
            _streamQueue?.Clear();
            _pendingContent?.Clear();

            // 重置状态
            _pendingUpdate = false;
            IsStreaming = false;

            // 清空文档
            UpdateDocument();
        }
    }

    /// <summary>
    /// 优化的流式输出助手类
    /// </summary>
    public class StreamingHelper
    {
        private AiMarkdownViewer _viewer;
        private StringBuilder _buffer;
        private StringBuilder _chunkBuffer;
        private int _chunkSize = 50; // 批量处理大小

        public StreamingHelper(AiMarkdownViewer viewer)
        {
            _viewer = viewer;
            _buffer = new StringBuilder();
            _chunkBuffer = new StringBuilder();
        }

        public void ClearMarkdownContent()
        {
            _viewer.ClearMarkdownContent();
        }

        public void AppendToken(string token)
        {
            _buffer.Append(token);
            _chunkBuffer.Append(token);

            // 批量发送以提高性能
            if (_chunkBuffer.Length >= _chunkSize)
            {
                _viewer.AppendStreamContent(_chunkBuffer.ToString());
                _chunkBuffer.Clear();
            }
        }

        public void Complete()
        {
            // 发送剩余内容
            if (_chunkBuffer.Length > 0)
            {
                _viewer.AppendStreamContent(_chunkBuffer.ToString());
                _chunkBuffer.Clear();
            }
            _viewer.StopStreaming();
        }

        public string GetFullContent()
        {
            return _buffer.ToString();
        }
    }
}
