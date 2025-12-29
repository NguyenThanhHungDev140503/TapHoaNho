using System.ComponentModel.DataAnnotations;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Entity khuyến mãi
/// </summary>
public class PromotionEntity : BaseEntity<int>
{
    [Required]
    [MaxLength(50)]
    public string PromoCode { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Description { get; set; }

    [Required]
    public DiscountType DiscountType { get; set; }

    [Required]
    public decimal DiscountValue { get; set; }

    [Required]
    public DateOnly StartDate { get; set; }

    [Required]
    public DateOnly EndDate { get; set; }

    /// <summary>
    /// Giá trị đơn hàng tối thiểu để áp dụng khuyến mãi
    /// </summary>
    public decimal MinOrderAmount { get; set; } = 0;

    /// <summary>
    /// Số lần sử dụng tối đa (0 = không giới hạn)
    /// </summary>
    public int UsageLimit { get; set; } = 0;

    /// <summary>
    /// Số lần đã sử dụng
    /// </summary>
    public int UsedCount { get; set; } = 0;

    public PromotionStatus Status { get; set; } = PromotionStatus.Active;

    // Navigation properties
    public virtual ICollection<OrderEntity> Orders { get; set; } = [];
}
