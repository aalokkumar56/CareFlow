using CureFlow.Application.Common;
using CureFlow.Application.Options;
using FluentAssertions;
using Xunit;

namespace CureFlow.UnitTests;

public class WhatsappMediaPolicyTests
{
    private static readonly WhatsappMediaOptions Options = new();

    [Theory]
    [InlineData("image/jpeg", WhatsappMediaKind.Image)]
    [InlineData("image/png", WhatsappMediaKind.Image)]
    [InlineData("video/mp4", WhatsappMediaKind.Video)]
    [InlineData("audio/ogg", WhatsappMediaKind.Audio)]
    [InlineData("audio/ogg; codecs=opus", WhatsappMediaKind.Audio)]
    [InlineData("application/pdf", WhatsappMediaKind.Document)]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document", WhatsappMediaKind.Document)]
    public void Classifies_known_mime_types(string mime, WhatsappMediaKind expected)
    {
        WhatsappMediaPolicy.ClassifyByMime(mime, Options).Should().Be(expected);
    }

    [Theory]
    [InlineData("image", WhatsappMediaKind.Image)]
    [InlineData("sticker", WhatsappMediaKind.Image)]
    [InlineData("video", WhatsappMediaKind.Video)]
    [InlineData("audio", WhatsappMediaKind.Audio)]
    [InlineData("voice", WhatsappMediaKind.Audio)]
    [InlineData("document", WhatsappMediaKind.Document)]
    public void Classifies_known_message_types(string type, WhatsappMediaKind expected)
    {
        WhatsappMediaPolicy.ClassifyByType(type).Should().Be(expected);
    }

    [Fact]
    public void Text_type_is_not_media()
    {
        WhatsappMediaPolicy.ClassifyByType("text").Should().BeNull();
    }

    [Theory]
    [InlineData("image/svg+xml")]
    [InlineData("image/svg+xml; charset=utf-8")]
    [InlineData("IMAGE/SVG+XML")]
    public void Rejects_svg_even_under_lenient_image_fallback(string mime)
    {
        WhatsappMediaPolicy.ClassifyByMime(mime, Options).Should().BeNull();
        WhatsappMediaPolicy.Validate(mime, 1024, Options).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Still_accepts_other_images_under_lenient_fallback()
    {
        WhatsappMediaPolicy.ClassifyByMime("image/bmp", Options).Should().Be(WhatsappMediaKind.Image);
    }

    [Fact]
    public void Validates_acceptable_image()
    {
        var result = WhatsappMediaPolicy.Validate("image/png", 2 * 1024 * 1024, Options);
        result.IsValid.Should().BeTrue();
        result.Kind.Should().Be(WhatsappMediaKind.Image);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Rejects_unsupported_mime_type()
    {
        var result = WhatsappMediaPolicy.Validate("application/x-msdownload", 1024, Options);
        result.IsValid.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Rejects_oversized_image()
    {
        var result = WhatsappMediaPolicy.Validate("image/jpeg", Options.MaxImageBytes + 1, Options);
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("too large");
    }

    [Fact]
    public void Rejects_empty_file()
    {
        var result = WhatsappMediaPolicy.Validate("image/jpeg", 0, Options);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Allows_large_document_within_limit()
    {
        var result = WhatsappMediaPolicy.Validate("application/pdf", 80L * 1024 * 1024, Options);
        result.IsValid.Should().BeTrue();
        result.Kind.Should().Be(WhatsappMediaKind.Document);
    }
}
