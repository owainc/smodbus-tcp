# Library tests

Run the release suite from the repository root:

```powershell
dotnet test tests/SmodbusTcp.Tests/SmodbusTcp.Tests.csproj -c Release
```

- `Protocol`: packet boundaries, segmented buffers, byte order, payload ownership, and MBAP headers.
- `Handlers`: standard decoding and sequential single-write fallback, including partial failure and address overflow.
- `Server`: exact wire replies, validation before dispatch, custom raw handlers, framing, cancellation, and recovery after rejected requests.
- `Transport`: loopback TCP lifecycle and connection isolation, plus the Kestrel connection adapter.
- `Support`: shared wire fixtures and recording handlers.

Tests use literal wire expectations and boundary cases based on the [Modbus application protocol specification](https://modbus.org/docs/Modbus_Application_Protocol_V1_1b3.pdf). Fragmentation tests synchronize on the reader examining incomplete input; they do not depend on sleeps. Network tests use ephemeral loopback ports and bounded waits and can run in parallel.

Keep tests focused on observable library contracts and regressions that could corrupt data, dispatch malformed writes, or break connection handling. Samples, framework behavior, trivial constants, and exhaustive combinations of equivalent inputs do not belong here. Raw interface overrides intentionally support device-specific semantics; standard value validation belongs to the base handler. The server still validates packet framing before dispatch.

This suite does not certify device behavior or provide TLS/mTLS integration coverage; the Kestrel test covers the protocol adapter only.

## Native AOT smoke test

`SmodbusTcp.AotSmoke` is a separate executable that roots both library assemblies
for whole-library analysis and treats native compilation/linker warnings as
errors. Publish and run the native binary (a normal build or `dotnet test` does
not perform this check):

```powershell
dotnet publish tests/SmodbusTcp.AotSmoke/SmodbusTcp.AotSmoke.csproj -c Release -r win-x64 -o artifacts/aot-smoke
./artifacts/aot-smoke/SmodbusTcp.AotSmoke.exe
```

On Linux, publish on a Linux machine with `-r linux-x64` and run
`./artifacts/aot-smoke/SmodbusTcp.AotSmoke`. Native publishing requires the
platform's native compiler/linker prerequisites. CI only runs the smoke test on the
CLR with `dotnet run` in the `build-packages` job (no native compilation), so it
covers the wire exchanges but not AOT/trim warnings. The Windows x64 native path has been
verified locally with SDK `11.0.100`; other targets need their own run.

The executable checks constructor injection, exact coil/register write and read
replies, and an unsupported-function reply over raw TCP and Kestrel TCP, TLS and
mTLS. It uses ephemeral ports and generated, pinned test certificates without
modifying the machine trust store, and has a 60-second overall timeout. The
Kestrel helper binds an ephemeral port on all interfaces; clients connect through
loopback. Any failed exchange exits nonzero. This is AOT integration coverage,
not an exhaustive certificate-validation test suite.
