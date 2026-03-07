using Application.Abstractions.Messaging;

namespace Application.Common.Models;

/// <summary>
/// Base class cho các Query có pagination
/// </summary>
/// <typeparam name="TResponse">Kiểu dữ liệu trả về</typeparam>
public class PaginationRequest<TResponse> : IQuery<TResponse>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool? SortDesc { get; set; }

    /// <summary>
    /// Helper to get effective sort order
    /// </summary>
    public string GetSortOrder() => SortDesc.HasValue && SortDesc.Value ? "desc" : "asc";
}
