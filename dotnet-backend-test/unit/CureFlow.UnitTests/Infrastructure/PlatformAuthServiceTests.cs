using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities.Saas;
using CureFlow.Infrastructure.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace CureFlow.UnitTests.Infrastructure;

public class PlatformAuthServiceTests
{
    private static (
        PlatformAuthService Service,
        Mock<ICureFlowDbSession> Db,
        Mock<IPasswordHasher> Hasher,
        Mock<IJwtTokenService> Jwt) Build()
    {
        var db = new Mock<ICureFlowDbSession>();
        var hasher = new Mock<IPasswordHasher>();
        var jwt = new Mock<IJwtTokenService>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed");
        jwt.Setup(j => j.IssuePlatformUser(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .Returns("platform.jwt.token");
        return (new PlatformAuthService(db.Object, hasher.Object, jwt.Object), db, hasher, jwt);
    }

    [Fact]
    public async Task GetSetupStatusAsync_needs_setup_when_no_active_owners()
    {
        var (service, db, _, _) = Build();
        db.Setup(x => x.QueryFirstOrDefaultAsync<int?>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var status = await service.GetSetupStatusAsync();
        status.NeedsSetup.Should().BeTrue();
    }

    [Fact]
    public async Task GetSetupStatusAsync_does_not_need_setup_when_owners_exist()
    {
        var (service, db, _, _) = Build();
        db.Setup(x => x.QueryFirstOrDefaultAsync<int?>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var status = await service.GetSetupStatusAsync();
        status.NeedsSetup.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("A")]
    public async Task BootstrapOwnerAsync_rejects_invalid_name(string? name)
    {
        var (service, _, _, _) = Build();
        var act = () => service.BootstrapOwnerAsync(new PlatformBootstrapRequest(name!, "ops@cureflow.test", "password12"));
        await act.Should().ThrowAsync<ValidationException>().WithMessage("*Name*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task BootstrapOwnerAsync_rejects_invalid_email(string? email)
    {
        var (service, _, _, _) = Build();
        var act = () => service.BootstrapOwnerAsync(new PlatformBootstrapRequest("Ops Admin", email!, "password12"));
        await act.Should().ThrowAsync<ValidationException>().WithMessage("*email*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("1234567")]
    public async Task BootstrapOwnerAsync_rejects_short_password(string password)
    {
        var (service, _, _, _) = Build();
        var act = () => service.BootstrapOwnerAsync(new PlatformBootstrapRequest("Ops Admin", "ops@cureflow.test", password));
        await act.Should().ThrowAsync<ValidationException>().WithMessage("*Password*");
    }

    [Fact]
    public async Task BootstrapOwnerAsync_conflicts_when_owner_already_exists()
    {
        var (service, db, _, _) = Build();
        db.Setup(x => x.QueryFirstOrDefaultAsync<int?>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var act = () => service.BootstrapOwnerAsync(
            new PlatformBootstrapRequest("Ops Admin", "ops@cureflow.test", "password12"));

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*already exists*");
        db.Verify(x => x.InsertAsync(It.IsAny<PlatformUser>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BootstrapOwnerAsync_creates_owner_and_issues_platform_jwt()
    {
        var (service, db, hasher, jwt) = Build();
        db.Setup(x => x.QueryFirstOrDefaultAsync<int?>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        PlatformUser? inserted = null;
        db.Setup(x => x.InsertAsync(It.IsAny<PlatformUser>(), true, It.IsAny<CancellationToken>()))
            .Callback<PlatformUser, bool, CancellationToken>((u, _, _) => inserted = u)
            .Returns(Task.CompletedTask);

        var result = await service.BootstrapOwnerAsync(
            new PlatformBootstrapRequest("  Ops Admin  ", "Ops@CureFlow.TEST", "password12"));

        result.AccessToken.Should().Be("platform.jwt.token");
        result.Email.Should().Be("ops@cureflow.test");
        result.Name.Should().Be("Ops Admin");
        inserted.Should().NotBeNull();
        inserted!.Email.Should().Be("ops@cureflow.test");
        inserted.PasswordHash.Should().Be("hashed");
        hasher.Verify(h => h.Hash("password12"), Times.Once);
        jwt.Verify(j => j.IssuePlatformUser(inserted.Id, "ops@cureflow.test", null), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_rejects_unknown_or_bad_password()
    {
        var (service, db, hasher, _) = Build();
        db.Setup(x => x.QueryFirstOrDefaultAsync<PlatformUser>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlatformUser?)null);

        var act = () => service.LoginAsync(new LoginRequest("missing@cureflow.test", "password12"));
        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.StatusCode.Should().Be(401);
        ex.Which.Message.Should().Contain("Invalid email or password");
        hasher.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_returns_token_when_credentials_valid()
    {
        var (service, db, hasher, jwt) = Build();
        var user = new PlatformUser
        {
            Id = Guid.NewGuid(),
            Name = "Ops",
            Email = "ops@cureflow.test",
            PasswordHash = "stored-hash",
        };
        db.Setup(x => x.QueryFirstOrDefaultAsync<PlatformUser>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        hasher.Setup(h => h.Verify("password12", "stored-hash")).Returns(true);

        var result = await service.LoginAsync(new LoginRequest("OPS@cureflow.test", "password12"));

        result.AccessToken.Should().Be("platform.jwt.token");
        result.Email.Should().Be("ops@cureflow.test");
        result.Name.Should().Be("Ops");
        jwt.Verify(j => j.IssuePlatformUser(user.Id, user.Email, null), Times.Once);
    }
}
