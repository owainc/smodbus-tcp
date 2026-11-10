using System.Net;
using System.Security.Cryptography.X509Certificates;
using client;
using NModbus;
using NModbus.Device;

using var certificate = X509Certificate2.CreateFromPem(
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "certs", "server.crt")));

using var pem = X509Certificate2.CreateFromPemFile(
    Path.Combine(AppContext.BaseDirectory, "certs", "client.crt"),
    Path.Combine(AppContext.BaseDirectory, "certs", "client.key"));
using var clientCertificate = OperatingSystem.IsWindows()
    ? X509CertificateLoader.LoadPkcs12(pem.Export(X509ContentType.Pkcs12), null)
    : new X509Certificate2(pem);

var factory = new ModbusFactory();
using var master =
    new ModbusIpMaster(
        factory.CreateIpTransport(await SslStreamResource.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 1504),
            certificate,
            clientCertificate)));

foreach (var value in new[] { true, false })
{
    master.WriteSingleCoil(1, 0, value);

    var actual = master.ReadCoils(1, 0, 1)[0];

    if (actual != value)
    {
        throw new IOException("Coil value mismatch.");
    }

    Console.WriteLine($"Coil 0: {actual}");
}
