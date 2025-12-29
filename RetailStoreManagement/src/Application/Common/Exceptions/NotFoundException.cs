namespace Application.Common.Exceptions;

/// <summary>
/// Exception khi không tìm thấy resource
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException() : base() { }
    
    public NotFoundException(string message) : base(message) { }
    
    public NotFoundException(string name, object key)
        : base($"Không tìm thấy \"{name}\" với key ({key}).") { }
}
