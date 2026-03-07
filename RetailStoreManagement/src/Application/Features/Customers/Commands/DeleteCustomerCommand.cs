using Application.Abstractions.Messaging;

namespace Application.Features.Customers.Commands;

public record DeleteCustomerCommand(int Id) : ICommand<bool>;
