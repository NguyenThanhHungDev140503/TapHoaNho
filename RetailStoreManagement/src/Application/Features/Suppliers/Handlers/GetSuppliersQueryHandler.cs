using Application.Abstractions.Messaging;
using Application.Common.Models;
using Application.Features.Suppliers.Dtos;
using Application.Features.Suppliers.Queries;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Suppliers.Handlers;

public class GetSuppliersQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetSuppliersQuery, PaginatedResponse<SupplierDto>>
{
    public async Task<ApiResponse<PaginatedResponse<SupplierDto>>> Handle(
        GetSuppliersQuery request, 
        CancellationToken cancellationToken)
    {
        var query = unitOfWork.Repository<SupplierEntity>()
            .GetAll()
            .Where(x => !x.DeletedAt.HasValue)
            .AsNoTracking();

        if (!string.IsNullOrEmpty(request.Search))
        {
            var term = request.Search.ToLower();
            query = query.Where(x => 
                x.Name.ToLower().Contains(term) || 
                x.Phone.Contains(term));
        }

        var dtoQuery = query.Select(x => new SupplierDto
        {
            Id = x.Id,
            Name = x.Name,
            Phone = x.Phone,
            Email = x.Email,
            Address = x.Address,
            ProductCount = x.Products.Count(p => !p.DeletedAt.HasValue),
            CreatedAt = x.CreatedAt
        });

        var response = await PaginatedResponse<SupplierDto>.CreateAsync(
            dtoQuery, request.Page, request.PageSize);
            
        return ApiResponse<PaginatedResponse<SupplierDto>>.Success(response);
    }
}
