using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

/// <summary>
/// Entity tồn kho sản phẩm
/// </summary>
public class InventoryEntity : BaseEntity<int>
{
    [Required]
    public int ProductId { get; set; }

    /// <summary>
    /// Số lượng tồn kho hiện tại
    /// </summary>
    public int Quantity { get; set; } = 0;

    // Navigation properties
    public virtual ProductEntity Product { get; set; } = null!;
}
