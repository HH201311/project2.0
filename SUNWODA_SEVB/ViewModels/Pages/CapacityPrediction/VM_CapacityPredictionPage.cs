using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using HalconDotNet;
using HandyControl.Controls;
using HandyControl.Tools.Extension;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Python.Runtime;
using SqlSugar;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Web;
using SUNWODA_SEVB.Data.Models;
using SUNWODA_SEVB.Data.Repositories;
using SUNWODA_SEVB.Tool.Helper;
using MessageBox = HandyControl.Controls.MessageBox;
using Row = DocumentFormat.OpenXml.Spreadsheet.Row;
using Text = DocumentFormat.OpenXml.Spreadsheet.Text;

namespace SUNWODA_SEVB.ViewModels.Pages.CapacityPrediction
{
    [Module("VM_CapacityPredictionPage", "容量预测", Order = 0)]
    class VM_CapacityPredictionPage : ViewModelBase
    {
        private readonly ILoggerService<VM_CapacityPredictionPage> _logger;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;

        #region 界面绑定数据源

        private string? directoryFilePath;
        private string? filePath;
        private string? outFilePath = Environment.CurrentDirectory.Replace("net8.0-windows", "拆分结果");

        /// <summary>
        /// 加载文件夹路径
        /// </summary>
        public string? DirectoryFilePath
        {
            get => directoryFilePath;
            set => SetProperty(ref directoryFilePath, value);
        }

        /// <summary>
        /// 加载文件路径
        /// </summary>
        public string? FilePath
        {
            get => filePath;
            set => SetProperty(ref filePath, value);
        }

        /// <summary>
        /// 导出文件路径
        /// </summary>
        public string? OutFilePath
        {
            get => outFilePath;
            set => SetProperty(ref outFilePath, value);
        }

        #endregion

        BlockingCollection<string> ExcelHandleBC { get; set; } = [];
        BlockingCollection<string> DataALGHandleBC { get; set; } = [];

        /// <summary>
        /// 构造函数
        /// </summary>        
        public VM_CapacityPredictionPage
        (
            ILoggerService<VM_CapacityPredictionPage> logger,
            IGlobalSettingRepository globalSettingRepository,
            IPLCConfigRepository plcConfigRepository,
            IWebApiClient webApiClient,
            IProjectSettingRepository projectSettingRepository,
            IModuleManager moduleManager
        )
        {
            _logger = logger;
            _globalSettingRepository = globalSettingRepository;
            _plcConfigRepository = plcConfigRepository;
            _webApiClient = webApiClient;
            _projectSettingRepository = projectSettingRepository;
            _moduleManager = moduleManager;

            Init_Process();
        }

        /// <summary>
        /// 初始化进程
        /// </summary>
        void Init_Process()
        {
            // 创建文件夹
            Directory.CreateDirectory(OutFilePath);
            
            // 消费者线程----处理Excel文件拆分
            Task.Run(async () =>
            {
                foreach (var excelFile in ExcelHandleBC.GetConsumingEnumerable())
                {
                    try
                    {
                        using var doc = SpreadsheetDocument.Open(excelFile, false);

                        var stepMap = LoadStepBarcodeMap(doc, "工步数据");

                        SplitDetailSheet(excelFile, "详细数据", stepMap, OutFilePath);
                        _logger.Info($"{excelFile}拆分完成！！！");

                        await RunOnUIThreadAsync(() =>
                        {
                            Growl.Success($"{excelFile}拆分完成！！！");
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"拆分{excelFile}文件异常!\r\n" + ex.Message);
                        MessageBox.Show($"拆分{excelFile}文件异常!\r\n" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            });

            // 消费者线程----处理算法数据上传
            Task.Run(async () =>
            {
                foreach (var excelFile in DataALGHandleBC.GetConsumingEnumerable())
                {
                    try
                    {
                        object[] ALGParam = [excelFile];
                        PythonConfigHelper.AddAction((TestALG, ALGParam));
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"{excelFile}文件算法处理异常!\r\n" + ex.Message);
                    }
                }
            });
        }

        #region 界面命令

        /// <summary>
        /// 加载文件夹中所有Excel文件
        /// </summary>
        public ICommand ExportDirectoryFile => new RelayCommand(() =>
        {
            // 选择文件夹
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                var result = MessageBox.Show
                (
                   $"请确认是否执行！",
                   "执行确认",
                   MessageBoxButton.YesNo,
                   MessageBoxImage.Question
                );
                if (result != MessageBoxResult.Yes) return;

                DirectoryFilePath = dialog.FileName;

                // 支持的文件类型
                var extensions = new[] { ".xlsx" };

                // 遍历目录下所有文件
                foreach (var file in Directory.GetFiles(DirectoryFilePath))
                {
                    if (extensions.Contains(Path.GetExtension(file).ToLower()))
                    {
                        ExcelHandleBC.Add(file);
                    }
                }
            }
        });

        /// <summary>
        /// 加载一个Excel文件
        /// </summary>
        public ICommand LoadFile => new RelayCommand(() =>
        {
            OpenFileDialog openFileDialog = new()
            {
                Multiselect = true,
                Filter = "Excel 文件 (*.xlsx)|*.xlsx"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var result = MessageBox.Show
                (
                   $"请确认是否执行！",
                   "执行确认",
                   MessageBoxButton.YesNo,
                   MessageBoxImage.Question
                );
                if (result != MessageBoxResult.Yes) return;

                foreach (var file in openFileDialog.FileNames)
                {
                    FilePath = file;
                    ExcelHandleBC.Add(file);
                }
            }
        });

        /// <summary>
        /// 选择导出文件夹路径
        /// </summary>
        public ICommand ExportFile => new RelayCommand(() =>
        {
            // 选择文件夹
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                var result = MessageBox.Show
                (
                   $"请确认是否执行！",
                   "执行确认",
                   MessageBoxButton.YesNo,
                   MessageBoxImage.Question
                );
                if (result != MessageBoxResult.Yes) return;

                OutFilePath = dialog.FileName;
            }
        });

        #endregion


        /// <summary>
        /// 从指定工作表中读取【工步号 → 条码】映射关系
        /// </summary>
        /// <remarks>
        /// 1. 打开指定 Sheet
        /// 2. 跳过表头行
        /// 3. 读取固定列（工步列、条码列）
        /// 4. 构建 Dictionary：Key=工步号，Value=条码
        /// 5. 用于后续按工步号拆分并按条码命名文件
        /// <remarks>
        Dictionary<string, string> LoadStepBarcodeMap(SpreadsheetDocument doc, string sheetName)
        {
            var map = new Dictionary<string, string>();
            var wbPart = doc.WorkbookPart!;
            var sheet = wbPart.Workbook.Sheets!.Elements<Sheet>().First(s => s.Name == sheetName);

            var wsPart = (WorksheetPart)wbPart.GetPartById(sheet.Id!);
            var rows = wsPart.Worksheet.GetFirstChild<SheetData>()!.Elements<Row>();

            foreach (var row in rows.Skip(1)) // 跳过表头
            {
                var cells = row.Elements<Cell>().ToList();
                string step = GetCellValue(doc, cells[0]);
                string barcode = GetCellValue(doc, cells[2]);

                if (!string.IsNullOrWhiteSpace(step))
                    map[step] = barcode;
            }

            return map;
        }

        /// <summary>
        /// 根据工步号将“详细数据表”按固定列宽拆分成多个独立 Excel 文件
        /// </summary>
        /// <remarks>
        /// 1. 打开源 Excel 文件
        /// 2. 按 Sheet 名称获取详细数据表
        /// 3. 计算总列数、每组列数、可拆分组数
        /// 4. 每一组列视为一个工步数据块
        /// 5. 从表头读取工步号并映射条码
        /// 6. 调用 CreateSingleExcel 生成单独 Excel 文件
        /// </remarks>
        void SplitDetailSheet(string sourcePath, string detailSheetName, Dictionary<string, string> stepBarcodeMap, string outputDir)
        {
            using var doc = SpreadsheetDocument.Open(sourcePath, false);
            var wbPart = doc.WorkbookPart!;
            var sheet = wbPart.Workbook.Sheets!.Elements<Sheet>().First(s => s.Name == detailSheetName);

            var wsPart = (WorksheetPart)wbPart.GetPartById(sheet.Id!);
            var rows = wsPart.Worksheet.GetFirstChild<SheetData>()!.Elements<Row>().ToList();

            int totalColumns = rows.First().Elements<Cell>().Count();
            int groupSize = 18;
            int groupCount = totalColumns / groupSize;

            for (int g = 0; g < groupCount; g++)
            {
                int startCol = g * groupSize;

                // 假设第一行中某列存放工步号
                string stepKey = GetCellValue(doc, rows[0].Elements<Cell>().ElementAt(startCol));

                if (!stepBarcodeMap.TryGetValue(stepKey, out var barcode))
                    barcode = $"Group_{g + 1}";

                string outputPath = Path.Combine(outputDir, $"{barcode}.xlsx");
                CreateSingleExcel(rows, doc, startCol, groupSize, outputPath);
                DataALGHandleBC.Add(outputPath);
            }
        }

        /// <summary>
        /// 根据指定列范围，从源数据中生成一个新的 Excel 文件
        /// </summary>
        /// <remarks>
        /// 1. 创建新的 Workbook / Worksheet
        /// 2. 跳过源表头，从第二行开始复制数据
        /// 3. 按“列索引”逐列复制，保证缺失单元格也能补齐
        /// 4. 对缺失或特殊值进行默认处理
        /// 5. 最终生成结构完整的独立 Excel 文件
        /// </remarks>
        void CreateSingleExcel(List<Row> sourceRows, SpreadsheetDocument sourceDoc, int startCol, int colCount, string filePath)
        {
            using var newDoc = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook);

            var wbPart = newDoc.AddWorkbookPart();
            wbPart.Workbook = new Workbook();

            var wsPart = wbPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();

            // 从第二行开始（跳过表头）
            foreach (var row in sourceRows.Skip(1))
            {
                var newRow = new Row();

                // ⚠️ 核心：按【列索引】遍历，而不是遍历已有 Cell
                for (int i = 0; i < colCount; i++)
                {

                    int sourceColIndex = startCol + i;

                    Cell? sourceCell = GetCellByColumnIndex(row, sourceColIndex);

                    newRow.Append(CloneCellWithDefaultZero(sourceDoc, newDoc, sourceCell, sourceColIndex));
                }

                sheetData.Append(newRow);
            }

            wsPart.Worksheet = new Worksheet(sheetData);

            var sheets = wbPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = wbPart.GetIdOfPart(wsPart),
                SheetId = 1,
                Name = "详细数据"
            });

            wbPart.Workbook.Save();
        }

        /// <summary>
        /// 根据列索引从指定行中查找对应的 Cell
        /// </summary>
        /// <remarks>
        /// 1. 将列索引转换为 Excel 列名（如 10 → K）
        /// 2. 遍历行内已有的 Cell
        /// 3. 对比列名，找到匹配的 Cell
        /// 4. 若该列不存在（Excel 稀疏存储），返回 null
        /// </remarks>
        Cell? GetCellByColumnIndex(Row row, int columnIndex)
        {
            string targetColumnName = GetExcelColumnName(columnIndex);

            foreach (var cell in row.Elements<Cell>())
            {
                string cellRef = cell.CellReference?.Value ?? string.Empty;
                string cellColumnName = GetColumnNameFromCellReference(cellRef);

                if (cellColumnName == targetColumnName)
                    return cell;
            }

            return null;
        }

        /// <summary>
        /// 从单元格引用中提取列名部分
        /// </summary>
        /// <remarks>
        /// 例如：
        /// KA123 → KA
        /// B5   → B
        /// </remarks>
        string GetColumnNameFromCellReference(string cellReference)
        {
            var columnName = new string(cellReference.TakeWhile(char.IsLetter).ToArray());

            return columnName;
        }

        /// <summary>
        /// 将零基列索引转换为 Excel 列名
        /// </summary>
        /// <remarks>
        /// 0  → A
        /// 25 → Z
        /// 26 → AA
        /// 27 → AB
        /// </remarks>
        string GetExcelColumnName(int index)
        {
            const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            string name = "";

            while (index >= 0)
            {
                name = letters[index % 26] + name;
                index = index / 26 - 1;
            }

            return name;
        }

        /// <summary>
        /// 克隆源单元格到目标文档，并对特殊列进行默认值处理
        /// </summary>
        /// <remarks>
        /// 1. 处理源单元格为空的情况
        /// 2. 对 SharedString 类型进行文本读取
        /// 3. 针对告警列（K 列）
        ///    - 空值 / NA / 无效字符统一转为数值 0
        /// 4. 普通文本保持原样
        /// </remarks>
        Cell CloneCellWithDefaultZero(SpreadsheetDocument sourceDoc, SpreadsheetDocument targetDoc, Cell? sourceCell, int absoluteColumnIndex)
        {
            bool isAlarmColumn = absoluteColumnIndex == 10; // K 列

            if (sourceCell == null || sourceCell.CellValue == null)
                return isAlarmColumn ? new Cell() : new Cell();

            if (sourceCell.DataType?.Value == CellValues.SharedString)
            {
                string text = GetCellValue(sourceDoc, sourceCell)?.Trim();

                if (isAlarmColumn &&
                    (string.IsNullOrEmpty(text) ||
                     text == "无" ||
                     text == "-" ||
                     text == "NA" ||
                     text == "N/A" ||
                     text == "--"))
                {
                    return CreateNumberCell(0);
                }

                return CreateTextCell(targetDoc.WorkbookPart!, text);
            }

            return new Cell
            {
                DataType = sourceCell.DataType,
                CellValue = new CellValue(sourceCell.CellValue.Text)
            };
        }

        /// <summary>
        /// 创建一个数值类型的单元格
        /// </summary>
        /// <remarks>
        /// 使用 InvariantCulture，避免不同系统区域导致小数点格式错误
        /// </remarks>
        Cell CreateNumberCell(double value)
        {
            return new Cell
            {
                DataType = CellValues.Number,
                CellValue = new CellValue(value.ToString(CultureInfo.InvariantCulture))
            };
        }

        /// <summary>
        /// 创建一个共享字符串类型的文本单元格
        /// </summary>
        /// <remarks>
        /// 1. 将文本插入 SharedStringTable
        /// 2. 返回引用该字符串索引的 Cell
        /// </remarks>
        Cell CreateTextCell(WorkbookPart wbPart, string text)
        {
            int index = InsertSharedString(wbPart, text);

            return new Cell
            {
                DataType = CellValues.SharedString,
                CellValue = new CellValue(index.ToString())
            };
        }

        /// <summary>
        /// 向 SharedStringTable 中插入字符串并返回索引
        /// </summary>
        /// <remarks>
        /// 1. 若 SharedStringTable 不存在则创建
        /// 2. 遍历现有字符串，避免重复插入
        /// 3. 返回字符串在表中的索引位置
        /// </remarks>
        int InsertSharedString(WorkbookPart wbPart, string text)
        {
            if (wbPart.SharedStringTablePart == null)
            {
                wbPart.AddNewPart<SharedStringTablePart>();
                wbPart.SharedStringTablePart.SharedStringTable = new SharedStringTable();
            }

            var sst = wbPart.SharedStringTablePart.SharedStringTable;

            int index = 0;
            foreach (SharedStringItem item in sst.Elements<SharedStringItem>())
            {
                if (item.InnerText == text)
                    return index;
                index++;
            }

            sst.AppendChild(new SharedStringItem
            (
                new Text(text)
                {
                    Space = SpaceProcessingModeValues.Preserve
                })
            );

            sst.Save();
            return index;
        }

        /// <summary>
        /// 读取单元格的实际显示值（统一返回字符串）
        /// </summary>
        /// <remarks>
        /// 1. 处理 SharedString 类型
        /// 2. 普通数值直接返回 CellValue
        /// 3. 统一为 string，便于业务处理
        /// </remarks>
        string GetCellValue(SpreadsheetDocument doc, Cell cell)
        {
            if (cell.CellValue == null)
                return string.Empty;

            if (cell.DataType?.Value == CellValues.SharedString)
            {
                var sst = doc.WorkbookPart!
                             .SharedStringTablePart!
                             .SharedStringTable;

                return sst
                    .ElementAt(int.Parse(cell.CellValue.Text))
                    .InnerText;
            }

            return cell.CellValue.Text;
        }

        /// <summary>
        /// 执行算法并处理数据进行上传web服务器
        /// </summary>
        /// <param name="obj"></param>
        private void TestALG(object[] obj)
        {
            using (Py.GIL())
            {
                try
                {
                    dynamic demo;
                    if (File.Exists($"{Environment.CurrentDirectory}\\PythonScripts\\容量预测.py"))
                    {
                        // 本地调用
                        demo = Py.Import("容量预测");
                    }
                    else
                    {
                        _logger.Warn($"文件不存在");
                        return;
                    }
                    var workship = _globalSettingRepository.GetSettingValue("workship")?.Split(':') ?? new string[4];

                    // 获取python算法对象
                    dynamic result = demo.FeatureProcessor(workship[1], workship[2], workship[3], obj[0]);
                    // 执行process方法获取python算法返回值
                    PyDict pydict = result.get_features();

                    var dict = new Dictionary<string, dynamic?>();
                    // 将python字典类型转换为C#字典类型
                    foreach (PyObject key in pydict.Keys())
                    {
                        string csharpKey = key.As<string>();
                        dynamic? csharpValue = pydict[key].AsManagedObject(typeof(object));
                        dict[csharpKey] = csharpValue;
                    }

                    CapacityPredictionData[] capacityPredictionDataAry =
                    [
                        ..dict.Skip(1).Select(item =>
                        {
                            double.TryParse(item.Value?.ToString(), out double v);

                            return new CapacityPredictionData
                            {
                                ParamName = item.Key,
                                ParamValue = double.IsFinite(v) ? v : 0,
                                ParamUnit = "",
                                ParamId = ""
                            };
                        })
                    ];

                    Task.Run(async () =>
                    {
                        var re = await _webApiClient.CapacityPredictionAsync(dict["productSn"], capacityPredictionDataAry);

                        if (re.IsSuccess || re.Code == 200)
                            _logger?.Info($"[{obj[0]}-{dict["productSn"]}] 数据上传成功！");
                        else
                            _logger?.Error($"[{obj[0]}-{dict["productSn"]}] 数据上传失败：[{re.Message}]");
                    });
                }
                catch (Exception ex)
                {
                    _logger.Error($"[TestALG]-算法执行错误！文件为：{obj[0]}\r\n" + ex.Message);
                }
            }
        }
    }
}
