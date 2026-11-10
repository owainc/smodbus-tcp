using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging.Abstractions;
using static SmodbusTcp.Tests.TestSupport;

namespace SmodbusTcp.Tests;

[TestClass]
public sealed class TransportTests
{
    [TestMethod]
    public async Task Tcp_FragmentedAndPipelinedRequests_AndShutdownWithIdleClient()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var server = new ModbusTcpServer(new IPEndPoint(IPAddress.Loopback, 0),
            new ModbusServer(new RecordingHandler()), NullLogger<ModbusTcpServer>.Instance);
        await server.StartAsync(timeout.Token);
        using var idle = new TcpClient();
        using var client = new TcpClient();
        await idle.ConnectAsync(server.LocalEndpoint, timeout.Token);
        await client.ConnectAsync(server.LocalEndpoint, timeout.Token);
        var stream = client.GetStream();
        var frame = Frame(6, 0, 20, 0x12, 0x34);
        var requests = frame.Concat(frame).ToArray();
        await stream.WriteAsync(requests.AsMemory(0, 3), timeout.Token);
        await stream.WriteAsync(requests.AsMemory(3), timeout.Token);
        client.Client.Shutdown(SocketShutdown.Send);
        var response = new byte[requests.Length];
        await stream.ReadExactlyAsync(response, timeout.Token);
        CollectionAssert.AreEqual(requests, response);
        Assert.AreEqual(0, await stream.ReadAsync(new byte[1], timeout.Token));
        await server.StopAsync(timeout.Token);
        await server.ExecuteTask!.WaitAsync(timeout.Token);
        Assert.AreEqual(0, await idle.GetStream().ReadAsync(new byte[1], timeout.Token));
    }

    [TestMethod]
    public async Task Tcp_ShutdownCancelsActiveHandler()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var handler = new WaitingHandler();
        using var server = new ModbusTcpServer(new IPEndPoint(IPAddress.Loopback, 0),
            new ModbusServer(handler), NullLogger<ModbusTcpServer>.Instance);
        await server.StartAsync(timeout.Token);
        using var client = new TcpClient();
        await client.ConnectAsync(server.LocalEndpoint, timeout.Token);
        await client.GetStream().WriteAsync(Frame(3, 0, 20, 0, 1), timeout.Token);
        var handlerToken = await handler.Started.Task.WaitAsync(timeout.Token);

        await server.StopAsync(timeout.Token);
        await server.ExecuteTask!.WaitAsync(timeout.Token);
        Assert.IsTrue(handlerToken.IsCancellationRequested);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Tcp_InvalidHeaderOrHandlerException_ClosesOnlyOffendingConnection(bool handlerThrows)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var server = new ModbusTcpServer(new IPEndPoint(IPAddress.Loopback, 0),
            new ModbusServer(new ThrowingHandler()), NullLogger<ModbusTcpServer>.Instance);
        await server.StartAsync(timeout.Token);
        try
        {
            using var bad = new TcpClient();
            await bad.ConnectAsync(server.LocalEndpoint, timeout.Token);
            var invalid = Frame(6, 0, 20, 0, 1);
            if (handlerThrows)
            {
                invalid[8] = 0xFF;
            }
            else
            {
                invalid[2] = 1;
            }

            await bad.GetStream().WriteAsync(invalid, timeout.Token);
            Assert.AreEqual(0, await bad.GetStream().ReadAsync(new byte[1], timeout.Token));
            using var good = new TcpClient();
            await good.ConnectAsync(server.LocalEndpoint, timeout.Token);
            var request = Frame(6, 0, 20, 0, 1);
            await good.GetStream().WriteAsync(request, timeout.Token);
            var response = new byte[request.Length];
            await good.GetStream().ReadExactlyAsync(response, timeout.Token);
            CollectionAssert.AreEqual(request, response);
        }
        finally
        {
            await server.StopAsync(timeout.Token);
        }
    }

    [TestMethod]
    public async Task KestrelAdapter_ProcessesRequests()
    {
        var input = new Pipe();
        var output = new Pipe();
        await using var context = new DefaultConnectionContext
            { Transport = new Duplex(input.Reader, output.Writer) };
        var adapter = new ModbusConnectionHandler(new ModbusServer(new RecordingHandler()),
            NullLogger<ModbusConnectionHandler>.Instance);
        var request = Frame(6, 0, 20, 0, 1);
        await input.Writer.WriteAsync(request);
        await input.Writer.CompleteAsync();
        await adapter.OnConnectedAsync(context).WaitAsync(TimeSpan.FromSeconds(5));
        await output.Writer.CompleteAsync();
        var response = new byte[request.Length];
        await output.Reader.AsStream().ReadExactlyAsync(response);
        CollectionAssert.AreEqual(request, response);
        await input.Reader.CompleteAsync();
        await output.Reader.CompleteAsync();
    }

    private sealed class WaitingHandler : ModbusFunctionHandlerBase
    {
        public TaskCompletionSource<CancellationToken> Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<ModbusResponseCode> OnReadHoldingRegisters(byte unitId, ushort address,
            ushort count, ModbusResponseWriter writer, CancellationToken cancellationToken = default)
        {
            Started.SetResult(cancellationToken);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return ModbusResponseCode.Ok;
        }
    }

    private sealed class ThrowingHandler : ModbusFunctionHandlerBase
    {
        protected override Task<ModbusResponseCode> OnWriteSingleRegister(byte unit, ushort address, ushort value,
            CancellationToken cancellationToken = default)
            => address > 255
                ? Task.FromException<ModbusResponseCode>(new InvalidOperationException("Device failure"))
                : Task.FromResult(ModbusResponseCode.Ok);
    }
}
