using Mapster;
using SqlSugar;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Tool.Converter;
using System.Linq.Expressions;

namespace SUNWODA_SEVB.Data.Repositories
{
    /// <summary>
    /// 支持实体映射的仓储基类
    /// </summary>
    public class MappingRepository<TEntity, TModel> : IRepository<TEntity>
        where TEntity : class, new()
        where TModel : class, new()
    {
        protected readonly ISqlSugarClient _db;

        public MappingRepository(ISqlSugarClient db)
        {
            _db = db;
        }

        // 统一使用ExpressionConverter进行表达式转换
        private Expression<Func<TModel, bool>> ConvertPredicate(Expression<Func<TEntity, bool>> expression)
        {
            var parameter = Expression.Parameter(typeof(TModel), "x");
            var converter = new ExpressionConverter<TEntity, TModel>(parameter);
            return converter.Convert(expression);
        }

        public async Task<TEntity?> GetByIdAsync(object id)
        {
            using (var dbCopy = _db.CopyNew())
            {
                var model = await dbCopy.Queryable<TModel>().InSingleAsync(id);
                return model?.Adapt<TEntity>();
            }
        }

        public TEntity? GetById(object id)
        {
            using (var dbCopy = _db.CopyNew())
            {
                var model = dbCopy.Queryable<TModel>().InSingle(id);
                return model?.Adapt<TEntity>();
            }
        }

        public async Task<TEntity?> GetAsync(Expression<Func<TEntity, bool>> predicate)
        {
            var modelPredicate = ConvertPredicate(predicate);
            using (var dbCopy = _db.CopyNew())
            {
                var model = await dbCopy.Queryable<TModel>()
                    .Where(modelPredicate)
                    .Take(1)
                    .ToListAsync();
                return model.FirstOrDefault()?.Adapt<TEntity>();
            }
        }

        public TEntity? Get(Expression<Func<TEntity, bool>> predicate)
        {
            var modelPredicate = ConvertPredicate(predicate);
            // 同步路径必须 CopyNew，且无数据返回 null（勿用 First 抛错拖垮模块注册）
            using (var dbCopy = _db.CopyNew())
            {
                var model = dbCopy
                    .Queryable<TModel>()
                    .Where(modelPredicate)
                    .Take(1)
                    .ToList()
                    .FirstOrDefault();
                return model?.Adapt<TEntity>();
            }
        }

        public async Task<List<TEntity>> GetAllAsync()
        {
            using (var dbCopy = _db.CopyNew())
            {
                var models = await dbCopy.Queryable<TModel>().ToListAsync();
                return models.Adapt<List<TEntity>>();
            }
        }

        public List<TEntity> GetAll()
        {
            using (var dbCopy = _db.CopyNew())
            {
                var models = dbCopy.Queryable<TModel>().ToList();
                return models.Adapt<List<TEntity>>();
            }
        }

        public async Task<List<TEntity>> GetListAsync(Expression<Func<TEntity, bool>> predicate)
        {
            var modelPredicate = ConvertPredicate(predicate);
            using (var dbCopy = _db.CopyNew())
            {
                var models = await dbCopy.Queryable<TModel>()
                    .Where(modelPredicate)
                    .ToListAsync();
                return models.Adapt<List<TEntity>>();
            }
        }

        public List<TEntity> GetList(Expression<Func<TEntity, bool>> predicate)
        {
            var modelPredicate = ConvertPredicate(predicate);
            using (var dbCopy = _db.CopyNew())
            {
                var models = dbCopy.Queryable<TModel>().Where(modelPredicate).ToList();
                return models.Adapt<List<TEntity>>();
            }
        }

        public async Task<(List<TEntity> Items, int Total)> GetPagedAsync(
            Expression<Func<TEntity, bool>>? predicate = null,
            int pageIndex = 1,
            int pageSize = 20,
            Expression<Func<TEntity, object>>? orderBy = null,
            bool isDesc = true)
        {
            RefAsync<int> total = 0;
            var query = _db.Queryable<TModel>();

            if (predicate != null)
            {
                var modelPredicate = ConvertPredicate(predicate);
                query = query.Where(modelPredicate);
            }

            // 简化排序处理，使用动态排序
            if (orderBy != null)
            {
                // 直接使用字符串排序，避免复杂的表达式转换
                query = query.OrderByIF(true, "ID DESC");
            }

            var models = await query.ToPageListAsync(pageIndex, pageSize, total);
            var items = models.Adapt<List<TEntity>>();
            return (items, total.Value);
        }

        public (List<TEntity> Items, int Total) GetPaged(
            Expression<Func<TEntity, bool>>? predicate = null,
            int pageIndex = 1,
            int pageSize = 20,
            Expression<Func<TEntity, object>>? orderBy = null,
            bool isDesc = true)
        {
            int total = 0;
            var query = _db.Queryable<TModel>();

            if (predicate != null)
            {
                var modelPredicate = ConvertPredicate(predicate);
                query = query.Where(modelPredicate);
            }

            // 简化排序处理，使用动态排序
            if (orderBy != null)
            {
                // 直接使用字符串排序，避免复杂的表达式转换
                query = query.OrderByIF(true, "ID DESC");
            }

            var models = query.ToPageList(pageIndex, pageSize, ref total);
            var items = models.Adapt<List<TEntity>>();
            return (items, total);
        }

        public async Task<bool> AddAsync(TEntity entity)
        {
            var model = entity.Adapt<TModel>();
            var result = await _db.Insertable(model).ExecuteCommandAsync();
            return result > 0;
        }

        public bool Add(TEntity entity)
        {
            var model = entity.Adapt<TModel>();
            var result = _db.Insertable(model).ExecuteCommand();
            return result > 0;
        }

        public async Task<bool> AddRangeAsync(IEnumerable<TEntity> entities)
        {
            var models = entities.Adapt<List<TModel>>();
            return await _db.Insertable(models).ExecuteCommandAsync() > 0;
        }

        public bool AddRange(IEnumerable<TEntity> entities)
        {
            var models = entities.Adapt<List<TModel>>();
            return _db.Insertable(models).ExecuteCommand() > 0;
        }

        public async Task<bool> UpdateAsync(TEntity entity)
        {
            var model = entity.Adapt<TModel>();
            return await _db.Updateable(model).ExecuteCommandAsync() > 0;
        }

        public bool Update(TEntity entity)
        {
            var model = entity.Adapt<TModel>();
            return _db.Updateable(model).ExecuteCommand() > 0;
        }

        public async Task<bool> UpdateRangeAsync(IEnumerable<TEntity> entities)
        {
            var models = entities.Adapt<List<TModel>>();
            return await _db.Updateable(models).ExecuteCommandAsync() > 0;
        }

        public bool UpdateRange(IEnumerable<TEntity> entities)
        {
            var models = entities.Adapt<List<TModel>>();
            return _db.Updateable(models).ExecuteCommand() > 0;
        }

        public async Task<bool> DeleteAsync(TEntity entity)
        {
            var model = entity.Adapt<TModel>();
            return await _db.Deleteable(model).ExecuteCommandAsync() > 0;
        }

        public bool Delete(TEntity entity)
        {
            var model = entity.Adapt<TModel>();
            return _db.Deleteable(model).ExecuteCommand() > 0;
        }

        public async Task<bool> DeleteAsync(Expression<Func<TEntity, bool>> predicate)
        {
            var modelPredicate = ConvertPredicate(predicate);
            return await _db.Deleteable<TModel>().Where(modelPredicate).ExecuteCommandAsync() > 0;
        }

        public bool Delete(Expression<Func<TEntity, bool>> predicate)
        {
            var modelPredicate = ConvertPredicate(predicate);
            return _db.Deleteable<TModel>().Where(modelPredicate).ExecuteCommand() > 0;
        }

        public async Task<bool> DeleteByIdAsync(object id)
        {
            return await _db.Deleteable<TModel>().In(id).ExecuteCommandAsync() > 0;
        }

        public bool DeleteById(object id)
        {
            return _db.Deleteable<TModel>().In(id).ExecuteCommand() > 0;
        }

        public async Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate)
        {
            var modelPredicate = ConvertPredicate(predicate);
            return await _db.Queryable<TModel>().AnyAsync(modelPredicate);
        }

        public bool Exists(Expression<Func<TEntity, bool>> predicate)
        {
            var modelPredicate = ConvertPredicate(predicate);
            return _db.Queryable<TModel>().Any(modelPredicate);
        }

        public async Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null)
        {
            if (predicate == null)
                return await _db.Queryable<TModel>().CountAsync();

            var modelPredicate = ConvertPredicate(predicate);
            return await _db.Queryable<TModel>().CountAsync(modelPredicate);
        }

        public int Count(Expression<Func<TEntity, bool>>? predicate = null)
        {
            if (predicate == null)
                return _db.Queryable<TModel>().Count();

            var modelPredicate = ConvertPredicate(predicate);
            return _db.Queryable<TModel>().Count(modelPredicate);
        }
    }
}