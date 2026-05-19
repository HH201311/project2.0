
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
            const string tableName = "detection_statistics";

            if (!_db.DbMaintenance.IsAnyTable(tableName, false))
            {
                _db.CodeFirst.InitTables<DetectionStatistics>();
            }

            EnsureUniqueIndex();
        }

        public async Task EnsureTableAsync()
        {
            await Task.Run(EnsureTable);
        }

        private void EnsureUniqueIndex()
        {
            const string tableName = "detection_statistics";
            const string indexName = "ux_detection_statistics_key";

            string sql = $@"
                CREATE UNIQUE INDEX {indexName}
                ON {tableName}(module_code, scope_type, statistics_date);";

            try
            {
                _db.Ado.ExecuteCommand(sql);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Duplicate key name", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                throw;
            }
        }

        public DetectionStatisticsModel? GetByKey(
            string moduleCode,
            DetectionStatisticsScope scopeType,
            DateTime statisticsDate)
        {
            statisticsDate = NormalizeStatisticsDate(scopeType, statisticsDate);

            return Get(model =>
                model.ModuleCode == moduleCode &&
                model.ScopeType == (int)scopeType &&
                model.StatisticsDate == statisticsDate);
        }

        public async Task<DetectionStatisticsModel?> GetByKeyAsync(
            string moduleCode,
            DetectionStatisticsScope scopeType,
            DateTime statisticsDate)
        {
            statisticsDate = NormalizeStatisticsDate(scopeType, statisticsDate);

            return await GetAsync(model =>
                model.ModuleCode == moduleCode &&
                model.ScopeType == (int)scopeType &&
                model.StatisticsDate == statisticsDate);
        }

        public DetectionStatisticsModel GetOrCreate(
            string moduleCode,
            DetectionStatisticsScope scopeType,
            DateTime statisticsDate)
        {
            EnsureTable();
            statisticsDate = NormalizeStatisticsDate(scopeType, statisticsDate);

            var model = GetByKey(moduleCode, scopeType, statisticsDate);
            if (model != null)
            {
                return model;
            }

            var newModel = CreateEmptyModel(moduleCode, scopeType, statisticsDate);
            Add(newModel);

            return GetByKey(moduleCode, scopeType, statisticsDate) ?? newModel;
        }

        public async Task<DetectionStatisticsModel> GetOrCreateAsync(
            string moduleCode,
            DetectionStatisticsScope scopeType,
            DateTime statisticsDate)
        {
            statisticsDate = NormalizeStatisticsDate(scopeType, statisticsDate);

            var model = await GetByKeyAsync(moduleCode, scopeType, statisticsDate);
            if (model != null)
            {
                return model;
            }

            var newModel = CreateEmptyModel(moduleCode, scopeType, statisticsDate);
            await AddAsync(newModel);

            return await GetByKeyAsync(moduleCode, scopeType, statisticsDate) ?? newModel;
        }

        public DetectionStatisticsModel Increment(
            string moduleCode,
            DateTime today,
            string detectionResult,
            IEnumerable<string>? triggeredDefects)
        {
            EnsureTable();
            today = today.Date;

            lock (StatisticsLock)
            {
                try
                {
                    // 测试连接是否可用
                    _db.Ado.CheckConnection();
                }
                catch
                {
                    // 尝试重新打开连接
                    _db.Open();
                }

                _db.Ado.BeginTran();
                try
                {
                    var total = GetOrCreate(moduleCode, DetectionStatisticsScope.Total, TotalStatisticsDate);
                    var daily = GetOrCreate(moduleCode, DetectionStatisticsScope.Daily, today);

                    ApplyIncrement(total, detectionResult, triggeredDefects);
                    ApplyIncrement(daily, detectionResult, triggeredDefects);

                    Update(total);
                    Update(daily);

                    _db.Ado.CommitTran();
                    return total;
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
            string detectionResult,
            IEnumerable<string>? triggeredDefects)
        {
            return await Task.Run(() =>
                Increment(moduleCode, today, detectionResult, triggeredDefects));
        }

        public bool ClearTotalAndToday(string moduleCode, DateTime today)
        {
            EnsureTable();
            today = today.Date;

            lock (StatisticsLock)
            {
                _db.Ado.BeginTran();

                try
                {
                    var total = GetOrCreate(
                        moduleCode,
                        DetectionStatisticsScope.Total,
                        TotalStatisticsDate);

                    var daily = GetOrCreate(
                        moduleCode,
                        DetectionStatisticsScope.Daily,
                        today);

                    ResetStatistics(total);
                    ResetStatistics(daily);

                    bool totalResult = Update(total);
                    bool dailyResult = Update(daily);

                    _db.Ado.CommitTran();

                    return totalResult && dailyResult;
                }
                catch
                {
                    _db.Ado.RollbackTran();
                    throw;
                }
            }
        }

        public async Task<bool> ClearTotalAndTodayAsync(string moduleCode, DateTime today)
        {
            return await Task.Run(() => ClearTotalAndToday(moduleCode, today));
        }

        private static DetectionStatisticsModel CreateEmptyModel(
            string moduleCode,
            DetectionStatisticsScope scopeType,
            DateTime statisticsDate)
        {
            return new DetectionStatisticsModel
            {
                ModuleCode = moduleCode,
                ScopeType = (int)scopeType,
                StatisticsDate = NormalizeStatisticsDate(scopeType, statisticsDate),
                TotalCount = 0,
                OKCount = 0,
                NGCount = 0,
                OKRate = 0,
                DefectNGJson = "{}",
                LastUpdateTime = DateTime.Now,
                Remark = scopeType == DetectionStatisticsScope.Total ? "总统计" : "每日统计"
            };
        }

        private static void ApplyIncrement(
            DetectionStatisticsModel model,
            string detectionResult,
            IEnumerable<string>? triggeredDefects)
        {
            if (!string.Equals(detectionResult, "OK", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(detectionResult, "NG", StringComparison.OrdinalIgnoreCase))
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

                        if (!defectMap.ContainsKey(defect))
                        {
                            defectMap[defect] = 0;
                        }

                        defectMap[defect]++;
                    }
                }

                model.DefectNGJson = JsonSerializer.Serialize(defectMap, JsonOptions);
            }

            model.OKRate = model.TotalCount > 0
                ? Math.Round((double)model.OKCount / model.TotalCount * 100, 2)
                : 0;

            model.LastUpdateTime = DateTime.Now;
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

        private static DateTime NormalizeStatisticsDate(
            DetectionStatisticsScope scopeType,
            DateTime statisticsDate)
        {
            return scopeType == DetectionStatisticsScope.Total
                ? TotalStatisticsDate
                : statisticsDate.Date;
        }
    }
}
