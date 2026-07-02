using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record ResetPasswordRequest(string? NewPassword, bool GenerateTemporary = false);
