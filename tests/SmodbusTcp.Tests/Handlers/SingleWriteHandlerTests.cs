namespace SmodbusTcp.Tests;

[TestClass]
public sealed class SingleWriteHandlerTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task MultipleWrites_AwaitEachSingleWriteAndStopOnFailure(bool registers, bool fail)
    {
        var handler = new SingleWriteHandler { FailAddress = fail ? 21 : null };

        var response = await (registers
            ? handler.WriteMultipleRegisters(7, 20, new ushort[] { 0x7FFF, 0xFFFF, 0x8000 })
            : handler.WriteMultipleCoils(7, 20, new[] { true, false, true }));

        Assert.AreEqual(fail ? ModbusResponseCode.IllegalDataValue : ModbusResponseCode.Ok, response);
        var expected = registers
            ? new (byte, ushort, int)[] { (7, 20, 0x7FFF), (7, 21, 0xFFFF), (7, 22, 0x8000) }
            : new (byte, ushort, int)[] { (7, 20, 1), (7, 21, 0), (7, 22, 1) };
        CollectionAssert.AreEqual(expected.Take(fail ? 2 : 3).ToArray(), handler.Writes.ToArray());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MultipleWrites_RejectOverflowingRangeBeforeAnyWrite(bool registers)
    {
        var handler = new SingleWriteHandler();

        var response = await (registers
            ? handler.WriteMultipleRegisters(7, ushort.MaxValue, new ushort[] { 1, 2 })
            : handler.WriteMultipleCoils(7, ushort.MaxValue, new[] { true, false }));

        Assert.AreEqual(ModbusResponseCode.IllegalDataAddress, response);
        Assert.AreEqual(0, handler.Writes.Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MultipleWrites_CancellationAfterFirstWriteStopsRemainingWrites(bool registers)
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new SingleWriteHandler
        {
            AfterWrite = token =>
            {
                Assert.AreEqual(cancellation.Token, token);
                cancellation.Cancel();
            }
        };

        var writes = registers
            ? handler.WriteMultipleRegisters(7, 20, new ushort[] { 1, 2 }, cancellation.Token)
            : handler.WriteMultipleCoils(7, 20, new[] { true, false }, cancellation.Token);
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => writes);
        CollectionAssert.AreEqual(new (byte, ushort, int)[] { (7, 20, 1) }, handler.Writes.ToArray());
    }

    private sealed class SingleWriteHandler : ModbusFunctionHandlerBase
    {
        private bool _writing;
        public ushort? FailAddress { get; init; }
        public Action<CancellationToken>? AfterWrite { get; init; }
        public List<(byte, ushort, int)> Writes { get; } = [];

        public Task<ModbusResponseCode> WriteMultipleCoils(byte unitId, ushort firstAddress,
            ReadOnlyMemory<bool> values,
            CancellationToken cancellationToken = default)
            => OnWriteMultipleCoils(unitId, firstAddress, values, cancellationToken);

        public Task<ModbusResponseCode> WriteMultipleRegisters(byte unitId, ushort firstAddress,
            ReadOnlyMemory<ushort> values,
            CancellationToken cancellationToken = default)
            => OnWriteMultipleRegisters(unitId, firstAddress, values, cancellationToken);

        protected override Task<ModbusResponseCode> OnWriteMultipleCoils(byte unitId, ushort firstAddress,
            ReadOnlyMemory<bool> values,
            CancellationToken cancellationToken = default)
            => HandleAsSingleWrites(unitId, firstAddress, values, cancellationToken);

        protected override Task<ModbusResponseCode> OnWriteMultipleRegisters(byte unitId, ushort firstAddress,
            ReadOnlyMemory<ushort> values, CancellationToken cancellationToken = default)
            => HandleAsSingleWrites(unitId, firstAddress, values, cancellationToken);

        protected override Task<ModbusResponseCode> OnWriteSingleCoil(byte unitId, ushort address, bool value,
            CancellationToken cancellationToken = default)
            => RecordWrite(unitId, address, value ? 1 : 0, cancellationToken);

        protected override Task<ModbusResponseCode> OnWriteSingleRegister(byte unitId, ushort address, ushort value,
            CancellationToken cancellationToken = default)
            => RecordWrite(unitId, address, value, cancellationToken);

        private async Task<ModbusResponseCode> RecordWrite(byte unitId, ushort address, int value,
            CancellationToken cancellationToken)
        {
            Assert.IsFalse(_writing, "Single writes must not overlap.");
            _writing = true;
            await Task.Yield();
            Writes.Add((unitId, address, value));
            AfterWrite?.Invoke(cancellationToken);
            _writing = false;
            return address == FailAddress ? ModbusResponseCode.IllegalDataValue : ModbusResponseCode.Ok;
        }
    }
}
