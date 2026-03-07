using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

/// <summary>
/// Entity sản phẩm
/// </summary>
public class ProductEntity : BaseEntity<int>
{
    [Required]
    public int CategoryId { get; set; }

    [Required]
    public int SupplierId { get; set; }

    [Required]
    [MaxLength(100)]
    public string ProductName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Barcode { get; set; } = string.Empty;

    [Required]
    public decimal Price { get; set; }

    [MaxLength(20)]
    public string Unit { get; set; } = "pcs";

    /// <summary>
    /// URL ảnh sản phẩm
    /// </summary>
    [MaxLength(1024)]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// ImageKit file identifier dùng để build URL ảnh
    /// </summary>
    [MaxLength(255)]
    public string? ImageFileId { get; set; }

    // Navigation properties
    public virtual CategoryEntity Category { get; set; } = null!;
    public virtual SupplierEntity Supplier { get; set; } = null!;
    public virtual InventoryEntity? Inventory { get; set; }
    public virtual ICollection<OrderItemEntity> OrderItems { get; set; } = [];
}
