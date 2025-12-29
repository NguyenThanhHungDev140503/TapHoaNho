using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Suppliers.Commands;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Suppliers.Handlers;

public class DeleteSupplierCommandHandler(IUnitOfWork unitOfWork) 
    : ICommandHandler<DeleteSupplierCommand, bool>
{
    public async Task<ApiResponse<bool>> Handle(
        DeleteSupplierCommand request, 
        CancellationToken cancellationToken)
    {
        var supplier = await unitOfWork.Repository<SupplierEntity>()
            .GetAll()
            .Include(s => s.Products)
            .FirstOrDefaultAsync(x => x.Id == request.Id && !x.DeletedAt.HasValue, cancellationToken);

        if (supplier is null)
            throw new NotFoundException("Nhà cung cấp", request.Id);

        if (supplier.Products.Any(p => !p.DeletedAt.HasValue))
        {
            return ApiResponse<bool>.Failure(
                $"Không thể xóa nhà cung cấp này vì đang có sản phẩm liên quan. " +
                "Vui lòng xóa hoặc chuyển các sản phẩm trước khi xóa nhà cung cấp.",
                400
            );
        }

        supplier.DeletedAt = DateTime.UtcNow;
        
        await unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.Success(true, "Xóa nhà cung cấp thành công");
    }
}
