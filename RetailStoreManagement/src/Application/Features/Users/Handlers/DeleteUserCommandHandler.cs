using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Users.Commands;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Users.Handlers;

public class DeleteUserCommandHandler(IUnitOfWork unitOfWork) 
    : ICommandHandler<DeleteUserCommand, bool>
{
    public async Task<ApiResponse<bool>> Handle(
        DeleteUserCommand request, 
        CancellationToken cancellationToken)
    {
        var user = await unitOfWork.Repository<UserEntity>()
            .GetAll()
            .Include(u => u.Orders)
            .FirstOrDefaultAsync(x => x.Id == request.Id && !x.DeletedAt.HasValue, cancellationToken);

        if (user is null)
            throw new NotFoundException("Người dùng", request.Id);

        if (user.Orders.Any(o => !o.DeletedAt.HasValue))
        {
            return ApiResponse<bool>.Failure(
                $"Không thể xóa người dùng này vì đang có đơn hàng liên quan. " +
                "Vui lòng xóa hoặc chuyển các đơn hàng trước khi xóa người dùng.",
                400
            );
        }

        user.DeletedAt = DateTime.UtcNow;
        
        await unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.Success(true, "Xóa người dùng thành công");
    }
}
