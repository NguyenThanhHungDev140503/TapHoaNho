using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

/// <summary>
/// Entity lịch sử thay đổi tồn kho
/// </summary>
public class InventoryHistoryEntity : BaseEntity<int>
{
    [Required]
    public int ProductId { get; set; }

    [Required]
    public int UserId { get; set; }

    /// <summary>
    /// Số lượng thay đổi (+ hoặc -)
    /// </summary>
    [Required]
    public int QuantityChange { get; set; }

    /// <summary>
    /// Số lượng sau khi thay đổi
    /// </summary>
    [Required]
    public int QuantityAfter { get; set; }

    /// <summary>
    /// Lý do thay đổi
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Reason { get; set; } = string.Empty;

    // Navigation properties
    public virtual ProductEntity Product { get; set; } = null!;
    public virtual UserEntity User { get; set; } = null!;
}
