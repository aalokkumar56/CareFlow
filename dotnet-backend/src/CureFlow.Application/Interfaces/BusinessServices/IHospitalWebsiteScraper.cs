using CureFlow.Application.Common;
using CureFlow.Application.DTOs;

namespace CureFlow.Application.Interfaces;

public interface IHospitalWebsiteScraper
{
    Task<string> FetchPagesAsync(string baseUrl, CancellationToken ct = default);
}
