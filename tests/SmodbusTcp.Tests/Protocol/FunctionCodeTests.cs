namespace SmodbusTcp.Tests;

[TestClass]
public sealed class FunctionCodeTests
{
    [TestMethod]
    public void DefinedMembers_AreExactlyOneOfReadOrWrite()
    {
        foreach (var code in Enum.GetValues<ModbusFunctionCode>())
        {
            Assert.AreNotEqual(code.IsRead, code.IsWrite, code.ToString());
        }
    }

    [TestMethod]
    [DataRow((byte)0)]
    [DataRow((byte)7)]
    [DataRow((byte)0x7F)]
    [DataRow((byte)0x81)]
    [DataRow((byte)0xFF)]
    public void UndefinedValues_AreNeitherReadNorWrite(byte value)
    {
        var code = (ModbusFunctionCode)value;
        Assert.IsFalse(code.IsRead);
        Assert.IsFalse(code.IsWrite);
    }
}
