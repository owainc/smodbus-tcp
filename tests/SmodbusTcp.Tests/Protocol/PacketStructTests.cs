using System.Buffers;
using static SmodbusTcp.Tests.TestSupport;

namespace SmodbusTcp.Tests;

[TestClass]
public sealed class PacketStructTests
{
    [TestMethod]
    [DataRow(1, 1)]
    [DataRow(7, 1)]
    [DataRow(8, 1)]
    [DataRow(9, 2)]
    [DataRow(1968, 246)]
    public void MultiCoilWrite_ParsesQuantityBoundaries(int count, int byteCount)
    {
        var payload = Enumerable.Repeat((byte)0x81, byteCount).ToArray();
        var bytes = new byte[] { 15, 0xAB, 0xCD, (byte)(count >> 8), (byte)count, (byte)payload.Length }
            .Concat(payload).ToArray();
        foreach (var source in new[] { new ReadOnlySequence<byte>(bytes), Sequence(bytes) })
        {
            Assert.IsTrue(MultiCoilWrite.TryParse(source, out var packet, out var bytesRead));
            Assert.AreEqual(bytes.Length, bytesRead);
            Assert.AreEqual(ModbusFunctionCode.WriteMultipleCoils, packet.Function);
            Assert.AreEqual((ushort)0xABCD, packet.FirstAddress);
            Assert.AreEqual((ushort)count, packet.CoilCount);
            Assert.AreEqual((byte)payload.Length, packet.ByteCount);
            CollectionAssert.AreEqual(payload, packet.RawValues.ToArray());
            CollectionAssert.AreEqual(Enumerable.Range(0, count).Select(i => i % 8 is 0 or 7).ToArray(),
                packet.GetValues());
        }
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(123)]
    public void MultiRegisterWrite_ParsesQuantityBoundaries(int count)
    {
        var payload = Enumerable.Range(0, count).SelectMany(_ => new byte[] { 0xAB, 0xCD }).ToArray();
        var bytes = new byte[] { 16, 0xFE, 0xDC, 0, (byte)count, (byte)payload.Length }
            .Concat(payload).ToArray();
        foreach (var source in new[] { new ReadOnlySequence<byte>(bytes), Sequence(bytes) })
        {
            Assert.IsTrue(MultiRegisterWrite.TryParse(source, out var packet, out var bytesRead));
            Assert.AreEqual(bytes.Length, bytesRead);
            Assert.AreEqual(ModbusFunctionCode.WriteMultipleRegisters, packet.Function);
            Assert.AreEqual((ushort)0xFEDC, packet.FirstAddress);
            Assert.AreEqual((ushort)count, packet.RegisterCount);
            Assert.AreEqual((byte)payload.Length, packet.ByteCount);
            CollectionAssert.AreEqual(payload, packet.RawValues.ToArray());
            CollectionAssert.AreEqual(Enumerable.Repeat((ushort)0xABCD, count).ToArray(), packet.GetValues());
        }
    }

    [TestMethod]
    public void MultipleWrites_RejectWrongFunctionCodes()
    {
        // Each packet is otherwise valid for the target type.
        AssertRejected<MultiCoilWrite>([16, 0, 20, 0, 1, 1, 1], MultiCoilWrite.TryParse, MultiCoilWrite.TryParse);
        AssertRejected<MultiRegisterWrite>([15, 0, 20, 0, 1, 2, 0, 1], MultiRegisterWrite.TryParse,
            MultiRegisterWrite.TryParse);
    }

    [TestMethod]
    public void MultiRegisterWrite_OwnsRawBytesAndReturnsIndependentUnsignedValues()
    {
        byte[] source = [16, 0, 0, 0, 4, 8, 0, 0, 0x7F, 0xFF, 0x80, 0, 0xFF, 0xFF];
        var success = MultiRegisterWrite.TryParse(source, out var packet, out var bytesRead);
        Assert.IsTrue(success);
        Assert.AreEqual(14, bytesRead);
        source[6] = 42;
        ushort[] expected = [0, 32767, 32768, 65535];
        var values = packet.GetValues();
        CollectionAssert.AreEqual(expected, values);
        values[0] = 42;
        CollectionAssert.AreEqual(expected, packet.GetValues());
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0x7F, 0xFF, 0x80, 0, 0xFF, 0xFF }, packet.RawValues.ToArray());
    }

    [TestMethod]
    public void MultipleWrite_CopyValuesToDecodesIntoSpanAndRejectsShortDestination()
    {
        Assert.IsTrue(MultiCoilWrite.TryParse([15, 0, 20, 0, 3, 1, 0b00000101], out var coils, out _));
        Span<bool> coilDestination = stackalloc bool[4];
        coilDestination.Fill(true);
        coils.CopyValuesTo(coilDestination);
        CollectionAssert.AreEqual(new[] { true, false, true, true }, coilDestination.ToArray());
        Assert.ThrowsExactly<ArgumentException>(() => coils.CopyValuesTo(new bool[2]));

        Assert.IsTrue(MultiRegisterWrite.TryParse([16, 0, 20, 0, 2, 4, 0x12, 0x34, 0xAB, 0xCD],
            out var registers, out _));
        Span<ushort> registerDestination = stackalloc ushort[3];
        registerDestination.Fill(0xEEEE);
        registers.CopyValuesTo(registerDestination);
        CollectionAssert.AreEqual(new ushort[] { 0x1234, 0xABCD, 0xEEEE }, registerDestination.ToArray());
        Assert.ThrowsExactly<ArgumentException>(() => registers.CopyValuesTo(new ushort[1]));
    }

    [TestMethod]
    public void MultiCoilWrite_OwnsRawBytesAndReturnsIndependentArrays()
    {
        byte[] source = [15, 0, 20, 0, 1, 1, 0xFE];
        var success = MultiCoilWrite.TryParse(source, out var packet, out var bytesRead);
        Assert.IsTrue(success);
        Assert.AreEqual(7, bytesRead);
        source[6] = 1;
        var values = packet.GetValues();
        CollectionAssert.AreEqual(new[] { false }, values);
        values[0] = true;
        CollectionAssert.AreEqual(new[] { false }, packet.GetValues());
        CollectionAssert.AreEqual(new byte[] { 0xFE }, packet.RawValues.ToArray());
    }

    [TestMethod]
    public void TryParse_AllPacketTypes_UseTheSameConsumptionContract()
    {
        AssertContract<ModbusApplicationHeader>([0, 42, 0, 0, 0, 6, 1],
            ModbusApplicationHeader.TryParse, ModbusApplicationHeader.TryParse);
        AssertContract<AddressValueRequest>([6, 0, 20, 0x12, 0x34],
            AddressValueRequest.TryParse, AddressValueRequest.TryParse);
        AssertContract<MultiCoilWrite>([15, 0, 20, 0, 9, 2, 0x81, 1],
            MultiCoilWrite.TryParse, MultiCoilWrite.TryParse);
        AssertContract<MultiRegisterWrite>([16, 0, 20, 0, 1, 2, 0xAB, 0xCD],
            MultiRegisterWrite.TryParse, MultiRegisterWrite.TryParse);
    }

    [TestMethod]
    [DataRow(15, 9, 1, 1)]
    [DataRow(15, 9, 2, 1)]
    [DataRow(16, 2, 3, 3)]
    [DataRow(16, 2, 4, 3)]
    [DataRow(15, 0, 0, 0)]
    [DataRow(16, 0, 0, 0)]
    [DataRow(15, 1969, 247, 247)]
    [DataRow(16, 124, 248, 248)]
    [DataRow(15, 1, 0, 0)]
    [DataRow(16, 1, 0, 0)]
    [DataRow(15, 65535, 0, 0)]
    [DataRow(16, 65535, 254, 254)]
    public void MultipleWrites_RejectInvalidCountsAndLengths(int function, int count, int byteCount, int length)
    {
        var bytes = new byte[] { (byte)function, 0, 20, (byte)(count >> 8), (byte)count, (byte)byteCount }
            .Concat(new byte[length]).ToArray();
        if (function == 15)
        {
            AssertRejected<MultiCoilWrite>(bytes, MultiCoilWrite.TryParse, MultiCoilWrite.TryParse);
        }
        else
        {
            AssertRejected<MultiRegisterWrite>(bytes, MultiRegisterWrite.TryParse, MultiRegisterWrite.TryParse);
        }
    }

    private static void AssertContract<T>(byte[] bytes, SpanParser<T> spanParser, SequenceParser<T> sequenceParser)
        where T : struct
    {
        // Every truncation must fail without publishing a partially populated packet.
        for (var length = 0; length < bytes.Length; length++)
        {
            AssertRejected(bytes[..length], spanParser, sequenceParser);
        }

        Assert.IsTrue(spanParser(bytes, out var expected, out var consumed));
        Assert.AreEqual(bytes.Length, consumed);
        foreach (var source in new[] { bytes, bytes.Concat(new byte[] { 99, 88 }).ToArray() })
        {
            Assert.IsTrue(spanParser(source, out var packet, out consumed));
            Assert.AreEqual(bytes.Length, consumed);
            AssertPacketEqual(expected, packet);
            // Exercise every possible segment boundary, including within the payload.
            for (var split = 1; split < source.Length; split++)
            {
                var first = new Segment(source[..split]);
                var last = first.Append(source[split..]);
                Assert.IsTrue(sequenceParser(new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length),
                    out packet, out consumed));
                Assert.AreEqual(bytes.Length, consumed);
                AssertPacketEqual(expected, packet);
            }

            Assert.IsTrue(sequenceParser(new ReadOnlySequence<byte>(source), out packet, out consumed));
            Assert.AreEqual(bytes.Length, consumed);
            AssertPacketEqual(expected, packet);
        }
    }

    private static void AssertPacketEqual<T>(T expected, T actual) where T : struct
    {
        // Multi-write packets own distinct memory, so compare decoded content and header fields.
        if (expected is MultiCoilWrite coils && actual is MultiCoilWrite actualCoils)
        {
            Assert.AreEqual(coils.FirstAddress, actualCoils.FirstAddress);
            Assert.AreEqual(coils.CoilCount, actualCoils.CoilCount);
            CollectionAssert.AreEqual(coils.RawValues.ToArray(), actualCoils.RawValues.ToArray());
        }
        else if (expected is MultiRegisterWrite registers && actual is MultiRegisterWrite actualRegisters)
        {
            Assert.AreEqual(registers.FirstAddress, actualRegisters.FirstAddress);
            Assert.AreEqual(registers.RegisterCount, actualRegisters.RegisterCount);
            CollectionAssert.AreEqual(registers.RawValues.ToArray(), actualRegisters.RawValues.ToArray());
        }
        else
        {
            Assert.AreEqual(expected, actual);
        }
    }

    private static void AssertRejected<T>(byte[] bytes, SpanParser<T> spanParser, SequenceParser<T> sequenceParser)
        where T : struct
    {
        var success = spanParser(bytes, out var packet, out var bytesRead);
        Assert.IsFalse(success);
        Assert.AreEqual(0, bytesRead);
        Assert.AreEqual(default, packet);
        foreach (var source in new[] { new ReadOnlySequence<byte>(bytes), Sequence(bytes) })
        {
            Assert.IsFalse(sequenceParser(source, out packet, out bytesRead));
            Assert.AreEqual(0, bytesRead);
            Assert.AreEqual(default, packet);
        }
    }

    private delegate bool SpanParser<T>(ReadOnlySpan<byte> source, out T packet, out int consumed);

    private delegate bool SequenceParser<T>(ReadOnlySequence<byte> source, out T packet, out int consumed);
}
