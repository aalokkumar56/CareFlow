namespace CureFlow.Application.Common;

/// <summary>Domain exception that maps to a 4xx response.</summary>
public class DomainException : Exception
{
    public int StatusCode { get; }
    public DomainException(string message, int statusCode = 400) : base(message) => StatusCode = statusCode;
}
