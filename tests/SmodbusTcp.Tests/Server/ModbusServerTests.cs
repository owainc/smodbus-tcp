using static SmodbusTcp.Tests.TestSupport;

namespace SmodbusTcp.Tests;

[TestClass]
public sealed class ModbusServerTests
{
    [TestMethod]
    public async Task HandlerError_DiscardsPartialResponseAndNextRequestStartsCleanly()
    {
        var first = Frame(3, 0, 20, 0, 1);
        var second = Frame(1, 0, 20, 0, 1);
        await Exchange(new PartialResponseHandler(), first.Concat(second).ToArray(),
            Frame(0x83, 2).Concat(Frame(1, 1, 1)).ToArray());
    }

    [TestMethod]
    public async Task RawSingleCoilOverride_CanHandleDeviceSpecificEncoding()
    {
        var handler = new CustomCoilHandler();
        var request = Frame(5, 2, 3, 0, 15);
        await Exchange(handler, request, request);
        Assert.AreEqual((ushort)15, handler.Value);
    }

    [TestMethod]
    [DataRow(15)]
    [DataRow(16)]
    public async Task RawOverride_ReceivesPacketWithOriginalBytes(int function)
    {
        var handler = new RawHandler();
        var count = function == 15 ? 9 : 1;
        await Exchange(handler, Frame((byte)function, 0, 20, 0, (byte)count, 2, 0xA5, 0xFF),
            Frame((byte)function, 0, 20, 0, (byte)count));
        Assert.AreEqual(1, handler.Calls);
        CollectionAssert.AreEqual(new byte[] { 0xA5, 0xFF }, handler.RawValues.ToArray());
    }

    [TestMethod]
    public async Task WriteMultipleCoils_EchoesAddressAndQuantity()
        => await Exchange(new RecordingHandler(), Frame(15, 0, 20, 0, 10, 2, 0xCD, 1), Frame(15, 0, 20, 0, 10));

    [TestMethod]
    public async Task WriteMultipleRegisters_EchoesAddressAndQuantity()
        => await Exchange(new RecordingHandler(), Frame(16, 0, 20, 0, 2, 4, 0x12, 0x34, 0xFF, 0xFE),
            Frame(16, 0, 20, 0, 2));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task UnknownFunction_ReturnsIllegalFunction(bool hasPayload)
        => await Exchange(new RecordingHandler(), hasPayload ? Frame(0x7F, 0) : Frame(0x7F), Frame(0xFF, 1));

    [TestMethod]
    public async Task HandlerError_ReturnsExceptionResponse()
        => await Exchange(new ErrorHandler(), Frame(16, 0, 20, 0, 1, 2, 0, 1), Frame(0x90, 2));

    [TestMethod]
    [DataRow(15)]
    [DataRow(16)]
    public async Task RawOverride_CannotBypassPacketValidation(int function)
    {
        var handler = new RawHandler();

        await Exchange(handler, Frame((byte)function, 0, 20, 0, 0, 99, 0xA5), Frame((byte)(function | 0x80), 3));

        Assert.AreEqual(0, handler.Calls);
    }

    [TestMethod]
    [DataRow(15)]
    [DataRow(16)]
    public async Task MultipleWrite_TrailingPayloadIsRejectedBeforeRawDispatch(int function)
    {
        var handler = new RawHandler();
        var count = function == 15 ? 9 : 1;
        await Exchange(handler, Frame((byte)function, 0, 20, 0, (byte)count, 2, 0xA5, 0xFF, 99),
            Frame((byte)(function | 0x80), 3));
        Assert.AreEqual(0, handler.Calls);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public async Task Read_ReturnsByteCountAndHandlerData(int function)
        => await Exchange(new RecordingHandler(), Frame((byte)function, 0, 20, 0, 1),
            function <= 2 ? Frame((byte)function, 1, 1) : Frame((byte)function, 2, 0x12, 0x34));

    [TestMethod]
    public async Task MaximumRegisterWrite_IsAccepted()
        => await Exchange(new RecordingHandler(),
            Frame(new byte[] { 16, 0, 0, 0, 123, 246 }.Concat(new byte[246]).ToArray()),
            Frame(16, 0, 0, 0, 123));

    private sealed class PartialResponseHandler : ModbusFunctionHandlerBase
    {
        protected override Task<ModbusResponseCode> OnReadHoldingRegisters(byte unit, ushort address, ushort count,
            ModbusResponseWriter writer, CancellationToken cancellationToken = default)
        {
            writer.WriteUInt16BigEndian(0xABCD);
            return Task.FromResult(ModbusResponseCode.IllegalDataAddress);
        }

        protected override Task<ModbusResponseCode> OnReadCoils(byte unit, ushort address, ushort count,
            ModbusResponseWriter writer, CancellationToken cancellationToken = default)
        {
            writer.Write(1);
            return Task.FromResult(ModbusResponseCode.Ok);
        }
    }

    private sealed class CustomCoilHandler : ModbusFunctionHandlerBase, IModbusFunctionHandler
    {
        public ushort Value { get; private set; }

        Task<ModbusResponseCode> IModbusFunctionHandler.WriteSingleCoil(byte unit, ushort address, ushort value,
            CancellationToken cancellationToken)
        {
            Value = value;
            return Task.FromResult(ModbusResponseCode.Ok);
        }
    }
}
