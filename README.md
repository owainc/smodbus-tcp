# SmodbusTCP

[![Build](https://github.com/owainc/smodbus/actions/workflows/build.yml/badge.svg)](https://github.com/owainc/smodbus/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

**A small, modular Modbus TCP server library for .NET.**

Implement a handler class, register it with the generic host, and serve Modbus over
raw TCP or Kestrel (including TLS and mutual TLS). No ASP.NET Core dependency is
required unless you want Kestrel.

- [Features](#features)
- [Packages](#packages)
- [Quick start](#quick-start)
- [Writing a handler](#writing-a-handler)
- [Overriding
  `IModbusFunctionHandler` methods in a base subclass](#overriding-imodbusfunctionhandler-methods-in-a-base-subclass)
- [Implementing `IModbusFunctionHandler` directly](#implementing-imodbusfunctionhandler-directly-advanced)
- [Transports](#transports)
- [Native AOT](#native-aot)
- [Samples](#samples)
- [License](#license)

## Features

- **Minimal API** – inherit from `ModbusFunctionHandlerBase` and implement only what you need.
- **Common Modbus functions** – read/write coils and registers, including single and multiple writes.
- **Built-in validation** – request and response validation with appropriate Modbus exceptions.
- **TCP or Kestrel** – lightweight TCP listener or Kestrel with TLS/mTLS support.
- **Generic Host integration** – simple registration with dependency injection.
- **Fully async** – `Task`-based handlers with cancellation support.
- **AOT-ready** – Native AOT and trimming compatible.

## Packages

| Package              | Purpose                                                                                         |
|----------------------|-------------------------------------------------------------------------------------------------|
| `SmodbusTcp`         | Core protocol, handler base class, and raw TCP listener. No ASP.NET Core dependency.            |
| `SmodbusTcp.Kestrel` | Kestrel transport adapter (TLS, mTLS, shared HTTP host). Depends on `Microsoft.AspNetCore.App`. |

```powershell
dotnet add package SmodbusTcp
dotnet add package SmodbusTcp.Kestrel # optional
```

Both packages target **.NET 11**.

## Quick start

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
```

Any Modbus TCP client can now read and write coil 0 on port 1502. See the
[minimal sample](samples/minimal/README.md) for a matching client.

## Writing a handler

Derive from `ModbusFunctionHandlerBase` and override the protected `On...` methods you support. Methods
you do not override return an `IllegalFunction` response.

| Function                 | Code   | Override                   |
|--------------------------|--------|----------------------------|
| Read Coils               | `0x01` | `OnReadCoils`              |
| Read Discrete Inputs     | `0x02` | `OnReadDiscreteInputs`     |
| Read Holding Registers   | `0x03` | `OnReadHoldingRegisters`   |
| Read Input Registers     | `0x04` | `OnReadInputRegisters`     |
| Write Single Coil        | `0x05` | `OnWriteSingleCoil`        |
| Write Single Register    | `0x06` | `OnWriteSingleRegister`    |
| Write Multiple Coils     | `0x0F` | `OnWriteMultipleCoils`     |
| Write Multiple Registers | `0x10` | `OnWriteMultipleRegisters` |

All hooks are asynchronous, take a final `CancellationToken cancellationToken = default`,
and return a `ModbusResponseCode` (`Ok`, `IllegalDataAddress`, `IllegalDataValue`,
`SlaveDeviceFailure`, and so on). Pass the token to device I/O and check it during
long-running work.

If a device cannot write multiple values atomically, call the protected
`HandleAsSingleWrites` helpers from your multi-write overrides. They write in
order, check cancellation before each write, and return the first non-success response.
Earlier completed writes remain in effect.

Register values are raw unsigned 16-bit integers (0–65535) for both single and multiple writes.

### Concurrency

One handler instance serves every listener and connection. Requests on a single
connection run sequentially, but different connections can call the handler
concurrently, so protect shared device state. Create an explicit scope if you
need scoped dependencies.

Calling `AddSmodbusServer<THandler>()` again with the same handler type is harmless.
Registering a different handler type throws `InvalidOperationException` when host
services are configured.

### Response payloads

Read handlers append payload bytes only, with no function code or byte count:

- **Coils / discrete inputs:** exactly `ModbusPayload.GetPackedBitByteCount(count)` bytes, least-significant bit first,
  unused high bits zero.
- **Registers:** exactly `ModbusPayload.GetRegisterByteCount(count)` bytes, big-endian.

Write responses are built by the server. Non-`Ok` results discard any partial payload.

Use `ModbusResponseWriter` to write response payload bytes. Complete all writes before the handler returns. Do not
retain the writer or its spans, or write concurrently. Writing after completion throws `InvalidOperationException`.

The response writer provides BE and LE helpers for signed and unsigned 16-, 32-,
and 64-bit integers, `float`, `double`, and signed fixed-point `decimal` values.

If a handler returns `Ok` with a wrongly sized payload, the base class calls the
protected virtual `OnInvalidResponseLength(InvalidModbusResponseLengthException)` hook
and responds with `SlaveDeviceFailure` (`0x04`). The exception exposes `FunctionCode`,
`ExpectedLength`, and `ActualLength`. The default hook does nothing; override it to log:

```csharp
public class MyHandler(ILogger<MyHandler> logger) : ModbusFunctionHandlerBase
{
    protected override void OnInvalidResponseLength(InvalidModbusResponseLengthException exception)
        => logger.LogError(exception, "Invalid Modbus response payload.");
}
```

### Overriding `IModbusFunctionHandler` methods in a base subclass

For a non-standard request shape or value, a subclass can redeclare
`IModbusFunctionHandler` and explicitly implement just the corresponding interface method.
`ModbusFunctionHandlerBase` continues to provide the remaining
`IModbusFunctionHandler` methods and its standard validation; the custom method can
delegate standard cases through a protected `Handle...` method:

```csharp
class DeviceHandler : ModbusFunctionHandlerBase, IModbusFunctionHandler
{
    Task<ModbusResponseCode> IModbusFunctionHandler.WriteSingleCoil(
        byte unitId, ushort address, ushort value, CancellationToken cancellationToken)
        => value == 0x1234
            ? Task.FromResult(ModbusResponseCode.Ok) // Handle the device-specific value.
            : HandleWriteSingleCoil(unitId, address, value, cancellationToken);

    protected override Task<ModbusResponseCode> OnWriteSingleCoil(
        byte unitId, ushort address, bool value, CancellationToken cancellationToken = default)
        => Task.FromResult(ModbusResponseCode.IllegalFunction); // Implement the standard write.
}
```

The [Modbus_POE_ETH_IO_8CH sample](samples/Modbus_POE_ETH_IO_8CH/server/ModbusService.cs)
uses this pattern to intercept non-standard coil values while retaining base-class
handling for standard writes and the other `IModbusFunctionHandler` methods.

### Implementing `IModbusFunctionHandler` directly (advanced)

Implement `IModbusFunctionHandler` directly when you do not want to derive from
`ModbusFunctionHandlerBase`. Implement only the functions your device supports:
all eight interface methods default to returning `ModbusResponseCode.IllegalFunction`
without writing a payload. These defaults do not perform request validation.

The public `ModbusFunctionHandlerHelpers` methods can
share request validation, read-response validation, writer completion, packet decoding,
and sequential writes. If you bypass those helpers, your implementation owns all
validation, including payload lengths.

See the [interface-handler sample](samples/interface-handler/README.md) for a complete
direct `IModbusFunctionHandler` implementation using the shared helpers for coils
and holding registers, with the interface defaults rejecting discrete-input and
input-register reads.

## Transports

### Raw TCP

`UseSmodbusTcpListener` uses the listener in `SmodbusTcp`, with no ASP.NET Core dependency.

```csharp
// Listen on a port
builder.UseSmodbusTcpListener(502);

// Or bind to a specific address
builder.Services.AddSmodbusTcpListener(new IPEndPoint(IPAddress.Loopback, 502));

// Multiple endpoints: call once per endpoint
builder.UseSmodbusTcpListener(1502).UseSmodbusTcpListener(1503);
```

Use `GetServices<ModbusTcpServer>()` to inspect registered listeners. `ModbusTcpServer`
can also be constructed directly and controlled with `StartAsync`/`StopAsync`.

### Kestrel

Reference `SmodbusTcp.Kestrel`. For a dedicated Modbus-only generic host,
`UseSmodbusKestrel(...)` is the one-line convenience:

```csharp
Host.CreateDefaultBuilder()
    .UseSmodbusKestrel(port: 502);
```

This convenience installs an empty HTTP application pipeline. Do not use it if
the same host also serves HTTP, because it can replace or conflict with that
application pipeline. Instead, add the Modbus endpoint to your existing Kestrel
configuration:

```csharp
webBuilder.ConfigureKestrel(kestrel => kestrel.UseSmodbusTcp(port: 502));
```

The endpoint's optional `configure` callback runs before the Modbus connection handler
is added, so middleware such as `UseHttps` (TLS/mTLS) can be configured there. Raw TCP
and Kestrel listeners can run together on distinct endpoints.

For custom wiring, use `ModbusConnectionHandler` or compose `ModbusServer` with your own
`IModbusFunctionHandler`. `ModbusServer` is sealed and its `ProcessAsync` method accepts an
`IDuplexPipe` and contains the shared protocol processing; its callers own the pipes and
handle connection failures.

## Samples

| Sample                                                   | Shows                                                                           |
|----------------------------------------------------------|---------------------------------------------------------------------------------|
| [minimal](samples/minimal/README.md)                     | Raw TCP server and a client writing/reading a coil.                             |
| [interface-handler](samples/interface-handler/README.md) | Direct `IModbusFunctionHandler` implementation using the shared helpers.        |
| [kestrel-tls](samples/kestrel-tls/README.md)             | Modbus over TLS with Kestrel, with sample PEM certificates.                     |
| [kestrel-mtls](samples/kestrel-mtls/README.md)           | Mutual TLS requiring a trusted client certificate.                              |
| [Modbus_POE_ETH_IO_8CH](samples/Modbus_POE_ETH_IO_8CH)   | Eight-channel I/O simulator; base subclass intercepts non-standard coil values. |

The sample certificates are for demonstration only. Never use them in production.

## Native AOT

Both libraries are AOT and trimming compatible. To publish a consuming app as a native
executable, add `<PublishAot>true</PublishAot>` to its project and publish for the target runtime:

```powershell
dotnet publish samples/minimal/server/server.csproj -c Release -r win-x64 -p:PublishAot=true
```

This requires the .NET SDK and native compiler tools for the target platform. Your
handlers and other dependencies must also be AOT compatible; investigate trimming/AOT
warnings rather than suppressing them. See the
[native AOT smoke test](tests/README.md#native-aot-smoke-test) for verifying both
libraries, including Kestrel TLS and mTLS.

## Building and testing

```powershell
dotnet test tests/SmodbusTcp.Tests/SmodbusTcp.Tests.csproj --configuration Release
```

Requires the .NET 11 SDK.

## License

Released under the [MIT License](LICENSE).
