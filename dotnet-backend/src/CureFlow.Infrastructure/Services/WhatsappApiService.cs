using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.External;
using CureFlow.Infrastructure.Persistence.Dapper;
using Microsoft.Extensions.Logging;

namespace CureFlow.Infrastructure.Services;

/// <summary>
/// Application-level WhatsApp service. Delegates transport to <see cref="IWhatsappProvider"/>
/// and persists outbound messages to the CRM database.
/// </summary>
public class WhatsappApiService : IWhatsappApiService
{
    private readonly ICureFlowDbSession _db;
    private readonly IWhatsappProvider _provider;
    private readonly ITenantContext _tenant;
    private readonly ILogger<WhatsappApiService> _logger;

    public WhatsappApiService(
        ICureFlowDbSession db,
        IWhatsappProvider provider,
        ITenantContext tenant,
        ILogger<WhatsappApiService> logger)
    {
        _db = db;
        _provider = provider;
        _tenant = tenant;
        _logger = logger;
    }

    public async Task<SendMessageResponse> SendMessageAsync(SendMessageRequest request, CancellationToken ct = default)
    {
        var normalizedPhone = WhatsappPhoneHelper.Normalize(request.Phone);
        if (string.IsNullOrWhiteSpace(normalizedPhone) || string.IsNullOrWhiteSpace(request.Message))
            return new SendMessageResponse { Success = false, Phone = request.Phone };

        if (!await _provider.IsConfiguredAsync(ct))
            return await DemoSendAsync(request.Phone, "text", request.Message, null, null, null, ct);

        ProviderSendResult result;
        try
        {
            result = await _provider.SendTextAsync(request.Phone, request.Message, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WhatsApp send failed for {Phone}.", request.Phone);
            result = new ProviderSendResult(false, null, ex.Message);
        }

        await SaveOutgoingMessageAsync(request.Phone, "text", request.Message, null, null, null, result.MessageId, result.Success, ct);

        return new SendMessageResponse
        {
            Success = result.Success,
            Phone = request.Phone,
            MessageId = result.MessageId ?? string.Empty,
            SentAt = DateTime.UtcNow,
            Error = result.Error
        };
    }

    public async Task<SendMessageResponse> SendMediaAsync(SendMediaRequest request, CancellationToken ct = default)
    {
        var normalizedPhone = WhatsappPhoneHelper.Normalize(request.Phone);
        var mediaType = request.MediaType?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedPhone) || string.IsNullOrWhiteSpace(mediaType) || string.IsNullOrWhiteSpace(request.MediaUrl))
            return new SendMessageResponse { Success = false, Phone = request.Phone };

        if (!await _provider.IsConfiguredAsync(ct))
            return await DemoSendAsync(request.Phone, mediaType, request.Caption ?? string.Empty, request.MediaUrl, null, request.Caption, ct);

        var result = await _provider.SendMediaAsync(request, ct);
        await SaveOutgoingMessageAsync(request.Phone, mediaType, request.Caption ?? request.MediaUrl, request.MediaUrl, null, request.Caption, result.MessageId, result.Success, ct);

        return new SendMessageResponse
        {
            Success = result.Success,
            Phone = request.Phone,
            MessageId = result.MessageId ?? string.Empty,
            SentAt = DateTime.UtcNow
        };
    }

    public async Task<SendMessageResponse> SendMediaFileAsync(
        string phone,
        string mediaType,
        byte[] fileBytes,
        string fileName,
        string contentType,
        string? caption,
        CancellationToken ct = default)
    {
        var normalizedPhone = WhatsappPhoneHelper.Normalize(phone);
        if (string.IsNullOrWhiteSpace(normalizedPhone) || string.IsNullOrWhiteSpace(mediaType))
            return new SendMessageResponse { Success = false, Phone = phone };

        if (!await _provider.IsConfiguredAsync(ct))
            return await DemoSendAsync(phone, mediaType, caption ?? string.Empty, null, contentType, caption, ct);

        var result = await _provider.SendMediaFileAsync(phone, mediaType, fileBytes, fileName, contentType, caption, ct);
        await SaveOutgoingMessageAsync(phone, mediaType, caption ?? fileName, null, contentType, caption, result.MessageId, result.Success, ct);

        return new SendMessageResponse
        {
            Success = result.Success,
            Phone = phone,
            MessageId = result.MessageId ?? string.Empty,
            SentAt = DateTime.UtcNow
        };
    }

    public async Task<SendMessageResponse> SendTemplateMessageAsync(SendTemplateMessageRequest request, CancellationToken ct = default)
    {
        var normalizedPhone = WhatsappPhoneHelper.Normalize(request.Phone);
        if (string.IsNullOrWhiteSpace(normalizedPhone) || string.IsNullOrWhiteSpace(request.TemplateName))
            return new SendMessageResponse { Success = false, Phone = request.Phone };

        if (!await _provider.IsConfiguredAsync(ct))
            return await DemoSendAsync(request.Phone, "template", request.TemplateName, null, null, null, ct);

        var result = await _provider.SendTemplateMessageAsync(request, ct);
        await SaveOutgoingMessageAsync(request.Phone, "template", request.TemplateName, null, null, null, result.MessageId, result.Success, ct);

        return new SendMessageResponse
        {
            Success = result.Success,
            Phone = request.Phone,
            MessageId = result.MessageId ?? string.Empty,
            SentAt = DateTime.UtcNow
        };
    }

    public async Task<CampaignTriggerResponse> SendCampaignAsync(SendCampaignRequest request, CancellationToken ct = default)
    {
        if (!await _provider.IsConfiguredAsync(ct))
        {
            _logger.LogInformation("WhatsApp provider not configured; demo campaign trigger for {CampaignId}.", request.CampaignId);
            return new CampaignTriggerResponse
            {
                Success = true,
                CampaignId = request.CampaignId.ToString(),
                Status = "triggered",
                TriggeredAt = DateTime.UtcNow
            };
        }

        return await _provider.SendCampaignAsync(request, ct);
    }

    public async Task<WhatsAppApiResponse> MakeContactAsync(MakeContactRequest request, CancellationToken ct = default)
    {
        if (!await _provider.IsConfiguredAsync(ct))
        {
            return new WhatsAppApiResponse
            {
                Success = true,
                Message = "Demo contact created"
            };
        }

        return await _provider.MakeContactAsync(request, ct);
    }

    public Task<GetTemplatesResponse> GetTemplatesAsync(bool forceRefresh = false, CancellationToken ct = default)
        => _provider.GetTemplatesAsync(forceRefresh, ct);

    public Task<GetGroupsResponse> GetGroupsAsync(bool forceRefresh = false, CancellationToken ct = default)
        => _provider.GetGroupsAsync(forceRefresh, ct);

    public Task<GetCampaignsResponse> GetCampaignsAsync(string type = "api", bool forceRefresh = false, CancellationToken ct = default)
        => _provider.GetCampaignsAsync(type, forceRefresh, ct);

    public Task<GetContactsResponse> GetContactsAsync(bool forceRefresh = false, CancellationToken ct = default)
        => _provider.GetContactsAsync(forceRefresh, ct);

    private async Task<SendMessageResponse> DemoSendAsync(
        string phone,
        string messageType,
        string body,
        string? mediaUrl,
        string? mimeType,
        string? caption,
        CancellationToken ct)
    {
        _logger.LogInformation("WhatsApp provider {Provider} not configured; demo send to {Phone}.", _provider.ProviderName, phone);
        var demoMessageId = $"demo-{Guid.NewGuid()}";
        await SaveOutgoingMessageAsync(phone, messageType, body, mediaUrl, mimeType, caption, demoMessageId, true, ct);
        return new SendMessageResponse
        {
            Success = true,
            Phone = phone,
            MessageId = demoMessageId,
            SentAt = DateTime.UtcNow
        };
    }

    private async Task SaveOutgoingMessageAsync(
        string phone,
        string messageType,
        string body,
        string? mediaUrl,
        string? mimeType,
        string? caption,
        string? waMessageId,
        bool success,
        CancellationToken ct)
    {
        var normalizedPhone = WhatsappPhoneHelper.Normalize(phone);
        if (string.IsNullOrWhiteSpace(normalizedPhone))
        {
            _logger.LogWarning("Invalid normalized phone for outgoing WhatsApp message: {Phone}", phone);
            return;
        }

        await EnsureTenantScopeAsync(ct);

        var conversation = await WhatsappConversationHelper.FindByPhoneAsync(_db, normalizedPhone, ct);
        if (conversation == null)
        {
            conversation = new Conversation
            {
                WaPhone = normalizedPhone,
                Name = normalizedPhone,
                Category = ConversationCategory.General,
                Priority = Priority.Medium,
                LastMessageAt = DateTime.UtcNow,
                LastMessagePreview = body.Length > 120 ? body[..120] : body
            };
            await _db.InsertAsync(conversation, ct: ct);
        }

        var message = new Message
        {
            ConversationId = conversation.Id,
            PatientId = conversation.PatientId,
            WaMessageId = waMessageId,
            Direction = MessageDirection.Outbound,
            Type = messageType,
            Body = body,
            MediaUrl = mediaUrl,
            MimeType = mimeType,
            Caption = caption,
            Status = success ? MessageStatus.Sent : MessageStatus.Failed
        };

        await _db.InsertAsync(message, ct: ct);
        conversation.LastMessageAt = DateTime.UtcNow;
        conversation.LastMessagePreview = body.Length > 120 ? body[..120] : body;
        await _db.UpdateAsync(conversation, ct: ct);
    }

    private async Task EnsureTenantScopeAsync(CancellationToken ct)
    {
        if (_tenant is not CurrentTenant current || current.TenantId != Guid.Empty)
            return;

        var tenantId = await _db.QueryFirstOrDefaultAsync<Guid?>(
            """
            SELECT "Id" FROM "Tenants"
            WHERE "IsActive" = true AND "IsDeleted" = false
            ORDER BY "CreatedAt" ASC
            LIMIT 1
            """,
            ignoreTenant: true,
            ct: ct);

        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            return;

        current.TenantId = tenantId.Value;
        current.IsAuthenticated = true;
        current.UserEmail ??= "whatsapp@api";
    }
}
