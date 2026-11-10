# Minimal client/server

A single in-memory coil at address `0`, served by `AddSmodbusTcpListener`
on `127.0.0.1:1502`. The server uses the generic host without Kestrel.
The client uses NModbus and unit ID `1`; this simple server ignores unit IDs.
Requires the .NET 11 SDK.

From the repository root, start the server:

```sh
dotnet run --project samples/minimal/server
```

In another terminal, run the client:

```sh
dotnet run --project samples/minimal/client
```

The client writes and reads the coil twice, then exits:

```text
Coil 0: True
Coil 0: False
```

Stop the server with Ctrl+C. Other coil addresses return `IllegalDataAddress`;
unimplemented functions return `IllegalFunction` through `ModbusFunctionHandlerBase`.
