using Application.Abstractions.Messaging;
using Application.Common.Models;
using Application.Features.Customers.Dtos;
using Application.Features.Customers.Queries;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customers.Handlers;

public class GetCustomersQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetCustomersQuery, PaginatedResponse<CustomerDto>>
{
    public async Task<ApiResponse<PaginatedResponse<CustomerDto>>> Handle(
        GetCustomersQuery request, 
        CancellationToken cancellationToken)
    {
        var query = unitOfWork.Repository<CustomerEntity>()
            .GetAllReadOnly()
            .Where(x => !x.DeletedAt.HasValue)
            .AsNoTracking();

        if (!string.IsNullOrEmpty(request.Search))
        {
            var term = request.Search.ToLower();
            query = query.Where(x => 
                x.Name.ToLower().Contains(term) || 
                x.Phone.Contains(term));
        }

        var dtoQuery = query.Select(x => new CustomerDto
        {
            Id = x.Id,
            Name = x.Name,
            Phone = x.Phone,
            Email = x.Email,
            Address = x.Address,
            OrderCount = x.Orders.Count(o => !o.DeletedAt.HasValue),
            CreatedAt = x.CreatedAt
        });

        var response = await PaginatedResponse<CustomerDto>.CreateAsync(
            dtoQuery, request.Page, request.PageSize);
            
        return ApiResponse<PaginatedResponse<CustomerDto>>.Success(response);
    }
}
