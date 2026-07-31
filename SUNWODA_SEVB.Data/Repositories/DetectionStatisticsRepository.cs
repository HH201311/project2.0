
using SqlSugar;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Data.Models;
using System.Text.Json;

namespace SUNWODA_SEVB.Data.Repositories
{
    public class DetectionStatisticsRepository
          : MappingRepository<DetectionStatisticsModel, DetectionStatistics>,
            IDetectionStatisticsRepository
    {
        private new readonly ISqlSugarClient _db;

        private static readonly object StatisticsLock = new();
        private static readonly object SchemaLock = new();

        private static bool _schemaEnsured = false;

        private static readonly DateTime TotalStatisticsDate = new DateTime(1900, 1, 1);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public DetectionStatisticsRepository(ISqlSugarClient db) : base(db)
        {
            _db = db;
        }

        public void EnsureTable()
        {
            if (_schemaEnsured)
            {
                return;
            }

            lock (SchemaLock)
            {
                if (_schemaEnsured)
                {
                    return;
                }

                const string tableName = "detection_statistics";

                if (!_db.DbMaintenance.IsAnyTable(tableName, false))
                {
                    _db.CodeFirst.InitTables<DetectionStatistics>();
                }

                EnsureSourceTypeColumn(tableName);
                EnsureUniqueIndex(tableName);

                _schemaEnsured = true;
            }
        }

        public async Task EnsureTableAsync()
        {
            await Task.Run(EnsureTable);
        }

        private void EnsureSourceTypeColumn(string tableName)
        {
            bool hasSourceType = _db.DbMaintenance.IsAnyColumn(
                tableName,
                "source_type",
                false);

            if (hasSourceType)
            {
                return;
            }

            // 旧数据默认归为 All / 全部统计
            _db.Ado.ExecuteCommand(@"
                ALTER TABLE detection_statistics
                ADD COLUMN source_type INT NOT NULL DEFAULT 0; ");
        }



        private void EnsureUniqueIndex(string tableName)
        {
            // 删除旧索引和可能存在的新索引，然后统一创建新索引
            TryDropIndex("ux_detection_statistics_key", tableName);
            TryDropIndex("detection_statistics_key", tableName);

            try
            {
                _db.Ado.ExecuteCommand($@"
                    CREATE UNIQUE INDEX ux_detection_statistics_key
                    ON {tableName}(module_code, source_type, scope_type, statistics_date);
                ");
            }
            catch (Exception ex)
            {
                if (IsIndexAlreadyExistsException(ex))
                {
                    return;
                }

                throw;
            }
        }

        private void TryDropIndex(string indexName, string tableName)
        {
            try
            {
                // SQLite 写法
                _db.Ado.ExecuteCommand($"DROP INDEX IF EXISTS {indexName};");
            }
            catch
            {
                try
                {
                    // MySQL 写法
                    _db.Ado.ExecuteCommand($"DROP INDEX {indexName} ON {tableName};");
                }
                catch
                {
                    // 不存在则忽略
                }
            }
        }

        private static bool IsIndexAlreadyExistsException(Exception ex)
        {
            return ex.Message.Contains("Duplicate key name", StringComparison.OrdinalIgnoreCase)
                   || ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                   || ex.Message.Contains("已存在", StringComparison.OrdinalIgnoreCase);
        }
        //private static void AddOrThrowMigrationException(string message, Exception ex)
        //{
        //    throw new Exception($"{message}：{ex.Message}", ex);
        //}
        #region 查询与创建
        public DetectionStatisticsModel? GetByKey(
           string moduleCode,
           DetectionStatisticsSource sourceType,
           DetectionStatisticsScope scopeType,
           DateTime statisticsDate)
        {
            EnsureTable();
            return GetByKeyCore(moduleCode, sourceType, scopeType, statisticsDate);
        }

        public async Task<DetectionStatisticsModel?> GetByKeyAsync(
            string moduleCode,
            DetectionStatisticsSource sourceType,
            DetectionStatisticsScope scopeType,
            DateTime statisticsDate)
        {
            return await Task.Run(() =>
                GetByKey(moduleCode, sourceType, scopeType, statisticsDate));
        }

        public DetectionStatisticsModel GetOrCreate(
            string moduleCode,
            DetectionStatisticsSource sourceType,
            DetectionStatisticsScope scopeType,
            DateTime statisticsDate)
        {
            EnsureTable();

            lock (StatisticsLock)
            {
                return GetOrCreateCore(moduleCode, sourceType, scopeType, statisticsDate);
            }
        }

        public async Task<DetectionStatisticsModel> GetOrCreateAsync(
            string moduleCode,
            DetectionStatisticsSource sourceType,
            DetectionStatisticsScope scopeType,
            DateTime statisticsDate)
        {
            return await Task.Run(() =>
                GetOrCreate(moduleCode, sourceType, scopeType, statisticsDate));
        }

        private DetectionStatisticsModel? GetByKeyCore(
            string moduleCode,
            DetectionStatisticsSource sourceType,
            DetectionStatisticsScope scopeType,
            DateTime statisticsDate)
        {
            statisticsDate = NormalizeStatisticsDate(scopeType, statisticsDate);

            return Get(model =>
                model.ModuleCode == moduleCode &&
                model.SourceType == (int)sourceType &&
                model.ScopeType == (int)scopeType &&
                model.StatisticsDate == statisticsDate);
        }

        private DetectionStatisticsModel GetOrCreateCore(
            string moduleCode,
            DetectionStatisticsSource sourceType,
            DetectionStatisticsScope scopeType,
            DateTime statisticsDate)
        {
            statisticsDate = NormalizeStatisticsDate(scopeType, statisticsDate);

            var model = GetByKeyCore(moduleCode, sourceType, scopeType, statisticsDate);
            if (model != null)
            {
                return model;
            }

            var newModel = CreateEmptyModel(
                moduleCode,
                sourceType,
                scopeType,
                statisticsDate);

            try
            {
                Add(newModel);
            }
            catch
            {
                // 多实例或并发场景下，可能被其他线程/进程抢先插入
                var existed = GetByKeyCore(moduleCode, sourceType, scopeType, statisticsDate);
                if (existed != null)
                {
                    return existed;
                }

                throw;
            }

            return GetByKeyCore(moduleCode, sourceType, scopeType, statisticsDate) ?? newModel;
        }
        #endregion


        #region 统计累加

        public DetectionStatisticsModel Increment(
            string moduleCode,
            DateTime today,
            DetectionStatisticsSource sourceType,
            string detectionResult,
            IEnumerable<string>? triggeredDefects)
        {
            EnsureTable();
            today = today.Date;

            if (sourceType == DetectionStatisticsSource.All)
            {
                throw new ArgumentException(
                    "Increment 必须明确传入 Online 或 Offline，不能传 All。",
                    nameof(sourceType));
            }

            if (!IsCountableResult(detectionResult))
            {
                return GetOrCreate(
                    moduleCode,
                    DetectionStatisticsSource.All,
                    DetectionStatisticsScope.Total,
                    TotalStatisticsDate);
            }

            lock (StatisticsLock)
            {
                EnsureConnection();

                _db.Ado.BeginTran();

                try
                {
                    var allTotal = GetOrCreateCore(
                        moduleCode,
                        DetectionStatisticsSource.All,
                        DetectionStatisticsScope.Total,
                        TotalStatisticsDate);

                    var allDaily = GetOrCreateCore(
                        moduleCode,
                        DetectionStatisticsSource.All,
                        DetectionStatisticsScope.Daily,
                        today);

                    var sourceTotal = GetOrCreateCore(
                        moduleCode,
                        sourceType,
                        DetectionStatisticsScope.Total,
                        TotalStatisticsDate);

                    var sourceDaily = GetOrCreateCore(
                        moduleCode,
                        sourceType,
                        DetectionStatisticsScope.Daily,
                        today);

                    ApplyIncrement(allTotal, detectionResult, triggeredDefects);
                    ApplyIncrement(allDaily, detectionResult, triggeredDefects);
                    ApplyIncrement(sourceTotal, detectionResult, triggeredDefects);
                    ApplyIncrement(sourceDaily, detectionResult, triggeredDefects);

                    Update(allTotal);
                    Update(allDaily);
                    Update(sourceTotal);
                    Update(sourceDaily);

                    _db.Ado.CommitTran();

                    // 保持原有界面兼容：返回全部总统计
                    return allTotal;
                }
                catch
                {
                    _db.Ado.RollbackTran();
                    throw;
                }
            }
        }

        public async Task<DetectionStatisticsModel> IncrementAsync(
            string moduleCode,
            DateTime today,
            DetectionStatisticsSource sourceType,
            string detectionResult,
            IEnumerable<string>? triggeredDefects)
        {
            return await Task.Run(() =>
                Increment(moduleCode, today, sourceType, detectionResult, triggeredDefects));
        }

        #endregion

        #region 清零

        public bool ClearTotalAndToday(
            string moduleCode,
            DateTime today,
            DetectionStatisticsSource sourceType = DetectionStatisticsSource.All,
            bool clearAllSources = true)
        {
            EnsureTable();
            today = today.Date;

            lock (StatisticsLock)
            {
                EnsureConnection();

                _db.Ado.BeginTran();

                try
                {
                    bool result = true;

                    if (clearAllSources || sourceType == DetectionStatisticsSource.All)
                    {
                        foreach (var source in new[]
                        {
                            DetectionStatisticsSource.All,
                            DetectionStatisticsSource.Online,
                            DetectionStatisticsSource.Offline
                        })
                        {
                            result = ResetSourceTotalAndToday(moduleCode, today, source) && result;
                        }
                    }
                    else
                    {
                        result = ResetSourceTotalAndToday(moduleCode, today, sourceType) && result;

                        // 清除单一来源后，重新汇总 All，避免 All 与 Online/Offline 不一致
                        RebuildAllStatisticsFromSources(moduleCode, today);
                    }

                    _db.Ado.CommitTran();
                    return result;
                }
                catch
                {
                    _db.Ado.RollbackTran();
                    throw;
                }
            }
        }

        public async Task<bool> ClearTotalAndTodayAsync(
            string moduleCode,
            DateTime today,
            DetectionStatisticsSource sourceType = DetectionStatisticsSource.All,
            bool clearAllSources = true)
        {
            return await Task.Run(() =>
                ClearTotalAndToday(moduleCode, today, sourceType, clearAllSources));
        }

        private bool ResetSourceTotalAndToday(
            string moduleCode,
            DateTime today,
            DetectionStatisticsSource sourceType)
        {
            var total = GetOrCreateCore(
                moduleCode,
                sourceType,
                DetectionStatisticsScope.Total,
                TotalStatisticsDate);

            var daily = GetOrCreateCore(
                moduleCode,
                sourceType,
                DetectionStatisticsScope.Daily,
                today);

            ResetStatistics(total);
            ResetStatistics(daily);

            return Update(total) && Update(daily);
        }

        private void RebuildAllStatisticsFromSources(string moduleCode, DateTime today)
        {
            var allTotal = GetOrCreateCore(
                moduleCode,
                DetectionStatisticsSource.All,
                DetectionStatisticsScope.Total,
                TotalStatisticsDate);

            var allDaily = GetOrCreateCore(
                moduleCode,
                DetectionStatisticsSource.All,
                DetectionStatisticsScope.Daily,
                today);

            var onlineTotal = GetOrCreateCore(
                moduleCode,
                DetectionStatisticsSource.Online,
                DetectionStatisticsScope.Total,
                TotalStatisticsDate);

            var offlineTotal = GetOrCreateCore(
                moduleCode,
                DetectionStatisticsSource.Offline,
                DetectionStatisticsScope.Total,
                TotalStatisticsDate);

            var onlineDaily = GetOrCreateCore(
                moduleCode,
                DetectionStatisticsSource.Online,
                DetectionStatisticsScope.Daily,
                today);

            var offlineDaily = GetOrCreateCore(
                moduleCode,
                DetectionStatisticsSource.Offline,
                DetectionStatisticsScope.Daily,
                today);

            MergeStatistics(allTotal, onlineTotal, offlineTotal);
            MergeStatistics(allDaily, onlineDaily, offlineDaily);

            Update(allTotal);
            Update(allDaily);
        }

        #endregion

        #region 内部统计逻辑

        private static DetectionStatisticsModel CreateEmptyModel(
            string moduleCode,
            DetectionStatisticsSource sourceType,
            DetectionStatisticsScope scopeType,
            DateTime statisticsDate)
        {
            return new DetectionStatisticsModel
            {
                ModuleCode = moduleCode,
                SourceType = (int)sourceType,
                ScopeType = (int)scopeType,
                StatisticsDate = NormalizeStatisticsDate(scopeType, statisticsDate),
                TotalCount = 0,
                OKCount = 0,
                NGCount = 0,
                OKRate = 0,
                DefectNGJson = "{}",
                LastUpdateTime = DateTime.Now,
                Remark = $"{GetSourceName(sourceType)}-{GetScopeName(scopeType)}"
            };
        }

        private static void ApplyIncrement(
            DetectionStatisticsModel model,
            string detectionResult,
            IEnumerable<string>? triggeredDefects)
        {
            if (!IsCountableResult(detectionResult))
            {
                return;
            }

            model.TotalCount++;

            if (string.Equals(detectionResult, "OK", StringComparison.OrdinalIgnoreCase))
            {
                model.OKCount++;
            }
            else
            {
                model.NGCount++;

                var defectMap = DeserializeDefectMap(model.DefectNGJson);

                if (triggeredDefects != null)
                {
                    foreach (var defect in triggeredDefects)
                    {
                        if (string.IsNullOrWhiteSpace(defect))
                        {
                            continue;
                        }

                        string defectKey = defect.Trim();

                        if (!defectMap.ContainsKey(defectKey))
                        {
                            defectMap[defectKey] = 0;
                        }

                        defectMap[defectKey]++;
                    }
                }

                model.DefectNGJson = JsonSerializer.Serialize(defectMap, JsonOptions);
            }

            RefreshRateAndTime(model);
        }

        private static void ResetStatistics(DetectionStatisticsModel model)
        {
            model.TotalCount = 0;
            model.OKCount = 0;
            model.NGCount = 0;
            model.OKRate = 0;
            model.DefectNGJson = "{}";
            model.ResetTime = DateTime.Now;
            model.LastUpdateTime = DateTime.Now;
        }

        private static void MergeStatistics(
            DetectionStatisticsModel target,
            DetectionStatisticsModel sourceA,
            DetectionStatisticsModel sourceB)
        {
            target.TotalCount = sourceA.TotalCount + sourceB.TotalCount;
            target.OKCount = sourceA.OKCount + sourceB.OKCount;
            target.NGCount = sourceA.NGCount + sourceB.NGCount;
            target.DefectNGJson = JsonSerializer.Serialize(
                MergeDefectMaps(sourceA.DefectNGJson, sourceB.DefectNGJson),
                JsonOptions);

            RefreshRateAndTime(target);
        }

        private static Dictionary<string, int> MergeDefectMaps(
            string? defectJsonA,
            string? defectJsonB)
        {
            var result = DeserializeDefectMap(defectJsonA);
            var second = DeserializeDefectMap(defectJsonB);

            foreach (var item in second)
            {
                if (!result.ContainsKey(item.Key))
                {
                    result[item.Key] = 0;
                }

                result[item.Key] += item.Value;
            }

            return result;
        }

        private static void RefreshRateAndTime(DetectionStatisticsModel model)
        {
            model.OKRate = model.TotalCount > 0
                ? Math.Round((double)model.OKCount / model.TotalCount * 100, 2)
                : 0;

            model.LastUpdateTime = DateTime.Now;
        }

        public static Dictionary<string, int> DeserializeDefectMap(string? defectJson)
        {
            if (string.IsNullOrWhiteSpace(defectJson))
            {
                return new Dictionary<string, int>();
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, int>>(defectJson)
                       ?? new Dictionary<string, int>();
            }
            catch
            {
                return new Dictionary<string, int>();
            }
        }

        private static bool IsCountableResult(string detectionResult)
        {
            return string.Equals(detectionResult, "OK", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(detectionResult, "NG", StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureConnection()
        {
            try
            {
                _db.Ado.CheckConnection();
            }
            catch
            {
                _db.Open();
            }
        }

        private static DateTime NormalizeStatisticsDate(
            DetectionStatisticsScope scopeType,
            DateTime statisticsDate)
        {
            return scopeType == DetectionStatisticsScope.Total
                ? TotalStatisticsDate
                : statisticsDate.Date;
        }

        private static string GetSourceName(DetectionStatisticsSource sourceType)
        {
            return sourceType switch
            {
                DetectionStatisticsSource.All => "全部统计",
                DetectionStatisticsSource.Online => "在线统计",
                DetectionStatisticsSource.Offline => "离线统计",
                _ => "未知来源"
            };
        }

        private static string GetScopeName(DetectionStatisticsScope scopeType)
        {
            return scopeType switch
            {
                DetectionStatisticsScope.Total => "总统计",
                DetectionStatisticsScope.Daily => "每日统计",
                _ => "未知范围"
            };
        }

        #endregion
    }
}
