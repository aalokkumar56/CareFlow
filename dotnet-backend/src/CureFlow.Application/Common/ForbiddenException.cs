namespace CureFlow.Application.Common;

public class ForbiddenException(string message = "Forbidden") : DomainException(message, 403);
