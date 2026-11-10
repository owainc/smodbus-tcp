namespace SmodbusTcp;

/// <summary>
/// Defines the response codes used in Modbus communication to indicate the status of a request.
/// </summary>
public enum ModbusResponseCode : byte
{
    /// <summary>
    /// Indicates successful handling; the server sends a normal response instead of an exception response.
    /// </summary>
    Ok = 0x00,

    /// <summary>
    /// The function code received in the query is not an allowable action for the server.
    /// </summary>
    IllegalFunction = 0x01,

    /// <summary>
    /// The data address received in the query is not an allowable address for the server.
    /// </summary>
    IllegalDataAddress = 0x02,

    /// <summary>
    /// A value contained in the query data field is not an allowable value for the server.
    /// </summary>
    IllegalDataValue = 0x03,

    /// <summary>
    /// An unrecoverable error occurred while the server was attempting to perform the requested action.
    /// </summary>
    SlaveDeviceFailure = 0x04
}
