using CureFlow.Domain.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Domain.Entities.Lifestyle;

/// <summary>
/// Comprehensive lifestyle profile for holistic, personalized treatment.
/// One-to-one with Patient.
/// </summary>
public class LifestyleProfile : TenantEntity
{
    public Guid PatientId { get; set; }

    // ===== Daily habits =====
    public decimal? AverageSleepHours { get; set; }
    public string? SleepQuality { get; set; }         // good / poor / interrupted
    public TimeSpan? WakeUpTime { get; set; }
    public TimeSpan? BedTime { get; set; }

    public decimal? WaterIntakeLitersPerDay { get; set; }
    public int? MealsPerDay { get; set; }
    public string? MealTimings { get; set; }          // e.g. "8am, 1pm, 8pm"
    public bool SkipsBreakfast { get; set; }

    // ===== Diet =====
    public string? DietType { get; set; }             // vegetarian / non-veg / vegan / jain
    public string? CuisinePreferences { get; set; }
    public string? FoodAllergiesText { get; set; }
    public string? DietaryRestrictions { get; set; }
    public decimal? CaffeineCupsPerDay { get; set; }
    public bool ConsumesProcessedFood { get; set; }
    public bool ConsumesSugaryDrinks { get; set; }

    // ===== Exercise =====
    public bool ExercisesRegularly { get; set; }
    public int? ExerciseMinutesPerWeek { get; set; }
    public string? ExerciseType { get; set; }         // walking / gym / yoga / cycling
    public string? PhysicalActivityLevel { get; set; } // sedentary / light / moderate / active

    // ===== Substances =====
    public SmokingStatus SmokingStatus { get; set; } = SmokingStatus.Unknown;
    public int? CigarettesPerDay { get; set; }
    public int? YearsOfSmoking { get; set; }
    public AlcoholConsumption AlcoholConsumption { get; set; } = AlcoholConsumption.Unknown;
    public string? AlcoholDetails { get; set; }
    public bool ChewsTobaccoOrPaan { get; set; }
    public string? OtherSubstances { get; set; }

    // ===== Work & stress =====
    public string? Occupation { get; set; }
    public string? WorkSchedule { get; set; }         // 9-5 / shift / night-shift
    public int? WorkHoursPerDay { get; set; }
    public bool HighStressJob { get; set; }
    public string? StressLevel { get; set; }          // low / moderate / high
    public string? StressManagementMethods { get; set; }
    public string? HobbiesAndInterests { get; set; }

    // ===== Mental wellness =====
    public string? MentalHealthConcerns { get; set; }
    public bool? HasAnxietyOrDepression { get; set; }
    public string? CurrentMentalHealthTreatment { get; set; }

    // ===== Reproductive (gender-relevant) =====
    public string? MenstrualCycleStatus { get; set; }
    public string? LastMenstrualPeriod { get; set; }
    public int? NumberOfPregnancies { get; set; }
    public string? ContraceptiveUse { get; set; }

    // ===== Sleep environment =====
    public string? LivingEnvironment { get; set; }    // urban / rural / industrial
    public bool ExposureToPollution { get; set; }
    public string? PetExposure { get; set; }

    // ===== Free-text =====
    public string? AdditionalLifestyleNotes { get; set; }

    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? LastUpdatedByUserId { get; set; }
}
