using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Abstractions;

/// <summary>
/// Base API Controller - cung cấp IMediator cho tất cả controllers
/// </summary>
[Route("api/[controller]")]
[ApiController]
public abstract class BaseApiController(IMediator mediator) : ControllerBase
{
    protected readonly IMediator Mediator = mediator;
}
