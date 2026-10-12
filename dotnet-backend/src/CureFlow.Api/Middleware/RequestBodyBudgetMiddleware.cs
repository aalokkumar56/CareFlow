using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http;

namespace CureFlow.Api.Middleware;

public sealed class RequestBodyBudgetMiddleware
{
    private readonly RequestDelegate _next;
    private readonly long _jsonBytes, _formBytes, _mediaBytes;
    public RequestBodyBudgetMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _jsonBytes = Read(config, "JsonBytes", 1024 * 1024, 8L * 1024 * 1024);
        _formBytes = Read(config, "FormBytes", 11L * 1024 * 1024, 32L * 1024 * 1024);
        _mediaBytes = Read(config, "MediaBytes", 101L * 1024 * 1024, 101L * 1024 * 1024);
    }
    private static long Read(IConfiguration config, string name, long fallback, long maximum) => Math.Clamp(config.GetValue<long?>("RequestLimits:" + name) ?? fallback, 1024, maximum);
    public async Task InvokeAsync(HttpContext ctx)
    {
        var isMedia = ctx.Request.Path.Equals(new PathString("/api/whatsapp/sendmedia"))
            || (ctx.Request.Path.StartsWithSegments("/api/conversations") && ctx.Request.Path.Value!.EndsWith("/media", StringComparison.OrdinalIgnoreCase));
        var limit = ctx.Request.HasFormContentType ? isMedia ? _mediaBytes : _formBytes : _jsonBytes;
        var serverLimit = ctx.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (serverLimit is { IsReadOnly: false }) serverLimit.MaxRequestBodySize = limit;
        if (ctx.Request.ContentLength > limit)
        {
            await ApiProblems.WriteAsync(ctx, ApiProblems.Create(ctx, 413, "Request exceeds the permitted size. Send a smaller request."));
            return;
        }
        var original = ctx.Request.Body;
        ctx.Request.Body = new LimitedBodyStream(original, limit, () => ctx.Items["request_body_limit_exceeded"] = true);
        try { await _next(ctx); }
        finally { ctx.Request.Body = original; }
    }

    private sealed class LimitedBodyStream(Stream inner, long limit, Action exceeded) : Stream
    {
        private long _read;
        private int Count(int size)
        {
            _read += size;
            if (_read > limit)
            {
                exceeded();
                throw new BadHttpRequestException("Request exceeds the permitted size.", 413);
            }
            return size;
        }
        public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));
        public override int Read(Span<byte> buffer) => Count(inner.Read(buffer));
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => Count(await inner.ReadAsync(buffer, offset, count, ct));
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => Count(await inner.ReadAsync(buffer, ct));
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        // The request owns the inner stream; never close it from this wrapper.
    }
}
