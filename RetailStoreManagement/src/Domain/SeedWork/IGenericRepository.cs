using System.Linq.Expressions;

namespace Domain.SeedWork;

/// <summary>
/// Generic repository interface providing common CRUD operations
/// </summary>
/// <typeparam name="T">Entity type</typeparam>
public interface IGenericRepository<T> where T : class
{
    #region Query Methods
    
    /// <summary>
    /// Get entity by primary key (synchronous)
    /// </summary>
    T? GetById<TKey>(TKey id);
    
    /// <summary>
    /// Get entity by primary key (asynchronous)
    /// </summary>
    Task<T?> GetAsync<TKey>(TKey id);
    
    /// <summary>
    /// Get entity by primary key with navigation properties
    /// </summary>
    Task<T?> GetByIdAsync<TKey, TProperty>(
        TKey id, 
        params Expression<Func<T, TProperty>>[] navigationProperties);
    
    /// <summary>
    /// Get all entities (no tracking)
    /// </summary>
    IQueryable<T> GetAll();
    IQueryable<T> GetAllReadOnly();
    
    /// <summary>
    /// Get all entities with optional filtering, ordering, and includes
    /// </summary>
    IQueryable<T> GetAllAsync(
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        IEnumerable<string>? includes = null,
        bool noneTracking = true);
    
    /// <summary>
    /// Find entities by predicate
    /// </summary>
    Task<IQueryable<T>> FindByAsync(
        Expression<Func<T, bool>> predicate,
        IEnumerable<string>? includes = null,
        bool noneTracking = true);
    
    #endregion

    #region Command Methods
    
    /// <summary>
    /// Add new entity
    /// </summary>
    Task<T> AddAsync(T entity);
    
    /// <summary>
    /// Add multiple entities
    /// </summary>
    Task AddRangeAsync(IEnumerable<T> entities);
    
    /// <summary>
    /// Update existing entity
    /// </summary>
    Task UpdateAsync(object key, T entity);
    
    /// <summary>
    /// Delete entity
    /// </summary>
    Task DeleteAsync(T entity);
    
    /// <summary>
    /// Delete multiple entities
    /// </summary>
    Task DeleteRangeAsync(IEnumerable<T> entities);
    
    #endregion

    #region Utility Methods
    
    /// <summary>
    /// Check if any entity matches the predicate
    /// </summary>
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);
    
    /// <summary>
    /// Save changes to database
    /// </summary>
    Task<int> SaveAsync();
    
    /// <summary>
    /// Get count of entities
    /// </summary>
    int Count();
    
    /// <summary>
    /// Get count of entities (async)
    /// </summary>
    Task<int> CountAsync();
    
    /// <summary>
    /// Get count of entities matching predicate
    /// </summary>
    Task<int> CountAsync(Expression<Func<T, bool>> predicate);
    
    #endregion
}
