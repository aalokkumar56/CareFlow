namespace CureFlow.Application.Common;

public static class MessageTemplateHelper
{
    public static string Render(string template, IReadOnlyDictionary<string, string?> values)
    {
        if (string.IsNullOrEmpty(template)) return template;
        var result = template;
        foreach (var (key, value) in values)
        {
            result = result.Replace($"{{{key}}}", value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }
}
