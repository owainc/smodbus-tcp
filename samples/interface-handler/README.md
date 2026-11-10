# Interface handler

This sample implements `IModbusFunctionHandler` directly, without deriving from
`ModbusFunctionHandlerBase`. It uses `ModbusFunctionHandlerHelpers` for request
validation, read-response validation and writer completion, coil-value validation,
multiple-write packet decoding, and sequential writes.

The in-memory device exposes 16 coils and 16 holding registers on unit ID `1`.
It implements coil and holding-register reads and writes. Discrete-input and
input-register reads use the interface defaults and return `IllegalFunction`.
Reads and writes outside the supported arrays return `IllegalDataAddress`.
The server listens on port `1505`.

From the repository root, start the server:

```sh
dotnet run --project samples/interface-handler/server
```

Stop it with Ctrl+C. The helper class shares the base class's validation behavior,
while the interface implementation supplies the device-specific read and write
callbacks.
