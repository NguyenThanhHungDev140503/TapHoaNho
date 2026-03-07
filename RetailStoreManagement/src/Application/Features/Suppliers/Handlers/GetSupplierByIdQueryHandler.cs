using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Suppliers.Dtos;
using Application.Features.Suppliers.Queries;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Suppliers.Handlers;

public class GetSupplierByIdQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetSupplierByIdQuery, SupplierDto>
{
    public async Task<ApiResponse<SupplierDto>> Handle(
        GetSupplierByIdQuery request, 
        CancellationToken cancellationToken)
    {
        var supplier = await unitOfWork.Repository<SupplierEntity>()
            .GetAll()
            .Where(x => x.Id == request.Id && !x.DeletedAt.HasValue)
            .Select(x => new SupplierDto
            {
                Id = x.Id,
                Name = x.Name,
                Phone = x.Phone,
                Email = x.Email,
                Address = x.Address,
                ProductCount = x.Products.Count(p => !p.DeletedAt.HasValue),
                CreatedAt = x.CreatedAt
            })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (supplier is null)
            throw new NotFoundException("Nhà cung cấp", request.Id);

        return ApiResponse<SupplierDto>.Success(supplier);
    }
}
