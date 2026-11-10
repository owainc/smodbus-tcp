namespace SmodbusTcp;

using static ModbusConstants;

/// <summary>
/// Provides extension methods for Modbus-related functionality.
/// </summary>
public static class ModbusExtensions
{
    /// <summary>
    /// Extension methods for the <see cref="ushort" /> type to handle Modbus values.
    /// </summary>
    extension(ushort value)
    {
        /// <summary>
        /// Determines if the Modbus value represents a coil that is ON (true).
        /// </summary>
        public bool IsModbusCoilOn => value == ModbusCoilOn;

        /// <summary>
        /// Determines if the Modbus value represents a coil that is OFF (false).
        /// </summary>
        public bool IsModbusCoilOff => value == ModbusCoilOff;

        /// <summary>
        /// Determines if the Modbus value is a valid representation of a coil state (either ON or OFF).
        /// </summary>
        public bool IsValidModbusCoilValue => value.IsModbusCoilOn || value.IsModbusCoilOff;
    }
}
