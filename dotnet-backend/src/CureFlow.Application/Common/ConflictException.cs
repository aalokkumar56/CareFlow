namespace CureFlow.Application.Common;

public class ConflictException(string message) : DomainException(message, 409);
