using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record LabReportUploadResultDto(Guid Id, string FileUrl, string TestName);
