using System.Buffers;
using System.Buffers.Binary;

namespace SmodbusTcp;

/// <summary>
/// Writes payload bytes for one Modbus response.
/// </summary>
/// <remarks>
/// Write payload only: registers are big-endian, and coils are packed least-significant bit first
/// with unused high bits zero. Finish all writes before the handler task completes. Do not retain
/// the writer or spans, or use them concurrently. Server buffers are never reused between requests.
/// Operations after completion throw <see cref="InvalidOperationException" />. Little-endian helpers
/// are device-specific encodings; for values spanning multiple registers, they write the
/// least-significant register first.
/// </remarks>
public sealed class ModbusResponseWriter
{
    private readonly IBufferWriter<byte> _writer;
    private bool _completed;

    /// <summary>Creates a response writer over the supplied buffer.</summary>
    /// <param name="writer">The buffer that receives the encoded payload bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="writer" /> is null.</exception>
    public ModbusResponseWriter(IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        _writer = writer;
    }

    /// <summary>Gets the number of bytes advanced through this writer.</summary>
    public int WrittenCount { get; private set; }

    internal void Complete() => _completed = true;

    private void ThrowIfCompleted()
    {
        if (_completed)
        {
            throw new InvalidOperationException("The response writer has completed.");
        }
    }

    /// <summary>
    /// Writes a single byte value to the response writer.
    /// </summary>
    /// <param name="value">The value to append to the response payload.</param>
    /// <exception cref="InvalidOperationException">The response writer has completed.</exception>
    public void Write(byte value)
    {
        var bytes = GetSpan(1);
        bytes[0] = value;
        Advance(1);
    }

    /// <summary>
    /// Writes a 16-bit unsigned integer value to the response writer in big-endian format.
    /// </summary>
    /// <param name="value">The value to append to the response payload.</param>
    /// <exception cref="InvalidOperationException">The response writer has completed.</exception>
    public void WriteUInt16BigEndian(ushort value)
    {
        var bytes = GetSpan(2);
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        Advance(2);
    }

    /// <summary>
    /// Writes a 16-bit signed integer value to the response writer in big-endian format.
    /// </summary>
    /// <param name="value">The value to append to the response payload.</param>
    /// <exception cref="InvalidOperationException">The response writer has completed.</exception>
    public void WriteInt16BigEndian(short value)
    {
        var bytes = GetSpan(2);
        BinaryPrimitives.WriteInt16BigEndian(bytes, value);
        Advance(2);
    }

    /// <summary>
    /// Writes a 32-bit unsigned integer value to the response writer in big-endian format.
    /// </summary>
    /// <param name="value">The value to append to the response payload.</param>
    /// <exception cref="InvalidOperationException">The response writer has completed.</exception>
    public void WriteUInt32BigEndian(uint value)
    {
        var bytes = GetSpan(4);
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        Advance(4);
    }

    /// <summary>
    /// Writes a 32-bit signed integer value to the response writer in big-endian format.
    /// </summary>
    /// <param name="value">The value to append to the response payload.</param>
    /// <exception cref="InvalidOperationException">The response writer has completed.</exception>
    public void WriteInt32BigEndian(int value)
    {
        var bytes = GetSpan(4);
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        Advance(4);
    }

    /// <summary>
    /// Writes a 64-bit unsigned integer value to the response writer in big-endian format.
    /// </summary>
    /// <param name="value">The value to append to the response payload.</param>
    /// <exception cref="InvalidOperationException">The response writer has completed.</exception>
    public void WriteUInt64BigEndian(ulong value)
    {
        var bytes = GetSpan(8);
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        Advance(8);
    }

    /// <summary>
    /// Writes a 64-bit signed integer value to the response writer in big-endian format.
    /// </summary>
    /// <param name="value">The value to append to the response payload.</param>
    /// <exception cref="InvalidOperationException">The response writer has completed.</exception>
    public void WriteInt64BigEndian(long value)
    {
        var bytes = GetSpan(8);
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        Advance(8);
    }

    /// <summary>
    /// Writes a 32-bit floating-point value to the response writer in big-endian IEEE 754 format.
    /// </summary>
    /// <param name="value">The value to append to the response payload.</param>
    /// <exception cref="InvalidOperationException">The response writer has completed.</exception>
    public void WriteSingleBigEndian(float value)
    {
        var bytes = GetSpan(4);
        BinaryPrimitives.WriteSingleBigEndian(bytes, value);
        Advance(4);
    }

    /// <summary>
    /// Writes a 64-bit floating-point value to the response writer in big-endian IEEE 754 format.
    /// </summary>
    /// <param name="value">The value to append to the response payload.</param>
    /// <exception cref="InvalidOperationException">The response writer has completed.</exception>
    public void WriteDoubleBigEndian(double value)
    {
        var bytes = GetSpan(8);
        BinaryPrimitives.WriteDoubleBigEndian(bytes, value);
        Advance(8);
    }

    /// <summary>
    /// Writes a signed fixed-point decimal as a 16-bit integer in big-endian format.
    /// </summary>
    /// <param name="value">The value to encode.</param>
    /// <param name="decimalPlaces">The number of decimal places to preserve, from 0 to 28.</param>
    /// <remarks>
    /// The value is rounded using midpoint rounding to even, then scaled by 10 to the
    /// specified power.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The response writer has completed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="decimalPlaces" /> is outside 0 to 28.</exception>
    /// <exception cref="OverflowException">The scaled value exceeds the decimal or target integer range.</exception>
    public void WriteInt16FixedPointBigEndian(decimal value, int decimalPlaces)
    {
        ThrowIfCompleted();
        var scaledValue = ToFixedPointInteger(value, decimalPlaces);
        WriteInt16BigEndian(checked((short)scaledValue));
    }

    /// <summary>
    /// Writes a signed fixed-point decimal as a 32-bit integer in big-endian format.
    /// </summary>
    /// <param name="value">The value to encode.</param>
    /// <param name="decimalPlaces">The number of decimal places to preserve, from 0 to 28.</param>
    /// <remarks>
    /// The value is rounded using midpoint rounding to even, then scaled by 10 to the
    /// specified power.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The response writer has completed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="decimalPlaces" /> is outside 0 to 28.</exception>
    /// <exception cref="OverflowException">The scaled value exceeds the decimal or target integer range.</exception>
    public void WriteInt32FixedPointBigEndian(decimal value, int decimalPlaces)
    {
        ThrowIfCompleted();
        var scaledValue = ToFixedPointInteger(value, decimalPlaces);
        WriteInt32BigEndian(checked((int)scaledValue));
    }

    /// <summary>
    /// Writes a signed fixed-point decimal as a 64-bit integer in big-endian format.
    /// </summary>
    /// <param name="value">The value to encode.</param>
    /// <param name="decimalPlaces">The number of decimal places to preserve, from 0 to 28.</param>
    /// <remarks>
    /// The value is rounded using midpoint rounding to even, then scaled by 10 to the
    /// specified power.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The response writer has completed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="decimalPlaces" /> is outside 0 to 28.</exception>
    /// <exception cref="OverflowException">The scaled value exceeds the decimal or target integer range.</exception>
    public void WriteInt64FixedPointBigEndian(decimal value, int decimalPlaces)
    {
        ThrowIfCompleted();
        WriteInt64BigEndian(ToFixedPointInteger(value, decimalPlaces));
    }

    /// <summary>
    /// Writes a 16-bit unsigned integer value to the response writer in little-endian format.
    /// </summary>
    /// <param name="value">The value to append to the response payload.</param>
    /// <exception cref="InvalidOperationException">The response writer has completed.</exception>
    public void WriteUInt16LittleEndian(ushort value)
    {
        var bytes = GetSpan(2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        Advance(2);
    }

    /// <summary>
    /// Writes a 16-bit signed integer value to the response writer in little-endian format.
    /// </summary>
    /// <param name="value">The value to append to the response payload.</param>
    /// <exception cref="InvalidOperationException">The response writer has completed.</exception>
    public void WriteInt16LittleEndian(short value)
    {
        var bytes = GetSpan(2);
        BinaryPrimitives.WriteInt16LittleEndian(bytes, value);
        Advance(2);
    }

    /// <summary>
    /// Writes a 32-bit unsigned integer value to the response writer in little-endian format.
    /// </summary>
    /// <param name="value">The value to append to the response payload.</param>
    /// <exception cref="InvalidOperationException">The response writer has completed.</exception>
    public void WriteUInt32LittleEndian(uint value)
    {
        var bytes = GetSpan(4);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        Advance(4);
    }

    /// <summary>
    /// Writes a 32-bit signed integer value to the response writer in little-endian format.
    /// </summary>
    /// <param name="value">The value to append to the response payload.</param>
    /// <exception cref="InvalidOperationException">The response writer has completed.</exception>
    public void WriteInt32LittleEndian(int value)
    {
        var bytes = GetSpan(4);
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        Advance(4);
    }

    /// <summary>
    /// Writes a 64-bit unsigned integer value to the response writer in little-endian format.
    /// </summary>
    /// <param name="value">The value to append to the response payload.</param>
    /// <exception cref="InvalidOperationException">The response writer has completed.</exception>
    public void WriteUInt64LittleEndian(ulong value)
    {
        var bytes = GetSpan(8);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        Advance(8);
    }

    /// <summary>
    /// Writes a 64-bit signed integer value to the response writer in little-endian format.
    /// </summary>
    /// <param name="value">The value to append to the response payload.</param>
    /// <exception cref="InvalidOperationException">The response writer has completed.</exception>
    public void WriteInt64LittleEndian(long value)
    {
        var bytes = GetSpan(8);
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        Advance(8);
    }

    /// <summary>
    /// Writes a 32-bit floating-point value to the response writer in little-endian IEEE 754 format.
    /// </summary>
    /// <param name="value">The value to append to the response payload.</param>
    /// <exception cref="InvalidOperationException">The response writer has completed.</exception>
    public void WriteSingleLittleEndian(float value)
    {
        var bytes = GetSpan(4);
        BinaryPrimitives.WriteSingleLittleEndian(bytes, value);
        Advance(4);
    }

    /// <summary>
    /// Writes a 64-bit floating-point value to the response writer in little-endian IEEE 754 format.
    /// </summary>
    /// <param name="value">The value to append to the response payload.</param>
    /// <exception cref="InvalidOperationException">The response writer has completed.</exception>
    public void WriteDoubleLittleEndian(double value)
    {
        var bytes = GetSpan(8);
        BinaryPrimitives.WriteDoubleLittleEndian(bytes, value);
        Advance(8);
    }

    /// <summary>
    /// Writes a signed fixed-point decimal as a 16-bit integer in little-endian format.
    /// </summary>
    /// <param name="value">The value to encode.</param>
    /// <param name="decimalPlaces">The number of decimal places to preserve, from 0 to 28.</param>
    /// <remarks>
    /// The value is rounded using midpoint rounding to even, then scaled by 10 to the
    /// specified power.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The response writer has completed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="decimalPlaces" /> is outside 0 to 28.</exception>
    /// <exception cref="OverflowException">The scaled value exceeds the decimal or target integer range.</exception>
    public void WriteInt16FixedPointLittleEndian(decimal value, int decimalPlaces)
    {
        ThrowIfCompleted();
        var scaledValue = ToFixedPointInteger(value, decimalPlaces);
        WriteInt16LittleEndian(checked((short)scaledValue));
    }

    /// <summary>
    /// Writes a signed fixed-point decimal as a 32-bit integer in little-endian format.
    /// </summary>
    /// <param name="value">The value to encode.</param>
    /// <param name="decimalPlaces">The number of decimal places to preserve, from 0 to 28.</param>
    /// <remarks>
    /// The value is rounded using midpoint rounding to even, then scaled by 10 to the
    /// specified power.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The response writer has completed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="decimalPlaces" /> is outside 0 to 28.</exception>
    /// <exception cref="OverflowException">The scaled value exceeds the decimal or target integer range.</exception>
    public void WriteInt32FixedPointLittleEndian(decimal value, int decimalPlaces)
    {
        ThrowIfCompleted();
        var scaledValue = ToFixedPointInteger(value, decimalPlaces);
        WriteInt32LittleEndian(checked((int)scaledValue));
    }

    /// <summary>
    /// Writes a signed fixed-point decimal as a 64-bit integer in little-endian format.
    /// </summary>
    /// <param name="value">The value to encode.</param>
    /// <param name="decimalPlaces">The number of decimal places to preserve, from 0 to 28.</param>
    /// <remarks>
    /// The value is rounded using midpoint rounding to even, then scaled by 10 to the
    /// specified power.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The response writer has completed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="decimalPlaces" /> is outside 0 to 28.</exception>
    /// <exception cref="OverflowException">The scaled value exceeds the decimal or target integer range.</exception>
    public void WriteInt64FixedPointLittleEndian(decimal value, int decimalPlaces)
    {
        ThrowIfCompleted();
        WriteInt64LittleEndian(ToFixedPointInteger(value, decimalPlaces));
    }

    private static long ToFixedPointInteger(decimal value, int decimalPlaces)
    {
        if (decimalPlaces is < 0 or > 28)
        {
            throw new ArgumentOutOfRangeException(nameof(decimalPlaces), "Precision must be between 0 and 28.");
        }

        var roundedValue = decimal.Round(value, decimalPlaces, MidpointRounding.ToEven);
        var scale = 1m;
        for (var i = 0; i < decimalPlaces; i++)
        {
            scale *= 10m;
        }

        return decimal.ToInt64(roundedValue * scale);
    }

    /// <summary>
    /// Gets a span of bytes from the response writer with the specified size hint.
    /// </summary>
    /// <param name="sizeHint">The minimum requested span length, or zero to request a nonempty span.</param>
    /// <returns>
    /// A writable span with at least the requested length. Commit initialized bytes with <see cref="Advance" />
    /// before requesting another span.
    /// </returns>
    /// <exception cref="InvalidOperationException">The response writer has completed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sizeHint" /> is negative.</exception>
    public Span<byte> GetSpan(int sizeHint)
    {
        ThrowIfCompleted();
        return _writer.GetSpan(sizeHint);
    }

    /// <summary>
    /// Advances the response writer by the specified count of bytes.
    /// </summary>
    /// <param name="count">The number of initialized bytes to commit from the last acquired span.</param>
    /// <exception cref="InvalidOperationException">The response writer has completed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count" /> is negative.</exception>
    /// <exception cref="OverflowException">The total written byte count exceeds <see cref="int.MaxValue" />.</exception>
    /// <remarks>
    /// The count must not exceed the length of the last acquired span. Additional validation is delegated to the
    /// underlying buffer writer.
    /// </remarks>
    public void Advance(int count)
    {
        ThrowIfCompleted();
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var total = checked(WrittenCount + count);
        _writer.Advance(count);
        WrittenCount = total;
    }
}
