using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record TenantDto(Guid Id, string Slug, string Name, SubscriptionPlan Plan, SubscriptionStatus Status);
