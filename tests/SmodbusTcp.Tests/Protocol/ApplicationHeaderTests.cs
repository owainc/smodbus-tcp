using System.Buffers;
using System.Buffers.Binary;
using static SmodbusTcp.Tests.TestSupport;

namespace SmodbusTcp.Tests;

[TestClass]
public sealed class ApplicationHeaderTests
{
    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(254)]
    public void ParseAndWriteResponse_PreservesIdentifiersAndUsesResponseLength(int length)
    {
        byte[] bytes = [0xAB, 0xCD, 0, 0, 0, (byte)length, 0xFE];
        foreach (var source in new[] { new ReadOnlySequence<byte>(bytes), Sequence(bytes) })
        {
            Assert.IsTrue(ModbusApplicationHeader.TryParse(source, out var header, out var consumed));
            Assert.AreEqual(7, consumed);
            Assert.AreEqual((ushort)(length - 1), header.PduLength);
            var response = new byte[8];
            response[7] = 0xCC;
            Assert.IsTrue(header.TryWriteResponse(response, 2, out var written));
            Assert.AreEqual(7, written);
            CollectionAssert.AreEqual(new byte[] { 0xAB, 0xCD, 0, 0, 0, 3, 0xFE, 0xCC }, response);
        }
    }

    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(0, 1)]
    [DataRow(0, 255)]
    [DataRow(0, 65535)]
    [DataRow(1, 6)]
    [DataRow(256, 6)]
    public void Parse_RejectsInvalidProtocolOrLength(int protocol, int length)
    {
        byte[] bytes = [0, 42, 0, 0, 0, 0, 1];
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(2), (ushort)protocol);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(4), (ushort)length);
        Assert.IsFalse(ModbusApplicationHeader.TryParse(bytes, out var spanHeader, out var spanConsumed));
        Assert.AreEqual(default, spanHeader);
        Assert.AreEqual(0, spanConsumed);
        foreach (var source in new[] { new ReadOnlySequence<byte>(bytes), Sequence(bytes) })
        {
            Assert.IsFalse(ModbusApplicationHeader.TryParse(source, out var header, out var consumed));
            Assert.AreEqual(0, consumed);
            Assert.AreEqual(default, header);
        }
    }

    [TestMethod]
    public void ShortBuffers_FailWithoutWriting()
    {
        Assert.IsTrue(ModbusApplicationHeader.TryParse([0, 42, 0, 0, 0, 6, 1], out var header, out _));
        for (var length = 0; length < 7; length++)
        {
            var bytes = Enumerable.Repeat((byte)0xCC, length).ToArray();
            Assert.IsFalse(ModbusApplicationHeader.TryParse(Sequence(bytes), out _, out var consumed));
            Assert.AreEqual(0, consumed);
            Assert.IsFalse(header.TryWriteResponse(bytes, 2, out var written));
            Assert.AreEqual(0, written);
            CollectionAssert.AreEqual(Enumerable.Repeat((byte)0xCC, length).ToArray(), bytes);
        }
    }
}
