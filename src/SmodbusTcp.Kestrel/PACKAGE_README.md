# SmodbusTcp.Kestrel

Kestrel transport for [SmodbusTcp](https://www.nuget.org/packages/SmodbusTcp), the small Modbus TCP
server library for .NET. Use it to serve Modbus over TLS or mutual TLS, or to share a host with
HTTP endpoints.

## Getting started

Requires .NET 11 and `Microsoft.AspNetCore.App`.

```powershell
dotnet add package SmodbusTcp.Kestrel
```

This also brings in `SmodbusTcp`, which provides the handler base class.

## Usage

Write a handler by deriving from `ModbusFunctionHandlerBase`, then host it on Kestrel with TLS:

```csharp
using Microsoft.AspNetCore.Server.Kestrel.Https;
using SmodbusTcp;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((_, services) => { services.AddSmodbusServer<MyHandler>(); })
    .UseSmodbusKestrel(1503, listen =>
    {
        listen.UseHttps(https =>
        {
            https.ServerCertificate = LoadServerCertificate();
            // Require a client certificate for mutual TLS.
            https.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
        });
    });

await builder.Build().RunAsync();
```

`MyHandler` overrides the `On...` methods for the Modbus functions your device supports. See the
[SmodbusTcp readme](https://www.nuget.org/packages/SmodbusTcp) for a complete handler example.

## Additional documentation

- [Full README and handler guide](https://github.com/owainc/smodbus#readme)
- [TLS sample](https://github.com/owainc/smodbus/tree/master/samples/kestrel-tls)
- [Mutual TLS sample](https://github.com/owainc/smodbus/tree/master/samples/kestrel-mtls)

## Feedback

Report bugs and request features
at [github.com/owainc/smodbus/issues](https://github.com/owainc/smodbus/issues).
