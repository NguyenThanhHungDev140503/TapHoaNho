namespace Domain.Enums;

/// <summary>
/// Trạng thái đơn hàng
/// </summary>
public enum OrderStatus
{
    /// <summary>
    /// Đơn hàng đang chờ xử lý
    /// </summary>
    Pending = 0,
    
    /// <summary>
    /// Đã thanh toán
    /// </summary>
    Paid = 1,
    
    /// <summary>
    /// Đã hủy
    /// </summary>
    Canceled = 2
}
