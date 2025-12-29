using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

/// <summary>
/// Base entity class với các properties chung cho tất cả entities
/// </summary>
/// <typeparam name="TKey">Kiểu dữ liệu của Primary Key</typeparam>
public abstract class BaseEntity<TKey>
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public virtual TKey Id { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }
    
    public DateTime? DeletedAt { get; set; }
    
    /// <summary>
    /// Soft delete flag - true nếu entity đã bị xóa
    /// </summary>
    public bool IsDeleted => DeletedAt.HasValue;
}
