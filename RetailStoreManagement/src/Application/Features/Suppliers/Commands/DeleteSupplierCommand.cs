using Application.Abstractions.Messaging;

namespace Application.Features.Suppliers.Commands;

public record DeleteSupplierCommand(int Id) : ICommand<bool>;
