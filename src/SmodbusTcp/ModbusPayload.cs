namespace SmodbusTcp;

/// <summary>Calculates data payload sizes in bytes, excluding headers and byte-count fields.</summary>
/// <remarks>
/// These helpers accept any 16-bit quantity. They do not validate protocol quantity limits; request handlers
/// enforce those limits.
/// </remarks>
public static class ModbusPayload
{
    /// <summary>Returns the bytes needed to pack the given number of coils or discrete inputs, rounding up.</summary>
    /// <param name="bitCount">The number of coils or discrete inputs, including zero.</param>
    /// <returns>The bit count divided by eight, rounded up.</returns>
    public static int GetPackedBitByteCount(ushort bitCount) => (bitCount + 7) / 8;

    /// <summary>Returns the bytes needed for the given number of 16-bit registers.</summary>
    /// <param name="registerCount">The number of 16-bit registers, including zero.</param>
    /// <returns>Twice the register count.</returns>
    public static int GetRegisterByteCount(ushort registerCount) => registerCount * sizeof(ushort);
}
