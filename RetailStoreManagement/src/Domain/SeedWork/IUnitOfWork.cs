using Microsoft.EntityFrameworkCore.Storage;

namespace Domain.SeedWork;

/// <summary>
/// Unit of Work pattern interface for managing transactions and repositories
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Get repository for specified entity type
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    /// <returns>Generic repository instance</returns>
    IGenericRepository<T> Repository<T>() where T : class;
    
    /// <summary>
    /// Save all pending changes (synchronous)
    /// </summary>
    int SaveChanges();
    
    /// <summary>
    /// Save all pending changes (asynchronous)
    /// </summary>
    Task<int> SaveChangesAsync();
    
    /// <summary>
    /// Begin a new database transaction
    /// </summary>
    IDbContextTransaction BeginTransaction();
    
    /// <summary>
    /// Begin a new database transaction (async)
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync();
}
