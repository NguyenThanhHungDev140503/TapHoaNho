using Application.Abstractions.Messaging;
using Application.Features.Customers.Dtos;

namespace Application.Features.Customers.Queries;

public record GetCustomerByIdQuery(int Id) : IQuery<CustomerDto>;
