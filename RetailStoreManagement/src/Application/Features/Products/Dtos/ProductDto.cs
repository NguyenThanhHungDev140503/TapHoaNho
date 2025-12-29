using System.Text.Json.Serialization;
using Domain.Enums;

namespace Application.Features.Products.Dtos;

/// <summary>
/// DTO cho thông tin sản phẩm
/// </summary>
public class ProductDto
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Unit { get; set; } = "pcs";
    public string? ImageUrl { get; set; }
    
    // Legacy support: New code uses image Url directly, legacy might use this field to delete/update image.
    public string? ImageFileId { get; set; } 
    
    public int CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public int SupplierId { get; set; }
    public string? SupplierName { get; set; }
    
    [JsonIgnore]
    public int? StockQuantity { get; set; }
    
    // Legacy support: "InventoryQuantity" non-nullable
    public int InventoryQuantity => StockQuantity ?? 0;
    
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
