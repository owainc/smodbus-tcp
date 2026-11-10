using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using SmodbusTcp;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((_, services) => { services.AddSmodbusServer<SingleCoilHandler>(); })
    .UseSmodbusKestrel(1503, listen =>
    {
        listen.UseHttps(https =>
        {
            https.ServerCertificate = Tls.LoadServerCertificate();
            https.ClientCertificateMode = ClientCertificateMode.AllowCertificate;
            https.CheckCertificateRevocation = false;
        });
    });

await builder.Build().RunAsync();

internal sealed class SingleCoilHandler : ModbusFunctionHandlerBase
{
    private bool _coil;

    protected override Task<ModbusResponseCode> OnReadCoils(byte unitId, ushort firstAddress,
        ushort registerCount, ModbusResponseWriter responseWriter, CancellationToken cancellationToken = default)
    {
        if (firstAddress != 0 || registerCount != 1)
        {
            return Task.FromResult(ModbusResponseCode.IllegalDataAddress);
        }

        responseWriter.Write((byte)(_coil ? 1 : 0));
        return Task.FromResult(ModbusResponseCode.Ok);
    }

    protected override Task<ModbusResponseCode> OnWriteSingleCoil(byte unitId, ushort address, bool value,
        CancellationToken cancellationToken = default)
    {
        if (address != 0)
        {
            return Task.FromResult(ModbusResponseCode.IllegalDataAddress);
        }

        _coil = value;
        return Task.FromResult(ModbusResponseCode.Ok);
    }
}

public static class Tls
{
    public static X509Certificate2 LoadServerCertificate()
    {
        var certDirectory = Path.Combine(AppContext.BaseDirectory, "certs");
        using var pem = X509Certificate2.CreateFromPemFile(
            Path.Combine(certDirectory, "server.crt"), Path.Combine(certDirectory, "server.key"));

        using var certificate = OperatingSystem.IsWindows()
            ? X509CertificateLoader.LoadPkcs12(
                pem.Export(X509ContentType.Pkcs12), null)
            : null;

        return certificate ?? pem;
    }
}
