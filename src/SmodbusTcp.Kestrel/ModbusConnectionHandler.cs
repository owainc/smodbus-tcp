using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace SmodbusTcp;

/// <summary>Adapts Kestrel connections to the Modbus protocol server.</summary>
/// <param name="server">The protocol server used to process connections.</param>
/// <param name="logger">The logger for connection failures.</param>
public sealed class ModbusConnectionHandler(ModbusServer server, ILogger<ModbusConnectionHandler> logger)
    : ConnectionHandler
{
    /// <summary>Processes a Modbus connection.</summary>
    /// <remarks>
    /// Connection failures are not rethrown; they are logged at Debug level. An invalid Modbus header
    /// also aborts the connection. Other failures leave the connection to Kestrel to close.
    /// </remarks>
    /// <param name="connection">The Kestrel connection whose transport carries Modbus frames.</param>
    /// <returns>A task that completes when connection processing ends.</returns>
    public override async Task OnConnectedAsync(ConnectionContext connection)
    {
        try
        {
            await server.ProcessAsync(connection.Transport, connection.ConnectionClosed).ConfigureAwait(false);
        }
        catch (InvalidDataException ex)
        {
            connection.Abort();
            logger.LogDebug(ex, "Exception thrown from connection handler.");
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Exception thrown from connection handler.");
        }
    }
}
