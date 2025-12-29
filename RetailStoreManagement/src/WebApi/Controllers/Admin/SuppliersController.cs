using Application.Common.Models;
using Application.Features.Suppliers.Commands;
using Application.Features.Suppliers.Dtos;
using Application.Features.Suppliers.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Abstractions;

namespace WebApi.Controllers.Admin;

[Route("api/admin/suppliers")]
[Authorize]
public class SuppliersController(IMediator mediator) : BaseApiController(mediator)
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<SupplierDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSuppliers([FromQuery] GetSuppliersQuery query)
    {
        return Ok(await Mediator.Send(query));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<SupplierDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSupplierById(int id)
    {
        return Ok(await Mediator.Send(new GetSupplierByIdQuery(id)));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<SupplierDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateSupplier([FromBody] CreateSupplierCommand command)
    {
        var result = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetSupplierById), new { id = result.Data?.Id }, result);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSupplier(int id, [FromBody] UpdateSupplierCommand command)
    {
        if (id != command.Id)
            return BadRequest(ApiResponse<bool>.Failure("ID không khớp"));
        return Ok(await Mediator.Send(command));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSupplier(int id)
    {
        return Ok(await Mediator.Send(new DeleteSupplierCommand(id)));
    }
}
