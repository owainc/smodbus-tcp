using System.Net;
using System.Net.Sockets;
using NModbus;

using var client = new TcpClient();
client.Connect(IPAddress.Loopback, 1502);
using var master = new ModbusFactory().CreateMaster(client);

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
