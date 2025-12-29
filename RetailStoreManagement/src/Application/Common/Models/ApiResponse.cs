using System.Text.Json.Serialization;

namespace Application.Common.Models;

/// <summary>
/// Chuẩn hóa response của API
/// </summary>
/// <typeparam name="T">Kiểu dữ liệu của Data</typeparam>
public class ApiResponse<T>
{
    // Legacy support
    public bool IsError { get; set; }
    public int StatusCode { get; set; } = 200;

    // Original properties (kept for consistency with internally created logic)
    [JsonIgnore]
    public bool Succeeded { 
        get => !IsError; 
        set => IsError = !value; 
    }
    
    [JsonIgnore]
    public int ResponseCode {
        get => StatusCode;
        set => StatusCode = value;
    }

    public string? Message { get; set; }
    public T? Data { get; set; }
    public IDictionary<string, string[]>? Errors { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public ApiResponse() { }

    public ApiResponse(T? data, bool succeeded = true, int code = 200, string? message = null)
    {
        Data = data;
        Succeeded = succeeded; // Sets IsError
        ResponseCode = code;   // Sets StatusCode
        Message = message;
    }

    /// <summary>
    /// Tạo response thành công
    /// </summary>
    public static ApiResponse<T> Success(T? data = default, string? message = null) 
        => new(data, true, 200, message);

    /// <summary>
    /// Tạo response thất bại
    /// </summary>
    public static ApiResponse<T> Failure(string? message = null, int code = 400) 
        => new(default, false, code, message);

    /// <summary>
    /// Tạo response không tìm thấy
    /// </summary>
    public static ApiResponse<T> NotFound(string? message = "Không tìm thấy dữ liệu") 
        => new(default, false, 404, message);
        
    /// <summary>
    /// Tạo response lỗi validation
    /// </summary>
    public static ApiResponse<T> ValidationError(IDictionary<string, string[]> errors, string? message = "Dữ liệu không hợp lệ") 
        => new(default, false, 400, message) { Errors = errors };
}
