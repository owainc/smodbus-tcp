using System.Buffers;
using System.Buffers.Binary;

namespace SmodbusTcp;

/// <summary>A multiple-coil-write request with owned raw payload bytes.</summary>
/// <remarks>
/// Create instances with <see cref="TryParse(ReadOnlySpan{byte}, out MultiCoilWrite, out int)" />.
/// A default instance contains no values and is not a valid write request.
/// </remarks>
public readonly struct MultiCoilWrite : IPacketStruct<MultiCoilWrite>
{
    /// <summary>The fixed header length, including the function and byte count.</summary>
    public static int HeaderLength => 6;

    static int IPacketStruct<MultiCoilWrite>.Length => HeaderLength;
    static bool IPacketStruct<MultiCoilWrite>.IsVariableLength => true;

    /// <summary>Gets the function code from the request.</summary>
    public ModbusFunctionCode Function { get; }

    /// <summary>Gets the starting address of the request.</summary>
    public ushort FirstAddress { get; }

    /// <summary>Gets the number of coils to write.</summary>
    public ushort CoilCount { get; }

    /// <summary>Gets the length of the encoded values in bytes.</summary>
    public byte ByteCount { get; }

    /// <summary>Gets owned coil bytes, packed least-significant bit first within each byte.</summary>
    public ReadOnlyMemory<byte> RawValues { get; }

    /// <summary>
    /// Creates a new <see cref="MultiCoilWrite" /> from the given source span.
    /// </summary>
    private MultiCoilWrite(ReadOnlySpan<byte> source)
    {
        Function = (ModbusFunctionCode)source[0];
        FirstAddress = BinaryPrimitives.ReadUInt16BigEndian(source[1..]);
        CoilCount = BinaryPrimitives.ReadUInt16BigEndian(source[3..]);
        ByteCount = source[5];
        RawValues = source[HeaderLength..].ToArray();
    }

    /// <summary>Decodes the raw payload into a new array in address order.</summary>
    /// <returns>A newly allocated array containing the decoded values in ascending address order.</returns>
    public bool[] GetValues()
    {
        var values = new bool[CoilCount];
        CopyValuesTo(values);
        return values;
    }

    /// <summary>Decodes the raw payload into the destination span in address order.</summary>
    /// <param name="destination">
    /// The span that receives exactly <see cref="CoilCount" /> values; any remaining elements are
    /// unchanged.
    /// </param>
    /// <exception cref="ArgumentException">The destination is shorter than <see cref="CoilCount" />.</exception>
    public void CopyValuesTo(Span<bool> destination)
    {
        if (destination.Length < CoilCount)
        {
            throw new ArgumentException("The destination is too short.", nameof(destination));
        }

        var values = destination[..CoilCount];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = (RawValues.Span[i / 8] & (1 << (i % 8))) != 0;
        }
    }

    /// <summary>Tries to parse one packet from the start of the span. Trailing bytes are accepted.</summary>
    /// <remarks>
    /// Validates the function, quantity (1 to 1968), byte count, and payload length. Address ranges and unused padding bits
    /// are not validated. Payload bytes are copied into owned memory.
    /// On incomplete or invalid input, returns false with a default packet and zero bytes consumed.
    /// </remarks>
    /// <param name="source">The input bytes, beginning at the packet header.</param>
    /// <param name="packet">The parsed packet, or the default value when parsing fails.</param>
    /// <param name="bytesConsumed">The length of the parsed packet in bytes, or zero when parsing fails.</param>
    /// <returns><see langword="true" /> if one complete packet was parsed; otherwise, <see langword="false" />.</returns>
    public static bool TryParse(ReadOnlySpan<byte> source, out MultiCoilWrite packet, out int bytesConsumed)
    {
        packet = default;
        bytesConsumed = 0;
        if (source.Length < HeaderLength)
        {
            return false;
        }

        var quantity = BinaryPrimitives.ReadUInt16BigEndian(source[3..]);
        var byteCount = source[5];
        if ((ModbusFunctionCode)source[0] != ModbusFunctionCode.WriteMultipleCoils
            || quantity is 0 or > ModbusConstants.MaxWriteBitsPerPacket
            || byteCount != ModbusPayload.GetPackedBitByteCount(quantity) || source.Length < HeaderLength + byteCount)
        {
            return false;
        }

        bytesConsumed = HeaderLength + byteCount;
        packet = new MultiCoilWrite(source[..bytesConsumed]);
        return true;
    }

    /// <summary>Tries to parse one packet from the start of the sequence. Trailing bytes are accepted.</summary>
    /// <remarks>
    /// Validates the function, quantity (1 to 1968), byte count, and payload length. Address ranges and unused padding bits
    /// are not validated. Payload bytes are copied into owned memory.
    /// On incomplete or invalid input, returns false with a default packet and zero bytes consumed.
    /// </remarks>
    /// <param name="source">The input bytes, beginning at the packet header.</param>
    /// <param name="packet">The parsed packet, or the default value when parsing fails.</param>
    /// <param name="bytesConsumed">The length of the parsed packet in bytes, or zero when parsing fails.</param>
    /// <returns><see langword="true" /> if one complete packet was parsed; otherwise, <see langword="false" />.</returns>
    public static bool TryParse(ReadOnlySequence<byte> source, out MultiCoilWrite packet, out int bytesConsumed)
        => IPacketStruct<MultiCoilWrite>.TryParse(source, out packet, out bytesConsumed);
}
