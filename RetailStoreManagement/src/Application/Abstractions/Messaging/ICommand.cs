using Application.Common.Models;
using MediatR;

namespace Application.Abstractions.Messaging;

/// <summary>
/// Interface cho Command - thao tác ghi dữ liệu (Create, Update, Delete)
/// </summary>
/// <typeparam name="TResponse">Kiểu dữ liệu trả về</typeparam>
public interface ICommand<TResponse> : IRequest<ApiResponse<TResponse>>
{
}

/// <summary>
/// Interface cho Command không trả về dữ liệu cụ thể
/// </summary>
public interface ICommand : IRequest<ApiResponse<bool>>
{
}
