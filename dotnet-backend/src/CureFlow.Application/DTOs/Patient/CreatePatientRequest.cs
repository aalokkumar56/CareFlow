using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record CreatePatientRequest(
    string Name, string Phone, int? Age, Gender? Gender, BloodGroup? BloodGroup,
    string? Department, string? InquirySource, Guid? ReferringDoctorId,
    string? Email, string? AddressLine1, string? City, string? State, string? Pincode,
    List<string>? Tags, string? Notes, string? Occupation);
