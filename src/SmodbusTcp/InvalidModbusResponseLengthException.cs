namespace SmodbusTcp;

/// <summary>A successful handler produced an incorrect response payload length.</summary>
public sealed class InvalidModbusResponseLengthException : InvalidOperationException
{
    /// <summary>Creates an exception describing the invalid payload, excluding function and byte-count prefixes.</summary>
    /// <param name="functionCode">The function whose response payload has an invalid length.</param>
    /// <param name="expectedLength">The expected payload length in bytes, excluding function and byte-count fields.</param>
    /// <param name="actualLength">The actual payload length in bytes, excluding function and byte-count fields.</param>
    public InvalidModbusResponseLengthException(ModbusFunctionCode functionCode, int expectedLength, int actualLength)
        : base(
            $"Function {functionCode} expected {expectedLength} payload bytes, but the handler wrote {actualLength}.")
    {
        FunctionCode = functionCode;
        ExpectedLength = expectedLength;
        ActualLength = actualLength;
    }

    /// <summary>Gets the function being handled.</summary>
    public ModbusFunctionCode FunctionCode { get; }

    /// <summary>Gets the expected payload length in bytes.</summary>
    public int ExpectedLength { get; }

    /// <summary>Gets the actual payload length in bytes.</summary>
    public int ActualLength { get; }
}
