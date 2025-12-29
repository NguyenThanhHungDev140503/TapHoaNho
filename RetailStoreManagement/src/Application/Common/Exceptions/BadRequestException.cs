namespace Application.Common.Exceptions;

/// <summary>
/// Exception khi request không hợp lệ
/// </summary>
public class BadRequestException : Exception
{
    public BadRequestException() : base() { }
    
    public BadRequestException(string message) : base(message) { }
}
