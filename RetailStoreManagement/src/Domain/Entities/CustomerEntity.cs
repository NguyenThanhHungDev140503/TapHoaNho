using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

/// <summary>
/// Entity khách hàng
/// </summary>
public class CustomerEntity : BaseEntity<int>
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(1024)]
    public string? Address { get; set; }

    // Navigation properties
    public virtual ICollection<OrderEntity> Orders { get; set; } = [];
}
