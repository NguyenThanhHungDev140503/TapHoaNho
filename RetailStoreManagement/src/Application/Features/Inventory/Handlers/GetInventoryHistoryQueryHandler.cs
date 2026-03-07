using Application.Abstractions.Messaging;
using Application.Common.Models;
using Application.Features.Inventory.Dtos;
using Application.Features.Inventory.Queries;
using Domain.Entities;
using Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Inventory.Handlers;

public class GetInventoryHistoryQueryHandler(IUnitOfWork unitOfWork) 
    : IQueryHandler<GetInventoryHistoryQuery, PaginatedResponse<InventoryHistoryDto>>
{
    public async Task<ApiResponse<PaginatedResponse<InventoryHistoryDto>>> Handle(
        GetInventoryHistoryQuery request, 
        CancellationToken cancellationToken)
    {
        var query = unitOfWork.Repository<InventoryHistoryEntity>()
            .GetAllReadOnly()
            .Where(x => x.ProductId == request.ProductId)
            .OrderByDescending(x => x.CreatedAt)
            .AsNoTracking();

        var dtoQuery = query.Select(x => new InventoryHistoryDto
        {
            Id = x.Id,
            ProductId = x.ProductId,
            ProductName = x.Product.ProductName,
            UserId = x.UserId,
            UserName = x.User.Username,
            QuantityChange = x.QuantityChange,
            QuantityAfter = x.QuantityAfter,
            Reason = x.Reason,
            CreatedAt = x.CreatedAt
        });

        var response = await PaginatedResponse<InventoryHistoryDto>.CreateAsync(
            dtoQuery, request.Page, request.PageSize);
            
        return ApiResponse<PaginatedResponse<InventoryHistoryDto>>.Success(response);
    }
}
