using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SmodbusTcp;

/// <summary>A raw TCP listener that can run directly or as a hosted service.</summary>
/// <param name="endpoint">The local address and port to bind; port zero requests an available port.</param>
/// <param name="server">The protocol server used to process connections.</param>
/// <param name="logger">The logger for connection failures.</param>
public sealed class ModbusTcpServer(
    IPEndPoint endpoint,
    ModbusServer server,
    ILogger<ModbusTcpServer> logger) : BackgroundService
{
    private readonly TcpListener _listener = new(endpoint);

    /// <summary>The bound endpoint, including the assigned port when port zero is used.</summary>
    /// <remarks>Before the server starts, and after it stops, this is the configured endpoint (port zero stays zero).</remarks>
    public IPEndPoint LocalEndpoint => (IPEndPoint)_listener.LocalEndpoint;

    /// <summary>
    /// Starts the TCP listener and begins accepting connections. This method is called by the host when the service is
    /// started.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes when the listener has started.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> is already canceled.</exception>
    /// <exception cref="SocketException">The listener cannot bind to the configured endpoint.</exception>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _listener.Start();
        return base.StartAsync(cancellationToken);
    }

    /// <summary>
    /// Executes the TCP listener loop, accepting and processing incoming client connections.
    /// </summary>
    /// <param name="stoppingToken">The token signaled when the host requests shutdown.</param>
    /// <returns>A task that completes when the listener and its active connections have stopped.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var connections = new List<Task>();
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(lifetime.Token).ConfigureAwait(false);
                connections.RemoveAll(task => task.IsCompleted);
                connections.Add(ProcessClientAsync(client, lifetime.Token));
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
        finally
        {
            _listener.Stop();
            await lifetime.CancelAsync().ConfigureAwait(false);
            await Task.WhenAll(connections).ConfigureAwait(false);
        }
    }

    private async Task ProcessClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        await using (cancellationToken.Register(client.Dispose).ConfigureAwait(false))
        {
            try
            {
                client.NoDelay = true;
                var stream = client.GetStream();
                await using (stream.ConfigureAwait(false))
                {
                    var input = PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: true));
                    var output = PipeWriter.Create(stream, new StreamPipeWriterOptions(leaveOpen: true));
                    try
                    {
                        await server.ProcessAsync(new Transport(input, output), cancellationToken)
                            .ConfigureAwait(false);
                    }
                    finally
                    {
                        await input.CompleteAsync().ConfigureAwait(false);
                        await output.CompleteAsync().ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Exception thrown from connection handler.");
            }
        }
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        base.Dispose();
        _listener.Stop();
    }

    private sealed class Transport(PipeReader input, PipeWriter output) : IDuplexPipe
    {
        public PipeReader Input => input;
        public PipeWriter Output => output;
    }
}
