using static SmodbusTcp.Tests.TestSupport;

namespace SmodbusTcp.Tests;

[TestClass]
public sealed class RequestValidationTests
{
    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    public async Task FixedRequest_RejectsShortAndTrailingPayloadsBeforeDispatch(int function)
    {
        foreach (var length in new[] { 1, 2, 3, 4, 6 })
        {
            var handler = new BoundaryHandler();
            var pdu = new byte[length];
            pdu[0] = (byte)function;
            if (length > 4)
            {
                pdu[4] = 1;
            }

            await Exchange(handler, Frame(pdu), Frame((byte)(function | 0x80), 3));
            Assert.AreEqual(0, handler.Calls);
        }
    }

    [TestMethod]
    [DataRow(1, 2000)]
    [DataRow(2, 2000)]
    [DataRow(3, 125)]
    [DataRow(4, 125)]
    public async Task ReadQuantity_RejectsInvalidValuesBeforeStandardHandler(int function, int maximum)
    {
        foreach (var count in new[] { 0, maximum + 1, 65535 })
        {
            var handler = new BoundaryHandler();
            await Exchange(handler, Frame((byte)function, 0, 20, (byte)(count >> 8), (byte)count),
                Frame((byte)(function | 0x80), 3));
            Assert.AreEqual(0, handler.Calls);
        }
    }

    [TestMethod]
    [DataRow(1, 2000)]
    [DataRow(2, 2000)]
    [DataRow(3, 125)]
    [DataRow(4, 125)]
    public async Task ReadQuantity_AcceptsMaximumAndRoundsBitByteCount(int function, int maximum)
    {
        var cases = function <= 2
            ? new[] { (1, 1), (7, 1), (8, 1), (9, 2), (maximum, 250) }
            : new[] { (1, 2), (maximum, 250) };
        foreach (var (count, byteCount) in cases)
        {
            var handler = new BoundaryHandler();
            var expected = new[] { (byte)function, (byte)byteCount }.Concat(new byte[byteCount]).ToArray();
            await Exchange(handler, Frame((byte)function, 0xAB, 0xCD, (byte)(count >> 8), (byte)count),
                Frame(expected));
            Assert.AreEqual(1, handler.Calls);
            Assert.AreEqual((byte)1, handler.Unit);
            Assert.AreEqual((ushort)0xABCD, handler.Address);
            Assert.AreEqual((ushort)count, handler.Value);
        }
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(255)]
    [DataRow(65281)]
    [DataRow(65535)]
    public async Task SingleCoil_RejectsInvalidEncodingBeforeStandardHandler(int value)
    {
        var handler = new BoundaryHandler();
        await Exchange(handler, Frame(5, 0, 20, (byte)(value >> 8), (byte)value), Frame(0x85, 3));
        Assert.AreEqual(0, handler.Calls);
    }

    [TestMethod]
    [DataRow(5, 0)]
    [DataRow(5, 65280)]
    [DataRow(6, 0)]
    [DataRow(6, 32767)]
    [DataRow(6, 32768)]
    [DataRow(6, 65535)]
    public async Task SingleWrite_PreservesIdentifiersAddressAndValue(int function, int value)
    {
        var handler = new BoundaryHandler();
        var request = Frame((byte)function, 0xFF, 0xFF, (byte)(value >> 8), (byte)value);
        request[0] = 0xAB;
        request[1] = 0xCD;
        request[6] = 0xFE;
        await Exchange(handler, request, request);
        Assert.AreEqual(1, handler.Calls);
        Assert.AreEqual((byte)0xFE, handler.Unit);
        Assert.AreEqual(ushort.MaxValue, handler.Address);
        Assert.AreEqual((ushort)value, handler.Value);
    }

    private sealed class BoundaryHandler : ModbusFunctionHandlerBase
    {
        public int Calls { get; private set; }
        public byte Unit { get; private set; }
        public ushort Address { get; private set; }
        public ushort Value { get; private set; }

        private Task<ModbusResponseCode> Record(byte unit, ushort address, ushort value)
        {
            Calls++;
            (Unit, Address, Value) = (unit, address, value);
            return Task.FromResult(ModbusResponseCode.Ok);
        }

        private Task<ModbusResponseCode> Read(byte unit, ushort address, ushort count, ModbusResponseWriter writer,
            bool bits)
        {
            var length = bits ? ModbusPayload.GetPackedBitByteCount(count) : ModbusPayload.GetRegisterByteCount(count);
            writer.GetSpan(length)[..length].Clear();
            writer.Advance(length);
            return Record(unit, address, count);
        }

        protected override Task<ModbusResponseCode> OnReadCoils(byte u, ushort a, ushort c, ModbusResponseWriter w,
            CancellationToken cancellationToken = default) =>
            Read(u, a, c, w, true);

        protected override Task<ModbusResponseCode>
            OnReadDiscreteInputs(byte u, ushort a, ushort c, ModbusResponseWriter w,
                CancellationToken cancellationToken = default) => Read(u, a, c, w, true);

        protected override Task<ModbusResponseCode> OnReadHoldingRegisters(byte u, ushort a, ushort c,
            ModbusResponseWriter w, CancellationToken cancellationToken = default) => Read(u, a, c, w, false);

        protected override Task<ModbusResponseCode>
            OnReadInputRegisters(byte u, ushort a, ushort c, ModbusResponseWriter w,
                CancellationToken cancellationToken = default) => Read(u, a, c, w, false);

        protected override Task<ModbusResponseCode> OnWriteSingleCoil(byte u, ushort a, bool v,
            CancellationToken cancellationToken = default) =>
            Record(u, a, v ? (ushort)0xFF00 : (ushort)0);

        protected override Task<ModbusResponseCode> OnWriteSingleRegister(byte u, ushort a, ushort v,
            CancellationToken cancellationToken = default) =>
            Record(u, a, v);
    }
}
