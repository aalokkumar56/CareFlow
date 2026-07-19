using CureFlow.Application.DTOs;
using CureFlow.Application.Validation;
using FluentAssertions;
using Xunit;

namespace CureFlow.UnitTests;

public class CreateVitalSignsValidatorTests
{
    private readonly CreateVitalSignsValidator _validator = new();

    private static CreateVitalSignsRequest Request(
        Guid? patientId = null,
        decimal? heightCm = null,
        decimal? weightKg = null,
        decimal? systolicBp = null,
        decimal? diastolicBp = null,
        decimal? heartRate = null,
        decimal? temperature = null,
        decimal? respiratoryRate = null,
        decimal? oxygenSaturation = null,
        string? notes = null) =>
        new(
            PatientId: patientId ?? Guid.NewGuid(),
            AppointmentId: null,
            VisitId: null,
            MeasuredAt: null,
            HeightCm: heightCm,
            WeightKg: weightKg,
            SystolicBp: systolicBp,
            DiastolicBp: diastolicBp,
            HeartRate: heartRate,
            Temperature: temperature,
            RespiratoryRate: respiratoryRate,
            OxygenSaturation: oxygenSaturation,
            BloodSugarFasting: null,
            BloodSugarPostprandial: null,
            Hba1c: null,
            Notes: notes);

    [Fact]
    public async Task Valid_request_with_one_measurement_passes()
    {
        var result = await _validator.ValidateAsync(Request(heartRate: 72));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Empty_patient_id_fails()
    {
        var result = await _validator.ValidateAsync(Request(patientId: Guid.Empty, heartRate: 72));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("patient_id"));
    }

    [Fact]
    public async Task No_measurements_fails()
    {
        var result = await _validator.ValidateAsync(Request());

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("at least one vital"));
    }

    [Fact]
    public async Task Notes_alone_count_as_a_measurement()
    {
        var result = await _validator.ValidateAsync(Request(notes: "Patient reports dizziness"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(20)]
    [InlineData(301)]
    public async Task Height_out_of_range_fails(decimal height)
    {
        var result = await _validator.ValidateAsync(Request(heightCm: height));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateVitalSignsRequest.HeightCm));
    }

    [Theory]
    [InlineData(49)]
    [InlineData(301)]
    public async Task Systolic_bp_out_of_range_fails(decimal systolic)
    {
        var result = await _validator.ValidateAsync(Request(systolicBp: systolic));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateVitalSignsRequest.SystolicBp));
    }

    [Theory]
    [InlineData(89)]
    [InlineData(116)]
    public async Task Temperature_out_of_range_fails(decimal temperature)
    {
        var result = await _validator.ValidateAsync(Request(temperature: temperature));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateVitalSignsRequest.Temperature));
    }

    [Theory]
    [InlineData(49)]
    [InlineData(101)]
    public async Task Oxygen_saturation_out_of_range_fails(decimal spo2)
    {
        var result = await _validator.ValidateAsync(Request(oxygenSaturation: spo2));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateVitalSignsRequest.OxygenSaturation));
    }

    [Fact]
    public async Task Boundary_values_pass()
    {
        var result = await _validator.ValidateAsync(Request(
            heightCm: 30,
            weightKg: 0.5m,
            systolicBp: 50,
            diastolicBp: 30,
            heartRate: 20,
            temperature: 90,
            respiratoryRate: 5,
            oxygenSaturation: 50));

        result.IsValid.Should().BeTrue();
    }
}
