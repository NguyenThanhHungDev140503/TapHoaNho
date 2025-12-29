using Application.Common.Models;
using Application.Features.Promotions.Commands;
using Application.Features.Promotions.Dtos;
using Application.Features.Promotions.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Abstractions;

namespace WebApi.Controllers.Admin;

[Route("api/admin/promotions")]
[Authorize(Roles = "Admin")]
public class PromotionsController(IMediator mediator) : BaseApiController(mediator)
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<PromotionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPromotions([FromQuery] GetPromotionsQuery query)
    {
        return Ok(await Mediator.Send(query));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<PromotionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPromotionById(int id)
    {
        return Ok(await Mediator.Send(new GetPromotionByIdQuery(id)));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PromotionDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreatePromotion([FromBody] CreatePromotionCommand command)
    {
        var result = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetPromotionById), new { id = result.Data?.Id }, result);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePromotion(int id, [FromBody] UpdatePromotionCommand command)
    {
        if (id != command.Id)
            return BadRequest(ApiResponse<bool>.Failure("ID không khớp"));
        return Ok(await Mediator.Send(command));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePromotion(int id)
    {
        return Ok(await Mediator.Send(new DeletePromotionCommand(id)));
    }

    [HttpGet("active-count")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActivePromotionCount()
    {
        return Ok(await Mediator.Send(new GetActivePromotionCountQuery()));
    }

    [HttpPost("validate")]
    [ProducesResponseType(typeof(ApiResponse<ValidatePromotionResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidatePromotion([FromBody] ValidatePromotionCommand command)
    {
        return Ok(await Mediator.Send(command));
    }
}
