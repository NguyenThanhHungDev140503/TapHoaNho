using Application.Abstractions.Messaging;
using Application.Features.Suppliers.Dtos;

namespace Application.Features.Suppliers.Queries;

public record GetSupplierByIdQuery(int Id) : IQuery<SupplierDto>;
