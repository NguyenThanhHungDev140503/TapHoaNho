using Application.Abstractions.Messaging;
using Application.Features.Suppliers.Dtos;

namespace Application.Features.Suppliers.Commands;

public record CreateSupplierCommand(
    string Name,
    string Phone,
    string? Email = null,
    string? Address = null
) : ICommand<SupplierDto>;
