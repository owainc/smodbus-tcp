using System.Buffers;
using System.IO.Pipelines;

namespace SmodbusTcp;

/// <summary>
/// A Modbus TCP server that processes requests on a transport and delegates to a function handler.
/// </summary>
/// <remarks>
/// This type is sealed; customize server behavior by composing an <see cref="IModbusFunctionHandler" />
/// implementation rather than deriving from the server.
/// Requests are processed sequentially per connection; multiple connections can invoke the shared handler concurrently.
/// </remarks>
public sealed class ModbusServer
{
    private readonly IModbusFunctionHandler _handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModbusServer" /> class with the specified function handler.
    /// </summary>
    /// <param name="handler">The function handler shared by all connections processed by this server.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handler" /> is null.</exception>
    public ModbusServer(IModbusFunctionHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
    }

    /// <summary>Processes Modbus requests on the specified duplex pipe. The caller owns the pipes.</summary>
    /// <remarks>
    /// Handler failures propagate to the caller; transport adapters catch and log connection failures.
    /// Raw handlers are responsible for response payload validation.
    /// If the input completes in the middle of a frame, the partial frame is discarded and the method returns without error.
    /// </remarks>
    /// <param name="pipe">The connection transport. The caller must complete both pipes after processing ends.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes when connection processing ends.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pipe" /> is null.</exception>
    /// <exception cref="InvalidDataException">The connection contains an invalid Modbus header.</exception>
    /// <exception cref="OperationCanceledException">Cancellation interrupts transport I/O or is reported by the handler.</exception>
    public async Task ProcessAsync(IDuplexPipe pipe, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pipe);
        var pipeReader = pipe.Input;

        while (true)
        {
            var readResult = await pipeReader.ReadAsync(cancellationToken).ConfigureAwait(false);
            var buffer = readResult.Buffer;

            // Keep the complete frame in the pipe until its handler has finished.
            if (buffer.Length < ModbusApplicationHeader.Length)
            {
                pipeReader.AdvanceTo(buffer.Start, buffer.End);
                if (readResult.IsCompleted || readResult.IsCanceled)
                {
                    return;
                }

                continue;
            }

            if (!ModbusApplicationHeader.TryParse(buffer, out var mbap, out _))
            {
                pipeReader.AdvanceTo(buffer.End);
                throw new InvalidDataException("Invalid Modbus application header.");
            }

            var frameLength = ModbusApplicationHeader.Length + mbap.PduLength;
            if (buffer.Length < frameLength)
            {
                pipeReader.AdvanceTo(buffer.Start, buffer.End);
                if (readResult.IsCompleted || readResult.IsCanceled)
                {
                    return;
                }

                continue;
            }

            var request = buffer.Slice(ModbusApplicationHeader.Length, mbap.PduLength);
            var requestReader = new SequenceReader<byte>(request);

            requestReader.TryRead(out var function);
            var functionCode = (ModbusFunctionCode)function;
            if (!Enum.IsDefined(functionCode))
            {
                WriteResponseException(pipe.Output, mbap, functionCode, ModbusResponseCode.IllegalFunction);
                goto NEXT_FRAME;
            }

            var responseWriter = new ArrayBufferWriter<byte>(256);
            var writer = new ModbusResponseWriter(responseWriter);
            ModbusResponseCode response;
            try
            {
                response = await HandleRequestAsync(mbap.UnitId, functionCode, request,
                    writer, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                writer.Complete();
            }

            if (response == ModbusResponseCode.Ok)
            {
                WriteResponse(pipe.Output, mbap, responseWriter.WrittenSpan);
            }
            else
            {
                WriteResponseException(pipe.Output, mbap, functionCode, response);
            }

            NEXT_FRAME:
            var consumed = buffer.GetPosition(frameLength);
            pipeReader.AdvanceTo(consumed, consumed);
            var flushResult = await pipe.Output.FlushAsync(cancellationToken).ConfigureAwait(false);
            if (flushResult.IsCompleted || flushResult.IsCanceled)
            {
                return;
            }
        }
    }

    private Task<ModbusResponseCode> HandleRequestAsync(byte unitId, ModbusFunctionCode functionCode,
        ReadOnlySequence<byte> buffer, ModbusResponseWriter writer, CancellationToken cancellationToken)
    {
        switch (functionCode)
        {
            case ModbusFunctionCode.WriteMultipleCoils:
            {
                if (!MultiCoilWrite.TryParse(buffer, out var packet, out var consumed) || consumed != buffer.Length)
                {
                    return Task.FromResult(ModbusResponseCode.IllegalDataValue);
                }

                writer.Write((byte)functionCode);
                writer.WriteUInt16BigEndian(packet.FirstAddress);
                writer.WriteUInt16BigEndian(packet.CoilCount);
                return _handler.WriteMultipleCoils(unitId, packet, cancellationToken);
            }
            case ModbusFunctionCode.WriteMultipleRegisters:
            {
                if (!MultiRegisterWrite.TryParse(buffer, out var packet, out var consumed) || consumed != buffer.Length)
                {
                    return Task.FromResult(ModbusResponseCode.IllegalDataValue);
                }

                writer.Write((byte)functionCode);
                writer.WriteUInt16BigEndian(packet.FirstAddress);
                writer.WriteUInt16BigEndian(packet.RegisterCount);
                return _handler.WriteMultipleRegisters(unitId, packet, cancellationToken);
            }
        }

        if (buffer.Length != AddressValueRequest.Length ||
            !AddressValueRequest.TryParse(buffer, out var request, out _))
        {
            return Task.FromResult(ModbusResponseCode.IllegalDataValue);
        }

        writer.Write((byte)functionCode);
        if (functionCode.IsRead)
        {
            var byteCount = functionCode is ModbusFunctionCode.ReadCoils or ModbusFunctionCode.ReadDiscreteInputs
                ? ModbusPayload.GetPackedBitByteCount(request.Value)
                : ModbusPayload.GetRegisterByteCount(request.Value);
            writer.Write((byte)byteCount);
        }
        else
        {
            // All write responses echo the address and value/quantity, without the payload.
            writer.WriteUInt16BigEndian(request.FirstAddress);
            writer.WriteUInt16BigEndian(request.Value);
        }

        return functionCode switch
        {
            ModbusFunctionCode.ReadCoils =>
                _handler.ReadCoils(unitId, request.FirstAddress, request.Value, writer, cancellationToken),
            ModbusFunctionCode.ReadDiscreteInputs =>
                _handler.ReadDiscreteInputs(unitId, request.FirstAddress, request.Value,
                    writer, cancellationToken),
            ModbusFunctionCode.ReadHoldingRegisters =>
                _handler.ReadHoldingRegisters(unitId, request.FirstAddress, request.Value,
                    writer, cancellationToken),
            ModbusFunctionCode.ReadInputRegisters =>
                _handler.ReadInputRegisters(unitId, request.FirstAddress, request.Value,
                    writer, cancellationToken),
            ModbusFunctionCode.WriteSingleCoil =>
                _handler.WriteSingleCoil(unitId, request.FirstAddress, request.Value, cancellationToken),
            ModbusFunctionCode.WriteSingleRegister =>
                _handler.WriteSingleRegister(unitId, request.FirstAddress, request.Value, cancellationToken),
            _ => Task.FromResult(ModbusResponseCode.IllegalFunction)
        };
    }

    private static void WriteResponse(PipeWriter writer, ModbusApplicationHeader mbap, ReadOnlySpan<byte> response)
    {
        var mbapLength = ModbusApplicationHeader.Length;
        var c = (ushort)(mbapLength + response.Length);

        var dest = writer.GetSpan(c);
        if (!mbap.TryWriteResponse(dest, (ushort)response.Length, out _))
        {
            throw new InvalidOperationException(
                $"Response PDU length {response.Length} is outside the supported range.");
        }

        response.CopyTo(dest.Slice(mbapLength));
        writer.Advance(c);
    }

    private static void WriteResponseException(PipeWriter writer, ModbusApplicationHeader mbap,
        ModbusFunctionCode function, ModbusResponseCode response)
    {
        var dest = writer.GetSpan(9);
        mbap.TryWriteResponse(dest, 2, out _);
        dest[7] = (byte)((byte)function | ModbusConstants.ExceptionFunctionCodeMask);
        dest[8] = (byte)response;
        writer.Advance(9);
    }
}
