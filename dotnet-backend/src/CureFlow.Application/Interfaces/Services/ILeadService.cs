using CureFlow.Application.DTOs;
namespace CureFlow.Application.Interfaces;

public interface ILeadService
{
    Task<object> ListAsync(string? q, string? status, int page, int pageSize, CancellationToken ct);
    Task<object> GetAsync(Guid id, CancellationToken ct);
    Task<Guid> CreateAsync(SaveLeadRequest request, CancellationToken ct);
    Task UpdateAsync(Guid id, SaveLeadRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<LeadImportResult> ImportAsync(Stream stream, string extension, CancellationToken ct);
    Task<object> BookAppointmentAsync(Guid id, BookLeadAppointmentRequest request, CancellationToken ct);
}
