namespace SmodbusTcp;

/// <summary>Provides default dispatch and overridable hooks for Modbus function requests.</summary>
/// <remarks>
/// Unimplemented hooks return <see cref="ModbusResponseCode.IllegalFunction" />. Interface dispatch validates
/// successful read payload lengths and calls <see cref="OnInvalidResponseLength" /> for an invalid length before
/// completing the response writer and returning <see cref="ModbusResponseCode.SlaveDeviceFailure" />.
/// Read hooks must write exactly <see cref="ModbusPayload.GetPackedBitByteCount" /> bytes for bits or registerCount
/// big-endian
/// ushort values (2 bytes each) for registers when returning Ok. Hooks must validate device address ranges and
/// support concurrent calls from different connections. Exceptions from hooks propagate to the caller.
/// </remarks>
public abstract class ModbusFunctionHandlerBase : IModbusFunctionHandler
{
    /// <inheritdoc />
    Task<ModbusResponseCode> IModbusFunctionHandler.ReadCoils(byte unitId, ushort firstAddress,
        ushort registerCount,
        ModbusResponseWriter responseWriter, CancellationToken cancellationToken)
        => HandleReadCoils(unitId, firstAddress, registerCount, responseWriter, cancellationToken);

    /// <inheritdoc />
    Task<ModbusResponseCode> IModbusFunctionHandler.ReadDiscreteInputs(byte unitId, ushort firstAddress,
        ushort registerCount,
        ModbusResponseWriter responseWriter, CancellationToken cancellationToken)
        => HandleReadDiscreteInputs(unitId, firstAddress, registerCount, responseWriter, cancellationToken);

    /// <inheritdoc />
    Task<ModbusResponseCode> IModbusFunctionHandler.ReadInputRegisters(byte unitId, ushort firstAddress,
        ushort registerCount,
        ModbusResponseWriter responseWriter, CancellationToken cancellationToken)
        => HandleReadInputRegisters(unitId, firstAddress, registerCount, responseWriter, cancellationToken);

    /// <inheritdoc />
    Task<ModbusResponseCode> IModbusFunctionHandler.ReadHoldingRegisters(byte unitId, ushort firstAddress,
        ushort registerCount,
        ModbusResponseWriter responseWriter, CancellationToken cancellationToken)
        => HandleReadHoldingRegisters(unitId, firstAddress, registerCount, responseWriter, cancellationToken);

    /// <inheritdoc />
    Task<ModbusResponseCode> IModbusFunctionHandler.WriteSingleCoil(byte unitId, ushort address, ushort value,
        CancellationToken cancellationToken)
        => HandleWriteSingleCoil(unitId, address, value, cancellationToken);

    /// <inheritdoc />
    Task<ModbusResponseCode> IModbusFunctionHandler.WriteSingleRegister(byte unitId, ushort address, ushort value,
        CancellationToken cancellationToken)
        => HandleWriteSingleRegister(unitId, address, value, cancellationToken);

    /// <inheritdoc />
    Task<ModbusResponseCode> IModbusFunctionHandler.WriteMultipleCoils(byte unitId, MultiCoilWrite packet,
        CancellationToken cancellationToken)
        => HandleWriteMultipleCoils(unitId, packet, cancellationToken);

    /// <inheritdoc />
    Task<ModbusResponseCode> IModbusFunctionHandler.WriteMultipleRegisters(byte unitId, MultiRegisterWrite packet,
        CancellationToken cancellationToken)
        => HandleWriteMultipleRegisters(unitId, packet, cancellationToken);

    /// <summary>Reports an invalid successful read payload. The default implementation does nothing.</summary>
    /// <remarks>
    /// After this method returns, the base handler returns <see cref="ModbusResponseCode.SlaveDeviceFailure" />
    /// and the server discards the invalid payload. Override to log, record diagnostics, or throw the exception.
    /// </remarks>
    /// <param name="exception">The exception describing the expected and actual payload lengths.</param>
    protected virtual void OnInvalidResponseLength(InvalidModbusResponseLengthException exception)
    {
    }

    /// <summary>Validates and dispatches a read-coils request.</summary>
    /// <param name="unitId">The unit identifier supplied by the client.</param>
    /// <param name="firstAddress">The zero-based address of the first item.</param>
    /// <param name="registerCount">The number of coils or discrete inputs to read (1 to 2000).</param>
    /// <param name="responseWriter">The response payload writer; finish writing before the returned task completes.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task containing <see cref="ModbusResponseCode.Ok" /> on success, or a Modbus exception response code.</returns>
    protected Task<ModbusResponseCode> HandleReadCoils(byte unitId, ushort firstAddress, ushort registerCount,
        ModbusResponseWriter responseWriter, CancellationToken cancellationToken = default)
        => ModbusFunctionHandlerHelpers.HandleRead(ModbusFunctionCode.ReadCoils, registerCount, responseWriter,
            () => OnReadCoils(unitId, firstAddress, registerCount, responseWriter, cancellationToken),
            OnInvalidResponseLength);

    /// <summary>Validates and dispatches a read-discrete-inputs request.</summary>
    /// <param name="unitId">The unit identifier supplied by the client.</param>
    /// <param name="firstAddress">The zero-based address of the first item.</param>
    /// <param name="registerCount">The number of coils or discrete inputs to read (1 to 2000).</param>
    /// <param name="responseWriter">The response payload writer; finish writing before the returned task completes.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task containing <see cref="ModbusResponseCode.Ok" /> on success, or a Modbus exception response code.</returns>
    protected Task<ModbusResponseCode> HandleReadDiscreteInputs(byte unitId, ushort firstAddress, ushort registerCount,
        ModbusResponseWriter responseWriter, CancellationToken cancellationToken = default)
        => ModbusFunctionHandlerHelpers.HandleRead(ModbusFunctionCode.ReadDiscreteInputs, registerCount, responseWriter,
            () => OnReadDiscreteInputs(unitId, firstAddress, registerCount, responseWriter, cancellationToken),
            OnInvalidResponseLength);

    /// <summary>Validates and dispatches a read-input-registers request.</summary>
    /// <param name="unitId">The unit identifier supplied by the client.</param>
    /// <param name="firstAddress">The zero-based address of the first item.</param>
    /// <param name="registerCount">The number of registers to read (1 to 125).</param>
    /// <param name="responseWriter">The response payload writer; finish writing before the returned task completes.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task containing <see cref="ModbusResponseCode.Ok" /> on success, or a Modbus exception response code.</returns>
    protected Task<ModbusResponseCode> HandleReadInputRegisters(byte unitId, ushort firstAddress, ushort registerCount,
        ModbusResponseWriter responseWriter, CancellationToken cancellationToken = default)
        => ModbusFunctionHandlerHelpers.HandleRead(ModbusFunctionCode.ReadInputRegisters, registerCount, responseWriter,
            () => OnReadInputRegisters(unitId, firstAddress, registerCount, responseWriter, cancellationToken),
            OnInvalidResponseLength);

    /// <summary>Validates and dispatches a read-holding-registers request.</summary>
    /// <param name="unitId">The unit identifier supplied by the client.</param>
    /// <param name="firstAddress">The zero-based address of the first item.</param>
    /// <param name="registerCount">The number of registers to read (1 to 125).</param>
    /// <param name="responseWriter">The response payload writer; finish writing before the returned task completes.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task containing <see cref="ModbusResponseCode.Ok" /> on success, or a Modbus exception response code.</returns>
    protected Task<ModbusResponseCode> HandleReadHoldingRegisters(byte unitId, ushort firstAddress,
        ushort registerCount,
        ModbusResponseWriter responseWriter, CancellationToken cancellationToken = default)
        => ModbusFunctionHandlerHelpers.HandleRead(ModbusFunctionCode.ReadHoldingRegisters, registerCount,
            responseWriter,
            () => OnReadHoldingRegisters(unitId, firstAddress, registerCount, responseWriter, cancellationToken),
            OnInvalidResponseLength);

    /// <summary>Validates and dispatches a single-coil write.</summary>
    /// <param name="unitId">The unit identifier supplied by the client.</param>
    /// <param name="address">The zero-based address to write.</param>
    /// <param name="value">The encoded coil state: 0xFF00 for on or 0x0000 for off.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task containing <see cref="ModbusResponseCode.Ok" /> on success, or a Modbus exception response code.</returns>
    protected Task<ModbusResponseCode> HandleWriteSingleCoil(byte unitId, ushort address, ushort value,
        CancellationToken cancellationToken = default)
        => ModbusFunctionHandlerHelpers.HandleWriteSingleCoil(unitId, address, value, OnWriteSingleCoil,
            cancellationToken);

    /// <summary>Dispatches a single-register write.</summary>
    /// <param name="unitId">The unit identifier supplied by the client.</param>
    /// <param name="address">The zero-based address to write.</param>
    /// <param name="value">The unsigned 16-bit register value; all bit patterns are preserved.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task containing <see cref="ModbusResponseCode.Ok" /> on success, or a Modbus exception response code.</returns>
    protected Task<ModbusResponseCode> HandleWriteSingleRegister(byte unitId, ushort address, ushort value,
        CancellationToken cancellationToken = default)
        => OnWriteSingleRegister(unitId, address, value, cancellationToken);

    /// <summary>Validates and dispatches a multiple-coil write packet.</summary>
    /// <param name="unitId">The unit identifier supplied by the client.</param>
    /// <param name="packet">The parsed multiple-write request, which owns its payload bytes.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task containing <see cref="ModbusResponseCode.Ok" /> on success, or a Modbus exception response code.</returns>
    protected Task<ModbusResponseCode> HandleWriteMultipleCoils(byte unitId, MultiCoilWrite packet,
        CancellationToken cancellationToken = default)
        => ModbusFunctionHandlerHelpers.HandleWriteMultipleCoils(unitId, packet, OnWriteMultipleCoils,
            cancellationToken);

    /// <summary>Validates and dispatches a multiple-register write packet.</summary>
    /// <param name="unitId">The unit identifier supplied by the client.</param>
    /// <param name="packet">The parsed multiple-write request, which owns its payload bytes.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task containing <see cref="ModbusResponseCode.Ok" /> on success, or a Modbus exception response code.</returns>
    protected Task<ModbusResponseCode> HandleWriteMultipleRegisters(byte unitId, MultiRegisterWrite packet,
        CancellationToken cancellationToken = default)
        => ModbusFunctionHandlerHelpers.HandleWriteMultipleRegisters(unitId, packet, OnWriteMultipleRegisters,
            cancellationToken);

    /// <summary>Called to write a single register.</summary>
    /// <param name="unitId">The unit identifier supplied by the client.</param>
    /// <param name="address">The zero-based address to write.</param>
    /// <param name="value">The unsigned 16-bit register value; all bit patterns are preserved.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>
    /// A task containing the response code. The default implementation returns
    /// <see cref="ModbusResponseCode.IllegalFunction" />.
    /// </returns>
    protected virtual Task<ModbusResponseCode> OnWriteSingleRegister(byte unitId, ushort address, ushort value,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ModbusResponseCode.IllegalFunction);

    /// <summary>Called to read coils.</summary>
    /// <param name="unitId">The unit identifier supplied by the client.</param>
    /// <param name="firstAddress">The zero-based address of the first item.</param>
    /// <param name="registerCount">The number of coils or discrete inputs to read (1 to 2000).</param>
    /// <param name="responseWriter">The response payload writer; finish writing before the returned task completes.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>
    /// A task containing the response code. The default implementation returns
    /// <see cref="ModbusResponseCode.IllegalFunction" />.
    /// </returns>
    protected virtual Task<ModbusResponseCode> OnReadCoils(byte unitId, ushort firstAddress, ushort registerCount,
        ModbusResponseWriter responseWriter, CancellationToken cancellationToken = default)
        => Task.FromResult(ModbusResponseCode.IllegalFunction);

    /// <summary>Called to read discrete inputs.</summary>
    /// <param name="unitId">The unit identifier supplied by the client.</param>
    /// <param name="firstAddress">The zero-based address of the first item.</param>
    /// <param name="registerCount">The number of coils or discrete inputs to read (1 to 2000).</param>
    /// <param name="responseWriter">The response payload writer; finish writing before the returned task completes.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>
    /// A task containing the response code. The default implementation returns
    /// <see cref="ModbusResponseCode.IllegalFunction" />.
    /// </returns>
    protected virtual Task<ModbusResponseCode> OnReadDiscreteInputs(byte unitId, ushort firstAddress,
        ushort registerCount,
        ModbusResponseWriter responseWriter, CancellationToken cancellationToken = default)
        => Task.FromResult(ModbusResponseCode.IllegalFunction);

    /// <summary>Called to read input registers.</summary>
    /// <param name="unitId">The unit identifier supplied by the client.</param>
    /// <param name="firstAddress">The zero-based address of the first item.</param>
    /// <param name="registerCount">The number of registers to read (1 to 125).</param>
    /// <param name="responseWriter">The response payload writer; finish writing before the returned task completes.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>
    /// A task containing the response code. The default implementation returns
    /// <see cref="ModbusResponseCode.IllegalFunction" />.
    /// </returns>
    protected virtual Task<ModbusResponseCode> OnReadInputRegisters(byte unitId, ushort firstAddress,
        ushort registerCount,
        ModbusResponseWriter responseWriter, CancellationToken cancellationToken = default)
        => Task.FromResult(ModbusResponseCode.IllegalFunction);

    /// <summary>Called to read holding registers.</summary>
    /// <param name="unitId">The unit identifier supplied by the client.</param>
    /// <param name="firstAddress">The zero-based address of the first item.</param>
    /// <param name="registerCount">The number of registers to read (1 to 125).</param>
    /// <param name="responseWriter">The response payload writer; finish writing before the returned task completes.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>
    /// A task containing the response code. The default implementation returns
    /// <see cref="ModbusResponseCode.IllegalFunction" />.
    /// </returns>
    protected virtual Task<ModbusResponseCode> OnReadHoldingRegisters(byte unitId, ushort firstAddress,
        ushort registerCount,
        ModbusResponseWriter responseWriter, CancellationToken cancellationToken = default)
        => Task.FromResult(ModbusResponseCode.IllegalFunction);

    /// <summary>Called to write a single coil.</summary>
    /// <param name="unitId">The unit identifier supplied by the client.</param>
    /// <param name="address">The zero-based address to write.</param>
    /// <param name="value">The coil state, where true means on.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>
    /// A task containing the response code. The default implementation returns
    /// <see cref="ModbusResponseCode.IllegalFunction" />.
    /// </returns>
    protected virtual Task<ModbusResponseCode> OnWriteSingleCoil(byte unitId, ushort address, bool value,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ModbusResponseCode.IllegalFunction);

    /// <summary>Writes consecutive coils, starting at the specified address.</summary>
    /// <remarks>The callback receives the decoded coil values in an owned read-only memory block.</remarks>
    /// <param name="unitId">The unit identifier supplied by the client.</param>
    /// <param name="firstAddress">The zero-based address of the first item.</param>
    /// <param name="values">The values to write in ascending address order.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>
    /// A task containing the response code. The default implementation returns
    /// <see cref="ModbusResponseCode.IllegalFunction" />.
    /// </returns>
    protected virtual Task<ModbusResponseCode> OnWriteMultipleCoils(byte unitId, ushort firstAddress,
        ReadOnlyMemory<bool> values,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ModbusResponseCode.IllegalFunction);

    /// <summary>Writes consecutive unsigned 16-bit registers, starting at the specified address.</summary>
    /// <remarks>The callback receives the decoded register values in an owned read-only memory block.</remarks>
    /// <param name="unitId">The unit identifier supplied by the client.</param>
    /// <param name="firstAddress">The zero-based address of the first item.</param>
    /// <param name="values">The values to write in ascending address order.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>
    /// A task containing the response code. The default implementation returns
    /// <see cref="ModbusResponseCode.IllegalFunction" />.
    /// </returns>
    protected virtual Task<ModbusResponseCode> OnWriteMultipleRegisters(byte unitId, ushort firstAddress,
        ReadOnlyMemory<ushort> values,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ModbusResponseCode.IllegalFunction);

    /// <summary>
    /// Writes coils in order through <see cref="OnWriteSingleCoil" />, returning the first non-success response.
    /// </summary>
    /// <param name="unitId">The unit identifier supplied by the client.</param>
    /// <param name="firstAddress">The zero-based address of the first item.</param>
    /// <param name="values">The values to write in ascending address order.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task containing <see cref="ModbusResponseCode.Ok" /> on success, or a Modbus exception response code.</returns>
    /// <remarks>
    /// The full address range is checked before writing. An out-of-range request returns
    /// <see cref="ModbusResponseCode.IllegalDataAddress" />; an empty input succeeds. Earlier successful writes are not rolled
    /// back.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// Cancellation is requested before a write, or the hook reports cancellation.
    /// </exception>
    protected Task<ModbusResponseCode> HandleAsSingleWrites(byte unitId, ushort firstAddress,
        ReadOnlyMemory<bool> values,
        CancellationToken cancellationToken = default)
        => ModbusFunctionHandlerHelpers.HandleAsSingleWrites(unitId, firstAddress, values,
            OnWriteSingleCoil, cancellationToken);

    /// <summary>
    /// Writes registers in order through <see cref="OnWriteSingleRegister" />, returning the first non-success response.
    /// </summary>
    /// <param name="unitId">The unit identifier supplied by the client.</param>
    /// <param name="firstAddress">The zero-based address of the first item.</param>
    /// <param name="values">The values to write in ascending address order.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task containing <see cref="ModbusResponseCode.Ok" /> on success, or a Modbus exception response code.</returns>
    /// <remarks>
    /// The full address range is checked before writing. An out-of-range request returns
    /// <see cref="ModbusResponseCode.IllegalDataAddress" />; an empty input succeeds. Earlier successful writes are not rolled
    /// back.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// Cancellation is requested before a write, or the hook reports
    /// cancellation.
    /// </exception>
    protected Task<ModbusResponseCode> HandleAsSingleWrites(byte unitId, ushort firstAddress,
        ReadOnlyMemory<ushort> values,
        CancellationToken cancellationToken = default)
        => ModbusFunctionHandlerHelpers.HandleAsSingleWrites(unitId, firstAddress, values,
            OnWriteSingleRegister, cancellationToken);
}
