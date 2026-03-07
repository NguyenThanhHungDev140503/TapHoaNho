using Application.Abstractions.Messaging;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Features.Customers.Commands;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customers.Handlers;

public class UpdateCustomerCommandHandler(IUnitOfWork unitOfWork) 
    : ICommandHandler<UpdateCustomerCommand, bool>
{
    public async Task<ApiResponse<bool>> Handle(
        UpdateCustomerCommand request, 
        CancellationToken cancellationToken)
    {
        var customer = await unitOfWork.Repository<CustomerEntity>()
            .GetAll()
            .FirstOrDefaultAsync(x => x.Id == request.Id && !x.DeletedAt.HasValue, cancellationToken);

        if (customer is null)
            throw new NotFoundException("Khách hàng", request.Id);

        customer.Name = request.Name;
        customer.Phone = request.Phone;
        customer.Email = request.Email;
        customer.Address = request.Address;
        customer.UpdatedAt = DateTime.UtcNow;
        
        await unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.Success(true, "Cập nhật khách hàng thành công");
    }
}
