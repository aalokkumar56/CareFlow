namespace CureFlow.Application.Common;

public class ValidationException(string message) : DomainException(message, 422);
