namespace FieldApp.Application.Items;

/// <summary>Read-only pass-through that fails once more than <c>limit</c> bytes are read.</summary>
public sealed class LimitedReadStream(Stream inner, long limit) : Stream
{
    private long _read;

    public bool Exceeded { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => _read;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        Count(await inner.ReadAsync(buffer, cancellationToken));

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private int Count(int bytes)
    {
        _read += bytes;
        if (_read > limit)
        {
            Exceeded = true;
            throw new InvalidDataException("The upload is larger than reserved.");
        }

        return bytes;
    }
}

/// <summary>Raised by <see cref="IPhotoStorage"/> implementations when the storage service fails (retryable).</summary>
public sealed class PhotoStorageException : Exception
{
    public PhotoStorageException()
    {
    }

    public PhotoStorageException(string message)
        : base(message)
    {
    }

    public PhotoStorageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
