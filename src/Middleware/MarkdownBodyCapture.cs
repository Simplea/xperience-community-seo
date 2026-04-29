namespace XperienceCommunity.SEO.Middleware;

internal sealed class MarkdownBodyCapture : Stream
{
    private readonly Stream originalBody;
    private readonly MemoryStream buffer = new();

    public MarkdownBodyCapture(Stream originalBody)
    {
        this.originalBody = originalBody;
    }

    public string GetCapturedText() =>
        Encoding.UTF8.GetString(buffer.ToArray());

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => buffer.Length;
    public override long Position { get => buffer.Position; set => throw new NotSupportedException(); }

    public override void Write(byte[] buffer, int offset, int count) => this.buffer.Write(buffer, offset, count);
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => this.buffer.WriteAsync(buffer, offset, count, cancellationToken);
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => this.buffer.WriteAsync(buffer, cancellationToken);

    public override void Flush() => buffer.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => buffer.FlushAsync(cancellationToken);

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

    public Stream OriginalBody => originalBody;
}
