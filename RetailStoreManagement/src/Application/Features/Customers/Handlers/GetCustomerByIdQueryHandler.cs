using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Customers.Dtos;
using Application.Features.Customers.Queries;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customers.Handlers;

public class GetCustomerByIdQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetCustomerByIdQuery, CustomerDto>
{
    public async Task<ApiResponse<CustomerDto>> Handle(
        GetCustomerByIdQuery request, 
        CancellationToken cancellationToken)
    {
        var customer = await unitOfWork.Repository<CustomerEntity>()
            .GetAllReadOnly()
            .Where(x => x.Id == request.Id && !x.DeletedAt.HasValue)
            .Select(x => new CustomerDto
            {
                Id = x.Id,
                Name = x.Name,
                Phone = x.Phone,
                Email = x.Email,
                Address = x.Address,
                OrderCount = x.Orders.Count(o => !o.DeletedAt.HasValue),
                CreatedAt = x.CreatedAt
            })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (customer is null)
            throw new NotFoundException("Khách hàng", request.Id);

        return ApiResponse<CustomerDto>.Success(customer);
    }
}
