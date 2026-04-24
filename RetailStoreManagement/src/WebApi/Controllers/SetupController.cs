using Application.Features.Setup.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Abstractions;

namespace WebApi.Controllers;

/// <summary>
/// Bootstrap controller — tạo admin đầu tiên của hệ thống khi DB rỗng.
/// Endpoint này là một lần bootstrap, sau đó authentication thực hiện
/// qua Duende IdentityServer (https://localhost:5001).
/// </summary>
[Route("api/setup")]
public class SetupController(IMediator mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Tạo admin đầu tiên. Chỉ thành công khi chưa có admin nào trong DB.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("admin")]
    public async Task<IActionResult> SetupAdmin([FromBody] SetupAdminCommand command)
    {
        return Ok(await Mediator.Send(command));
    }
}
