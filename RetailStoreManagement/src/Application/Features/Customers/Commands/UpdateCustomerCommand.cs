using Application.Abstractions.Messaging;

namespace Application.Features.Customers.Commands;

public record UpdateCustomerCommand(
    int Id,
    string Name,
    string Phone,
    string? Email = null,
    string? Address = null
) : ICommand<bool>;
