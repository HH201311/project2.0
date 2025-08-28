using SUNWODA_SEVB.Component.CustomControls;
using SUNWODA_SEVB.Component.UserControls;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.ViewModels.Windows.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace SUNWODA_SEVB.ViewModels.Pages.Demo
{
    [Module("AIMarkdownViewerTestPage", "AI Markdown控件演示")]
    public class VM_AIMarkdownViewerTestPage : ViewModelBase
    {
        private readonly ILoggerService<VM_MainWindow> _logger;
        private readonly INavigationService _navigationService;
        private readonly IModuleManager _moduleManager;
        private string? _markdownContent;
        private string? _thinkingContent;
        private bool _isStreaming;
        private bool _initializedView = false;
        private AiMarkdownViewer? _markdownViewer;

        #region 属性

        public string? MarkdownContent
        {
            get => _markdownContent;
            set => SetProperty(ref _markdownContent, value);
        }

        public string? ThinkingContent
        {
            get => _thinkingContent;
            set => SetProperty(ref _thinkingContent, value);
        }

        public bool IsStreaming
        {
            get => _isStreaming;
            set => SetProperty(ref _isStreaming, value);
        }

        #endregion

        #region 命令

        public ICommand SimulateAiResponseCommand { get; }
        public ICommand SimulateThinkingCommand { get; }
        public ICommand TestMarkdownCommand { get; }
        public ICommand ClearAllCommand { get; }

        #endregion

        public VM_AIMarkdownViewerTestPage(
            ILoggerService<VM_MainWindow> logger,
            INavigationService navigationService,
            IModuleManager moduleManager)
        {
            _logger = logger;
            _navigationService = navigationService;
            _moduleManager = moduleManager;
            // 初始化命令
            SimulateAiResponseCommand = new RelayCommand(async () => await SimulateAiResponse());
            SimulateThinkingCommand = new RelayCommand(async () => await SimulateThinking());
            TestMarkdownCommand = new RelayCommand(TestMarkdownFeatures);
            ClearAllCommand = new RelayCommand(ClearAll);
        }

        protected override async Task OnNavigatedToAsync(object? parameter)
        {
            if (!_initializedView)
            {
                var moduleName = typeof(VM_AIMarkdownViewerTestPage).GetCustomAttribute<ModuleAttribute>()?.Name;
                if (moduleName != null)
                {
                    var view = _moduleManager.GetViewFromService(moduleName);

                    await RunOnUIThreadAsync(() =>
                    {
                        _markdownViewer = view?.FindName("MarkdownViewer") as AiMarkdownViewer;
                    });
                }
                _initializedView = true;
            }

            await base.OnNavigatedToAsync(parameter);
        }

        #region 方法

        /// <summary>
        /// 模拟AI流式响应
        /// </summary>
        private async Task SimulateAiResponse()
        {
            IsStreaming = true;

            string aiResponse =
                @"# AI响应示例

我正在为您生成一个**详细的响应**。这个响应包含了多种Markdown元素。

## 主要特性

1. **实时渲染** - 支持Markdown的实时渲染
2. *流式输出* - 模拟AI的逐字输出效果
3. `代码高亮` - 支持内联代码和代码块

### 代码示例

```csharp
public class Example
{
    public void HelloWorld()
    {
        Console.WriteLine(""Hello, World!"");
    }
}
```

## 引用示例

> 这是一个引用块，通常用于引用其他来源的内容。
> 可以包含多行文本。

## 列表示例

### 无序列表
- 第一项
- 第二项
  - 子项目1
  - 子项目2
- 第三项

### 有序列表
1. 步骤一
2. 步骤二
3. 步骤三

## 链接和图片

访问 [百度](https://www.baidu.com) 获取更多信息。";

            // 模拟流式输出
            var helper = new StreamingHelper(
                _markdownViewer ?? throw new ArgumentNullException(nameof(_markdownViewer))
            );
            foreach (char c in aiResponse)
            {
                helper.AppendToken(c.ToString());
                await Task.Delay(10); // 模拟网络延迟
            }

            helper.Complete();
            IsStreaming = false;
        }

        /// <summary>
        /// 模拟深度思考
        /// </summary>
        private async Task SimulateThinking()
        {
            // 清空之前的思考内容
            ClearAll();

            // 逐步添加思考内容
            ThinkingContent = "🤔 正在分析您的问题...\n";
            await Task.Delay(800);

            ThinkingContent += "📚 搜索相关知识库...\n";
            await Task.Delay(800);

            ThinkingContent += "🔗 构建逻辑链条...\n";
            await Task.Delay(800);

            ThinkingContent += "💡 生成响应方案...\n";
            await Task.Delay(800);

            ThinkingContent += "✨ 优化输出内容...\n";
            await Task.Delay(1000);

            ThinkingContent += "\n✅ 思考完成！开始生成响应...\n";

            // 开始输出响应，但保留思考内容
            await SimulateAiResponse();
        }

        /// <summary>
        /// 清空思考内容
        /// </summary>
        private void ClearAll()
        {
            IsStreaming = false;
            // 清空思考内容
            ThinkingContent = "";
            MarkdownContent = "";
            _markdownViewer?.ClearMarkdownContent();
        }

        /// <summary>
        /// 测试Markdown功能
        /// </summary>
        private void TestMarkdownFeatures()
        {
            //ClearAll();

            // 等待一帧，确保UI更新
            //await Task.Delay(50);

            MarkdownContent =
                @"# Markdown功能测试

## 文本格式化

这是**粗体文本**，这是*斜体文本*。

这是`内联代码`，适合标记变量名或短代码。

## 代码块

```csharp
public class Example
{
    public void HelloWorld()
    {
        Console.WriteLine(""Hello, World!"");
    }
}
```

## 任务列表（扩展功能）

- [x] 实现基础Markdown渲染
- [x] 支持流式输出
- [x] 添加深度思考显示
- [ ] 支持数学公式
- [ ] 支持表格渲染
- [ ] 支持Mermaid图表

> 这是一个引用";
        }
        #endregion
    }
}
