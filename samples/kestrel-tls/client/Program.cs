using System.Net;
using System.Security.Cryptography.X509Certificates;
using client;
using NModbus;
using NModbus.Device;

using var certificate = X509Certificate2.CreateFromPem(
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "certs", "server.crt")));

var factory = new ModbusFactory();
using var master =
    new ModbusIpMaster(
        factory.CreateIpTransport(
            await SslStreamResource.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 1503), certificate)));

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
