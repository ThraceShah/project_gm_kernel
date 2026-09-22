using System.Buffers;

namespace ProjectGmKernel.Xt.JsonTool;

// Utf8JsonWriter(Stream) stores the entire document in one byte array and
// refuses to grow it past the runtime's single-array limit. This buffer
// commits each chunk to the stream so a multi-gigabyte JSON file stays
// within a fixed in-memory window, except for one token larger than the chunk.
public sealed class JsonFileBuffer : IBufferWriter<byte>, IDisposable
{
    private readonly Stream _stream;
    private readonly int _chunkSize;
    private byte[] _buffer;
    private int _written;

    public JsonFileBuffer(Stream stream, int chunkSize = 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);
        _stream = stream;
        _chunkSize = chunkSize;
        _buffer = ArrayPool<byte>.Shared.Rent(chunkSize);
    }

    public void Advance(int count)
    {
        if (count < 0 || _written > _buffer.Length - count)
            throw new ArgumentOutOfRangeException(nameof(count));
        _written += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        Ensure(sizeHint);
        return _buffer.AsMemory(_written);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        Ensure(sizeHint);
        return _buffer.AsSpan(_written);
    }

    public void Dispose()
    {
        FlushWritten();
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = [];
        _stream.Flush();
    }

    private void Ensure(int sizeHint)
    {
        if (sizeHint < 1)
            sizeHint = 1;
        if (_written > 0 && (_written >= _chunkSize || _buffer.Length - _written < sizeHint))
            FlushWritten();
        if (_buffer.Length - _written >= sizeHint)
            return;
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = ArrayPool<byte>.Shared.Rent(Math.Max(_chunkSize, sizeHint));
        _written = 0;
    }

    private void FlushWritten()
    {
        if (_written == 0)
            return;
        _stream.Write(_buffer, 0, _written);
        _written = 0;
    }

}

