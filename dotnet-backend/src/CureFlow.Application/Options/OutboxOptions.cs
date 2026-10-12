namespace CureFlow.Application.Options;

public sealed class OutboxOptions
{
    public int OperatorRetryLimit { get; set; } = 3;
    public bool Enabled { get; set; }
    public int BatchSize { get; set; } = 16;
    public int LeaseSeconds { get; set; } = 120;
    public int HandlerTimeoutSeconds { get; set; } = 90;
    public int MaximumAttempts { get; set; } = 8;
    public int PollSeconds { get; set; } = 5;
    public void Validate()
    {
        if (OperatorRetryLimit is < 0 or > 3 || BatchSize is < 1 or > 100 || LeaseSeconds is < 30 or > 600 || HandlerTimeoutSeconds < 1
            || HandlerTimeoutSeconds > LeaseSeconds - 5 || MaximumAttempts is < 1 or > 20 || PollSeconds is < 1 or > 60)
            throw new InvalidOperationException("Outbox batch/lease/timeout/retry settings are outside their bounded contract.");
    }
}

