using Application.Common.Models;
using Application.Features.Inventory.Commands;
using Application.Features.Inventory.Dtos;
using Application.Features.Inventory.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebApi.Abstractions;

namespace WebApi.Controllers.Admin;

[Route("api/admin/inventory")]
[Authorize]
public class InventoryController(IMediator mediator) : BaseApiController(mediator)
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<InventoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInventory([FromQuery] GetInventoryQuery query)
    {
        return Ok(await Mediator.Send(query));
    }

    [HttpGet("{productId:int}")]
    [ProducesResponseType(typeof(ApiResponse<InventoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInventoryByProductId(int productId)
    {
        return Ok(await Mediator.Send(new GetInventoryByProductIdQuery(productId)));
    }

    [HttpPatch("{productId:int}")]
    [ProducesResponseType(typeof(ApiResponse<InventoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateInventory(int productId, [FromBody] UpdateInventoryRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var command = new UpdateInventoryCommand(productId, request.QuantityChange, request.Reason, userId);
        return Ok(await Mediator.Send(command));
    }

    [HttpGet("low-stock")]
    [ProducesResponseType(typeof(ApiResponse<List<InventoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLowStockAlerts()
    {
        return Ok(await Mediator.Send(new GetLowStockQuery()));
    }

    [HttpGet("{productId:int}/history")]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<InventoryHistoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInventoryHistory(int productId, [FromQuery] GetInventoryHistoryQuery query)
    {
        query.ProductId = productId;
        return Ok(await Mediator.Send(query));
    }
}

public class UpdateInventoryRequest
{
    public int QuantityChange { get; set; }
    public string Reason { get; set; } = string.Empty;
}
