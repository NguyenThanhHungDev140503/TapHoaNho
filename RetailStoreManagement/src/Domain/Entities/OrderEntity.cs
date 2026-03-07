using System.ComponentModel.DataAnnotations;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Entity đơn hàng
/// </summary>
public class OrderEntity : BaseEntity<int>
{
    public int CustomerId { get; set; }

    [Required]
    public int UserId { get; set; }

    public int? PromoId { get; set; }

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public decimal TotalAmount { get; set; }

    public decimal DiscountAmount { get; set; } = 0;

    // Navigation properties
    public virtual CustomerEntity? Customer { get; set; }
    public virtual UserEntity User { get; set; } = null!;
    public virtual PromotionEntity? Promotion { get; set; }
    public virtual ICollection<OrderItemEntity> OrderItems { get; set; } = [];
    public virtual ICollection<PaymentEntity> Payments { get; set; } = [];
}
