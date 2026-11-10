using System.Buffers;
using System.Buffers.Binary;

namespace SmodbusTcp;

/// <summary>
/// Represents the function, address, and 16-bit value shared by read and single-write requests.
/// </summary>
public readonly struct AddressValueRequest : IPacketStruct<AddressValueRequest>
{
    /// <summary>Gets the fixed request length in bytes.</summary>
    public static int Length { get; } = 5;

    private AddressValueRequest(ModbusFunctionCode function, ushort firstAddress, ushort value)
    {
        Function = function;
        FirstAddress = firstAddress;
        Value = value;
    }

    /// <summary>Gets the function code from the request.</summary>
    public ModbusFunctionCode Function { get; }

    /// <summary>Gets the starting address of the request.</summary>
    public ushort FirstAddress { get; }

    /// <summary>Gets the requested quantity for reads or the encoded value for single writes.</summary>
    public ushort Value { get; }

    /// <summary>Tries to parse one packet from the start of the span. Trailing bytes are accepted.</summary>
    /// <remarks>
    /// Only the five-byte structure is checked; function codes, quantities, coil encodings, and address ranges are not
    /// validated.
    /// On incomplete or invalid input, returns false with a default packet and zero bytes consumed.
    /// </remarks>
    /// <param name="source">The input bytes, beginning at the packet header.</param>
    /// <param name="packet">The parsed packet, or the default value when parsing fails.</param>
    /// <param name="bytesConsumed">The length of the parsed packet in bytes, or zero when parsing fails.</param>
    /// <returns><see langword="true" /> if one complete packet was parsed; otherwise, <see langword="false" />.</returns>
    public static bool TryParse(ReadOnlySpan<byte> source, out AddressValueRequest packet, out int bytesConsumed)
    {
        packet = default;
        bytesConsumed = 0;
        if (source.Length < Length)
        {
            return false;
        }

        packet = new AddressValueRequest((ModbusFunctionCode)source[0],
            BinaryPrimitives.ReadUInt16BigEndian(source[1..]),
            BinaryPrimitives.ReadUInt16BigEndian(source[3..]));
        bytesConsumed = Length;
        return true;
    }

    /// <summary>Tries to parse one packet from the start of the sequence. Trailing bytes are accepted.</summary>
    /// <remarks>
    /// Only the five-byte structure is checked; function codes, quantities, coil encodings, and address ranges are not
    /// validated.
    /// On incomplete or invalid input, returns false with a default packet and zero bytes consumed.
    /// </remarks>
    /// <param name="source">The input bytes, beginning at the packet header.</param>
    /// <param name="packet">The parsed packet, or the default value when parsing fails.</param>
    /// <param name="bytesConsumed">The length of the parsed packet in bytes, or zero when parsing fails.</param>
    /// <returns><see langword="true" /> if one complete packet was parsed; otherwise, <see langword="false" />.</returns>
    public static bool TryParse(ReadOnlySequence<byte> source, out AddressValueRequest packet, out int bytesConsumed)
        => IPacketStruct<AddressValueRequest>.TryParse(source, out packet, out bytesConsumed);
}
