using System.Buffers;
using System.Buffers.Binary;

namespace SmodbusTcp;

/// <summary>
/// Represents the Modbus Application Protocol (MBAP) header structure.
/// </summary>
public readonly struct ModbusApplicationHeader : IPacketStruct<ModbusApplicationHeader>
{
    /// <summary>
    /// Gets the length of the Modbus Application Protocol (MBAP) header in bytes.
    /// </summary>
    public static int Length { get; } = 7;

    private const int MaxPduLength = 253;

    private ModbusApplicationHeader(ushort transactionId, ushort pduLength, byte unitId)
    {
        TransactionId = transactionId;
        ProtocolId = 0;
        PduLength = pduLength;
        UnitId = unitId;
    }

    /// <summary>
    /// Gets the transaction identifier for the Modbus Application Protocol (MBAP) header.
    /// </summary>
    public ushort TransactionId { get; }

    /// <summary>
    /// Gets the protocol identifier for the Modbus Application Protocol (MBAP) header.
    /// </summary>
    public ushort ProtocolId { get; }

    /// <summary>
    /// Gets the PDU length in bytes, including the function code and excluding the unit identifier.
    /// </summary>
    public ushort PduLength { get; }

    /// <summary>
    /// Gets the unit identifier for the Modbus Application Protocol (MBAP) header.
    /// </summary>
    public byte UnitId { get; }

    /// <summary>
    /// Tries to write the Modbus Application Protocol (MBAP) header to the specified destination span.
    /// </summary>
    /// <param name="destination">The span that receives the header; it must be at least <see cref="Length" /> bytes.</param>
    /// <param name="responseLength">The response PDU length in bytes (function code plus data); valid values are 1 to 253.</param>
    /// <param name="bytesWritten">The number of bytes written, or zero when this method returns false.</param>
    /// <returns>
    /// <see langword="false" /> if the destination is too small or <paramref name="responseLength" /> is out of range.
    /// </returns>
    public bool TryWriteResponse(Span<byte> destination, ushort responseLength, out int bytesWritten)
    {
        bytesWritten = 0;
        if (destination.Length < Length || responseLength is < 1 or > MaxPduLength)
        {
            return false;
        }

        BinaryPrimitives.WriteUInt16BigEndian(destination[bytesWritten..], TransactionId);
        bytesWritten += 2;

        BinaryPrimitives.WriteUInt16BigEndian(destination[bytesWritten..], ProtocolId);
        bytesWritten += 2;

        BinaryPrimitives.WriteUInt16BigEndian(destination[bytesWritten..], (ushort)(responseLength + 1));
        bytesWritten += 2;

        destination[bytesWritten] = UnitId;
        bytesWritten++;

        return true;
    }

    /// <summary>Tries to parse one packet from the start of the span. Trailing bytes are accepted.</summary>
    /// <remarks>
    /// Requires protocol identifier zero and a wire length of 2 to 254 bytes. The PDU itself need not be present.
    /// On incomplete or invalid input, returns <see langword="false" /> with a default packet and zero bytes consumed.
    /// </remarks>
    /// <param name="source">The input bytes, beginning at the packet header.</param>
    /// <param name="packet">The parsed packet, or the default value when parsing fails.</param>
    /// <param name="bytesConsumed">The length of the parsed packet in bytes, or zero when parsing fails.</param>
    /// <returns><see langword="true" /> if one complete packet was parsed; otherwise, <see langword="false" />.</returns>
    public static bool TryParse(ReadOnlySpan<byte> source, out ModbusApplicationHeader packet, out int bytesConsumed)
    {
        packet = default;
        bytesConsumed = 0;
        if (source.Length < Length || BinaryPrimitives.ReadUInt16BigEndian(source[2..]) != 0)
        {
            return false;
        }

        var wireLength = BinaryPrimitives.ReadUInt16BigEndian(source[4..]);
        if (wireLength is < 2 or > 254)
        {
            return false;
        }

        packet = new ModbusApplicationHeader(BinaryPrimitives.ReadUInt16BigEndian(source), (ushort)(wireLength - 1),
            source[6]);
        bytesConsumed = Length;
        return true;
    }

    /// <summary>Tries to parse one packet from the start of the sequence. Trailing bytes are accepted.</summary>
    /// <remarks>
    /// Requires protocol identifier zero and a wire length of 2 to 254 bytes. The PDU itself need not be present.
    /// On incomplete or invalid input, returns <see langword="false" /> with a default packet and zero bytes consumed.
    /// </remarks>
    /// <param name="source">The input bytes, beginning at the packet header.</param>
    /// <param name="packet">The parsed packet, or the default value when parsing fails.</param>
    /// <param name="bytesConsumed">The length of the parsed packet in bytes, or zero when parsing fails.</param>
    /// <returns><see langword="true" /> if one complete packet was parsed; otherwise, <see langword="false" />.</returns>
    public static bool TryParse(ReadOnlySequence<byte> source, out ModbusApplicationHeader packet,
        out int bytesConsumed)
        => IPacketStruct<ModbusApplicationHeader>.TryParse(source, out packet, out bytesConsumed);
}
