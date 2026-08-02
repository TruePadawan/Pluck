namespace Pluck.Cli.Utils;

/// <summary>
/// A read-only stream wrapper that reports bytes read via a callback,
/// enabling Spectre.Console progress bar integration with StreamContent.
/// </summary>
public sealed class ProgressStream(Stream inner, Action<long> onBytesRead) : Stream
{
    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length => inner.Length;

    public override long Position
    {
        get => inner.Position;
        set => inner.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = inner.Read(buffer, offset, count);
        if (bytesRead > 0)
            onBytesRead(bytesRead);
        return bytesRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
        CancellationToken cancellationToken)
    {
        var bytesRead = await inner.ReadAsync(buffer, offset, count, cancellationToken);
        if (bytesRead > 0)
            onBytesRead(bytesRead);
        return bytesRead;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        var bytesRead = await inner.ReadAsync(buffer, cancellationToken);
        if (bytesRead > 0)
            onBytesRead(bytesRead);
        return bytesRead;
    }

    public override void Flush() => inner.Flush();
    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            inner.Dispose();
        base.Dispose(disposing);
    }
}
