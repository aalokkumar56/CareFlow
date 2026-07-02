using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Persistence.Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/templates")]
public class TemplatesController : ControllerBase
{
    private readonly ICureFlowDbSession _db;
    public TemplatesController(ICureFlowDbSession db) => _db = db;

    public record TemplateRequest(string Name, string Body, string? Category);

    [HttpGet]
    [Authorize(Policy = "Permission:Settings.View")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var where = SqlFragments.WhereActive<QuickTemplate>(ignoreTenant: false);
        var rows = await _db.QueryAsync<QuickTemplate>(
            $"""
            SELECT * FROM "Templates"
            WHERE {where}
            ORDER BY "CreatedAt" DESC
            """,
            ct: ct);
        return Ok(rows);
    }

    [HttpGet("placeholders")]
    [Authorize(Policy = "Permission:Settings.View")]
    public async Task<IActionResult> ListPlaceholders(CancellationToken ct)
    {
        var where = SqlFragments.WhereActive<TemplatePlaceholder>(ignoreTenant: false);
        var rows = await _db.QueryAsync<TemplatePlaceholder>(
            $"""
            SELECT * FROM "TemplatePlaceholders"
            WHERE {where}
            ORDER BY "IsSystem" DESC, "Label"
            """,
            ct: ct);
        return Ok(rows.Select(MapPlaceholder));
    }

    [HttpPost("placeholders")]
    [Authorize(Policy = "Permission:Settings.Edit")]
    public async Task<IActionResult> CreatePlaceholder([FromBody] CreateTemplatePlaceholderRequest req, CancellationToken ct)
    {
        var key = NormalizeKey(req.Key);
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest(new { detail = "Placeholder key is required (letters, numbers, underscore)." });

        var where = SqlFragments.WhereActive<TemplatePlaceholder>(ignoreTenant: false);
        if (await _db.QueryFirstOrDefaultAsync<int?>(
                $"""SELECT 1 FROM "TemplatePlaceholders" WHERE {where} AND "Key" = @Key LIMIT 1""",
                new { Key = key },
                ct: ct) != null)
            return Conflict(new { detail = $"Placeholder '{key}' already exists." });

        var entity = new TemplatePlaceholder
        {
            Key = key,
            Label = req.Label.Trim(),
            Description = req.Description?.Trim(),
            Example = req.Example?.Trim(),
            StaticValue = req.StaticValue?.Trim(),
            IsSystem = false,
        };
        await _db.InsertAsync(entity, ct: ct);
        return Ok(MapPlaceholder(entity));
    }

    [HttpDelete("placeholders/{id:guid}")]
    [Authorize(Policy = "Permission:Settings.Edit")]
    public async Task<IActionResult> DeletePlaceholder(Guid id, CancellationToken ct)
    {
        var entity = await _db.GetByIdAsync<TemplatePlaceholder>(id, ct: ct);
        if (entity == null) return NotFound();
        if (entity.IsSystem)
            return BadRequest(new { detail = "System placeholders cannot be deleted." });

        await _db.ExecuteAsync(
            """DELETE FROM "TemplatePlaceholders" WHERE "Id" = @Id AND "TenantId" = @TenantId""",
            new { Id = id },
            ct: ct);
        return Ok(new { ok = true });
    }

    [HttpPost]
    [Authorize(Policy = "Permission:Settings.Edit")]
    public async Task<IActionResult> Create([FromBody] TemplateRequest req, CancellationToken ct)
    {
        var entity = new QuickTemplate
        {
            Name = req.Name,
            Body = req.Body,
            Category = string.IsNullOrWhiteSpace(req.Category) ? "general" : req.Category,
        };
        await _db.InsertAsync(entity, ct: ct);
        return Ok(entity);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Permission:Settings.Edit")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var entity = await _db.GetByIdAsync<QuickTemplate>(id, ct: ct);
        if (entity == null) return NotFound();

        await _db.ExecuteAsync(
            """DELETE FROM "Templates" WHERE "Id" = @Id AND "TenantId" = @TenantId""",
            new { Id = id },
            ct: ct);
        return Ok(new { ok = true });
    }

    private static TemplatePlaceholderDto MapPlaceholder(TemplatePlaceholder p) =>
        new(p.Id, p.Key, p.Label, p.Description, p.Example, p.IsSystem, p.StaticValue);

    private static string NormalizeKey(string raw)
    {
        var key = new string(raw.Trim().ToLowerInvariant()
            .Where(ch => char.IsLetterOrDigit(ch) || ch == '_')
            .ToArray());
        return key;
    }
}
