using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;

namespace SmodbusTcp.Tests;

internal static class TestSupport
{
    public static ReadOnlySequence<byte> Sequence(params byte[] bytes)
    {
        var first = new Segment(ReadOnlyMemory<byte>.Empty);
        var last = first;
        foreach (var value in bytes)
        {
            last = last.Append(new[] { value });
        }

        return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
    }

    public static byte[] Frame(params byte[] pdu)
    {
        var bytes = new byte[7 + pdu.Length];
        bytes[1] = 42;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(4), (ushort)(pdu.Length + 1));
        bytes[6] = 1;
        pdu.CopyTo(bytes, 7);
        return bytes;
    }

    public static async Task Exchange(IModbusFunctionHandler handler, byte[] request, byte[] expected)
    {
        var input = new Pipe();
        var output = new Pipe();
        var server = new ModbusServer(handler);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var run = server.ProcessAsync(new Duplex(input.Reader, output.Writer), timeout.Token);
        try
        {
            await input.Writer.WriteAsync(request, timeout.Token);
            await input.Writer.CompleteAsync();
            await run.WaitAsync(timeout.Token);
            await output.Writer.CompleteAsync();
            var result = await output.Reader.ReadAsync(timeout.Token);
            var actual = result.Buffer.ToArray();
            output.Reader.AdvanceTo(result.Buffer.End);
            CollectionAssert.AreEqual(expected, actual,
                $"Wire response: expected {Convert.ToHexString(expected)}, got {Convert.ToHexString(actual)}");
        }
        finally
        {
            await timeout.CancelAsync();
            try
            {
                await run.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                await input.Writer.CompleteAsync();
                await input.Reader.CompleteAsync();
                await output.Writer.CompleteAsync();
                await output.Reader.CompleteAsync();
            }
        }
    }
}

internal sealed class Segment : ReadOnlySequenceSegment<byte>
{
    public Segment(ReadOnlyMemory<byte> memory)
    {
        Memory = memory;
    }

    public Segment Append(ReadOnlyMemory<byte> memory)
    {
        var next = new Segment(memory) { RunningIndex = RunningIndex + Memory.Length };
        Next = next;
        return next;
    }
}

internal sealed class Duplex(PipeReader input, PipeWriter output) : IDuplexPipe
{
    public PipeReader Input => input;
    public PipeWriter Output => output;
}

internal sealed class EmptyHandler : ModbusFunctionHandlerBase;

internal sealed class ErrorHandler : ModbusFunctionHandlerBase
{
    protected override Task<ModbusResponseCode> OnWriteMultipleRegisters(byte unitId, ushort address,
        ReadOnlyMemory<ushort> values,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ModbusResponseCode.IllegalDataAddress);
}

internal sealed class RawHandler : ModbusFunctionHandlerBase, IModbusFunctionHandler
{
    public int Calls { get; private set; }

    public ReadOnlyMemory<byte> RawValues { get; private set; }

    Task<ModbusResponseCode> IModbusFunctionHandler.WriteMultipleCoils(byte unitId, MultiCoilWrite packet,
        CancellationToken cancellationToken)
    {
        RawValues = packet.RawValues;
        Calls++;
        return Task.FromResult(ModbusResponseCode.Ok);
    }

    Task<ModbusResponseCode> IModbusFunctionHandler.WriteMultipleRegisters(byte unitId, MultiRegisterWrite packet,
        CancellationToken cancellationToken)
    {
        RawValues = packet.RawValues;
        Calls++;
        return Task.FromResult(ModbusResponseCode.Ok);
    }
}

internal class RecordingHandler : ModbusFunctionHandlerBase
{
    public ReadOnlyMemory<bool> Coils { get; private set; }
    public ReadOnlyMemory<ushort> Registers { get; private set; }
    public int Calls { get; private set; }

    protected override Task<ModbusResponseCode> OnWriteMultipleCoils(byte unitId, ushort address,
        ReadOnlyMemory<bool> values,
        CancellationToken cancellationToken = default)
    {
        Coils = values;
        Calls++;
        return Task.FromResult(ModbusResponseCode.Ok);
    }

    protected override Task<ModbusResponseCode> OnWriteMultipleRegisters(byte unitId, ushort address,
        ReadOnlyMemory<ushort> values,
        CancellationToken cancellationToken = default)
    {
        Registers = values;
        Calls++;
        return Task.FromResult(ModbusResponseCode.Ok);
    }

    protected override Task<ModbusResponseCode> OnReadCoils(byte unitId, ushort address, ushort count,
        ModbusResponseWriter writer, CancellationToken cancellationToken = default)
    {
        writer.Write(1);
        return Task.FromResult(ModbusResponseCode.Ok);
    }

    protected override Task<ModbusResponseCode> OnReadDiscreteInputs(byte unitId, ushort address, ushort count,
        ModbusResponseWriter writer, CancellationToken cancellationToken = default)
        => OnReadCoils(unitId, address, count, writer, cancellationToken);

    protected override Task<ModbusResponseCode> OnReadHoldingRegisters(byte unitId, ushort address, ushort count,
        ModbusResponseWriter writer, CancellationToken cancellationToken = default)
    {
        writer.WriteUInt16BigEndian(0x1234);
        return Task.FromResult(ModbusResponseCode.Ok);
    }

    protected override Task<ModbusResponseCode> OnReadInputRegisters(byte unitId, ushort address, ushort count,
        ModbusResponseWriter writer, CancellationToken cancellationToken = default)
        => OnReadHoldingRegisters(unitId, address, count, writer, cancellationToken);

    protected override Task<ModbusResponseCode> OnWriteSingleRegister(byte unitId, ushort address, ushort value,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ModbusResponseCode.Ok);

    protected override Task<ModbusResponseCode> OnWriteSingleCoil(byte unitId, ushort address, bool value,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ModbusResponseCode.Ok);
}
