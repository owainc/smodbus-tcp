# SmodbusTcp

A small, modular Modbus TCP server library for .NET. Derive from a handler base class, override
only the Modbus functions your device supports, and serve them over a dependency-free raw TCP
listener. Request validation and Modbus exception responses are handled for you.

## Getting started

Requires .NET 11.

```powershell
dotnet add package SmodbusTcp
```

For TLS, mutual TLS, or sharing a host with HTTP endpoints, also add
[SmodbusTcp.Kestrel](https://www.nuget.org/packages/SmodbusTcp.Kestrel).

## Usage

```csharp
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
```

Supports reading coils, discrete inputs, holding registers and input registers, and writing single
or multiple coils and registers. Native AOT and trimming compatible.

## Additional documentation

- [Full README and handler guide](https://github.com/owainc/smodbus#readme)
- [Samples](https://github.com/owainc/smodbus/tree/master/samples)

## Feedback

Report bugs and request features
at [github.com/owainc/smodbus/issues](https://github.com/owainc/smodbus/issues).
