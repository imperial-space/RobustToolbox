using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using Robust.Shared.Log;

namespace Robust.Server.Imperial.Serialization;

/// <summary>
/// Read-only stream wrapper that stops the deserialization of untrusted network data once it nests too deep,
/// before NetSerializer's recursion can overflow the native stack and crash the server.
/// </summary>
/// <remarks>
/// <para>
/// NetSerializer recurses once per nesting level and reads at least a type id or a collection length before it
/// descends, so checking on every read covers every recursive shape, not only nested <c>object[]</c>.
/// </para>
/// <para>
/// The limit is a stack budget measured from where deserialization started, so it trips at the same depth on every
/// thread and OS and leaves the rest of the stack for unwinding. A <see cref="StackOverflowException"/> cannot be
/// caught, while the <see cref="InvalidDataException"/> thrown here is caught by <c>NetManager</c>, which then
/// drops the sender's connection.
/// </para>
/// </remarks>
internal sealed class RecursionGuardStream : Stream
{
    private readonly Stream _stream;
    private readonly long _maxStackBytes;
    private readonly ISawmill _sawmill;
    private readonly nint _startStackPosition;

    /// <param name="stream">Stream holding the untrusted data.</param>
    /// <param name="maxStackBytes">Stack budget in bytes. 0 or less disables it and keeps only the end-of-stack backstop.</param>
    /// <param name="sawmill">Sawmill that reports blocked packets.</param>
    public RecursionGuardStream(Stream stream, long maxStackBytes, ISawmill sawmill)
    {
        _stream = stream;
        _maxStackBytes = maxStackBytes;
        _sawmill = sawmill;
        _startStackPosition = GetStackPosition();
    }

    private static unsafe nint GetStackPosition()
    {
        byte marker = 0;
        return (nint) (&marker);
    }

    private void CheckStack()
    {
        // The stack grows down on every platform .NET runs on.
        var usedBytes = _startStackPosition - GetStackPosition();

        if (_maxStackBytes > 0 && usedBytes > _maxStackBytes)
            Reject(usedBytes);

        // Backstop for a thread whose whole stack is smaller than the budget.
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
            Reject(usedBytes);
    }

    [DoesNotReturn]
    private void Reject(long usedBytes)
    {
        var payloadBytes = _stream.CanSeek ? _stream.Length : -1;

        _sawmill.Warning($"""
            Deserialization guard blocked a nested packet before it could crash the server.
            Stack used: {usedBytes / 1024} KB, limit: {_maxStackBytes / 1024} KB, payload: {payloadBytes} bytes.
            The client disconnected with "Failed to deserialize packet." right after this line sent it.
        """);

        throw new InvalidDataException($"Deserialized data is nested too deep: {usedBytes / 1024} KB of stack used, limit is {_maxStackBytes / 1024} KB.");
    }

    public override int ReadByte()
    {
        CheckStack();
        return _stream.ReadByte();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        CheckStack();
        return _stream.Read(buffer, offset, count);
    }

    public override int Read(Span<byte> buffer)
    {
        CheckStack();
        return _stream.Read(buffer);
    }

    public override bool CanRead => _stream.CanRead;
    public override bool CanSeek => _stream.CanSeek;
    public override bool CanWrite => false;
    public override long Length => _stream.Length;

    public override long Position
    {
        get => _stream.Position;
        set => _stream.Position = value;
    }

    public override void Flush() => _stream.Flush();
    public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
