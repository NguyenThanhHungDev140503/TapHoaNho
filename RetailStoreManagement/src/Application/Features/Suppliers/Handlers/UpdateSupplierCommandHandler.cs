using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Suppliers.Commands;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Suppliers.Handlers;

public class UpdateSupplierCommandHandler(IUnitOfWork unitOfWork) 
    : ICommandHandler<UpdateSupplierCommand, bool>
{
    public async Task<ApiResponse<bool>> Handle(
        UpdateSupplierCommand request, 
        CancellationToken cancellationToken)
    {
        var supplier = await unitOfWork.Repository<SupplierEntity>()
            .GetAll()
            .FirstOrDefaultAsync(x => x.Id == request.Id && !x.DeletedAt.HasValue, cancellationToken);

        if (supplier is null)
            throw new NotFoundException("Nhà cung cấp", request.Id);

        supplier.Name = request.Name;
        supplier.Phone = request.Phone;
        supplier.Email = request.Email;
        supplier.Address = request.Address;
        supplier.UpdatedAt = DateTime.UtcNow;
        
        await unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.Success(true, "Cập nhật nhà cung cấp thành công");
    }
}
