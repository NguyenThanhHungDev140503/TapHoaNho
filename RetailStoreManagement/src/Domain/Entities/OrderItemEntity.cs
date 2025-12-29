using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

/// <summary>
/// Entity chi tiết đơn hàng (order line item)
/// </summary>
public class OrderItemEntity : BaseEntity<int>
{
    [Required]
    public int OrderId { get; set; }

    [Required]
    public int ProductId { get; set; }

    [Required]
    public int Quantity { get; set; }

    /// <summary>
    /// Giá tại thời điểm đặt hàng
    /// </summary>
    [Required]
    public decimal Price { get; set; }

    /// <summary>
    /// Thành tiền = Quantity * Price
    /// </summary>
    [Required]
    public decimal Subtotal { get; set; }

    // Navigation properties
    public virtual OrderEntity Order { get; set; } = null!;
    public virtual ProductEntity Product { get; set; } = null!;
}
