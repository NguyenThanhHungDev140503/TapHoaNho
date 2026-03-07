using Application.Abstractions.Messaging;
using Application.Common.Models;
using Application.Features.Customers.Commands;
using Application.Features.Customers.Dtos;
using Domain.Entities;
using Domain.SeedWork;

namespace Application.Features.Customers.Handlers;

public class CreateCustomerCommandHandler(IUnitOfWork unitOfWork) 
    : ICommandHandler<CreateCustomerCommand, CustomerDto>
{
    public async Task<ApiResponse<CustomerDto>> Handle(
        CreateCustomerCommand request, 
        CancellationToken cancellationToken)
    {
        var customer = new CustomerEntity
        {
            Name = request.Name,
            Phone = request.Phone,
            Email = request.Email,
            Address = request.Address
        };
        
        await unitOfWork.Repository<CustomerEntity>().AddAsync(customer);
        await unitOfWork.SaveChangesAsync();
        
        var dto = new CustomerDto
        {
            Id = customer.Id,
            Name = customer.Name,
            Phone = customer.Phone,
            Email = customer.Email,
            Address = customer.Address,
            OrderCount = 0,
            CreatedAt = customer.CreatedAt
        };
        
        return ApiResponse<CustomerDto>.Success(dto, "Tạo khách hàng thành công");
    }
}
