namespace CureFlow.Application.Options;

public class MarketingCalendarOptions
{
    public const string SectionName = "MarketingCalendar";

    /// <summary>When true and ApiKey is set, a stub sync may run (no external call without key).</summary>
    public bool GoogleCalendarSyncEnabled { get; set; }

    public string? GoogleCalendarApiKey { get; set; }
}
