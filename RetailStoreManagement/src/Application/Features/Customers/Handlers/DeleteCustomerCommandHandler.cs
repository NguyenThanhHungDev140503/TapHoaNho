using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Customers.Commands;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customers.Handlers;

public class DeleteCustomerCommandHandler(IUnitOfWork unitOfWork) 
    : ICommandHandler<DeleteCustomerCommand, bool>
{
    public async Task<ApiResponse<bool>> Handle(
        DeleteCustomerCommand request, 
        CancellationToken cancellationToken)
    {
        var customer = await unitOfWork.Repository<CustomerEntity>()
            .GetAll()
            .Include(c => c.Orders)
            .FirstOrDefaultAsync(x => x.Id == request.Id && !x.DeletedAt.HasValue, cancellationToken);

        if (customer is null)
            throw new NotFoundException("Khách hàng", request.Id);

        if (customer.Orders.Any(o => !o.DeletedAt.HasValue))
        {
            return ApiResponse<bool>.Failure(
                $"Không thể xóa khách hàng này vì đang có đơn hàng liên quan. " +
                "Vui lòng xóa hoặc chuyển các đơn hàng trước khi xóa khách hàng.",
                400
            );
        }

        customer.DeletedAt = DateTime.UtcNow;
        
        await unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.Success(true, "Xóa khách hàng thành công");
    }
}
