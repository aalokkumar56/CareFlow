using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record LifestyleProfileDto(
    Guid PatientId,
    decimal? AverageSleepHours, string? SleepQuality,
    decimal? WaterIntakeLitersPerDay, int? MealsPerDay, bool SkipsBreakfast,
    string? DietType, string? CuisinePreferences, string? DietaryRestrictions,
    decimal? CaffeineCupsPerDay, bool ConsumesProcessedFood, bool ConsumesSugaryDrinks,
    bool ExercisesRegularly, int? ExerciseMinutesPerWeek, string? ExerciseType,
    SmokingStatus SmokingStatus, int? CigarettesPerDay,
    AlcoholConsumption AlcoholConsumption, bool ChewsTobaccoOrPaan,
    string? Occupation, string? WorkSchedule, bool HighStressJob, string? StressLevel,
    string? MentalHealthConcerns, bool? HasAnxietyOrDepression,
    string? LivingEnvironment, bool ExposureToPollution,
    string? AdditionalLifestyleNotes,
    DateTime LastUpdatedAt);
