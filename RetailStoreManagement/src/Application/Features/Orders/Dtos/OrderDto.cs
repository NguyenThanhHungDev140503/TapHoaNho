using System.Text.Json.Serialization;
using Domain.Enums;

namespace Application.Features.Orders.Dtos;

public class OrderDto
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public int UserId { get; set; }
    
    [JsonIgnore]
    public string? UserName { get; set; }

    [JsonIgnore]
    public string? UserFullName { get; set; }
    
    // Legacy support: "StaffName"
    public string StaffName => UserFullName ?? UserName ?? string.Empty;
    
    public int? PromoId { get; set; }
    public string? PromoCode { get; set; }
    public DateTime OrderDate { get; set; }
    
    [JsonIgnore]
    public OrderStatus StatusEnum { get; set; }
    
    // Legacy support: Status as string
    public string Status 
    { 
        get => StatusEnum.ToString();
        set 
        {
            if (Enum.TryParse<OrderStatus>(value, true, out var result))
            {
                StatusEnum = result;
            }
        }
    }

    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount => TotalAmount - DiscountAmount;
    public List<OrderItemDto> Items { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

public class OrderItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Subtotal { get; set; }
}
