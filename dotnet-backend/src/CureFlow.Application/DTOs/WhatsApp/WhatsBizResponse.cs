using System.Text.Json.Serialization;

namespace CureFlow.Application.DTOs;

public class WhatsBizResponse
{
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("message_id")]
    public int Message_Id { get; set; }

    [JsonPropertyName("message_wamid")]
    public string Message_Wamid { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
