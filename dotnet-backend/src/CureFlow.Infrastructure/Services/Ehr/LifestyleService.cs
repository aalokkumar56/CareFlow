using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Enums;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Lifestyle;
using CureFlow.Infrastructure.Persistence.Dapper;

namespace CureFlow.Infrastructure.Services.Ehr;

public class LifestyleService : ILifestyleService
{
    private readonly ICureFlowDbSession _db;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;

    public LifestyleService(ICureFlowDbSession db, ITenantContext tenant, IAuditService audit)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<LifestyleProfileDto?> GetAsync(Guid patientId, CancellationToken ct = default)
    {
        var l = await _db.QueryFirstOrDefaultAsync<LifestyleProfile>(
            """
            SELECT * FROM "LifestyleProfiles"
            WHERE "PatientId" = @patientId AND "IsDeleted" = false AND "TenantId" = @TenantId
            """,
            new { patientId },
            ct: ct);
        if (l == null)
        {
            return new LifestyleProfileDto(
                patientId,
                null, null,
                null, null, false,
                null, null, null,
                null, false, false,
                false, null, null,
                SmokingStatus.Unknown, null,
                AlcoholConsumption.Unknown, false,
                null, null, false, null,
                null, null,
                null, false,
                null,
                DateTime.UtcNow);
        }
        return new LifestyleProfileDto(
            l.PatientId,
            l.AverageSleepHours, l.SleepQuality,
            l.WaterIntakeLitersPerDay, l.MealsPerDay, l.SkipsBreakfast,
            l.DietType, l.CuisinePreferences, l.DietaryRestrictions,
            l.CaffeineCupsPerDay, l.ConsumesProcessedFood, l.ConsumesSugaryDrinks,
            l.ExercisesRegularly, l.ExerciseMinutesPerWeek, l.ExerciseType,
            l.SmokingStatus, l.CigarettesPerDay,
            l.AlcoholConsumption, l.ChewsTobaccoOrPaan,
            l.Occupation, l.WorkSchedule, l.HighStressJob, l.StressLevel,
            l.MentalHealthConcerns, l.HasAnxietyOrDepression,
            l.LivingEnvironment, l.ExposureToPollution,
            l.AdditionalLifestyleNotes,
            l.LastUpdatedAt);
    }

    public async Task UpsertAsync(Guid patientId, LifestyleProfileDto dto, CancellationToken ct = default)
    {
        if (patientId == Guid.Empty || dto.PatientId != patientId)
            throw new ValidationException("Lifestyle details must belong to the selected patient.");
        var isNew = false;
        await _db.TransactionAsync(async db =>
        {
        // Serialize initial creation as well as updates to the existing one-profile-per-patient mapping.
        _ = await db.QueryFirstOrDefaultAsync<Patient>("""
            SELECT "Id" FROM "Patients" WHERE "Id"=@patientId AND "TenantId"=@TenantId
                AND NOT "IsDeleted" FOR UPDATE
            """, new { patientId }, ct: ct) ?? throw new NotFoundException("Patient");
        var l = await db.QueryFirstOrDefaultAsync<LifestyleProfile>(
            """
            SELECT * FROM "LifestyleProfiles"
            WHERE "PatientId" = @patientId AND "IsDeleted" = false AND "TenantId" = @TenantId
            """,
            new { patientId },
            ct: ct);
        isNew = l == null;
        if (l == null)
        {
            l = new LifestyleProfile { PatientId = patientId };
        }

        l.AverageSleepHours = dto.AverageSleepHours;
        l.SleepQuality = dto.SleepQuality;
        l.WaterIntakeLitersPerDay = dto.WaterIntakeLitersPerDay;
        l.MealsPerDay = dto.MealsPerDay;
        l.SkipsBreakfast = dto.SkipsBreakfast;
        l.DietType = dto.DietType;
        l.CuisinePreferences = dto.CuisinePreferences;
        l.DietaryRestrictions = dto.DietaryRestrictions;
        l.CaffeineCupsPerDay = dto.CaffeineCupsPerDay;
        l.ConsumesProcessedFood = dto.ConsumesProcessedFood;
        l.ConsumesSugaryDrinks = dto.ConsumesSugaryDrinks;
        l.ExercisesRegularly = dto.ExercisesRegularly;
        l.ExerciseMinutesPerWeek = dto.ExerciseMinutesPerWeek;
        l.ExerciseType = dto.ExerciseType;
        l.SmokingStatus = dto.SmokingStatus;
        l.CigarettesPerDay = dto.CigarettesPerDay;
        l.AlcoholConsumption = dto.AlcoholConsumption;
        l.ChewsTobaccoOrPaan = dto.ChewsTobaccoOrPaan;
        l.Occupation = dto.Occupation;
        l.WorkSchedule = dto.WorkSchedule;
        l.HighStressJob = dto.HighStressJob;
        l.StressLevel = dto.StressLevel;
        l.MentalHealthConcerns = dto.MentalHealthConcerns;
        l.HasAnxietyOrDepression = dto.HasAnxietyOrDepression;
        l.LivingEnvironment = dto.LivingEnvironment;
        l.ExposureToPollution = dto.ExposureToPollution;
        l.AdditionalLifestyleNotes = dto.AdditionalLifestyleNotes;
        l.LastUpdatedAt = DateTime.UtcNow;
        l.LastUpdatedByUserId = _tenant.UserId;

        if (isNew)
            await db.InsertAsync(l, ct: ct);
        else
            await db.UpdateAsync(l, ct: ct);
        }, ct);

        await _audit.LogAsync(isNew ? "lifestyle.create" : "lifestyle.update", "lifestyle", patientId.ToString(), null, ct);
    }
}
