using System.Buffers;
using System.Buffers.Binary;

namespace SmodbusTcp;

/// <summary>A multiple-register-write request with owned raw payload bytes.</summary>
/// <remarks>
/// Create instances with <see cref="TryParse(ReadOnlySpan{byte}, out MultiRegisterWrite, out int)" />.
/// A default instance contains no values and is not a valid write request.
/// </remarks>
public readonly struct MultiRegisterWrite : IPacketStruct<MultiRegisterWrite>
{
    /// <summary>The fixed header length, including the function and byte count.</summary>
    public static int HeaderLength => 6;

    static int IPacketStruct<MultiRegisterWrite>.Length => HeaderLength;
    static bool IPacketStruct<MultiRegisterWrite>.IsVariableLength => true;

    /// <summary>Gets the function code from the request.</summary>
    public ModbusFunctionCode Function { get; }

    /// <summary>Gets the starting address of the request.</summary>
    public ushort FirstAddress { get; }

    /// <summary>Gets the number of registers to write.</summary>
    public ushort RegisterCount { get; }

    /// <summary>Gets the length of the encoded values in bytes.</summary>
    public byte ByteCount { get; }

    /// <summary>Gets the big-endian register values in wire order.</summary>
    public ReadOnlyMemory<byte> RawValues { get; }

    private MultiRegisterWrite(ReadOnlySpan<byte> source)
    {
        Function = (ModbusFunctionCode)source[0];
        FirstAddress = BinaryPrimitives.ReadUInt16BigEndian(source[1..]);
        RegisterCount = BinaryPrimitives.ReadUInt16BigEndian(source[3..]);
        ByteCount = source[5];
        RawValues = source[HeaderLength..].ToArray();
    }

    /// <summary>Decodes the raw payload into a new array in address order.</summary>
    /// <returns>A newly allocated array containing the decoded values in ascending address order.</returns>
    public ushort[] GetValues()
    {
        var values = new ushort[RegisterCount];
        CopyValuesTo(values);
        return values;
    }

    /// <summary>Decodes the raw payload into the destination span in address order.</summary>
    /// <param name="destination">
    /// The span that receives exactly <see cref="RegisterCount" /> values; any remaining elements
    /// are unchanged.
    /// </param>
    /// <exception cref="ArgumentException">The destination is shorter than <see cref="RegisterCount" />.</exception>
    public void CopyValuesTo(Span<ushort> destination)
    {
        if (destination.Length < RegisterCount)
        {
            throw new ArgumentException("The destination is too short.", nameof(destination));
        }

        var values = destination[..RegisterCount];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = BinaryPrimitives.ReadUInt16BigEndian(RawValues.Span[(i * 2)..]);
        }
    }

    /// <summary>Tries to parse one packet from the start of the span. Trailing bytes are accepted.</summary>
    /// <remarks>
    /// Validates the function, quantity (1 to 123), byte count, and payload length. Address ranges are not validated. Payload
    /// bytes are copied into owned memory.
    /// On incomplete or invalid input, returns false with a default packet and zero bytes consumed.
    /// </remarks>
    /// <param name="source">The input bytes, beginning at the packet header.</param>
    /// <param name="packet">The parsed packet, or the default value when parsing fails.</param>
    /// <param name="bytesConsumed">The length of the parsed packet in bytes, or zero when parsing fails.</param>
    /// <returns><see langword="true" /> if one complete packet was parsed; otherwise, <see langword="false" />.</returns>
    public static bool TryParse(ReadOnlySpan<byte> source, out MultiRegisterWrite packet, out int bytesConsumed)
    {
        packet = default;
        bytesConsumed = 0;
        if (source.Length < HeaderLength)
        {
            return false;
        }

        var quantity = BinaryPrimitives.ReadUInt16BigEndian(source[3..]);
        var byteCount = source[5];
        if ((ModbusFunctionCode)source[0] != ModbusFunctionCode.WriteMultipleRegisters
            || quantity is 0 or > ModbusConstants.MaxWriteRegistersPerPacket
            || byteCount != ModbusPayload.GetRegisterByteCount(quantity) || source.Length < HeaderLength + byteCount)
        {
            return false;
        }

        bytesConsumed = HeaderLength + byteCount;
        packet = new MultiRegisterWrite(source[..bytesConsumed]);
        return true;
    }

    /// <summary>Tries to parse one packet from the start of the sequence. Trailing bytes are accepted.</summary>
    /// <remarks>
    /// Validates the function, quantity (1 to 123), byte count, and payload length. Address ranges are not validated. Payload
    /// bytes are copied into owned memory.
    /// On incomplete or invalid input, returns false with a default packet and zero bytes consumed.
    /// </remarks>
    /// <param name="source">The input bytes, beginning at the packet header.</param>
    /// <param name="packet">The parsed packet, or the default value when parsing fails.</param>
    /// <param name="bytesConsumed">The length of the parsed packet in bytes, or zero when parsing fails.</param>
    /// <returns><see langword="true" /> if one complete packet was parsed; otherwise, <see langword="false" />.</returns>
    public static bool TryParse(ReadOnlySequence<byte> source, out MultiRegisterWrite packet, out int bytesConsumed)
        => IPacketStruct<MultiRegisterWrite>.TryParse(source, out packet, out bytesConsumed);
}
