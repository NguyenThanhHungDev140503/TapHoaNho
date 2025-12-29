using Domain.Enums;

namespace Application.Features.Promotions.Dtos;

public class PromotionDto
{
    public int Id { get; set; }
    public string PromoCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal MinOrderAmount { get; set; }
    public int UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public PromotionStatus Status { get; set; }
    public bool IsActive => Status == PromotionStatus.Active && 
        DateOnly.FromDateTime(DateTime.UtcNow) >= StartDate && 
        DateOnly.FromDateTime(DateTime.UtcNow) <= EndDate;
    public DateTime CreatedAt { get; set; }
}
