namespace CureFlow.Application.Common;

public static class Pagination
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 500;

    public static (int page, int pageSize, int skip) Normalize(int page, int pageSize, int maxPageSize = MaxPageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? DefaultPageSize : pageSize, 1, maxPageSize);
        return (page, pageSize, (page - 1) * pageSize);
    }
}
