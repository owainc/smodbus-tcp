namespace SmodbusTcp;

/// <summary>
/// Defines the Modbus function codes used in Modbus communication.
/// </summary>
public enum ModbusFunctionCode : byte
{
    /// <summary>
    /// Modbus Read Coils function code (0x01).
    /// </summary>
    ReadCoils = 01,

    /// <summary>
    /// Modbus Read Discrete Inputs function code (0x02).
    /// </summary>
    ReadDiscreteInputs = 02,

    /// <summary>
    /// Modbus Read Holding Registers function code (0x03).
    /// </summary>
    ReadHoldingRegisters = 03,

    /// <summary>
    /// Modbus Read Input Registers function code (0x04).
    /// </summary>
    ReadInputRegisters = 04,

    /// <summary>
    /// Modbus Write Single Coil function code (0x05).
    /// </summary>
    WriteSingleCoil = 05,

    /// <summary>
    /// Modbus Write Single Register function code (0x06).
    /// </summary>
    WriteSingleRegister = 06,

    /// <summary>
    /// Modbus Write Multiple Coils function code (0x0F).
    /// </summary>
    WriteMultipleCoils = 0x0F,

    /// <summary>
    /// Modbus Write Multiple Registers function code (0x10).
    /// </summary>
    WriteMultipleRegisters = 0x10
}

/// <summary>
/// Provides extension methods for the <see cref="ModbusFunctionCode" /> enum.
/// </summary>
public static class ModbusFunctionCodeExtensions
{
    extension(ModbusFunctionCode functionCode)
    {
        /// <summary>
        /// Determines whether the specified function code is a read operation.
        /// </summary>
        public bool IsRead
            => functionCode is ModbusFunctionCode.ReadCoils
                or ModbusFunctionCode.ReadDiscreteInputs
                or ModbusFunctionCode.ReadHoldingRegisters
                or ModbusFunctionCode.ReadInputRegisters;

        /// <summary>
        /// Determines whether the specified function code is a supported write operation.
        /// Undefined values are neither reads nor writes.
        /// </summary>
        public bool IsWrite
            => functionCode is ModbusFunctionCode.WriteSingleCoil
                or ModbusFunctionCode.WriteSingleRegister
                or ModbusFunctionCode.WriteMultipleCoils
                or ModbusFunctionCode.WriteMultipleRegisters;
    }
}
