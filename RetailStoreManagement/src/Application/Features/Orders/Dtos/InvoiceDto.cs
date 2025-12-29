namespace Application.Features.Orders.Dtos;

public class InvoiceDto
{
    public int OrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = string.Empty;
    
    // Customer info
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerAddress { get; set; }
    
    // Staff info  
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    
    // Items
    public List<InvoiceItemDto> Items { get; set; } = [];
    
    // Totals
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? PromoCode { get; set; }
    public decimal TotalAmount { get; set; }
    
    // Payment info
    public List<PaymentDto> Payments { get; set; } = [];
}

public class InvoiceItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
}

public class PaymentDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
}
