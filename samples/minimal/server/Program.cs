using Microsoft.Extensions.Hosting;
using SmodbusTcp;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSmodbusServer<SingleCoilHandler>();
builder.UseSmodbusTcpListener(1502);

using var host = builder.Build();
await host.RunAsync();

internal sealed class SingleCoilHandler : ModbusFunctionHandlerBase
{
    private volatile bool _coil;

    protected override Task<ModbusResponseCode> OnReadCoils(byte unitId, ushort firstAddress,
        ushort registerCount, ModbusResponseWriter responseWriter, CancellationToken cancellationToken = default)
    {
        if (firstAddress != 0 || registerCount != 1)
        {
            return Task.FromResult(ModbusResponseCode.IllegalDataAddress);
        }

        // Coil 0 occupies the lowest bit of the response byte.
        responseWriter.Write((byte)(_coil ? 1 : 0));
        return Task.FromResult(ModbusResponseCode.Ok);
    }

    protected override Task<ModbusResponseCode> OnWriteSingleCoil(byte unitId, ushort address, bool value,
        CancellationToken cancellationToken = default)
    {
        if (address != 0)
        {
            return Task.FromResult(ModbusResponseCode.IllegalDataAddress);
        }

        _coil = value;
        return Task.FromResult(ModbusResponseCode.Ok);
    }
}
