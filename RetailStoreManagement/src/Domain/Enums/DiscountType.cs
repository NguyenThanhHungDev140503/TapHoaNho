namespace Domain.Enums;

/// <summary>
/// Giảm giá theo phần trăm hoặc số tiền cố định
/// </summary>
public enum DiscountType
{
    /// <summary>
    /// Giảm giá theo phần trăm
    /// </summary>
    Percent = 0,
    
    /// <summary>
    /// Giảm giá theo số tiền cố định
    /// </summary>
    Fixed = 1
}
