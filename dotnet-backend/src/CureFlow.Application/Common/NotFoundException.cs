namespace CureFlow.Application.Common;

public class NotFoundException(string entity) : DomainException($"{entity} not found", 404);
