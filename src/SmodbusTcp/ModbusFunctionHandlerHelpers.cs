namespace SmodbusTcp;

/// <summary>Provides reusable validation and dispatch helpers for Modbus function handlers.</summary>
public static class ModbusFunctionHandlerHelpers
{
    /// <summary>Validates a read request and its response payload before returning the handler result.</summary>
    /// <remarks>
    /// A successful response must contain exactly the expected payload bytes. The writer is completed after
    /// the handler finishes, including when it throws. Invalid counts return
    /// <see cref="ModbusResponseCode.IllegalDataValue" /> without invoking the handler or completing the writer. Invalid
    /// successful payload lengths return
    /// <see cref="ModbusResponseCode.SlaveDeviceFailure" /> after invoking the optional diagnostic callback.
    /// </remarks>
    /// <param name="functionCode">The read function being handled.</param>
    /// <param name="registerCount">The requested number of bits or registers.</param>
    /// <param name="responseWriter">The writer for the response payload.</param>
    /// <param name="handler">The callback that writes the payload and returns the response code.</param>
    /// <param name="onInvalidResponseLength">
    /// An optional callback for an invalid successful payload length. If it throws, the
    /// exception propagates.
    /// </param>
    /// <returns>A task containing <see cref="ModbusResponseCode.Ok" /> on success, or a Modbus exception response code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="responseWriter" /> or <paramref name="handler" /> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="functionCode" /> is not a supported read function.</exception>
    public static Task<ModbusResponseCode> HandleRead(
        ModbusFunctionCode functionCode,
        ushort registerCount,
        ModbusResponseWriter responseWriter,
        Func<Task<ModbusResponseCode>> handler,
        Action<InvalidModbusResponseLengthException>? onInvalidResponseLength = null)
    {
        ArgumentNullException.ThrowIfNull(responseWriter);
        ArgumentNullException.ThrowIfNull(handler);

        var (maximumCount, expectedLength) = functionCode switch
        {
            ModbusFunctionCode.ReadCoils or ModbusFunctionCode.ReadDiscreteInputs =>
                (ModbusConstants.MaxBitsPerPacket, ModbusPayload.GetPackedBitByteCount(registerCount)),
            ModbusFunctionCode.ReadHoldingRegisters or ModbusFunctionCode.ReadInputRegisters =>
                (ModbusConstants.MaxRegistersPerPacket, ModbusPayload.GetRegisterByteCount(registerCount)),
            _ => throw new ArgumentOutOfRangeException(nameof(functionCode), functionCode,
                "The function code must identify a read operation.")
        };

        if (registerCount == 0 || registerCount > maximumCount)
        {
            return Task.FromResult(ModbusResponseCode.IllegalDataValue);
        }

        var start = responseWriter.WrittenCount;
        return ValidateReadResponse(functionCode, responseWriter, start, expectedLength, handler,
            onInvalidResponseLength);
    }

    /// <summary>Validates and dispatches a single-coil write using the encoded Modbus value.</summary>
    /// <remarks>Invalid encoded values return <see cref="ModbusResponseCode.IllegalDataValue" />.</remarks>
    /// <param name="unitId">The unit identifier.</param>
    /// <param name="address">The coil address.</param>
    /// <param name="value">The encoded coil state: 0xFF00 for on or 0x0000 for off.</param>
    /// <param name="handler">The callback that writes the decoded coil value.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>A task containing <see cref="ModbusResponseCode.Ok" /> on success, or a Modbus exception response code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler" /> is null.</exception>
    public static Task<ModbusResponseCode> HandleWriteSingleCoil(
        byte unitId,
        ushort address,
        ushort value,
        Func<byte, ushort, bool, CancellationToken, Task<ModbusResponseCode>> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return !value.IsValidModbusCoilValue
            ? Task.FromResult(ModbusResponseCode.IllegalDataValue)
            : handler(unitId, address, value.IsModbusCoilOn, cancellationToken);
    }

    /// <summary>Validates and dispatches a multiple-coil write packet.</summary>
    /// <remarks>An empty packet returns <see cref="ModbusResponseCode.IllegalDataValue" />.</remarks>
    /// <param name="unitId">The unit identifier.</param>
    /// <param name="packet">The validated packet to handle.</param>
    /// <param name="handler">The callback that writes the decoded coil values.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>A task containing <see cref="ModbusResponseCode.Ok" /> on success, or a Modbus exception response code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler" /> is null.</exception>
    public static Task<ModbusResponseCode> HandleWriteMultipleCoils(
        byte unitId,
        MultiCoilWrite packet,
        Func<byte, ushort, ReadOnlyMemory<bool>, CancellationToken, Task<ModbusResponseCode>> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return packet.CoilCount == 0
            ? Task.FromResult(ModbusResponseCode.IllegalDataValue)
            : handler(unitId, packet.FirstAddress, packet.GetValues(), cancellationToken);
    }

    /// <summary>Validates and dispatches a multiple-register write packet.</summary>
    /// <remarks>An empty packet returns <see cref="ModbusResponseCode.IllegalDataValue" />.</remarks>
    /// <param name="unitId">The unit identifier.</param>
    /// <param name="packet">The validated packet to handle.</param>
    /// <param name="handler">The callback that writes the decoded register values.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>A task containing <see cref="ModbusResponseCode.Ok" /> on success, or a Modbus exception response code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler" /> is null.</exception>
    public static Task<ModbusResponseCode> HandleWriteMultipleRegisters(
        byte unitId,
        MultiRegisterWrite packet,
        Func<byte, ushort, ReadOnlyMemory<ushort>, CancellationToken, Task<ModbusResponseCode>> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return packet.RegisterCount == 0
            ? Task.FromResult(ModbusResponseCode.IllegalDataValue)
            : handler(unitId, packet.FirstAddress, packet.GetValues(), cancellationToken);
    }

    /// <summary>Writes coils sequentially, stopping at the first non-success response.</summary>
    /// <remarks>
    /// The full address range is checked before any writes. Earlier successful writes are not rolled back.
    /// </remarks>
    /// <param name="unitId">The unit identifier.</param>
    /// <param name="firstAddress">The first coil address.</param>
    /// <param name="values">The coil values to write.</param>
    /// <param name="writeSingle">The callback that writes one coil.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>
    /// A task containing <see cref="ModbusResponseCode.Ok" /> if all writes succeed or the input is empty,
    /// <see cref="ModbusResponseCode.IllegalDataAddress" /> if the range exceeds address 65535, or the first failing callback
    /// result.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="writeSingle" /> is null.</exception>
    /// <exception cref="OperationCanceledException">
    /// Cancellation is requested before a write, or the callback reports
    /// cancellation.
    /// </exception>
    public static Task<ModbusResponseCode> HandleAsSingleWrites(
        byte unitId,
        ushort firstAddress,
        ReadOnlyMemory<bool> values,
        Func<byte, ushort, bool, CancellationToken, Task<ModbusResponseCode>> writeSingle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writeSingle);

        return HandleAsSingleWrites(firstAddress, values.Length,
            (address, index) => writeSingle(unitId, address, values.Span[index], cancellationToken),
            cancellationToken);
    }

    /// <summary>Writes registers sequentially, stopping at the first non-success response.</summary>
    /// <remarks>
    /// The full address range is checked before any writes. Register bit patterns are preserved, and earlier
    /// successful writes are not rolled back.
    /// </remarks>
    /// <param name="unitId">The unit identifier.</param>
    /// <param name="firstAddress">The first register address.</param>
    /// <param name="values">The register values to write.</param>
    /// <param name="writeSingle">The callback that writes one register.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>
    /// A task containing <see cref="ModbusResponseCode.Ok" /> if all writes succeed or the input is empty,
    /// <see cref="ModbusResponseCode.IllegalDataAddress" /> if the range exceeds address 65535, or the first failing callback
    /// result.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="writeSingle" /> is null.</exception>
    /// <exception cref="OperationCanceledException">
    /// Cancellation is requested before a write, or the callback reports
    /// cancellation.
    /// </exception>
    public static Task<ModbusResponseCode> HandleAsSingleWrites(
        byte unitId,
        ushort firstAddress,
        ReadOnlyMemory<ushort> values,
        Func<byte, ushort, ushort, CancellationToken, Task<ModbusResponseCode>> writeSingle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writeSingle);

        return HandleAsSingleWrites(firstAddress, values.Length,
            (address, index) => writeSingle(unitId, address, values.Span[index], cancellationToken),
            cancellationToken);
    }

    private static async Task<ModbusResponseCode> ValidateReadResponse(
        ModbusFunctionCode functionCode,
        ModbusResponseWriter responseWriter,
        int start,
        int expectedLength,
        Func<Task<ModbusResponseCode>> handler,
        Action<InvalidModbusResponseLengthException>? onInvalidResponseLength)
    {
        try
        {
            var response = await handler().ConfigureAwait(false);
            var actualLength = responseWriter.WrittenCount - start;
            if (response == ModbusResponseCode.Ok && actualLength != expectedLength)
            {
                onInvalidResponseLength?.Invoke(
                    new InvalidModbusResponseLengthException(functionCode, expectedLength, actualLength));
                return ModbusResponseCode.SlaveDeviceFailure;
            }

            return response;
        }
        finally
        {
            responseWriter.Complete();
        }
    }

    private static async Task<ModbusResponseCode> HandleAsSingleWrites(
        ushort firstAddress,
        int count,
        Func<ushort, int, Task<ModbusResponseCode>> writeAtAddress,
        CancellationToken cancellationToken)
    {
        if (firstAddress + count > ModbusConstants.AddressSpaceSize)
        {
            return ModbusResponseCode.IllegalDataAddress;
        }

        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await writeAtAddress((ushort)(firstAddress + i), i).ConfigureAwait(false);
            if (response != ModbusResponseCode.Ok)
            {
                return response;
            }
        }

        return ModbusResponseCode.Ok;
    }
}
