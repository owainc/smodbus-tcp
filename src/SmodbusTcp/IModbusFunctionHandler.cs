namespace SmodbusTcp;

/// <summary>
/// Defines handlers for Modbus function requests.
/// </summary>
/// <remarks>
/// Each method defaults to returning <see cref="ModbusResponseCode.IllegalFunction" />.
/// Prefer inheriting from <see cref="ModbusFunctionHandlerBase" /> and overriding the protected <c>On...</c> methods.
/// </remarks>
public interface IModbusFunctionHandler
{
    /// <summary>Reads coils into <paramref name="responseWriter" />.</summary>
    /// <param name="unitId">The unit identifier supplied by the client.</param>
    /// <param name="firstAddress">The zero-based address of the first item.</param>
    /// <param name="registerCount">The number of coils or discrete inputs to read (1 to 2000).</param>
    /// <param name="responseWriter">The response payload writer; finish writing before the returned task completes.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>
    /// A task containing <see cref="ModbusResponseCode.Ok" /> on success, or a Modbus exception response code. The
    /// default implementation returns <see cref="ModbusResponseCode.IllegalFunction" />.
    /// </returns>
    Task<ModbusResponseCode> ReadCoils(byte unitId, ushort firstAddress, ushort registerCount,
        ModbusResponseWriter responseWriter, CancellationToken cancellationToken = default)
        => Task.FromResult(ModbusResponseCode.IllegalFunction);

    /// <summary>Reads discrete inputs into <paramref name="responseWriter" />.</summary>
    /// <param name="unitId">The unit identifier supplied by the client.</param>
    /// <param name="firstAddress">The zero-based address of the first item.</param>
    /// <param name="registerCount">The number of coils or discrete inputs to read (1 to 2000).</param>
    /// <param name="responseWriter">The response payload writer; finish writing before the returned task completes.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>
    /// A task containing <see cref="ModbusResponseCode.Ok" /> on success, or a Modbus exception response code. The
    /// default implementation returns <see cref="ModbusResponseCode.IllegalFunction" />.
    /// </returns>
    Task<ModbusResponseCode> ReadDiscreteInputs(byte unitId, ushort firstAddress, ushort registerCount,
        ModbusResponseWriter responseWriter, CancellationToken cancellationToken = default)
        => Task.FromResult(ModbusResponseCode.IllegalFunction);

    /// <summary>Reads input registers into <paramref name="responseWriter" />.</summary>
    /// <param name="unitId">The unit identifier supplied by the client.</param>
    /// <param name="firstAddress">The zero-based address of the first item.</param>
    /// <param name="registerCount">The number of registers to read (1 to 125).</param>
    /// <param name="responseWriter">The response payload writer; finish writing before the returned task completes.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>
    /// A task containing <see cref="ModbusResponseCode.Ok" /> on success, or a Modbus exception response code. The
    /// default implementation returns <see cref="ModbusResponseCode.IllegalFunction" />.
    /// </returns>
    Task<ModbusResponseCode> ReadInputRegisters(byte unitId, ushort firstAddress, ushort registerCount,
        ModbusResponseWriter responseWriter, CancellationToken cancellationToken = default)
        => Task.FromResult(ModbusResponseCode.IllegalFunction);

    /// <summary>Reads holding registers into <paramref name="responseWriter" />.</summary>
    /// <param name="unitId">The unit identifier supplied by the client.</param>
    /// <param name="firstAddress">The zero-based address of the first item.</param>
    /// <param name="registerCount">The number of registers to read (1 to 125).</param>
    /// <param name="responseWriter">The response payload writer; finish writing before the returned task completes.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>
    /// A task containing <see cref="ModbusResponseCode.Ok" /> on success, or a Modbus exception response code. The
    /// default implementation returns <see cref="ModbusResponseCode.IllegalFunction" />.
    /// </returns>
    Task<ModbusResponseCode> ReadHoldingRegisters(byte unitId, ushort firstAddress, ushort registerCount,
        ModbusResponseWriter responseWriter, CancellationToken cancellationToken = default)
        => Task.FromResult(ModbusResponseCode.IllegalFunction);

    /// <summary>Writes the coil at the specified address.</summary>
    /// <param name="unitId">The unit identifier supplied by the client.</param>
    /// <param name="address">The zero-based address to write.</param>
    /// <param name="value">The encoded coil state: 0xFF00 for on or 0x0000 for off.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>
    /// A task containing <see cref="ModbusResponseCode.Ok" /> on success, or a Modbus exception response code. The
    /// default implementation returns <see cref="ModbusResponseCode.IllegalFunction" />.
    /// </returns>
    Task<ModbusResponseCode> WriteSingleCoil(byte unitId, ushort address, ushort value,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ModbusResponseCode.IllegalFunction);

    /// <summary>Writes the register at the specified address.</summary>
    /// <param name="unitId">The unit identifier supplied by the client.</param>
    /// <param name="address">The zero-based address to write.</param>
    /// <param name="value">The unsigned 16-bit register value; all bit patterns are preserved.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>
    /// A task containing <see cref="ModbusResponseCode.Ok" /> on success, or a Modbus exception response code. The
    /// default implementation returns <see cref="ModbusResponseCode.IllegalFunction" />.
    /// </returns>
    Task<ModbusResponseCode> WriteSingleRegister(byte unitId, ushort address, ushort value,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ModbusResponseCode.IllegalFunction);

    /// <summary>Writes coils from a validated packet.</summary>
    /// <remarks>
    /// Values are packed least-significant bit first. The packet owns its payload bytes and may be retained.
    /// </remarks>
    /// <param name="unitId">The unit identifier supplied by the client.</param>
    /// <param name="packet">The parsed multiple-write request, which owns its payload bytes.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>
    /// A task containing <see cref="ModbusResponseCode.Ok" /> on success, or a Modbus exception response code. The
    /// default implementation returns <see cref="ModbusResponseCode.IllegalFunction" />.
    /// </returns>
    Task<ModbusResponseCode> WriteMultipleCoils(byte unitId, MultiCoilWrite packet,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ModbusResponseCode.IllegalFunction);

    /// <summary>Writes registers from a validated packet.</summary>
    /// <remarks>
    /// Values are encoded big-endian. The packet owns its payload bytes and may be retained.
    /// </remarks>
    /// <param name="unitId">The unit identifier supplied by the client.</param>
    /// <param name="packet">The parsed multiple-write request, which owns its payload bytes.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>
    /// A task containing <see cref="ModbusResponseCode.Ok" /> on success, or a Modbus exception response code. The
    /// default implementation returns <see cref="ModbusResponseCode.IllegalFunction" />.
    /// </returns>
    Task<ModbusResponseCode> WriteMultipleRegisters(byte unitId, MultiRegisterWrite packet,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ModbusResponseCode.IllegalFunction);
}
