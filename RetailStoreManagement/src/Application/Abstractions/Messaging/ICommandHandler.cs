using Application.Common.Models;
using MediatR;

namespace Application.Abstractions.Messaging;

/// <summary>
/// Interface cho Command Handler - xử lý logic của Command
/// </summary>
/// <typeparam name="TCommand">Loại Command</typeparam>
/// <typeparam name="TResponse">Kiểu dữ liệu trả về</typeparam>
public interface ICommandHandler<in TCommand, TResponse> 
    : IRequestHandler<TCommand, ApiResponse<TResponse>> 
    where TCommand : ICommand<TResponse>
{
}

/// <summary>
/// Interface cho Command Handler không trả về dữ liệu cụ thể
/// </summary>
/// <typeparam name="TCommand">Loại Command</typeparam>
public interface ICommandHandler<in TCommand> 
    : IRequestHandler<TCommand, ApiResponse<bool>> 
    where TCommand : ICommand
{
}
