using Microsoft.Extensions.Hosting;
using SmodbusTcp;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSmodbusServer<InterfaceHandler>();
builder.UseSmodbusTcpListener(1505);

using var host = builder.Build();
await host.RunAsync();

public sealed class InterfaceHandler : IModbusFunctionHandler
{
    private readonly bool[] _coils = new bool[16];
    private readonly ushort[] _holdingRegisters = new ushort[16];

    public Task<ModbusResponseCode> ReadCoils(byte unitId, ushort firstAddress, ushort registerCount,
        ModbusResponseWriter responseWriter, CancellationToken cancellationToken = default)
        => ModbusFunctionHandlerHelpers.HandleRead(ModbusFunctionCode.ReadCoils, registerCount, responseWriter,
            () => ReadCoils(firstAddress, registerCount, responseWriter), OnInvalidResponseLength);

    public Task<ModbusResponseCode> ReadHoldingRegisters(byte unitId, ushort firstAddress, ushort registerCount,
        ModbusResponseWriter responseWriter, CancellationToken cancellationToken = default)
        => ModbusFunctionHandlerHelpers.HandleRead(ModbusFunctionCode.ReadHoldingRegisters, registerCount,
            responseWriter,
            () => ReadRegisters(firstAddress, registerCount, responseWriter), OnInvalidResponseLength);

    public Task<ModbusResponseCode> WriteSingleCoil(byte unitId, ushort address, ushort value,
        CancellationToken cancellationToken = default)
        => ModbusFunctionHandlerHelpers.HandleWriteSingleCoil(unitId, address, value, WriteCoil, cancellationToken);

    public Task<ModbusResponseCode> WriteSingleRegister(byte unitId, ushort address, ushort value,
        CancellationToken cancellationToken = default)
        => WriteRegister(unitId, address, value, cancellationToken);

    public Task<ModbusResponseCode> WriteMultipleCoils(byte unitId, MultiCoilWrite packet,
        CancellationToken cancellationToken = default)
        => ModbusFunctionHandlerHelpers.HandleWriteMultipleCoils(unitId, packet,
            (id, address, values, token) => ModbusFunctionHandlerHelpers.HandleAsSingleWrites(
                id, address, values, WriteCoil, token), cancellationToken);

    public Task<ModbusResponseCode> WriteMultipleRegisters(byte unitId, MultiRegisterWrite packet,
        CancellationToken cancellationToken = default)
        => ModbusFunctionHandlerHelpers.HandleWriteMultipleRegisters(unitId, packet,
            (id, address, values, token) => ModbusFunctionHandlerHelpers.HandleAsSingleWrites(
                id, address, values, WriteRegister, token), cancellationToken);

    private Task<ModbusResponseCode> ReadCoils(ushort firstAddress, ushort count, ModbusResponseWriter writer)
    {
        if (!IsRangeValid(firstAddress, count, _coils.Length))
        {
            return Task.FromResult(ModbusResponseCode.IllegalDataAddress);
        }

        for (var offset = 0; offset < count; offset += 8)
        {
            byte packed = 0;
            for (var bit = 0; bit < 8 && offset + bit < count; bit++)
            {
                if (_coils[firstAddress + offset + bit])
                {
                    packed |= (byte)(1 << bit);
                }
            }

            writer.Write(packed);
        }

        return Task.FromResult(ModbusResponseCode.Ok);
    }

    private Task<ModbusResponseCode> ReadRegisters(ushort firstAddress, ushort count, ModbusResponseWriter writer)
    {
        if (!IsRangeValid(firstAddress, count, _holdingRegisters.Length))
        {
            return Task.FromResult(ModbusResponseCode.IllegalDataAddress);
        }

        for (var i = 0; i < count; i++)
        {
            writer.WriteUInt16BigEndian(_holdingRegisters[firstAddress + i]);
        }

        return Task.FromResult(ModbusResponseCode.Ok);
    }

    private Task<ModbusResponseCode> WriteCoil(byte unitId, ushort address, bool value,
        CancellationToken cancellationToken)
    {
        if (address >= _coils.Length)
        {
            return Task.FromResult(ModbusResponseCode.IllegalDataAddress);
        }

        _coils[address] = value;
        return Task.FromResult(ModbusResponseCode.Ok);
    }

    private Task<ModbusResponseCode> WriteRegister(byte unitId, ushort address, ushort value,
        CancellationToken cancellationToken)
    {
        if (address >= _holdingRegisters.Length)
        {
            return Task.FromResult(ModbusResponseCode.IllegalDataAddress);
        }

        _holdingRegisters[address] = value;
        return Task.FromResult(ModbusResponseCode.Ok);
    }

    private static bool IsRangeValid(ushort firstAddress, ushort count, int capacity)
        => firstAddress + count <= capacity;

    private static void OnInvalidResponseLength(InvalidModbusResponseLengthException exception)
        => Console.Error.WriteLine(exception.Message);
}
