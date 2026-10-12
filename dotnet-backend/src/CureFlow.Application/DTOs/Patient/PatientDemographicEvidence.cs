using CureFlow.Application.Common;
using CureFlow.Domain.Entities;
namespace CureFlow.Application.DTOs;
public record DemographicSourceInput(string SourceType,string? SourceReference,string Reason);
public record BirthDateEvidence(string Precision,int? Year,int? Month,int? Day);
public static class PatientDemographicRules
{
    public static void ValidateSource(DemographicSourceInput? source)
    {
        if(source is null)return;
        if(source.SourceType is not ("local_application" or "patient_reported" or "guardian_reported" or "document_reported" or "staff_observed" or "import"))
            throw new ValidationException("Select a supported demographic evidence source.");
        if(source.SourceReference is not null&&(source.SourceReference.Length>200||source.SourceReference.Any(char.IsControl)))
            throw new ValidationException("Source reference must be bounded.");
        if(source.SourceType is "document_reported" or "import"&&string.IsNullOrWhiteSpace(source.SourceReference))
            throw new ValidationException("This source requires an explicit evidence reference.");
        if(string.IsNullOrWhiteSpace(source.Reason)||source.Reason.Length>500||source.Reason.Any(char.IsControl))
            throw new ValidationException("Supply a bounded demographic change reason.");
    }
    public static void ValidateBirth(BirthDateEvidence? birth)
    {
        if(birth is null)return;
        if(birth.Precision is not ("unknown" or "year" or "month" or "day" or "estimated_year" or "estimated_month" or "estimated_day"))
            throw new ValidationException("Select an explicit birth date precision.");
        var precision=birth.Precision.Replace("estimated_","",StringComparison.Ordinal);
        if(precision=="unknown")
        {
            if(birth.Year.HasValue||birth.Month.HasValue||birth.Day.HasValue)throw new ValidationException("Unknown birth date cannot contain guessed calendar components.");
            return;
        }
        if(birth.Year is not >=1 or >9999||birth.Year>DateTime.UtcNow.Year
            ||precision=="year"&&(birth.Month.HasValue||birth.Day.HasValue)
            ||precision=="month"&&(birth.Month is not >=1 or >12||birth.Day.HasValue)
            ||precision=="day"&&(birth.Month is not >=1 or >12||birth.Day is not >=1 or >31))
            throw new ValidationException("Birth date components must match their stated precision.");
        var month=birth.Month??1;
        var day=birth.Day??1;
        if(day>DateTime.DaysInMonth(birth.Year.Value,month)||new DateOnly(birth.Year.Value,month,day)>DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ValidationException("Supply a valid nonfuture calendar birth date.");
    }
    public static void Apply(Patient patient,BirthDateEvidence? birth)
    {
        ValidateBirth(birth);
        if(birth is null)return;
        patient.BirthDatePrecision=birth.Precision;patient.BirthDateYear=birth.Year;patient.BirthDateMonth=birth.Month;patient.BirthDateDay=birth.Day;
    }
}
