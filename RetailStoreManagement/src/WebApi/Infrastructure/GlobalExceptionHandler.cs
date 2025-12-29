using Application.Common.Exceptions;
using Application.Common.Models;
using Microsoft.AspNetCore.Diagnostics;
using System.Net;
using System.Text.Json;

namespace WebApi.Infrastructure;

/// <summary>
/// Global Exception Handler - xử lý tất cả exceptions và trả về ApiResponse thống nhất
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, 
        Exception exception, 
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Exception: {Message}", exception.Message);

        var (response, statusCode) = exception switch
        {
            ValidationException validationEx => (
                new ApiResponse<object>
                {
                    Succeeded = false,
                    ResponseCode = (int)HttpStatusCode.BadRequest,
                    Message = "Dữ liệu không hợp lệ",
                    Errors = validationEx.Errors
                },
                HttpStatusCode.BadRequest
            ),
            NotFoundException notFoundEx => (
                ApiResponse<object>.NotFound(notFoundEx.Message),
                HttpStatusCode.NotFound
            ),
            BadRequestException badRequestEx => (
                ApiResponse<object>.Failure(badRequestEx.Message),
                HttpStatusCode.BadRequest
            ),
            UnauthorizedAccessException => (
                new ApiResponse<object>
                {
                    Succeeded = false,
                    ResponseCode = (int)HttpStatusCode.Unauthorized,
                    Message = "Không có quyền truy cập"
                },
                HttpStatusCode.Unauthorized
            ),
            _ => (
                new ApiResponse<object>
                {
                    Succeeded = false,
                    ResponseCode = (int)HttpStatusCode.InternalServerError,
                    Message = "Đã xảy ra lỗi. Vui lòng thử lại sau."
                },
                HttpStatusCode.InternalServerError
            )
        };

        httpContext.Response.StatusCode = (int)statusCode;
        httpContext.Response.ContentType = "application/json";

        await httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(response, _jsonOptions), 
            cancellationToken);

        return true;
    }
}
