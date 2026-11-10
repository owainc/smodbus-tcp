using System.Buffers;

namespace SmodbusTcp.Tests;

[TestClass]
public sealed class ResponseWriterTests
{
    [TestMethod]
    public void MixedWrites_PreserveByteOrderSignedBitsAndAdvanceCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>(1);
        var writer = new ModbusResponseWriter(buffer);
        writer.Write(0xA5);
        writer.WriteUInt16BigEndian(0xFEDC);
        writer.WriteInt16BigEndian(-2);
        writer.WriteUInt32BigEndian(0x89ABCDEF);
        writer.WriteInt32BigEndian(unchecked((int)0x89ABCDEF));
        writer.WriteUInt64BigEndian(0x0123456789ABCDEF);
        writer.WriteInt64BigEndian(unchecked((long)0x89ABCDEF01234567));
        writer.WriteUInt16LittleEndian(0xFEDC);
        writer.WriteInt16LittleEndian(-2);
        writer.WriteUInt32LittleEndian(0x89ABCDEF);
        writer.WriteInt32LittleEndian(unchecked((int)0x89ABCDEF));
        writer.WriteUInt64LittleEndian(0x0123456789ABCDEF);
        writer.WriteInt64LittleEndian(unchecked((long)0x89ABCDEF01234567));
        writer.GetSpan(2)[..2].Fill(0x42);
        writer.Advance(2);
        CollectionAssert.AreEqual(new byte[]
        {
            0xA5,
            0xFE, 0xDC, 0xFF, 0xFE, 0x89, 0xAB, 0xCD, 0xEF,
            0x89, 0xAB, 0xCD, 0xEF,
            0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF,
            0x89, 0xAB, 0xCD, 0xEF, 0x01, 0x23, 0x45, 0x67,
            0xDC, 0xFE, 0xFE, 0xFF, 0xEF, 0xCD, 0xAB, 0x89,
            0xEF, 0xCD, 0xAB, 0x89, 0xEF, 0xCD, 0xAB, 0x89,
            0x67, 0x45, 0x23, 0x01,
            0x67, 0x45, 0x23, 0x01, 0xEF, 0xCD, 0xAB, 0x89,
            0x42, 0x42
        }, buffer.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void FloatingPointAndFixedPointWrites_PreserveEncodingAndPrecision()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new ModbusResponseWriter(buffer);
        writer.WriteSingleBigEndian(1.5f);
        writer.WriteDoubleBigEndian(-2.25);
        writer.WriteSingleLittleEndian(1.5f);
        writer.WriteDoubleLittleEndian(-2.25);
        writer.WriteInt16FixedPointBigEndian(12.34m, 2);
        writer.WriteInt32FixedPointBigEndian(-12.34m, 2);
        writer.WriteInt64FixedPointBigEndian(12.34m, 2);
        writer.WriteInt16FixedPointLittleEndian(12.34m, 2);
        writer.WriteInt32FixedPointLittleEndian(-12.34m, 2);
        writer.WriteInt64FixedPointLittleEndian(12.34m, 2);
        writer.WriteInt16FixedPointBigEndian(1.245m, 2);
        writer.WriteInt16FixedPointBigEndian(1.255m, 2);

        CollectionAssert.AreEqual(new byte[]
        {
            0x3F, 0xC0, 0x00, 0x00,
            0xC0, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0xC0, 0x3F,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0xC0,
            0x04, 0xD2,
            0xFF, 0xFF, 0xFB, 0x2E,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0xD2,
            0xD2, 0x04,
            0x2E, 0xFB, 0xFF, 0xFF,
            0xD2, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x7C,
            0x00, 0x7E
        }, buffer.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void FixedPointWrites_RejectInvalidPrecisionAndOverflow()
    {
        var writer = new ModbusResponseWriter(new ArrayBufferWriter<byte>());

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            writer.WriteInt16FixedPointBigEndian(1m, 29));
        Assert.ThrowsExactly<OverflowException>(() =>
            writer.WriteInt16FixedPointBigEndian(327.68m, 2));
    }
}
