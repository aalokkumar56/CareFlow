using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record ResetPasswordResult(string TemporaryPassword, bool Generated);
