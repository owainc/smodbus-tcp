# Samples

These samples demonstrate hosting a SmodbusTCP server and connecting to it from
a separate Modbus client. All projects target .NET 11 and require the .NET 11 SDK.

The build discovers samples in immediate subdirectories using
`client/client.csproj` and `server/server.csproj`. Portable artifacts include
both project types; native AOT artifacts include server projects only.

- [Minimal](minimal/README.md)
- [Interface handler](interface-handler/README.md)
- [Kestrel TLS](kestrel-tls/README.md)
- [Kestrel mutual TLS](kestrel-mtls/README.md)
- [Eight-channel I/O simulator](Modbus_POE_ETH_IO_8CH/README.md)
