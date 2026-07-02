using System.Text.Json;
using System.Text.Json.Serialization;
using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace CureFlow.Tests;

public class CreateUserRequestBindingTests
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    [Theory]
    [InlineData("""{"name":"Jane","email":"j@x.com","password":"secret","role":"reception"}""")]
    [InlineData("""{"name":"Jane","email":"j@x.com","password":"secret","role":"tenant_owner"}""")]
    [InlineData("""{"name":"Jane","email":"j@x.com","password":"secret","role":"doctor","specialty":"Cardio","phone":"999"}""")]
    public void Deserializes_snake_case_role_string(string json)
    {
        var req = JsonSerializer.Deserialize<CreateUserRequest>(json, ApiJsonOptions);
        req.Should().NotBeNull();
        req!.Name.Should().Be("Jane");
        req.Role.Should().BeOneOf("reception", "tenant_owner", "doctor");
        EnumParseHelper.TryParseSnakeCase<UserRole>(req.Role, out _).Should().BeTrue();
    }
}
