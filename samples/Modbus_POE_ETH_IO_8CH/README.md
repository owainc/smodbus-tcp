# Eight-channel I/O simulator

This sample simulates a Waveshare Modbus POE ETH IO 8CH device in memory; no
physical hardware is required. The server listens on port `502`, and the client
connects to loopback. Both projects require the .NET 11 SDK.

Start the server with its terminal UI:

```sh
dotnet run --project samples/Modbus_POE_ETH_IO_8CH/server -- ui
```

Omit `-- ui` for normal hosting, or use `-- log` to print successful device
commands. The `ui` and `log` modes cannot be combined. Stop the server with
Ctrl+C.

In another terminal, start the client UI:

```sh
dotnet run --project samples/Modbus_POE_ETH_IO_8CH/client -- ui
```

Or send individual commands:

```sh
dotnet run --project samples/Modbus_POE_ETH_IO_8CH/client -- output 1 on
dotnet run --project samples/Modbus_POE_ETH_IO_8CH/client -- output all off
dotnet run --project samples/Modbus_POE_ETH_IO_8CH/client -- output 1 flash on 1000
```

Commands use channel numbers `1` through `8`. The server UI can change
simulated inputs; the client can read inputs but cannot set them. The simulator
demonstrates coil and discrete-input reads, holding registers, single and
multiple coil writes, and register writes. Outputs support manual, linkage,
toggle, edge-trigger, and timed flash behavior. Both UIs display device state
and command help; press Esc to leave a UI. Running the client without arguments
prints usage.

See the [device protocol reference](docs.html), the
[simulator implementation](server/ModbusService.cs), and the
[samples guide](../README.md) for shared build information.
