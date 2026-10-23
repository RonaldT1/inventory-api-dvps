namespace ApiInventario.Exceptions;

public enum BusinessError
{
    Validation,
    Conflict,
    NotFound,
    InvalidCredentials,
    Forbidden
}

// Only messages deliberately intended for API clients belong in this exception.
public sealed class BusinessRuleException(string message, BusinessError error = BusinessError.Validation)
    : InvalidOperationException(message)
{
    public BusinessError Error { get; } = error;
}
