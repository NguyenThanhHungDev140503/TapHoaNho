namespace RetailStoreManagement.Models.Product;

public class ProductListDto
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public int SupplierId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public int InventoryQuantity { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageFileId { get; set; }
}
