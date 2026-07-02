using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;

namespace CureFlow.Infrastructure.Services;

public class StaffService : IStaffService
{
    private readonly ICureFlowDbSession _db;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;

    private static readonly UserRole[] StaffRoles =
        [UserRole.Doctor, UserRole.Nurse, UserRole.Staff, UserRole.Reception];

    private static readonly int[] StaffRoleInts =
        StaffRoles.Select(r => (int)r).ToArray();

    public StaffService(ICureFlowDbSession db, ITenantContext tenant, IAuditService audit)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<Guid> CreateAsync(CreateStaffProfileRequest req, CancellationToken ct = default)
    {
        var user = await _db.GetByIdAsync<User>(req.UserId, ct: ct)
            ?? throw new NotFoundException("User");

        if (!StaffRoles.Contains(user.Role))
            throw new ValidationException("User role must be Doctor, Nurse, Staff, or Reception");

        var existing = await _db.QueryFirstOrDefaultAsync<StaffProfile>(
            """SELECT * FROM "StaffProfiles" WHERE "UserId" = @userId LIMIT 1""",
            new { userId = req.UserId }, ct: ct);
        if (existing != null)
            throw new ValidationException("Staff profile already exists for this user");

        var profile = new StaffProfile
        {
            UserId = req.UserId,
            Department = req.Department ?? user.Specialty,
            Specialization = req.Specialization ?? user.Specialty,
            Qualification = req.Qualification ?? user.Qualifications,
            ConsultationFee = req.ConsultationFee,
            EmploymentType = req.EmploymentType ?? (user.Role == UserRole.Doctor
                ? StaffEmploymentType.Permanent
                : StaffEmploymentType.Permanent),
            Shift = req.Shift,
            WardAssignment = req.WardAssignment,
            IsAvailable = req.IsAvailable,
        };
        await _db.InsertAsync(profile, ct: ct);
        await _audit.LogAsync("staff.create", "staff_profile", profile.Id.ToString(), new { req.UserId }, ct);
        return profile.Id;
    }

    public async Task<StaffProfileDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var row = await QueryStaffAsync(null, null, id, ct);
        return row.FirstOrDefault() is { } match
            ? match
            : throw new NotFoundException("Staff profile");
    }

    public async Task<IReadOnlyList<StaffProfileDto>> ListAsync(string? q, UserRole? role, CancellationToken ct = default)
        => await QueryStaffAsync(q, role, null, ct);

    public Task<IReadOnlyList<StaffProfileDto>> ListDoctorsAsync(string? q, CancellationToken ct = default)
        => ListAsync(q, UserRole.Doctor, ct);

    public Task<IReadOnlyList<StaffProfileDto>> ListNursesAsync(string? q, CancellationToken ct = default)
        => ListAsync(q, UserRole.Nurse, ct);

    public async Task<AppointmentBookingOptionsDto> GetBookingOptionsAsync(CancellationToken ct = default)
    {
        var doctors = await QueryStaffAsync(null, UserRole.Doctor, null, ct, availableOnly: true);
        var bookable = doctors.Select(d => new BookableDoctorDto(
            d.UserId, d.Id, d.Name, d.Department, d.Specialization,
            d.ConsultationFee, d.EmploymentType)).ToList();
        var departments = await DepartmentCatalog.LoadAsync(_db, ct);
        return new AppointmentBookingOptionsDto(bookable, departments);
    }

    public async Task UpdateAsync(Guid id, UpdateStaffProfileRequest req, CancellationToken ct = default)
    {
        var profile = await _db.GetByIdAsync<StaffProfile>(id, ct: ct)
            ?? throw new NotFoundException("Staff profile");

        if (req.Department != null) profile.Department = req.Department;
        if (req.Specialization != null) profile.Specialization = req.Specialization;
        if (req.Qualification != null) profile.Qualification = req.Qualification;
        if (req.ConsultationFee.HasValue) profile.ConsultationFee = req.ConsultationFee;
        if (req.EmploymentType.HasValue) profile.EmploymentType = req.EmploymentType.Value;
        if (req.Shift != null) profile.Shift = req.Shift;
        if (req.WardAssignment != null) profile.WardAssignment = req.WardAssignment;
        if (req.IsAvailable.HasValue) profile.IsAvailable = req.IsAvailable.Value;

        await _db.UpdateAsync(profile, ct: ct);
        await _audit.LogAsync("staff.update", "staff_profile", id.ToString(), null, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var profile = await _db.GetByIdAsync<StaffProfile>(id, ct: ct)
            ?? throw new NotFoundException("Staff profile");
        profile.IsDeleted = true;
        await _db.UpdateAsync(profile, ct: ct);
        await _audit.LogAsync("staff.delete", "staff_profile", id.ToString(), null, ct);
    }

    public async Task<Guid> AddScheduleAsync(CreateDoctorScheduleRequest req, CancellationToken ct = default)
    {
        var profile = await _db.GetByIdAsync<StaffProfile>(req.StaffProfileId, ct: ct)
            ?? throw new NotFoundException("Staff profile");

        if (!TimeSpan.TryParse(req.StartTime, out var startTime))
            throw new DomainException("Invalid start time format. Use HH:mm or HH:mm:ss.", 400);
        if (!TimeSpan.TryParse(req.EndTime, out var endTime))
            throw new DomainException("Invalid end time format. Use HH:mm or HH:mm:ss.", 400);

        var schedule = new DoctorSchedule
        {
            StaffProfileId = profile.Id,
            DayOfWeek = req.DayOfWeek,
            SpecificDate = DateTimeHelper.EnsureUtc(req.SpecificDate),
            StartTime = startTime,
            EndTime = endTime,
            IsAvailable = req.IsAvailable,
            Notes = req.Notes,
        };
        await _db.InsertAsync(schedule, ct: ct);
        return schedule.Id;
    }

    public async Task<IReadOnlyList<DoctorScheduleDto>> ListSchedulesAsync(Guid staffProfileId, CancellationToken ct = default)
    {
        var rows = await _db.QueryAsync<DoctorSchedule>(
            """
            SELECT * FROM "DoctorSchedules"
            WHERE "StaffProfileId" = @staffProfileId
            ORDER BY "DayOfWeek", "StartTime"
            """,
            new { staffProfileId }, ct: ct);
        return rows.Select(MapSchedule).ToList();
    }

    public async Task UpdateScheduleAsync(Guid scheduleId, UpdateDoctorScheduleRequest req, CancellationToken ct = default)
    {
        var schedule = await _db.GetByIdAsync<DoctorSchedule>(scheduleId, ct: ct)
            ?? throw new NotFoundException("Schedule");
        if (req.DayOfWeek.HasValue) schedule.DayOfWeek = req.DayOfWeek;
        if (req.SpecificDate.HasValue) schedule.SpecificDate = DateTimeHelper.EnsureUtc(req.SpecificDate);
        if (req.StartTime.HasValue) schedule.StartTime = req.StartTime.Value;
        if (req.EndTime.HasValue) schedule.EndTime = req.EndTime.Value;
        if (req.IsAvailable.HasValue) schedule.IsAvailable = req.IsAvailable.Value;
        if (req.Notes != null) schedule.Notes = req.Notes;
        await _db.UpdateAsync(schedule, ct: ct);
    }

    public async Task DeleteScheduleAsync(Guid scheduleId, CancellationToken ct = default)
    {
        var schedule = await _db.GetByIdAsync<DoctorSchedule>(scheduleId, ct: ct)
            ?? throw new NotFoundException("Schedule");
        schedule.IsDeleted = true;
        await _db.UpdateAsync(schedule, ct: ct);
    }

    private async Task<IReadOnlyList<StaffProfileDto>> QueryStaffAsync(
        string? q, UserRole? role, Guid? id, CancellationToken ct, bool availableOnly = false)
    {
        var filters = new List<string>
        {
            "sp.\"IsDeleted\" = false",
            "sp.\"TenantId\" = @tenantId",
            "u.\"IsDeleted\" = false",
            "u.\"Role\" = ANY(@staffRoles)",
        };
        var param = new Dictionary<string, object?>
        {
            ["tenantId"] = _db.TenantId,
            ["staffRoles"] = role.HasValue ? new[] { (int)role.Value } : StaffRoleInts,
        };

        if (id.HasValue)
        {
            filters.Add("sp.\"Id\" = @id");
            param["id"] = id.Value;
        }
        if (availableOnly)
            filters.Add("sp.\"IsAvailable\" = true");
        if (!string.IsNullOrWhiteSpace(q))
        {
            filters.Add("""
                (LOWER(u."Name") LIKE @term OR LOWER(u."Email") LIKE @term
                 OR (sp."Department" IS NOT NULL AND LOWER(sp."Department") LIKE @term))
                """);
            param["term"] = $"%{q.Trim().ToLower()}%";
        }

        var sql = $"""
            SELECT sp."Id", sp."UserId", sp."Department", sp."Specialization", sp."Qualification",
                   sp."ConsultationFee", sp."EmploymentType", sp."Shift", sp."WardAssignment",
                   sp."IsAvailable", sp."CreatedAt",
                   u."Name" AS "UserName", u."Email" AS "UserEmail", u."Role" AS "UserRole", u."Phone" AS "UserPhone"
            FROM "StaffProfiles" sp
            INNER JOIN "Users" u ON u."Id" = sp."UserId" AND u."TenantId" = sp."TenantId"
            WHERE {string.Join(" AND ", filters)}
            ORDER BY u."Name"
            """;

        var rows = await _db.QueryAsync<StaffRow>(sql, param, ignoreTenant: true, ct: ct);
        return rows.Select(Map).ToList();
    }

    private static StaffProfileDto Map(StaffRow s) => new(
        s.Id, s.UserId, s.UserName, s.UserEmail, s.UserRole,
        s.UserPhone, s.Department, s.Specialization, s.Qualification,
        s.ConsultationFee, s.EmploymentType, s.Shift, s.WardAssignment, s.IsAvailable, s.CreatedAt);

    private static DoctorScheduleDto MapSchedule(DoctorSchedule s) => new(
        s.Id, s.StaffProfileId, s.DayOfWeek, s.SpecificDate,
        s.StartTime, s.EndTime, s.IsAvailable, s.Notes);

    private sealed class StaffRow
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string? Department { get; set; }
        public string? Specialization { get; set; }
        public string? Qualification { get; set; }
        public decimal? ConsultationFee { get; set; }
        public StaffEmploymentType EmploymentType { get; set; }
        public string? Shift { get; set; }
        public string? WardAssignment { get; set; }
        public bool IsAvailable { get; set; }
        public DateTime CreatedAt { get; set; }
        public string UserName { get; set; } = "";
        public string UserEmail { get; set; } = "";
        public UserRole UserRole { get; set; }
        public string? UserPhone { get; set; }
    }
}
