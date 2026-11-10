namespace SmodbusTcp.Tests;

[TestClass]
public sealed class FunctionHandlerTests
{
    [TestMethod]
    public async Task WriteMultipleCoils_DecodesLeastSignificantBitFirstAndIgnoresPadding()
    {
        var handler = new RecordingHandler();
        IModbusFunctionHandler raw = handler;

        Assert.IsTrue(MultiCoilWrite.TryParse([15, 0, 20, 0, 10, 2, 0xCD, 0xFD], out var packet, out _));
        var response = await raw.WriteMultipleCoils(1, packet);

        Assert.AreEqual(ModbusResponseCode.Ok, response);
        CollectionAssert.AreEqual(new[] { true, false, true, true, false, false, true, true, true, false },
            handler.Coils.ToArray());
        Assert.AreEqual(1, handler.Calls);
    }

    [TestMethod]
    public async Task WriteMultipleRegisters_DecodesUnsignedBigEndianValues()
    {
        var handler = new RecordingHandler();
        IModbusFunctionHandler raw = handler;

        Assert.IsTrue(MultiRegisterWrite.TryParse([16, 0, 20, 0, 3, 6, 0x7F, 0xFF, 0x80, 0x00, 0xFF, 0xFF],
            out var packet, out _));
        var response = await raw.WriteMultipleRegisters(1, packet);

        Assert.AreEqual(ModbusResponseCode.Ok, response);
        CollectionAssert.AreEqual(new ushort[] { 0x7FFF, 0x8000, 0xFFFF }, handler.Registers.ToArray());
        Assert.AreEqual(1, handler.Calls);
    }

    [TestMethod]
    public async Task DefaultMultipleWritePackets_DoNotCallStandardHandler()
    {
        var handler = new RecordingHandler();
        IModbusFunctionHandler raw = handler;
        Assert.AreEqual(ModbusResponseCode.IllegalDataValue, await raw.WriteMultipleCoils(1, default));
        Assert.AreEqual(ModbusResponseCode.IllegalDataValue, await raw.WriteMultipleRegisters(1, default));
        Assert.AreEqual(0, handler.Calls);
    }

    [TestMethod]
    [DataRow(15, 1968)]
    [DataRow(16, 123)]
    public async Task MultipleWrite_MaximumQuantity_CallsStandardHandler(int function, int count)
    {
        var handler = new RecordingHandler();
        IModbusFunctionHandler raw = handler;
        var values = new byte[] { (byte)function, 0, 20, (byte)(count >> 8), (byte)count, 246 }.Concat(new byte[246])
            .ToArray();

        ModbusResponseCode response;
        if (function == 15)
        {
            Assert.IsTrue(MultiCoilWrite.TryParse(values, out var packet, out _));
            response = await raw.WriteMultipleCoils(1, packet);
        }
        else
        {
            Assert.IsTrue(MultiRegisterWrite.TryParse(values, out var packet, out _));
            response = await raw.WriteMultipleRegisters(1, packet);
        }

        Assert.AreEqual(ModbusResponseCode.Ok, response);
        Assert.AreEqual(count, function == 15 ? handler.Coils.Length : handler.Registers.Length);
        Assert.AreEqual(1, handler.Calls);
    }
}
