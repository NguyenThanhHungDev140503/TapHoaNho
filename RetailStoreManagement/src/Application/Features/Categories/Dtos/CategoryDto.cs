namespace Application.Features.Categories.Dtos;

/// <summary>
/// DTO cho thông tin danh mục
/// </summary>
public class CategoryDto
{
    public int Id { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int ProductCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
