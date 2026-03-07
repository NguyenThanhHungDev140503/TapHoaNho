using System.Linq.Expressions;
using Domain.SeedWork;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.SeedWork;

/// <summary>
/// Generic repository implementation using Entity Framework Core
/// </summary>
/// <typeparam name="T">Entity type</typeparam>
public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly ApplicationDbContext _dbContext;
    private readonly DbSet<T> _dbSet;

    public GenericRepository(ApplicationDbContext context)
    {
        _dbContext = context;
        _dbSet = _dbContext.Set<T>();
    }

    #region Query Methods

    public T? GetById<TKey>(TKey id)
    {
        return _dbSet.Find(id);
    }

    public async Task<T?> GetAsync<TKey>(TKey id)
    {
        return await _dbSet.FindAsync(id);
    }

    public async Task<T?> GetByIdAsync<TKey, TProperty>(
        TKey id, 
        params Expression<Func<T, TProperty>>[] navigationProperties)
    {
        IQueryable<T> query = _dbSet;
        
        foreach (var navigationProperty in navigationProperties)
        {
            query = query.Include(navigationProperty);
        }
        
        return await query.FirstOrDefaultAsync(e => EF.Property<TKey>(e, "Id")!.Equals(id));
    }

    public IQueryable<T> GetAll()
    {
        return _dbSet;
    }

    public IQueryable<T> GetAllReadOnly()
    {
        return _dbSet.AsNoTracking();
    }

    public IQueryable<T> GetAllAsync(
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        IEnumerable<string>? includes = null,
        bool noneTracking = true)
    {
        var query = noneTracking ? _dbSet.AsNoTracking() : _dbSet.AsQueryable();

        if (filter is not null)
            query = query.Where(filter);

        if (includes is not null)
        {
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
        }

        if (orderBy is not null)
            query = orderBy(query);

        return query;
    }

    public async Task<IQueryable<T>> FindByAsync(
        Expression<Func<T, bool>> predicate,
        IEnumerable<string>? includes = null,
        bool noneTracking = true)
    {
        var query = noneTracking ? _dbSet.AsNoTracking() : _dbSet.AsQueryable();

        if (includes is not null)
        {
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
        }

        return await Task.FromResult(query.Where(predicate));
    }

    #endregion

    #region Command Methods

    public async Task<T> AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        return entity;
    }

    public async Task AddRangeAsync(IEnumerable<T> entities)
    {
        await _dbSet.AddRangeAsync(entities);
    }

    public async Task UpdateAsync(object key, T entity)
    {
        var existingEntity = await _dbSet.FindAsync(key);
        if (existingEntity is not null)
        {
            _dbContext.Entry(existingEntity).CurrentValues.SetValues(entity);
        }
    }

    public Task DeleteAsync(T entity)
    {
        _dbSet.Remove(entity);
        return Task.CompletedTask;
    }

    public Task DeleteRangeAsync(IEnumerable<T> entities)
    {
        _dbSet.RemoveRange(entities);
        return Task.CompletedTask;
    }

    #endregion

    #region Utility Methods

    public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.AnyAsync(predicate);
    }

    public async Task<int> SaveAsync()
    {
        return await _dbContext.SaveChangesAsync();
    }

    public int Count()
    {
        return _dbSet.Count();
    }

    public async Task<int> CountAsync()
    {
        return await _dbSet.CountAsync();
    }

    public async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.CountAsync(predicate);
    }

    #endregion
}
