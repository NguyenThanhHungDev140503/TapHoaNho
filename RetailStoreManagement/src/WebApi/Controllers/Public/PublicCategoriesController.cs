using Application.Common.Models;
using Application.Features.Categories.Dtos;
using Application.Features.Categories.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using WebApi.Abstractions;

namespace WebApi.Controllers.Public;

[Route("api/public/categories")]
public class PublicCategoriesController(IMediator mediator) : BaseApiController(mediator)
{
    [HttpGet]
    public async Task<IActionResult> GetCategories([FromQuery] GetCategoriesQuery query)
    {
        return Ok(await Mediator.Send(query));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCategoryById(int id)
    {
        return Ok(await Mediator.Send(new GetCategoryByIdQuery(id)));
    }
}
