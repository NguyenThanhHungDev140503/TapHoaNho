using Application.Abstractions.Messaging;
using Application.Features.Customers.Dtos;

namespace Application.Features.Customers.Commands;

public record CreateCustomerCommand(
    string Name,
    string Phone,
    string? Email = null,
    string? Address = null
) : ICommand<CustomerDto>;
