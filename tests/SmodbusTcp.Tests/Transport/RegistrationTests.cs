using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SmodbusTcp.Tests;

[TestClass]
public sealed class RegistrationTests
{
    [TestMethod]
    public void RepeatedHandlerRegistration_KeepsOneSharedHandler()
    {
        var services = new ServiceCollection();
        services.AddSmodbusServer<RecordingHandler>();
        services.AddSmodbusServer<RecordingHandler>();
        Assert.ThrowsExactly<InvalidOperationException>(() => services.AddSmodbusServer<OtherHandler>());

        using var provider = services.BuildServiceProvider();
        Assert.AreSame(provider.GetRequiredService<RecordingHandler>(),
            provider.GetServices<IModbusFunctionHandler>().Single());
        Assert.IsNotNull(provider.GetServices<ModbusServer>().Single());
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task MultipleListeners_ServeTheSameHandler(bool firstKestrel, bool secondKestrel)
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddSmodbusServer<RecordingHandler>());
        AddListener(builder, firstKestrel);
        AddListener(builder, secondKestrel);

        using var host = builder.Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StartAsync(timeout.Token);
        try
        {
            var ports = host.Services.GetServices<ModbusTcpServer>().Select(s => s.LocalEndpoint.Port).ToList();
            if (firstKestrel || secondKestrel)
            {
                var addresses = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
                ports.AddRange(addresses.Addresses.Select(address => new Uri(address).Port));
            }

            Assert.AreEqual(2, ports.Distinct().Count());
            var handler = host.Services.GetRequiredService<RecordingHandler>();
            foreach (var port in ports)
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
                var stream = client.GetStream();
                await stream.WriteAsync(TestSupport.Frame(16, 0, 0, 0, 1, 2, 0x12, 0x34), timeout.Token);
                var expected = TestSupport.Frame(16, 0, 0, 0, 1);
                var response = new byte[expected.Length];
                await stream.ReadExactlyAsync(response, timeout.Token);
                CollectionAssert.AreEqual(expected, response);
            }

            Assert.AreEqual(2, handler.Calls);
        }
        finally
        {
            await host.StopAsync(timeout.Token);
        }
    }

    [TestMethod]
    public async Task HostApplicationBuilder_CanRegisterRawTcpListener()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSmodbusServer<RecordingHandler>();
        builder.UseSmodbusTcpListener(new IPEndPoint(IPAddress.Loopback, 0));

        using var host = builder.Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StartAsync(timeout.Token);
        try
        {
            var listener = host.Services.GetRequiredService<ModbusTcpServer>();
            Assert.AreNotEqual(0, listener.LocalEndpoint.Port);
        }
        finally
        {
            await host.StopAsync(timeout.Token);
        }
    }

    [TestMethod]
    public async Task KestrelEndpointHelper_PreservesExistingHttpPipeline()
    {
        var httpPort = GetAvailablePort();
        var builder = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddSmodbusServer<RecordingHandler>())
            .ConfigureWebHost(web => web
                .UseKestrel()
                .ConfigureKestrel(kestrel =>
                {
                    kestrel.Listen(IPAddress.Loopback, httpPort);
                    kestrel.UseSmodbusTcp(0);
                })
                .Configure(app => app.Run(context => context.Response.WriteAsync("http-ok"))));

        using var host = builder.Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StartAsync(timeout.Token);
        try
        {
            using var httpClient = new HttpClient();
            Assert.AreEqual("http-ok", await httpClient.GetStringAsync($"http://127.0.0.1:{httpPort}", timeout.Token));

            var addresses = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
            var modbusPort = addresses.Addresses
                .Select(address => new Uri(address).Port)
                .Single(port => port != httpPort);
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, modbusPort, timeout.Token);
            var stream = client.GetStream();
            await stream.WriteAsync(TestSupport.Frame(16, 0, 0, 0, 1, 2, 0x12, 0x34), timeout.Token);
            var expected = TestSupport.Frame(16, 0, 0, 0, 1);
            var response = new byte[expected.Length];
            await stream.ReadExactlyAsync(response, timeout.Token);
            CollectionAssert.AreEqual(expected, response);
        }
        finally
        {
            await host.StopAsync(timeout.Token);
        }
    }

    private static IHostBuilder AddListener(IHostBuilder builder, bool kestrel) =>
        kestrel ? builder.UseSmodbusKestrel(0) : builder.UseSmodbusTcpListener(new IPEndPoint(IPAddress.Loopback, 0));

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed class OtherHandler : ModbusFunctionHandlerBase
    {
    }
}
