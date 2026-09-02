using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool
{
    public class DefectFileMoverService
    {

        private readonly string _sourceRootPath;
        private readonly string _targetRootPath;
        private readonly string[] _targetDefectTypes = new[]
            {
            "负极多片",
            "负极边缘间隙不良",
            "正极边缘间隙不良",
            "正极中间间隙不良",
            "负极压伤",
            "负极突起", // 2026-08：翻折面积超限沿用此归档名，不另建「负极翻折」
            "正极Y方向掉片"
        };

        // 工位文件夹列表
        private readonly string[] _stationFolders = new[] { "LAB", "LCD", "RAB", "RCD" };

        public DefectFileMoverService(string sourceRootPath, string targetRootPath)
        {
            _sourceRootPath = Path.GetFullPath(sourceRootPath ?? throw new ArgumentNullException(nameof(sourceRootPath)));
            _targetRootPath = Path.GetFullPath(targetRootPath ?? throw new ArgumentNullException(nameof(targetRootPath)));
        }

        public async Task MoveYesterdayDefectFilesAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    // 计算昨天的日期
                    string yesterdayDate = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
                    AddLog($"[文件复制服务] 开始处理 {yesterdayDate} 的特定缺陷文件...");

                    //遍历所有工位文件夹
                    foreach (var stationFolder in _stationFolders)
                    {
                        // 构建源日期目录的完整路径
                        string sourceDateDir = Path.Combine(_sourceRootPath, stationFolder, yesterdayDate);

                        if (Directory.Exists(sourceDateDir))
                        {
                            AddLog($"\n[文件复制服务] 正在处理工位目录: {sourceDateDir}");

                            // 获取日期目录下的所有缺陷类型子目录
                            string[] allDefectDirsInDate = Directory.GetDirectories(sourceDateDir);

                            foreach (var sourceDefectDir in allDefectDirsInDate)
                            {
                                string defectTypeName = Path.GetFileName(sourceDefectDir);

                                //筛选出需要移动的缺陷目录
                                if (_targetDefectTypes.Contains(defectTypeName))
                                {
                                    // 计算目标路径，保持原有目录结构
                                    string relativePath = Path.GetRelativePath(_sourceRootPath, sourceDefectDir);
                                    string targetDefectDir = Path.Combine(_targetRootPath, relativePath);

                                    AddLog($"[文件复制服务] 准备复制: {sourceDefectDir}");
                                    AddLog($"[文件复制服务] 目标位置: {targetDefectDir}");

                                    // 确保目标目录的父级目录存在
                                    if (!Directory.Exists(Path.GetDirectoryName(targetDefectDir)))
                                    {
                                        string? directoryPath = Path.GetDirectoryName(targetDefectDir);
                                        if (directoryPath != null)
                                        {
                                            Directory.CreateDirectory(directoryPath);
                                        }

                                        AddLog($"[文件复制服务] 创建目标父目录: {Path.GetDirectoryName(targetDefectDir)}");
                                    }

                                    // 执行移动操作
                                    try
                                    {
                                        CopyDirectory(sourceDefectDir, targetDefectDir);
                                        AddLog($"[文件复制服务] 复制成功: {sourceDefectDir} -> {targetDefectDir}");
                                    }
                                    catch (Exception ex)
                                    {
                                        AddLog($"[文件复制服务] 复制失败: {sourceDefectDir}。错误: {ex.Message}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            AddLog($"[文件复制服务] 目录不存在，跳过: {sourceDateDir}");
                        }
                    }

                    AddLog("\n[文件复制服务] 所有文件复制任务处理完毕。");
                }
                catch (Exception ex)
                {
                    AddLog($"[文件复制服务] 执行任务时发生严重错误: {ex.Message}");
                    throw;
                }
            });
        }


        /// <summary>
        /// 自定义的递归复制目录方法
        /// </summary>
        /// <param name="sourceDir">源目录路径</param>
        /// <param name="targetDir">目标目录路径</param>
        private void CopyDirectory(string sourceDir, string targetDir)
        {
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
                AddLog($"[文件复制服务] 创建目标目录: {targetDir}");
            }

            // 复制目录中的所有文件
            foreach (string filePath in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(filePath);
                string destFilePath = Path.Combine(targetDir, fileName);
                File.Copy(filePath, destFilePath, overwrite: true);
            }

            // 递归复制所有子目录
            foreach (string subDirPath in Directory.GetDirectories(sourceDir))
            {
                string subDirName = Path.GetFileName(subDirPath);
                string destSubDirPath = Path.Combine(targetDir, subDirName);
                CopyDirectory(subDirPath, destSubDirPath);
            }
        }
        //日志委托
        public Action<string> AddLog { get; set; } = (message) => Console.WriteLine(message);
    }
}
