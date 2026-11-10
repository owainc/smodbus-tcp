using System.Buffers;
using static SmodbusTcp.Tests.TestSupport;

namespace SmodbusTcp.Tests;

[TestClass]
public sealed class ResponseContractTests
{
    [TestMethod]
    [DataRow(1, 9, 2)]
    [DataRow(2, 9, 2)]
    [DataRow(3, 2, 4)]
    [DataRow(4, 2, 4)]
    public async Task BaseHandler_ValidatesPayloadAndCompletesWriter(int function, int count, int expected)
    {
        foreach (var length in new[] { 0, expected - 1, expected, expected + 1, 65536 })
        {
            var writer = new ModbusResponseWriter(new ArrayBufferWriter<byte>());
            // Existing bytes are not part of this callback's payload.
            writer.Write(42);
            var typedHandler = new PayloadHandler(length);
            IModbusFunctionHandler handler = typedHandler;

            Task<ModbusResponseCode> Call() => function switch
            {
                1 => handler.ReadCoils(1, 0, (ushort)count, writer),
                2 => handler.ReadDiscreteInputs(1, 0, (ushort)count, writer),
                3 => handler.ReadHoldingRegisters(1, 0, (ushort)count, writer),
                _ => handler.ReadInputRegisters(1, 0, (ushort)count, writer)
            };

            if (length == expected)
            {
                Assert.AreEqual(ModbusResponseCode.Ok, await Call());
            }
            else
            {
                Assert.AreEqual(ModbusResponseCode.SlaveDeviceFailure, await Call());
                var error = typedHandler.Error!;
                Assert.IsNotNull(error);
                Assert.AreEqual((ModbusFunctionCode)function, error.FunctionCode);
                Assert.AreEqual(expected, error.ExpectedLength);
                Assert.AreEqual(length, error.ActualLength);
            }

            Assert.ThrowsExactly<InvalidOperationException>(() => writer.Write(1));
            Assert.ThrowsExactly<InvalidOperationException>(() => writer.GetSpan(1));
            Assert.ThrowsExactly<InvalidOperationException>(() => writer.Advance(0));
        }
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(3)]
    public async Task RawHandler_OwnsPayloadValidation(int length)
    {
        await Exchange(new RawPayloadHandler(length), Frame(3, 0, 0, 0, 1),
            Frame(new byte[] { 3, 2 }.Concat(new byte[length]).ToArray()));
    }

    [TestMethod]
    public async Task InvalidLength_DefaultHookReturnsDeviceFailure()
        => await Exchange(new InvalidPayloadHandler(), Frame(3, 0, 0, 0, 1), Frame(0x83, 4));

    [TestMethod]
    public async Task RetainedWriter_CannotCorruptNextRequest()
    {
        var handler = new RawPayloadHandler(2);
        await Exchange(handler, Frame(3, 0, 0, 0, 1).Concat(Frame(3, 0, 0, 0, 1)).ToArray(),
            Frame(3, 2, 0, 0).Concat(Frame(3, 2, 0, 0)).ToArray());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task FailedHandler_CompletesWriterWithoutMaskingFailure(bool throws)
    {
        var writer = new ModbusResponseWriter(new ArrayBufferWriter<byte>());
        IModbusFunctionHandler handler = new FailingHandler(throws);
        if (throws)
        {
            await Assert.ThrowsExactlyAsync<NotSupportedException>(() => handler.ReadCoils(1, 0, 1, writer));
        }
        else
        {
            Assert.AreEqual(ModbusResponseCode.IllegalDataAddress, await handler.ReadCoils(1, 0, 1, writer));
        }

        Assert.ThrowsExactly<InvalidOperationException>(() => writer.Write(0));
    }

    private sealed class InvalidPayloadHandler : ModbusFunctionHandlerBase
    {
        protected override Task<ModbusResponseCode> OnReadHoldingRegisters(byte unitId, ushort address, ushort count,
            ModbusResponseWriter writer, CancellationToken cancellationToken = default)
            => Task.FromResult(ModbusResponseCode.Ok);
    }

    private sealed class FailingHandler(bool throws) : ModbusFunctionHandlerBase
    {
        protected override async Task<ModbusResponseCode> OnReadCoils(byte unitId, ushort address, ushort count,
            ModbusResponseWriter writer, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            writer.WriteInt32BigEndian(42);
            if (throws)
            {
                throw new NotSupportedException();
            }

            return ModbusResponseCode.IllegalDataAddress;
        }
    }

    private class PayloadHandler(int length) : ModbusFunctionHandlerBase
    {
        public InvalidModbusResponseLengthException? Error { get; private set; }

        protected override void OnInvalidResponseLength(InvalidModbusResponseLengthException exception)
            => Error = exception;

        protected override Task<ModbusResponseCode> OnReadCoils(byte unitId, ushort address, ushort count,
            ModbusResponseWriter writer, CancellationToken cancellationToken = default)
        {
            writer.GetSpan(length)[..length].Clear();
            writer.Advance(length);
            return Task.FromResult(ModbusResponseCode.Ok);
        }

        protected override Task<ModbusResponseCode> OnReadDiscreteInputs(byte unitId, ushort address, ushort count,
            ModbusResponseWriter writer, CancellationToken cancellationToken = default)
            => OnReadCoils(unitId, address, count, writer, cancellationToken);

        protected override Task<ModbusResponseCode> OnReadHoldingRegisters(byte unitId, ushort address, ushort count,
            ModbusResponseWriter writer, CancellationToken cancellationToken = default)
            => OnReadCoils(unitId, address, count, writer, cancellationToken);

        protected override Task<ModbusResponseCode> OnReadInputRegisters(byte unitId, ushort address, ushort count,
            ModbusResponseWriter writer, CancellationToken cancellationToken = default)
            => OnReadCoils(unitId, address, count, writer, cancellationToken);
    }

    private sealed class RawPayloadHandler(int length) : ModbusFunctionHandlerBase, IModbusFunctionHandler
    {
        public ModbusResponseWriter? Writer { get; private set; }

        Task<ModbusResponseCode> IModbusFunctionHandler.ReadHoldingRegisters(byte unitId, ushort address,
            ushort count, ModbusResponseWriter writer, CancellationToken cancellationToken)
        {
            var span = writer.GetSpan(length);
            if (Writer is not null)
            {
                Assert.AreNotSame(Writer, writer);
                Assert.ThrowsExactly<InvalidOperationException>(() => Writer.Write(99));
            }

            Writer = writer;
            span[..length].Clear();
            writer.Advance(length);
            return Task.FromResult(ModbusResponseCode.Ok);
        }
    }
}
