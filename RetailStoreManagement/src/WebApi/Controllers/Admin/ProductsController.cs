using Application.Common.Models;
using Application.Features.Products.Commands;
using Application.Features.Products.Dtos;
using Application.Features.Products.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Abstractions;

namespace WebApi.Controllers.Admin;

/// <summary>
/// Controller quản lý sản phẩm
/// </summary>
[Route("api/admin/products")]
[Authorize]
public class ProductsController(IMediator mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Lấy danh sách sản phẩm với phân trang
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<ProductDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProducts([FromQuery] GetProductsQuery query)
    {
        return Ok(await Mediator.Send(query));
    }

    /// <summary>
    /// Lấy chi tiết sản phẩm theo ID
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductById(int id)
    {
        return Ok(await Mediator.Send(new GetProductByIdQuery(id)));
    }

    /// <summary>
    /// Tạo sản phẩm mới
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductCommand command)
    {
        var result = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetProductById), new { id = result.Data?.Id }, result);
    }

    /// <summary>
    /// Cập nhật sản phẩm
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProduct(int id, [FromBody] UpdateProductCommand command)
    {
        if (id != command.Id)
            return BadRequest(ApiResponse<bool>.Failure("ID không khớp"));
            
        return Ok(await Mediator.Send(command));
    }

    /// <summary>
    /// Xóa sản phẩm (soft delete)
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        return Ok(await Mediator.Send(new DeleteProductCommand(id)));
    }

    /// <summary>
    /// Lấy danh sách sản phẩm theo IDs
    /// </summary>
    [HttpPost("by-ids")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProductsByIds([FromBody] List<int> ids)
    {
        return Ok(await Mediator.Send(new GetProductsByIdsQuery(ids)));
    }
}
