using System.Buffers;

namespace SmodbusTcp;

/// <summary>
/// Defines a packet structure that can be parsed from a byte sequence.
/// </summary>
/// <typeparam name="T"></typeparam>
internal interface IPacketStruct<T> where T : struct, IPacketStruct<T>
{
    /// <summary>
    /// Gets the length of the packet structure in bytes.
    /// </summary>
    static abstract int Length { get; }

    /// <summary>Whether parsing requires the complete packet rather than only <see cref="Length" /> bytes.</summary>
    static virtual bool IsVariableLength => false;

    /// <summary>
    /// Parses the packet structure from a byte span.
    /// </summary>
    static abstract bool TryParse(ReadOnlySpan<byte> source, out T packet, out int bytesConsumed);

    /// <summary>
    /// Tries to parse the packet structure from a read-only sequence of bytes.
    /// </summary>
    public static bool TryParse(ReadOnlySequence<byte> source, out T packet, out int bytesConsumed)
    {
        packet = default;
        bytesConsumed = 0;

        if (source.Length < T.Length)
        {
            return false;
        }

        var buffer = T.IsVariableLength ? source : source.Slice(0, T.Length);
        if (buffer.IsSingleSegment)
        {
            return T.TryParse(buffer.FirstSpan, out packet, out bytesConsumed);
        }

        var b = buffer.ToArray();
        return T.TryParse(b, out packet, out bytesConsumed);
    }
}
