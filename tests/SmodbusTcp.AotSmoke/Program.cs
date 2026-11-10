using System.Buffers.Binary;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmodbusTcp;

using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
var token = timeout.Token;
var rawBuilder = Host.CreateApplicationBuilder();
rawBuilder.Logging.ClearProviders();
rawBuilder.Services.AddSmodbusServer<SmokeHandler>();
rawBuilder.UseSmodbusTcpListener(new IPEndPoint(IPAddress.Loopback, 0));
using (var host = rawBuilder.Build())
{
    await host.StartAsync(token);
    try
    {
        await Exercise(host.Services.GetRequiredService<ModbusTcpServer>().LocalEndpoint, null, null);
    }
    finally
    {
        await host.StopAsync(token);
    }
}

Console.WriteLine("Raw TCP passed.");

using var serverCertificate = CreateCertificate("localhost");
using var clientCertificate = CreateCertificate("smoke-client");
foreach (var mode in new[] { "TCP", "TLS", "mTLS" })
{
    var tls = mode != "TCP";
    var mutual = mode == "mTLS";
    using var host = Host.CreateDefaultBuilder()
        .ConfigureLogging(logging => logging.ClearProviders())
        .ConfigureServices(services => services.AddSmodbusServer<SmokeHandler>())
        .UseSmodbusKestrel(0, listen =>
        {
            if (tls)
            {
                listen.UseHttps(options =>
                {
                    options.ServerCertificate = serverCertificate;
                    options.ClientCertificateMode = mutual
                        ? ClientCertificateMode.RequireCertificate
                        : ClientCertificateMode.NoCertificate;
                    // Pin the ephemeral test certificate; no machine trust-store changes.
                    options.ClientCertificateValidation = (certificate, _, _) =>
                        certificate.GetCertHashString() == clientCertificate.GetCertHashString();
                });
            }
        }).Build();
    await host.StartAsync(token);
    try
    {
        var addresses = host.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses;
        var port = new Uri(addresses.Single()).Port;
        await Exercise(new IPEndPoint(IPAddress.Loopback, port),
            tls ? serverCertificate : null, mutual ? clientCertificate : null);
    }
    finally
    {
        await host.StopAsync(token);
    }

    Console.WriteLine($"Kestrel {mode} passed.");
}

async Task Exercise(IPEndPoint endpoint, X509Certificate2? server, X509Certificate2? client)
{
    using var tcp = new TcpClient();
    await tcp.ConnectAsync(endpoint, token);
    using var network = tcp.GetStream();
    using var ssl = server is null
        ? null
        : new SslStream(network, true,
            (_, certificate, _, _) => certificate?.GetCertHashString() == server.GetCertHashString());
    if (ssl is not null)
    {
        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = "localhost",
            ClientCertificates = client is null ? null : new X509CertificateCollection { client }
        }, token);
    }

    Stream stream = ssl is null ? network : ssl;
    await Exchange(stream, [5, 0, 0, 255, 0], [5, 0, 0, 255, 0]);
    await Exchange(stream, [1, 0, 0, 0, 1], [1, 1, 1]);
    await Exchange(stream, [6, 0, 0, 0x12, 0x34], [6, 0, 0, 0x12, 0x34]);
    await Exchange(stream, [3, 0, 0, 0, 1], [3, 2, 0x12, 0x34]);
    await Exchange(stream, [0x7F, 0, 0, 0, 1], [0xFF, 1]);
}

async Task Exchange(Stream stream, byte[] request, byte[] expected)
{
    static byte[] Frame(byte[] pdu)
    {
        var frame = new byte[7 + pdu.Length];
        frame[1] = 1; // Transaction ID.
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4), (ushort)(pdu.Length + 1));
        frame[6] = 1; // Unit ID.
        pdu.CopyTo(frame, 7);
        return frame;
    }

    await stream.WriteAsync(Frame(request), token);
    var expectedFrame = Frame(expected);
    var actual = new byte[expectedFrame.Length];
    await stream.ReadExactlyAsync(actual, token);
    if (!actual.AsSpan().SequenceEqual(expectedFrame))
    {
        throw new InvalidOperationException(
            $"Expected {Convert.ToHexString(expectedFrame)}, got {Convert.ToHexString(actual)}.");
    }
}

static X509Certificate2 CreateCertificate(string name)
{
    using var rsa = RSA.Create(2048);
    var request = new CertificateRequest($"CN={name}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    using var certificate =
        request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));
    return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pkcs12), null);
}

// Constructor injection deliberately exercises constructor preservation under trimming.
internal sealed class SmokeHandler(ILogger<SmokeHandler> logger) : ModbusFunctionHandlerBase
{
    private bool _coil;
    private ushort _register;

    protected override Task<ModbusResponseCode> OnWriteSingleCoil(byte unitId, ushort address, bool value,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Writing coil");
        _coil = value;
        return Task.FromResult(ModbusResponseCode.Ok);
    }

    protected override Task<ModbusResponseCode> OnReadCoils(byte unitId, ushort firstAddress, ushort registerCount,
        ModbusResponseWriter responseWriter,
        CancellationToken cancellationToken = default)
    {
        responseWriter.Write((byte)(_coil ? 1 : 0));
        return Task.FromResult(ModbusResponseCode.Ok);
    }

    protected override Task<ModbusResponseCode> OnWriteSingleRegister(byte unitId, ushort address, ushort value,
        CancellationToken cancellationToken = default)
    {
        _register = value;
        return Task.FromResult(ModbusResponseCode.Ok);
    }

    protected override Task<ModbusResponseCode> OnReadHoldingRegisters(byte unitId, ushort firstAddress,
        ushort registerCount, ModbusResponseWriter responseWriter,
        CancellationToken cancellationToken = default)
    {
        responseWriter.WriteUInt16BigEndian(_register);
        return Task.FromResult(ModbusResponseCode.Ok);
    }
}
