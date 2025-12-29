namespace Domain.Enums;

/// <summary>
/// Phương thức thanh toán
/// </summary>
public enum PaymentMethod
{
    /// <summary>
    /// Tiền mặt
    /// </summary>
    Cash = 0,
    
    /// <summary>
    /// Thẻ (Credit/Debit)
    /// </summary>
    Card = 1,
    
    /// <summary>
    /// Chuyển khoản ngân hàng
    /// </summary>
    BankTransfer = 2,
    
    /// <summary>
    /// Ví điện tử
    /// </summary>
    EWallet = 3
}
