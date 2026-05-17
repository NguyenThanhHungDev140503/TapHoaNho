using Application.Features.Setup.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebApi.Abstractions;

namespace WebApi.Controllers;

[Route("api/setup")]
public class SetupController(IMediator mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Tạo admin đầu tiên. Chỉ thành công khi chưa có admin nào trong DB.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting("SetupPolicy")]
    [HttpPost("admin")]
    public async Task<IActionResult> SetupAdmin([FromBody] SetupAdminCommand command)
    {
        return Ok(await Mediator.Send(command));
    }
}
