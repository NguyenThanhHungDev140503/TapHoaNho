using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.Models;

/// <summary>
/// Response với pagination
/// </summary>
/// <typeparam name="T">Kiểu dữ liệu của items</typeparam>
public class PaginatedResponse<T>
{
    public List<T> Items { get; set; } = [];
    
    [JsonPropertyName("Page")] 
    public int Page { get; set; }
    
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    
    [JsonPropertyName("HasPrevious")] 
    public bool HasPreviousPage => Page > 1;
    
    [JsonPropertyName("HasNext")] 
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Tạo PaginatedResponse từ IQueryable
    /// </summary>
    public static async Task<PaginatedResponse<T>> CreateAsync(
        IQueryable<T> source, 
        int page, 
        int pageSize)
    {
        var count = await source.CountAsync();
        var items = await source
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedResponse<T>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = count
        };
    }
    
    /// <summary>
    /// Tạo PaginatedResponse từ List (đã có sẵn)
    /// </summary>
    public static PaginatedResponse<T> Create(
        List<T> items,
        int page,
        int pageSize,
        int totalCount)
    {
        return new PaginatedResponse<T>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
