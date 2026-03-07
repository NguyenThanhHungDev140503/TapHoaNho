using Application.Common.Models;
using MediatR;

namespace Application.Abstractions.Messaging;

/// <summary>
/// Interface cho Query - thao tác đọc dữ liệu
/// </summary>
/// <typeparam name="TResponse">Kiểu dữ liệu trả về</typeparam>
public interface IQuery<TResponse> : IRequest<ApiResponse<TResponse>>
{
}
