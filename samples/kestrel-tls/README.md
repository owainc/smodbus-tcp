# Kestrel TLS client/server

Based on the RawListener sample (`samples/minimal`), this serves one in-memory
coil at address `0` on `127.0.0.1:1503` using Kestrel and TLS. Requires the .NET 11 SDK.
The server registers TLS before the Modbus connection handler. This is Modbus TCP
over TLS, not HTTP. Unit ID `1` is used by the client; the server ignores unit IDs.

From the repository root, start the server:

```sh
dotnet run --project samples/kestrel-tls/server
```

In another terminal, run the client:

```sh
dotnet run --project samples/kestrel-tls/client
```

Expected output:

```text
Coil 0: True
Coil 0: False
```

Stop the server with Ctrl+C. Other coil addresses return `IllegalDataAddress`;
unimplemented functions return `IllegalFunction`.

The client uses `TcpClient` and `SslStream` to send the two Modbus functions,
checks the response framing and coil values, and uses a shared ten-second deadline
for TCP connection and TLS authentication, followed by ten-second stream read/write
timeouts. These are not an overall deadline for the client's complete run.
It trusts only the bundled self-signed certificate through a custom trust store,
while retaining certificate validity and hostname checks for `localhost`.
No system certificate installation or client certificate is required.

The PEM files in [`samples/certs`](../certs) are shared with the mTLS sample.
`server.crt` and `server.key` are copied into the server's
output directory; only the public certificate is copied into the client's output.
The private key is intentionally public sample material: use your own certificate
and private key for deployment. The certificate is valid for five years from generation
and includes `localhost` and `127.0.0.1` subject alternative names.
