using CureFlow.Application.DTOs;

namespace CureFlow.Application.Interfaces;

public interface IWhatsappApiService
{
    /// <summary>
    /// Sends a text message via WhatsApp
    /// </summary>
    Task<SendMessageResponse> SendMessageAsync(SendMessageRequest request, CancellationToken ct = default);

    /// <summary>
    /// Sends a template message via WhatsApp
    /// </summary>
    Task<SendMessageResponse> SendTemplateMessageAsync(SendTemplateMessageRequest request, CancellationToken ct = default);

    /// <summary>
    /// Sends media (image, video, audio, document) via WhatsApp
    /// </summary>
    Task<SendMessageResponse> SendMediaAsync(SendMediaRequest request, CancellationToken ct = default);

    /// <summary>
    /// Sends media via file upload to WhatsApp Cloud API
    /// </summary>
    Task<SendMessageResponse> SendMediaFileAsync(string phone, string mediaType, byte[] fileBytes, string fileName, string contentType, string? caption, CancellationToken ct = default);

    /// <summary>
    /// Triggers a WhatsApp campaign
    /// </summary>
    Task<CampaignTriggerResponse> SendCampaignAsync(SendCampaignRequest request, CancellationToken ct = default);

    /// <summary>
    /// Creates or updates a WhatsApp contact
    /// </summary>
    Task<WhatsAppApiResponse> MakeContactAsync(MakeContactRequest request, CancellationToken ct = default);

    /// <summary>
    /// Gets all approved templates from cache or WhatsApp API
    /// </summary>
    Task<GetTemplatesResponse> GetTemplatesAsync(bool forceRefresh = false, CancellationToken ct = default);

    /// <summary>
    /// Gets all contact groups from cache or WhatsApp API
    /// </summary>
    Task<GetGroupsResponse> GetGroupsAsync(bool forceRefresh = false, CancellationToken ct = default);

    /// <summary>
    /// Gets all campaigns from cache or WhatsApp API
    /// </summary>
    Task<GetCampaignsResponse> GetCampaignsAsync(string type = "api", bool forceRefresh = false, CancellationToken ct = default);

    /// <summary>
    /// Gets all contacts from cache or WhatsApp API
    /// </summary>
    Task<GetContactsResponse> GetContactsAsync(bool forceRefresh = false, CancellationToken ct = default);
}
