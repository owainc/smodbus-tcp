using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using NModbus.IO;

namespace client;

public class SslStreamResource(SslStream stream, TcpClient tcp) : IStreamResource
{
    public int InfiniteTimeout => Timeout.Infinite;

    public int ReadTimeout
    {
        get => stream.ReadTimeout;
        set => stream.ReadTimeout = value;
    }

    public int WriteTimeout
    {
        get => stream.WriteTimeout;
        set => stream.WriteTimeout = value;
    }

    public int Read(byte[] buffer, int offset, int count)
        => stream.Read(buffer, offset, count);

    public void Write(byte[] buffer, int offset, int count)
        => stream.Write(buffer, offset, count);

    public void DiscardInBuffer()
    {
    }

    public void Dispose()
    {
        stream.Dispose();
        tcp.Dispose();
    }

    public static async Task<SslStreamResource> ConnectAsync(IPEndPoint endPoint, X509Certificate2 certificate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var tcp = new TcpClient();
        SslStream? tls = null;
        try
        {
            await tcp.ConnectAsync(endPoint, timeout.Token);

            tls = new SslStream(tcp.GetStream(), false);

            tls.ReadTimeout = 10_000;
            tls.WriteTimeout = 10_000;

            await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = "localhost",
                CertificateChainPolicy = new X509ChainPolicy
                {
                    TrustMode = X509ChainTrustMode.CustomRootTrust,
                    CustomTrustStore = { certificate },
                    RevocationMode = X509RevocationMode.NoCheck
                }
            }, timeout.Token);

            return new SslStreamResource(tls, tcp);
        }
        catch
        {
            tls?.Dispose();
            tcp.Dispose();
            throw;
        }
    }
}
