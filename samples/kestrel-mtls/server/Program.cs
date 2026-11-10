using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using SmodbusTcp;

using var serverCertificate = Tls.LoadServerCertificate();
using var trustedClient = X509Certificate2.CreateFromPem(
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "certs", "client.crt")));

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((_, services) => { services.AddSmodbusServer<SingleCoilHandler>(); })
    .UseSmodbusKestrel(1504, listen =>
    {
        listen.UseHttps(https =>
        {
            https.ServerCertificate = serverCertificate;
            https.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
            https.CheckCertificateRevocation = false;
            https.ClientCertificateValidation = (certificate, _, _) =>
            {
                using var chain = new X509Chain();
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.Add(trustedClient);
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.2"));
                return chain.Build(certificate);
            };
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
        var pem = X509Certificate2.CreateFromPemFile(
            Path.Combine(certDirectory, "server.crt"), Path.Combine(certDirectory, "server.key"));

        if (!OperatingSystem.IsWindows())
        {
            return pem;
        }

        using (pem)
        {
            return X509CertificateLoader.LoadPkcs12(pem.Export(X509ContentType.Pkcs12), null);
        }
    }
}
