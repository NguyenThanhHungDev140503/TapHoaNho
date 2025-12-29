namespace Application.Features.Suppliers.Dtos;

public class SupplierDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public int ProductCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
