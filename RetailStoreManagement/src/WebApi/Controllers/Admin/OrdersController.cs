using Application.Common.Models;
using Application.Features.Orders.Commands;
using Application.Features.Orders.Dtos;
using Application.Features.Orders.Queries;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebApi.Abstractions;

namespace WebApi.Controllers.Admin;

[Route("api/admin/orders")]
[Authorize]
public class OrdersController(IMediator mediator) : BaseApiController(mediator)
{
    [HttpGet]
    [Authorize(Roles = "Admin,Staff")]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<OrderDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrders([FromQuery] GetOrdersQuery query)
    {
        return Ok(await Mediator.Send(query));
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin,Staff")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrderById(int id)
    {
        return Ok(await Mediator.Send(new GetOrderByIdQuery(id)));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Staff")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderCommand command)
    {
        var result = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetOrderById), new { id = result.Data?.Id }, result);
    }

    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = "Admin,Staff")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusRequest request)
    {
        return Ok(await Mediator.Send(new UpdateOrderStatusCommand(id, request.Status)));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteOrder(int id)
    {
        return Ok(await Mediator.Send(new DeleteOrderCommand(id)));
    }

    [HttpPost("{orderId:int}/items")]
    [Authorize(Roles = "Admin,Staff")]
    [ProducesResponseType(typeof(ApiResponse<OrderItemDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddOrderItem(int orderId, [FromBody] AddOrderItemRequest request)
    {
        var command = new AddOrderItemCommand(orderId, request.ProductId, request.Quantity, request.Price);
        return Created("", await Mediator.Send(command));
    }

    [HttpPut("{orderId:int}/items/{itemId:int}")]
    [Authorize(Roles = "Admin,Staff")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateOrderItem(int orderId, int itemId, [FromBody] UpdateOrderItemRequest request)
    {
        return Ok(await Mediator.Send(new UpdateOrderItemCommand(orderId, itemId, request.Quantity)));
    }

    [HttpDelete("{orderId:int}/items/{itemId:int}")]
    [Authorize(Roles = "Admin,Staff")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteOrderItem(int orderId, int itemId)
    {
        return Ok(await Mediator.Send(new DeleteOrderItemCommand(orderId, itemId)));
    }

    [HttpGet("total-revenue")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<decimal>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTotalRevenue([FromQuery] GetTotalRevenueQuery query)
    {
        return Ok(await Mediator.Send(query));
    }

    [HttpGet("{id:int}/invoice")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetOrderInvoice(int id)
    {
        var result = await Mediator.Send(new GetOrderInvoiceQuery(id));
        if (!result.Succeeded || result.Data == null)
             return StatusCode(result.ResponseCode, result);

        // Mock PDF generation (Text content for now)
        var invoice = result.Data;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"INVOICE #{invoice.OrderId}");
        sb.AppendLine($"DATE: {invoice.OrderDate}");
        sb.AppendLine($"CUSTOMER: {invoice.CustomerName}");
        sb.AppendLine("--------------------------------");
        foreach(var item in invoice.Items)
        {
             sb.AppendLine($"{item.ProductName} x {item.Quantity} : {item.Subtotal}");
        }
        sb.AppendLine("--------------------------------");
        sb.AppendLine($"TOTAL: {invoice.TotalAmount}");
        
        var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "application/pdf", $"invoice_{id}.pdf");
    }
}

public record UpdateOrderStatusRequest(OrderStatus Status);
public record AddOrderItemRequest(int ProductId, int Quantity, decimal Price);
public record UpdateOrderItemRequest(int Quantity);
