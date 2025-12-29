using Application.Abstractions.Messaging;
using Application.Common.Models;
using Application.Features.Suppliers.Commands;
using Application.Features.Suppliers.Dtos;
using Domain.Entities;
using Domain.SeedWork;

namespace Application.Features.Suppliers.Handlers;

public class CreateSupplierCommandHandler(IUnitOfWork unitOfWork) 
    : ICommandHandler<CreateSupplierCommand, SupplierDto>
{
    public async Task<ApiResponse<SupplierDto>> Handle(
        CreateSupplierCommand request, 
        CancellationToken cancellationToken)
    {
        var supplier = new SupplierEntity
        {
            Name = request.Name,
            Phone = request.Phone,
            Email = request.Email,
            Address = request.Address
        };
        
        await unitOfWork.Repository<SupplierEntity>().AddAsync(supplier);
        await unitOfWork.SaveChangesAsync();
        
        var dto = new SupplierDto
        {
            Id = supplier.Id,
            Name = supplier.Name,
            Phone = supplier.Phone,
            Email = supplier.Email,
            Address = supplier.Address,
            ProductCount = 0,
            CreatedAt = supplier.CreatedAt
        };
        
        return ApiResponse<SupplierDto>.Success(dto, "Tạo nhà cung cấp thành công");
    }
}
