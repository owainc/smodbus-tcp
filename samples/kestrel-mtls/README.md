# Kestrel mutual TLS client/server

Cloned from `samples/kestrel-tls`, this serves one in-memory coil at address `0`
using Modbus TCP over mutual TLS. Requires the .NET 11 SDK. Kestrel listens on
port `1504` on all interfaces; the client connects to localhost. TLS runs before
the Modbus handler, and clients must present a trusted certificate.

From the repository root, start the server:

```sh
dotnet run --project samples/kestrel-mtls/server
```

In another terminal, run the client:

```sh
dotnet run --project samples/kestrel-mtls/client
```

Expected output:

```text
Coil 0: True
Coil 0: False
```

Stop the server with Ctrl+C. The NModbus client writes and reads coil `0` using
unit ID `1`. The client uses a shared ten-second deadline for TCP connection and TLS
authentication, followed by ten-second stream read/write timeouts. These are not
an overall deadline for the client's complete run. The server ignores unit IDs.
Other coil addresses return `IllegalDataAddress`; unimplemented functions
return `IllegalFunction`.

Both peers use custom trust stores containing the other peer's bundled
self-signed public certificate. The client checks the server's validity and
`localhost` hostname and presents `client.crt` with `client.key`. The server
requires a client certificate and validates its trust, validity, and client
authentication usage. Missing, untrusted, or expired client certificates are
rejected before Modbus requests are processed. No system trust-store changes
are needed. Revocation checking is disabled for these self-signed sample certificates.

The PEM files in [`samples/certs`](../certs) are shared with the TLS sample.
The server output contains `server.crt`, `server.key`, and `client.crt`; the
client output contains `client.crt`, `client.key`, and `server.crt`. These private
keys are intentionally public development material. Use your own certificates,
keys, and appropriate trust/revocation policy for deployment.

Certificates are valid for five years
from generation. The server certificate includes `localhost` and `127.0.0.1`
subject alternative names; the client certificate has client authentication usage.
