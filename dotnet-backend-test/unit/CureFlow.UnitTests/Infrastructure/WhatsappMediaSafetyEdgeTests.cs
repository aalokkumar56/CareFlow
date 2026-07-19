using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Options;
using CureFlow.Infrastructure.External;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CureFlow.UnitTests.Infrastructure;

/// <summary>
/// Media policy edges plus path/filename safety for the WhatsApp media store.
/// </summary>
public class WhatsappMediaPolicyEdgeTests
{
    private static readonly WhatsappMediaOptions Options = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ClassifyByMime_rejects_null_or_blank(string? mime)
    {
        WhatsappMediaPolicy.ClassifyByMime(mime, Options).Should().BeNull();
        WhatsappMediaPolicy.Validate(mime, 1024, Options).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("reaction")]
    [InlineData("location")]
    [InlineData("contacts")]
    public void ClassifyByType_rejects_non_media(string? type)
    {
        WhatsappMediaPolicy.ClassifyByType(type).Should().BeNull();
    }

    [Fact]
    public void ClassifyByType_trims_and_is_case_insensitive()
    {
        WhatsappMediaPolicy.ClassifyByType("  IMAGE  ").Should().Be(WhatsappMediaKind.Image);
        WhatsappMediaPolicy.ClassifyByType("Voice").Should().Be(WhatsappMediaKind.Audio);
    }

    [Theory]
    [InlineData("application/javascript")]
    [InlineData("text/html")]
    [InlineData("application/x-sh")]
    [InlineData("application/octet-stream")]
    public void Validate_rejects_executable_or_opaque_mime_types(string mime)
    {
        var result = WhatsappMediaPolicy.Validate(mime, 2048, Options);
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Unsupported");
    }

    [Fact]
    public void Validate_rejects_oversized_video_and_audio()
    {
        WhatsappMediaPolicy.Validate("video/mp4", Options.MaxVideoBytes + 1, Options)
            .IsValid.Should().BeFalse();
        WhatsappMediaPolicy.Validate("audio/mpeg", Options.MaxAudioBytes + 1, Options)
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_rejects_document_over_cap()
    {
        var result = WhatsappMediaPolicy.Validate(
            "application/pdf", Options.MaxDocumentBytes + 1, Options);
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("too large");
    }

    [Fact]
    public void Lenient_image_fallback_still_rejects_svg_with_parameters()
    {
        WhatsappMediaPolicy.ClassifyByMime("image/svg+xml;charset=utf-8", Options)
            .Should().BeNull();
    }

    [Fact]
    public void Custom_allow_list_can_restrict_jpeg_only()
    {
        var tight = new WhatsappMediaOptions
        {
            AllowedImageMimeTypes = ["image/jpeg"],
            AllowedVideoMimeTypes = [],
            AllowedAudioMimeTypes = [],
            AllowedDocumentMimeTypes = [],
        };

        WhatsappMediaPolicy.ClassifyByMime("image/jpeg", tight).Should().Be(WhatsappMediaKind.Image);
        // Lenient image/* fallback still classifies png as image for inbound.
        WhatsappMediaPolicy.ClassifyByMime("image/png", tight).Should().Be(WhatsappMediaKind.Image);
        WhatsappMediaPolicy.ClassifyByMime("application/pdf", tight).Should().BeNull();
    }

    [Theory]
    [InlineData(WhatsappMediaKind.Image, "image")]
    [InlineData(WhatsappMediaKind.Video, "video")]
    [InlineData(WhatsappMediaKind.Audio, "audio")]
    [InlineData(WhatsappMediaKind.Document, "document")]
    public void ToTypeString_maps_kinds(WhatsappMediaKind kind, string expected)
    {
        WhatsappMediaPolicy.ToTypeString(kind).Should().Be(expected);
    }

    [Fact]
    public void FormatBytes_uses_readable_units()
    {
        WhatsappMediaPolicy.FormatBytes(500).Should().Be("500 B");
        WhatsappMediaPolicy.FormatBytes(2048).Should().Contain("KB");
        WhatsappMediaPolicy.FormatBytes(5L * 1024 * 1024).Should().Contain("MB");
    }

    [Fact]
    public void MaxBytesFor_matches_options()
    {
        WhatsappMediaPolicy.MaxBytesFor(WhatsappMediaKind.Image, Options).Should().Be(Options.MaxImageBytes);
        WhatsappMediaPolicy.MaxBytesFor(WhatsappMediaKind.Document, Options).Should().Be(Options.MaxDocumentBytes);
    }
}

public class WhatsappMediaStoreSafetyTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly WhatsappMediaStore _store;

    public WhatsappMediaStoreSafetyTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), "cureflow-media-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_contentRoot);

        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(_contentRoot);

        _store = new WhatsappMediaStore(
            env.Object,
            Mock.Of<IHttpClientFactory>(),
            Mock.Of<IWhatsAppSettingsService>(),
            Options.Create(new WhatsappMediaOptions()),
            Options.Create(new WhatsappOptions()),
            NullLogger<WhatsappMediaStore>.Instance);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_contentRoot))
                Directory.Delete(_contentRoot, recursive: true);
        }
        catch
        {
            // Best-effort cleanup for temp media fixtures.
        }
    }

    [Fact]
    public async Task SaveOutboundAsync_strips_path_traversal_from_file_name()
    {
        var tenantId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        var stored = await _store.SaveOutboundAsync(
            tenantId,
            messageId,
            bytes,
            @"..\..\evil\..\passwd.png",
            "image/png");

        stored.FileName.Should().Be("passwd.png");
        stored.FileName.Should().NotContain("..");
        stored.FileName.Should().NotContain(Path.DirectorySeparatorChar.ToString());
        stored.RelativePath.Should().NotContain("..");
        stored.Kind.Should().Be(WhatsappMediaKind.Image);

        var absolute = _store.ResolveExistingPath(stored.RelativePath);
        absolute.Should().NotBeNull();
        File.Exists(absolute!).Should().BeTrue();
    }

    [Fact]
    public async Task SaveOutboundAsync_replaces_invalid_filename_characters()
    {
        var stored = await _store.SaveOutboundAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new byte[] { 1, 2, 3, 4 },
            "report:name|with*bad?.pdf",
            "application/pdf");

        stored.FileName.Should().NotContain(":");
        stored.FileName.Should().NotContain("|");
        stored.FileName.Should().NotContain("*");
        stored.FileName.Should().NotContain("?");
        stored.FileName.Should().EndWith(".pdf");
    }

    [Fact]
    public async Task SaveOutboundAsync_defaults_name_when_blank()
    {
        var stored = await _store.SaveOutboundAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new byte[] { 1, 2, 3 },
            "   ",
            "image/jpeg");

        stored.FileName.Should().Be("attachment.jpg");
    }

    [Fact]
    public async Task ResolveExistingPath_blocks_path_traversal_outside_storage()
    {
        var tenantId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var stored = await _store.SaveOutboundAsync(
            tenantId, messageId, new byte[] { 9, 9, 9 }, "safe.png", "image/png");

        // Sibling file outside the media root must not be readable via traversal.
        var outside = Path.Combine(_contentRoot, "secret.txt");
        await File.WriteAllTextAsync(outside, "classified");

        var traversal = stored.RelativePath.Replace(
            $"{messageId:N}.png",
            $"../../secret.txt");

        _store.ResolveExistingPath(traversal).Should().BeNull();
        _store.ResolveExistingPath("../secret.txt").Should().BeNull();
        _store.ResolveExistingPath("/etc/passwd").Should().BeNull();
        _store.ResolveExistingPath(null).Should().BeNull();
        _store.ResolveExistingPath("").Should().BeNull();
    }

    [Fact]
    public async Task SaveOutboundAsync_rejects_empty_and_unsupported_payloads()
    {
        var actEmpty = () => _store.SaveOutboundAsync(
            Guid.NewGuid(), Guid.NewGuid(), Array.Empty<byte>(), "a.png", "image/png");
        await actEmpty.Should().ThrowAsync<ValidationException>();

        var actMime = () => _store.SaveOutboundAsync(
            Guid.NewGuid(), Guid.NewGuid(), new byte[] { 1 }, "a.exe", "application/x-msdownload");
        await actMime.Should().ThrowAsync<ValidationException>();
    }
}
