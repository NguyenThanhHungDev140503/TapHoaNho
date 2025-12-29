using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

/// <summary>
/// Entity danh mục sản phẩm
/// </summary>
public class CategoryEntity : BaseEntity<int>
{
    [Required]
    [MaxLength(100)]
    public string CategoryName { get; set; } = string.Empty;

    // Navigation properties
    public virtual ICollection<ProductEntity> Products { get; set; } = [];
}
