using Application.Abstractions.Messaging;

namespace Application.Features.Suppliers.Commands;

public record UpdateSupplierCommand(
    int Id,
    string Name,
    string Phone,
    string? Email = null,
    string? Address = null
) : ICommand<bool>;
