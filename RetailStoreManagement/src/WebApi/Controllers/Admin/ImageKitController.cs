using Application.Common.Models;
using Application.Features.ImageKit.Commands;
using Application.Features.ImageKit.Dtos;
using Application.Features.ImageKit.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Abstractions;

namespace WebApi.Controllers.Admin;

[Route("api/admin/imagekit")]
[Authorize(Roles = "Admin")]
public class ImageKitController(IMediator mediator) : BaseApiController(mediator)
{
    [HttpPost("auth")]
    [ProducesResponseType(typeof(ApiResponse<ImageKitAuthResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuth()
    {
        return Ok(await Mediator.Send(new GetImageKitAuthQuery()));
    }

    [HttpDelete("file/{fileId}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteFile(string fileId)
    {
        return Ok(await Mediator.Send(new DeleteImageKitFileCommand(fileId)));
    }
}
