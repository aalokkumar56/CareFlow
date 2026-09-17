using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.External;
using ExcelDataReader;
using Npgsql;

namespace CureFlow.Infrastructure.Services;

public class LeadService(ICureFlowDbSession db, IAppointmentService appointments) : ILeadService
{
    public sealed class LeadRow : Lead
    {
        public int SentCount { get; set; }
        public int FailedCount { get; set; }
        public int ScheduledCount { get; set; }
        public DateTime? LastSentAt { get; set; }
    }

    public async Task<object> ListAsync(string? q, string? status, int page, int pageSize, CancellationToken ct)
    {
        var (number, size, skip) = Pagination.Normalize(page, pageSize);
        var filter = status switch
        {
            "converted" => "AND l.\"ConvertedAt\" IS NOT NULL",
            "all" => "",
            _ => "AND l.\"ConvertedAt\" IS NULL AND l.\"IsActive\" = true"
        };
        var args = new { q = $"%{q?.Trim()}%", skip, take = size };
        var where = $"""l."TenantId" = @TenantId AND NOT l."IsDeleted" {filter} AND (l."Phone" ILIKE @q OR COALESCE(l."Name", '') ILIKE @q)""";
        var total = await db.QuerySingleAsync<int>($"""SELECT COUNT(*)::int FROM "Leads" l WHERE {where}""", args, ct: ct);
        var items = await db.QueryAsync<LeadRow>($"""
            SELECT l.*,
                (SELECT COUNT(*)::int FROM "CampaignRecipients" r WHERE r."LeadId" = l."Id" AND r."TenantId" = @TenantId AND NOT r."IsDeleted" AND r."Status" IN (1, 2, 3, 5)) AS "SentCount",
                (SELECT COUNT(*)::int FROM "CampaignRecipients" r WHERE r."LeadId" = l."Id" AND r."TenantId" = @TenantId AND NOT r."IsDeleted" AND r."Status" = @failed) AS "FailedCount",
                (SELECT MAX(r."SentAt") FROM "CampaignRecipients" r WHERE r."LeadId" = l."Id" AND r."TenantId" = @TenantId AND NOT r."IsDeleted" AND r."Status" IN (1, 2, 3, 5)) AS "LastSentAt",
                (SELECT COUNT(*)::int FROM "Campaigns" c WHERE c."TenantId" = @TenantId AND NOT c."IsDeleted" AND c."Status" = @scheduled
                    AND l."ConvertedAt" IS NULL AND l."IsActive"
                    AND c."AudienceJson"::jsonb ->> 'source' = 'leads'
                    AND (CASE WHEN jsonb_array_length(COALESCE(c."AudienceJson"::jsonb -> 'lead_ids', '[]'::jsonb)) > 0
                        THEN c."AudienceJson"::jsonb -> 'lead_ids' ? l."Id"::text
                        ELSE jsonb_array_length(COALESCE(c."AudienceJson"::jsonb -> 'genders', '[]'::jsonb)) = 0
                            OR c."AudienceJson"::jsonb -> 'genders' ? CASE l."Gender" WHEN 0 THEN 'male' WHEN 1 THEN 'female' WHEN 2 THEN 'other' ELSE 'unknown' END END)) AS "ScheduledCount"
            FROM "Leads" l WHERE {where} ORDER BY l."CreatedAt" DESC, l."Id" OFFSET @skip LIMIT @take
            """, new { args.q, args.skip, args.take, sent = (int)RecipientStatus.Sent, failed = (int)RecipientStatus.Failed, scheduled = (int)CampaignStatus.Scheduled }, ct: ct);
        var summary = await db.QuerySingleAsync<object>("""
            SELECT COUNT(*)::int AS total,
                COUNT(*) FILTER (WHERE "ConvertedAt" IS NULL AND "IsActive")::int AS active,
                COUNT(*) FILTER (WHERE "ConvertedAt" IS NOT NULL)::int AS converted
            FROM "Leads" WHERE "TenantId" = @TenantId AND NOT "IsDeleted"
            """, ct: ct);
        return new { items, total, page = number, pageSize = size, summary };
    }

    public async Task<object> GetAsync(Guid id, CancellationToken ct)
    {
        var lead = await FindAsync(id, ct);
        var history = await db.QueryAsync<object>("""
            SELECT r."Id", r."CampaignId", c."Name" AS "CampaignName", r."Status", r."SentAt", r."ErrorMessage"
            FROM "CampaignRecipients" r JOIN "Campaigns" c ON c."Id" = r."CampaignId" AND c."TenantId" = r."TenantId"
            WHERE r."LeadId" = @id AND r."TenantId" = @TenantId AND NOT r."IsDeleted"
            ORDER BY r."SentAt" DESC LIMIT 100
            """, new { id }, ct: ct);
        return new { lead, history };
    }

    public async Task<Guid> CreateAsync(SaveLeadRequest request, CancellationToken ct)
    {
        var lead = new Lead();
        Apply(lead, request);
        try { await db.InsertAsync(lead, ct: ct); }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        { throw new ValidationException("This phone number already exists in leads, including converted leads."); }
        return lead.Id;
    }

    public async Task UpdateAsync(Guid id, SaveLeadRequest request, CancellationToken ct)
    {
        await db.TransactionAsync(async session =>
        {
            var lead = await session.QueryFirstOrDefaultAsync<Lead>("""SELECT * FROM "Leads" WHERE "Id" = @id AND "TenantId" = @TenantId AND NOT "IsDeleted" FOR UPDATE""", new { id }, ct: ct)
                ?? throw new NotFoundException("Lead");
            if (lead.ConvertedAt.HasValue) throw new ValidationException("Converted leads are read-only. Edit the patient instead.");
            Apply(lead, request);
            try { await session.UpdateAsync(lead, ct: ct); }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            { throw new ValidationException("This phone number already exists in leads."); }
        }, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var changed = await db.ExecuteAsync("""
            UPDATE "Leads" SET "IsDeleted" = true, "IsActive" = false, "UpdatedAt" = @now, "UpdatedBy" = @userId
            WHERE "Id" = @id AND "TenantId" = @TenantId AND NOT "IsDeleted" AND "ConvertedAt" IS NULL
            """, new { id, now = DateTime.UtcNow, userId = db.UserId }, ct: ct);
        if (changed == 0) throw new ValidationException("Lead not found or already converted. Conversion history is retained.");
    }

    public async Task<LeadImportResult> ImportAsync(Stream stream, string extension, CancellationToken ct)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var reader = extension == ".csv" ? ExcelReaderFactory.CreateCsvReader(stream) : ExcelReaderFactory.CreateReader(stream);
        if (!reader.Read()) throw new ValidationException("The sheet is empty.");
        string Cell(int column) => column < 0 ? "" : Convert.ToString(reader.GetValue(column), CultureInfo.InvariantCulture)?.Trim() ?? "";
        var columns = new Dictionary<string, int>();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var key = Regex.Replace(Cell(i).ToLowerInvariant(), "[^a-z]", "");
            key = key switch { "phonenumber" or "mobile" or "mobilenumber" or "contactnumber" => "phone", "fullname" => "name", _ => key };
            if (!columns.TryAdd(key, i) && key is "phone" or "name" or "age" or "gender")
                throw new ValidationException($"Duplicate {key} column.");
        }
        if (!columns.TryGetValue("phone", out var phoneColumn)) throw new ValidationException("The first row must contain a Phone Number column. Name, Age and Gender are optional.");
        int Column(string key) => columns.GetValueOrDefault(key, -1);
        var rows = new List<(int Row, Lead Lead)>();
        var errors = new List<string>();
        var seen = new HashSet<string>();
        var row = 1;
        while (reader.Read())
        {
            row++;
            if (row > 10001) throw new ValidationException("Import up to 10,000 rows at a time.");
            var values = new[] { Cell(phoneColumn), Cell(Column("name")), Cell(Column("age")), Cell(Column("gender")) };
            if (values.All(string.IsNullOrWhiteSpace)) continue;
            try
            {
                int? age = null;
                if (values[2].Length > 0)
                {
                    if (!int.TryParse(values[2], out var parsed)) throw new ValidationException("Age must be a whole number.");
                    age = parsed;
                }
                var gender = Gender.Unknown;
                if (values[3].Length > 0 && (!Enum.TryParse(values[3], true, out gender) || !Enum.IsDefined(gender)))
                    throw new ValidationException("Gender must be Male, Female, Other or Unknown.");
                var lead = new Lead();
                Apply(lead, new SaveLeadRequest(values[0], values[1], age, gender));
                if (!seen.Add(lead.Phone)) throw new ValidationException("Duplicate phone number in this file.");
                rows.Add((row, lead));
            }
            catch (ValidationException ex) { errors.Add($"Row {row}: {ex.Message}"); }
        }
        IReadOnlyList<string> inserted = Array.Empty<string>();
        if (rows.Count > 0)
        {
            inserted = await db.QueryAsync<string>("""
                INSERT INTO "Leads" ("Id", "TenantId", "Phone", "Name", "Age", "Gender", "CreatedAt", "UpdatedAt", "CreatedBy", "UpdatedBy", "IsActive", "IsDeleted")
                SELECT input.id, @TenantId, input.phone, input.name, input.age, input.gender, @now, @now, @userId, @userId, true, false
                FROM unnest(@ids::uuid[], @phones::text[], @names::text[], @ages::int[], @genders::int[]) AS input(id, phone, name, age, gender)
                ON CONFLICT ("TenantId", "Phone") WHERE "IsDeleted" = false DO NOTHING
                RETURNING "Phone"
                """, new {
                    ids = rows.Select(r => r.Lead.Id).ToArray(), phones = rows.Select(r => r.Lead.Phone).ToArray(),
                    names = rows.Select(r => r.Lead.Name).ToArray(), ages = rows.Select(r => r.Lead.Age).ToArray(),
                    genders = rows.Select(r => (int)r.Lead.Gender).ToArray(), now = DateTime.UtcNow, userId = db.UserId
                }, ct: ct);
        }
        var insertedPhones = inserted.ToHashSet();
        foreach (var (rowNumber, lead) in rows.Where(r => !insertedPhones.Contains(r.Lead.Phone)))
            errors.Add($"Row {rowNumber}: Phone number already exists in leads (possibly converted).");
        return new LeadImportResult(inserted.Count, errors.Count, errors);
    }
    public Task<object> BookAppointmentAsync(Guid id, BookLeadAppointmentRequest request, CancellationToken ct) =>
        appointments.CreateFromLeadAsync(id, request, ct);

    private async Task<Lead> FindAsync(Guid id, CancellationToken ct) =>
        await db.GetByIdAsync<Lead>(id, ct: ct) ?? throw new NotFoundException("Lead");

    private static void Apply(Lead lead, SaveLeadRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Phone) || !Regex.IsMatch(request.Phone, @"^[+\d\s().-]+$"))
            throw new ValidationException("A valid phone number is required.");
        var phone = WhatsappPhoneHelper.Normalize(request.Phone);
        if (phone.Length is < 10 or > 15) throw new ValidationException("Phone number must have 10 to 15 digits including country code.");
        if (request.Age is < 0 or > 130) throw new ValidationException("Age must be between 0 and 130.");
        if (!Enum.IsDefined(request.Gender)) throw new ValidationException("Invalid gender.");
        if (request.Name?.Trim().Length > 200) throw new ValidationException("Name must be 200 characters or fewer.");
        lead.Phone = phone;
        lead.Name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim();
        lead.Age = request.Age;
        lead.Gender = request.Gender;
    }
}
