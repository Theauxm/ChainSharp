using System.Text.Json;

namespace ChainSharp.Effect.Utils;

public class LimitedStream(Stream inner, long maxBytes) : Stream
{
    private long _count;

    public override void Write(byte[] buffer, int offset, int count)
    {
        _count += count;
        if (_count > maxBytes)
            throw new JsonException($"Serialized JSON exceeded {maxBytes} bytes");
        inner.Write(buffer, offset, count);
    }

    // override required members by delegating to _inner
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => inner.Length;
    public override long Position
    {
        get => inner.Position;
        set => throw new NotSupportedException();
    }

    public override void Flush() => inner.Flush();

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => inner.SetLength(value);
}
