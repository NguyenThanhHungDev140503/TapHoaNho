using Application.Common.Models;
using MediatR;

namespace Application.Abstractions.Messaging;

/// <summary>
/// Interface cho Query Handler - xử lý logic của Query
/// </summary>
/// <typeparam name="TQuery">Loại Query</typeparam>
/// <typeparam name="TResponse">Kiểu dữ liệu trả về</typeparam>
public interface IQueryHandler<in TQuery, TResponse> 
    : IRequestHandler<TQuery, ApiResponse<TResponse>> 
    where TQuery : IQuery<TResponse>
{
}
