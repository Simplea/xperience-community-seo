namespace XperienceCommunity.SEO.Middleware;

/// <summary>
/// Buffers an HTML response so it can be converted. Any other response goes straight
/// to the original stream, untouched, so binary files are neither buffered nor altered.
/// </summary>
internal sealed class MarkdownBodyCapture : Stream
{
    private readonly HttpResponse response;
    private readonly Stream originalBody;
    private readonly MemoryStream buffer = new();
    private bool? isHtml;

    public MarkdownBodyCapture(HttpResponse response, Stream originalBody)
    {
        this.response = response;
        this.originalBody = originalBody;
    }

    /// <summary>
    /// Whether the response is HTML. Decided on the first write, when the headers are final.
    /// </summary>
    public bool IsHtml => isHtml ??= response.ContentType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true;

    public string GetCapturedText() =>
        Encoding.UTF8.GetString(buffer.ToArray());

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => buffer.Length;
    public override long Position { get => buffer.Position; set => throw new NotSupportedException(); }

    public override void Write(byte[] buffer, int offset, int count) => Target.Write(buffer, offset, count);
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => Target.WriteAsync(buffer, offset, count, cancellationToken);
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => Target.WriteAsync(buffer, cancellationToken);

    public override void Flush() => Target.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => Target.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            buffer.Dispose();
        }
        base.Dispose(disposing);
    }

    private Stream Target => IsHtml ? buffer : originalBody;
}
