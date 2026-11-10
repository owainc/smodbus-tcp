namespace SmodbusTcp;

/// <summary>
/// Contains constants used in the Modbus protocol.
/// </summary>
public static class ModbusConstants
{
    /// <summary>
    /// The bit mask applied to a function code to indicate a Modbus exception response.
    /// </summary>
    public const byte ExceptionFunctionCodeMask = 0x80;

    /// <summary>
    /// The Modbus representation of a coil that is ON (true).
    /// </summary>
    public const ushort ModbusCoilOn = 0xFF00;

    /// <summary>
    /// The Modbus representation of a coil that is OFF (false).
    /// </summary>
    public const ushort ModbusCoilOff = 0x0000;

    /// <summary>
    /// The maximum number of registers that can be read in a single Modbus packet.
    /// </summary>
    public const int MaxRegistersPerPacket = 125;

    /// <summary>
    /// The maximum number of bits that can be read in a single Modbus packet.
    /// </summary>
    public const int MaxBitsPerPacket = 2000;

    /// <summary>The maximum number of coils in a multiple-write request.</summary>
    public const int MaxWriteBitsPerPacket = 1968;

    /// <summary>The maximum number of registers in a multiple-write request.</summary>
    public const int MaxWriteRegistersPerPacket = 123;

    /// <summary>
    /// The number of addressable Modbus data addresses (0 to 65535), as addresses are 16-bit.
    /// A range starting at <c>firstAddress</c> with <c>count</c> items is valid only if
    /// <c>firstAddress + count &lt;= AddressSpaceSize</c>.
    /// </summary>
    public const int AddressSpaceSize = 0x10000;
}
